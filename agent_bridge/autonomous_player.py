#!/usr/bin/env python3
"""
Autonomous Player Bot for Deep Space Pioneers.
Connects via External Agent Bridge and plays through the game automatically.

Usage:
    python3 autonomous_player.py [--delay 0.5]
"""

import json
import os
import random
import sys
import time
from pathlib import Path

BRIDGE_DIR = Path(__file__).resolve().parent
STATE_FILE = BRIDGE_DIR / "state.json"
ACTION_FILE = BRIDGE_DIR / "action.json"
RESULT_FILE = BRIDGE_DIR / "result.json"
TARGET_PLAYER_INDEX = None
TARGET_PLAYER_NAME = None
BOT_PREFIX = ""

_orig_print = print
def print(*args, **kwargs):
    if BOT_PREFIX and args:
        s = str(args[0])
        nl_prefix = ""
        while s.startswith("\n"):
            nl_prefix += "\n"
            s = s[1:]
        first = nl_prefix + BOT_PREFIX + s
        _orig_print(first, *args[1:], **kwargs)
    else:
        _orig_print(*args, **kwargs)

_CANDIDATE_DIRS = []

def set_bridge_dir(new_dir: Path):
    global BRIDGE_DIR, STATE_FILE, ACTION_FILE, RESULT_FILE
    BRIDGE_DIR = new_dir.resolve()
    STATE_FILE = BRIDGE_DIR / "state.json"
    ACTION_FILE = BRIDGE_DIR / "action.json"
    RESULT_FILE = BRIDGE_DIR / "result.json"

def resolve_bridge_dir(raw_path: str) -> Path:
    global _CANDIDATE_DIRS
    p = Path(raw_path)
    if p.is_absolute():
        _CANDIDATE_DIRS = [p.resolve()]
        return p.resolve()

    workspace_root = Path(__file__).resolve().parent.parent
    cwd_cand = (Path.cwd() / p).resolve()
    ws_cand = (workspace_root / p).resolve()
    linux_build_cand = (workspace_root / "Builds" / "Linux_Client" / p).resolve()
    win_build_cand = (workspace_root / "Builds" / "Windows_Client" / p).resolve()

    seen = set()
    _CANDIDATE_DIRS = []
    for cand in [cwd_cand, ws_cand, linux_build_cand, win_build_cand]:
        if cand not in seen:
            seen.add(cand)
            _CANDIDATE_DIRS.append(cand)

    # Prefer candidate with the freshest state.json
    candidates_with_state = [c for c in _CANDIDATE_DIRS if (c / "state.json").exists()]
    if candidates_with_state:
        candidates_with_state.sort(key=lambda c: (c / "state.json").stat().st_mtime, reverse=True)
        return candidates_with_state[0]

    for cand in _CANDIDATE_DIRS:
        if cand.exists():
            return cand

    return ws_cand

def read_state(timeout=5.0):
    global BRIDGE_DIR
    start = time.time()
    while time.time() - start < timeout:
        if STATE_FILE.exists():
            try:
                with open(STATE_FILE, "r", encoding="utf-8") as f:
                    return json.load(f)
            except (json.JSONDecodeError, OSError):
                time.sleep(0.05)
        else:
            # Check candidate directories in case state.json was written to Builds/... or Workspace
            if _CANDIDATE_DIRS:
                for cand in _CANDIDATE_DIRS:
                    cand_state = cand / "state.json"
                    if cand != BRIDGE_DIR and cand_state.exists():
                        print(f"[Bot] Discovered active bridge directory: {cand}")
                        set_bridge_dir(cand)
                        break
            time.sleep(0.05)
    return None

def send_action(action_dict, timeout=8.0):
    if action_dict.get("action") == "MoveShip":
        timeout = max(timeout, 15.0)
    action_id = action_dict.get("actionId", f"bot-{int(time.time()*1000)}")
    action_dict["actionId"] = action_id

    if RESULT_FILE.exists():
        try:
            RESULT_FILE.unlink()
        except OSError:
            pass

    tmp_file = BRIDGE_DIR / f"action_{action_id}.tmp"
    with open(tmp_file, "w", encoding="utf-8") as f:
        json.dump(action_dict, f, indent=2)
    tmp_file.replace(ACTION_FILE)

    start = time.time()
    while time.time() - start < timeout:
        if RESULT_FILE.exists():
            try:
                with open(RESULT_FILE, "r", encoding="utf-8") as f:
                    res = json.load(f)
                if res.get("actionId") == action_id:
                    return res
            except (json.JSONDecodeError, OSError):
                time.sleep(0.05)
        time.sleep(0.05)

    return {"actionId": action_id, "success": False, "error": f"Timeout after {timeout}s"}

def get_free_home_colony_sites(state):
    sites = state.get("board", {}).get("sites", [])
    home = [s for s in sites if s.get("isColonyPoint") and s.get("isDiscovered") and not s.get("isOccupied")]
    home.sort(key=lambda s: s.get("productionProbability", 0), reverse=True)
    return home

def get_free_colony_sites(state):
    sites = state.get("board", {}).get("sites", [])
    ready = [s for s in sites if s.get("isColonyPoint") and not s.get("isOccupied") and (not s.get("isBlockedByHazard") or s.get("canResolveHazard"))]
    if ready:
        return ready
    # If all peaceful sites are taken, allow ships to approach prospective hazard sites we are arming for
    return [s for s in sites if s.get("isColonyPoint") and not s.get("isOccupied")]

def get_free_trade_sites(state):
    sites = state.get("board", {}).get("sites", [])
    return [s for s in sites if s.get("isTradePoint") and not s.get("isOccupied")]

# ==============================================================================
# UNIFIED STRATEGIC SCORER
# ==============================================================================

class PioneerScorer:
    """
    Unified multi-dimensional evaluation engine for Deep Space Pioneers.
    Evaluates:
      1. Colony Targets (Pips, Resource Diversity, Hazards for +2 VP, Distance)
      2. Trade Outposts (First Contact Cards, Standee Majority for +2 VP, Distance)
      3. Build Options & Supply Trading (VP/turn efficiency, Enablers, Win-Rush)
    """

    @staticmethod
    def get_existing_player_resources_produced(current_player, state):
        """Returns the set of resource types already produced by the player's existing colonies."""
        produced = set()
        if not current_player:
            return produced
        my_colonies = {(c["q"], c["r"]) for c in current_player.get("colonies", [])}
        for site in state.get("board", {}).get("sites", []):
            if (site.get("q"), site.get("r")) in my_colonies:
                for res in site.get("adjacentResources", []):
                    if res:
                        produced.add(res.lower())
        return produced

    @staticmethod
    def score_colony_target(site, ship_q, ship_r, current_player, state):
        """Scores a colony site based on immediate VP, production pips, resource diversity, and distance."""
        dist = ((site["q"] - ship_q)**2 + (site["r"] - ship_r)**2) ** 0.5
        pips = site.get("productionProbability", 0)
        score = 0.0

        # Win-Rush multiplier
        my_vp = current_player.get("victoryPoints", 0) if current_player else 0
        is_win_rush = my_vp >= 13

        # Hazard resolution: Ice Planet or Marauder Base awards +1 Colony AND +1 Special Token = +2 VP!
        if site.get("hazardKind") in ("IcePlanet", "MarauderBase"):
            if site.get("canResolveHazard"):
                score += 220.0 if is_win_rush else 170.0
            elif site.get("isBlockedByHazard"):
                all_sites = state.get("board", {}).get("sites", [])
                has_peaceful = any(s.get("isColonyPoint") and not s.get("isOccupied") and not s.get("isBlockedByHazard") for s in all_sites)
                if has_peaceful:
                    return -999999.0
                # When all peaceful sites are occupied, allow approaching prospective hazard targets while building upgrades
                score += 50.0

        # Production yield: 6 points per pip
        score += pips * 6.0

        # Resource diversity bonus: reward expanding into resource types we do not yet harvest
        existing_res = PioneerScorer.get_existing_player_resources_produced(current_player, state)
        for res in site.get("adjacentResources", []):
            if res and res.lower() not in existing_res:
                score += 15.0

        # Travel distance penalty (lower speed = greater penalty)
        flagship = (current_player.get("flagship") or current_player.get("mothership", {})) if current_player else {}
        boosters = flagship.get("boosters", 0)
        effective_speed = 3.0 + boosters * 0.5
        travel_turns = dist / max(1.0, effective_speed)
        score -= travel_turns * 8.0

        return score

    @staticmethod
    def score_trade_target(site, ship_q, ship_r, current_player, state):
        """Scores a trade outpost based on folk first contact, majority race standees (+2 VP), and distance."""
        dist = ((site["q"] - ship_q)**2 + (site["r"] - ship_r)**2) ** 0.5
        score = 70.0  # Base value of establishing a trade outpost

        folk = site.get("folkType")
        players = state.get("players", [])
        my_idx = current_player.get("playerIndex", 0) if current_player else 0
        my_vp = current_player.get("victoryPoints", 0) if current_player else 0
        is_win_rush = my_vp >= 13

        # Check existing stations at this folk (Trader, Scientist, Diplomat, Cultivator)
        my_stations_at_folk = 0
        max_opponent_stations = 0
        for p in players:
            p_stations = p.get("tradeStations", [])
            if folk:
                matching = [st for st in p_stations if not st.get("folkType") or st.get("folkType") == folk]
                p_cnt = len(matching)
            else:
                p_cnt = len(p_stations)
            if p.get("playerIndex") == my_idx:
                my_stations_at_folk = p_cnt
            else:
                if p_cnt > max_opponent_stations:
                    max_opponent_stations = p_cnt

        # First contact bonus (grants a valuable Friendship card)
        if my_stations_at_folk == 0:
            score += 50.0

        # Folk majority bonus (controlling majority awards +2 VP Standee)
        if my_stations_at_folk + 1 >= max_opponent_stations:
            score += 130.0 if is_win_rush else 70.0

        # Travel distance penalty
        flagship = (current_player.get("flagship") or current_player.get("mothership", {})) if current_player else {}
        boosters = flagship.get("boosters", 0)
        built_pods = flagship.get("freightPods", 0)
        effective_speed = 3.0 + boosters * 0.5
        travel_turns = dist / max(1.0, effective_speed)
        score -= travel_turns * 8.0

        # Freight pod requirement: establishing a trade station requires FlagshipCargoModuleCount > currentStations!
        station_count = site.get("tradeStationCount", 0)
        if built_pods <= station_count:
            # Cannot legally dock here without more freight pods!
            score -= 500.0

        # Prefer outposts where we do not already have a station
        my_trade_stations = current_player.get("tradeStations", []) if current_player else []
        if any(ts.get("q") == site.get("q") and ts.get("r") == site.get("r") for ts in my_trade_stations):
            score -= 200.0

        return score

    @staticmethod
    def evaluate_build_goals(current_player, state, legal):
        """
        Evaluates and ranks all potential build goals by expected VP and strategic impact.
        Returns a sorted list of candidate goals.
        """
        if not current_player:
            return []

        res = current_player.get("resources", {})
        colonies = current_player.get("colonies", [])
        regular_colonies = [c for c in colonies if c.get("type") == "Colony"]
        my_vp = current_player.get("victoryPoints", 0)
        is_win_rush = my_vp >= 13
        half_medals = current_player.get("halfMedals", 0)

        has_spaceport_supply = current_player.get("availableSpaceports", 0) > 0
        has_ship_supply = current_player.get("availableShips", 0) > 0
        has_colony_supply = current_player.get("availableColonies", 0) > 0
        has_trade_supply = current_player.get("availableTradeStations", 0) > 0

        # Robust flagship upgrade capacity tracking
        flagship = current_player.get("flagship") or current_player.get("mothership", {})
        built_boosters = flagship.get("boosters", 0)
        built_cannons = flagship.get("cannons", 0)
        built_pods = flagship.get("freightPods", 0)

        avail_boosters = current_player.get("availableBoosters", max(0, 6 - built_boosters))
        avail_cannons = current_player.get("availableCannons", max(0, 6 - built_cannons))
        avail_pods = current_player.get("availableFreightPods", max(0, 5 - built_pods))

        all_sites = state.get("board", {}).get("sites", [])
        my_units = current_player.get("units", [])
        near_marauder = any(
            s.get("hazardKind") == "MarauderBase" and not s.get("canResolveHazard")
            and any(abs(s.get("q", 99) - u.get("q", 0)) + abs(s.get("r", 99) - u.get("r", 0)) <= 8 for u in my_units if u.get("shipType") == "ColonyShip")
            for s in all_sites
        )
        near_ice = any(
            s.get("hazardKind") == "IcePlanet" and not s.get("canResolveHazard")
            and any(abs(s.get("q", 99) - u.get("q", 0)) + abs(s.get("r", 99) - u.get("r", 0)) <= 8 for u in my_units if u.get("shipType") == "ColonyShip")
            for s in all_sites
        )

        goals = []

        # 1. Buy Fame (Gekaufter Ruhm)
        if "BuyFame" in legal:
            score = 320.0 if (half_medals % 2 == 1 or is_win_rush) else 170.0
            can_afford = res.get("tradeGoods", 0) >= 1
            goals.append({
                "name": "BuyFame",
                "action": "BuyFame",
                "args": {},
                "cost": {"tradeGoods": 1},
                "score": score,
                "can_afford": can_afford,
                "is_legal": True
            })

        # 2. Build Spaceport (+1 VP and forward staging dock)
        if has_spaceport_supply and regular_colonies:
            best_colony = min(regular_colonies, key=lambda c: c["q"])
            q_arg = {"q": best_colony["q"], "r": best_colony["r"]}
            cost = {"carbon": 3, "food": 2}
            score = 350.0 if is_win_rush else 190.0
            can_afford = all(res.get(r, 0) >= cost[r] for r in cost)
            goals.append({
                "name": f"Spaceport at ({best_colony['q']}, {best_colony['r']})",
                "action": "BuildSpaceport",
                "args": q_arg,
                "cost": cost,
                "score": score,
                "can_afford": can_afford,
                "is_legal": "BuildSpaceport" in legal
            })

        # 3. Cannon (Unlocks Marauder Base combat for +2 VP!)
        if avail_cannons > 0:
            cost = {"carbon": 2}
            if near_marauder:
                score = 240.0 if is_win_rush else 190.0
            elif built_cannons < 2:
                score = 80.0
            elif built_cannons < 3:
                score = 45.0
            else:
                score = 25.0
            can_afford = res.get("carbon", 0) >= 2
            goals.append({
                "name": "Cannon (Combat & Marauder Base defeat)",
                "action": "BuildUpgrade",
                "args": {"upgradeType": "Cannon"},
                "cost": cost,
                "score": score,
                "can_afford": can_afford,
                "is_legal": "BuildUpgrade" in legal
            })

        # 4. Freight Pod (MANDATORY for trade stations + unlocks Ice Planet terraforming for +2 VP!)
        if avail_pods > 0:
            cost = {"ore": 2}
            active_trade_ships = sum(1 for u in my_units if u.get("shipType") == "TradeShip")
            if near_ice:
                score = 240.0 if is_win_rush else 190.0
            elif active_trade_ships > 0 and built_pods == 0:
                # Active trade ship has 0 freight pods and cannot dock anywhere! Critical emergency!
                score = 250.0
            elif built_pods == 0 and has_trade_supply:
                # 0 freight pods blocks all trade stations! Crucial unlock!
                score = 225.0
            elif built_pods == 1 and has_trade_supply and any(s.get("tradeStationCount", 0) >= 1 for s in get_free_trade_sites(state)):
                # If open outposts already have 1 station, 2nd pod is needed to dock there
                score = 165.0
            elif built_pods < 2:
                score = 85.0
            elif built_pods < 3:
                score = 45.0
            else:
                score = 20.0
            can_afford = res.get("ore", 0) >= 2
            goals.append({
                "name": "FreightPod (Trade Stations & Ice Planet)",
                "action": "BuildUpgrade",
                "args": {"upgradeType": "FreightPod"},
                "cost": cost,
                "score": score,
                "can_afford": can_afford,
                "is_legal": "BuildUpgrade" in legal
            })

        # 5. Booster (Increases flagship speed - essential to match AI velocity!)
        if avail_boosters > 0 and not is_win_rush:
            cost = {"fuel": 2}
            # Heavy priority in early/mid game if bot has fewer than 4 boosters
            if built_boosters < 3:
                score = 160.0 - built_boosters * 20.0
            elif built_boosters < 6:
                score = 75.0
            else:
                score = 30.0
            can_afford = res.get("fuel", 0) >= 2
            goals.append({
                "name": f"Booster #{built_boosters + 1}",
                "action": "BuildUpgrade",
                "args": {"upgradeType": "Booster"},
                "cost": cost,
                "score": score,
                "can_afford": can_afford,
                "is_legal": "BuildUpgrade" in legal
            })

        # 6. Colony Ship
        if has_ship_supply and has_colony_supply:
            cost = {"ore": 1, "fuel": 1, "carbon": 1, "food": 1}
            free_colonies = get_free_colony_sites(state)
            best_target_score = max([PioneerScorer.score_colony_target(s, 0, 0, current_player, state) for s in free_colonies], default=0.0)
            score = 110.0 + max(0.0, best_target_score * 0.4)
            if is_win_rush:
                score += 60.0
            can_afford = all(res.get(r, 0) >= cost[r] for r in cost)
            goals.append({
                "name": "ColonyShip",
                "action": "BuildShip",
                "args": {"shipType": "ColonyShip"},
                "cost": cost,
                "score": score,
                "can_afford": can_afford,
                "is_legal": "BuildShip" in legal
            })

        # 7. Trade Ship
        if has_ship_supply and has_trade_supply:
            cost = {"ore": 1, "fuel": 1, "tradeGoods": 2}
            free_trade = get_free_trade_sites(state)
            if free_trade:
                best_trade_score = max([PioneerScorer.score_trade_target(s, 0, 0, current_player, state) for s in free_trade], default=0.0)
                active_trade_ships = sum(1 for u in my_units if u.get("shipType") == "TradeShip")
                if active_trade_ships == 0:
                    base_trade_score = 220.0
                    if built_pods == 0:
                        # Slightly dampen trade ship if no pods yet so FreightPod is preferred or built concurrently
                        base_trade_score = 200.0
                    score = base_trade_score + max(0.0, best_trade_score * 0.5)
                elif active_trade_ships == 1:
                    score = 140.0 + max(0.0, best_trade_score * 0.4)
                else:
                    score = 60.0
                can_afford = all(res.get(r, 0) >= cost[r] for r in cost)
                goals.append({
                    "name": "TradeShip",
                    "action": "BuildShip",
                    "args": {"shipType": "TradeShip"},
                    "cost": cost,
                    "score": score,
                    "can_afford": can_afford,
                    "is_legal": "BuildShip" in legal
                })

        # 8. Deploy Captured Trade Ship (Free!)
        if "DeployCapturedShip" in legal:
            goals.append({
                "name": "DeployCapturedShip",
                "action": "DeployCapturedShip",
                "args": {},
                "cost": {},
                "score": 280.0,
                "can_afford": True,
                "is_legal": True
            })

        goals.sort(key=lambda g: g["score"], reverse=True)
        return goals

def score_colony_site(site, ship_q, ship_r, current_player=None, state=None):
    if state and current_player:
        return PioneerScorer.score_colony_target(site, ship_q, ship_r, current_player, state)
    dist = ((site["q"] - ship_q)**2 + (site["r"] - ship_r)**2) ** 0.5
    pips = site.get("productionProbability", 0)
    score = pips * 5.0 - dist * 1.5
    if site.get("hazardKind") in ("IcePlanet", "MarauderBase") and site.get("canResolveHazard"):
        score += 150.0
    return score

def get_supply_trade_rate(current_player, resource_name):
    """Returns the supply trade rate (1, 2, or 3) taking generic Trader FriendshipCardIds into account."""
    cards = set(current_player.get("friendshipCards", [])) if current_player else set()
    if resource_name == "tradeGoods":
        return 1 if "TraderTradeGoods11" in cards else 2
    card_map = {
        "ore": "TraderTradeOre21",
        "fuel": "TraderTradeFuel21",
        "carbon": "TraderTradeCarbon21",
        "food": "TraderTradeFood21",
    }
    if resource_name in card_map and card_map[resource_name] in cards:
        return 2
    return 3

def try_trade_with_supply_for_goal(current_player, goal_name, cost, step_delay=0.5):
    """
    Checks if current resources lack any part of cost, and if so, checks if surplus
    resources (TradeGoods at 1:1/2:1 or others at 2:1/3:1) can be traded with supply to obtain
    the missing resource.
    Returns True if a trade action was sent, False otherwise.
    """
    res = current_player.get("resources", {}) if current_player else {}
    current_counts = {r: res.get(r, 0) for r in ["tradeGoods", "ore", "fuel", "carbon", "food"]}

    missing = {}
    for r, needed in cost.items():
        have = current_counts.get(r, 0)
        if have < needed:
            missing[r] = needed - have

    if not missing:
        return False

    surplus = {}
    for r in ["tradeGoods", "ore", "fuel", "carbon", "food"]:
        needed = cost.get(r, 0)
        have = current_counts.get(r, 0)
        if have > needed:
            surplus[r] = have - needed

    # Protect TradeGoods: if bot has available trade stations and open outposts exist, preserve at least 2 TradeGoods unless building a TradeShip!
    has_trade_supply = current_player.get("availableTradeStations", 0) > 0
    trade_goods_reserve = 2 if (has_trade_supply and goal_name != "TradeShip") else 0
    spendable_trade_goods = max(0, surplus.get("tradeGoods", 0) - trade_goods_reserve)

    # Prefer trading TradeGoods if spendable (1:1 with TraderTradeGoods11 or 2:1 base rate)
    tg_rate = get_supply_trade_rate(current_player, "tradeGoods")
    if spendable_trade_goods >= tg_rate:
        target_res = next(iter(missing.keys()))
        print(f"[Bot] Trading with supply: {tg_rate} TradeGoods -> 1 {target_res} (Goal: {goal_name})...")
        res_act = send_action({"action": "TradeWithSupply", "resource": "tradeGoods", "targetResource": target_res})
        if res_act.get("success"):
            print(f"[Bot] Result: {res_act.get('message')}")
            time.sleep(step_delay)
            return True
        elif tg_rate == 1 and spendable_trade_goods >= 2:
            # If 1:1 was already used this turn, fall back to 2:1
            res_act = send_action({"action": "TradeWithSupply", "resource": "tradeGoods", "targetResource": target_res})
            if res_act.get("success"):
                print(f"[Bot] Result: {res_act.get('message')}")
                time.sleep(step_delay)
                return True

    # Otherwise trade other surplus resources according to their effective rate (2:1 or 3:1)
    other_surplus = []
    for r, cnt in surplus.items():
        if r == "tradeGoods":
            continue
        rate = get_supply_trade_rate(current_player, r)
        if cnt >= rate:
            other_surplus.append((r, cnt, rate))
    other_surplus.sort(key=lambda x: (x[2], -x[1]))
    if other_surplus:
        spend_res, _, rate = other_surplus[0]
        target_res = next(iter(missing.keys()))
        print(f"[Bot] Trading with supply: {rate} {spend_res} -> 1 {target_res} (Goal: {goal_name})...")
        res_act = send_action({"action": "TradeWithSupply", "resource": spend_res, "targetResource": target_res})
        print(f"[Bot] Result: {res_act.get('message') or res_act.get('error')}")
        time.sleep(step_delay)
        return True

    return False

TEST_SPACE_JUMP = False
TRIGGERED_SPACE_JUMP_DEBUG = False

def decide_and_act(state, step_delay=0.6):
    legal = state.get("legalActions", [])
    current_idx = state.get("currentPlayerIndex", 0)
    current_player = None
    players = state.get("players", [])
    if 0 <= current_idx < len(players):
        current_player = players[current_idx]

    player_name = current_player.get("name") if current_player else f"Player {current_idx}"
    player_type = current_player.get("playerType", "AI") if current_player else "AI"
    is_waiting_for_agent = state.get("isWaitingForAgent", len(legal) > 0)
    local_idx = state.get("localPlayerIndex", 0)
    local_player = players[local_idx] if 0 <= local_idx < len(players) else None
    local_name = local_player.get("name") if local_player else f"Player {local_idx}"

    if TARGET_PLAYER_INDEX is not None and local_idx != TARGET_PLAYER_INDEX:
        time.sleep(0.3)
        return True

    if TARGET_PLAYER_NAME is not None and local_name != TARGET_PLAYER_NAME:
        time.sleep(0.3)
        return True

    if not is_waiting_for_agent or not legal:
        print(f"[Bot] Turn {state.get('turnNumber')} | Active: {player_name} ({player_type}) | Waiting for agent turn ({local_name})...", end="\r", flush=True)
        time.sleep(0.4)
        return True

    print(f"\n[Bot] Turn {state.get('turnNumber')} | Phase: {state.get('turnPhase')} ({state.get('foundingPhase')}) | Active: {player_name} | Acting for: {local_name}")
    print(f"[Bot] Legal actions: {legal}")

    # 0. Pre-game start screen setup
    if "StartGame" in legal:
        print("[Bot] Game setup active. Launching game from start screen...")
        res = send_action({"action": "StartGame"})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    # 1. Dialogs (7-Roll Discard or Steal)
    dialogs = state.get("dialogs", {})
    if "SevenRollDiscard" in legal:
        discard_idx = dialogs.get("discardPlayerIndex", local_idx)
        discarding_player = players[discard_idx] if 0 <= discard_idx < len(players) else local_player
        res = discarding_player.get("resources", {}) if discarding_player else {}
        total_res = res.get("total", 0)
        req = dialogs.get("discardRequiredCount", 0)
        if req <= 0 and total_res > 7:
            req = total_res // 2

        discards = {"ore": 0, "fuel": 0, "carbon": 0, "food": 0, "tradeGoods": 0}
        needed = req
        for r_name in ["food", "carbon", "ore", "fuel", "tradeGoods"]:
            have = res.get(r_name, 0)
            take = min(have, needed)
            discards[r_name] = take
            needed -= take
            if needed <= 0:
                break
        print(f"[Bot] Discarding {req} cards for {discarding_player.get('name', 'player')}: {discards}")
        res = send_action({"action": "SevenRollDiscard", **discards})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    if "SevenRollSteal" in legal:
        targets = dialogs.get("eligibleStealTargetIndices", [])
        target_idx = targets[0] if targets else 1
        print(f"[Bot] Stealing card from player {target_idx}")
        res = send_action({"action": "SevenRollSteal", "partnerIndex": target_idx})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    if "SelectFriendshipCard" in legal:
        options = dialogs.get("friendshipCardOptions", [])
        priority_ids = [
            "DiplomatBoughtFame1",
            "DiplomatBoughtFame2",
            "TraderTradeGoods11",
            "DiplomatTributeDiscount",
            "ScientistUpgradeCannonAndDrive1",
            "ScientistUpgradeCannonAndDrive2",
            "ScientistUpgradeCannonAndDrive3",
            "ScientistUpgradeDrives2",
            "ScientistUpgradeCannons2",
            "DiplomatRichHelpsPoor",
            "TraderTradeOre21",
            "TraderTradeFuel21",
            "TraderTradeCarbon21",
            "TraderTradeFood21",
            "CultivatorProductionGoods",
            "CultivatorProductionOre",
            "CultivatorProductionFood",
            "CultivatorProductionFuel",
            "CultivatorProductionCarbon",
            "GrangefolkProductionGoods",
            "GrangefolkProductionOre",
            "GrangefolkProductionFood",
            "GrangefolkProductionFuel",
            "GrangefolkProductionCarbon",
        ]
        chosen_idx = 0
        if options:
            for pid in priority_ids:
                match = next((opt for opt in options if opt.get("cardId") == pid), None)
                if match is not None:
                    chosen_idx = match.get("index", 0)
                    break
        card_desc = f"#{chosen_idx}"
        if options and 0 <= chosen_idx < len(options):
            opt = options[chosen_idx]
            card_desc = f"#{chosen_idx} ({opt.get('cardId', 'Card')}: '{opt.get('name', 'Card')}')"
        print(f"[Bot] Selecting friendship card {card_desc}...")
        res = send_action({"action": "SelectFriendshipCard", "cardIndex": chosen_idx})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    # 2. Founding Phase Actions
    if "RollDice" in legal and state.get("foundingPhase") == "RollForStartingPlayer":
        print("[Bot] Rolling for starting player...")
        res = send_action({"action": "RollDice"})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    if "PlaceFoundingColony" in legal:
        free_home = get_free_home_colony_sites(state)
        if free_home:
            chosen = free_home[0]
            print(f"[Bot] Placing founding colony at ({chosen['q']}, {chosen['r']})...")
            res = send_action({"action": "PlaceFoundingColony", "q": chosen["q"], "r": chosen["r"]})
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    if "SelectFoundingSpaceport" in legal:
        colonies = current_player.get("colonies", []) if current_player else []
        my_colonies = [c for c in colonies if c.get("ownerIndex") == local_idx]
        if not my_colonies:
            my_colonies = colonies
        regular = [c for c in my_colonies if c.get("type") == "Colony"]
        candidates = regular if regular else my_colonies

        # Evaluate distance to planned targets (Sector 1 colony sites and trade outposts)
        free_colonies = get_free_colony_sites(state)
        free_trades = get_free_trade_sites(state)
        top_colonies = sorted(free_colonies, key=lambda s: s.get("productionProbability", 0) * 8 - s.get("q", 0) * 2, reverse=True)[:3]
        top_trades = sorted(free_trades, key=lambda s: -s.get("q", 0))[:2]
        planned_targets = top_colonies + top_trades

        def score_spaceport_colony(c):
            cq, cr = c.get("q", 28), c.get("r", 0)
            score = 0.0
            # Forward bonus: Q = 27 faces directly towards deep space/Sector 1; Q = 28 is side; Q = 29 is behind home system
            score += (28 - cq) * 25.0

            if planned_targets:
                pri = planned_targets[0]
                pri_d = ((cq - pri["q"])**2 + (cr - pri["r"])**2) ** 0.5
                score -= pri_d * 12.0

                avg_d = sum(((cq - t["q"])**2 + (cr - t["r"])**2) ** 0.5 for t in planned_targets) / len(planned_targets)
                score -= avg_d * 6.0
            return score

        sorted_candidates = sorted(candidates, key=score_spaceport_colony, reverse=True)
        for c in sorted_candidates:
            print(f"[Bot] Selecting strategic spaceport at ({c['q']}, {c['r']}) [score: {score_spaceport_colony(c):.1f}]...")
            res = send_action({"action": "SelectFoundingSpaceport", "q": c["q"], "r": c["r"]})
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
            if res.get("success"):
                break
        time.sleep(step_delay)
        return True

    if "SelectStartingShip" in legal:
        print("[Bot] Selecting ColonyShip...")
        res = send_action({"action": "SelectStartingShip", "shipType": "ColonyShip"})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    if "SelectFoundingShipPoint" in legal:
        colonies = current_player.get("colonies", []) if current_player else []
        spaceport = next((c for c in colonies if c.get("type") == "Spaceport"), colonies[0] if colonies else None)
        if spaceport:
            sq, sr = spaceport["q"], spaceport["r"]
            free_colonies = get_free_colony_sites(state)
            pri = free_colonies[0] if free_colonies else {"q": 16, "r": 5}
            candidates = [(sq - 1, sr), (sq, sr - 1), (sq, sr + 1), (sq + 1, sr)]
            # Sort launch candidates by proximity to planned destination
            candidates.sort(key=lambda pt: ((pt[0] - pri["q"])**2 + (pt[1] - pri["r"])**2))
            for cq, cr in candidates:
                print(f"[Bot] Trying founding ship spawn point ({cq}, {cr})...")
                res = send_action({"action": "SelectFoundingShipPoint", "q": cq, "r": cr})
                if res.get("success"):
                    print(f"[Bot] Result: {res.get('message')}")
                    break
        time.sleep(step_delay)
        return True

    if "SelectStartingUpgrade" in legal:
        rem_boosters = state.get("foundingRemainingBoosters", 0)
        rem_cannons = state.get("foundingRemainingCannons", 0)
        rem_pods = state.get("foundingRemainingFreightPods", 0)
        print(f"[Bot] Remaining starting upgrades: Booster={rem_boosters}, FreightPod={rem_pods}, Cannon={rem_cannons}")

        candidates = []
        if rem_pods > 0:
            candidates.append("FreightPod")
        if rem_boosters > 0:
            candidates.append("Booster")
        if rem_cannons > 0:
            candidates.append("Cannon")

        if not candidates:
            candidates = ["Booster", "FreightPod", "Cannon"]

        for upgrade in candidates:
            print(f"[Bot] Trying starting upgrade '{upgrade}'...")
            res = send_action({"action": "SelectStartingUpgrade", "upgradeType": upgrade})
            if res.get("success"):
                print(f"[Bot] Result: {res.get('message')}")
                break
            else:
                print(f"[Bot] Selection failed for '{upgrade}': {res.get('error')}")
        time.sleep(step_delay)
        return True

    # 3. Encounters (can occur on any player's flight phase)
    enc = state.get("encounter", {})
    if enc.get("isPending"):
        enc_phase = enc.get("phase", "")

        # Always prioritize confirming an already applied outcome
        if enc_phase == "ApplyOutcome" or "ConfirmEncounter" in legal:
            print("[Bot] Confirming encounter...")
            res = send_action({"action": "ConfirmEncounter"})
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
            time.sleep(step_delay)
            return True

        if "AdvanceEncounter" in legal and enc_phase == "ReadPrompt":
            c_type = enc.get("cardType", "Encounter")
            c_char = enc.get("characterIndex", 0)
            c_title = enc.get("title", "")
            print(f"[Bot] Advancing encounter [{c_type}, char #{c_char}] '{c_title}'...")
            res = send_action({"action": "AdvanceEncounter"})
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
            time.sleep(step_delay)
            return True

        if "SelectEncounterOutcome" in legal or enc_phase in ("SelectOutcome", "ReadPrompt"):
            c_type = enc.get("cardType", "")
            c_char = enc.get("characterIndex", 0)
            outcomes = enc.get("outcomes", [])
            hand = current_player.get("resources", {}) if current_player else {}
            total_res = hand.get("total", 0)
            outcome_idx = 0
            res_payload = {}

            # Find opponent player (for combat/speed comparison)
            opponents = [p for p in state.get("players", []) if p.get("playerIndex") != current_player.get("playerIndex")]
            opponent = opponents[0] if opponents else None

            flagship = (current_player.get("flagship") or current_player.get("mothership", {})) if current_player else {}
            opp_flagship = (opponent.get("flagship") or opponent.get("mothership", {})) if opponent else {}

            if c_type in ("TraderGift", "Drifter", "WanderingPeople") and outcomes:
                # Determine donation amount (0, 1, or 2)
                if total_res >= 2 and len(outcomes) > 2:
                    donation_amt = 2
                elif total_res >= 1 and len(outcomes) > 1:
                    donation_amt = 1
                else:
                    donation_amt = 0

                if c_type == "TraderGift":
                    outcome_idx = min(donation_amt, len(outcomes) - 1)
                else: # Drifter / WanderingPeople
                    outcome_idx = min(donation_amt, len(outcomes) - 1)

                # Allocate specific resources to donate
                needed = donation_amt
                for r_key in ["ore", "carbon", "fuel", "food", "tradeGoods"]:
                    have = hand.get(r_key, 0)
                    spend = min(have, needed)
                    if spend > 0:
                        res_payload[r_key] = spend
                        needed -= spend
                    if needed <= 0:
                        break

            elif c_type in ("TraderMarauder", "MarauderDemand", "MarauderAttack", "Rescue") and len(outcomes) > 1:
                # Check if this encounter has combat or speed comparison
                outcomes_text = " ".join([o.get("title", "") + " " + o.get("description", "") for o in outcomes])
                is_speed = "schnell" in outcomes_text.lower() or "speed" in outcomes_text.lower()

                if is_speed:
                    my_boosters = flagship.get("boosters", 0)
                    opp_boosters = opp_flagship.get("boosters", 0)
                    my_speed = random.randint(1, 3) + my_boosters
                    opp_speed = random.randint(1, 3) + opp_boosters
                    print(f"[Bot] Encounter Speed Test: player={my_speed} ({my_boosters} boosters) vs opp={opp_speed} ({opp_boosters} boosters)")
                    if my_speed >= opp_speed:
                        outcome_idx = 1 # Mindestens so schnell
                    else:
                        outcome_idx = 2 if len(outcomes) > 2 else 0
                else:
                    # Combat comparison
                    my_cannons = flagship.get("cannons", 0)
                    opp_cannons = opp_flagship.get("cannons", 0)
                    my_combat = random.randint(1, 3) + my_cannons
                    opp_combat = random.randint(1, 3) + opp_cannons
                    print(f"[Bot] Encounter Combat Test: player={my_combat} ({my_cannons} cannons) vs marauder/opp={opp_combat} ({opp_cannons} cannons)")
                    if my_combat >= opp_combat:
                        outcome_idx = 1 # Sieg im Kampf
                    else:
                        outcome_idx = 2 if len(outcomes) > 2 else 0

            elif c_type == "SpaceDistortion" and len(outcomes) > 1:
                my_boosters = flagship.get("boosters", 0)
                opp_boosters = opp_flagship.get("boosters", 0)
                my_speed = random.randint(1, 3) + my_boosters
                opp_speed = random.randint(1, 3) + opp_boosters
                print(f"[Bot] Space Distortion Speed Test: player={my_speed} vs opp={opp_speed}")
                outcome_idx = 1 # Attempt Space Jump outcome

            elif c_type in ("MarauderTrade", "MarauderRaidOffer") and len(outcomes) > 1:
                if total_res >= 1:
                    roll = random.randint(1, 5)
                    if roll <= 2 and len(outcomes) > 1:
                        outcome_idx = 1
                    elif roll == 3 and len(outcomes) > 2:
                        outcome_idx = 2
                    elif len(outcomes) > 3:
                        outcome_idx = 3
                    else:
                        outcome_idx = 1
                else:
                    outcome_idx = 0 # Ablehnen

            elif c_type == "Event" and outcomes:
                outcome_idx = len(outcomes) - 1 # Execute event conclusion

            action_data = {"action": "SelectEncounterOutcome", "outcomeIndex": outcome_idx}
            action_data.update(res_payload)
            print(f"[Bot] Selecting encounter outcome index {outcome_idx} for [{c_type}, char #{c_char}] (donation: {res_payload})...")
            res = send_action(action_data)
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
            if not res.get("success") and "not waiting for an outcome" in (res.get("error") or ""):
                print("[Bot] Fallback: Encounter already waiting for confirmation; sending ConfirmEncounter...")
                send_action({"action": "ConfirmEncounter"})
            time.sleep(step_delay)
            return True

    # 4. Regular Turns (only drive turns for LOCAL / Human player; AI players play automatically)
    if state.get("foundingPhase") == "Complete" and current_player and current_player.get("playerType") == "AI":
        print(f"[Bot] AI player '{player_name}' is taking their turn. Waiting for AI controller...")
        time.sleep(step_delay)
        return True

    if "RollDice" in legal:
        print("[Bot] Rolling production dice...")
        res = send_action({"action": "RollDice"})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    global TRIGGERED_SPACE_JUMP_DEBUG
    if TEST_SPACE_JUMP and not TRIGGERED_SPACE_JUMP_DEBUG and state.get("turnNumber", 0) >= 1 and state.get("turnPhase") == "Flight":
        print("[Bot] [TestMode] Triggering Debug Encounter #26 (Tachyon Space Jump)...")
        TRIGGERED_SPACE_JUMP_DEBUG = True
        res = send_action({"action": "TriggerDebugEncounter", "outcomeIndex": 26})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    if "RollFlight" in legal:
        print("[Bot] Engaging flight drives / rolling flight impulse...")
        res = send_action({"action": "RollFlight"})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    if "ExecuteSpaceJump" in legal:
        units = current_player.get("units", []) if current_player else []
        colony_ships = [u for u in units if u.get("shipType") == "ColonyShip"]
        trade_ships = [u for u in units if u.get("shipType") == "TradeShip"]
        ship_coords = {(u.get("q"), u.get("r")) for u in units}

        target = None
        if colony_ships:
            c_sites = get_free_colony_sites(state)
            valid = [s for s in c_sites if (s.get("q"), s.get("r")) not in ship_coords and not s.get("isOccupied")]
            if valid:
                target = (valid[0]["q"], valid[0]["r"])
        elif trade_ships:
            t_sites = get_free_trade_sites(state)
            valid = [s for s in t_sites if (s.get("q"), s.get("r")) not in ship_coords and not s.get("isOccupied")]
            if valid:
                target = (valid[0]["q"], valid[0]["r"])

        if not target:
            sites = state.get("board", {}).get("sites", [])
            for s in sites:
                if not s.get("isOccupied") and (s.get("q"), s.get("r")) not in ship_coords:
                    target = (s["q"], s["r"])
                    break

        if target:
            t_q, t_r = target
            print(f"[Bot] Executing Space Jump to ({t_q}, {t_r})...")
            res = send_action({"action": "ExecuteSpaceJump", "q": t_q, "r": t_r})
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
            time.sleep(step_delay)
            return True
        else:
            print("[Bot] No target for Space Jump; cancelling...")
            res = send_action({"action": "CancelSpaceJump"})
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
            time.sleep(step_delay)
            return True

    if "FoundColony" in legal:
        units = current_player.get("units", []) if current_player else []
        colony_ships = [u for u in units if u.get("shipType") == "ColonyShip"]
        founded = False
        for ship in colony_ships:
            print(f"[Bot] Founding colony with ship at ({ship['q']}, {ship['r']})...")
            res = send_action({"action": "FoundColony", "q": ship["q"], "r": ship["r"]})
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
            if res.get("success"):
                founded = True
                break
        if not founded:
            res = send_action({"action": "FoundColony"})
            if res.get("success"):
                print(f"[Bot] Result: {res.get('message')}")
                founded = True
        if founded:
            time.sleep(step_delay)
            return True

    if "BuildTradeStation" in legal:
        units = current_player.get("units", []) if current_player else []
        trade_ships = [u for u in units if u.get("shipType") == "TradeShip"]
        built = False
        for ship in trade_ships:
            print(f"[Bot] Building trade station with ship at ({ship['q']}, {ship['r']})...")
            res = send_action({"action": "BuildTradeStation", "q": ship["q"], "r": ship["r"]})
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
            if res.get("success"):
                built = True
                break
        if not built:
            res = send_action({"action": "BuildTradeStation"})
            if res.get("success"):
                print(f"[Bot] Result: {res.get('message')}")
                built = True
        if built:
            time.sleep(step_delay)
            return True

    if "MoveShip" in legal:
        units = current_player.get("units", []) if current_player else []
        movable = [u for u in units if u.get("canMove") and u.get("movementRemaining", 0) > 0]
        occupied_friendly_positions = {(u["q"], u["r"]) for u in units}
        moved = False
        for ship in movable:
            if ship.get("shipType") == "ColonyShip":
                targets = get_free_colony_sites(state)
            elif ship.get("shipType") == "TradeShip":
                targets = get_free_trade_sites(state)
            else:
                targets = []

            if not targets:
                continue

            candidate_targets = [t for t in targets if (t["q"], t["r"]) not in occupied_friendly_positions or (t["q"], t["r"]) == (ship["q"], ship["r"])]
            if not candidate_targets:
                candidate_targets = targets

            if candidate_targets:
                if ship.get("shipType") == "ColonyShip":
                    sorted_targets = sorted(candidate_targets, key=lambda t: PioneerScorer.score_colony_target(t, ship["q"], ship["r"], current_player, state), reverse=True)
                else:
                    sorted_targets = sorted(candidate_targets, key=lambda t: PioneerScorer.score_trade_target(t, ship["q"], ship["r"], current_player, state), reverse=True)

                for target in sorted_targets[:5]:
                    if (target["q"], target["r"]) == (ship["q"], ship["r"]):
                        continue
                    target_info = f"({target['q']}, {target['r']})"
                    if target.get("hazardKind") in ("IcePlanet", "MarauderBase") and target.get("canResolveHazard"):
                        target_info += f" [{target.get('hazardKind')} - terraforming/defeating for +2 VP!]"
                    elif target.get("folkType"):
                        target_info += f" [Outpost: {target.get('folkType')}]"
                    elif target.get("productionProbability", 0) > 0:
                        target_info += f" [Pips: {target.get('productionProbability')}]"

                    print(f"[Bot] Moving ship {ship.get('id')} ({ship.get('shipType')}) from ({ship['q']}, {ship['r']}) towards {target_info}...")
                    res = send_action({
                        "action": "MoveShip",
                        "unitId": ship.get("id", 0),
                        "fromQ": ship["q"],
                        "fromR": ship["r"],
                        "toQ": target["q"],
                        "toR": target["r"]
                    }, timeout=15.0)
                    print(f"[Bot] Result: {res.get('message') or res.get('error')}")
                    if res.get("success"):
                        moved = True
                        if ship.get("shipType") == "ColonyShip":
                            res_col = send_action({"action": "FoundColony", "q": target["q"], "r": target["r"]})
                            if res_col.get("success"):
                                print(f"[Bot] Founded colony immediately upon arrival at ({target['q']}, {target['r']}): {res_col.get('message')}")
                        elif ship.get("shipType") == "TradeShip":
                            res_trade = send_action({"action": "BuildTradeStation", "q": target["q"], "r": target["r"]})
                            if res_trade.get("success"):
                                print(f"[Bot] Built trade station immediately upon arrival at ({target['q']}, {target['r']}): {res_trade.get('message')}")
                        break
                    elif "Timeout" in str(res.get("error", "")):
                        # If movement timed out, do not retry outdated coordinates
                        break
                    else:
                        # Path/target blocked or unreachable; try next candidate target
                        continue
            if moved:
                break
        if moved:
            time.sleep(step_delay)
            return True
        elif "EndTurn" in legal:
            print("[Bot] No ship could move; ending turn...")
            res = send_action({"action": "EndTurn"})
            print(f"[Bot] Result: {res.get('message') or res.get('error')}")
            time.sleep(step_delay)
            return True

    # In Trade & Build: evaluate all candidate build goals with PioneerScorer!
    if state.get("turnPhase") == "TradeAndBuild":
        build_goals = PioneerScorer.evaluate_build_goals(current_player, state, legal)

        # 1. Direct builds: execute the highest-scored affordable goal
        for goal in build_goals:
            if goal["can_afford"] and goal["is_legal"]:
                print(f"[Bot] Executing top build goal '{goal['name']}' (Score: {goal['score']:.1f})...")
                res_act = send_action({"action": goal["action"], **goal["args"]})
                if res_act.get("success"):
                    print(f"[Bot] Result: {res_act.get('message')}")
                    time.sleep(step_delay)
                    return True
                else:
                    print(f"[Bot] Build action '{goal['name']}' failed: {res_act.get('error')}")

        # 2. Goal-directed supply trading: convert surplus resources to unlock the highest-scored goal
        if "TradeWithSupply" in legal:
            for goal in build_goals:
                if not goal["can_afford"]:
                    if try_trade_with_supply_for_goal(current_player, goal["name"], goal["cost"], step_delay=step_delay):
                        return True

        # 3. Advance to flight phase
        if "AdvancePhase" in legal:
            print("[Bot] Advancing from Trade & Build to Flight phase...")
            res_act = send_action({"action": "AdvancePhase"})
            print(f"[Bot] Result: {res_act.get('message') or res_act.get('error')}")
            time.sleep(step_delay)
            return True

    if "EndTurn" in legal:
        print("[Bot] Ending turn...")
        res = send_action({"action": "EndTurn"})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    if "AdvancePhase" in legal:
        print("[Bot] Advancing phase...")
        res = send_action({"action": "AdvancePhase"})
        print(f"[Bot] Result: {res.get('message') or res.get('error')}")
        time.sleep(step_delay)
        return True

    time.sleep(0.5)
    return True

def print_game_over_ranking(state):
    print("\n" + "=" * 76)
    print("                      🏁 SPIELENDE – ENDSTAND 🏁")
    print("=" * 76)
    winner_name = state.get("winnerName", "Unbekannt")
    print(f" 🏆 Sieger: {winner_name}!\n")

    rankings = state.get("rankings", [])
    if not rankings:
        players = state.get("players", [])
        winner_idx = state.get("winnerIndex", -1)
        sorted_p = sorted(players, key=lambda p: (p.get("playerIndex") == winner_idx, p.get("victoryPoints", 0)), reverse=True)
        rankings = []
        for i, p in enumerate(sorted_p):
            rankings.append({
                "rank": i + 1,
                "playerName": p.get("name"),
                "playerType": p.get("playerType"),
                "victoryPoints": p.get("victoryPoints", 0),
                "isWinner": p.get("playerIndex") == winner_idx,
                "coloniesCount": p.get("regularColonies", 0),
                "spaceportsCount": p.get("spaceportsCount", 0),
                "tradeStationsCount": p.get("tradeStationsCount", 0),
                "totalResources": p.get("resources", {}).get("total", 0),
            })

    header = f"{'Rang':<5} | {'Spieler':<18} | {'Typ':<6} | {'Siegpunkte':<10} | {'Kolonien':<8} | {'Raumhäfen':<9} | {'Handelsposten':<13} | {'Karten':<7}"
    print(header)
    print("-" * len(header))

    for r in rankings:
        winner_mark = " 🏆" if r.get("isWinner") else ""
        name_str = f"{r.get('playerName')}{winner_mark}"
        row = (
            f"#{r.get('rank'):<4} | "
            f"{name_str:<18} | "
            f"{r.get('playerType', 'AI'):<6} | "
            f"{r.get('victoryPoints', 0):<10} | "
            f"{r.get('coloniesCount', 0):<8} | "
            f"{r.get('spaceportsCount', 0):<9} | "
            f"{r.get('tradeStationsCount', 0):<13} | "
            f"{r.get('totalResources', 0):<7}"
        )
        print(row)

    print("=" * 76)
    agent_rank = next((r for r in rankings if r.get("playerType") == "LOCAL"), None)
    if agent_rank:
        status_word = "GEWONNEN! 🎉" if agent_rank.get("isWinner") else f"Platz #{agent_rank.get('rank')} erreicht."
        print(f" Agenten-Leistung ({agent_rank.get('playerName')}): {status_word} ({agent_rank.get('victoryPoints')} Siegpunkte).")
    print("=" * 76 + "\n")

def run_bot(max_turns=500, delay=0.5):
    print("=" * 60)
    print(" Deep Space Pioneers Autonomous Bot Agent Started")
    print(f" Bridge Directory: {BRIDGE_DIR}")
    print("=" * 60)

    while True:
        state = read_state(timeout=3.0)
        if not state:
            print("[Bot] Waiting for Unity state.json...")
            time.sleep(1.0)
            continue

        if state.get("isGameOver"):
            print_game_over_ranking(state)
            break

        turn = state.get("turnNumber", 0)
        if turn > max_turns:
            print(f"[Bot] Reached maximum turn limit ({max_turns}). Stopping.")
            break

        decide_and_act(state, step_delay=delay)

if __name__ == "__main__":
    delay = 0.5
    if "--delay" in sys.argv:
        idx = sys.argv.index("--delay")
        if idx + 1 < len(sys.argv):
            delay = float(sys.argv[idx + 1])
    bridge_target = "agent_bridge"
    if "--dir" in sys.argv or "--bridge-dir" in sys.argv:
        flag = "--dir" if "--dir" in sys.argv else "--bridge-dir"
        idx = sys.argv.index(flag)
        if idx + 1 < len(sys.argv):
            bridge_target = sys.argv[idx + 1]

    resolved = resolve_bridge_dir(bridge_target)
    set_bridge_dir(resolved)
    if "--player" in sys.argv or "--player-index" in sys.argv:
        flag = "--player" if "--player" in sys.argv else "--player-index"
        idx = sys.argv.index(flag)
        if idx + 1 < len(sys.argv):
            TARGET_PLAYER_INDEX = int(sys.argv[idx + 1])
            print(f"[Bot] Restricting bot to Player Index: {TARGET_PLAYER_INDEX}")
    if "--player-name" in sys.argv:
        idx = sys.argv.index("--player-name")
        if idx + 1 < len(sys.argv):
            TARGET_PLAYER_NAME = sys.argv[idx + 1]
            print(f"[Bot] Restricting bot to Player Name: '{TARGET_PLAYER_NAME}'")
    if "--prefix" in sys.argv:
        idx = sys.argv.index("--prefix")
        if idx + 1 < len(sys.argv):
            BOT_PREFIX = sys.argv[idx + 1]

    max_turns = 500
    if "--max-turns" in sys.argv:
        idx = sys.argv.index("--max-turns")
        if idx + 1 < len(sys.argv):
            max_turns = int(sys.argv[idx + 1])
    if "--test-space-jump" in sys.argv:
        TEST_SPACE_JUMP = True
        print("[Bot] Enabled --test-space-jump flag: will trigger Card #26 on Turn 1 Flight phase.")
    try:
        run_bot(max_turns=max_turns, delay=delay)
    except KeyboardInterrupt:
        print("\n[Bot] Bot stopped by user (SIGINT).")
        sys.exit(0)


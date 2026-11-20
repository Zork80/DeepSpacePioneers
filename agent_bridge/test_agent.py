#!/usr/bin/env python3
"""
Test client for the Deep Space Pioneers External Agent Bridge.
Monitors state.json, issues actions via action.json, and inspects result.json.

Usage:
    python3 test_agent.py [--ping | --roll | --step | --watch]
"""

import json
import os
import sys
import time
from pathlib import Path

BRIDGE_DIR = Path(__file__).resolve().parent
STATE_FILE = BRIDGE_DIR / "state.json"
ACTION_FILE = BRIDGE_DIR / "action.json"
RESULT_FILE = BRIDGE_DIR / "result.json"

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
            if _CANDIDATE_DIRS:
                for cand in _CANDIDATE_DIRS:
                    cand_state = cand / "state.json"
                    if cand != BRIDGE_DIR and cand_state.exists():
                        print(f"[Agent] Discovered active bridge directory: {cand}")
                        set_bridge_dir(cand)
                        break
            time.sleep(0.05)
    return None

def send_action(action_dict, timeout=25.0):
    if action_dict.get("action") == "MoveShip":
        timeout = max(timeout, 35.0)
    action_id = action_dict.get("actionId", f"cmd-{int(time.time()*1000)}")
    action_dict["actionId"] = action_id

    # Remove previous result file if present
    if RESULT_FILE.exists():
        try:
            RESULT_FILE.unlink()
        except OSError:
            pass

    # Atomic write to action.json
    tmp_file = BRIDGE_DIR / f"action_{action_id}.tmp"
    with open(tmp_file, "w", encoding="utf-8") as f:
        json.dump(action_dict, f, indent=2)
    tmp_file.replace(ACTION_FILE)

    print(f"[Agent] Sent action '{action_dict.get('action')}' (id={action_id}). Awaiting result...")

    # Poll for result.json
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

    return {"actionId": action_id, "success": False, "error": f"Timeout after {timeout}s waiting for result.json"}

def print_summary(state):
    if not state:
        print("[Agent] No state.json available. Is Unity running?")
        return

    print("\n" + "=" * 60)
    print(f" Turn: {state.get('turnNumber', '?')} | Step: {state.get('step', '?')} | Phase: {state.get('turnPhase')} (Founding: {state.get('foundingPhase')})")
    print(f" Active Player: #{state.get('currentPlayerIndex')} '{state.get('currentPlayerName')}'")
    print(f" Game Over: {state.get('isGameOver')} (Winner: {state.get('winnerName', 'None')})")
    print(f" Legal Actions: {state.get('legalActions', [])}")

    enc = state.get("encounter", {})
    if enc and enc.get("isPending"):
        print(f" Active Encounter: #{enc.get('cardId')} [{enc.get('cardType')}, char #{enc.get('characterIndex', 0)}] '{enc.get('title')}' (Phase: {enc.get('phase')})")

    players = state.get("players", [])
    if players:
        print("-" * 60)
        print(" Players:")
        for p in players:
            res = p.get("resources", {})
            m = p.get("flagship") or p.get("mothership", {})
            units = p.get("units", [])
            fcards = p.get("friendshipCards", [])
            fcards_str = f" | Friendship: {','.join(fcards)}" if fcards else ""
            print(f"  [{p.get('playerIndex')}] {p.get('name')} ({p.get('playerType')}) | VP: {p.get('victoryPoints')} | Medals: {p.get('halfMedals')} | Cards: {res.get('total', 0)} (Ore:{res.get('ore')} Fuel:{res.get('fuel')} Carb:{res.get('carbon')} Food:{res.get('food')} Goods:{res.get('tradeGoods')}) | Ships: {len(units)} | Boosters:{m.get('boosters')} Cannons:{m.get('cannons')} Pods:{m.get('freightPods')}{fcards_str}")
    print("=" * 60 + "\n")

def main():
    global BRIDGE_DIR, STATE_FILE, ACTION_FILE, RESULT_FILE

    bridge_target = "agent_bridge"
    if "--dir" in sys.argv or "--bridge-dir" in sys.argv:
        flag = "--dir" if "--dir" in sys.argv else "--bridge-dir"
        idx = sys.argv.index(flag)
        if idx + 1 < len(sys.argv):
            bridge_target = sys.argv[idx + 1]

    resolved = resolve_bridge_dir(bridge_target)
    set_bridge_dir(resolved)

    args = [a for a in sys.argv[1:] if not a.startswith("--dir") and not a.startswith("--bridge-dir") and a != str(BRIDGE_DIR)]
    mode = args[0] if args else "--summary"

    print(f"[Agent] Checking bridge directory: {BRIDGE_DIR}")
    state = read_state(timeout=2.0)

    if mode == "--summary":
        print_summary(state)

    elif mode == "--ping":
        res = send_action({"action": "Ping"})
        print(f"[Agent] Result: {json.dumps(res, indent=2)}")
        new_state = read_state(timeout=2.0)
        print_summary(new_state)

    elif mode == "--roll":
        res = send_action({"action": "RollDice"})
        print(f"[Agent] Result: {json.dumps(res, indent=2)}")

    elif mode == "--advance":
        res = send_action({"action": "AdvancePhase"})
        print(f"[Agent] Result: {json.dumps(res, indent=2)}")

    elif mode == "--watch":
        print("[Agent] Watching state.json (Ctrl+C to quit)...")
        last_step = None
        while True:
            st = read_state(timeout=1.0)
            if st and st.get("step") != last_step:
                last_step = st.get("step")
                print_summary(st)
            time.sleep(0.5)

    else:
        print(f"Unknown mode '{mode}'. Available: --summary, --ping, --roll, --advance, --watch")

if __name__ == "__main__":
    main()


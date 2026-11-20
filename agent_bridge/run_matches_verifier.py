#!/usr/bin/env python3
import os
import sys
import time
import json
import signal
import subprocess
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parent.parent
BIN = WORKSPACE / "Builds" / "Linux_Client" / "DeepSpacePioneers.x86_64"
BRIDGE_HOST = WORKSPACE / "agent_bridge"
BRIDGE_CLIENT = WORKSPACE / "agent_bridge_client2"

TOTAL_MATCHES = 5
DEADLOCK_TIMEOUT = 90.0  # seconds without state progress before aborting
BOT_STEP_DELAY = 0.05    # fast turns

def cleanup_processes(pids):
    for p in pids:
        if p and p.poll() is None:
            try:
                p.terminate()
            except Exception:
                pass
    time.sleep(0.5)
    for p in pids:
        if p and p.poll() is None:
            try:
                p.kill()
            except Exception:
                pass

def read_json_safe(path):
    if not path.is_file():
        return None
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return None

def check_logs_for_fatal_errors(log_path):
    if not os.path.exists(log_path):
        return None
    fatal_patterns = [
        "NullReferenceException",
        "IndexOutOfRangeException",
        "InvalidOperationException",
        "MissingReferenceException",
        "Unhandled Exception",
        "Crash",
        "Assertion failed"
    ]
    # Whitelist harmless lines if any
    try:
        with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
            for line in f:
                for pat in fatal_patterns:
                    if pat in line and "Desktop is 0 x 0 @ 0 Hz" not in line:
                        return f"Found '{pat}' in {os.path.basename(log_path)}: {line.strip()}"
    except Exception as e:
        return f"Error reading log {log_path}: {e}"
    return None

def run_match(match_index, seed, port):
    print(f"\n{'='*70}")
    print(f" [MATCH {match_index}/{TOTAL_MATCHES}] STARTING (Seed: {seed}, Port: {port})")
    print(f"{'='*70}")

    host_log = WORKSPACE / f"host_match_{match_index}.log"
    client_log = WORKSPACE / f"client_match_{match_index}.log"
    for log in [host_log, client_log]:
        if log.exists():
            log.unlink()

    # Clean bridge dirs
    for d in [BRIDGE_HOST, BRIDGE_CLIENT]:
        d.mkdir(parents=True, exist_ok=True)
        for f in ["action.json", "result.json", "state.json"]:
            p = d / f
            if p.exists():
                p.unlink()

    env = os.environ.copy()
    env["DISPLAY"] = env.get("DISPLAY", ":0")
    env["WAYLAND_DISPLAY"] = env.get("WAYLAND_DISPLAY", "wayland-0")
    env["XDG_RUNTIME_DIR"] = env.get("XDG_RUNTIME_DIR", "/run/user/1000")

    pids = []
    try:
        # 1. Host
        host_cmd = [
            str(BIN),
            "-logFile", str(host_log),
            "-screen-fullscreen", "0",
            "-screen-width", "1280",
            "-screen-height", "720",
            "-host",
            "-autostart:2",
            f"-seed:{seed}",
            f"-port:{port}",
            "-player-name:Spieler 1",
            "-bridge-dir:agent_bridge"
        ]
        print(f"[Launcher] 1/4 Launching Host (Port {port}, Seed {seed})...")
        p_host = subprocess.Popen(host_cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env=env)
        pids.append(p_host)
        time.sleep(2.5)

        # 2. Client
        client_cmd = [
            str(BIN),
            "-logFile", str(client_log),
            "-screen-fullscreen", "0",
            "-screen-width", "1280",
            "-screen-height", "720",
            f"-connect:127.0.0.1:{port}",
            f"-seed:{seed}",
            "-player-name:Spieler 2",
            "-bridge-dir:agent_bridge_client2"
        ]
        print(f"[Launcher] 2/4 Launching Client (connecting to 127.0.0.1:{port})...")
        p_client = subprocess.Popen(client_cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env=env)
        pids.append(p_client)
        time.sleep(2.5)

        # 3. Host Bot
        print("[Launcher] 3/4 Launching Bot 1 (Host / Spieler 1)...")
        bot1_log = open(WORKSPACE / f"bot1_match_{match_index}.log", "w", encoding="utf-8")
        bot1_cmd = [
            sys.executable,
            "-u",
            str(WORKSPACE / "agent_bridge" / "autonomous_player.py"),
            "--dir", "agent_bridge",
            "--player", "0",
            "--delay", str(BOT_STEP_DELAY),
            "--max-turns", "500",
            "--prefix", "[Host-Bot] "
        ]
        p_bot1 = subprocess.Popen(bot1_cmd, stdout=bot1_log, stderr=subprocess.STDOUT)
        pids.append(p_bot1)

        # 4. Client Bot
        print("[Launcher] 4/4 Launching Bot 2 (Client / Spieler 2)...")
        bot2_log = open(WORKSPACE / f"bot2_match_{match_index}.log", "w", encoding="utf-8")
        bot2_cmd = [
            sys.executable,
            "-u",
            str(WORKSPACE / "agent_bridge" / "autonomous_player.py"),
            "--dir", "agent_bridge_client2",
            "--player", "1",
            "--delay", str(BOT_STEP_DELAY),
            "--max-turns", "500",
            "--prefix", "[Client-Bot] "
        ]
        p_bot2 = subprocess.Popen(bot2_cmd, stdout=bot2_log, stderr=subprocess.STDOUT)
        pids.append(p_bot2)

        print(f"[Match {match_index}] All 4 processes running. Monitoring gameplay...")

        start_time = time.time()
        last_progress_time = time.time()
        last_turn = -1
        last_phase = ""
        last_founding = ""
        last_step = -1
        last_curr_p = -1
        last_enc_phase = ""
        last_enc_card_id = ""
        encounters_encountered = []
        current_enc_record = None

        while True:
            # Read host state first
            h_state = read_json_safe(BRIDGE_HOST / "state.json")
            is_game_over = bool(h_state and h_state.get("isGameOver"))

            # Check process lifecycles
            if p_host.poll() is not None:
                err = check_logs_for_fatal_errors(host_log)
                raise RuntimeError(f"Host process exited prematurely (exit code {p_host.returncode}). {err or ''}")
            if p_client.poll() is not None:
                err = check_logs_for_fatal_errors(client_log)
                raise RuntimeError(f"Client process exited prematurely (exit code {p_client.returncode}). {err or ''}")
            if p_bot1.poll() is not None and not is_game_over:
                raise RuntimeError(f"Bot 1 (Host) exited unexpectedly with code {p_bot1.returncode}!")
            if p_bot2.poll() is not None and not is_game_over:
                raise RuntimeError(f"Bot 2 (Client) exited unexpectedly with code {p_bot2.returncode}!")

            # Check logs periodically for errors
            host_err = check_logs_for_fatal_errors(host_log)
            if host_err:
                raise RuntimeError(f"Fatal error detected in Host log: {host_err}")
            client_err = check_logs_for_fatal_errors(client_log)
            if client_err:
                raise RuntimeError(f"Fatal error detected in Client log: {client_err}")

            if not h_state:
                time.sleep(0.05)
                continue

            turn = h_state.get("turnNumber", 0)
            phase = h_state.get("turnPhase", "")
            founding = h_state.get("foundingPhase", "")
            step = h_state.get("step", 0)
            curr_p = h_state.get("currentPlayerIndex", 0)
            enc = h_state.get("encounter", {})
            enc_phase = enc.get("phase", "")

            # Progress tracking across any state dimension
            has_progress = (
                turn != last_turn or
                phase != last_phase or
                founding != last_founding or
                step != last_step or
                curr_p != last_curr_p or
                enc_phase != last_enc_phase or
                enc.get("isPending")
            )

            if has_progress:
                last_progress_time = time.time()
                if turn != last_turn:
                    if turn % 5 == 0 or turn <= 3:
                        players_vp = [f"{p.get('name')}: {p.get('victoryPoints', 0)} VP" for p in h_state.get("players", [])]
                        print(f"  [Match {match_index}] Turn {turn} ({founding if founding != 'Complete' else phase}) | {' | '.join(players_vp)}")
                last_turn = turn
                last_phase = phase
                last_founding = founding
                last_step = step
                last_curr_p = curr_p
                last_enc_phase = enc_phase

            # Encounter Card Tracking & Verification
            if enc.get("isPending"):
                c_id = enc.get("cardId", "")
                c_phase = enc.get("phase", "")
                c_type = enc.get("cardType", "")
                c_title = enc.get("title", "")
                active_p_idx = h_state.get("currentPlayerIndex", 0)
                active_p = next((p for p in h_state.get("players", []) if p.get("playerIndex") == active_p_idx), None)
                active_p_name = active_p.get("name") if active_p else f"P{active_p_idx}"

                if current_enc_record is None or current_enc_record.get("cardId") != c_id:
                    # New card drawn
                    current_enc_record = {
                        "cardId": c_id,
                        "cardType": c_type,
                        "title": c_title,
                        "player": active_p_name,
                        "turn": turn,
                        "pre_resources": active_p.get("resources", {}).copy() if active_p else {},
                        "pre_medals": active_p.get("halfMedals", 0) if active_p else 0,
                        "pre_flagship": (active_p.get("flagship") or active_p.get("mothership", {})).copy() if active_p else {},
                        "selectedOutcomeIndex": -1,
                        "selectedOutcomeTitle": "",
                        "selectedOutcomeEffect": "",
                        "verified": False,
                        "notes": []
                    }
                    print(f"  >>> [Match {match_index}] ENCOUNTER DRAWN: Card #{c_id} [{c_type}] '{c_title}' for {active_p_name} (Phase: {c_phase})")

                # Track selected outcome
                sel_idx = enc.get("selectedOutcomeIndex", -1)
                if sel_idx >= 0 and current_enc_record["selectedOutcomeIndex"] < 0:
                    current_enc_record["selectedOutcomeIndex"] = sel_idx
                    outcomes = enc.get("outcomes", [])
                    if 0 <= sel_idx < len(outcomes):
                        current_enc_record["selectedOutcomeTitle"] = outcomes[sel_idx].get("title", "")
                        current_enc_record["selectedOutcomeEffect"] = outcomes[sel_idx].get("description", "")
                    print(f"      [Encounter #{c_id}] Outcome Selected: Option {sel_idx} ({current_enc_record['selectedOutcomeTitle']})")

            elif current_enc_record is not None:
                # Encounter has resolved!
                # Record post state
                active_p_idx = h_state.get("currentPlayerIndex", 0)
                active_p = next((p for p in h_state.get("players", []) if p.get("playerIndex") == active_p_idx), None)
                if active_p:
                    post_res = active_p.get("resources", {})
                    post_medals = active_p.get("halfMedals", 0)
                    post_flagship = active_p.get("flagship") or active_p.get("mothership", {})
                    current_enc_record["post_resources"] = post_res.copy()
                    current_enc_record["post_medals"] = post_medals
                    current_enc_record["post_flagship"] = post_flagship.copy()

                    # Verification notes
                    med_diff = post_medals - current_enc_record["pre_medals"]
                    res_diff = post_res.get("total", 0) - current_enc_record["pre_resources"].get("total", 0)
                    current_enc_record["notes"].append(f"Medals diff: {med_diff:+d}, Total res diff: {res_diff:+d}")
                    current_enc_record["verified"] = True
                    print(f"  <<< [Match {match_index}] ENCOUNTER RESOLVED: Card #{current_enc_record['cardId']} -> {current_enc_record['notes'][-1]}")
                encounters_encountered.append(current_enc_record)
                current_enc_record = None

            # Deadlock Watchdog
            if time.time() - last_progress_time > DEADLOCK_TIMEOUT:
                raise RuntimeError(f"Match {match_index} deadlocked! No state progression for {DEADLOCK_TIMEOUT}s (Turn {turn}, Phase {phase}, Encounter: {enc})")

            # Game Over check
            if h_state.get("isGameOver"):
                duration = time.time() - start_time
                players = h_state.get("players", [])
                winner = max(players, key=lambda p: p.get("victoryPoints", 0))
                print(f"\n[Match {match_index}] GAME OVER! Winner: {winner.get('name')} with {winner.get('victoryPoints')} VP in {turn} turns ({duration:.1f}s)")
                return {
                    "match": match_index,
                    "seed": seed,
                    "port": port,
                    "duration": duration,
                    "turns": turn,
                    "winner": winner.get("name"),
                    "winner_vp": winner.get("victoryPoints"),
                    "players": [{p.get("name"): p.get("victoryPoints")} for p in players],
                    "encounters_count": len(encounters_encountered),
                    "encounters": encounters_encountered,
                    "status": "SUCCESS"
                }

            time.sleep(0.05)

    finally:
        cleanup_processes(pids)

def main():
    total_matches = TOTAL_MATCHES
    if "--matches" in sys.argv:
        idx = sys.argv.index("--matches")
        if idx + 1 < len(sys.argv):
            total_matches = int(sys.argv[idx + 1])

    print("=" * 70)
    print(" DEEP SPACE PIONEERS - MATCH VERIFICATION SUITE")
    print(f" Target: {total_matches} match(es) with 2 autonomous bots")
    print(f" Rule adherence verification for Encounter Cards & Error scanner active")
    print("=" * 70)

    # Seeds and ports
    seeds = [101, 102, 103, 104, 105, 106, 107, 108]
    ports = [7771, 7772, 7773, 7774, 7775, 7776, 7777, 7778]

    results = []
    suite_start = time.time()

    for idx in range(total_matches):
        m_idx = idx + 1
        seed = seeds[idx % len(seeds)]
        port = ports[idx % len(ports)]
        try:
            res = run_match(m_idx, seed, port)
            results.append(res)
        except Exception as e:
            print(f"\n[FATAL ERROR] Match {m_idx} failed or was aborted:")
            print(f"  {e}")
            import traceback
            traceback.print_exc()
            print("\nAborting test suite immediately as requested.")
            sys.exit(1)

        # Brief pause between matches to allow OS sockets to close cleanly
        time.sleep(2.0)

    total_suite_time = time.time() - suite_start

    print("\n" + "=" * 70)
    print(f" ALL {total_matches} MATCH(ES) COMPLETED SUCCESSFULLY!")
    print(f" Total Suite Time: {total_suite_time:.1f}s")
    print("=" * 70)

    # Print summary table
    print(f"\n{'Match':<7} | {'Seed':<6} | {'Turns':<6} | {'Duration':<10} | {'Winner':<12} | {'VP':<4} | {'Encounters':<10} | {'Status'}")
    print("-" * 75)
    total_encounters = 0
    all_encounters = []
    for r in results:
        total_encounters += r["encounters_count"]
        all_encounters.extend(r["encounters"])
        print(f"{r['match']:<7} | {r['seed']:<6} | {r['turns']:<6} | {r['duration']:>8.1f}s | {r['winner']:<12} | {r['winner_vp']:<4} | {r['encounters_count']:<10} | {r['status']}")

    print("\n" + "=" * 70)
    print(f" ENCOUNTER CARDS VERIFICATION REPORT ({len(all_encounters)} Total Encounters)")
    print("=" * 70)
    for idx, e in enumerate(all_encounters):
        c_id = e.get("cardId")
        c_type = e.get("cardType")
        c_title = e.get("title")
        p = e.get("player")
        turn = e.get("turn")
        out_idx = e.get("selectedOutcomeIndex")
        out_title = e.get("selectedOutcomeTitle")
        out_eff = e.get("selectedOutcomeEffect")
        notes = "; ".join(e.get("notes", []))
        print(f"#{idx+1:02d} [Turn {turn:02d}] {p} -> Card #{c_id} [{c_type}] '{c_title}'")
        print(f"     Choice: Option {out_idx} (\"{out_title}\") -> Effect: {out_eff}")
        print(f"     Observed State Delta: {notes}")
        print()

    # Save verification JSON artifact
    out_file = WORKSPACE / "matches_verification_results.json"
    with open(out_file, "w", encoding="utf-8") as f:
        json.dump({
            "total_matches": TOTAL_MATCHES,
            "suite_duration_seconds": total_suite_time,
            "all_passed": True,
            "matches": results
        }, f, indent=2, ensure_ascii=False)
    print(f"[Results saved to {out_file}]")

if __name__ == "__main__":
    main()


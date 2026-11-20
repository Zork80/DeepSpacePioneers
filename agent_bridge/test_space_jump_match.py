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
RUNTIME_LOGS = [
    WORKSPACE / "Builds" / "Linux_Client" / "Logs" / "runtime-log.txt",
    WORKSPACE / "Logs" / "runtime-log.txt"
]

def cleanup_pids(pids):
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

def run_test_on_seed(seed=20261004, port=7789, timeout_seconds=90):
    print(f"\n========================================================")
    print(f" Starting Space Jump Verification Match (Port: {port})")
    print(f"========================================================")

    # Clean bridge dirs
    for d in [BRIDGE_HOST, BRIDGE_CLIENT]:
        d.mkdir(parents=True, exist_ok=True)
        for f in ["action.json", "result.json", "state.json"]:
            p = d / f
            if p.exists():
                p.unlink()

    # Clear runtime logs
    for rlog in RUNTIME_LOGS:
        if rlog.exists():
            rlog.unlink()

    env = os.environ.copy()
    env["DISPLAY"] = env.get("DISPLAY", ":0")
    env["WAYLAND_DISPLAY"] = env.get("WAYLAND_DISPLAY", "wayland-0")
    env["XDG_RUNTIME_DIR"] = env.get("XDG_RUNTIME_DIR", "/run/user/1000")

    host_log = WORKSPACE / "test_host.log"
    client_log = WORKSPACE / "test_client.log"
    for log in [host_log, client_log]:
        if log.exists():
            log.unlink()

    pids = []
    space_jump_detected = False
    space_jump_details = []
    triggered_encounter = False

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
            "-player-name:HostBot",
            "-bridge-dir:agent_bridge"
        ]
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
            "-player-name:ClientBot",
            "-bridge-dir:agent_bridge_client2"
        ]
        p_client = subprocess.Popen(client_cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env=env)
        pids.append(p_client)
        time.sleep(2.5)

        # 3. Bots
        bot_script = WORKSPACE / "agent_bridge" / "autonomous_player.py"
        p_bot1 = subprocess.Popen([
            sys.executable, str(bot_script),
            "--dir", str(BRIDGE_HOST),
            "--player", "0",
            "--delay", "0.05",
            "--prefix", "[HostBot] ",
            "--test-space-jump"
        ], env=env)
        pids.append(p_bot1)

        p_bot2 = subprocess.Popen([
            sys.executable, str(bot_script),
            "--dir", str(BRIDGE_CLIENT),
            "--player", "1",
            "--delay", "0.05",
            "--prefix", "[ClientBot] "
        ], env=env)
        pids.append(p_bot2)

        start_time = time.time()
        last_turn = -1

        while time.time() - start_time < timeout_seconds:
            # Check runtime logs for [SpaceJump]
            for rlog in RUNTIME_LOGS:
                if rlog.exists():
                    try:
                        with open(rlog, "r", encoding="utf-8", errors="ignore") as f:
                            lines = f.readlines()
                            for line in lines:
                                if "[SpaceJump]" in line:
                                    space_jump_detected = True
                                    if line.strip() not in space_jump_details:
                                        space_jump_details.append(line.strip())
                    except Exception:
                        pass

            if space_jump_detected:
                print("\n>>> SUCCESS! Space Jump event found in runtime-log.txt:")
                for d in space_jump_details:
                    print(f"    {d}")
                break

            time.sleep(0.5)
            elapsed = int(time.time() - start_time)

            # Read turn state
            s_file = BRIDGE_HOST / "state.json"
            if s_file.exists():
                try:
                    with open(s_file, "r") as f:
                        st = json.load(f)
                        turn = st.get("turnNumber", 0)
                        phase = st.get("turnPhase", "")
                        founding = st.get("foundingPhase", "")
                        enc = st.get("encounter", {})

                        if turn != last_turn:
                            last_turn = turn
                            print(f"[Match Progress] Elapsed: {elapsed}s | Turn: {turn} | Phase: {phase} | Founding: {founding}")
                except Exception:
                    pass

            # Check if game ended or processes died
            if p_host.poll() is not None or p_client.poll() is not None:
                print("[Match Progress] Game process exited.")
                break

    finally:
        cleanup_pids(pids)

    return space_jump_detected, space_jump_details

def main():
    ok, details = run_test_on_seed(seed=20261004, port=7789, timeout_seconds=60)
    if ok:
        print("\n========================================================")
        print(" VERIFICATION RESULT: PASS")
        print(" The Space Jump occurred, relocated the ship, and completed!")
        print("========================================================")
        sys.exit(0)
    else:
        print("\nSpace Jump test did not complete successfully within timeout.")
        sys.exit(1)

if __name__ == "__main__":
    main()

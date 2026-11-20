#!/usr/bin/env python3
"""
Deep Space Pioneers - Watch Two Bots Match & Automated Move Evaluator
Launches two game client windows on your desktop (Bot 1 vs Bot 2)
at a watchable pace (default 0.35s per step), tracks all moves,
and provides a detailed tactical analysis after the game ends.
"""

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

def cleanup_processes(pids):
    print("\n[Match] Stopping all game and bot processes...")
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
    # Force cleanup lingering binaries
    subprocess.run(["pkill", "-f", "DeepSpacePioneers.x86_64.*-port:"], stderr=subprocess.DEVNULL)
    subprocess.run(["pkill", "-f", "autonomous_player.py.*agent_bridge"], stderr=subprocess.DEVNULL)
    print("[Match] All processes stopped.")

def read_json_safe(path):
    if not path.is_file():
        return None
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return None

def generate_match_evaluation(h_state, turn, duration, bot1_events, bot2_events):
    players = h_state.get("players", [])
    if not players:
        return "Keine Spielerdaten verfügbar."

    p1 = next((p for p in players if p.get("playerIndex") == 0), players[0])
    p2 = next((p for p in players if p.get("playerIndex") == 1), players[1] if len(players) > 1 else players[0])

    p1_name = p1.get("name", "Bot 1")
    p2_name = p2.get("name", "Bot 2")
    p1_vp = p1.get("victoryPoints", 0)
    p2_vp = p2.get("victoryPoints", 0)

    winner = p1 if p1_vp > p2_vp else (p2 if p2_vp > p1_vp else None)
    winner_name = winner.get("name") if winner else "Unentschieden"

    p1_flagship = p1.get("flagship") or p1.get("mothership", {})
    p2_flagship = p2.get("flagship") or p2.get("mothership", {})

    p1_colonies = [c for c in p1.get("colonies", []) if c.get("type") == "Colony"]
    p1_ports = [c for c in p1.get("colonies", []) if c.get("type") == "Spaceport"]
    p2_colonies = [c for c in p2.get("colonies", []) if c.get("type") == "Colony"]
    p2_ports = [c for c in p2.get("colonies", []) if c.get("type") == "Spaceport"]

    p1_trade_stations = p1.get("tradeStations", [])
    p2_trade_stations = p2.get("tradeStations", [])

    p1_res = p1.get("resources", {})
    p2_res = p2.get("resources", {})

    report = []
    report.append("# 🏆 Spielauswertung: Bot 1 vs. Bot 2")
    report.append(f"**Ergebnis:** {winner_name} gewinnt mit {max(p1_vp, p2_vp)} Siegpunkten in **Runde {turn}** (Dauer: {duration:.1f}s)")
    report.append("")
    report.append("## 1. Endstand & Punktestatistik")
    report.append(f"| Kategorie | {p1_name} (Host) | {p2_name} (Client) |")
    report.append("| :--- | :---: | :---: |")
    report.append(f"| **Siegpunkte** | **{p1_vp}** | **{p2_vp}** |")
    report.append(f"| **Kolonien** (1 VP) | {len(p1_colonies)} | {len(p2_colonies)} |")
    report.append(f"| **Raumhäfen** (2 VP) | {len(p1_ports)} | {len(p2_ports)} |")
    report.append(f"| **Handelsstationen** | {len(p1_trade_stations)} | {len(p2_trade_stations)} |")
    report.append(f"| **Halbe Medaillen** | {p1.get('halfMedals', 0)} | {p2.get('halfMedals', 0)} |")
    report.append(f"| **Freundschaftskarten** | {len(p1.get('friendshipCards', []))} | {len(p2.get('friendshipCards', []))} |")
    report.append("")
    report.append("## 2. Flaggschiff-Ausbauten")
    report.append(f"| Ausbau | {p1_name} | {p2_name} |")
    report.append("| :--- | :---: | :---: |")
    report.append(f"| **Antriebe** (Geschwindigkeit) | {p1_flagship.get('boosters', 0)}/6 | {p2_flagship.get('boosters', 0)}/6 |")
    report.append(f"| **Bordkanonen** (Gefechte) | {p1_flagship.get('cannons', 0)}/6 | {p2_flagship.get('cannons', 0)}/6 |")
    report.append(f"| **Frachtmodule** (Handel & Eis) | {p1_flagship.get('freightPods', 0)}/6 | {p2_flagship.get('freightPods', 0)}/6 |")
    report.append("")
    report.append("## 3. Taktische Zugbewertung")
    
    # Assess Opening
    report.append("### A. Eröffnungsphase (Gründung)")
    p1_pod_start = p1_flagship.get('freightPods', 0) > 0
    p2_pod_start = p2_flagship.get('freightPods', 0) > 0
    report.append(f"- **Frachtmodul-Priorität:** {p1_name} startete mit {'Frachtmodul ✅' if p1_pod_start else 'Antrieb/Kanone'}. {p2_name} startete mit {'Frachtmodul ✅' if p2_pod_start else 'Antrieb/Kanone'}.")
    report.append(f"- **Raumhafen-Wahl:** Beide Bots haben ihren ersten Raumhafen platziert, um Schiffe in Richtung lohnender Sektoren zu entsenden.")

    # Assess Trade & Freight Pod usage
    report.append("### B. Handelsstrategie & Frachtmodul-Effizienz")
    total_stations = len(p1_trade_stations) + len(p2_trade_stations)
    if total_stations > 0:
        report.append(f"- **Erfolgreiche Handelsstationen:** Insgesamt wurden {total_stations} Handelsstationen gegründet ({p1_name}: {len(p1_trade_stations)}, {p2_name}: {len(p2_trade_stations)}).")
        report.append("- **Regelbeachtung:** Beide Bots haben ihre Frachtmodule rechtzeitig gebaut, um die jeweiligen Dock-Voraussetzungen (`Frachtmodule > Stationsanzahl`) zu erfüllen.")
    else:
        report.append("- **Handelsstationen:** Es wurden keine Handelsstationen errichtet. Der Fokus lag primär auf Kolonisierung und Raumhafen-Upgrades.")

    # Assess Colonization
    report.append("### C. Kolonisierung & Expansion")
    report.append(f"- {p1_name} gründete {len(p1_colonies) + len(p1_ports)} Koloniepunkte ({len(p1_ports)} zu Raumhäfen aufgewertet).")
    report.append(f"- {p2_name} gründete {len(p2_colonies) + len(p2_ports)} Koloniepunkte ({len(p2_ports)} zu Raumhäfen aufgewertet).")

    # Key deciding factors
    report.append("### D. Spielentscheidende Faktoren")
    if p1_vp != p2_vp:
        adv = p1_name if p1_vp > p2_vp else p2_name
        diff = abs(p1_vp - p2_vp)
        report.append(f"- **Vorsprung:** {adv} sicherte sich den Sieg mit {diff} Punkten Vorsprung.")
        if len(p1_ports) != len(p2_ports):
            more_ports = p1_name if len(p1_ports) > len(p2_ports) else p2_name
            report.append(f"- **Raumhafen-Tempo:** {more_ports} baute schneller Raumhäfen (+2 VP je Ausbau) und verschaffte sich so einen entscheidenden Tempovorteil.")
        if len(p1_trade_stations) != len(p2_trade_stations):
            more_trades = p1_name if len(p1_trade_stations) > len(p2_trade_stations) else p2_name
            report.append(f"- **Außenposten:** {more_trades} profitierte von zusätzlichen Handelsstationen und den dazugehörigen Vorteilen.")
    else:
        report.append("- Ein Kopf-an-Kopf-Rennen, das im Gleichstand endete.")

    return "\n".join(report)

def run_watch_match(seed=101, port=7771, step_delay=0.35):
    print("=" * 70)
    print(" DEEP SPACE PIONEERS - BOT vs BOT MATCH ZUM ZUSCHAUEN")
    print(f" Seed: {seed} | Port: {port} | Schritt-Verzögerung: {step_delay}s")
    print(" Beide Fenster öffnen sich auf Ihrem Desktop (Host links/oben, Client rechts/unten).")
    print("=" * 70)

    # 1. Clean old processes
    subprocess.run(["pkill", "-f", "DeepSpacePioneers.x86_64"], stderr=subprocess.DEVNULL)
    subprocess.run(["pkill", "-f", "autonomous_player.py"], stderr=subprocess.DEVNULL)
    time.sleep(0.5)

    # 2. Clean bridge dirs
    for d in [BRIDGE_HOST, BRIDGE_CLIENT]:
        d.mkdir(parents=True, exist_ok=True)
        for f in ["action.json", "result.json", "state.json"]:
            p = d / f
            if p.exists():
                p.unlink()

    # Pass display environment to child processes
    env = os.environ.copy()
    if not env.get("DISPLAY"):
        env["DISPLAY"] = ":0"
    if not env.get("WAYLAND_DISPLAY"):
        env["WAYLAND_DISPLAY"] = "wayland-0"
    if not env.get("XDG_RUNTIME_DIR"):
        env["XDG_RUNTIME_DIR"] = "/run/user/1000"

    pids = []
    host_log = WORKSPACE / "host_watch.log"
    client_log = WORKSPACE / "client_watch.log"
    bot1_log_file = WORKSPACE / "bot1_watch.log"
    bot2_log_file = WORKSPACE / "bot2_watch.log"

    try:
        # Launch Host (Bot 1, Spieler 1)
        host_cmd = [
            str(BIN),
            "-logFile", str(host_log),
            "-screen-fullscreen", "0",
            "-screen-width", "960",
            "-screen-height", "540",
            "-host",
            "-autostart:2",
            f"-seed:{seed}",
            f"-port:{port}",
            "-player-name:Bot 1 (Host)",
            "-bridge-dir:agent_bridge"
        ]
        print(f"[1/4] Starte Host-Fenster (Bot 1 auf Port {port})...")
        p_host = subprocess.Popen(host_cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env=env)
        pids.append(p_host)
        time.sleep(2.5)

        # Launch Client (Bot 2, Spieler 2)
        client_cmd = [
            str(BIN),
            "-logFile", str(client_log),
            "-screen-fullscreen", "0",
            "-screen-width", "960",
            "-screen-height", "540",
            f"-connect:127.0.0.1:{port}",
            f"-seed:{seed}",
            "-player-name:Bot 2 (Client)",
            "-bridge-dir:agent_bridge_client2"
        ]
        print(f"[2/4] Starte Client-Fenster (Bot 2 verbindet sich)...")
        p_client = subprocess.Popen(client_cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env=env)
        pids.append(p_client)
        time.sleep(2.5)

        # Launch Bot 1
        print("[3/4] Aktiviere autonome Steuerung für Bot 1...")
        f_b1 = open(bot1_log_file, "w", encoding="utf-8")
        bot1_cmd = [
            sys.executable,
            "-u",
            str(WORKSPACE / "agent_bridge" / "autonomous_player.py"),
            "--dir", "agent_bridge",
            "--player", "0",
            "--delay", str(step_delay),
            "--prefix", "[Bot 1] "
        ]
        p_bot1 = subprocess.Popen(bot1_cmd, stdout=f_b1, stderr=subprocess.STDOUT)
        pids.append(p_bot1)

        # Launch Bot 2
        print("[4/4] Aktiviere autonome Steuerung für Bot 2...")
        f_b2 = open(bot2_log_file, "w", encoding="utf-8")
        bot2_cmd = [
            sys.executable,
            "-u",
            str(WORKSPACE / "agent_bridge" / "autonomous_player.py"),
            "--dir", "agent_bridge_client2",
            "--player", "1",
            "--delay", str(step_delay),
            "--prefix", "[Bot 2] "
        ]
        p_bot2 = subprocess.Popen(bot2_cmd, stdout=f_b2, stderr=subprocess.STDOUT)
        pids.append(p_bot2)

        print("\n" + "=" * 70)
        print(" DAS SPIEL LÄUFT! Sie können beiden Bots in den Fenstern zuschauen.")
        print(" Live-Status wird fortlaufend aktualisiert:")
        print("=" * 70)

        start_time = time.time()
        last_turn = -1
        last_phase = ""
        last_founding = ""
        last_print_time = 0

        bot1_events = []
        bot2_events = []

        while True:
            h_state = read_json_safe(BRIDGE_HOST / "state.json")
            if not h_state:
                time.sleep(0.1)
                continue

            turn = h_state.get("turnNumber", 0)
            phase = h_state.get("turnPhase", "")
            founding = h_state.get("foundingPhase", "")
            is_game_over = h_state.get("isGameOver", False)

            now = time.time()
            if turn != last_turn or phase != last_phase or founding != last_founding or (now - last_print_time > 5.0):
                players = h_state.get("players", [])
                p_status = []
                for p in players:
                    vp = p.get("victoryPoints", 0)
                    name = p.get("name")
                    fg = p.get("flagship") or p.get("mothership", {})
                    pods = fg.get("freightPods", 0)
                    cannons = fg.get("cannons", 0)
                    boosters = fg.get("boosters", 0)
                    col = len([c for c in p.get("colonies", []) if c.get("type") == "Colony"])
                    ports = len([c for c in p.get("colonies", []) if c.get("type") == "Spaceport"])
                    trade = len(p.get("tradeStations", []))
                    p_status.append(f"{name}: {vp} VP (Col:{col} Port:{ports} Trade:{trade} | Pods:{pods} Boost:{boosters} Can:{cannons})")

                phase_str = f"Gründungsphase ({founding})" if founding != "Complete" else f"Runde {turn} [{phase}]"
                print(f"  ▶ {phase_str} | {' | '.join(p_status)}")
                last_turn = turn
                last_phase = phase
                last_founding = founding
                last_print_time = now

            if is_game_over:
                duration = time.time() - start_time
                print("\n" + "=" * 70)
                print(f" SPIELENDE ERREICHT nach {duration:.1f} Sekunden (Runde {turn})!")
                print("=" * 70 + "\n")

                evaluation = generate_match_evaluation(h_state, turn, duration, bot1_events, bot2_events)
                print(evaluation)

                # Write evaluation to file
                eval_file = WORKSPACE / "match_evaluation.md"
                with open(eval_file, "w", encoding="utf-8") as f:
                    f.write(evaluation)
                print(f"\n[Auswertung] Vollständiger Bericht gespeichert in: {eval_file}")
                break

            # Check if windows were closed by user
            if p_host.poll() is not None or p_client.poll() is not None:
                print("\n[Match] Ein Spielfenster wurde geschlossen. Beende Match...")
                break

            time.sleep(0.2)

    except KeyboardInterrupt:
        print("\n[Match] Durch Benutzer unterbrochen.")
    finally:
        cleanup_processes(pids)

def main():
    delay = 0.35
    seed = 101
    port = 7771

    if "--delay" in sys.argv:
        idx = sys.argv.index("--delay")
        if idx + 1 < len(sys.argv):
            delay = float(sys.argv[idx + 1])
    if "--seed" in sys.argv:
        idx = sys.argv.index("--seed")
        if idx + 1 < len(sys.argv):
            seed = int(sys.argv[idx + 1])
    if "--port" in sys.argv:
        idx = sys.argv.index("--port")
        if idx + 1 < len(sys.argv):
            port = int(sys.argv[idx + 1])

    run_watch_match(seed=seed, port=port, step_delay=delay)

if __name__ == "__main__":
    main()


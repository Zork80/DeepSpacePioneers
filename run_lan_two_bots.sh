#!/usr/bin/env bash
# ==============================================================================
# Deep Space Pioneers - Automated 2-Player LAN Game with Autonomous Bots
#
# This script:
#  1. Launches the Host game instance (listening on port 7777 as "Spieler 1")
#  2. Launches the Client game instance (connecting to 127.0.0.1:7777 as "Spieler 2")
#  3. Enforces a fixed board seed for reproducible comparisons across runs
#  4. Automatically starts the LAN game as soon as Spieler 2 joins
#  5. Launches Autonomous Bot 1 for Spieler 1 (Host)
#  6. Launches Autonomous Bot 2 for Spieler 2 (Client)
#  7. Handles Ctrl+C (SIGINT/SIGTERM) to cleanly shut down all 4 processes
#
# Usage:
#   ./run_lan_two_bots.sh [SEED] [PORT] [BOT_DELAY]
#
# Examples:
#   ./run_lan_two_bots.sh                  # Uses default seed 20261004, port 7777, delay 0.5s
#   ./run_lan_two_bots.sh 42               # Uses fixed seed 42
#   ./run_lan_two_bots.sh 20261004 7777 0.2
# ==============================================================================
set -e

export DISPLAY="${DISPLAY:-:0}"
export WAYLAND_DISPLAY="${WAYLAND_DISPLAY:-wayland-0}"
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/1000}"

SEED="${1:-20261004}"
PORT="${2:-7777}"
DELAY="${3:-0.5}"
EXTRA_FLAGS="${4:-}"

WORKSPACE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BIN="$WORKSPACE_DIR/Builds/Linux_Client/DeepSpacePioneers.x86_64"

if [ ! -f "$BIN" ]; then
    echo "[Error] Binary not found at $BIN."
    echo "Please build the Linux client first via Unity: Deep Space Pioneers -> Build -> 1. Build Linux Client"
    exit 1
fi

BRIDGE_HOST="agent_bridge"
BRIDGE_CLIENT="agent_bridge_client2"

echo "============================================================"
echo " Deep Space Pioneers - Automated 2-Bot LAN Game Launcher"
echo " Workspace:        $WORKSPACE_DIR"
echo " Board Seed:       $SEED (Fixed)"
echo " LAN Port:         $PORT"
echo " Bot Step Delay:   ${DELAY}s"
echo " Host Bridge:      $BRIDGE_HOST"
echo " Client Bridge:    $BRIDGE_CLIENT"
echo "============================================================"

# Ensure bridge directories exist and clean up old artifacts
mkdir -p "$WORKSPACE_DIR/$BRIDGE_HOST" "$WORKSPACE_DIR/$BRIDGE_CLIENT"
rm -f "$WORKSPACE_DIR/$BRIDGE_HOST/action.json" "$WORKSPACE_DIR/$BRIDGE_HOST/result.json" "$WORKSPACE_DIR/$BRIDGE_HOST/state.json"
rm -f "$WORKSPACE_DIR/$BRIDGE_CLIENT/action.json" "$WORKSPACE_DIR/$BRIDGE_CLIENT/result.json" "$WORKSPACE_DIR/$BRIDGE_CLIENT/state.json"

PIDS=()
cleanup() {
    trap - EXIT INT TERM
    set +e
    echo ""
    echo "[Launcher] Stopping all processes..."
    for pid in "${PIDS[@]}"; do
        if kill -0 "$pid" 2>/dev/null; then
            kill "$pid" 2>/dev/null || true
        fi
    done
    sleep 0.5
    for pid in "${PIDS[@]}"; do
        if kill -0 "$pid" 2>/dev/null; then
            kill -9 "$pid" 2>/dev/null || true
        fi
    done
    echo "[Launcher] All processes terminated. Clean exit."
    exit 0
}
trap cleanup EXIT INT TERM

# 1. Start Host Instance with fixed seed
echo "[Launcher] 1/4 Launching Host (Spieler 1) with seed $SEED on port $PORT..."
"$BIN" -logFile "$WORKSPACE_DIR/host_player.log" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -host -autostart:2 -seed:"$SEED" -port:"$PORT" -player-name:"Spieler 1" -bridge-dir:"$BRIDGE_HOST" $EXTRA_FLAGS > /dev/null 2>&1 &
HOST_PID=$!
PIDS+=($HOST_PID)
echo "[Launcher] Host PID: $HOST_PID. Waiting 2.5s for server initialization..."
sleep 2.5

# 2. Start Client Instance with matching seed
echo "[Launcher] 2/4 Launching Client (Spieler 2) connecting to 127.0.0.1:$PORT..."
"$BIN" -logFile "$WORKSPACE_DIR/client_player.log" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -connect:"127.0.0.1:$PORT" -seed:"$SEED" -player-name:"Spieler 2" -bridge-dir:"$BRIDGE_CLIENT" $EXTRA_FLAGS > /dev/null 2>&1 &
CLIENT_PID=$!
PIDS+=($CLIENT_PID)
echo "[Launcher] Client PID: $CLIENT_PID. Waiting 2.5s for connection and game auto-start..."
sleep 2.5

# 3. Start Bot for Host (Spieler 1)
echo "[Launcher] 3/4 Launching Bot 1 for Host (Spieler 1)..."
python3 "$WORKSPACE_DIR/agent_bridge/autonomous_player.py" --dir "$BRIDGE_HOST" --player 0 --delay "$DELAY" --prefix "[Host-Bot] " &
BOT1_PID=$!
PIDS+=($BOT1_PID)

# 4. Start Bot for Client (Spieler 2)
echo "[Launcher] 4/4 Launching Bot 2 for Client (Spieler 2)..."
python3 "$WORKSPACE_DIR/agent_bridge/autonomous_player.py" --dir "$BRIDGE_CLIENT" --player 1 --delay "$DELAY" --prefix "[Client-Bot] " &
BOT2_PID=$!
PIDS+=($BOT2_PID)

echo "============================================================"
echo " [Launcher] All 2 game instances and 2 bots are active!"
echo " Fixed Seed: $SEED"
echo " Press Ctrl+C at any time to gracefully stop all processes."
echo "============================================================"

wait

#!/usr/bin/env bash
# ==============================================================================
# Dual Autonomous Player Bot Launcher
# Runs two bot processes in parallel, each connected to its dedicated client.
#
# Usage:
#   ./agent_bridge/run_two_bots.sh [DIR1] [DIR2] [DELAY]
#
# Defaults:
#   DIR1: agent_bridge          (Client 1 / Unity Editor)
#   DIR2: agent_bridge_client2  (Client 2)
#   DELAY: 0.5                  (Step delay in seconds)
# ==============================================================================

DIR1="${1:-agent_bridge}"
DIR2="${2:-agent_bridge_client2}"
DELAY="${3:-0.5}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BOT_SCRIPT="${SCRIPT_DIR}/autonomous_player.py"

echo "============================================================"
echo " Starting Dual Autonomous Bots"
echo " Bot 1 Directory: ${DIR1}"
echo " Bot 2 Directory: ${DIR2}"
echo " Step Delay:      ${DELAY}s"
echo "============================================================"

# Ensure directories exist
mkdir -p "${DIR1}" "${DIR2}"

cleanup() {
    echo ""
    echo "[DualBots] Stopping bots..."
    kill $PID1 $PID2 2>/dev/null
    wait $PID1 $PID2 2>/dev/null
    echo "[DualBots] Both bots stopped."
}

trap cleanup SIGINT SIGTERM

python3 "${BOT_SCRIPT}" --dir "${DIR1}" --delay "${DELAY}" 2>&1 | sed -e 's/^/[Bot 1] /' &
PID1=$!

python3 "${BOT_SCRIPT}" --dir "${DIR2}" --delay "${DELAY}" 2>&1 | sed -e 's/^/[Bot 2] /' &
PID2=$!

wait $PID1 $PID2


#!/usr/bin/env bash
# ==============================================================================
# Watch Two Bots Play (Bot 1 vs Bot 2)
# Opens two game windows side-by-side so you can watch every move live.
# Evaluates both bots' strategies upon match completion.
#
# Usage:
#   ./agent_bridge/watch_two_bots.sh [DELAY] [SEED] [PORT]
#
# Examples:
#   ./agent_bridge/watch_two_bots.sh        (default: delay 0.35s, seed 101)
#   ./agent_bridge/watch_two_bots.sh 0.4
# ==============================================================================

set -e

DELAY="${1:-0.35}"
SEED="${2:-101}"
PORT="${3:-7771}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Ensure display environment is present for desktop viewing
export DISPLAY="${DISPLAY:-:0}"
export WAYLAND_DISPLAY="${WAYLAND_DISPLAY:-wayland-0}"
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/1000}"

chmod +x "${SCRIPT_DIR}/watch_two_bots.py"

python3 "${SCRIPT_DIR}/watch_two_bots.py" --delay "${DELAY}" --seed "${SEED}" --port "${PORT}"


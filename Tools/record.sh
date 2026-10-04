#!/usr/bin/env bash
# Records the scripted showcase playthrough to an mp4 (fixed 30 fps timestep, game audio included).
#   Tools/record.sh [Recordings/agent-clicker-gameplay.mp4] [width] [height]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/Recordings/agent-clicker-gameplay.mp4}")"
W="${2:-1920}"; H="${3:-1080}"
mkdir -p "$(dirname "$OUT")"
timeout -s KILL 3000 "$ROOT/Tools/play.sh" -screen-width "$W" -screen-height "$H" -record "$OUT" -logFile /tmp/agentclicker-record.log > /dev/null 2>&1 || true
grep -E "\[Video\] done|\[Demo\] could not|Exception" /tmp/agentclicker-record.log || true
ls -la "$OUT"

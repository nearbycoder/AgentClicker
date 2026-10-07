#!/usr/bin/env bash
# Long-session check: a late-game office left alone for N minutes (the day autopilot runs the days), sampling memory,
# object counts and frame times every 30 s into soak.csv and the player log. Throwaway save and settings.
#   Tools/soak.sh [minutes, default 60] [output dir, default Logs/soak] [extra player args]
# Runs a copy of Builds/Linux (in Builds/Soak), so the project can be rebuilt while it runs.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MIN="${1:-60}"
OUT="$(realpath -m "${2:-$ROOT/Logs/soak}")"
shift 2 || shift $# || true
[ -x "$ROOT/Builds/Linux/AgentClicker.x86_64" ] || { echo "No build yet. Run Tools/unity.sh build first." >&2; exit 1; }
rm -rf "$OUT" "$ROOT/Builds/Soak"; mkdir -p "$OUT"
cp -a "$ROOT/Builds/Linux" "$ROOT/Builds/Soak"
export XDG_CONFIG_HOME="$ROOT/Logs/player-home/config"; mkdir -p "$XDG_CONFIG_HOME"
args=(-screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "$OUT/player.log" -soak "$OUT" "$MIN" -daylength 30)
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
timeout -s KILL $(( MIN * 60 + 180 )) "$ROOT/Builds/Soak/AgentClicker.x86_64" "${args[@]}" "$@" > /dev/null 2>&1 || true
grep -E "\[Soak\] (start|done)|Exception|NullReference" "$OUT/player.log" | head -50 || true
grep -c "\[Autopilot\] clock out" "$OUT/player.log" | xargs echo "days clocked out by the autopilot:"

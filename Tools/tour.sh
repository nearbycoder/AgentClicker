#!/usr/bin/env bash
# Screenshot tour: the built game plays through every phase (throwaway save) and writes PNGs.
#   Tools/tour.sh [output dir, default Logs/tour] [player args, e.g. -screen-width 1280 -screen-height 800]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# absolute: the player resolves relative paths against its own folder
OUT="$(realpath -m "${1:-$ROOT/Logs/tour}")"
shift || true
rm -rf "$OUT"; mkdir -p "$OUT"
# Unity writes its own window/session prefs on every run; keep them (and anything else) out of ~/.config
export XDG_CONFIG_HOME="$ROOT/Logs/player-home/config"; mkdir -p "$XDG_CONFIG_HOME"
timeout -s KILL 420 "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" -tour "$OUT" "$@" > /dev/null 2>&1 || true
grep -E "\[Tour\]|Exception|NullReference" "$OUT/player.log" || true
ls "$OUT"/*.png 2>/dev/null | wc -l | xargs echo "screenshots:"

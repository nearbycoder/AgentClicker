#!/usr/bin/env bash
# Screenshot tour: the built game plays through every phase (throwaway save) and writes PNGs.
#   Tools/tour.sh [/tmp/agentclicker-tour]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-/tmp/agentclicker-tour}"
rm -rf "$OUT"; mkdir -p "$OUT"
timeout -s KILL 300 "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" -tour "$OUT" > /dev/null 2>&1 || true
grep -E "\[Tour\]|Exception|NullReference" "$OUT/player.log" || true
ls "$OUT"/*.png 2>/dev/null | wc -l | xargs echo "screenshots:"

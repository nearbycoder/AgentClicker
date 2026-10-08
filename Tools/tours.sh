#!/usr/bin/env bash
# The screenshot tour at every window shape it's checked at, one after another, each in a private nested KWin:
# 16:9 (1600×900), 4:3 (1024×768), 21:9 (1680×720), 16:10 (1280×800, the Steam Deck's shape) and 32:9 (2560×720).
#   Tools/tours.sh [output prefix, default Logs/tour] [WxH ...]
#   Tools/tours.sh Logs/r11/final            # Logs/r11/final-1600x900/, ... plus a .out and .load per size
#   Tools/tours.sh Logs/x 2560x720            # just one size
# Waits while the load average is above 24 (input-driven checks have failed on an overloaded machine).
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PREFIX="${1:-$ROOT/Logs/tour}"; shift || true
SIZES=("$@"); [ ${#SIZES[@]} -gt 0 ] || SIZES=(1600x900 1024x768 1680x720 1280x800 2560x720)
load() { cut -d' ' -f1 /proc/loadavg; }
for size in "${SIZES[@]}"; do
  w="${size%x*}"; h="${size#*x}"; out="$PREFIX-$size"
  while awk "BEGIN{exit !($(load) > 24)}"; do echo "[tours] load $(load), waiting"; sleep 30; done
  uptime > "$out.load"
  nice -n 10 "$ROOT/Tools/nested.sh" --size "$size" "$ROOT/Tools/tour.sh" "$out" -screen-width "$w" -screen-height "$h" > "$out.out" 2>&1
  uptime >> "$out.load"
  echo "[tours] $size: $(grep -c '\[Tour\] PASS' "$out.out") passed, $(grep -c '\[Tour\] FAIL' "$out.out") failed, $(grep -cE 'Exception|NullReference' "$out.out") exceptions ($out.out)"
done

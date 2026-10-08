#!/usr/bin/env bash
# Graphics fidelity, step by step: the built game holds one frame still in the office, title and monitor views,
# screenshots it at every step (Low, Medium, High, Ultra), then measures uncapped frame times for each step and view.
# Runs in a private nested KWin (Tools/nested.sh), waiting while the load average is above 24, and notes the load.
#   Tools/fidelity.sh [output dir, default Logs/fidelity] [WxH, default 1600x900] [player args, e.g. -force-vulkan]
#   Tools/fidelity.sh Logs/r12/fidelity        # office_0_low.png ... monitor_3_ultra.png, player.log, run.out, run.load
#   Tools/fidelity.sh Logs/x 1600x900 -force-vulkan   # Vulkan reports GPU frame times (OpenGL, the default here, does not)
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/Logs/fidelity}")"; SIZE="${2:-1600x900}"; shift 2 || shift $#
w="${SIZE%x*}"; h="${SIZE#*x}"
rm -rf "$OUT"; mkdir -p "$OUT"
load() { cut -d' ' -f1 /proc/loadavg; }
while awk "BEGIN{exit !($(load) > 24)}"; do echo "[fidelity] load $(load), waiting"; sleep 30; done
uptime > "$OUT/run.load"
# nested.sh gives everything inside it a scratch config folder (deleted afterwards), so the player starts from default
# settings and never touches ~/.config
nice -n 10 "$ROOT/Tools/nested.sh" --size "$SIZE" timeout -s KILL 300 "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" \
  -fidelity "$OUT" -screen-width "$w" -screen-height "$h" "$@" > "$OUT/run.out" 2>&1
uptime >> "$OUT/run.load"
grep -E "\[Fidelity\]|Exception|NullReference" "$OUT/player.log" | grep -v "\] still " || true
ls "$OUT"/*.png 2>/dev/null | wc -l | xargs echo "screenshots:"

#!/usr/bin/env bash
# Records the trailer shots and stills from the built game, then edits them into docs/media.
#   Tools/make_trailer.sh            record + edit (about 20 minutes; needs Builds/Linux, ffmpeg, python3)
#   Tools/make_trailer.sh --edit     edit only, reusing Recordings/trailer
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/Recordings/trailer"
if [ "${1:-}" != "--edit" ]; then
  rm -rf "$OUT/clips" "$OUT/stills"
  mkdir -p "$OUT"
  # pass 1: every shot as its own clip at a fixed 30 fps timestep, game SFX only, plus the music loops as WAVs
  timeout -s KILL 2400 "$ROOT/Tools/play.sh" -screen-width 1920 -screen-height 1080 \
    -logFile "$OUT/record.log" -trailer "$OUT" > /dev/null 2>&1 || true
  # pass 2: the same script in real time, saving cursor-free screenshots
  timeout -s KILL 900 "$ROOT/Tools/play.sh" -screen-width 1920 -screen-height 1080 \
    -logFile "$OUT/stills.log" -trailer-stills "$OUT" > /dev/null 2>&1 || true
  grep -h -E "\[Trailer\] done|could not find|Exception" "$OUT/record.log" "$OUT/stills.log" || true
fi
VENV="$ROOT/.venv"
[ -x "$VENV/bin/python" ] || { python3 -m venv "$VENV" && "$VENV/bin/pip" install -q pillow numpy; }
"$VENV/bin/python" "$ROOT/Tools/make_trailer.py"

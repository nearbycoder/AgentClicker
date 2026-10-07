#!/usr/bin/env bash
# Home-screen icons for the browser build's page, from the template's icon.svg (needs rsvg-convert).
#   Tools/web_icons.sh
set -euo pipefail
T="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Unity/Assets/WebGLTemplates/AgentClicker"
for s in 180 192 512; do rsvg-convert -w "$s" -h "$s" "$T/icon.svg" -o "$T/icon-$s.png"; done
ls -la "$T"/icon-*.png

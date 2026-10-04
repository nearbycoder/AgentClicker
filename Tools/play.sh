#!/usr/bin/env bash
# Run the built Linux player. On CachyOS/Wayland the X11 (XWayland) path hangs at startup,
# so use Unity's native Wayland backend whenever a Wayland session is available.
# Defaults to a 1600x900 window; pass -screen-width/-screen-height to override.
set -euo pipefail
GAME="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Builds/Linux/AgentClicker.x86_64"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build first." >&2; exit 1; }
args=(-screen-fullscreen 0)
[[ " $* " == *" -screen-width "* ]] || args+=(-screen-width 1600)
[[ " $* " == *" -screen-height "* ]] || args+=(-screen-height 900)
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
exec "$GAME" "${args[@]}" "$@"

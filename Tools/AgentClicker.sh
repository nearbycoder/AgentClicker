#!/usr/bin/env bash
# Agent Clicker launcher (ships next to AgentClicker.x86_64 in the Linux zip).
# On a Wayland desktop the player can hang at startup through XWayland, so this starts it with Unity's
# native Wayland backend. Set AGENTCLICKER_X11=1 to skip that. Other arguments (-reset, -daylength 120…)
# are passed through.
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
args=()
if [ -n "${WAYLAND_DISPLAY:-}" ] && [ -z "${AGENTCLICKER_X11:-}" ] && [[ " $* " != *" -force-wayland "* ]]; then
  args+=(-force-wayland)
fi
exec "$DIR/AgentClicker.x86_64" "${args[@]}" "$@"

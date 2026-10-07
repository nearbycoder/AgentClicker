#!/usr/bin/env bash
# Runs a command (normally Tools/tour.sh or Tools/benchmark.sh) inside a private nested KWin: its own
# Wayland socket, D-Bus session and config folder, closed afterwards. The game window opens there, not on
# the shared desktop, so it can't go fullscreen on the real desktop and the real pointer can't reach it.
#
#   Tools/nested.sh [--size WxH] [--keep] <command> [args...]
#   Tools/nested.sh Tools/tour.sh
#   Tools/nested.sh --size 1024x768 Tools/tour.sh Logs/tour-4x3 -screen-width 1024 -screen-height 768
#
# --size is the nested desktop's size (default 1920x1080). --keep leaves the scratch folder
# (Logs/nested-<pid>/, with KWin's log) for a look afterwards. The exit code is the command's. Only the
# processes started here are stopped, by PID.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SIZE=1920x1080; KEEP=0
while [ $# -gt 0 ]; do
  case "$1" in
    --size) SIZE="$2"; shift 2 ;;
    --keep) KEEP=1; shift ;;
    *) break ;;
  esac
done
[ $# -gt 0 ] || { echo "usage: $0 [--size WxH] [--keep] <command> [args...]" >&2; exit 2; }
for tool in kwin_wayland dbus-run-session; do
  command -v "$tool" > /dev/null || { echo "nested.sh: needs $tool (KDE's KWin and D-Bus)" >&2; exit 2; }
done
[ -n "${XDG_RUNTIME_DIR:-}" ] || { echo "nested.sh: XDG_RUNTIME_DIR isn't set" >&2; exit 2; }

SCRATCH="$ROOT/Logs/nested-$$"
rm -rf "$SCRATCH"; mkdir -p "$SCRATCH/config"
export AC_NESTED_SOCK="agentclicker-nested-$$" AC_NESTED_DIR="$SCRATCH"
export AC_NESTED_W="${SIZE%x*}" AC_NESTED_H="${SIZE#*x}"
# KWin reads and writes its settings in the scratch folder; tour.sh and benchmark.sh set their own for the player.
export XDG_CONFIG_HOME="$SCRATCH/config"

set +e
dbus-run-session -- bash -c '
  kwin_wayland --virtual --socket "$AC_NESTED_SOCK" --width "$AC_NESTED_W" --height "$AC_NESTED_H" \
    --no-lockscreen --no-global-shortcuts > "$AC_NESTED_DIR/kwin.log" 2>&1 & KW=$!
  trap "kill \$KW 2>/dev/null; wait \$KW 2>/dev/null" EXIT
  for i in $(seq 40); do [ -S "$XDG_RUNTIME_DIR/$AC_NESTED_SOCK" ] && break; sleep 0.25; done
  [ -S "$XDG_RUNTIME_DIR/$AC_NESTED_SOCK" ] || { echo "nested.sh: KWin did not start (see $AC_NESTED_DIR/kwin.log)" >&2; exit 3; }
  echo "[nested] kwin $KW on $AC_NESTED_SOCK (${AC_NESTED_W}x$AC_NESTED_H)"
  # No X11 display inside: the player must use the nested Wayland socket.
  env -u DISPLAY WAYLAND_DISPLAY="$AC_NESTED_SOCK" "$@" & G=$!
  wait $G; code=$?
  echo "[nested] command exited $code"
  exit $code
' nested "$@"
code=$?
set -e
left=$(pgrep -f "$AC_NESTED_SOCK" || true)
[ -z "$left" ] || echo "[nested] warning: still running with this session's socket: $left" >&2
[ "$KEEP" = 1 ] || rm -rf "$SCRATCH"
exit $code

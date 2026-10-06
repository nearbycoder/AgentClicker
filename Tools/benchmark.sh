#!/usr/bin/env bash
# Performance benchmark of the built game (uncapped frame rate, late-game office).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$ROOT/Logs"
# Unity writes its own window/session prefs on every run; keep them (and anything else) out of ~/.config
export XDG_CONFIG_HOME="$ROOT/Logs/player-home/config"; mkdir -p "$XDG_CONFIG_HOME"
LOG="$(realpath -m "${1:-$ROOT/Logs/benchmark.log}")"
timeout -s KILL 200 "$ROOT/Tools/play.sh" -logFile "$LOG" -benchmark "${@:2}" > /dev/null 2>&1 || true
grep -E "\[Bench\]|Exception" "$LOG" || true
grep -m1 -E "Renderer:|GL_RENDERER|Vulkan.*device" "$LOG" || true

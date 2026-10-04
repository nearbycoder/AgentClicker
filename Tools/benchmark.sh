#!/usr/bin/env bash
# Performance benchmark of the built game (uncapped frame rate, late-game office).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG="${1:-/tmp/agentclicker-bench.log}"
timeout -s KILL 200 "$ROOT/Tools/play.sh" -logFile "$LOG" -benchmark "${@:2}" > /dev/null 2>&1 || true
grep -E "\[Bench\]|Exception" "$LOG" || true
grep -m1 -E "Renderer:|GL_RENDERER|Vulkan.*device" "$LOG" || true

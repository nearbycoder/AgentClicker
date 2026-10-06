#!/usr/bin/env bash
# Helper for running the Unity editor headless against this project.
#   Tools/unity.sh setup | tests | build | build-mac | build-webgl | scene | exec <Method>
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/Unity"
UNITY="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
# Arch ships libxml2.so.16; the editor still links against .so.2.
COMPAT="${UNITY_COMPAT_LIBS:-$HOME/.local/share/ptt-unity-libs}"
[ -d "$COMPAT" ] && export LD_LIBRARY_PATH="$COMPAT${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
LOG_DIR="$ROOT/Logs"; mkdir -p "$LOG_DIR"

run() { local log="$1"; shift; "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$LOG_DIR/$log" "$@"; }

case "${1:-}" in
  setup)  run setup.log -quit -executeMethod AgentClicker.EditorTools.ProjectSetup.Run ;;
  scene)  run scene.log -quit -executeMethod AgentClicker.EditorTools.SceneBuilder.BuildFromCommandLine ;;
  build)  run build.log -quit -executeMethod AgentClicker.EditorTools.BuildScript.BuildLinux ;;
  build-mac) run build-mac.log -quit -buildTarget OSXUniversal -executeMethod AgentClicker.EditorTools.BuildScript.BuildMac ;;
  build-webgl) run build-webgl.log -quit -buildTarget WebGL -executeMethod AgentClicker.EditorTools.BuildScript.BuildWebGL ;;
  build-dev) run build.log -quit -executeMethod AgentClicker.EditorTools.BuildScript.BuildLinuxDev ;;
  tests)  run tests.log -runTests -testPlatform EditMode -testResults "$LOG_DIR/test-results.xml" || true
          python3 - "$LOG_DIR/test-results.xml" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
print(f"tests: total={r.get('total')} passed={r.get('passed')} failed={r.get('failed')} skipped={r.get('skipped')}")
for tc in r.iter('test-case'):
    if tc.get('result') != 'Passed':
        msg = tc.find('failure/message')
        print("FAIL", tc.get('fullname'), (msg.text or '').strip()[:800] if msg is not None else '')
for out in r.iter('output'):
    if out.text and any(k in out.text for k in ('[Balance]', '[Endless', '[Goals]')): print(out.text.strip())
PY
          ;;
  exec)   shift; m="$1"; shift; run exec.log -quit -executeMethod "$m" "$@" ;;
  *) echo "usage: $0 setup|scene|build|build-mac|build-webgl|tests|exec <Method>"; exit 2 ;;
esac

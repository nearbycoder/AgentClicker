#!/usr/bin/env bash
# Release zips from the existing builds (run Tools/unity.sh build / build-mac / build-webgl first):
#   Tools/package.sh v0.2.0   →  Builds/Release/AgentClicker-v0.2.0-{linux-x86_64,macos-universal,webgl}.zip
# Unity's do-not-ship folders are left out. The Linux zip gets the AgentClicker.sh launcher. The WebGL zip has
# index.html at its root, ready for a static host. Nothing is uploaded.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:?usage: Tools/package.sh <version, e.g. v0.2.0>}"
OUT="$ROOT/Builds/Release"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$OUT"

stage() { # stage <source dir> <dest dir>: copy a build without Unity's debug folders
  mkdir -p "$2"
  cp -a "$1"/. "$2"/
  find "$2" -depth \( -name '*_BackUpThisFolder_ButDontShipItWithYourGame' -o -name '*_BurstDebugInformation_DoNotShip' \) -exec rm -rf {} +
}

zipit() { # zipit <zip name> <dir to zip from> <entries...>
  local zip="$OUT/$1"; shift
  local from="$1"; shift
  rm -f "$zip"
  bsdtar -a -cf "$zip" -C "$from" "$@"
  echo "$(du -h "$zip" | cut -f1)  $zip"
}

if [ -x "$ROOT/Builds/Linux/AgentClicker.x86_64" ]; then
  stage "$ROOT/Builds/Linux" "$STAGE/linux/AgentClicker"
  install -m 755 "$ROOT/Tools/AgentClicker.sh" "$STAGE/linux/AgentClicker/AgentClicker.sh"
  zipit "AgentClicker-$VERSION-linux-x86_64.zip" "$STAGE/linux" AgentClicker
else echo "skip Linux: no Builds/Linux (Tools/unity.sh build)"; fi

if [ -d "$ROOT/Builds/Mac/Agent Clicker.app" ]; then
  stage "$ROOT/Builds/Mac/Agent Clicker.app" "$STAGE/mac/Agent Clicker.app"
  zipit "AgentClicker-$VERSION-macos-universal.zip" "$STAGE/mac" "Agent Clicker.app"
else echo "skip macOS: no Builds/Mac (Tools/unity.sh build-mac)"; fi

if [ -f "$ROOT/Builds/WebGL/index.html" ]; then
  stage "$ROOT/Builds/WebGL" "$STAGE/webgl"
  mapfile -t entries < <(ls -A "$STAGE/webgl")
  zipit "AgentClicker-$VERSION-webgl.zip" "$STAGE/webgl" "${entries[@]}"
else echo "skip WebGL: no Builds/WebGL (Tools/unity.sh build-webgl)"; fi

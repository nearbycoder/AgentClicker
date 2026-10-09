#!/usr/bin/env bash
# Builds the browser version and lays it out as a GitHub Pages site in Builds/Pages (gitignored):
# index.html at the root, .nojekyll, Build/ with Brotli files that decompress themselves (Pages can't send
# Content-Encoding headers), and only relative URLs, so it works under https://nearbycoder.github.io/AgentClicker/.
#
#   Tools/build-pages.sh            build with Unity, then lay out Builds/Pages
#   Tools/build-pages.sh --no-build lay out Builds/Pages from the existing Builds/WebGL
#
# Deploying is a copy of Builds/Pages/. onto the gh-pages branch. Check a served copy with Tools/check-pages.mjs <url>.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WEBGL="$ROOT/Builds/WebGL"
SITE="$ROOT/Builds/Pages"

if [ "${1:-}" != "--no-build" ]; then
  # file names are content hashes, so earlier builds' files would pile up next to the new ones
  rm -rf "$WEBGL/Build"
  nice -n 10 "$ROOT/Tools/unity.sh" build-webgl || { echo "build failed: see Logs/build-webgl.log" >&2; exit 1; }
fi
[ -f "$WEBGL/index.html" ] && [ -d "$WEBGL/Build" ] || { echo "no browser build in $WEBGL" >&2; exit 1; }

rm -rf "$SITE"
mkdir -p "$SITE"
cp -r "$WEBGL/." "$SITE/"
touch "$SITE/.nojekyll" # serve the files as they are, no Jekyll
chmod -R a+rX "$SITE"   # Unity writes some build files owner-only

# GitHub rejects files over 100 MB and warns over 50 MB
largest=$(find "$SITE" -type f -printf '%s %P\n' | sort -n | tail -1)
size=${largest%% *}
if [ "$size" -ge $((100 * 1024 * 1024)) ]; then echo "too big for GitHub: $largest" >&2; exit 1; fi
if [ "$size" -ge $((50 * 1024 * 1024)) ]; then echo "warning: over GitHub's 50 MB advice: $largest" >&2; fi
grep -q 'src="/\|href="/' "$SITE/index.html" && { echo "index.html has a root-absolute URL; Pages serves under /AgentClicker/" >&2; exit 1; }

first=$(find "$SITE" -type f ! -name 'icon-*' ! -name '*.svg' ! -name 'manifest.webmanifest' ! -name '.nojekyll' -printf '%s\n' | awk '{s+=$1} END {print s}')
echo "site: $SITE"
echo "total $(du -sh --apparent-size "$SITE" | cut -f1), first download $(awk -v b="$first" 'BEGIN {printf "%.1f MB", b / 1048576}'), largest file ${largest#* } ($(awk -v b="$size" 'BEGIN {printf "%.1f MB", b / 1048576}'))"
find "$SITE" -type f -printf '%10s  %P\n' | sort -k2

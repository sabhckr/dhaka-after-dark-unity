#!/bin/sh
# Render a golden view in the macOS development player and compare it with Tests/Golden.
#
#   Tools/shot.sh <view> [--build] [--accept]
#
# Builds Builds/MacBench first if it is missing or --build is given. Output goes to
# Evidence/local/shot-<view>-<time>/: shot.png, shot.json, player.log. --accept copies the new
# shot over Tests/Golden/<view>.png (look at it first). Exits 0 when the shot matches the golden
# (or was accepted), 2 when it differs, 1 when the run failed. The editor must not have the
# project open.
set -eu
here=$(cd "$(dirname "$0")/.." && pwd)
view=""; build=0; accept=0
for a in "$@"; do
  case $a in
    --build) build=1 ;;
    --accept) accept=1 ;;
    -*) echo "unknown option $a"; exit 2 ;;
    *) view=$a ;;
  esac
done
[ -n "$view" ] || { echo "usage: Tools/shot.sh <view> [--build] [--accept]"; exit 2; }
unity=${UNITY:-/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' "$here/ProjectSettings/ProjectVersion.txt")/Unity.app/Contents/MacOS/Unity}
app="$here/Builds/MacBench/DhakaAfterDark.app"
mkdir -p "$here/Evidence/local"
if [ "$build" = 1 ] || [ ! -d "$app" ]; then
  "$unity" -batchmode -projectPath "$here" -executeMethod Dad.EditorTools.ProjectTools.BuildMacBench -logFile "$here/Evidence/local/build-macbench.log" \
    || { echo "build failed; see Evidence/local/build-macbench.log"; exit 1; }
  cat "$here/Evidence/local/build-macbench.json"
fi
exe="$app/Contents/MacOS/$(ls "$app/Contents/MacOS" | head -1)"
golden="$here/Tests/Golden/$view.png"
dir="$here/Evidence/local/shot-$view-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$dir"
set +e
"$exe" -dad-shot "$view" -dad-shot-out "$dir" -dad-shot-golden "$golden" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "$dir/player.log" &
pid=$!
# The watchdog keeps no stdout or stderr open, so a caller piping this script is not held for 90 s.
( sleep 90; kill "$pid" 2>/dev/null ) >/dev/null 2>&1 &
watchdog=$!
wait "$pid"
code=$?
kill "$watchdog" 2>/dev/null
wait "$watchdog" 2>/dev/null
set -e
if [ ! -f "$dir/shot.json" ] || [ ! -f "$dir/shot.png" ]; then
  echo "no shot (player exit $code); see $dir/player.log"
  exit 1
fi
node -e '
const s = require(process.argv[1]);
console.log(`${s.view}: ${s.width}x${s.height} on ${s.build} (${s.gpu}, ${s.graphicsApi}); ${s.resident} chunks resident, settled in ${s.settleSeconds.toFixed(1)} s, ${s.failedPolygons} polygons failed`);
console.log(s.compared ? `vs golden: mean diff ${s.meanAbsDiff.toFixed(3)} (limit ${s.maxMeanDiff}), changed ${(s.changedFraction*100).toFixed(3)} % (limit ${(s.maxChangedFraction*100).toFixed(1)} %): ${s.pass ? "PASS" : "FAIL"}` : `not compared: ${s.error || "no golden"}`);
' "$dir/shot.json"
echo "$dir"
if [ "$accept" = 1 ]; then
  mkdir -p "$here/Tests/Golden"
  cp "$dir/shot.png" "$golden"
  echo "accepted as $golden"
  exit 0
fi
exit $code

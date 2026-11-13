#!/bin/sh
# Run the fixed camera-path benchmark in the macOS development player and print the summary.
#
#   Tools/bench.sh [path] [--build] [--capture] [--capture-from N] [--no-vsync] [--traffic] [--colliders] [--drive] [--npcs] [--quality low|medium|high]
#
# Builds Builds/MacBench first if it is missing or --build is given. Results go to
# Evidence/local/bench-<path>-<time>/: summary.json, frames.csv, player.log and, with
# --capture, capture.raw of 2,000 frames, from sampled frame N with --capture-from N (open it in
# the Unity Profiler, or list its markers with Tools/capture-report.sh). The player opens a
# 1280x720 window and quits by itself; a watchdog kills it after 180 s. Exits 1 if there is no
# summary or the path did not finish. The editor must not have the project open.
#
# "main thread over 4 ms" is the Phase C rule read per frame: frames after the set-up rows whose
# main thread took more than 4 ms; "near streaming" ones had streaming work (0.3 ms or more) or a
# garbage collection within 3 frames, and are listed with a *. The rest need a capture to say why.
# Each is listed as row:ms/cpu, cpu being the most main-thread CPU time (mainThreadCpuMs, macOS)
# in that row or one either side: a slow frame with no more CPU than usual was not running.
set -eu
here=$(cd "$(dirname "$0")/.." && pwd)
path=hub-rich-air; build=0; extra=""; next=""
for a in "$@"; do
  if [ "$next" = from ]; then extra="$extra -dad-bench-capture-from $a"; next=""; continue; fi
  if [ "$next" = quality ]; then extra="$extra -dad-quality $a"; next=""; continue; fi
  case $a in
    --capture-from) extra="$extra -dad-bench-capture"; next=from ;;
    --quality) next=quality ;;
    --build) build=1 ;;
    --capture) extra="$extra -dad-bench-capture" ;;
    --no-vsync) extra="$extra -dad-bench-no-vsync" ;;
    --traffic) extra="$extra -dad-traffic" ;;
    --colliders) extra="$extra -dad-colliders" ;;
    --npcs) extra="$extra -dad-npcs" ;;
    --drive) extra="$extra -dad-traffic -dad-colliders -dad-drive" ;;
    -*) echo "unknown option $a"; exit 2 ;;
    *) path=$a ;;
  esac
done
unity=${UNITY:-/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' "$here/ProjectSettings/ProjectVersion.txt")/Unity.app/Contents/MacOS/Unity}
# BENCH_APP runs another player, as an A/B against an older build does (E-04); --build is for the default one.
app="${BENCH_APP:-$here/Builds/MacBench/DhakaAfterDark.app}"
mkdir -p "$here/Evidence/local"
if [ "$build" = 1 ] || [ ! -d "$app" ]; then
  "$unity" -batchmode -projectPath "$here" -executeMethod Dad.EditorTools.ProjectTools.BuildMacBench -logFile "$here/Evidence/local/build-macbench.log" \
    || { echo "build failed; see Evidence/local/build-macbench.log"; exit 1; }
  cat "$here/Evidence/local/build-macbench.json"
fi
exe="$app/Contents/MacOS/$(ls "$app/Contents/MacOS" | head -1)"
dir="$here/Evidence/local/bench-$path-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$dir"
set +e
# shellcheck disable=SC2086
"$exe" -dad-bench "$path" -dad-bench-out "$dir" $extra -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "$dir/player.log" &
pid=$!
# The watchdog keeps no stdout or stderr open, so a caller piping this script is not held for 180 s.
( sleep 180; kill "$pid" 2>/dev/null ) >/dev/null 2>&1 &
watchdog=$!
wait "$pid"
code=$?
kill "$watchdog" 2>/dev/null
wait "$watchdog" 2>/dev/null
set -e
if [ ! -f "$dir/summary.json" ]; then
  echo "no summary.json (player exit $code); see $dir/player.log"
  exit 1
fi
node -e '
const s = require(process.argv[1]);
const f = (k) => s[k] && s[k].samples ? `p50 ${s[k].p50.toFixed(2)}  p95 ${s[k].p95.toFixed(2)}  p99 ${s[k].p99.toFixed(2)}  max ${s[k].max.toFixed(2)} ms` : "unavailable";
console.log(`${s.path} on ${s.build}, ${s.device} / ${s.gpu} (${s.graphicsApi}), ${s.width}x${s.height}, vSync ${s.vSyncCount}`);
console.log(`complete: ${s.complete} (path ${s.pathSeconds.toFixed(1)} s)`);
console.log(`${s.frames} frames in ${s.seconds.toFixed(1)} s; over 20 ms: ${s.framesOver20ms}; over 33.3 ms: ${s.framesOver33_3ms} (by CPU frame time: ${s.cpuFramesOver33_3ms}); origin shifts ${s.originShifts}`);
for (const k of ["frameMs", "cpuMs", "mainThreadMs", "mainThreadCpuMs", "renderThreadMs", "gpuMs"]) console.log(k.padEnd(15), f(k));
console.log(`main thread over 4 ms after set-up: ${s.mainFramesOver4ms}, near streaming ${s.mainFramesOver4msNearStreaming}${(s.mainFramesOver4msList || []).length ? " (" + s.mainFramesOver4msList.join(" ") + (s.mainFramesOver4ms > s.mainFramesOver4msList.length ? " ..." : "") + ")" : ""}`);
console.log(`streaming: ${s.streamingApplied} applied, ${s.streamingUnloaded} unloaded; apply in ${s.streamingApplyFrames} frames: ${f("streamingApplyMs")}; streamer max frame ${s.streamingMaxApplyFrameMs.toFixed(2)} ms, max step ${s.streamingMaxApplyStepMs.toFixed(2)} ms (by kind: ${(s.streamingMaxStepMsByKind || []).map(x => x.toFixed(2)).join(", ")}); warm-up at start ${(s.streamingWarmupMs || 0).toFixed(2)} ms (${s.streamingPreparedMethods || 0} methods compiled ahead); ${s.streamingStepsWithGc || 0} apply steps with a garbage collection (slowest ${(s.streamingMaxGcStepMs || 0).toFixed(2)} ms); apply held ${s.streamingHeldFrames || 0} frames for loads still building (longest ${s.streamingMaxHeldFrames || 0})`);
console.log(`GC alloc: ${s.gcAllocTotalBytes} B total, ${s.gcAllocFrames} frames allocating, max ${s.gcAllocMaxFrameBytes} B; ${s.gcCollections} collections (incremental ${s.gcIncremental})`);
console.log(`system memory: start ${(s.systemMemoryStartBytes/1048576).toFixed(0)} MB, peak ${(s.systemMemoryPeakBytes/1048576).toFixed(0)} MB; graphics memory peak ${((s.gfxMemoryPeakBytes || 0)/1048576).toFixed(0)} MB, end ${((s.gfxMemoryEndBytes || 0)/1048576).toFixed(0)} MB`);
console.log(`  of which: end ${(s.systemMemoryEndBytes/1048576).toFixed(0)} MB; mesh memory peak ${((s.meshMemoryPeakBytes || 0)/1048576).toFixed(0)} MB, end ${((s.meshMemoryEndBytes || 0)/1048576).toFixed(0)} MB; managed heap (GC reserved) peak ${((s.gcReservedPeakBytes || 0)/1048576).toFixed(0)} MB, end ${((s.gcReservedEndBytes || 0)/1048576).toFixed(0)} MB`);
console.log(`mesh copies dropped once drawn: ${s.streamingReleased}, still held ${s.streamingStillReadable}; slowest drop ${(s.streamingMaxReleaseMs || 0).toFixed(3)} ms, slowest frame of drops ${(s.streamingMaxReleaseFrameMs || 0).toFixed(3)} ms`);
if (s.streamingKitsBuilt !== undefined) console.log(`near rooftop kits: ${s.streamingKitsBuilt} built, ${s.streamingKitsDropped} dropped, ${s.streamingKitsHeld} held at the end, ${s.streamingKitFailures} failed; uploads in ${s.streamingKitFrames} frames: ${f("streamingKitMs")}; slowest upload ${(s.streamingMaxKitUploadMs || 0).toFixed(3)} ms, slowest frame ${(s.streamingMaxKitFrameMs || 0).toFixed(3)} ms; drawn near without their kit in ${s.streamingKitLateFrames} frames since start, ${s.streamingKitLateFramesOnPath} of them on the path (at most ${s.streamingMaxKitLateCells} cells)`);
const n = (k) => s[k] && s[k].samples ? `p50 ${Math.round(s[k].p50)} p95 ${Math.round(s[k].p95)} max ${Math.round(s[k].max)}` : "unavailable";
console.log(`render per frame (means of 10-frame blocks): batches ${n("batches")}; SetPass calls ${n("setPassCalls")}; triangles ${n("triangles")}; shadow casters ${n("shadowCasters")}`);
console.log(`unavailable: [${s.unavailable.join(", ")}]  capture: ${s.capture || "none"} (${s.captureFrames} frames)`);
' "$dir/summary.json"
echo "$dir"
node -e '
const s = require(process.argv[1]);
// The macOS player times frames from the display: with the display asleep or the window hidden, the
// clock stands still (every frame reads Unity'"'"'s 1e-5 s minimum) and the path never moves (seen in D-01).
if (!s.complete && s.seconds > 10 && s.pathSeconds < 0.05 * s.seconds) console.log("the frame clock stood still (path " + s.pathSeconds.toFixed(2) + " s in " + s.seconds.toFixed(0) + " s): display asleep or player window hidden? Run again with the display on");
process.exit(s.complete ? 0 : 1)' "$dir/summary.json" || { echo "path did not finish"; exit 1; }
exit "$code"

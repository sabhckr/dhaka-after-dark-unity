#!/bin/sh
# List the markers of a profiler capture without opening the Profiler window.
#
#   Tools/capture-report.sh <bench folder or capture.raw> [busy ms, default 4]
#
# Runs Dad.EditorTools.CaptureReport in batch mode, which writes markers.tsv next to the capture
# (main thread and render thread, every marker of 0.05 ms or more, per frame). Then prints the
# frames whose main thread was busy (PlayerLoop minus waiting for vsync and the GPU) for longer
# than the limit, or took longer than 20 ms, with their largest markers. The editor must not have
# the project open.
set -eu
here=$(cd "$(dirname "$0")/.." && pwd)
target=${1:?usage: Tools/capture-report.sh <bench folder or capture.raw> [busy ms]}
busy=${2:-4}
[ -d "$target" ] && target="$target/capture.raw"
target=$(cd "$(dirname "$target")" && pwd)/$(basename "$target")
[ -f "$target" ] || { echo "no capture at $target"; exit 1; }
unity=${UNITY:-/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' "$here/ProjectSettings/ProjectVersion.txt")/Unity.app/Contents/MacOS/Unity}
tsv="$(dirname "$target")/markers.tsv"
"$unity" -batchmode -quit -projectPath "$here" -executeMethod Dad.EditorTools.CaptureReport.Run \
  -dad-capture "$target" -dad-capture-out "$tsv" -logFile "$(dirname "$target")/capture-report.log" \
  || { echo "capture report failed; see $(dirname "$target")/capture-report.log"; exit 1; }
head -1 "$tsv"
python=/Library/Developer/CommandLineTools/usr/bin/python3
[ -x "$python" ] || python=python3
"$python" -I "$here/Tools/slow_frames.py" "$tsv" "$busy" 20

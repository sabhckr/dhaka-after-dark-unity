#!/bin/sh
# Compile the Unity DADC reader with the editor's bundled Roslyn compiler and run it on
# the editor's bundled Mono against Assets/StreamingAssets/World. Needs no Unity licence.
#
#   Tools/dadc-check.sh
set -eu
here=$(cd "$(dirname "$0")/.." && pwd)
version=${UNITY_VERSION:-6000.3.27f1}
s="/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/Resources/Scripting"
lib="$s/MonoBleedingEdge/lib/mono/4.7.1-api"
out="$here/Evidence/local/dadc-check"
world="$here/Assets/StreamingAssets/World"
mkdir -p "$out"
node -e '
const w = require(process.argv[1]);
const rows = ["id\tix\tiy\tcellE\tcellN\tbytes\tsha256\tbuildings\troads\tnodes\tedges\tpositions\tsumE\tsumN\tsumInts"];
for (const c of w.chunks) rows.push([c.id, c.ix, c.iy, c.cell[0], c.cell[1], c.bytes, c.sha256, c.counts.buildings, c.counts.roads, c.counts.graphNodes, c.counts.graphEdges, c.checks.positions, c.checks.sumEastCm, c.checks.sumNorthCm, c.checks.sumInts].join("\t"));
require("fs").writeFileSync(process.argv[2], rows.join("\n") + "\n");
' "$world/world.json" "$out/expected.tsv"
"$s/NetCoreRuntime/dotnet" "$s/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -noconfig -langversion:9 -optimize+ -target:exe \
  -out:"$out/dadc-check.exe" \
  -r:"$lib/mscorlib.dll" -r:"$lib/System.dll" -r:"$lib/System.Core.dll" -r:"$lib/Facades/System.Runtime.dll" \
  "$here/Assets/_Project/Runtime/World/Dadc.cs" "$here/Tools/dadc-check/Program.cs"
"$s/MonoBleedingEdge/bin/mono" "$out/dadc-check.exe" "$world/chunks" "$out/expected.tsv"

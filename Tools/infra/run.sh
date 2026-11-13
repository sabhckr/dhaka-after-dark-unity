#!/bin/zsh
# Infrastructure survey, read-only (AXIOM v3 audit, section G): builds Dad.World and the survey outside Unity,
# surveys the slice (or the chunks given) and writes the register.
#   Tools/infra/run.sh [out dir] [chunk ids, comma-separated]
# Default out dir: Evidence/local/infra-<time> (copy to Docs/PRODUCTION/INFRA by hand). Compared with the known
# list, Tests/Infra/known-violations.csv. Needs the Unity editor's bundled .NET and Mono (no editor run).
set -e
P=${0:a:h:h:h}
H=${0:a:h}
O=${1:-$P/Evidence/local/infra-$(date +%Y%m%d-%H%M%S)}
mkdir -p $O
U=/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' $P/ProjectSettings/ProjectVersion.txt)/Unity.app/Contents/Resources/Scripting
NS=$U/NetStandard/ref/2.1.0/netstandard.dll
MS=$U/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll
CSC=($U/NetCoreRuntime/dotnet $U/DotNetSdkRoslyn/csc.dll -nologo -noconfig -nostdlib -langversion:9 -nowarn:1701,1702 -optimize+)
$CSC -target:library -unsafe -out:$O/Dad.World.dll -r:$NS $P/Assets/_Project/Runtime/World/*.cs
$CSC -target:exe -out:$O/infra.exe -r:$NS -r:$O/Dad.World.dll -r:$MS $P/Assets/_Project/Editor/Infra/*.cs $H/Infra.cs
M=$U/MonoBleedingEdge
MONO_PATH=$M/lib/mono/4.5:$M/lib/mono/4.5/Facades $M/bin/mono $O/infra.exe $P/Assets/StreamingAssets/World/chunks $O $P/Tests/Infra/known-violations.csv ${2:+$2}
echo "written to $O"

#!/bin/zsh
# K-37 pier investigation, read-only: builds Dad.World outside Unity, surveys every viaduct pier in the
# 12 slice chunks (and the two chunks north of it, for spans), classifies them and draws the plans.
#   Tools/k37/run.sh [out dir]     (default Evidence/local/k37-<time>; copy to Docs/PRODUCTION/K37 by hand)
# Needs the Unity editor's bundled .NET and Mono (no editor run) and the Command Line Tools python.
set -e
P=${0:a:h:h:h}
H=${0:a:h}
O=${1:-$P/Evidence/local/k37-$(date +%Y%m%d-%H%M%S)}
mkdir -p $O
U=/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' $P/ProjectSettings/ProjectVersion.txt)/Unity.app/Contents/Resources/Scripting
NS=$U/NetStandard/ref/2.1.0/netstandard.dll
MS=$U/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll
CSC=($U/NetCoreRuntime/dotnet $U/DotNetSdkRoslyn/csc.dll -nologo -noconfig -nostdlib -langversion:9 -nowarn:1701,1702 -optimize+)
$CSC -target:library -unsafe -out:$O/Dad.World.dll -r:$NS $P/Assets/_Project/Runtime/World/*.cs
$CSC -target:exe -out:$O/k37.exe -r:$NS -r:$O/Dad.World.dll -r:$MS $H/K37.cs
$CSC -target:exe -out:$O/lanes.exe -r:$NS -r:$O/Dad.World.dll -r:$MS $H/Lanes.cs
M=$U/MonoBleedingEdge
run() { MONO_PATH=$M/lib/mono/4.5:$M/lib/mono/4.5/Facades $M/bin/mono "$@" }
W=$P/Assets/StreamingAssets/World/chunks
run $O/k37.exe $W $O/piers.json
run $O/k37.exe $W $O/north.json c_6_-31,c_7_-31
run $O/lanes.exe $W | tee $O/lanes.txt
PY=(/Library/Developer/CommandLineTools/usr/bin/python3 -I)
$PY $H/classify.py $O/piers.json $O/k37.json $O/north.json > $O/classify.txt
$PY $H/report.py $O/k37.json $O/piers.json $O/north.json $O | tee $O/groups.txt
$PY $H/plans.py $O
$PY $H/register.py $O
echo "written to $O"

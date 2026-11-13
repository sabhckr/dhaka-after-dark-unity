#!/bin/zsh
# Building census for the master directive audit, read-only: builds Dad.World outside Unity and writes one
# row per building id for the whole exported world, then the summary tables.
#   Tools/census/run.sh [out dir]     (default Evidence/local/census-<time>)
# Needs the Unity editor's bundled .NET and Mono (no editor run) and the Command Line Tools python.
set -e
P=${0:a:h:h:h}
H=${0:a:h}
O=${1:-$P/Evidence/local/census-$(date +%Y%m%d-%H%M%S)}
mkdir -p $O
U=/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' $P/ProjectSettings/ProjectVersion.txt)/Unity.app/Contents/Resources/Scripting
NS=$U/NetStandard/ref/2.1.0/netstandard.dll
MS=$U/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll
CSC=($U/NetCoreRuntime/dotnet $U/DotNetSdkRoslyn/csc.dll -nologo -noconfig -nostdlib -langversion:9 -nowarn:1701,1702 -optimize+)
$CSC -target:library -unsafe -out:$O/Dad.World.dll -r:$NS $P/Assets/_Project/Runtime/World/*.cs
$CSC -target:exe -out:$O/census.exe -r:$NS -r:$O/Dad.World.dll -r:$MS $H/Census.cs
M=$U/MonoBleedingEdge
W=$P/Assets/StreamingAssets/World
MONO_PATH=$M/lib/mono/4.5:$M/lib/mono/4.5/Facades $M/bin/mono $O/census.exe $W/chunks $O/buildings.csv | tee $O/census.txt
/Library/Developer/CommandLineTools/usr/bin/python3 -I $H/summary.py $O/buildings.csv $W/places.json $O | tee -a $O/census.txt
echo "written to $O"

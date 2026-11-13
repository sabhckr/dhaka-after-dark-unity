#!/bin/zsh
# The traffic tests outside Unity (F-03): builds Dad.World, Dad.Traffic and TrafficTests with the editor's bundled compiler, and runs them under its Mono.
# A fast mirror for pre-checks and mutations; the Unity EditMode run stays the record. Needs no editor run.
#   Tools/traffic/run.sh [out dir] [test-name filters...]
set -e
P=${0:a:h:h:h}
O=${1:-$P/Evidence/local/traffic-tests}
[[ $# -gt 0 ]] && shift
mkdir -p $O
U=/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' $P/ProjectSettings/ProjectVersion.txt)/Unity.app/Contents/Resources/Scripting
NS=$U/NetStandard/ref/2.1.0/netstandard.dll
MS=$U/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll
NU=($P/Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll)
CSC=($U/NetCoreRuntime/dotnet $U/DotNetSdkRoslyn/csc.dll -nologo -noconfig -nostdlib -langversion:9 -nowarn:1701,1702,8321 -optimize+)
R=${SRC:-$P/Assets/_Project/Runtime}
cat > $O/UnityStub.cs <<STUB
namespace UnityEngine {
 public static class Debug { public static void Log(object o) { System.Console.WriteLine("  " + o); } }
 public static class Application { public static string streamingAssetsPath => "$P/Assets/StreamingAssets"; public static string dataPath => "$P/Assets"; }
}
STUB
cp $NU[1] $O/
$CSC -target:library -unsafe -out:$O/Dad.World.dll -r:$NS $R/World/*.cs
$CSC -target:library -out:$O/Dad.Traffic.dll -r:$NS -r:$O/Dad.World.dll $R/Traffic/*.cs
$CSC -target:exe -out:$O/tests.exe -r:$NS -r:$MS -r:$O/Dad.World.dll -r:$O/Dad.Traffic.dll -r:$O/nunit.framework.dll $P/Assets/_Project/Tests/EditMode/TrafficTests.cs $O/UnityStub.cs $P/Docs/PRODUCTION/INFRA/TestRunner.cs.txt
M=$U/MonoBleedingEdge
cd $O
MONO_PATH=$M/lib/mono/4.5:$M/lib/mono/4.5/Facades $M/bin/mono $O/tests.exe "$@"

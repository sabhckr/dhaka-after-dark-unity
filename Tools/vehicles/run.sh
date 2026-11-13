#!/bin/zsh
# The vehicle tests outside Unity (E-04): builds Dad.World, Dad.Traffic, Dad.Vehicles and the tests with the editor's bundled compiler,
# and runs them under its Mono. A fast mirror for pre-checks and mutations; the Unity EditMode run stays the record. Needs no editor run.
#   Tools/vehicles/run.sh [out dir] [test-name filters...]
set -e
P=${0:a:h:h:h}
O=${${1:-$P/Evidence/local/vehicle-tests}:a}
[[ $# -gt 0 ]] && shift
mkdir -p $O
U=/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' $P/ProjectSettings/ProjectVersion.txt)/Unity.app/Contents/Resources/Scripting
NS=$U/NetStandard/ref/2.1.0/netstandard.dll
MS=$U/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll
NU=($P/Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll)
CSC=($U/NetCoreRuntime/dotnet $U/DotNetSdkRoslyn/csc.dll -nologo -noconfig -nostdlib -langversion:9 -nowarn:1701,1702,8321 -optimize+)
R=${SRC:-$P/Assets/_Project/Runtime}
T=${SRC_TESTS:-$P/Assets/_Project/Tests/EditMode}
cat > $O/UnityStub.cs <<STUB
namespace UnityEngine {
 public static class Debug { public static void Log(object o) { System.Console.WriteLine("  " + o); } }
 public static class Application { public static string streamingAssetsPath => "$P/Assets/StreamingAssets"; public static string dataPath => "$P/Assets"; }
}
STUB
cp $NU[1] $O/
$CSC -target:library -unsafe -out:$O/Dad.World.dll -r:$NS $R/World/*.cs
$CSC -target:library -out:$O/Dad.Traffic.dll -r:$NS -r:$O/Dad.World.dll $R/Traffic/*.cs
$CSC -target:library -out:$O/Dad.Vehicles.dll -r:$NS -r:$O/Dad.World.dll -r:$O/Dad.Traffic.dll $R/Vehicles/*.cs
$CSC -target:exe -out:$O/tests.exe -r:$NS -r:$MS -r:$O/Dad.World.dll -r:$O/Dad.Traffic.dll -r:$O/Dad.Vehicles.dll -r:$O/nunit.framework.dll \
  $T/VehicleDynamicsTests.cs $T/ChunkCollisionTests.cs $T/PlayerCabinTests.cs $T/DrivenTrafficTests.cs $T/VehicleArtTests.cs $O/UnityStub.cs $P/Docs/PRODUCTION/INFRA/TestRunner.cs.txt
M=$U/MonoBleedingEdge
cd $O
MONO_PATH=$M/lib/mono/4.5:$M/lib/mono/4.5/Facades $M/bin/mono $O/tests.exe "$@"

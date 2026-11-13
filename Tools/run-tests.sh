#!/bin/sh
# Run Unity tests from the command line and report the real result.
#
#   Tools/run-tests.sh [EditMode|PlayMode] [test filter]
#
# Results XML and the editor log go to Evidence/local/. Prints the totals from
# the XML and exits non-zero if any test failed, the XML is missing, or no
# test ran. The editor must not already have this project open.
set -eu
here=$(cd "$(dirname "$0")/.." && pwd)
platform=${1:-EditMode}
filter=${2:-}
unity=${UNITY:-/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' "$here/ProjectSettings/ProjectVersion.txt")/Unity.app/Contents/MacOS/Unity}
out="$here/Evidence/local"
mkdir -p "$out"
stamp=$(date +%Y%m%d-%H%M%S)
xml="$out/tests-$platform-$stamp.xml"
log="$out/tests-$platform-$stamp.log"
set +e
"$unity" -batchmode -projectPath "$here" -runTests -testPlatform "$platform" ${filter:+-testFilter "$filter"} -testResults "$xml" -logFile "$log"
code=$?
set -e
if [ ! -f "$xml" ]; then
  echo "no results XML (editor exit $code); see $log"
  exit 1
fi
node -e '
const xml = require("fs").readFileSync(process.argv[1], "utf8");
const run = /<test-run[^>]*>/.exec(xml)[0];
const get = (k) => Number((new RegExp(` ${k}="(\\d+)"`).exec(run) || [])[1] || 0);
const t = { total: get("total"), passed: get("passed"), failed: get("failed"), skipped: get("skipped"), inconclusive: get("inconclusive") };
console.log(JSON.stringify(t));
for (const m of xml.matchAll(/<test-case [^>]*?fullname="([^"]+)"[^>]*?result="Failed"/g)) console.log("FAILED", m[1]);
process.exit(t.failed || !t.total ? 1 : 0);
' "$xml"

#!/bin/sh
# Generates the city into Assets/StreamingAssets/World. The editor must not have the project open.
#   Tools/worldgen/build.sh [seed]
set -eu
here=$(cd "$(dirname "$0")/../.." && pwd)
python=/Library/Developer/CommandLineTools/usr/bin/python3
out="$here/Assets/StreamingAssets/World"
mkdir -p "$out"
"$python" -I "$here/Tools/worldgen/gen.py" "$out" "${1:-1971}"
mv "$out/plan.svg" "$here/Docs/PRODUCTION/city-plan.svg"

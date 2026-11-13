#!/bin/zsh
# Mouth cues for the protagonists' lines (2026-11-10), by Rhubarb Lip Sync 1.14.0 (MIT, github.com/DanielSWolf/rhubarb-lip-sync).
# For each recorded line (.wav or .ogg) given, writes <line>.tsv beside it: one row per mouth change, the time in seconds and Rhubarb's mouth (A to H, X),
# which Face.Read takes (Assets/_Project/Runtime/Core/Face.cs). A text file of what is said, <line>.txt beside the sound, makes the cues better and is used when there.
#
#   Tools/characters/lipsync.sh Assets/_Project/Resources/Speech/*.wav
#
# Rhubarb is not tracked: it is unpacked in Tools/rhubarb/ (gitignored) from the release zip. Its macOS build is for Intel processors only; on Apple silicon it
# runs under Rosetta 2, which the owner installs (softwareupdate --install-rosetta: it asks to accept Apple's licence). Without it this stops and says so.
set -eu
here=${0:A:h:h:h}
rhubarb="$here/Tools/rhubarb/Rhubarb-Lip-Sync-1.14.0-macOS/rhubarb"
[[ -x $rhubarb ]] || { print -u2 "no Rhubarb at $rhubarb: unpack Rhubarb-Lip-Sync-1.14.0-macOS.zip into Tools/rhubarb/"; exit 1 }
if ! "$rhubarb" --version >/dev/null 2>&1; then
  print -u2 "Rhubarb does not run here: on Apple silicon it needs Rosetta 2 (softwareupdate --install-rosetta)."
  exit 1
fi
(( $# )) || { print -u2 "usage: $0 line.wav [line.wav ...]"; exit 1 }
for line in "$@"; do
  out="${line:r}.tsv"
  words=()
  [[ -f "${line:r}.txt" ]] && words=(--dialogFile "${line:r}.txt")
  # phonetic recognition works for any language (the lines may be Bangla); the English recogniser would need English words
  "$rhubarb" --recognizer phonetic --exportFormat tsv --extendedShapes GHX "${words[@]}" -o "$out" "$line"
  print "$out"
done

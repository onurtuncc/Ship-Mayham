#!/bin/bash
# Build one hand-authored prop headlessly: FBX + two preview PNGs.
#   Tools/BlenderAssets/build_asset.sh <AssetName> <outdir> [VARIANT]
# Needs Blender 5.1+ (BLENDER env var overrides the default Windows install path).
set -e
BLENDER="${BLENDER:-/c/Program Files/Blender Foundation/Blender 5.2/blender.exe}"
HERE="$(cd "$(dirname "$0")" && pwd)"
NAME="$1"; OUT="$2"; export VARIANT="${3:-1}"
SCRIPT="$NAME"; case "$NAME" in Character_Crew*) SCRIPT=Character_Crew;; Character_Passenger*) SCRIPT=Character_Passenger;; Character_Human*) SCRIPT=Character_Human;; esac
"$BLENDER" -b --python "$HERE/assets/$SCRIPT.py" -- "$OUT"

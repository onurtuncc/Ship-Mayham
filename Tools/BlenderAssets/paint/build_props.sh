#!/bin/bash
# usage: build_props.sh Name1 Name2 ...  -> D:/blender/propout/<Name>/ (blend, fbx, png, preview)
B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
cd /d/blender
for n in "$@"; do
  mkdir -p propout/$n
  "$B" -b --python props_pack.py -- $n D:/blender/propout/$n/$n.blend > propout/$n/build.log 2>&1
  grep -q "^BUILT" propout/$n/build.log || { echo "BUILDFAIL $n" >> propout/progress.txt; grep -E "Error|Traceback|line [0-9]+" propout/$n/build.log | head -4 >> propout/progress.txt; continue; }
  SCRIPT=paint_pipeline.py; case "$n" in GangwayGate|FishingRod) SCRIPT=paint_multi.py;; esac
  SZ=1024; case "$n" in Fish_Small|Fish_Big|Fish_Boot|SunkenCoin|Floaties|Shovel|FishingRod|DigSpot|DigHole) SZ=512;; esac
  "$B" -b propout/$n/$n.blend --python $SCRIPT -- $n D:/blender/propout/$n $SZ > propout/$n/paint.log 2>&1
  if grep -q "^PAINTED" propout/$n/paint.log; then echo "ok $n" >> propout/progress.txt; else echo "PAINTFAIL $n" >> propout/progress.txt; grep -E "Error|Traceback|line [0-9]+" propout/$n/paint.log | head -4 >> propout/progress.txt; fi
  rm -rf propout/$n/*.fbm
done
echo DONE >> propout/progress.txt

#!/bin/bash
cd /d/blender
B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
SMALL=" Lantern RopeCoil FishCrate Toolbox Anchor Bollard Buoy FuseBox "
rm -f batch14.progress
for n in Anchor Bollard Buoy CrateStack DeckBench DockPlank FishCrate FlagPole FuseBox Gangway HarborCrane HarborHut Lantern LifeRing Lighthouse MarketStall PirateShipSmall PlayerShipDeco RopeCoil Rowboat ShipBoost ShipBunting ShipCarpet ShipFunnel ShipPlanter ShipWheel Suitcase TicketCounter Toolbox Character_Crew1 Character_Crew2 Character_Crew3 Character_Crew4 Character_Passenger1 Character_Passenger2 Character_Passenger3 Character_Pirate Character_VIP; do
  sz=1024; case "$SMALL" in *" $n "*) sz=512;; esac
  mkdir -p paint/$n
  PAINT_FBX="D:/blender/repo-art/Assets/ShipMayham/Art/Generated/Models/$n.fbx" "$B" -b --python paint_pipeline.py -- $n D:/blender/paint/$n $sz > paint/$n/run.log 2>&1
  if grep -q "^PAINTED" paint/$n/run.log; then echo "ok $n" >> batch14.progress; else echo "FAIL $n" >> batch14.progress; fi
  rm -rf paint/$n/*.fbm
done
echo "ALLDONE" >> batch14.progress

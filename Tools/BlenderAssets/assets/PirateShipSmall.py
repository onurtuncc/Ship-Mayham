import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
hull = mat("Hull_Black", hexc("#2b2430")); hullT = mat("Hull_Trim", hexc("#6b4a2a")); sail = mat("Sail_Black", hexc("#1f1c24"))
bone = mat("Bone", hexc("#efe8d8")); deck = mat("Deck", hexc("#9a6a3c"))
# hull, bow toward +Y
b.box((1.9, 4.2, 0.5), (0, 0, 0.35), hull)
b.box((1.7, 3.0, 0.6), (0, -0.5, 0.9), hull)
for w_, y, z in ((1.7, 2.2, 0.45), (1.4, 2.7, 0.55), (1.0, 3.1, 0.65), (0.6, 3.45, 0.8)):
    b.box((w_, 0.5, 0.6), (0, y, z), hull)
b.box((1.3, 3.6, 0.08), (0, -0.2, 1.24), deck)
b.box((1.9, 0.1, 0.55), (0, -2.05, 1.0), hullT)
for sx in (-1, 1):
    b.box((0.08, 3.6, 0.18), (sx * 0.88, -0.2, 1.35), hullT)
    for k in range(3):
        b.cyl(0.11, 0.16, (sx * 0.97, -1.0 + k * 1.0, 0.75), metalD, segs=8, rot=(0, 90, 0))
b.cyl(0.07, 3.6, (0, 0.6, 3.0), woodD, segs=8); b.cyl(0.07, 3.0, (0, -1.0, 2.7), woodD, segs=8)
b.box((1.5, 0.05, 1.6), (0, 0.7, 3.0), sail); b.box((1.2, 0.05, 1.3), (0, -0.9, 2.8), sail)
b.box((1.7, 0.07, 0.07), (0, 0.62, 3.85), woodD); b.box((1.4, 0.07, 0.07), (0, -0.98, 3.5), woodD)
b.sphere(0.16, (0, 0.74, 3.05), bone, segs=8, rings=5)
b.box((0.22, 0.04, 0.06), (0, 0.74, 2.8), bone, rot=(0, 35, 0)); b.box((0.22, 0.04, 0.06), (0, 0.74, 2.8), bone, rot=(0, -35, 0))
b.box((1.3, 1.0, 0.6), (0, -1.55, 1.6), hullT); b.box((1.4, 1.1, 0.08), (0, -1.55, 1.94), woodD)
b.cyl(0.06, 1.2, (0, 3.9, 1.05), woodD, segs=6, rot=(-70, 0, 0))
deliver(b, "PirateShipSmall")

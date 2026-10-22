import os
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
from charlib import *
reset(); globals().update(std()); b = Builder()
V = os.environ.get("VARIANT", "1")
sets = {"1": ("#a77be0", "#f4b88a", "#f4efe2", "#3a3f6a"), "2": ("#34b8a0", "#c98a5e", "#f0b93a", "#7a4a2a"), "3": ("#f06a9a", "#fbd0aa", "#7a4a2a", "#2e6a5a")}
cloth, skinc, hatc, pantc = sets[V]
coat = mat("Coat", hexc(cloth), 0.45); hat = mat("Hat", hexc(hatc), 0.5)
bean(b, coat, mat("Pants", hexc(pantc), 0.55), mat("Shoes", hexc("#2a2018"), 0.4), mat("Skin", hexc(skinc), 0.55), torso_taper=0.7)
if V == "1":                                                             # sun hat
    b.cyl(0.4, 0.03, (0, 0, 1.31), hat, segs=32); b.lathe([(0.0, 1.55), (0.14, 1.53), (0.21, 1.42), (0.23, 1.32)], (0, 0, 0), hat, segs=28)
    b.cyl(0.215, 0.05, (0, 0, 1.37), red, segs=28, bevel=0)
elif V == "2":                                                           # straw hat
    b.cyl(0.38, 0.025, (0, 0, 1.3), hat, segs=32); b.lathe([(0.0, 1.5), (0.17, 1.48), (0.24, 1.36), (0.25, 1.3)], (0, 0, 0), hat, segs=28)
    b.cyl(0.24, 0.05, (0, 0, 1.36), mat("HatBand", hexc("#cf3a2d"), 0.5), segs=28, bevel=0)
else:                                                                    # woolly beanie with pom
    dome(b, hat, 1.27, flare=0.03); b.cyl(0.285, 0.07, (0, 0, 1.25), mat("BeanieBrim", hexc("#f4efe2"), 0.7), segs=28)
    b.sphere(0.07, (0, 0, 1.5), mat("BeanieBrim", hexc("#f4efe2"), 0.7))
b.box((0.34, 0.1, 0.24), (0.42, -0.02, 0.46), mat("Leather", hexc("#8c4a2b"), 0.5), rot=(0, 0, 0))       # little suitcase in hand
deliver(b, "Character_Passenger" + V, flip=True)

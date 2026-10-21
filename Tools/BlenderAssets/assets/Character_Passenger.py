import os
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
V = os.environ.get("VARIANT", "1")
sets = {"1": ("#8a5fb0", "#e8b48a", "#f0e8d4"), "2": ("#3f9a8a", "#c98f66", "#d9a63a"), "3": ("#c06a8a", "#f0c8a0", "#6b4a2a")}
cloth, skinc, hatc = sets[V]
coat = mat("Coat", hexc(cloth)); skin = mat("Skin", hexc(skinc)); pants = mat("Pants", hexc("#3a3a44")); shoes = mat("Shoes", hexc("#2a2018")); hat = mat("Hat", hexc(hatc))
for sx in (-1, 1):
    b.box((0.15, 0.17, 0.45), (sx * 0.1, 0, 0.38), pants); b.box((0.17, 0.24, 0.1), (sx * 0.1, -0.02, 0.05), shoes)
b.cyl(0.25, 0.55, (0, 0, 0.85), coat, segs=10, r2=0.2)
for sx in (-1, 1):
    b.box((0.11, 0.13, 0.4), (sx * 0.27, 0, 0.92), coat, rot=(0, sx * 8, 0)); b.sphere(0.065, (sx * 0.3, 0, 0.68), skin, segs=8, rings=5)
b.sphere(0.16, (0, 0, 1.33), skin, segs=12, rings=8)
b.cyl(0.26, 0.03, (0, 0, 1.46), hat, segs=14); b.cyl(0.15, 0.16, (0, 0, 1.55), hat, segs=12, r2=0.13)
b.sphere(0.02, (-0.06, -0.15, 1.35), metalD, segs=5, rings=3); b.sphere(0.02, (0.06, -0.15, 1.35), metalD, segs=5, rings=3)
deliver(b, "Character_Passenger" + V, flip=True)

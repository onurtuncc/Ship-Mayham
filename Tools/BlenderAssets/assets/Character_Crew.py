import os
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
V = os.environ.get("VARIANT", "1")
cols = {"1": "#d94a3a", "2": "#3a8fd9", "3": "#5cb85c", "4": "#e0b13a"}
shirt = mat("Crew_Shirt", hexc(cols[V])); skin = mat("Skin", hexc("#e8b48a")); pants = mat("Pants", hexc("#2c3a52")); boots = mat("Boots", hexc("#3a2616"))
for sx in (-1, 1):
    b.box((0.15, 0.17, 0.45), (sx * 0.1, 0, 0.38), pants); b.box((0.17, 0.24, 0.12), (sx * 0.1, -0.02, 0.06), boots)
b.box((0.42, 0.24, 0.5), (0, 0, 0.9), shirt)
for i in range(3):
    b.box((0.43, 0.25, 0.06), (0, 0, 0.78 + i * 0.14), cream)
for sx in (-1, 1):
    b.box((0.12, 0.14, 0.42), (sx * 0.28, 0, 0.92), shirt, rot=(0, sx * 8, 0)); b.sphere(0.07, (sx * 0.31, 0, 0.66), skin, segs=8, rings=5)
b.sphere(0.16, (0, 0, 1.33), skin, segs=12, rings=8)
b.cyl(0.165, 0.1, (0, 0, 1.43), shirt, segs=12, r2=0.15)
b.box((0.06, 0.1, 0.06), (0.12, 0.1, 1.43), shirt)
b.sphere(0.02, (-0.06, -0.15, 1.35), metalD, segs=5, rings=3); b.sphere(0.02, (0.06, -0.15, 1.35), metalD, segs=5, rings=3)
b.box((0.03, 0.03, 0.04), (0, -0.17, 1.31), skin)
deliver(b, "Character_Crew" + V, flip=True)

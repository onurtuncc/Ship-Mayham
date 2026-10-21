import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
coat = mat("Pirate_Coat", hexc("#7a1f1f")); skin = mat("Skin", hexc("#d9a47a")); pants = mat("Pants", hexc("#2a2a30")); boots = mat("Boots", hexc("#1e1510"))
hat = mat("Hat", hexc("#1c1a22")); beard = mat("Beard", hexc("#2a1c12"))
for sx in (-1, 1):
    b.box((0.16, 0.18, 0.45), (sx * 0.1, 0, 0.38), pants); b.box((0.18, 0.26, 0.16), (sx * 0.1, -0.02, 0.08), boots)
b.box((0.46, 0.26, 0.52), (0, 0, 0.9), coat); b.box((0.48, 0.27, 0.06), (0, 0, 0.68), metalD); b.box((0.08, 0.28, 0.06), (0, 0, 0.68), brass)
for sx in (-1, 1):
    b.box((0.13, 0.15, 0.42), (sx * 0.3, 0, 0.92), coat, rot=(0, sx * 8, 0)); b.sphere(0.075, (sx * 0.33, 0, 0.66), skin, segs=8, rings=5)
b.sphere(0.17, (0, 0, 1.34), skin, segs=12, rings=8)
b.sphere(0.12, (0, -0.06, 1.22), beard, segs=8, rings=5, scale=(1.1, 0.8, 0.9))
b.wedge((0.5, 0.34, 0.16), (0, 0, 1.56), hat)
b.box((0.5, 0.34, 0.04), (0, 0, 1.49), hat)
b.sphere(0.035, (-0.06, -0.16, 1.37), metalD, segs=6, rings=4); b.box((0.34, 0.02, 0.02), (0, -0.16, 1.4), hat)
b.sphere(0.02, (0.06, -0.16, 1.37), cream, segs=5, rings=3)
b.cyl(0.02, 0.5, (0.36, -0.05, 0.6), metal, segs=6, rot=(0, 25, 0))
deliver(b, "Character_Pirate", flip=True)

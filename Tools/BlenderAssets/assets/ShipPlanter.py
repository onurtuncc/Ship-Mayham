import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()

# Comfort-upgrade flower planter. Origin at the base centre, ~0.9 m tall.
pot = mat("Planter_Clay", hexc("#c46a3a"), 0.8); leaf = mat("Planter_Leaf", hexc("#3fae4f"), 0.7); soil = mat("Planter_Soil", hexc("#4a3020"), 0.9)
b.cyl(0.38, 0.45, (0, 0, 0.225), pot, segs=20, r2=0.3)
b.cyl(0.4, 0.07, (0, 0, 0.47), pot, segs=20)
b.cyl(0.33, 0.03, (0, 0, 0.5), soil, segs=20, bevel=0)
for k, (x, y, z, r) in enumerate(((0, 0, 0.78, 0.3), (0.18, 0.08, 0.66, 0.2), (-0.17, -0.06, 0.68, 0.2), (0.02, -0.18, 0.64, 0.17))):
    b.sphere(r, (x, y, z), leaf)
for (x, y, z, c) in ((0.12, 0.2, 0.9, "#ff5f8a"), (-0.2, 0.05, 0.92, "#ffd23f"), (0.2, -0.12, 0.84, "#ff7a3d"), (-0.05, -0.2, 0.86, "#ffffff"), (0.0, 0.05, 1.05, "#ff5f8a")):
    b.sphere(0.07, (x, y, z), mat("Flower_" + c, hexc(c), 0.6))
deliver(b, "ShipPlanter")

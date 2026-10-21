import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
fishA = mat("Fish_Blue", hexc("#6aa7c8")); fishB = mat("Fish_Orange", hexc("#e0773a")); ice = mat("Ice", hexc("#dff1f7"), 0.3)
b.box((0.7, 0.45, 0.04), (0, 0, 0.02), woodD)
for sy in (-1, 1): b.box((0.7, 0.04, 0.26), (0, sy * 0.205, 0.15), wood)
for sx in (-1, 1): b.box((0.04, 0.45, 0.26), (sx * 0.33, 0, 0.15), wood)
b.box((0.62, 0.38, 0.17), (0, 0, 0.12), ice)
for i, (x, y, rz, m) in enumerate(((-0.15, -0.08, 15, fishA), (0.1, 0.05, -10, fishB), (-0.05, 0.12, 25, fishA), (0.2, -0.1, -30, fishB))):
    b.sphere(0.07, (x, y, 0.24), m, segs=8, rings=5, scale=(1.9, 0.6, 0.55), rot=(0, 0, rz))
    tx, ty = x + 0.13 * math.cos(math.radians(rz)), y + 0.13 * math.sin(math.radians(rz))
    b.wedge((0.06, 0.06, 0.09), (x - 0.15 * math.cos(math.radians(rz)), y - 0.15 * math.sin(math.radians(rz)), 0.24), m, rot=(0, 0, rz + 90))
deliver(b, "FishCrate")

import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
iron = mat("Anchor_Iron", hexc("#3a424c"), 0.5, metal=0.6)
b.cyl(0.05, 1.0, (0, 0, 0.55), iron, segs=8)                      # shank
b.box((0.55, 0.08, 0.07), (0, 0, 0.88), iron)                      # stock
b.torus(0.1, 0.03, (0, 0, 1.12), iron, major=14, minor=5, rot=(90, 0, 0))   # ring
b.sphere(0.07, (0, 0, 0.88), iron, segs=8, rings=5)
b.torus_arc(0.42, 0.045, 180, 360, (0, 0, 0.18), iron, steps=8, minor=6, rot=(90, 0, 0))   # arms (lower arc)
for sx in (-1, 1):
    b.wedge((0.18, 0.06, 0.2), (sx * 0.44, 0, 0.22), iron, rot=(0, 0, 0))   # flukes
deliver(b, "Anchor")

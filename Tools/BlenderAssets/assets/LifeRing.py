import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset()
red = mat("LifeRing_Red", hexc("#d8322b")); white = mat("LifeRing_White", hexc("#f2ede0")); rope = mat("LifeRing_Rope", hexc("#c9a35a"), 0.9)
b = Builder()
R, r = 0.30, 0.085
for i in range(8):
    b.torus_arc(R, r, i * 45, (i + 1) * 45, (0, 0, r), red if i % 2 == 0 else white, steps=5, minor=10)
# rope garland: 4 loops hanging outside the ring, as small tori laid on the ring surface
for i in range(4):
    a = math.radians(i * 90 + 45)
    b.torus(0.07, 0.012, ((R + r * 0.55) * math.cos(a), (R + r * 0.55) * math.sin(a), r * 1.45), rope, major=12, minor=5, rot=(0, 0, 0))
deliver(b, "LifeRing")

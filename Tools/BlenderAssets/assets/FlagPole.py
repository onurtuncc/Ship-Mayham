import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
flag = mat("Flag_Red", hexc("#cf3a2d")); flagW = mat("Flag_White", hexc("#f0e8d4"))
b.cyl(0.14, 0.12, (0, 0, 0.06), woodD, segs=8, r2=0.1)
b.cyl(0.04, 2.6, (0, 0, 1.42), woodD, segs=8)
b.sphere(0.07, (0, 0, 2.78), brass, segs=8, rings=5)
for i in range(4):                                                              # waving flag in 4 panels
    b.box((0.22, 0.03, 0.55), (0.14 + i * 0.22, 0.04 * ((i % 2) * 2 - 1) * -1, 2.3), flag if i % 2 == 0 else flagW, rot=(0, 0, 6 * (-1) ** i))
b.box((0.2, 0.035, 0.12), (0.45, 0.0, 2.32), flagW, rot=(0, 45, 0))              # small emblem
deliver(b, "FlagPole")

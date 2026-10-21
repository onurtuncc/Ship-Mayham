import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
wall = mat("Hut_Wall", hexc("#e6d5b2")); roof = mat("Hut_Roof", hexc("#b5472d")); glass = mat("Hut_Window", hexc("#8fc4d9"), 0.2)
b.box((3.4, 2.6, 0.2), (0, 0, 0.1), stone)
b.box((3.0, 2.2, 1.8), (0, 0, 1.1), wall)
b.wedge((3.6, 2.7, 1.0), (0, 0, 2.5), roof)
b.box((0.8, 0.12, 1.4), (-0.6, -1.14, 0.9), woodD)                               # door
b.sphere(0.04, (-0.35, -1.2, 0.95), brass, segs=6, rings=4)
for x in (0.5, 1.1):
    b.box((0.5, 0.1, 0.5), (x, -1.14, 1.3), glass); b.box((0.58, 0.07, 0.58), (x, -1.12, 1.3), woodD)
b.box((1.2, 0.08, 0.35), (0.0, -1.2, 2.1), woodL)                                # name board
b.box((0.3, 0.5, 0.9), (1.0, 0.5, 2.9), stoneD)                                  # chimney
b.cyl(0.07, 0.8, (-1.3, -1.0, 0.55), woodD, segs=6); b.torus(0.12, 0.03, (-1.3, -1.0, 1.0), rope, major=12, minor=4)   # mooring post
deliver(b, "HarborHut")

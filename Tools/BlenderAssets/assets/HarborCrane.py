import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
yel = mat("Crane_Yellow", hexc("#e0a82e")); yelD = mat("Crane_Yellow_Dark", hexc("#b07f1a"))
b.box((1.4, 1.4, 0.2), (0, 0, 0.1), stoneD)
b.cyl(0.35, 3.6, (0, 0, 2.0), yel, segs=8, r2=0.3)
b.cyl(0.5, 0.25, (0, 0, 3.9), yelD, segs=10)
b.box((0.5, 3.8, 0.3), (0, 1.2, 4.2), yel, rot=(8, 0, 0))                       # jib
b.box((0.5, 1.2, 0.5), (0, -0.7, 4.0), yelD)                                    # counterweight
b.box((0.5, 0.5, 0.4), (0, 0.0, 4.4), brass)                                    # cab
b.box((0.04, 0.04, 1.8), (0, 2.9, 3.4), metalD)                                  # hoist cable
b.torus(0.12, 0.03, (0, 2.9, 2.45), metalD, major=12, minor=4, rot=(90, 0, 0))
b.box((0.5, 0.5, 0.5), (0, 2.9, 2.05), wood)                                     # hanging crate
deliver(b, "HarborCrane")

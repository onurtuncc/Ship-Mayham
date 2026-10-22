import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()

# Level-2 engine nacelle bolted to the stern. The nozzle faces +Y (Blender), which imports as Unity -Z, i.e. astern.
glow = mat("Boost_Glow", hexc("#ff8a2a"), 0.4, emit=hexc("#ff7a1a"))
b.cyl(0.34, 1.7, (0, 0, 0.5), metal, segs=20, rot=(90, 0, 0))                    # barrel
b.cyl(0.4, 0.25, (0, 0.85, 0.5), brass, segs=20, rot=(90, 0, 0))                 # nozzle collar
b.cyl(0.27, 0.06, (0, 1.0, 0.5), glow, segs=20, rot=(90, 0, 0), bevel=0)         # glowing core
b.cyl(0.3, 0.2, (0, -0.8, 0.5), red, segs=20, rot=(90, 0, 0))                    # nose cap
for sx in (-1, 1):
    b.box((0.05, 0.9, 0.34), (sx * 0.34, 0.1, 0.78), metalD, rot=(0, 0, sx * 12))   # stabiliser fins
b.box((0.5, 0.7, 0.14), (0, -0.1, 0.1), metalD)                                    # mounting bracket
deliver(b, "ShipBoost")

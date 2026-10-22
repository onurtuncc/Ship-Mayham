import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()

# Engine-upgrade smokestack for the player ship. Origin at the base centre; ~2.5 m tall, 1.1 m wide at the collar.
stack = mat("Funnel_Navy", hexc("#2a4a6b"), 0.5); soot = mat("Funnel_Soot", hexc("#1b1d22"), 0.8)
b.cyl(0.62, 0.2, (0, 0, 0.1), metalD, segs=24)                                  # deck collar
b.cyl(0.5, 2.15, (0, 0, 1.2), stack, segs=24, r2=0.4)                           # tapered stack
b.cyl(0.46, 0.2, (0, 0, 1.55), cream, segs=24, bevel=0.01)                      # cream band
b.cyl(0.44, 0.12, (0, 0, 1.78), red, segs=24, bevel=0.01)                       # red stripe
b.cyl(0.46, 0.14, (0, 0, 2.32), brass, segs=24)                                 # brass rim
b.cyl(0.36, 0.05, (0, 0, 2.38), soot, segs=24, bevel=0)                         # soot opening
for sx in (-1, 1):                                                              # steam pipes
    b.cyl(0.05, 0.9, (sx * 0.52, 0.0, 0.95), brass, segs=12)
    b.sphere(0.09, (sx * 0.52, 0.0, 1.42), brass)
deliver(b, "ShipFunnel")

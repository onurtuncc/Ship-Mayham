import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
b.box((1.2, 3.0, 0.06), (0, 0, 0.1), woodL)
for i in range(8):
    b.box((1.15, 0.05, 0.05), (0, -1.3 + i * 0.37, 0.15), woodD)                # cleats
for sx in (-1, 1):
    b.box((0.08, 3.0, 0.08), (sx * 0.62, 0, 0.1), woodD)
    for y in (-1.3, 0.0, 1.3):
        b.cyl(0.035, 0.7, (sx * 0.6, y, 0.5), woodD, segs=6)                    # rail posts
    b.torus(0.0001, 0.001, (0, 0, 0), woodD) if False else None
    b.box((0.05, 2.9, 0.05), (sx * 0.6, 0, 0.82), rope)                         # rope rail
deliver(b, "Gangway")

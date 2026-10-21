import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
hullm = mat("Boat_Hull", hexc("#3b7a9e")); inner = mat("Boat_Inner", hexc("#d9b27a"))


def prof(w, zb, top):
    h = top - zb
    return [(0.0, zb), (w * 0.6, zb + 0.1 * h), (w * 0.95, zb + 0.5 * h), (w, top)]


b.loft([(-1.0, prof(0.5, 0.1, 0.55)), (-0.5, prof(0.62, 0.05, 0.5)), (0.3, prof(0.6, 0.05, 0.5)), (1.0, prof(0.4, 0.15, 0.6)), (1.45, prof(0.08, 0.35, 0.72))], hullm)
b.box((1.1, 2.3, 0.04), (0, 0.1, 0.4), inner)                                  # floor boards
for y in (-0.5, 0.2):
    b.box((1.15, 0.12, 0.06), (0, y, 0.52), woodL)                              # thwarts
for sx in (-1, 1):
    b.box((0.05, 2.0, 0.05), (sx * 0.58, 0.0, 0.54), woodD)                      # gunwale
    b.cyl(0.03, 1.5, (sx * 0.85, 0.0, 0.62), woodD, segs=6, rot=(0, 60 * sx, 0))  # oars
    b.box((0.12, 0.35, 0.03), (sx * 1.45, 0.0, 0.95), woodL, rot=(0, 60 * sx, 0))
deliver(b, "Rowboat")

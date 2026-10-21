import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()


def crate(x, y, z, s, rotz=0):
    b.box((s, s, s), (x, y, z + s / 2), wood, rot=(0, 0, rotz))
    for k in (-1, 1):
        b.box((s + 0.02, 0.05 * s / 0.6, s + 0.02), (x, y, z + s / 2), woodD, rot=(0, 0, rotz)) if False else None
    for ang in (0, 90):
        b.box((s * 0.12, s + 0.02, s * 0.12), (x, y, z + s - s * 0.06), woodD, rot=(0, 0, rotz + ang))
        b.box((s * 0.12, s + 0.02, s * 0.12), (x, y, z + s * 0.06), woodD, rot=(0, 0, rotz + ang))
    for sx in (-1, 1):
        for sy in (-1, 1):
            c = mathutils.Vector((sx * (s / 2 - s * 0.06), sy * (s / 2 - s * 0.06), 0))
            c.rotate(mathutils.Euler((0, 0, math.radians(rotz))))
            b.box((s * 0.12, s * 0.12, s + 0.02), (x + c.x, y + c.y, z + s / 2), woodD)


crate(0, 0, 0, 0.7); crate(0.72, 0.1, 0, 0.6, 12); crate(-0.55, 0.65, 0, 0.55, -8)
crate(0.05, 0.05, 0.7, 0.55, 20)
b.cyl(0.3, 0.7, (0.9, 0.9, 0.35), woodD, segs=10, r2=0.26)                      # barrel
b.torus(0.3, 0.02, (0.9, 0.9, 0.18), metalD, major=14, minor=4); b.torus(0.3, 0.02, (0.9, 0.9, 0.55), metalD, major=14, minor=4)
deliver(b, "CrateStack")

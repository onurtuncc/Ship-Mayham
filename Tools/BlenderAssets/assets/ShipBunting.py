import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import math
from assetlib import *
reset(); globals().update(std()); b = Builder()

# Comfort-upgrade pennant garland strung between the bulwark posts of one side of the ship (five spans of 3.4 m).
# Authored along Y; the builder places one copy per side. Origin at the centre of the string, bottom at the lowest flag.
colors = [mat("Bunting_" + h, hexc(h), 0.7) for h in ("#ff5a5f", "#ffd23f", "#3fc1c9", "#7ad45f", "#b46cff", "#ff9f43")]
cord = mat("Bunting_Cord", hexc("#e8dcc0"), 0.9)
SPAN, SPANS, TOP, SAG = 3.4, 5, 2.0, 0.55
n = 0
for s in range(SPANS):
    y0 = -SPAN * SPANS / 2 + s * SPAN
    pts = []
    for k in range(9):
        t = k / 8
        pts.append((y0 + SPAN * t, TOP - SAG * 4 * t * (1 - t)))
    for (ya, za), (yb, zb) in zip(pts, pts[1:]):
        length = math.hypot(yb - ya, zb - za)
        ang = -math.degrees(math.atan2(zb - za, yb - ya))
        b.box((0.025, length + 0.02, 0.025), (0, (ya + yb) / 2, (za + zb) / 2), cord, rot=(ang, 0, 0), r=0)
    for k in range(1, 8):
        y, z = pts[k]
        b.wedge((0.46, 0.03, 0.52), (0, y, z - 0.26), colors[n % len(colors)], rot=(180, 0, 0))
        n += 1
deliver(b, "ShipBunting")

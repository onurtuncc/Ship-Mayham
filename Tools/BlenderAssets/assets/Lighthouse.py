import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
glow = mat("Glow", hexc("#ffe08a"), 0.2, emit=hexc("#ffc23a"))
b.cyl(2.3, 0.5, (0, 0, 0.25), stone, segs=16, r2=2.0)
for i in range(5):
    b.cyl(1.55 - i * 0.14, 1.3, (0, 0, 0.5 + i * 1.3 + 0.65), red if i % 2 == 0 else cream, segs=14, r2=1.55 - (i + 1) * 0.14)
b.cyl(1.4, 0.18, (0, 0, 7.09), woodD, segs=14)
for i in range(14):
    a = math.radians(i * 360 / 14)
    b.box((0.06, 0.06, 0.5), (1.35 * math.cos(a), 1.35 * math.sin(a), 7.43), metalD)
b.torus(1.35, 0.04, (0, 0, 7.7), metalD, major=14, minor=4)
b.cyl(0.8, 0.9, (0, 0, 7.75), glow, segs=10)
b.cyl(0.95, 0.14, (0, 0, 8.27), metalD, segs=10)
b.cyl(0.95, 0.7, (0, 0, 8.65), red, segs=10, r2=0.02)
b.sphere(0.1, (0, 0, 9.05), brass, segs=8, rings=5)
b.box((0.5, 0.12, 0.9), (0, -1.45, 0.95), woodD)
for z in (2.4, 4.4):
    b.box((0.35, 0.1, 0.35), (0, -1.4, z), navy)
deliver(b, "Lighthouse")

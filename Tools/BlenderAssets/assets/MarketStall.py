import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
apple = mat("Apple", hexc("#d94a2a")); pear = mat("Pear", hexc("#9cc43c"))
for sx in (-1, 1):
    for sy in (-1, 1):
        b.box((0.08, 0.08, 2.0), (sx * 0.95, sy * 0.5, 1.0), woodD)
b.box((2.0, 0.9, 0.9), (0, 0.1, 0.45), wood)
b.box((2.1, 1.0, 0.06), (0, 0.05, 0.93), woodL)
for i in range(8):
    c = red if i % 2 == 0 else cream
    b.box((0.27, 1.5, 0.05), (-0.945 + i * 0.27, 0, 2.02), c, rot=(-14, 0, 0))
    b.box((0.27, 0.05, 0.14), (-0.945 + i * 0.27, -0.74, 1.86), c)
for x, y in ((-0.5, 0.0), (0.45, 0.05)):
    b.box((0.4, 0.4, 0.3), (x, y, 1.11), wood); b.box((0.42, 0.42, 0.04), (x, y, 1.27), woodD)
b.sphere(0.07, (0.05, -0.25, 1.0), apple, segs=8, rings=5)
b.sphere(0.07, (0.2, -0.3, 1.0), apple, segs=8, rings=5)
b.sphere(0.07, (0.12, -0.2, 1.0), pear, segs=8, rings=5)
b.box((0.5, 0.04, 0.3), (-0.5, -0.52, 1.0), woodD)
deliver(b, "MarketStall")

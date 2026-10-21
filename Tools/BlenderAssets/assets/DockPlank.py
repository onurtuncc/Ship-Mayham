import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
for i in range(10):
    b.box((0.38, 2.0, 0.08), (-1.8 + i * 0.4, 0, 0.5 + (0.015 if i % 3 == 0 else 0.0)), wood if i % 2 else woodL)
for sy in (-0.75, 0.75):
    b.box((4.0, 0.12, 0.14), (0, sy, 0.4), woodD)
for sx in (-1.7, -0.57, 0.57, 1.7):
    for sy in (-0.95, 0.95):
        b.cyl(0.1, 0.9, (sx, sy, 0.05), woodD, segs=8)
for sx in (-1.9, 1.9):
    for sy in (-0.95, 0.95):
        b.cyl(0.09, 0.5, (sx, sy, 0.78), woodD, segs=8)       # mooring posts at the corners
        b.box((0.2, 0.2, 0.04), (sx, sy, 1.04), woodL)
deliver(b, "DockPlank")

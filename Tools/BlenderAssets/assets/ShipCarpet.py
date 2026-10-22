import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()

# Comfort-upgrade runner for the centre aisle: 2.3 m wide, 12.4 m long, a few millimetres thick. Origin at its centre.
rug = mat("Carpet_Red", hexc("#b8323c"), 0.9); gold = mat("Carpet_Gold", hexc("#e0b043"), 0.7); navyc = mat("Carpet_Navy", hexc("#233f66"), 0.9)
b.box((2.3, 12.4, 0.03), (0, 0, 0.015), rug, r=0)
for sx in (-1, 1):
    b.box((0.1, 12.4, 0.036), (sx * 1.0, 0, 0.018), gold, r=0)
    b.box((0.18, 12.4, 0.04), (sx * 0.82, 0, 0.02), navyc, r=0)
for i in range(-5, 6):
    b.box((1.3, 0.1, 0.04), (0, i * 1.1, 0.022), gold, r=0)                       # diamond-ish cross stripes
deliver(b, "ShipCarpet")

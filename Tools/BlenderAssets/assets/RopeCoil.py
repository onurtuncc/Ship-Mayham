import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
for i in range(5):
    b.torus(0.22 - i * 0.012, 0.045, (0, 0, 0.045 + i * 0.075), rope, major=18, minor=6)
b.torus(0.12, 0.04, (0, 0, 0.42), rope, major=14, minor=5)
b.box((0.5, 0.06, 0.05), (0.0, -0.2, 0.05), rope, rot=(0, 0, 8))      # loose tail
deliver(b, "RopeCoil")

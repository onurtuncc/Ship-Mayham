import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
suit = mat("VIP_Suit", hexc("#1d2a44")); skin = mat("Skin", hexc("#e8b48a")); gold = mat("VIP_Gold", hexc("#e6b830"), 0.35, metal=0.7)
shirt = mat("VIP_Shirt", hexc("#f4efe2")); shoes = mat("Shoes", hexc("#16120e")); hat = mat("VIP_Hat", hexc("#15151b"))
for sx in (-1, 1):
    b.box((0.15, 0.17, 0.45), (sx * 0.1, 0, 0.38), suit); b.box((0.17, 0.26, 0.1), (sx * 0.1, -0.03, 0.05), shoes)
b.box((0.44, 0.26, 0.52), (0, 0, 0.9), suit)
b.box((0.12, 0.27, 0.5), (0, -0.01, 0.9), shirt); b.box((0.1, 0.28, 0.06), (0, -0.01, 1.0), red)      # shirt front + tie
for sx in (-1, 1):
    b.box((0.12, 0.14, 0.42), (sx * 0.29, 0, 0.92), suit, rot=(0, sx * 8, 0)); b.sphere(0.07, (sx * 0.32, 0, 0.66), skin, segs=8, rings=5)
    b.box((0.13, 0.15, 0.05), (sx * 0.3, 0, 0.7), gold)                                                   # gold cuffs
b.sphere(0.16, (0, 0, 1.33), skin, segs=12, rings=8)
b.cyl(0.24, 0.03, (0, 0, 1.47), hat, segs=14); b.cyl(0.14, 0.26, (0, 0, 1.62), hat, segs=12)          # top hat
b.cyl(0.145, 0.05, (0, 0, 1.53), gold, segs=12)
b.box((0.2, 0.04, 0.04), (0, -0.16, 1.28), hat)                                                       # moustache
b.sphere(0.02, (-0.06, -0.15, 1.36), metalD, segs=5, rings=3); b.sphere(0.02, (0.06, -0.15, 1.36), metalD, segs=5, rings=3)
b.cyl(0.015, 0.55, (0.34, -0.1, 0.45), woodD, segs=6); b.sphere(0.04, (0.34, -0.1, 0.74), gold, segs=6, rings=4)   # cane
deliver(b, "Character_VIP", flip=True)

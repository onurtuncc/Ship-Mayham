import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
from charlib import *
reset(); globals().update(std()); b = Builder()
suit = mat("VIP_Suit", hexc("#232f55"), 0.4); gold = mat("VIP_Gold", hexc("#f0c030"), 0.3, metal=0.8); shirtW = mat("VIP_Shirt", hexc("#fbf6ea"), 0.5)
hat = mat("VIP_Hat", hexc("#17171f"), 0.35)
bean(b, suit, mat("Pants", hexc("#232f55"), 0.4), mat("Shoes", hexc("#16120e"), 0.3), mat("Skin", hexc("#f4b88a"), 0.55), torso_taper=0.9)
b.capsule(0.07, 0.2, (0, -0.19, 0.7), shirtW, rot=(0, 0, 0))                          # shirt front
b.box((0.08, 0.03, 0.2), (0, -0.265, 0.74), red, r=0.015)                              # tie
for sx in (-1, 1):
    b.torus(0.062, 0.016, (sx * 0.298, -0.01, 0.58), gold, major=14, minor=5, rot=(0, 0, 0))   # cuffs
b.cyl(0.43, 0.035, (0, 0, 1.36), hat, segs=32, bevel=0.012)                           # top hat brim
b.cyl(0.215, 0.36, (0, 0, 1.56), hat, segs=28)
b.cyl(0.222, 0.07, (0, 0, 1.43), gold, segs=28, bevel=0)
b.capsule(0.022, 0.16, (0, -0.255, 1.052), mat("Moustache", hexc("#2a1c12"), 0.6), rot=(0, 90, 0), segs=12)    # moustache
b.capsule(0.02, 0.6, (0.42, -0.1, 0.42), mat("Cane", hexc("#5a3a20"), 0.5)); b.sphere(0.05, (0.42, -0.1, 0.78), gold)   # cane
deliver(b, "Character_VIP", flip=True)

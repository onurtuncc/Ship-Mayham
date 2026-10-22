import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
from charlib import *
reset(); globals().update(std()); b = Builder()
coat = mat("Pirate_Coat", hexc("#9b2a2f"), 0.45); hat = mat("Pirate_Hat", hexc("#1d1b26"), 0.4); beard = mat("Beard", hexc("#2a1c12"), 0.7)
bean(b, coat, mat("Pants", hexc("#2a2a35"), 0.55), mat("Boots", hexc("#1e1510"), 0.35), mat("Skin", hexc("#e0a070"), 0.55), torso_taper=0.9, brow_col=beard)
b.sphere(0.2, (0, -0.1, 0.99), beard, scale=(1.2, 0.8, 0.9))                           # big beard
b.sphere(0.07, (0, -0.24, 1.07), beard, scale=(1.5, 0.6, 0.7))                         # moustache
b.box((0.5, 0.05, 0.05), (0, 0, 0.62), metalD, r=0.015)                                 # belt
b.box((0.09, 0.06, 0.07), (0, -0.21, 0.62), brass, r=0.015)
# tricorn hat
b.cyl(0.36, 0.05, (0, 0, 1.34), hat, segs=3, rot=(0, 0, 90), bevel=0.015)
b.lathe([(0.0, 1.5), (0.17, 1.47), (0.25, 1.36), (0.26, 1.32)], (0, 0, 0), hat, segs=24)
b.sphere(0.06, (0, -0.28, 1.43), mat("Skull", hexc("#f0e8d4"), 0.5), scale=(1, 0.4, 1))
# eyepatch + strap
b.sphere(0.065, (-0.1, -0.265, 1.15), metalD, scale=(1, 0.35, 1.1))
b.torus(0.265, 0.008, (0, 0, 1.15), metalD, major=24, minor=4, rot=(0, 0, 0))
# cutlass
b.capsule(0.018, 0.5, (0.43, -0.08, 0.5), mat("Blade", hexc("#cfd6dd"), 0.25, metal=0.8), rot=(25, 0, 10)); b.sphere(0.035, (0.41, -0.02, 0.27), brass)
deliver(b, "Character_Pirate", flip=True)

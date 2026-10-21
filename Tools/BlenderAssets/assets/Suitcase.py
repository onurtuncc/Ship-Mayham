import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset()
wood=mat("Wood",hexc("#9a6a3c")); woodD=mat("Wood_Dark",hexc("#5f3d22")); woodL=mat("Wood_Light",hexc("#c08a52")); metal=mat("Metal",hexc("#5d6772"),0.5,metal=0.6); metalD=mat("Metal_Dark",hexc("#2c3138"),0.6,metal=0.5); brass=mat("Brass",hexc("#d9a63a"),0.4,metal=0.7); cream=mat("Cream",hexc("#f0e8d4")); red=mat("Red",hexc("#cf3a2d")); navy=mat("Navy",hexc("#25435f")); rope=mat("Rope",hexc("#c9a35a"),0.9)
b=Builder()
leather=mat("Leather",hexc("#8c4a2b")); leatherD=mat("Leather_Dark",hexc("#5a2d19"))
W,H,D=0.6,0.4,0.2
b.box((W,D,H),(0,0,H/2),leather)
b.box((W+0.02,D+0.02,0.03),(0,0,H*0.5),leatherD)          # middle seam band
for sx in (-1,1):
    for sz in (0.035,H-0.035):
        b.box((0.07,D+0.03,0.07),(sx*(W/2-0.02),0,sz),metal)   # corner guards
b.box((0.22,0.05,0.03),(0,0,H+0.015),leatherD); 
for sx in (-1,1): b.box((0.03,0.05,0.07),(sx*0.1,0,H+0.02),leatherD)  # handle posts
b.box((0.24,0.05,0.03),(0,0,H+0.07),leatherD)
for sx in (-0.18,0.18): b.box((0.07,0.03,0.05),(sx,D/2+0.01,H*0.5),brass)  # latches
deliver(b,"Suitcase")

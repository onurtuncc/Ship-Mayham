import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset()
wood=mat("Wood",hexc("#9a6a3c")); woodD=mat("Wood_Dark",hexc("#5f3d22")); woodL=mat("Wood_Light",hexc("#c08a52")); metal=mat("Metal",hexc("#5d6772"),0.5,metal=0.6); metalD=mat("Metal_Dark",hexc("#2c3138"),0.6,metal=0.5); brass=mat("Brass",hexc("#d9a63a"),0.4,metal=0.7); cream=mat("Cream",hexc("#f0e8d4")); red=mat("Red",hexc("#cf3a2d")); navy=mat("Navy",hexc("#25435f")); rope=mat("Rope",hexc("#c9a35a"),0.9)
b=Builder()
ball=mat("Iron_Ball",hexc("#2d3238"),0.45,metal=0.6)
b.box((0.62,0.62,0.04),(0,0,0.02),woodD)
r=0.06
layers=[(4,0.0)]
z=0.04+r
for lay,n in enumerate((4,3,2,1)):
    off=-(n-1)*r
    for i in range(n):
        for j in range(n):
            b.sphere(r,(off+i*2*r*1.0,off+j*2*r*1.0,z+lay*r*1.55),ball,segs=10,rings=7)
deliver(b,"CannonballPile")

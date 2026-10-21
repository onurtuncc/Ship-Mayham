import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset()
wood=mat("Wood",hexc("#9a6a3c")); woodD=mat("Wood_Dark",hexc("#5f3d22")); woodL=mat("Wood_Light",hexc("#c08a52")); metal=mat("Metal",hexc("#5d6772"),0.5,metal=0.6); metalD=mat("Metal_Dark",hexc("#2c3138"),0.6,metal=0.5); brass=mat("Brass",hexc("#d9a63a"),0.4,metal=0.7); cream=mat("Cream",hexc("#f0e8d4")); red=mat("Red",hexc("#cf3a2d")); navy=mat("Navy",hexc("#25435f")); rope=mat("Rope",hexc("#c9a35a"),0.9)
b=Builder()
for sx in (-0.52,0.52):
    b.box((0.08,0.42,0.42),(sx,0,0.21),woodD)               # side panels
    b.box((0.08,0.06,0.5),(sx,-0.2,0.67),woodD)              # backrest posts
b.box((1.2,0.42,0.06),(0,0,0.45),wood)                       # seat
for i in range(2): b.box((1.12,0.025,0.1),(0,-0.2,0.58+i*0.14),woodL, rot=(-8,0,0))  # backrest slats
b.box((1.12,0.4,0.03),(0,0,0.1),woodD)                       # lower brace
for sx in (-0.52,0.52): b.box((0.1,0.46,0.04),(sx,0,0.47),woodL)  # armrest caps
b.box((1.0,0.03,0.04),(0,0.1,0.51),red)                      # seat cushion stripe
deliver(b,"DeckBench")

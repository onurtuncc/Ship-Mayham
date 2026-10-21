import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset()
wood=mat("Wood",hexc("#9a6a3c")); woodD=mat("Wood_Dark",hexc("#5f3d22")); woodL=mat("Wood_Light",hexc("#c08a52")); metal=mat("Metal",hexc("#5d6772"),0.5,metal=0.6); metalD=mat("Metal_Dark",hexc("#2c3138"),0.6,metal=0.5); brass=mat("Brass",hexc("#d9a63a"),0.4,metal=0.7); cream=mat("Cream",hexc("#f0e8d4")); red=mat("Red",hexc("#cf3a2d")); navy=mat("Navy",hexc("#25435f")); rope=mat("Rope",hexc("#c9a35a"),0.9)
b=Builder()
b.box((0.5,0.35,0.025),(0,0,0.0125),woodL)
for sy in (-1,1): b.box((0.5,0.03,0.05),(0,sy*0.16,0.04),woodD)
for sx in (-1,1): b.box((0.03,0.35,0.05),(sx*0.235,0,0.04),woodD)
for sx in (-1,1): b.box((0.05,0.1,0.02),(sx*0.255,0,0.06),woodD)    # handle lugs
b.box((0.4,0.25,0.005),(0,0,0.03),cream)
deliver(b,"ServingTray")

import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset()
wood=mat("Wood",hexc("#9a6a3c")); woodD=mat("Wood_Dark",hexc("#5f3d22")); woodL=mat("Wood_Light",hexc("#c08a52")); metal=mat("Metal",hexc("#5d6772"),0.5,metal=0.6); metalD=mat("Metal_Dark",hexc("#2c3138"),0.6,metal=0.5); brass=mat("Brass",hexc("#d9a63a"),0.4,metal=0.7); cream=mat("Cream",hexc("#f0e8d4")); red=mat("Red",hexc("#cf3a2d")); navy=mat("Navy",hexc("#25435f")); rope=mat("Rope",hexc("#c9a35a"),0.9)
b=Builder()
b.box((0.5,0.5,0.1),(0,0,0.05),woodD)                         # base
b.cyl(0.14,0.95,(0,0,0.55),wood,segs=10,r2=0.09)              # post
b.cyl(0.18,0.08,(0,0,1.0),woodD,segs=10)
# wheel in XZ plane facing -Y (toward player)
b.torus(0.33,0.032,(0,-0.1,1.1),wood,major=24,minor=6,rot=(90,0,0))
b.cyl(0.075,0.12,(0,-0.1,1.1),brass,segs=10,rot=(90,0,0))     # hub
for i in range(8):
    a=math.radians(i*45)
    b.box((0.03,0.03,0.7),(0,-0.1,1.1),wood,rot=(0,math.degrees(a)*-1+0,0)) if False else None
    ca,sa=math.cos(a),math.sin(a)
    b.box((0.035,0.035,0.62),(0.33*sa*0.0,-0.1,1.1),wood,rot=(0,i*45,0)) if i<4 else None
    # handles beyond rim
    b.cyl(0.026,0.16,(0.4*sa,-0.1,1.1+0.4*ca),brass,segs=6,rot=(0,i*45,0)) 
b.cyl(0.03,0.1,(0,-0.1,0.99),brass,segs=6,rot=(90,0,0))
deliver(b,"ShipWheel")

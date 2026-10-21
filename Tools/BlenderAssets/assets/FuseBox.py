import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset()
wood=mat("Wood",hexc("#9a6a3c")); woodD=mat("Wood_Dark",hexc("#5f3d22")); woodL=mat("Wood_Light",hexc("#c08a52")); metal=mat("Metal",hexc("#5d6772"),0.5,metal=0.6); metalD=mat("Metal_Dark",hexc("#2c3138"),0.6,metal=0.5); brass=mat("Brass",hexc("#d9a63a"),0.4,metal=0.7); cream=mat("Cream",hexc("#f0e8d4")); red=mat("Red",hexc("#cf3a2d")); navy=mat("Navy",hexc("#25435f")); rope=mat("Rope",hexc("#c9a35a"),0.9)
b=Builder()
grey=mat("Panel_Grey",hexc("#7e8893"),0.6,metal=0.3); hazA=mat("Hazard_Yellow",hexc("#f2c230")); hazB=mat("Hazard_Black",hexc("#23262b"))
lamp=mat("Lamp_Green",hexc("#5be07a"),0.2,emit=hexc("#2fd05a")); lever=mat("Lever_Red",hexc("#cf3a2d"))
b.box((0.5,0.22,0.08),(0,0,0.04),metalD)
b.box((0.45,0.18,0.62),(0,0,0.39),grey)
b.box((0.38,0.02,0.5),(0,-0.1,0.4),metalD)                      # inner recess
for i in range(3):
    x=-0.12+i*0.12
    b.box((0.03,0.03,0.12),(x,-0.125,0.36),metal); b.cyl(0.035,0.04,(x,-0.14,0.45+0.02*(i%2)),lever,segs=8,rot=(90,0,0))
b.cyl(0.05,0.04,(0,-0.11,0.64),lamp,segs=10,rot=(90,0,0))
for i in range(6): b.box((0.07,0.012,0.07),(-0.175+i*0.07,-0.1,0.1),hazA if i%2==0 else hazB)
b.box((0.4,0.04,0.04),(0,0,0.72),metalD)
deliver(b,"FuseBox")

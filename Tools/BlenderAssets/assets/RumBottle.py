import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset()
wood=mat("Wood",hexc("#9a6a3c")); woodD=mat("Wood_Dark",hexc("#5f3d22")); woodL=mat("Wood_Light",hexc("#c08a52")); metal=mat("Metal",hexc("#5d6772"),0.5,metal=0.6); metalD=mat("Metal_Dark",hexc("#2c3138"),0.6,metal=0.5); brass=mat("Brass",hexc("#d9a63a"),0.4,metal=0.7); cream=mat("Cream",hexc("#f0e8d4")); red=mat("Red",hexc("#cf3a2d")); navy=mat("Navy",hexc("#25435f")); rope=mat("Rope",hexc("#c9a35a"),0.9)
b=Builder()
glass=mat("Drink_Blue",hexc("#2f6fe0"),0.15); corkm=mat("Cork",hexc("#c79a63"))
b.cyl(0.07,0.17,(0,0,0.085),glass,segs=12,r2=0.075)
b.sphere(0.075,(0,0,0.17),glass,segs=12,rings=6,scale=(1,1,0.6))
b.cyl(0.032,0.11,(0,0,0.23),glass,segs=10,r2=0.026)
b.cyl(0.03,0.025,(0,0,0.3),glass,segs=10)
b.cyl(0.026,0.04,(0,0,0.325),corkm,segs=8)
b.cyl(0.0765,0.07,(0,0,0.1),cream,segs=12)               # label band
b.box((0.04,0.02,0.04),(0,-0.078,0.1),red)               # label mark
deliver(b,"RumBottle")

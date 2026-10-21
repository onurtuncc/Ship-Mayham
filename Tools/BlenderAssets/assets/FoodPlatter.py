import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset()
wood=mat("Wood",hexc("#9a6a3c")); woodD=mat("Wood_Dark",hexc("#5f3d22")); woodL=mat("Wood_Light",hexc("#c08a52")); metal=mat("Metal",hexc("#5d6772"),0.5,metal=0.6); metalD=mat("Metal_Dark",hexc("#2c3138"),0.6,metal=0.5); brass=mat("Brass",hexc("#d9a63a"),0.4,metal=0.7); cream=mat("Cream",hexc("#f0e8d4")); red=mat("Red",hexc("#cf3a2d")); navy=mat("Navy",hexc("#25435f")); rope=mat("Rope",hexc("#c9a35a"),0.9)
b=Builder()
food=mat("Food_Orange",hexc("#e8892e")); crust=mat("Bread",hexc("#c98a45")); fish=mat("Fish",hexc("#d96b3a")); green=mat("Apple",hexc("#74b83a"))
b.cyl(0.2,0.025,(0,0,0.0125),woodD,segs=20)
b.cyl(0.17,0.02,(0,0,0.03),woodL,segs=20)
b.sphere(0.1,(-0.03,0.03,0.1),fish,segs=10,rings=6,scale=(1.6,0.7,0.55),rot=(0,0,20))   # fish body
b.wedge((0.06,0.1,0.1),(-0.19,0.07,0.1),fish,rot=(0,0,20+90))                            # tail
b.sphere(0.012,(0.05,0.07,0.12),cream,segs=6,rings=4)
b.sphere(0.06,(0.08,-0.09,0.095),crust,segs=10,rings=6,scale=(1.3,1,0.8))               # bread
b.sphere(0.05,(-0.09,-0.09,0.1),green,segs=10,rings=6)                                   # apple
b.box((0.01,0.01,0.03),(-0.09,-0.09,0.15),woodD)
deliver(b,"FoodPlatter")

import os
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
from charlib import *
reset(); globals().update(std()); b = Builder()
V = os.environ.get("VARIANT", "1")
cols = {"1": "#e8453c", "2": "#3b8fe6", "3": "#4dbb5f", "4": "#f2b630"}
shirt = mat("Crew_Shirt", hexc(cols[V]), 0.45); stripe = mat("Crew_Stripe", hexc("#fbf3df"), 0.5)
bean(b, shirt, mat("Pants", hexc("#2f4a7a"), 0.55), mat("Shoes", hexc("#6a3f22"), 0.4), mat("Skin", hexc("#f4b88a"), 0.55), stripe=stripe)
band = mat("Bandana", hexc(cols[V]), 0.5)
dome(b, band, 1.29)                                                      # bandana cap
b.sphere(0.05, (0.2, 0.2, 1.22), band, scale=(1, 1, 0.8)); b.sphere(0.04, (0.27, 0.27, 1.16), band)   # knot tails
deliver(b, "Character_Crew" + V, flip=True)

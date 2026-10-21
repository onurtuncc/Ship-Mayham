import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()
hull = mat("Hull_Black", hexc("#2b2430")); hullT = mat("Hull_Trim", hexc("#6b4a2a")); sail = mat("Sail_Black", hexc("#1f1c24"))
bone = mat("Bone", hexc("#efe8d8")); deck = mat("Deck", hexc("#9a6a3c")); redS = mat("Stripe_Red", hexc("#8f2a2a"))


def prof(w, zb, top):
    h = top - zb
    return [(0.0, zb), (w * 0.55, zb + 0.08 * h), (w * 0.9, zb + 0.4 * h), (w, zb + 0.75 * h), (w * 0.93, top)]


# hull: stern -> bow along +Y, sheer line rising toward the bow
sections = [(-2.3, prof(0.85, 0.5, 1.5)), (-1.6, prof(0.98, 0.35, 1.38)), (0.0, prof(1.05, 0.3, 1.3)),
            (1.4, prof(0.95, 0.4, 1.38)), (2.6, prof(0.6, 0.62, 1.55)), (3.4, prof(0.16, 0.85, 1.75))]
b.loft(sections, hull)
b.box((1.55, 4.6, 0.06), (0, 0.15, 1.33), deck)                                  # main deck plate
for sx in (-1, 1):
    b.box((0.07, 4.0, 0.05), (sx * 0.97, -0.3, 1.36), hullT)  # gunwale cap
    b.box((0.05, 3.6, 0.1), (sx * 0.96, -0.2, 1.0), redS)                          # painted stripe
    for k in range(3):
        b.cyl(0.1, 0.16, (sx * 1.02, -0.9 + k * 1.0, 0.8), metalD, segs=8, rot=(0, 90, 0))   # cannon ports
b.box((1.7, 0.1, 0.55), (0, -2.2, 1.45), hullT)                                  # transom
b.box((1.4, 1.3, 0.6), (0, -1.6, 1.62), hullT); b.box((1.5, 1.4, 0.08), (0, -1.6, 1.96), woodD)   # aft cabin + roof
b.box((0.34, 0.04, 0.3), (0, -2.3, 1.7), bone)                                   # stern lantern plate
b.cyl(0.07, 3.8, (0, 0.7, 3.1), woodD, segs=8); b.cyl(0.07, 3.2, (0, -0.8, 2.8), woodD, segs=8)   # masts
b.box((1.6, 0.05, 1.7), (0, 0.78, 3.1), sail); b.box((1.25, 0.05, 1.4), (0, -0.72, 2.9), sail)
b.box((1.8, 0.07, 0.07), (0, 0.7, 3.98), woodD); b.box((1.45, 0.07, 0.07), (0, -0.8, 3.62), woodD)
b.sphere(0.17, (0, 0.84, 3.2), bone, segs=8, rings=5, scale=(1, 0.4, 1))          # skull
b.box((0.3, 0.03, 0.06), (0, 0.85, 2.95), bone, rot=(0, 35, 0)); b.box((0.3, 0.03, 0.06), (0, 0.85, 2.95), bone, rot=(0, -35, 0))
b.cyl(0.06, 1.3, (0, 3.75, 1.75), woodD, segs=6, rot=(-75, 0, 0))                 # bowsprit
b.box((0.5, 0.03, 0.35), (0, -0.8, 4.0), red) if False else None
deliver(b, "PirateShipSmall")

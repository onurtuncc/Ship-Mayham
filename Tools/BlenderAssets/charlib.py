"""Shared cartoon 'bean' character rig-less body used by all people (crew, passengers, VIP, pirate).

Authored facing -Y (deliver(..., flip=True) turns it to +Y). Height ~1.45 m, huge round head, mitten hands.
"""
from assetlib import *


def bean(b, shirt, pants, shoe, skin, stripe=None, torso_taper=0.82, skin_blush=None, brow_col=None):
    eyeW = mat("Eye_White", hexc("#ffffff"), 0.25); eyeB = mat("Eye_Black", hexc("#15171c"), 0.2)
    blush = skin_blush or mat("Skin_Blush", hexc("#ee8f7a"), 0.6)
    brow = brow_col or mat("Brow", hexc("#4a2e1a"), 0.6)
    for sx in (-1, 1):
        b.capsule(0.078, 0.17, (sx * 0.1, 0, 0.27), pants)
        b.sphere(0.12, (sx * 0.1, -0.035, 0.075), shoe, scale=(0.95, 1.35, 0.7))
    b.capsule(0.215, 0.2, (0, 0, 0.66), shirt, taper=torso_taper)
    if stripe:
        for z in (0.58, 0.66, 0.74):
            rad = 0.215 + (0.215 * torso_taper - 0.215) * ((z - 0.56) / 0.2)
            b.cyl(rad + 0.004, 0.034, (0, 0, z), stripe, segs=24, bevel=0)
    for sx in (-1, 1):
        b.capsule(0.06, 0.2, (sx * 0.27, 0, 0.7), shirt, rot=(0, -sx * 18, 0))
        b.sphere(0.075, (sx * 0.325, -0.01, 0.52), skin)
    b.sphere(0.27, (0, 0, 1.13), skin, segs=28, rings=18, scale=(1.0, 0.96, 0.95))
    for sx in (-1, 1):
        b.sphere(0.08, (sx * 0.1, -0.215, 1.15), eyeW, scale=(1, 0.55, 1.25))
        b.sphere(0.04, (sx * 0.1, -0.255, 1.15), eyeB, scale=(1, 0.5, 1.3))
        b.sphere(0.012, (sx * 0.1 + 0.014, -0.275, 1.18), eyeW, segs=8, rings=6)
        b.capsule(0.014, 0.07, (sx * 0.1, -0.235, 1.265), brow, rot=(0, 90 + sx * -12, 0), segs=10)
        b.sphere(0.04, (sx * 0.17, -0.22, 1.06), blush, scale=(1, 0.4, 0.7))
    b.sphere(0.05, (0, -0.265, 1.08), blush, scale=(1, 0.8, 0.9))
    b.torus_arc(0.06, 0.009, 200, 340, (0, -0.222, 1.02), eyeB, steps=8, minor=6, rot=(90, 0, 0))


def dome(b, m, z_rim, z_top=None, r_head=0.27, squash=0.95, flare=0.012, segs=28, cz=1.13):
    """Skull cap that hugs the head above z_rim (beanie, bandana, helmet)."""
    z_top = z_top or cz + r_head * squash + 0.04
    a_rim = math.degrees(math.asin(min(1.0, (z_rim - cz) / (r_head * squash))))
    pts = [(0.0, z_top)]
    steps = 6
    for k in range(steps):
        a = 88 - (88 - a_rim) * k / (steps - 1)
        pts.append(((r_head + 0.012) * math.cos(math.radians(a)), cz + (r_head + 0.012) * math.sin(math.radians(a)) * squash))
    rr = (r_head + 0.012) * math.cos(math.radians(a_rim)) + flare
    pts += [(rr, z_rim + 0.012), (rr, z_rim - 0.012)]
    b.lathe(pts, (0, 0, 0), m, segs=segs)

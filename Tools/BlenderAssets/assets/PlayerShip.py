import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()

# Player ship hull that replaces the box prototype: 12 m beam, deck rectangle y -10..10 (the gameplay footprint), a pointed
# prow beyond it. Origin: keel at z=0. Deck top is z=1.65, bulwark cap z=2.65. +Y forward (Unity +Z).
hull = mat("Hull_Navy", hexc("#1f4b73"), 0.55); hullD = mat("Hull_Dark", hexc("#143550"), 0.6)
wale = mat("Wale", hexc("#f0e8d4"), 0.5); deck = mat("Deck_Wood", hexc("#b98350"), 0.8); plank = mat("Deck_Plank", hexc("#7a5030"), 0.85)
rail = mat("Rail_Wood", hexc("#8a5a32"), 0.7); port = mat("Port_Dark", hexc("#1d1f24"), 0.6)
DECK, BULWARK = 1.65, 1.0
HALF_LEN, BEAM = 10.0, 6.0

base = [(0.0, 0.15), (2.2, 0.1), (4.4, 0.5), (5.6, 1.05), (BEAM, DECK)]
def section(y, f, lift):
    return (y, [(x * f, z + lift * (z / DECK) ** 2) for x, z in base])
sections = [section(-10.2, 0.93, 0), section(-9.6, 1.0, 0), section(HALF_LEN, 1.0, 0), section(11.2, 0.78, 0.15),
            section(12.6, 0.46, 0.45), section(13.8, 0.14, 0.9)]
def hull_x(z):
    """Half-width of the hull side at height z (matches the loft profile above the turn of the bilge)."""
    pts = [(0.5, 4.4), (1.05, 5.6), (DECK, BEAM)]
    if z <= pts[0][0]: return pts[0][1]
    for (z0, x0), (z1, x1) in zip(pts, pts[1:]):
        if z <= z1: return x0 + (x1 - x0) * (z - z0) / (z1 - z0)
    return pts[-1][1]
PART = os.environ.get("PART", "all")                       # "hull" = paintable livery mesh, "rest" = everything else
HULL_ONLY, NO_HULL = PART == "hull", PART == "rest"
if not NO_HULL: b.loft(sections, hull)
if not HULL_ONLY:

    b.box((12.0, 19.9, 0.06), (0, 0, DECK + 0.02), deck, r=0)                      # planked deck
    for i in range(-5, 6):
        b.box((0.05, 19.9, 0.03), (i * 1.0, 0, DECK + 0.055), plank, r=0)

    for sx in (-1, 1):
        b.box((0.26, 20.4, BULWARK), (sx * (BEAM - 0.13), 0, DECK + BULWARK / 2), rail, r=0.04)   # bulwark
        b.box((0.4, 20.5, 0.14), (sx * (BEAM - 0.13), 0, DECK + BULWARK + 0.04), wale, r=0.05)      # cap rail
        b.box((0.08, 20.0, 0.18), (sx * (hull_x(1.25) + 0.03), 0, 1.25), wale, r=0.03)                    # wale stripe
        b.box((0.08, 20.0, 0.14), (sx * (hull_x(0.5) + 0.02), 0, 0.5), red, r=0.03)                        # waterline band
        for i in range(5):                                                                        # cannon ports
            b.box((0.14, 0.9, 0.62), (sx * (hull_x(0.85) + 0.02), -7.2 + i * 3.6, 0.85), port, r=0.04)
        for i in range(6):                                                                        # bulwark posts
            b.box((0.3, 0.3, BULWARK + 0.35), (sx * (BEAM - 0.13), -8.6 + i * 3.4, DECK + (BULWARK + 0.35) / 2), wale, r=0.05)
    b.box((12.4, 0.26, BULWARK), (0, -HALF_LEN + 0.1, DECK + BULWARK / 2), rail, r=0.04)          # stern rail
    b.box((12.5, 0.4, 0.14), (0, -HALF_LEN + 0.1, DECK + BULWARK + 0.04), wale, r=0.05)
    b.box((12.0, 0.2, 1.05), (0, -10.3, 1.05), hullD, r=0.04)                                      # transom
    for i in (-3.6, 0, 3.6):                                                                      # stern windows
        b.box((1.6, 0.12, 0.7), (i, -10.42, 1.15), wale, r=0.06)
        b.box((1.3, 0.14, 0.5), (i, -10.46, 1.15), port, r=0.05)

    # Prow: deck cover and bulwarks follow the hull outline given by the lifted bow sections.
    ring = [(HALF_LEN, BEAM, DECK)] + [(y, BEAM * f, DECK + lift) for y, f, lift in ((11.2, 0.78, 0.15), (12.6, 0.46, 0.45), (13.8, 0.14, 0.9))]
    def prow_deck(bm):
        rows = [(bm.verts.new((-x, y, z)), bm.verts.new((x, y, z))) for y, x, z in ring]
        for (l0, r0), (l1, r1) in zip(rows, rows[1:]):
            bm.faces.new((l0, r0, r1, l1))
    b._add(prow_deck, deck, (0, 0, 0.02), (0, 0, 0))
    def rail_segment(p0, p1):
        (x0, y0, z0), (x1, y1, z1) = p0, p1
        length = math.hypot(x1 - x0, y1 - y0) + 0.3
        ang = -math.degrees(math.atan2(x1 - x0, y1 - y0))
        zc = (z0 + z1) / 2
        b.box((0.26, length, BULWARK), ((x0 + x1) / 2, (y0 + y1) / 2, zc + BULWARK / 2), rail, r=0.04, rot=(0, 0, ang))
        b.box((0.4, length, 0.14), ((x0 + x1) / 2, (y0 + y1) / 2, zc + BULWARK + 0.04), wale, r=0.05, rot=(0, 0, ang))
    for sx in (-1, 1):
        for (y0, x0, z0), (y1, x1, z1) in zip(ring, ring[1:]):
            rail_segment((sx * (x0 - 0.13 * (y0 == HALF_LEN)), y0, z0), (sx * x1, y1, z1))
    b.cyl(0.2, 4.4, (0, 12.9, 3.0), rail, segs=14, rot=(-72, 0, 0))                                # bowsprit
    b.sphere(0.34, (0, 14.0, 2.2), brass, scale=(1, 1.3, 1))                                       # figurehead
    b.sphere(0.22, (0, 14.4, 3.15), wale)
    b.cyl(0.12, 1.5, (0, 9.2, DECK + 1.55 + 0.8), brass, segs=12)                                  # bow lamp post
    b.sphere(0.3, (0, 9.2, DECK + 2.55 + 0.55), brass)
bb = [v.co.copy() for v in b.bm.verts]
print("CENTER_Y", (min(v.y for v in bb) + max(v.y for v in bb)) / 2, "MINY", min(v.y for v in bb), "MAXY", max(v.y for v in bb))
if PART != "all":                                                       # pin the bounding box so both parts share one origin
    for pos in ((-6.25, -10.53, 0.0), (6.25, 15.04, 4.95)):
        b.box((0.002, 0.002, 0.002), pos, hull if HULL_ONLY else deck, r=0)
deliver(b, "PlayerShip" + {"all": "", "hull": "Hull", "rest": "Deco"}[PART])

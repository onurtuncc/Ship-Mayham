"""Gameplay props for swimming / gangway / fishing / digging / lock-picking, built with llm_helpers (art.md: chunky, warm, painted).
usage: blender -b --python props_pack.py -- <PropName> <out.blend>
Units metres, Z up, +Y front; origin bottom-centre unless a prop says otherwise (RopeLadder: top-centre, GangwayGate leaves: hinge)."""
import sys, importlib, math
import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)      # no default cube/camera/light
sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *
from mathutils import Vector

WHICH, OUT = sys.argv[sys.argv.index('--') + 1], sys.argv[sys.argv.index('--') + 2]
PI = math.pi

def palette():
    mat('Wood', (0.20, 0.09, 0.035)); mat('WoodDark', (0.12, 0.055, 0.02)); mat('WoodLight', (0.30, 0.15, 0.06))
    mat('Rope', (0.40, 0.30, 0.15), 0.95); mat('Brass', (0.55, 0.35, 0.04), 0.4); mat('Iron', (0.03, 0.03, 0.035), 0.5)
    mat('Red', (0.50, 0.02, 0.015)); mat('White', (0.80, 0.80, 0.78)); mat('Gold', (0.70, 0.50, 0.02), 0.3)

# ------------------------------------------------------------------ RopeLadder (origin = top centre, hangs 3 m down)
def rope_ladder():
    palette()
    W, Hh = 0.5, 3.0; side = W / 2 - 0.03
    for sx in (-1, 1):
        cyl(0.022, Hh, (sx * side, 0, -Hh / 2), 'Rope', 'Z', segments=8)
        for k in range(0, 11):
            sphere(0.034, (sx * side, 0, -0.1 - k * 0.28), 'Rope', 8)      # knots
    for i in range(10):
        cyl(0.034, W - 0.02, (0, 0, -0.28 - i * 0.28), 'Wood', 'X', segments=8, bevel=0.004)
    box((W + 0.12, 0.09, 0.09), (0, 0, -0.02), 'WoodDark', 0.02)             # top bar that hooks over the rail
    finish('RopeLadder')

# ------------------------------------------------------------------ GangwayGate (root + Frame + LeafLeft + LeafRight)
def gate():
    palette(); root = empty('GangwayGate')
    for sx in (-1, 1): box((0.16, 0.16, 2.05), (sx * 1.13, 0, 1.025), 'WoodDark', 0.025)
    box((2.45, 0.16, 0.16), (0, 0, 2.0), 'WoodDark', 0.025)
    box((2.45, 0.16, 0.10), (0, 0, 0.05), 'WoodDark', 0.02)
    for sx in (-1, 1): sphere(0.07, (sx * 1.13, 0, 2.1), 'Brass', 8)          # post caps
    finish_part('Frame', parent=root)
    def leaf(sx, name):
        x0 = sx * 1.0                                                          # hinge line (outer edge)
        for i in range(5):                                                      # vertical planks
            cx = x0 - sx * (0.1 + i * 0.2); tone = 'Wood' if i % 2 == 0 else 'WoodLight'
            box((0.19, 0.07, 1.8), (cx, 0, 1.0), tone, 0.012)
        for z in (0.35, 1.65): box((0.98, 0.09, 0.12), (x0 - sx * 0.5, 0.0, z), 'WoodDark', 0.015)       # cross braces
        box((0.1, 0.09, 1.3), (x0 - sx * 0.5, 0.0, 1.0), 'WoodDark', 0.015, rot=(0, 52 * sx, 0))         # diagonal brace
        for z in (0.35, 1.65): cyl(0.045, 0.2, (x0, 0, z), 'Iron', 'Z', segments=8)                         # hinges
        if sx == 1:                                                                                          # brass latch + chunky padlock on the free edge
            box((0.30, 0.05, 0.14), (0.20, -0.07, 1.0), 'Brass', 0.015)
            box((0.17, 0.07, 0.17), (0.16, -0.12, 0.88), 'Brass', 0.02)
            box((0.03, 0.05, 0.10), (0.09, -0.12, 1.0), 'Brass', 0.006); box((0.03, 0.05, 0.10), (0.23, -0.12, 1.0), 'Brass', 0.006); box((0.17, 0.05, 0.03), (0.16, -0.12, 1.06), 'Brass', 0.008)
            box((0.03, 0.02, 0.06), (0.16, -0.16, 0.88), 'Iron', 0.004)
        return finish_part(name, pivot=(x0, 0, 0), parent=root)
    leaf(-1, 'LeafLeft'); leaf(1, 'LeafRight')
    return root

# ------------------------------------------------------------------ Floaties (red/white swim ring for the waist)
def floaties():
    palette()
    ring(0.34, 0.10, (0, 0, 0.10), ['Red', 'White'], segs=16, tube=8)
    for k in range(4):
        a = k * PI / 2 + PI / 4
        box((0.07, 0.05, 0.03), (0.40 * math.cos(a), 0.40 * math.sin(a), 0.10), 'Brass', 0.008, rot=(0, 0, math.degrees(a)))   # valve plugs
    finish('Floaties')

# ------------------------------------------------------------------ FishingRod (root + Rod + Bobber)
def rod():
    palette(); root = empty('FishingRod')
    cyl(0.032, 0.34, (0, 0, 0.17), 'WoodLight', 'Z', segments=10, bevel=0.006)                     # cork handle
    cyl(0.026, 0.9, (0, 0, 0.79), 'Wood', 'Z', radius2=0.018, segments=8)
    cyl(0.018, 0.9, (0, 0, 1.69), 'WoodDark', 'Z', radius2=0.008, segments=8)
    cyl(0.044, 0.07, (0.06, 0, 0.30), 'Iron', 'X', segments=10)                                    # reel
    box((0.04, 0.04, 0.12), (0.03, 0, 0.30), 'Iron', 0.006); box((0.05, 0.016, 0.016), (0.10, 0.03, 0.30), 'Brass', 0.004)
    for z in (0.62, 1.02, 1.42): ring(0.016, 0.004, (0, 0.016, z), ['Brass'], 8, 4, 'Y')
    cyl(0.0045, 0.79, (0, 0.016, 1.745), 'White', 'Z', segments=6)                                    # line from the tip down to the bobber
    sphere(0.016, (0, 0, 2.14), 'Brass', 6)
    finish_part('Rod', parent=root)
    sphere(0.055, (0, 0.016, 1.35), 'White', 10); sphere(0.056, (0, 0.016, 1.372), 'Red', 10, squash=(1, 1, 0.55))
    box((0.012, 0.012, 0.10), (0, 0.016, 1.45), 'Brass', 0.003)
    finish_part('Bobber', pivot=(0, 0.016, 1.35), parent=root)

# ------------------------------------------------------------------ fish
def fish(length, body, belly, name):
    palette(); mat('Body', body); mat('Belly', belly); mat('Fin', tuple(c * 0.8 for c in body)); mat('Eye', (0.01, 0.01, 0.012))
    L = length; r = L * 0.20
    sphere(r, (0, 0, r * 1.1), 'Body', 12, squash=(0.62, 2.35, 1.0))
    sphere(r * 0.9, (0, L * 0.02, r * 0.78), 'Belly', 10, squash=(0.52, 2.0, 0.72))
    box((0.012, L * 0.30, L * 0.30), (0, -L * 0.52, r * 1.1), 'Fin', 0.006, rot=(0, 0, 0))             # tail
    box((0.012, L * 0.24, L * 0.28), (0, -L * 0.40, r * 1.1), 'Fin', 0.006, rot=(35, 0, 0))
    box((0.012, L * 0.30, L * 0.20), (0, -L * 0.02, r * 2.0), 'Fin', 0.006, rot=(0, 0, 0))              # dorsal fin
    for sx in (-1, 1):
        sphere(r * 0.42, (sx * r * 0.52, L * 0.33, r * 1.38), 'White', 8); sphere(r * 0.22, (sx * r * 0.74, L * 0.355, r * 1.40), 'Eye', 6)
        box((0.012, L * 0.14, L * 0.10), (sx * r * 0.62, L * 0.10, r * 0.85), 'Fin', 0.005, rot=(0, 0, sx * 28))
    box((r * 0.55, L * 0.05, r * 0.14), (0, L * 0.49, r * 0.95), 'Red', 0.004)                           # mouth
    finish(name)

def boot():
    palette(); mat('Leather', (0.10, 0.06, 0.03)); mat('Sole', (0.03, 0.02, 0.015)); mat('Slime', (0.05, 0.28, 0.06))
    box((0.17, 0.17, 0.22), (0, -0.13, 0.27), 'Leather', 0.025)                                           # shaft (short)
    box((0.18, 0.44, 0.13), (0, 0.05, 0.10), 'Leather', 0.03)                                             # long foot
    sphere(0.10, (0, 0.26, 0.11), 'Leather', 10, squash=(0.95, 1.25, 0.80))                               # rounded toe cap
    box((0.19, 0.50, 0.04), (0, 0.07, 0.03), 'Sole', 0.012); box((0.19, 0.11, 0.065), (0, -0.14, 0.04), 'Sole', 0.012)
    box((0.19, 0.19, 0.05), (0, -0.13, 0.385), 'Slime', 0.02)                                             # soggy cuff
    for k in range(3): sphere(0.03, (0.07 - k * 0.07, 0.10 + 0.04 * k, 0.17 + 0.02 * k), 'Slime', 6)        # weeds
    ob = finish('Fish_Boot')
    ob.scale = (0.62, 0.62, 0.62)                                                                       # 0.61 m -> ~0.38 m joke catch
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# ------------------------------------------------------------------ Shovel / DigSpot / DigHole
def shovel():
    palette()
    cyl(0.022, 1.05, (0, 0, 0.62), 'Wood', 'Z', segments=8, bevel=0.004)
    box((0.26, 0.05, 0.05), (0, 0, 1.16), 'WoodDark', 0.012)                                                 # D-grip bar
    for sx in (-1, 1): box((0.05, 0.05, 0.12), (sx * 0.12, 0, 1.11), 'WoodDark', 0.01)
    box((0.07, 0.07, 0.16), (0, 0, 0.18), 'Iron', 0.012)                                                       # socket
    box((0.24, 0.03, 0.26), (0, 0.0, 0.13), 'Iron', 0.012, rot=(-8, 0, 0))
    box((0.17, 0.03, 0.10), (0, 0.01, 0.02), 'Iron', 0.012, rot=(-8, 0, 0))
    finish('Shovel')

def dig_spot():
    palette(); mat('Dirt', (0.10, 0.055, 0.025)); mat('DirtLight', (0.17, 0.10, 0.05))
    sphere(0.42, (0, 0, 0.0), 'Dirt', 14, squash=(1.0, 1.0, 0.33))
    for a, rr in ((0.3, 0.30), (1.9, 0.33), (3.5, 0.28), (5.0, 0.31)): sphere(0.10, (rr * math.cos(a), rr * math.sin(a), 0.03), 'DirtLight', 8, squash=(1, 1, 0.6))
    for sgn in (-1, 1): box((0.70, 0.09, 0.035), (0, 0, 0.145), 'Red', 0.01, rot=(0, 0, 45 * sgn))      # painted X
    finish('DigSpot')

def dig_hole():
    palette(); mat('Dirt', (0.10, 0.055, 0.025)); mat('DirtLight', (0.17, 0.10, 0.05)); mat('Pit', (0.012, 0.007, 0.004))
    cyl(0.42, 0.10, (0, 0, 0.05), 'Dirt', 'Z', segments=16, bevel=0.012)
    cyl(0.29, 0.02, (0, 0, 0.105), 'Pit', 'Z', segments=16)
    for k in range(7):
        a = k * 2 * PI / 7 + 0.3; sphere(0.09, (0.40 * math.cos(a), 0.40 * math.sin(a), 0.10), 'DirtLight', 8, squash=(1, 1, 0.6))
    finish('DigHole')

# ------------------------------------------------------------------ LockBox / SunkenCoin / Barrel_Float
def lockbox():
    palette(); W, D, H = 0.5, 0.34, 0.26
    box((W, D, H), (0, 0, H / 2), 'WoodDark', 0.02)
    halfcyl(D / 2, W, (0, 0, H), 'Wood', segments=12, bevel=0.01)
    for bx in (-0.17, 0.17): box((0.06, D + 0.02, H + 0.02), (bx, 0, H / 2), 'Brass', 0.008); halfcyl(D / 2 + 0.01, 0.06, (bx, 0, H), 'Brass', segments=12)
    for sx in (-1, 1):
        for sy in (-1, 1): box((0.07, 0.07, 0.10), (sx * W / 2, sy * D / 2, 0.06), 'Brass', 0.008)
    box((0.19, 0.07, 0.17), (0, -D / 2 - 0.035, 0.14), 'Brass', 0.02)                                         # big padlock body
    box((0.026, 0.05, 0.10), (-0.06, -D / 2 - 0.035, 0.265), 'Brass', 0.006); box((0.026, 0.05, 0.10), (0.06, -D / 2 - 0.035, 0.265), 'Brass', 0.006)
    box((0.15, 0.05, 0.03), (0, -D / 2 - 0.035, 0.31), 'Brass', 0.008)                                         # shackle
    box((0.03, 0.02, 0.06), (0, -D / 2 - 0.075, 0.14), 'Iron', 0.004)                                          # keyhole
    finish('LockBox')

def coin():
    palette(); mat('GoldDark', (0.45, 0.30, 0.01), 0.35)
    cyl(0.15, 0.04, (0, 0, 0.02), 'GoldDark', 'Z', segments=24, bevel=0.006)
    ring(0.138, 0.022, (0, 0, 0.042), ['Gold'], 28, 6)                                                    # raised rolled rim
    cyl(0.108, 0.012, (0, 0, 0.044), 'Gold', 'Z', segments=24)
    box((0.04, 0.12, 0.014), (0, 0, 0.056), 'GoldDark', 0.003); box((0.12, 0.04, 0.014), (0, 0, 0.056), 'GoldDark', 0.003)   # embossed cross
    finish('SunkenCoin')

def barrel_float():
    palette()
    for z, r in ((0.12, 0.25), (0.30, 0.285), (0.48, 0.25)): cyl(r, 0.2, (0, 0, z), 'Wood', 'Z', segments=14, bevel=0.008)
    for z in (0.16, 0.44): cyl(0.29, 0.045, (0, 0, z), 'Iron', 'Z', segments=14)
    cyl(0.255, 0.02, (0, 0, 0.58), 'WoodLight', 'Z', segments=14)
    ring(0.23, 0.028, (0, 0, 0.30), ['Rope'], 14, 6, 'Z'); ring(0.10, 0.025, (0, 0, 0.62), ['Rope'], 10, 6, 'Z')
    sphere(0.05, (0, 0, 0.66), 'Red', 8)
    finish('Barrel_Float')

PROPS = {'RopeLadder': rope_ladder, 'GangwayGate': gate, 'Floaties': floaties, 'FishingRod': rod,
         'Fish_Small': lambda: fish(0.30, (0.10, 0.28, 0.55), (0.45, 0.60, 0.70), 'Fish_Small'),
         'Fish_Big': lambda: fish(0.50, (0.75, 0.22, 0.04), (0.85, 0.65, 0.35), 'Fish_Big'),
         'Fish_Boot': boot, 'Shovel': shovel, 'DigSpot': dig_spot, 'DigHole': dig_hole, 'LockBox': lockbox, 'SunkenCoin': coin, 'Barrel_Float': barrel_float}
PROPS[WHICH]()
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print('BUILT', WHICH, [o.name for o in bpy.data.objects])

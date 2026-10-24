"""Land clips on Mixamo skeletons: FishingIdle, FishingCast, FishingReel, Digging, LockPicking, DoorOpen, ClimbOut.
usage: blender -b --python clips_land.py -- <ClipName>"""
import sys, math
sys.path.insert(0, 'D:/blender/anim')
from clipkit import *
which = sys.argv[sys.argv.index('--') + 1]
TAU = 2 * math.pi
def V(*a): return Vector(a[0] if len(a) == 1 else a)
base_file = BASE_DIR + ('Mixamo_CrouchIdle.fbx' if which == 'LockPicking' else 'Mixamo_Idle.fbx')
k = Kit(base_file, 1)
k.reset()
FOOT = {s: k.head(s + 'Foot') for s in ('Right', 'Left')}

def plant_feet(pole=(0, 0, 1)):
    k.leg_ik('R', FOOT['Right'], pole); k.leg_ik('L', FOOT['Left'], pole)

def hands(r, l):
    k.arm_ik('R', r); k.arm_ik('L', l)

# ---------------- fishing
ROD_R = V((-9, 108, 30)); ROD_L = V((8, 112, 22))
def fishing_ready(sway=0.0, lean=4):
    k.rot('Spine', (lean / 3, 0, 0)); k.rot('Spine1', (lean / 3, 0, 0)); k.rot('Spine2', (lean / 3, 0, 0))
    k.rot('Head', (6, 8 * sway, 0))
    hands(ROD_R + V(0, 1.2 * sway, 0), ROD_L + V(0, -1.0 * sway, 0))

def fishing_idle(t):
    s = math.sin(TAU * t); fishing_ready(s, 4 + 1.5 * s); k.rot('Hips', (0, 0, 1.2 * s))

def fishing_reel(t):
    a = TAU * t
    k.rot('Spine2', (0, 5 * math.sin(a), 0)); k.rot('Head', (8, 0, 0)); k.rot('Spine', (3, 0, 0)); k.rot('Spine1', (3, 0, 0))
    hands(V((-12, 108 + 6.5 * math.cos(a), 26 + 6.5 * math.sin(a))), ROD_L)

def cast_pose(name):
    P_ = {'ready': (4, ROD_R, ROD_L), 'wind': (-14, V((-10, 172, -12)), V((0, 160, -6))), 'top': (2, V((-10, 178, 22)), V((0, 170, 18))),
          'release': (16, V((-9, 128, 56)), V((4, 124, 50)))}[name]
    lean, r, l = P_
    k.rot('Spine', (lean / 3, 0, 0)); k.rot('Spine1', (lean / 3, 0, 0)); k.rot('Spine2', (lean / 3, 0, 0))
    k.rot('Head', (6 + (6 if name == 'release' else 0), 0, 0)); hands(r, l)

# ---------------- digging
def digging(t):
    a = TAU * t; s = 0.5 - 0.5 * math.cos(a)             # 0 = lifted, 1 = blade in the ground
    lean = 12 + 16 * s
    k.rot('Spine', (lean / 3, 0, 0)); k.rot('Spine1', (lean / 3, 0, 0)); k.rot('Spine2', (lean / 3, 0, 0))
    k.rot('Head', (14 - lean * 0.4, 0, 0)); k.move('Hips', (0, -5 * s, 0))
    plant_feet()
    k.arm_ik('R', V((-9, 112 - 24 * s, 22 + 22 * s))); k.arm_ik('L', V((7, 86 - 16 * s, 40 + 26 * s)))

# ---------------- lock picking (crouched)
def lockpick(t):
    a = TAU * 3 * t; b = TAU * 2 * t
    k.rot('Head', (14, 4 * math.sin(TAU * t), 0)); k.rot('Spine2', (4, 0, 0))
    s2 = k.head('Spine2')
    k.arm_ik('R', s2 + V(-6 + 1.6 * math.cos(a), -9 + 1.6 * math.sin(a), 24)); k.arm_ik('L', s2 + V(5 + 0.8 * math.cos(b), -11 + 0.8 * math.sin(b), 23))

# ---------------- door push
def door(phase):
    cfg = {'rest': (0, None, None), 'raise': (3, V((-14, 118, 34)), V((14, 118, 34))), 'push': (9, V((-17, 119, 56)), V((17, 119, 56))), 'hold': (10, V((-18, 119, 58)), V((18, 119, 58)))}[phase]
    lean, r, l = cfg
    k.rot('Spine', (lean / 3, 0, 0)); k.rot('Spine1', (lean / 3, 0, 0)); k.rot('Spine2', (lean / 3, 0, 0))
    if r is not None: hands(r, l)

# ---------------- climb out of water onto a deck 45 cm above the surface (deck level = y 0 = standing foot height)
DECK_R = V((-22, 0, 38)); DECK_L = V((22, 0, 38))
def climb(stage):
    dy = {'hang': -185, 'pull': -150, 'chest': -105, 'knee': -62, 'stand': 0}[stage]
    k.move('Hips', (0, dy, 0))
    if stage == 'hang':
        k.rot('Spine1', (8, 0, 0)); hands(DECK_R, DECK_L)
        k.leg_ik('R', V(-10, dy + 8, 6), (0, 0, 1)); k.leg_ik('L', V(10, dy + 8, 2), (0, 0, 1))
    elif stage == 'pull':
        k.rot('Spine', (10, 0, 0)); k.rot('Spine1', (14, 0, 0)); k.rot('Spine2', (10, 0, 0)); hands(DECK_R, DECK_L)
        k.leg_ik('R', V(-10, dy + 20, -14), (0, 0, 1)); k.leg_ik('L', V(10, dy + 15, -18), (0, 0, 1))
    elif stage == 'chest':
        k.rot('Spine', (16, 0, 0)); k.rot('Spine1', (22, 0, 0)); k.rot('Spine2', (16, 0, 0)); hands(DECK_R + V(0, 0, 8), DECK_L + V(0, 0, 8))
        k.leg_ik('R', V(-12, -10, 20), (0, 0, 1)); k.leg_ik('L', V(10, dy + 30, -12), (0, 0, 1))
    elif stage == 'knee':
        k.rot('Spine', (12, 0, 0)); k.rot('Spine1', (12, 0, 0)); k.rot('Spine2', (10, 0, 0)); hands(DECK_R + V(0, 0, 12), DECK_L + V(0, 0, 12))
        k.leg_ik('R', V(-12, 0, 22), (0, 0, 1)); k.leg_ik('L', V(10, dy + 15, 8), (0, 0, 1))

clips = {
    'FishingIdle': ('cycle', 60, fishing_idle), 'FishingReel': ('cycle', 30, fishing_reel), 'Digging': ('cycle', 48, digging), 'LockPicking': ('cycle', 40, lockpick),
    'FishingCast': ('keys', [(1, 'ready'), (9, 'wind'), (15, 'top'), (21, 'release'), (36, 'ready')], cast_pose),
    'DoorOpen': ('keys', [(1, 'rest'), (8, 'raise'), (15, 'push'), (24, 'hold'), (36, 'rest')], door),
    'ClimbOut': ('keys', [(1, 'hang'), (14, 'pull'), (28, 'chest'), (40, 'knee'), (52, 'stand')], climb),
}
kind, arg, fn = clips[which]
if kind == 'cycle': k.cycle(arg, fn, step=2)
else:
    for f, name in arg:
        k.reset()
        if which == 'ClimbOut' and name == 'stand': pass
        else: fn(name)
        if which == 'FishingCast' and name == 'ready': pass
        k.key(f)
k.report(which, ['Hips', 'Head', 'RightHand', 'LeftHand', 'RightFoot', 'LeftFoot'])
k.export(f'D:/blender/anim/out/Mixamo_{which}.fbx')

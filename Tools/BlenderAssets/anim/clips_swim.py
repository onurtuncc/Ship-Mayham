"""Swim clips on the Mixamo_Idle skeleton: SwimSurface, TreadWater, SwimPanic. usage: blender -b --python clips_swim.py -- <SwimSurface|TreadWater|SwimPanic>"""
import sys, math
sys.path.insert(0, 'D:/blender/anim')
from clipkit import *
which = sys.argv[sys.argv.index('--') + 1]
k = Kit(BASE_DIR + 'Mixamo_Idle.fbx', 1)
TAU = 2 * math.pi
def V(*a): return Vector(a[0] if len(a) == 1 else a)

def surface(t):
    pitch = 84
    k.rot('Hips', (pitch, 0, 0))
    Rb = Euler((math.radians(pitch), 0, 0)).to_matrix()
    k.rot('Spine1', (0, 6 * math.sin(TAU * t), 0)); k.rot('Spine2', (0, 6 * math.sin(TAU * t), 0))      # body roll
    k.rot('Neck', (-32, 0, 0)); k.rot('Head', (-26, 0, 0))                                           # look ahead
    for side, sx, off in (('R', -1, 0.0), ('L', 1, 0.5)):
        s = 'Right' if side == 'R' else 'Left'
        a = TAU * (t + off)
        up = V((sx * 0.30, math.cos(a), math.sin(a)))
        fo = V((sx * 0.12, math.cos(a + 0.9), math.sin(a + 0.9)))
        k.aim(s + 'Arm', s + 'ForeArm', Rb @ up); k.aim(s + 'ForeArm', s + 'Hand', Rb @ fo)
        d = 0.30 * math.sin(TAU * 3 * t + (0 if side == 'R' else math.pi))
        thigh = V((sx * 0.06, -math.cos(d), math.sin(d))); shin = V((sx * 0.04, -math.cos(d * 0.8 - 0.15), math.sin(d * 0.8 - 0.15)))
        k.aim(s + 'UpLeg', s + 'Leg', Rb @ thigh); k.aim(s + 'Leg', s + 'Foot', Rb @ shin)

def tread(t):
    k.rot('Head', (-8, 0, 0))
    k.rot('Hips', (6 * math.sin(TAU * t), 0, 0))
    for side, sx, off in (('R', -1, 0.0), ('L', 1, 0.5)):
        s = 'Right' if side == 'R' else 'Left'
        a = TAU * (t + off)
        k.aim(s + 'Arm', s + 'ForeArm', V((sx * 0.85, -0.45, 0.15)))
        k.aim(s + 'ForeArm', s + 'Hand', V((sx * (0.45 + 0.45 * math.cos(a)), -0.30, 0.75 + 0.2 * math.sin(a))))
        thigh = V((sx * (0.25 + 0.1 * math.cos(a)), -0.70, 0.50 + 0.25 * math.sin(a)))
        shin = V((sx * (0.12 + 0.25 * math.sin(a)), -0.95, -0.25 + 0.22 * math.cos(a)))
        k.aim(s + 'UpLeg', s + 'Leg', thigh); k.aim(s + 'Leg', s + 'Foot', shin)

def panic(t):
    k.rot('Head', (-24, 0, 0)); k.rot('Neck', (-8, 0, 0))
    k.rot('Spine1', (-8, 14 * math.sin(TAU * 2 * t), 0)); k.rot('Spine2', (-6, 14 * math.sin(TAU * 2 * t), 0))
    for side, sx, off in (('R', -1, 0.0), ('L', 1, 0.43)):
        s = 'Right' if side == 'R' else 'Left'
        a = TAU * (t + off); a2 = TAU * 2 * (t + off)
        up = V((sx * (0.55 + 0.15 * math.sin(a2)), 0.70 + 0.28 * math.sin(a), 0.20 + 0.30 * math.cos(a)))
        fo = V((sx * (0.25 + 0.55 * math.sin(a + 1.0)), 0.45 + 0.5 * math.cos(a2), 0.55 * math.sin(a + 0.4)))
        k.aim(s + 'Arm', s + 'ForeArm', up); k.aim(s + 'ForeArm', s + 'Hand', fo)
        b = TAU * 2 * (t + off)
        thigh = V((sx * 0.18, -0.80, 0.45 + 0.40 * math.sin(b))); shin = V((sx * 0.05, -0.95, -0.30 + 0.5 * math.sin(b + 1.0)))
        k.aim(s + 'UpLeg', s + 'Leg', thigh); k.aim(s + 'Leg', s + 'Foot', shin)

clip = {'SwimSurface': (surface, 36), 'TreadWater': (tread, 48), 'SwimPanic': (panic, 24)}[which]
k.cycle(clip[1], clip[0], step=2)
k.report(which, ['Hips', 'Head', 'RightHand', 'LeftHand', 'RightFoot', 'LeftFoot'])
k.export(f'D:/blender/anim/out/Mixamo_{which}.fbx')

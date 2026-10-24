"""Mixamo_GetUpFromFall: lying face-down (last frame of Mixamo_FallingFlatImpact) -> DizzyIdle stance (frame 1), 45 frames, in place."""
import sys, math
sys.path.insert(0, 'D:/blender/anim')
from clipkit import *
k = Kit(BASE_DIR + 'Mixamo_Idle.fbx', 1)
main = k.arm
def pose_of(fbx, frame=None):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx)
    a = [o for o in bpy.data.objects if o.type == 'ARMATURE' and o not in before][0]
    act = a.animation_data.action if a.animation_data else bpy.data.actions[-1]
    f = int(act.frame_range[1]) if frame is None else frame
    k.sc.frame_set(f); bpy.context.view_layer.update()
    data = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy()) for pb in a.pose.bones}
    print('POSE', fbx.split('/')[-1], 'frame', f, 'hips loc', [round(x, 2) for x in data['mixamorig:Hips'][0]])
    return data
START = pose_of(BASE_DIR + 'Mixamo_FallingFlatImpact.fbx', None)
END = pose_of(BASE_DIR + 'Mixamo_DizzyIdle.fbx', 1)
k.sc.frame_set(1)
def blend(t):
    for name in k.base:
        l0, q0 = START[name]; l1, q1 = END[name]
        k.PB[name].location = l0.lerp(l1, t); k.PB[name].rotation_quaternion = q0.slerp(q1, t)
    k.upd()
def ease(x): return x * x * (3 - 2 * x)

# hand/foot contact points taken from the lying pose
blend(0.0)
HAND = {s: k.head(s + 'Hand') for s in ('Right', 'Left')}
TOE = {s: k.head(s + 'ToeBase') for s in ('Right', 'Left')}
KEYS = [(1, 0.0, 1.0, 1.0), (10, 0.22, 1.0, 1.0), (20, 0.50, 1.0, 0.0), (30, 0.74, 0.0, 0.0), (38, 0.92, 0.0, 0.0), (45, 1.0, 0.0, 0.0)]   # frame, blend t, hands planted, feet planted
for f, t, hp, fp in KEYS:
    k.reset(); blend(ease(t))
    if hp:      # hands stay on the floor under the shoulders while the chest rises
        for s, side in (('Right', 'R'), ('Left', 'L')):
            tgt = HAND[s].copy(); tgt.y = max(tgt.y, 6.0)
            k.arm_ik(side, tgt)
    if fp:      # toes stay planted
        for s, side in (('Right', 'R'), ('Left', 'L')):
            k.leg_ik(side, k.head(s + 'Foot') if t == 0 else TOE[s] + Vector((0, 4, -6)), (0, 0, 1)) if False else None
    k.key(f)
k.reset(); blend(1.0); k.key(45)       # exact end pose
k.report('GetUp', ['Hips', 'Head', 'RightHand', 'LeftHand', 'RightFoot'])
k.export('D:/blender/anim/out/Mixamo_GetUpFromFall.fbx', 1, 45)

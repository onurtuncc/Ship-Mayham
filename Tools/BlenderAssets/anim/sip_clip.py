"""Author Mixamo_SittingSip: seated, in-place sip on the Mixamo_SittingIdle skeleton (first and last pose == SittingIdle frame 1).
usage: blender -b --python sip_clip.py -- <out.fbx>"""
import bpy, math, sys
from mathutils import Vector, Matrix, Euler
OUT = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'D:/blender/repo-art/Assets/ShipMayham/Art/Generated/Animations/Mixamo_SittingIdle.fbx')
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
sc = bpy.context.scene; sc.frame_set(1); bpy.context.view_layer.update()
PB = arm.pose.bones; P = 'mixamorig:'
base = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy()) for pb in PB}
arm.animation_data_clear()
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for pb in PB: pb.rotation_mode = 'QUATERNION'

def rest_rot(b): return arm.data.bones[b].matrix_local.to_3x3()
def rest_head(b): return arm.data.bones[b].head_local.copy()
def aim(bone, child, d_des):
    d_rest = (rest_head(child) - rest_head(bone)).normalized()
    m_rot = d_rest.rotation_difference(Vector(d_des).normalized()).to_matrix() @ rest_rot(bone)
    parent = PB[bone].parent
    p_rot = parent.matrix.to_3x3() if parent else Matrix.Identity(3)
    p_rest = rest_rot(parent.name) if parent else Matrix.Identity(3)
    rel = p_rest.inverted() @ rest_rot(bone)
    PB[bone].rotation_quaternion = (rel.inverted() @ p_rot.inverted() @ m_rot).to_quaternion()
    bpy.context.view_layer.update()
def reset():
    for b, (loc, rot) in base.items():
        PB[b].location = loc; PB[b].rotation_quaternion = rot.copy()
    bpy.context.view_layer.update()
def add_euler(bone, deg):
    PB[bone].rotation_quaternion = Euler([math.radians(a) for a in deg], 'XYZ').to_quaternion() @ base[bone][1]
    bpy.context.view_layer.update()

reset()
head0 = PB[P + 'Head'].head.copy()
MOUTH = head0 + Vector((0, 11, 10))                 # cm, armature space (+z forward, y up, right = -x)
WRIST_T = MOUTH + Vector((-4, -3, -5))               # cup held just below/in front of the lips
L1 = (rest_head(P + 'RightForeArm') - rest_head(P + 'RightArm')).length
L2 = (rest_head(P + 'RightHand') - rest_head(P + 'RightForeArm')).length

def sip(head_tilt, wrist_target):
    reset()
    if head_tilt: add_euler(P + 'Head', (-head_tilt, 0, 0)); add_euler(P + 'Neck', (-head_tilt * 0.4, 0, 0))
    S = PB[P + 'RightArm'].head.copy(); d = wrist_target - S; dist = min(d.length, L1 + L2 - 0.5); dn = d.normalized()
    a = (L1 ** 2 - L2 ** 2 + dist ** 2) / (2 * dist); h = math.sqrt(max(L1 ** 2 - a ** 2, 0))
    pole = Vector((-1.0, -0.8, -0.1)); pole = (pole - dn * pole.dot(dn)).normalized()      # elbow out to the right and down
    E = S + dn * a + pole * h
    aim(P + 'RightArm', P + 'RightForeArm', E - S); aim(P + 'RightForeArm', P + 'RightHand', wrist_target - E)
    return PB[P + 'RightHand'].head.copy(), E

KEYS = [(1, 'base'), (28, 'up'), (44, 'tilt'), (66, 'tilt'), (80, 'up'), (90, 'base')]
rep = {}
for f, name in KEYS:
    if name == 'base': reset(); w = PB[P + 'RightHand'].head.copy()
    elif name == 'up': w, _ = sip(0, WRIST_T)
    else: w, _ = sip(9, WRIST_T)
    for pb in PB:
        pb.keyframe_insert('rotation_quaternion', frame=f); pb.keyframe_insert('location', frame=f)
    rep[(f, name)] = (w, PB[P + 'Hips'].location.copy(), PB[P + 'Hips'].rotation_quaternion.copy(), PB[P + 'Head'].head.copy())
sc.frame_start, sc.frame_end = 1, 90
bpy.data.actions[0].name = 'Armature|mixamo.com|Layer0'
for (f, n), (w, hl, hq, hh) in rep.items():
    print('REPORT', f, n, 'wrist', [round(x, 1) for x in w], 'wrist->mouth(cm)', round((w - MOUTH).length, 1), 'hipsloc', [round(x, 2) for x in hl], 'hipsq', [round(x, 3) for x in hq])
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_simplify_factor=0.0, add_leaf_bones=False, object_types={'ARMATURE'}, bake_anim_use_nla_strips=False)
print('EXPORTED', OUT)

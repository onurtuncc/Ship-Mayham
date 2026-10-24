"""Author Mixamo_RepairHammering on the Mixamo Idle armature. usage: blender -b --python hammer_clip.py -- <out.fbx> [--report]"""
import bpy, math, sys
from mathutils import Vector, Quaternion, Matrix, Euler

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else None
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'D:/blender/repo-art/Assets/ShipMayham/Art/Generated/Animations/Mixamo_Idle.fbx')
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
sc = bpy.context.scene
sc.frame_set(1); bpy.context.view_layer.update()
PB = arm.pose.bones
P = 'mixamorig:'

# base pose = Idle frame 1 (everything not authored below stays exactly like this)
base = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy()) for pb in PB}
arm.animation_data_clear()
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for pb in PB: pb.rotation_mode = 'QUATERNION'

def rest_rot(b): return arm.data.bones[b].matrix_local.to_3x3()
def rest_head(b): return arm.data.bones[b].head_local.copy()

def aim(bone, child, d_des):
    """Pose `bone` so the direction bone-head -> child-head points along d_des (character space: x lateral (right=-x), y up, z forward)."""
    d_rest = (rest_head(child) - rest_head(bone)).normalized()
    delta = d_rest.rotation_difference(Vector(d_des).normalized()).to_matrix()   # aligns rest direction to target with minimal twist
    m_rot = delta @ rest_rot(bone)
    parent = PB[bone].parent
    p_rot = parent.matrix.to_3x3() if parent else Matrix.Identity(3)
    p_rest = rest_rot(parent.name) if parent else Matrix.Identity(3)
    rel = p_rest.inverted() @ rest_rot(bone)
    q = (rel.inverted() @ p_rot.inverted() @ m_rot).to_quaternion()
    PB[bone].rotation_quaternion = q
    bpy.context.view_layer.update()

def euler_add(bone, deg):
    q = Euler([math.radians(a) for a in deg], 'XYZ').to_quaternion()
    PB[bone].rotation_quaternion = q @ base[bone][1]
    bpy.context.view_layer.update()

# key poses: (spine lean degrees, R upper arm dir, R forearm dir, L upper arm dir, L forearm dir)
POSES = {
    'windup': dict(lean=3,  ra=(-0.45, 0.35, 0.82), rf=(-0.15, 0.97, 0.20), la=(0.25, -0.55, 0.80), lf=(0.10, -0.25, 0.97)),
    'strike': dict(lean=10, ra=(-0.25, -0.30, 0.92), rf=(-0.10, -0.55, 0.83), la=(0.25, -0.55, 0.80), lf=(0.10, -0.25, 0.97)),
    'bounce': dict(lean=8,  ra=(-0.30, -0.10, 0.95), rf=(-0.12, -0.10, 0.99), la=(0.25, -0.55, 0.80), lf=(0.10, -0.25, 0.97)),
}

def apply_pose(name):
    for b, (loc, rot) in base.items():
        PB[b].location = loc; PB[b].rotation_quaternion = rot.copy()
    bpy.context.view_layer.update()
    p = POSES[name]
    for i, b in enumerate(('Spine', 'Spine1', 'Spine2')):
        euler_add(P + b, (p['lean'] / 3.0, 0, 0))
    # shoulders neutral (clavicle follows idle base) then aim arm chains
    aim(P + 'RightArm', P + 'RightForeArm', p['ra']); aim(P + 'RightForeArm', P + 'RightHand', p['rf'])
    aim(P + 'LeftArm', P + 'LeftForeArm', p['la']); aim(P + 'LeftForeArm', P + 'LeftHand', p['lf'])

KEYS = [(1, 'windup'), (9, 'strike'), (13, 'bounce'), (40, 'windup')]
report = {}
for f, name in KEYS:
    apply_pose(name)
    for pb in PB:
        pb.keyframe_insert('rotation_quaternion', frame=f)
        pb.keyframe_insert('location', frame=f)
    wrist = PB[P + 'RightHand'].head.copy(); elbow = PB[P + 'RightForeArm'].head.copy(); sh = PB[P + 'RightArm'].head.copy()
    report[(f, name)] = (sh, elbow, wrist)

sc.frame_start, sc.frame_end = 1, 40
act = arm.animation_data.action; act.name = 'Armature|mixamo.com|Layer0'
print('REPORT shoulder/elbow/wrist (armature cm, +z forward, y up, right=-x):')
for k, (s, e, w) in report.items():
    print('REPORT', k, 'elbow', [round(x, 1) for x in e], 'wrist', [round(x, 1) for x in w], 'wrist-minus-shoulder', [round(x, 1) for x in (w - s)])
if OUT:
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_simplify_factor=0.0, add_leaf_bones=False, object_types={'ARMATURE'}, bake_anim_use_nla_strips=False)
    print('EXPORTED', OUT)

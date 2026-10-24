"""usage: blender -b --python preview_clip.py -- <clip.fbx> <out.png> <frames comma> <cam: side|front|iso> [ortho_scale] [cz]"""
import bpy, math, sys
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]
FBX, OUT, FR, CAM = a[0], a[1], [int(x) for x in a[2].split(',')], a[3]
SCALE = float(a[4]) if len(a) > 4 else 2.6; CZ = float(a[5]) if len(a) > 5 else 0.9
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'; R = 300; sc.render.resolution_x = sc.render.resolution_y = R
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.78, 0.84, 0.9, 1)
l = bpy.data.objects.new('l', bpy.data.lights.new('l', 'SUN')); l.data.energy = 3; l.rotation_euler = (0.8, 0.2, -0.6); sc.collection.objects.link(l)
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); cam.data.type = 'ORTHO'; cam.data.ortho_scale = SCALE; sc.collection.objects.link(cam); sc.camera = cam
if CAM == 'side': cam.location = (-6, 0, CZ); cam.rotation_euler = (math.radians(90), 0, math.radians(-90))
elif CAM == 'front': cam.location = (0, -6, CZ); cam.rotation_euler = (math.radians(90), 0, 0)
else: cam.location = (-4, -4, CZ + 2.5); cam.rotation_euler = (math.radians(65), 0, math.radians(-45))
def mat(c):
    m = bpy.data.materials.new('m'); m.use_nodes = True; m.node_tree.nodes['Principled BSDF'].inputs[0].default_value = c; return m
skin = mat((0.8, 0.5, 0.35, 1)); left = mat((0.2, 0.4, 0.8, 1)); water = mat((0.2, 0.6, 0.8, 1))
def seg(a_, b_, r, m):
    d = b_ - a_; Ln = d.length
    if Ln < 1e-6: return
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=Ln, location=(a_ + b_) / 2); o = bpy.context.object
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d); o.data.materials.append(m); o.name = '_s'
CH = [('Hips', 'Spine'), ('Spine', 'Spine1'), ('Spine1', 'Spine2'), ('Spine2', 'Neck'), ('Neck', 'Head'), ('Head', 'HeadTop_End'), ('RightArm', 'RightForeArm'), ('RightForeArm', 'RightHand'), ('LeftArm', 'LeftForeArm'),
      ('LeftForeArm', 'LeftHand'), ('RightUpLeg', 'RightLeg'), ('RightLeg', 'RightFoot'), ('RightFoot', 'RightToeBase'), ('LeftUpLeg', 'LeftLeg'), ('LeftLeg', 'LeftFoot'), ('LeftFoot', 'LeftToeBase'), ('Spine2', 'RightArm'), ('Spine2', 'LeftArm')]
for i, f in enumerate(FR):
    sc.frame_set(f); bpy.context.view_layer.update()
    for o in [o for o in bpy.data.objects if o.name.startswith('_s')]: bpy.data.objects.remove(o)
    pos = lambda n: arm.matrix_world @ arm.pose.bones['mixamorig:' + n].head
    for x, y in CH:
        seg(pos(x), pos(y), 0.035, left if x.startswith('Left') or y.startswith('Left') else skin)
    sc.render.filepath = OUT.replace('.png', f'_{i}.png'); bpy.ops.render.render(write_still=True)
print('PREVIEWED', len(FR))

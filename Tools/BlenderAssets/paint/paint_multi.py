"""Hand-painted look for a generated prop: UV unwrap, bake a procedural painterly colour map, one textured material, FBX + PNG.

MULTI-OBJECT variant (keeps several mesh objects / a parent Empty, one shared atlas).
usage: blender -b <asset.blend> --python paint_multi.py -- <Name> <outdir> [size] [seed]
Output: <outdir>/<Name>.fbx, <outdir>/<Name>_Paint.png, <outdir>/<Name>_paint_preview.png
Painterly = broad paint patches, directional brush strokes, light worn edges (Bevel normal), darker crevices (AO), bottom dirt.
"""
import bpy, sys, os, math

argv = sys.argv[sys.argv.index('--') + 1:]
NAME, OUT = argv[0], argv[1]
SIZE = int(argv[2]) if len(argv) > 2 else 1024
SEED = float(argv[3]) if len(argv) > 3 else 1.0
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene
objs = [o for o in bpy.data.objects if o.type == 'MESH']
import mathutils as _mu
_pts = [o.matrix_world @ _mu.Vector(c) for o in objs for c in o.bound_box]
_dims = [max(p[i] for p in _pts) - min(p[i] for p in _pts) for i in range(3)]
K = min(max(max(_dims), 0.6), 3.0)
bpy.ops.object.select_all(action='DESELECT')
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]; ob = objs[0]

# --- UVs
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.015)
bpy.ops.object.mode_set(mode='OBJECT')

img = bpy.data.images.new(NAME + '_Paint', SIZE, SIZE, alpha=False)
img.colorspace_settings.name = 'sRGB'

import colorsys
def paint_nodes(mat, base_rgb):
    h_, s_, v_ = colorsys.rgb_to_hsv(*[min(1, max(0, c)) ** (1 / 2.2) for c in base_rgb])
    wood = (0.02 < h_ < 0.14 and s_ > 0.35) or any(k in mat.name.lower() for k in ('wood', 'plank', 'trunk', 'handle', 'frame', 'brace', 'mast'))
    if any(k in mat.name.lower() for k in ('skin', 'face', 'hand', 'head', 'cloth', 'shirt', 'sail', 'foam', 'leaf', 'stone', 'rock', 'cream', 'white', 'red', 'blue')): wood = False
    stroke_gain, patch_gain = (1.05, 0.42) if wood else (0.35, 0.75)

    """Replace the material surface with an emission of the painterly colour (baked later)."""
    mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    N = nt.nodes.new; L = nt.links.new
    out = N('ShaderNodeOutputMaterial'); em = N('ShaderNodeEmission')
    coord = N('ShaderNodeTexCoord')
    # broad paint patches
    patch = N('ShaderNodeTexNoise'); patch.noise_dimensions = '4D'
    patch.inputs['Scale'].default_value = 2.2 / K; patch.inputs['Detail'].default_value = 3; patch.inputs['W'].default_value = SEED
    # directional brush strokes: stretch noise along object X (long strokes) plus a second pass along Z
    mp1 = N('ShaderNodeMapping'); mp1.inputs['Scale'].default_value = (3.0 / K, 38.0 / K, 38.0 / K)
    st1 = N('ShaderNodeTexNoise'); st1.inputs['Scale'].default_value = 5.0; st1.inputs['Detail'].default_value = 6; st1.inputs['Roughness'].default_value = 0.65
    mp2 = N('ShaderNodeMapping'); mp2.inputs['Scale'].default_value = (38.0 / K, 38.0 / K, 3.0 / K)
    st2 = N('ShaderNodeTexNoise'); st2.inputs['Scale'].default_value = 5.0; st2.inputs['Detail'].default_value = 6; st2.inputs['Roughness'].default_value = 0.65
    L(coord.outputs['Object'], patch.inputs['Vector'])
    L(coord.outputs['Object'], mp1.inputs['Vector']); L(mp1.outputs['Vector'], st1.inputs['Vector'])
    L(coord.outputs['Object'], mp2.inputs['Vector']); L(mp2.outputs['Vector'], st2.inputs['Vector'])
    # value = 1 + 0.30*(patch-.5) + 0.34*(stroke-.5)   -> multiplicative tone variation
    def math_node(op, a=None, b=None, clamp=False):
        n = N('ShaderNodeMath'); n.operation = op; n.use_clamp = clamp
        if a is not None: n.inputs[0].default_value = a
        if b is not None: n.inputs[1].default_value = b
        return n
    s_mix = N('ShaderNodeMix'); s_mix.data_type = 'FLOAT'; s_mix.inputs[0].default_value = 0.5
    L(st1.outputs['Fac'], s_mix.inputs[2]); L(st2.outputs['Fac'], s_mix.inputs[3])
    pm = math_node('SUBTRACT', None, 0.5); L(patch.outputs['Fac'], pm.inputs[0])
    sm = math_node('SUBTRACT', None, 0.5); L(s_mix.outputs[0], sm.inputs[0])
    pm2 = math_node('MULTIPLY', None, patch_gain); L(pm.outputs[0], pm2.inputs[0])
    sm2 = math_node('MULTIPLY', None, stroke_gain); L(sm.outputs[0], sm2.inputs[0])
    tone = math_node('ADD', None, None); L(pm2.outputs[0], tone.inputs[0]); L(sm2.outputs[0], tone.inputs[1])
    gain = math_node('ADD', None, 1.0); L(tone.outputs[0], gain.inputs[0])
    base = N('ShaderNodeRGB'); base.outputs[0].default_value = (base_rgb[0], base_rgb[1], base_rgb[2], 1)
    painted = N('ShaderNodeMix'); painted.data_type = 'RGBA'; painted.blend_type = 'MULTIPLY'; painted.inputs[0].default_value = 1.0
    gain_c = N('ShaderNodeCombineColor');
    L(gain.outputs[0], gain_c.inputs[0]); L(gain.outputs[0], gain_c.inputs[1]); L(gain.outputs[0], gain_c.inputs[2])
    L(base.outputs[0], painted.inputs[6]); L(gain_c.outputs[0], painted.inputs[7])
    # worn edges: Bevel normal vs true normal
    bev = N('ShaderNodeBevel'); bev.inputs['Radius'].default_value = 0.05 * K; bev.samples = 8
    geo = N('ShaderNodeNewGeometry')
    dot = N('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
    L(bev.outputs['Normal'], dot.inputs[0]); L(geo.outputs['Normal'], dot.inputs[1])
    inv = math_node('SUBTRACT', 1.0, None, True); L(dot.outputs['Value'], inv.inputs[1])
    edge = math_node('MULTIPLY', None, 14.0, True); L(inv.outputs[0], edge.inputs[0])
    worn = N('ShaderNodeMix'); worn.data_type = 'RGBA'; worn.blend_type = 'ADD'
    wear_col = N('ShaderNodeRGB'); wear_col.outputs[0].default_value = (0.22, 0.13, 0.05, 1) if wood else (0.10, 0.10, 0.11, 1)    # warm worn highlight
    L(edge.outputs[0], worn.inputs[0]); L(painted.outputs[2], worn.inputs[6]); L(wear_col.outputs[0], worn.inputs[7])
    # crevices: AO darkening
    ao = N('ShaderNodeAmbientOcclusion'); ao.inputs['Distance'].default_value = 0.25 * K; ao.samples = 8
    L(worn.outputs[2], ao.inputs['Color'])
    ao_mix = N('ShaderNodeMix'); ao_mix.data_type = 'RGBA'; ao_mix.blend_type = 'MULTIPLY'; ao_mix.inputs[0].default_value = 1.0
    L(worn.outputs[2], ao_mix.inputs[6]); L(ao.outputs['AO'], ao_mix.inputs[7])
    ao_c = N('ShaderNodeCombineColor')
    # AO scalar -> grey colour (lift so crevices are not black)
    lift = math_node('MULTIPLY_ADD', None, 0.72); lift.inputs[2].default_value = 0.28; L(ao.outputs['AO'], lift.inputs[0])
    L(lift.outputs[0], ao_c.inputs[0]); L(lift.outputs[0], ao_c.inputs[1]); L(lift.outputs[0], ao_c.inputs[2])
    L(ao_c.outputs[0], ao_mix.inputs[7])
    # bottom dirt (object-space z gradient)
    sep = N('ShaderNodeSeparateXYZ'); L(coord.outputs['Object'], sep.inputs[0])
    dz = math_node('SUBTRACT', None, None); L(sep.outputs['Z'], dz.inputs[0])
    dz.inputs[1].default_value = 0.0
    dirt_f = math_node('MULTIPLY', None, -2.2 / K, True); L(dz.outputs[0], dirt_f.inputs[0])
    dirt_f2 = math_node('ADD', None, 0.0, True); L(dirt_f.outputs[0], dirt_f2.inputs[0])
    dirt = N('ShaderNodeMix'); dirt.data_type = 'RGBA'; dirt.blend_type = 'MULTIPLY'; dirt.inputs[7].default_value = (0.55, 0.50, 0.45, 1)
    L(dirt_f2.outputs[0], dirt.inputs[0]); L(ao_mix.outputs[2], dirt.inputs[6])
    hsv = N('ShaderNodeHueSaturation'); hsv.inputs['Saturation'].default_value = 1.35; hsv.inputs['Value'].default_value = 0.92
    L(dirt.outputs[2], hsv.inputs['Color']); L(hsv.outputs['Color'], em.inputs['Color'])
    L(em.outputs[0], out.inputs['Surface'])
    # bake target image node (must be active & selected)
    tex = N('ShaderNodeTexImage'); tex.image = img; tex.name = 'BAKE_TARGET'
    nt.nodes.active = tex; tex.select = True

old_mats = []
for _o in objs:
    for _m in _o.data.materials:
        if _m not in old_mats: old_mats.append(_m)
colors = []
for m in old_mats:
    c = (0.5, 0.5, 0.5)
    if m and m.use_nodes:
        b = m.node_tree.nodes.get('Principled BSDF')
        if b: c = tuple(b.inputs['Base Color'].default_value)[:3]
    colors.append(c)
for m, c in zip(old_mats, colors):
    paint_nodes(m, c)

sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 12
sc.cycles.bake_type = 'EMIT'
sc.render.bake.margin = 6; sc.render.bake.use_clear = True
bpy.ops.object.bake(type='EMIT')
png = os.path.join(OUT, NAME + '_Paint.png')
img.filepath_raw = png; img.file_format = 'PNG'; img.save()

# --- single textured material
final = bpy.data.materials.new(NAME + '_Paint'); final.use_nodes = True
nt = final.node_tree; bsdf = nt.nodes['Principled BSDF']
t = nt.nodes.new('ShaderNodeTexImage'); t.image = img
nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value = 0.78
for _o in objs:
    _o.data.materials.clear(); _o.data.materials.append(final)
    for p in _o.data.polygons: p.material_index = 0

# --- preview render (EEVEE, textured)
sc.render.engine = 'BLENDER_EEVEE'
import mathutils
mn = mathutils.Vector((1e9,) * 3); mx = mathutils.Vector((-1e9,) * 3)
for _o in objs:
  for c in _o.bound_box:
    w = _o.matrix_world @ mathutils.Vector(c); mn = mathutils.Vector(map(min, mn, w)); mx = mathutils.Vector(map(max, mx, w))
ctr = (mn + mx) / 2; size = max((mx - mn).length, 0.1)
cam = bpy.data.objects.new('_c', bpy.data.cameras.new('_c')); sc.collection.objects.link(cam); sc.camera = cam
d = size * 1.6; az = math.radians(35)
cam.location = ctr + mathutils.Vector((d * math.sin(az) * 1.1, -d * math.cos(az) * 1.1, d * 0.55))
cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
for n, loc, e in (('_k', (3, -4, 5), 3.0), ('_f', (-4, -2, 2), 1.0)):
    l = bpy.data.objects.new(n, bpy.data.lights.new(n, 'SUN')); l.data.energy = e
    l.rotation_euler = (mathutils.Vector((0, 0, 0)) - mathutils.Vector(loc)).to_track_quat('-Z', 'Y').to_euler(); sc.collection.objects.link(l)
w = sc.world or bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.75, 0.82, 0.9, 1)
sc.render.resolution_x = sc.render.resolution_y = 768
sc.render.filepath = os.path.join(OUT, NAME + '_paint_preview.png'); bpy.ops.render.render(write_still=True)

# --- FBX (Unity axes like the other generated models)
for o in sc.objects: o.select_set(o in objs or o.type == 'EMPTY')
bpy.context.view_layer.objects.active = objs[0]
for _o in objs:
    for p in _o.data.polygons: p.use_smooth = True
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         bake_space_transform=True, axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', add_leaf_bones=False,
                         path_mode='COPY', embed_textures=False, object_types={'MESH', 'EMPTY'})
print('PAINTED', NAME, png)

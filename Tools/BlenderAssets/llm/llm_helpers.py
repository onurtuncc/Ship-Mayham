import bpy, bmesh, math
from mathutils import Vector, Matrix

_MATS = {}
_BM = bmesh.new()
_MAT_LIST = []

def mat(name, rgb, rough=0.8):
    """Create/get a material. rgb is LINEAR (r,g,b)."""
    if name not in _MATS:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1)
        b.inputs["Roughness"].default_value = rough
        _MATS[name] = m
        _MAT_LIST.append(m)
    return name

def _finish_part(geom_bm, matname, bevel):
    if bevel > 0:
        bmesh.ops.bevel(geom_bm, geom=list(geom_bm.edges), offset=bevel, segments=2, affect='EDGES')
    idx = _MAT_LIST.index(_MATS[matname])
    for f in geom_bm.faces:
        f.material_index = idx
    me = bpy.data.meshes.new("_p")
    geom_bm.to_mesh(me); geom_bm.free()
    _BM.from_mesh(me); bpy.data.meshes.remove(me)

def box(size, center, matname, bevel=0.0, rot=(0, 0, 0)):
    """Box of size (sx,sy,sz) centered at center (x,y,z); rot = euler degrees (x,y,z) about its own center."""
    g = bmesh.new()
    bmesh.ops.create_cube(g, size=1.0)
    bmesh.ops.scale(g, vec=Vector(size), verts=g.verts)
    R = Matrix.Rotation(math.radians(rot[2]), 4, 'Z') @ Matrix.Rotation(math.radians(rot[1]), 4, 'Y') @ Matrix.Rotation(math.radians(rot[0]), 4, 'X')
    bmesh.ops.transform(g, matrix=Matrix.Translation(center) @ R, verts=g.verts)
    _finish_part(g, matname, bevel)

def cyl(radius, length, center, matname, axis='Z', radius2=None, segments=12, bevel=0.0):
    """Cylinder/cone with its axis along 'X','Y' or 'Z', centered at center. radius2 = radius at the +axis end."""
    g = bmesh.new()
    bmesh.ops.create_cone(g, cap_ends=True, segments=segments, radius1=radius, radius2=radius if radius2 is None else radius2, depth=length)
    rot = {'Z': Matrix.Identity(4), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'), 'Y': Matrix.Rotation(math.radians(-90), 4, 'X')}[axis]
    bmesh.ops.transform(g, matrix=Matrix.Translation(center) @ rot, verts=g.verts)
    _finish_part(g, matname, bevel)

def sphere(radius, center, matname, segments=12, squash=(1, 1, 1)):
    g = bmesh.new()
    bmesh.ops.create_uvsphere(g, u_segments=segments, v_segments=max(6, segments // 2), radius=radius)
    bmesh.ops.scale(g, vec=Vector(squash), verts=g.verts)
    bmesh.ops.translate(g, vec=Vector(center), verts=g.verts)
    _finish_part(g, matname, 0.0)

def finish(name):
    """Create the single mesh object; call once at the end."""
    me = bpy.data.meshes.new(name)
    _BM.to_mesh(me)
    for m in _MAT_LIST:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

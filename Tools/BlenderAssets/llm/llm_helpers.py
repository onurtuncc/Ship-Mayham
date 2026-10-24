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

def halfcyl(radius, length, center, matname, segments=14, bevel=0.0, rot=(0, 0, 0)):
    """Upper half of a cylinder lying along X (an arched/domed lid): flat underside at center z, arch up to center z + radius.
    center = middle of the flat underside. rot = euler degrees about that center point (e.g. (-35,0,0) tilts a hinged lid open)."""
    g = bmesh.new()
    ring = []
    for sx in (-length / 2, length / 2):
        ring.append([g.verts.new((sx, radius * math.cos(math.pi * k / segments), radius * math.sin(math.pi * k / segments))) for k in range(segments + 1)])
    a, b = ring
    for k in range(segments):
        g.faces.new((a[k], a[k + 1], b[k + 1], b[k]))
    g.faces.new((a[0], b[0], b[-1], a[-1]))
    g.faces.new(a)
    g.faces.new(list(reversed(b)))
    bmesh.ops.recalc_face_normals(g, faces=g.faces)
    R = Matrix.Rotation(math.radians(rot[2]), 4, 'Z') @ Matrix.Rotation(math.radians(rot[1]), 4, 'Y') @ Matrix.Rotation(math.radians(rot[0]), 4, 'X')
    bmesh.ops.transform(g, matrix=Matrix.Translation(center) @ R, verts=g.verts)
    _finish_part(g, matname, bevel)

def finish(name):
    """Create the single mesh object; call once at the end."""
    me = bpy.data.meshes.new(name)
    _BM.to_mesh(me)
    for m in _MAT_LIST:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def new_part():
    """Start a fresh mesh buffer so the next primitives go into another object (multi-object props)."""
    global _BM
    _BM = bmesh.new()

def finish_part(name, pivot=(0, 0, 0), parent=None):
    """Turn the current buffer into an object whose origin sits at `pivot` (world/parent-space), keep geometry in place, and start a new buffer."""
    global _BM
    bm = _BM
    bmesh.ops.translate(bm, vec=-Vector(pivot), verts=bm.verts)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in _MAT_LIST: me.materials.append(m)
    ob = bpy.data.objects.new(name, me); ob.location = Vector(pivot)
    bpy.context.scene.collection.objects.link(ob)
    if parent is not None: ob.parent = parent
    _BM = bmesh.new()
    return ob

def empty(name, loc=(0, 0, 0)):
    ob = bpy.data.objects.new(name, None); ob.location = Vector(loc); bpy.context.scene.collection.objects.link(ob); return ob

def ring(R, r, center, matnames, segs=16, tube=8, axis='Z'):
    """Torus (lifebuoy/ring) around `axis`; segment i uses matnames[i % len(matnames)] (e.g. red/white swim ring)."""
    g = bmesh.new()
    verts = [[g.verts.new((((R + r * math.cos(2 * math.pi * j / tube)) * math.cos(2 * math.pi * i / segs)),
                           ((R + r * math.cos(2 * math.pi * j / tube)) * math.sin(2 * math.pi * i / segs)),
                           r * math.sin(2 * math.pi * j / tube))) for j in range(tube)] for i in range(segs)]
    for i in range(segs):
        for j in range(tube):
            f = g.faces.new((verts[i][j], verts[i][(j + 1) % tube], verts[(i + 1) % segs][(j + 1) % tube], verts[(i + 1) % segs][j]))
            f.material_index = _MAT_LIST.index(_MATS[matnames[i % len(matnames)]])
    bmesh.ops.recalc_face_normals(g, faces=g.faces)
    rot = {'Z': Matrix.Identity(4), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'), 'Y': Matrix.Rotation(math.radians(-90), 4, 'X')}[axis]
    bmesh.ops.transform(g, matrix=Matrix.Translation(center) @ rot, verts=g.verts)
    me = bpy.data.meshes.new("_r"); g.to_mesh(me); g.free()
    _BM.from_mesh(me); bpy.data.meshes.remove(me)

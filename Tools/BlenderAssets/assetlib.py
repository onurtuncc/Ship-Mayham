"""Headless Blender helpers: build stylized low-poly props from primitives, export FBX, render preview.

Run:  blender -b --python <script> -- <outdir>
Convention: meters, Z up in Blender, +Y forward; origin bottom-center; one mesh with one slot per material.
"""
import math
import os
import sys

import bmesh
import bpy
import mathutils

_mats = {}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()


def mat(name, rgb, rough=0.8, emit=None, metal=0.0):
    if name in _mats:
        return _mats[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = 2.0
    m.diffuse_color = (*rgb, 1)
    _mats[name] = m
    return m


def hexc(h):
    h = h.lstrip("#")
    lin = lambda c: ((c / 255 + 0.055) / 1.055) ** 2.4 if c / 255 > 0.04045 else c / 255 / 12.92
    return tuple(lin(int(h[i:i + 2], 16)) for i in (0, 2, 4))


def std():
    """Shared game palette so every asset matches."""
    return dict(
        wood=mat("Wood", hexc("#9a6a3c")), woodD=mat("Wood_Dark", hexc("#5f3d22")), woodL=mat("Wood_Light", hexc("#c08a52")),
        metal=mat("Metal", hexc("#5d6772"), 0.5, metal=0.6), metalD=mat("Metal_Dark", hexc("#2c3138"), 0.6, metal=0.5),
        brass=mat("Brass", hexc("#d9a63a"), 0.4, metal=0.7), cream=mat("Cream", hexc("#f0e8d4")), red=mat("Red", hexc("#cf3a2d")),
        navy=mat("Navy", hexc("#25435f")), rope=mat("Rope", hexc("#c9a35a"), 0.9),
        stone=mat("Stone", hexc("#8b8d92"), 0.95), stoneD=mat("Stone_Dark", hexc("#6a6c72"), 0.95))


class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def _idx(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def _place(self, verts_before, m, loc, rot, scale=(1, 1, 1)):
        mi = self._idx(m)
        new = [v for v in self.bm.verts if v.index >= verts_before] if False else None
        return mi

    def _add(self, builder_fn, m, loc, rot, scale=(1, 1, 1)):
        """builder_fn(bm) -> geometry dict; transform the new geometry and assign material."""
        mi = self._idx(m)
        before_f = set(self.bm.faces)
        before_v = set(self.bm.verts)
        builder_fn(self.bm)
        newv = [v for v in self.bm.verts if v not in before_v]
        newf = [f for f in self.bm.faces if f not in before_f]
        mat_m = (mathutils.Matrix.Translation(loc) @ mathutils.Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4()
                 @ mathutils.Matrix.Diagonal((*scale, 1)))
        bmesh.ops.transform(self.bm, matrix=mat_m, verts=newv)
        for f in newf:
            f.material_index = mi
            f.smooth = False

    def box(self, size, loc, m, rot=(0, 0, 0)):
        sx, sy, sz = size
        self._add(lambda bm: bmesh.ops.create_cube(bm, size=1.0), m, loc, rot, (sx, sy, sz))

    def cyl(self, r, depth, loc, m, segs=12, r2=None, rot=(0, 0, 0)):
        r2 = r if r2 is None else r2
        self._add(lambda bm: bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=r2, depth=depth), m, loc, rot)

    def sphere(self, r, loc, m, segs=12, rings=8, scale=(1, 1, 1), rot=(0, 0, 0)):
        self._add(lambda bm: bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=r), m, loc, rot, scale)

    def torus(self, R, r, loc, m, major=24, minor=8, rot=(0, 0, 0)):
        def fn(bm):
            verts = []
            for i in range(major):
                a = 2 * math.pi * i / major
                ring = []
                for j in range(minor):
                    b = 2 * math.pi * j / minor
                    x = (R + r * math.cos(b)) * math.cos(a)
                    y = (R + r * math.cos(b)) * math.sin(a)
                    ring.append(bm.verts.new((x, y, r * math.sin(b))))
                verts.append(ring)
            for i in range(major):
                for j in range(minor):
                    bm.faces.new((verts[i][j], verts[(i + 1) % major][j], verts[(i + 1) % major][(j + 1) % minor], verts[i][(j + 1) % minor]))
        self._add(fn, m, loc, rot)

    def torus_arc(self, R, r, a0, a1, loc, m, steps=6, minor=8, rot=(0, 0, 0)):
        """Segment of a torus between angles a0..a1 (degrees), capped."""
        def fn(bm):
            rings = []
            for i in range(steps + 1):
                a = math.radians(a0 + (a1 - a0) * i / steps)
                ring = []
                for j in range(minor):
                    b = 2 * math.pi * j / minor
                    ring.append(bm.verts.new(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b))))
                rings.append(ring)
            for i in range(steps):
                for j in range(minor):
                    bm.faces.new((rings[i][j], rings[i + 1][j], rings[i + 1][(j + 1) % minor], rings[i][(j + 1) % minor]))
            bm.faces.new(rings[0][::-1])
            bm.faces.new(rings[-1])
        self._add(fn, m, loc, rot)

    def wedge(self, size, loc, m, rot=(0, 0, 0)):
        """Triangular prism: base sx*sy, rising to a ridge along Y at top (height sz)."""
        sx, sy, sz = size
        def fn(bm):
            p = [(-sx / 2, -sy / 2, -sz / 2), (sx / 2, -sy / 2, -sz / 2), (0, -sy / 2, sz / 2),
                 (-sx / 2, sy / 2, -sz / 2), (sx / 2, sy / 2, -sz / 2), (0, sy / 2, sz / 2)]
            v = [bm.verts.new(c) for c in p]
            for f in ((0, 2, 1), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)):
                bm.faces.new([v[i] for i in f])
        self._add(fn, m, loc, rot)

    def finish(self, name):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        zs = [v.co.z for v in bm.verts]; xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]
        bmesh.ops.translate(bm, vec=(-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs)), verts=bm.verts)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me); bm.free()
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def export_fbx(ob, path):
    for o in bpy.context.scene.objects:
        o.select_set(o is ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             bake_space_transform=True, axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False)


def render(ob, path_base, res=768):
    sc = bpy.context.scene
    mn = mathutils.Vector(ob.bound_box[0]); mx = mathutils.Vector(ob.bound_box[6])
    ctr = (mn + mx) / 2; size = max((mx - mn).length, 0.1)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    for n, rot, e in (("key", (math.radians(50), 0, math.radians(35)), 3.5), ("fill", (math.radians(65), 0, math.radians(-140)), 1.2)):
        l = bpy.data.objects.new(n, bpy.data.lights.new(n, "SUN")); l.data.energy = e; l.rotation_euler = rot; sc.collection.objects.link(l)
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.82, 0.9, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = sc.render.resolution_y = res
    out = []
    for suffix, az in (("", 35), ("_back", 215)):
        a = math.radians(az); d = size * 1.7
        cam.location = ctr + mathutils.Vector((d * math.sin(a), -d * math.cos(a), d * 0.5))
        cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = path_base + suffix + ".png"
        bpy.ops.render.render(write_still=True)
        out.append(sc.render.filepath)
    return out


def outdir():
    return sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.getcwd()


def deliver(b, name, flip=False):
    """flip=True turns the asset 180deg about Z: characters are authored facing -Y but must face +Y (Unity +Z)."""
    if flip:
        bmesh.ops.rotate(b.bm, cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.pi, 3, "Z"), verts=b.bm.verts)
    ob = b.finish(name)
    d = outdir(); os.makedirs(d, exist_ok=True)
    export_fbx(ob, os.path.join(d, name + ".fbx"))
    render(ob, os.path.join(d, name))
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print(f"DELIVERED {name} tris={tris} size={tuple(round(x, 3) for x in ob.dimensions)} mats={[m.name for m in b.mats]}")

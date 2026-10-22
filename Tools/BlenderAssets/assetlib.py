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

    @staticmethod
    def _bevel_sharp(bm, verts, offset, seg, min_angle=28):
        """Round every hard edge of freshly created geometry: the soft, chunky toy look."""
        edges = {e for v in verts for e in v.link_edges}
        sel = [e for e in edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(min_angle)]
        if sel and offset > 1e-4:
            bmesh.ops.bevel(bm, geom=sel, offset=offset, offset_type="OFFSET", segments=seg, profile=0.5, affect="EDGES")

    def box(self, size, loc, m, rot=(0, 0, 0), r=None, seg=2):
        """Rounded box. r=None picks a bevel proportional to the smallest side (capped); r=0 keeps hard edges."""
        sx, sy, sz = size
        rr = min(0.06, min(size) * 0.22) if r is None else min(r, min(size) * 0.45)

        def fn(bm):
            vs = bmesh.ops.create_cube(bm, size=1.0)["verts"]
            for v in vs:
                v.co.x *= sx; v.co.y *= sy; v.co.z *= sz
            self._bevel_sharp(bm, vs, rr, seg if rr > 0.006 else 1)
        self._add(fn, m, loc, rot)

    def cyl(self, r, depth, loc, m, segs=12, r2=None, rot=(0, 0, 0), bevel=None, seg=2):
        """Rounded cylinder / cone frustum (bevel on cap rims; bevel=0 for sharp)."""
        r2 = r if r2 is None else r2
        segs = max(segs, 16) if min(r, r2 if r2 else r) > 0.06 else max(segs, 10)
        rr = min(0.04, min(r, r2 if r2 > 0.01 else r, depth) * 0.3) if bevel is None else bevel

        def fn(bm):
            vs = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=r2, depth=depth)["verts"]
            if rr > 0.004 and r2 > 0.01:
                self._bevel_sharp(bm, vs, rr, seg, min_angle=35)
        self._add(fn, m, loc, rot)

    def sphere(self, r, loc, m, segs=12, rings=8, scale=(1, 1, 1), rot=(0, 0, 0)):
        adaptive = int(min(24, max(10, 10 + r * 60)))          # small spheres (eyes, knobs) stay cheap, big ones stay round
        segs, rings = max(segs, adaptive), max(rings, int(adaptive * 0.6))
        self._add(lambda bm: bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=r), m, loc, rot, scale)

    def lathe(self, profile, loc, m, segs=20, rot=(0, 0, 0), scale=(1, 1, 1)):
        """Revolve [(radius, z), ...] (bottom to top) around Z. radius 0 makes a pole; open ends get flat caps."""
        def fn(bm):
            rings = []
            for rad, z in profile:
                if rad < 1e-6:
                    rings.append(bm.verts.new((0, 0, z)))
                else:
                    rings.append([bm.verts.new((rad * math.cos(2 * math.pi * i / segs), rad * math.sin(2 * math.pi * i / segs), z)) for i in range(segs)])
            for a, c in zip(rings, rings[1:]):
                for i in range(segs):
                    j = (i + 1) % segs
                    if isinstance(a, list) and isinstance(c, list):
                        bm.faces.new((a[i], a[j], c[j], c[i]))
                    elif isinstance(a, list):
                        bm.faces.new((a[i], a[j], c))
                    else:
                        bm.faces.new((a, c[j], c[i]))
            if isinstance(rings[0], list):
                bm.faces.new(rings[0][::-1])
            if isinstance(rings[-1], list):
                bm.faces.new(rings[-1])
        self._add(fn, m, loc, rot, scale)

    def capsule(self, r, length, loc, m, rot=(0, 0, 0), segs=None, taper=1.0, n=5):
        """Smooth pill of overall height length + 2r (axis = local Z); taper scales the top radius."""
        segs = segs or int(min(20, max(10, 10 + r * 60)))
        rt = r * taper
        prof = [(0.0, -length / 2 - r)]
        for k in range(1, n):
            ang = math.radians(-90 + 90 * k / n)
            prof.append((r * math.cos(ang), -length / 2 + r * math.sin(ang)))
        prof += [(r, -length / 2), (rt, length / 2)]
        for k in range(1, n):
            ang = math.radians(90 * k / n)
            prof.append((rt * math.cos(ang), length / 2 + rt * math.sin(ang)))
        prof.append((0.0, length / 2 + rt))
        self.lathe(prof, loc, m, segs=segs, rot=rot)

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

    def loft(self, sections, m, loc=(0, 0, 0)):
        """Hull-like solid along +Y. sections: [(y, [(x, z), ...])] with each profile listed from the keel
        centre up to the gunwale on the +X side (first x should be 0). The profile is mirrored to -X,
        consecutive sections are bridged, both ends are capped."""
        def ring(bm, y, prof):
            right = [bm.verts.new((x, y, z)) for x, z in prof]
            left = [bm.verts.new((-x, y, z)) for x, z in prof[1:]][::-1]
            return left + right
        def fn(bm):
            rings = [ring(bm, y, p) for y, p in sections]
            n = len(rings[0])
            for a, c in zip(rings, rings[1:]):
                for i in range(n - 1):
                    bm.faces.new((a[i], a[i + 1], c[i + 1], c[i]))
                bm.faces.new((a[n - 1], a[0], c[0], c[n - 1]))      # close across the deck opening
            bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
        self._add(fn, m, loc, (0, 0, 0))

    def finish(self, name):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        for f in bm.faces:
            f.smooth = True
        for e in bm.edges:                      # hard edges only where the surface really breaks (>40 deg)
            if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(40):
                e.smooth = False
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
                             bake_space_transform=True, axis_forward="-Z", axis_up="Y", mesh_smooth_type="EDGE", add_leaf_bones=False)


def render(ob, path_base, res=768, az0=35):
    sc = bpy.context.scene
    mn = mathutils.Vector(ob.bound_box[0]); mx = mathutils.Vector(ob.bound_box[6])
    ctr = (mn + mx) / 2; size = max((mx - mn).length, 0.1)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 55
    # ground disc catches a soft contact shadow
    gm = mat("_ground", (0.42, 0.52, 0.6), 1.0)
    gme = bpy.data.meshes.new("_g"); gbm = bmesh.new(); bmesh.ops.create_circle(gbm, cap_ends=True, radius=size * 5, segments=48)
    gbm.to_mesh(gme); gbm.free(); gme.materials.append(gm)
    g = bpy.data.objects.new("_g", gme); g.location = (ctr.x, ctr.y, -0.002); sc.collection.objects.link(g)
    for n, rot, e, ang in (("key", (math.radians(48), 0, math.radians(30)), 3.2, 0.12), ("fill", (math.radians(70), 0, math.radians(-150)), 1.1, 0.3)):
        l = bpy.data.objects.new(n, bpy.data.lights.new(n, "SUN")); l.data.energy = e; l.data.angle = ang; l.rotation_euler = rot
        l.data.color = (1.0, 0.96, 0.9) if n == "key" else (0.8, 0.88, 1.0); sc.collection.objects.link(l)
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.78, 0.86, 0.95, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.75
    sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x = sc.render.resolution_y = res
    out = []
    for suffix, az in (("", az0), ("_back", az0 + 180)):
        a = math.radians(az); d = size * 1.75
        cam.location = ctr + mathutils.Vector((d * math.sin(a), -d * math.cos(a), d * 0.45))
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
    render(ob, os.path.join(d, name), az0=215 if flip else 35)
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    import json
    with open(os.path.join(d, name + ".json"), "w") as f:
        json.dump({"name": name, "tris": tris, "size": [round(x, 3) for x in ob.dimensions], "materials": [m.name for m in b.mats]}, f)
    print(f"DELIVERED {name} tris={tris} size={tuple(round(x, 3) for x in ob.dimensions)} mats={[m.name for m in b.mats]}")

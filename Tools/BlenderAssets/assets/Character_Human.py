import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from assetlib import *
reset(); globals().update(std()); b = Builder()

# T-pose passenger for the Mixamo auto-rigger (arms along +-X, legs apart, mouth/chin/knees/elbows readable).
# Authored facing -Y; deliver(..., flip=True) turns it to +Y (Unity +Z). ~1.62 m tall, oversized eyes and head.
V = os.environ.get("VARIANT", "1")
looks = {
    "1": dict(shirt="#2fb4a8", shirt2="#f2c230", pants="#c9a46a", skin="#f2b48c", hair="#5a3a22", style="short"),
    "2": dict(shirt="#2b4f8f", shirt2="#f4efe2", pants="#2a2d3a", skin="#d99a6c", hair="#1c1a1f", style="short"),
    "3": dict(shirt="#e8527a", shirt2="#f4efe2", pants="#3b5d8c", skin="#8e5a3c", hair="#2a1a14", style="bun"),
}[V]
shirt = mat("Shirt", hexc(looks["shirt"]), 0.55); shirt2 = mat("Shirt_Accent", hexc(looks["shirt2"]), 0.55)
pants = mat("Pants", hexc(looks["pants"]), 0.6); shoe = mat("Shoes", hexc("#3a2a20"), 0.45)
skin = mat("Skin", hexc(looks["skin"]), 0.55); hair = mat("Hair", hexc(looks["hair"]), 0.6)
eyeW = mat("Eye_White", hexc("#ffffff"), 0.25); eyeB = mat("Eye_Black", hexc("#15171c"), 0.2)
blush = mat("Skin_Blush", hexc("#ee8f7a"), 0.6)

for sx in (-1, 1):
    b.capsule(0.088, 0.23, (sx * 0.105, 0, 0.68), pants)                                  # thigh
    b.capsule(0.066, 0.27, (sx * 0.105, 0, 0.28), pants)                                  # shin
    b.sphere(0.1, (sx * 0.105, -0.045, 0.065), shoe, scale=(0.92, 1.6, 0.62))             # shoe
b.capsule(0.17, 0.08, (0, 0, 0.9), pants, taper=1.0)                                      # hips
b.capsule(0.17, 0.27, (0, 0, 1.14), shirt, taper=1.1)                                     # torso
b.cyl(0.176, 0.05, (0, 0, 0.99), shirt2, segs=24, bevel=0)                                # hem stripe
for sx in (-1, 1):
    b.capsule(0.058, 0.19, (sx * 0.355, 0, 1.35), shirt, rot=(0, 90, 0))                  # sleeve / upper arm (X axis)
    b.capsule(0.046, 0.19, (sx * 0.64, 0, 1.35), skin, rot=(0, 90, 0))                    # forearm
    b.sphere(0.07, (sx * 0.83, 0, 1.35), skin, scale=(1.35, 0.55, 1.0))                   # hand
    b.capsule(0.022, 0.05, (sx * 0.83, -0.075, 1.35), skin, rot=(0, 90, -sx * 35))        # thumb
b.capsule(0.055, 0.05, (0, 0, 1.4), skin)                                                  # neck
b.sphere(0.215, (0, 0, 1.52), skin, segs=28, rings=18, scale=(1.0, 0.96, 1.02))           # head
for sx in (-1, 1):
    b.sphere(0.092, (sx * 0.085, -0.15, 1.56), eyeW, scale=(1, 0.7, 1.15))                # big eyes
    b.sphere(0.04, (sx * 0.085, -0.225, 1.56), eyeB, scale=(1, 0.5, 1.2))
    b.sphere(0.013, (sx * 0.085 + 0.015, -0.248, 1.59), eyeW, segs=8, rings=6)
    b.capsule(0.013, 0.06, (sx * 0.085, -0.2, 1.67), hair, rot=(0, 90 + sx * -10, 0), segs=10)  # brow
    b.sphere(0.03, (sx * 0.215, 0, 1.51), skin, scale=(0.5, 0.9, 1.0))                    # ear
    b.sphere(0.038, (sx * 0.14, -0.17, 1.44), blush, scale=(1, 0.4, 0.7))
b.sphere(0.04, (0, -0.215, 1.5), skin, scale=(1, 0.9, 1.0))                                # nose
b.torus_arc(0.05, 0.008, 200, 340, (0, -0.19, 1.44), eyeB, steps=8, minor=6, rot=(90, 0, 0))   # smile
b.sphere(0.226, (0, 0.03, 1.57), hair, segs=26, rings=14, scale=(1.0, 0.95, 0.8))         # hair cap
if looks["style"] == "bun":
    b.sphere(0.09, (0, 0.1, 1.76), hair)
if os.environ.get("WELD") == "1":
    # Mixamo's auto-rigger fails on bodies made of overlapping parts: weld everything into one watertight shell
    # (voxel remesh) and carry the colours over by nearest original face.
    from mathutils.bvhtree import BVHTree
    bmesh.ops.rotate(b.bm, cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.pi, 3, "Z"), verts=b.bm.verts)
    src = b.finish("Character_Human_src")
    ref_bm = bmesh.new(); ref_bm.from_mesh(src.data); ref_bm.transform(src.matrix_world)
    tree = BVHTree.FromBMesh(ref_bm); ref_mats = [f.material_index for f in ref_bm.faces]
    rm = src.modifiers.new("remesh", "REMESH"); rm.mode = "VOXEL"; rm.voxel_size = 0.014
    bpy.context.view_layer.objects.active = src
    bpy.ops.object.modifier_apply(modifier="remesh")
    me = src.data
    for poly in me.polygons:
        _, _, idx, _ = tree.find_nearest(poly.center)
        poly.material_index = ref_mats[idx]
        poly.use_smooth = True
    me.name = src.name = "Character_Human" + V + "_Welded"
    d = outdir(); os.makedirs(d, exist_ok=True)
    export_fbx(src, os.path.join(d, src.name + ".fbx"))
    print("DELIVERED", src.name, len(me.polygons))
elif os.environ.get("RIG") == "1":
    # Humanoid skeleton (Mixamo bone names, T-pose) for Unity's Humanoid avatar; Mixamo animations retarget onto it.
    # Unlike the static props this stays facing -Y: the armature export (no baked space transform) lands facing Unity +Z.
    body = b.finish("Character_Human" + V)
    # finish() recentres the mesh; the skeleton below is authored on that same bounding frame (x/y centred, feet at z=0)
    arm_data = bpy.data.armatures.new("Armature"); rig = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    P = "mixamorig:"
    def bone(name, head, tail, parent=None):
        e = arm_data.edit_bones.new(P + name); e.head = head; e.tail = tail
        if parent: e.parent = arm_data.edit_bones[P + parent]
        return e
    bone("Hips", (0, 0, .9), (0, 0, 1.0)); bone("Spine", (0, 0, 1.0), (0, 0, 1.12), "Hips")
    bone("Spine1", (0, 0, 1.12), (0, 0, 1.24), "Spine"); bone("Spine2", (0, 0, 1.24), (0, 0, 1.36), "Spine1")
    bone("Neck", (0, 0, 1.36), (0, 0, 1.44), "Spine2"); bone("Head", (0, 0, 1.44), (0, 0, 1.78), "Neck")
    for side, sx in (("Left", -1), ("Right", 1)):      # facing -Y, the character's left is +X
        z = 1.35
        bone(side + "Shoulder", (sx * .05, 0, z), (sx * .20, 0, z), "Spine2")
        bone(side + "Arm", (sx * .20, 0, z), (sx * .50, 0, z), side + "Shoulder")
        bone(side + "ForeArm", (sx * .50, 0, z), (sx * .76, 0, z), side + "Arm")
        bone(side + "Hand", (sx * .76, 0, z), (sx * .92, 0, z), side + "ForeArm")
        x = sx * .105
        bone(side + "UpLeg", (x, 0, .88), (x, 0, .48), "Hips")
        bone(side + "Leg", (x, 0, .48), (x, 0, .10), side + "UpLeg")
        bone(side + "Foot", (x, 0, .10), (x, -.13, .04), side + "Leg")
        bone(side + "ToeBase", (x, -.13, .04), (x, -.24, .04), side + "Foot")
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in bpy.context.scene.objects: o.select_set(False)
    # Rigid-by-region weights: the body is made of overlapping primitives, so heat-diffusion weighting mixes parts
    # (floating heads, eyes on the chest). Each vertex blends between the two nearest bones along its limb axis.
    def blend(v, stops):
        if v <= stops[0][0]: return {stops[0][1]: 1.0}
        for (a, na), (c, nc) in zip(stops, stops[1:]):
            if v <= c:
                t = (v - a) / (c - a)
                return {na: 1 - t, nc: t}
        return {stops[-1][1]: 1.0}
    column = [(.90, "Hips"), (1.06, "Spine"), (1.18, "Spine1"), (1.30, "Spine2"), (1.38, "Neck"), (1.46, "Head")]
    ymin = min(v.co.y for v in body.data.vertices)
    def weights_for(co):
        side = "Left" if co.x < 0 else "Right"
        ax = abs(co.x)
        if ax > .19 and co.z > 1.2:
            w = blend(ax, [(.19, "Spine2"), (.24, "Shoulder"), (.38, "Arm"), (.52, "Arm"), (.60, "ForeArm"), (.74, "ForeArm"), (.80, "Hand")])
            return {(n if n == "Spine2" else side + n): v for n, v in w.items()}
        if co.z < .92:
            if ax < .03 and co.z > .8: return {"Hips": 1.0}
            if co.z < .13 and co.y < ymin + .12:
                w = blend(ymin + .12 - co.y, [(0.0, "Foot"), (.08, "ToeBase")])
            else:
                w = blend(co.z, [(.06, "Foot"), (.12, "Leg"), (.42, "Leg"), (.54, "UpLeg"), (.80, "UpLeg"), (.92, "Hips")])
            return {(n if n == "Hips" else side + n): v for n, v in w.items()}
        return blend(co.z, column)
    for v in body.data.vertices:
        for bone_name, weight in weights_for(v.co).items():
            if weight < 1e-3: continue
            full = P + bone_name
            group = body.vertex_groups.get(full) or body.vertex_groups.new(name=full)
            group.add([v.index], weight, "REPLACE")
    body.parent = rig
    modifier = body.modifiers.new("Armature", "ARMATURE"); modifier.object = rig
    groups = len(body.vertex_groups)
    d = outdir(); os.makedirs(d, exist_ok=True)
    for o in bpy.context.scene.objects: o.select_set(o in (body, rig))
    bpy.ops.export_scene.fbx(filepath=os.path.join(d, "Character_Human" + V + "_Rigged.fbx"), use_selection=True, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=False, axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="EDGE", add_leaf_bones=False, object_types={"ARMATURE", "MESH"})
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(d, "Character_Human" + V + "_Rigged.blend"))
    print("DELIVERED Character_Human%s_Rigged groups=%d bounds_z=%s" % (V, groups, [round(v.co.z, 3) for v in body.data.vertices][:1]))
else:
    deliver(b, "Character_Human" + V, flip=True)

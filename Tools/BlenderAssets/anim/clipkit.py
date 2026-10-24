"""Shared helpers for authoring humanoid clips on a Mixamo skeleton (headless Blender).

Armature space (cm): +z forward, +y up, the character's right side is -x. Bones keep Mixamo's armature-aligned rest frames and
point 'up' in rest, so joints are read from bone heads; aim()/ik_* solve rotations from desired joint directions.
"""
import bpy, math, sys
from mathutils import Vector, Matrix, Euler, Quaternion

P = 'mixamorig:'
BASE_DIR = r'D:/blender/repo-art/Assets/ShipMayham/Art/Generated/Animations/'


class Kit:
    def __init__(self, base_fbx, base_frame=1):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=base_fbx)
        self.arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
        self.sc = bpy.context.scene
        self.sc.frame_set(base_frame); bpy.context.view_layer.update()
        self.PB = self.arm.pose.bones
        self.base = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy()) for pb in self.PB}
        self.arm.animation_data_clear()
        for a in list(bpy.data.actions): bpy.data.actions.remove(a)
        for pb in self.PB: pb.rotation_mode = 'QUATERNION'
        self.keyed = []

    # --- low level
    def upd(self): bpy.context.view_layer.update()
    def rest_rot(self, b): return self.arm.data.bones[b].matrix_local.to_3x3()
    def rest_head(self, b): return self.arm.data.bones[b].head_local.copy()
    def head(self, b): return self.PB[P + b].head.copy()
    def reset(self):
        for b, (loc, rot) in self.base.items():
            self.PB[b].location = loc; self.PB[b].rotation_quaternion = rot.copy()
        self.upd()

    def rot(self, bone, deg):
        """Premultiply an armature-axes euler (degrees) onto the bone's current pose rotation."""
        q = Euler([math.radians(a) for a in deg], 'XYZ').to_quaternion()
        self.PB[P + bone].rotation_quaternion = q @ self.PB[P + bone].rotation_quaternion
        self.upd()

    def move(self, bone, delta_cm):
        self.PB[P + bone].location = self.PB[P + bone].location + Vector(delta_cm); self.upd()

    def global_rot(self, bone): return self.PB[P + bone].matrix.to_3x3().copy()

    def aim(self, bone, child, d_des):
        """Pose `bone` so that head(bone) -> head(child) points along d_des (armature space)."""
        b, c = P + bone, P + child
        d_rest = (self.rest_head(c) - self.rest_head(b)).normalized()
        m_rot = d_rest.rotation_difference(Vector(d_des).normalized()).to_matrix() @ self.rest_rot(b)
        parent = self.PB[b].parent
        p_rot = parent.matrix.to_3x3() if parent else Matrix.Identity(3)
        p_rest = self.rest_rot(parent.name) if parent else Matrix.Identity(3)
        rel = p_rest.inverted() @ self.rest_rot(b)
        self.PB[b].rotation_quaternion = (rel.inverted() @ p_rot.inverted() @ m_rot).to_quaternion()
        self.upd()

    def limb(self, a, b, c, target, pole, reach=0.995):
        """Two-bone IK: root bone a, mid bone b, end bone c (all head positions); end reaches `target` (armature space), elbow/knee bends toward `pole`."""
        L1 = (self.rest_head(P + b) - self.rest_head(P + a)).length
        L2 = (self.rest_head(P + c) - self.rest_head(P + b)).length
        S = self.head(a); d = Vector(target) - S
        dist = max(min(d.length, (L1 + L2) * reach), abs(L1 - L2) + 0.5); dn = d.normalized()
        x = (L1 ** 2 - L2 ** 2 + dist ** 2) / (2 * dist); h = math.sqrt(max(L1 ** 2 - x ** 2, 0))
        pv = Vector(pole); pv = (pv - dn * pv.dot(dn)); pv = pv.normalized() if pv.length > 1e-6 else Vector((1, 0, 0))
        E = S + dn * x + pv * h
        self.aim(a, b, E - S); self.aim(b, c, Vector(target) - E)
        return E

    def arm_ik(self, side, target, pole=None):
        s = 'Right' if side == 'R' else 'Left'
        pole = pole or ((-1, -0.7, -0.1) if side == 'R' else (1, -0.7, -0.1))
        return self.limb(s + 'Arm', s + 'ForeArm', s + 'Hand', target, pole)

    def leg_ik(self, side, target, pole=(0, 0, 1)):
        s = 'Right' if side == 'R' else 'Left'
        return self.limb(s + 'UpLeg', s + 'Leg', s + 'Foot', target, pole)

    # --- keys / export
    def key(self, frame):
        for pb in self.PB:
            pb.keyframe_insert('rotation_quaternion', frame=frame); pb.keyframe_insert('location', frame=frame)
        self.keyed.append(frame)

    def export(self, out, fstart=None, fend=None):
        fstart = fstart or min(self.keyed); fend = fend or max(self.keyed)
        self.sc.frame_start, self.sc.frame_end = fstart, fend
        bpy.data.actions[0].name = 'Armature|mixamo.com|Layer0'
        bpy.ops.object.select_all(action='DESELECT'); self.arm.select_set(True); bpy.context.view_layer.objects.active = self.arm
        bpy.ops.export_scene.fbx(filepath=out, use_selection=True, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_simplify_factor=0.0,
                                 add_leaf_bones=False, object_types={'ARMATURE'}, bake_anim_use_nla_strips=False)
        print('EXPORTED', out, fstart, fend)

    def cycle(self, frames, pose_fn, step=2):
        """Key pose_fn(phase in [0,1)) every `step` frames over `frames` frames; last key repeats the first (seamless loop)."""
        f = 1
        while f <= frames:
            self.reset(); pose_fn((f - 1) / frames); self.key(f); f += step
        self.reset(); pose_fn(0.0); self.key(frames + 1)

    def report(self, label, joints):
        print('REPORT', label, {j: [round(x, 1) for x in self.head(j)] for j in joints})


def vecs(*a): return Vector(a)

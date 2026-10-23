import bpy
import bmesh
import math
import random
from mathutils import Vector, Matrix

def create_material(name, color, roughness=0.8):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (*color, 1.0)
        bsdf.inputs['Roughness'].default_value = roughness
    return mat

def main():
    # Clear scene
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()

    # Materials (Linear colors)
    mat_blue_grey = create_material("Mat_BlueGrey", (0.12, 0.15, 0.2), 0.85)
    mat_warm_grey = create_material("Mat_WarmGrey", (0.18, 0.17, 0.17), 0.85)
    mat_dark_grey = create_material("Mat_DarkGrey", (0.08, 0.1, 0.14), 0.85)
    mat_pale_cap = create_material("Mat_PaleCap", (0.4, 0.42, 0.45), 0.7)
    mat_grass = create_material("Mat_Grass", (0.05, 0.35, 0.04), 0.9)
    mat_pebble = create_material("Mat_Pebble", (0.2, 0.2, 0.2), 0.8)

    # Create the single mesh object
    mesh = bpy.data.meshes.new("RockCluster_Mesh")
    obj = bpy.data.objects.new("RockCluster", mesh)
    bpy.context.collection.objects.link(obj)
    
    # Add materials to the object
    obj.data.materials.append(mat_blue_grey) # 0
    obj.data.materials.append(mat_warm_grey) # 1
    obj.data.materials.append(mat_dark_grey) # 2
    obj.data.materials.append(mat_pale_cap)  # 3
    obj.data.materials.append(mat_grass)     # 4
    obj.data.materials.append(mat_pebble)    # 5

    bm = bmesh.new()

    def add_rock(center, scale, mat_idx, jitter=0.05):
        temp_bm = bmesh.new()
        bmesh.ops.create_uvsphere(temp_bm, u_segments=16, v_segments=10, radius=1.0)
        
        for v in temp_bm.verts:
            v.co.x *= scale[0]
            v.co.y *= scale[1]
            v.co.z *= scale[2]
            v.co += Vector(((v.co.x*10)%0.1 - 0.05, (v.co.y*10)%0.1 - 0.05, (v.co.z*10)%0.1 - 0.05))
        
        bmesh.ops.bevel(temp_bm, geom=temp_bm.faces, offset=0.05, segments=1, affect='VERTICES')
        bmesh.ops.translate(temp_bm, verts=temp_bm.verts, vec=center)
        
        for face in temp_bm.faces:
            new_verts = []
            for v in face.verts:
                new_verts.append(bm.verts.new(v.co.copy()))
            try:
                new_face = bm.faces.new(new_verts)
                new_face.material_index = mat_idx
            except:
                pass
        temp_bm.free()

    def add_grass_blade(center, mat_idx):
        temp_bm = bmesh.new()
        # Create a wider, taller blade
        bmesh.ops.create_cube(temp_bm, size=1.0)
        for v in temp_bm.verts:
            v.co.x *= 0.08
            v.co.y *= 0.02
            v.co.z *= 0.5
            v.co.z += 0.25
        
        # Tilt
        rot_mat = Matrix.Rotation(random.uniform(-0.5, 0.5), 4, 'X') @ Matrix.Rotation(random.uniform(-0.5, 0.5), 4, 'Y')
        for v in temp_bm.verts:
            v.co = rot_mat @ v.co

        bmesh.ops.translate(temp_bm, verts=temp_bm.verts, vec=center)
        
        for face in temp_bm.faces:
            new_verts = []
            for v in face.verts:
                new_verts.append(bm.verts.new(v.co.copy()))
            try:
                new_face = bm.faces.new(new_verts)
                new_face.material_index = mat_idx
            except:
                pass
        temp_bm.free()

    def add_pebble(center, scale, mat_idx):
        temp_bm = bmesh.new()
        bmesh.ops.create_uvsphere(temp_bm, u_segments=8, v_segments=6, radius=0.25)
        for v in temp_bm.verts:
            v.co.x *= scale[0]
            v.co.y *= scale[1]
            v.co.z *= scale[2]
        bmesh.ops.translate(temp_bm, verts=temp_bm.verts, vec=center)
        for face in temp_bm.faces:
            new_verts = []
            for v in face.verts:
                new_verts.append(bm.verts.new(v.co.copy()))
            try:
                new_face = bm.faces.new(new_verts)
                new_face.material_index = mat_idx
            except:
                pass
        temp_bm.free()

    # 1. The 3 Main Rocks
    add_rock((0, 0, 0.8), (1.4, 1.2, 1.0), 0)
    add_rock((1.3, 0.5, 0.4), (0.8, 0.7, 0.6), 1)
    add_rock((-1.2, -0.4, 0.3), (0.9, 0.8, 0.5), 2)

    # 2. Pale Cap (Better integration)
    temp_cap = bmesh.new()
    bmesh.ops.create_uvsphere(temp_cap, u_segments=12, v_segments=8, radius=0.75)
    for v in temp_cap.verts:
        v.co.z *= 0.4
        v.co.z += 1.4
        v.co.x *= 1.1
        v.co.y *= 1.1
    bmesh.ops.translate(temp_cap, verts=temp_cap.verts, vec=(0,0,0))
    for face in temp_cap.faces:
        new_verts = []
        for v in face.verts:
            new_verts.append(bm.verts.new(v.co.copy()))
        try:
            new_face = bm.faces.new(new_verts)
            new_face.material_index = 3
        except:
            pass
    temp_cap.free()

    # 3. Grass Tuft (Clearly visible blades)
    for i in range(10):
        angle = i * (math.pi * 2 / 10)
        dist = 0.15
        gx = math.cos(angle) * dist
        gy = math.sin(angle) * dist
        add_grass_blade((gx, gy, 1.75), 4)

    # 4. Pebbles (Larger, more distinct)
    add_pebble((0.8, 1.5, 0.05), (1.2, 1.2, 0.5), 5)
    add_pebble((-1.8, 0.5, 0.05), (1.0, 0.8, 0.4), 5)

    bm.to_mesh(mesh)
    bm.free()

    mesh.update()
    verts = mesh.vertices
    if verts:
        min_z = min(v.co.z for v in verts)
        min_x = min(v.co.x for v in verts)
        max_x = max(v.co.x for v in verts)
        min_y = min(v.co.y for v in verts)
        max_y = max(v.co.y for v in verts)
        center_x = (min_x + max_x) / 2
        center_y = (min_y + max_y) / 2
        for v in verts:
            v.co.z -= min_z
            v.co.x -= center_x
            v.co.y -= center_y
        mesh.update()
        obj.location = (0, 0, 0)

main()
import bpy, math, sys
from mathutils import Vector, Matrix
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'D:/blender/anim/Mixamo_RepairHammering.fbx')
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
print('ACTIONS',[(a.name,tuple(a.frame_range)) for a in bpy.data.actions])
bpy.ops.import_scene.fbx(filepath=r'D:/blender/repo-art/Assets/ShipMayham/Art/Generated/Models/RepairHammer.fbx')
ham=[o for o in bpy.data.objects if o.type=='MESH'][0]
bpy.ops.object.select_all(action='DESELECT'); ham.select_set(True); bpy.context.view_layer.objects.active=ham
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
print('HAMDIM',[round(x,2) for x in ham.dimensions],'zmin',round(min(v.co.z for v in ham.data.vertices),2),'zmax',round(max(v.co.z for v in ham.data.vertices),2))
sc=bpy.context.scene
sc.render.engine='BLENDER_EEVEE'; sc.render.resolution_x=sc.render.resolution_y=360
w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(0.78,0.84,0.9,1)
l=bpy.data.objects.new('l',bpy.data.lights.new('l','SUN')); l.data.energy=3; l.rotation_euler=(0.8,0.2,-0.6); sc.collection.objects.link(l)
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); cam.data.type='ORTHO'; cam.data.ortho_scale=2.4; sc.collection.objects.link(cam); sc.camera=cam
cam.location=(-6,0,0.95); cam.rotation_euler=(math.radians(90),0,math.radians(-90))
def mat(n,c):
    m=bpy.data.materials.new(n); m.use_nodes=True; m.node_tree.nodes['Principled BSDF'].inputs[0].default_value=c; return m
skin=mat('skin',(0.8,0.5,0.35,1)); left=mat('left',(0.2,0.4,0.8,1))
import bmesh
def seg(a,b,r,m):
    d=b-a; L=d.length
    if L<1e-6: return
    bpy.ops.mesh.primitive_cylinder_add(radius=r,depth=L,location=(a+b)/2); o=bpy.context.object
    o.rotation_mode='QUATERNION'; o.rotation_quaternion=Vector((0,0,1)).rotation_difference(d); o.data.materials.append(m); return o
CH=[('Hips','Spine'),('Spine','Spine1'),('Spine1','Spine2'),('Spine2','Neck'),('Neck','Head'),('RightArm','RightForeArm'),('RightForeArm','RightHand'),('LeftArm','LeftForeArm'),('LeftForeArm','LeftHand'),('RightUpLeg','RightLeg'),('RightLeg','RightFoot'),('LeftUpLeg','LeftLeg'),('LeftLeg','LeftFoot'),('Spine2','RightArm'),('Spine2','LeftArm')]
frames=[1,5,9,11,13,24]
for i,f in enumerate(frames):
    sc.frame_set(f); bpy.context.view_layer.update()
    for o in [o for o in bpy.data.objects if o.name.startswith('_s')]: bpy.data.objects.remove(o)
    pos=lambda n: arm.matrix_world@arm.pose.bones['mixamorig:'+n].head
    objs=[]
    for a,b in CH:
        o=seg(pos(a),pos(b),0.035,left if a.startswith('Left') or b.startswith('Left') else skin)
        if o: o.name='_s'+a+b; objs.append(o)
    hp=pos('RightHand'); fo=pos('RightHand')-pos('RightForeArm'); fo.normalize()
    ham.parent=None; ham.location=hp; ham.rotation_mode='QUATERNION'; ham.rotation_quaternion=Vector((0,0,1)).rotation_difference(fo)
    sc.render.filepath=f'D:/blender/anim/prev_{i}.png'; bpy.ops.render.render(write_still=True)
    print('RENDERED',f, [round(x,2) for x in hp])

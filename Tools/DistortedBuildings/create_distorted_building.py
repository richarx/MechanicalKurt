"""Run with Blender 3.1+: blender --background --python <this file>."""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/DistortedBuildings'
ASSETS = ROOT / 'Assets/Decor/DistortedBuildings'
SOURCE.mkdir(parents=True, exist_ok=True)
ASSETS.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/Decor/URBAN/Buildings Prebuild/flat_Lshape_vertical_stripes.fbx'))
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
body.name = 'Architecture_Lshape'
body.location = (0, 0, 0)
body.rotation_euler = (0, 0, 0)
original = body.copy()
original.data = body.data.copy()
original.name = 'Original_reference_hidden'
bpy.context.collection.objects.link(original)
original.hide_render = True
original.hide_set(True)

atlas = body.data.materials[0]
atlas.name = 'Houses'
atlas.use_nodes = True
shader = atlas.node_tree.nodes.get('Principled BSDF')
texture = atlas.node_tree.nodes.new('ShaderNodeTexImage')
texture.image = bpy.data.images.load(str(ROOT / 'Assets/Decor/URBAN/textures/houses.png'))
texture.interpolation = 'Linear'
atlas.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
shader.inputs['Roughness'].default_value = .9

def material(name, color, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    p = mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = .82
    p.inputs['Emission'].default_value = (*color, 1)
    p.inputs['Emission Strength'].default_value = emission
    return mat

dark = material('Distorted_Interior', (.018, .013, .012))
frame = material('Distorted_WindowFrames', (.075, .048, .029))
glass = material('Distorted_WarmGlass', (.65, .43, .18), .3)
stone = material('Distorted_OldStone', (.42, .36, .25))

# Replace only the outer front wall. The L footprint, side UVs and roof survive.
bm = bmesh.new()
bm.from_mesh(body.data)
front = [f for f in bm.faces if all(abs(v.co.y + 18) < .015 for v in f.verts)]
bmesh.ops.delete(bm, geom=front, context='FACES')
bm.to_mesh(body.data)
bm.free()

def gaussian(x, z, cx, cz, sx, sz):
    return math.exp(-.5 * (((x-cx)/sx)**2 + ((z-cz)/sz)**2))

def depth(x, z):
    # Broad masonry masses, then smaller eyelids, nose, lips and chin.
    d = .48 * gaussian(x,z,10.5,15,8.5,10)
    for cx in (5.25, 15.75):
        d += 1.8 * gaussian(x,z,cx,22.7,3,1.05)
        d -= 1.3 * gaussian(x,z,cx,20.6,2.0,1.55)
        d += 1.5 * gaussian(x,z,cx,13.3,3.5,2.4)
    d += 2.0 * gaussian(x,z,10.5,18.1,1.35,3.3)
    d += 3.1 * gaussian(x,z,10.5,14.8,1.9,1.3)
    d -= .6 * gaussian(x,z,8.95,13.65,.55,.4)
    d -= .6 * gaussian(x,z,12.05,13.65,.55,.4)
    mouth_z = 8.4 + .045 * (x-10.5)**2
    d += 1.65 * gaussian(x,z,10.5,mouth_z+1.1,4.3,.65)
    d += 1.6 * gaussian(x,z,10.5,mouth_z-1.15,4.1,.7)
    d += 1.1 * gaussian(x,z,10.5,5.2,4.6,1.5)
    edge = min(1, x/2, (21-x)/2, z/2, (29-z)/2)
    edge = max(0, edge)
    return d * edge * edge * (3-2*edge)

def hole(x, z):
    for cx in (5.25,15.75):
        if abs(x-cx) < 1.4 and abs(z-20.6) < 1.6:
            return 'eye'
    if ((x-10.5)/4.55)**2 + ((z-(8.4+.045*(x-10.5)**2))/1.05)**2 < 1:
        return 'mouth'
    return None

NX, NZ = 168, 232
DX, DZ = 21/NX, 29/NZ
verts = [(i*DX,-18,j*DZ) for j in range(NZ+1) for i in range(NX+1)]
faces, cells = [], []
for j in range(NZ):
    for i in range(NX):
        if hole((i+.5)*DX, (j+.5)*DZ):
            continue
        a = j*(NX+1)+i
        faces.append((a,a+1,a+NX+2,a+NX+1))
        cells.append((i,j))
# Project the mouth boundary to a continuous ellipse instead of a stair-step cut.
boundary_counts = {}
for f in faces:
    for a,b in zip(f, f[1:]+f[:1]):
        key = tuple(sorted((a,b)))
        boundary_counts[key] = boundary_counts.get(key,0)+1
mouth_boundary = set()
for (a,b),count in boundary_counts.items():
    if count == 1:
        for index in (a,b):
            x,y,z = verts[index]
            if 5 < z < 12 and 4 < x < 17:
                mouth_boundary.add(index)
for index in mouth_boundary:
    x,y,z = verts[index]
    u=(x-10.5)/4.55
    v=(z-8.4-.045*(x-10.5)**2)/1.05
    radius=math.sqrt(u*u+v*v)
    if radius:
        x=10.5+4.55*u/radius
        z=8.4+.045*(x-10.5)**2+1.05*v/radius
        verts[index]=(x,y,z)
mesh = bpy.data.meshes.new('Sculpted_masonry_mesh')
mesh.from_pydata(verts, [], faces)
mesh.materials.append(atlas)
mesh.materials.append(dark)
facade = bpy.data.objects.new('Facade_sculpted_face', mesh)
bpy.context.collection.objects.link(facade)
uv = mesh.uv_layers.new(name='UVMap')
for poly,(i,j) in zip(mesh.polygons,cells):
    # Repeat a brick-only rectangle of the EXISTING atlas, no texture edits.
    u0, v0 = (i % 24)/24, (j % 24)/24
    for loop, (du,dv) in zip(poly.loop_indices, ((0,0),(1/24,0),(1/24,1/24),(0,1/24))):
        uv.data[loop].uv = ((403+(u0+du)*123)/1024, 1-(125-(v0+dv)*123)/1024)
    poly.use_smooth = True
facade.shape_key_add(name='Basis')
expression = facade.shape_key_add(name='Visage')
for v in expression.data:
    v.co.y -= depth(v.co.x, v.co.z)
expression.value = 1

# Reveals close each opening in depth, with a dark recessed back wall.
edge_counts = {}
for f in faces:
    for a,b in zip(f, f[1:]+f[:1]):
        key = tuple(sorted((a,b)))
        edge_counts[key] = edge_counts.get(key,0)+1
rv, rf = [], []
for (a,b),count in edge_counts.items():
    if count != 1:
        continue
    va, vb = Vector(verts[a]), Vector(verts[b])
    if min(va.x,vb.x) < .01 or max(va.x,vb.x) > 20.99 or min(va.z,vb.z) < .01 or max(va.z,vb.z) > 28.99:
        continue
    va.y -= depth(va.x,va.z)
    vb.y -= depth(vb.x,vb.z)
    k=len(rv)
    rv += [tuple(va),tuple(vb),(vb.x,-14.8,vb.z),(va.x,-14.8,va.z)]
    rf.append((k,k+1,k+2,k+3))
reveal_mesh=bpy.data.meshes.new('Recesses_mesh')
reveal_mesh.from_pydata(rv,[],rf)
reveal_mesh.materials.append(stone)
bm=bmesh.new()
bm.from_mesh(reveal_mesh)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.001)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(reveal_mesh)
bm.free()
for p in reveal_mesh.polygons:
    p.use_smooth=True
reveals=bpy.data.objects.new('Window_and_mouth_reveals',reveal_mesh)
bpy.context.collection.objects.link(reveals)

def box(name, location, scale, mat, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    ob=bpy.context.object
    ob.name=name
    ob.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    ob.data.materials.append(mat)
    if bevel:
        ob.data.use_auto_smooth=True
        mod=ob.modifiers.new('Soft_edges','BEVEL')
        mod.width=bevel
        mod.segments=3
        ob.modifiers.new('Weighted_normals','WEIGHTED_NORMAL')
    return ob

details=[]
for side,cx in [('Left',5.25),('Right',15.75)]:
    details.append(box(side+'_window_frame',(cx,-15.35,20.6),(3.1,.3,3.5),frame,.1))
    details.append(box(side+'_window_glass',(cx,-15.53,20.6),(2.58,.12,2.95),glass,.03))
    details.append(box(side+'_window_transom',(cx,-15.65,20.45),(2.7,.14,.16),frame,.02))
details.append(box('Mouth_dark_interior',(10.5,-14.6,8.8),(10,.22,4),dark))
for i,cx in enumerate((8.7,10.5,12.3)):
    details.append(box('Stone_tooth_'+str(i+1),(cx,-18.65,8.95),(1.55,.6,.85),stone,.12))

# A real editable lattice bends the complete architecture, anchored at ground.
lat_data=bpy.data.lattices.new('Architecture_cage')
lat_data.points_u=3
lat_data.points_v=3
lat_data.points_w=6
lat_data.interpolation_type_u='KEY_BSPLINE'
lat_data.interpolation_type_v='KEY_BSPLINE'
lat_data.interpolation_type_w='KEY_BSPLINE'
lat=bpy.data.objects.new('Lattice_editable_silhouette',lat_data)
bpy.context.collection.objects.link(lat)
lat.location=(10.5,-4.5,14.5)
lat.scale=(24,33,31)
lat.show_in_front=True
for p in lat_data.points:
    t=max(0,min(1,(p.co_deform.z+.5)))
    p.co_deform.x += (1.4*t*t+.3*math.sin(t*math.pi))/24
    p.co_deform.y += (.7*t*t)/33
    p.co_deform.x += p.co_deform.x*.045*math.sin(t*math.pi)
model=[body,facade,reveals]+details
for ob in model:
    mod=ob.modifiers.new('Architecture_lattice','LATTICE')
    mod.object=lat

# Export evaluated static meshes. Shape keys and cage remain in the .blend.
exports=[]
depsgraph=bpy.context.evaluated_depsgraph_get()
for ob in model:
    evaluated=ob.evaluated_get(depsgraph)
    baked=bpy.data.meshes.new_from_object(evaluated, depsgraph=depsgraph)
    export=bpy.data.objects.new(ob.name,baked)
    bpy.context.collection.objects.link(export)
    export.matrix_world=ob.matrix_world.copy()
    exports.append(export)
# A separate decimated collision mesh follows the face and its blind recesses.
bpy.ops.object.select_all(action='DESELECT')
collision_parts=[]
for ob in exports:
    part=ob.copy()
    part.data=ob.data.copy()
    bpy.context.collection.objects.link(part)
    part.select_set(True)
    collision_parts.append(part)
bpy.context.view_layer.objects.active=collision_parts[0]
bpy.ops.object.join()
collision=bpy.context.object
collision.name='Collision_LowPoly'
collision.data.name='Collision_LowPoly_mesh'
decimate=collision.modifiers.new('Collision_simplification','DECIMATE')
decimate.ratio=.065
bpy.ops.object.modifier_apply(modifier=decimate.name)
collision.hide_render=True
exports.append(collision)
bpy.ops.object.select_all(action='DESELECT')
for ob in exports:
    ob.select_set(True)
bpy.context.view_layer.objects.active=exports[0]
fbx=ASSETS / 'flat_Lshape_vertical_stripes_distorted.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
counts={'vertices':sum(len(o.data.vertices) for o in exports if o != collision),'triangles':sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in exports if o != collision),'collision_triangles':sum(len(f.vertices)-2 for f in collision.data.polygons)}
for ob in exports:
    bpy.data.objects.remove(ob,do_unlink=True)

scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.render.resolution_x=1200
scene.render.resolution_y=1200
scene.render.resolution_percentage=100
scene.world.color=(.17,.19,.23)
scene.view_settings.view_transform='Filmic'
scene.view_settings.look='Medium High Contrast'
target=Vector((10.5,-7,14))
for name,loc,energy,size,color in [('Key',(-12,-42,46),24000,22,(1,.8,.6)),('Fill',(35,-15,26),11000,18,(.64,.76,1)),('Rim',(6,25,44),23000,18,(1,.9,.72))]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    lamp=bpy.context.object
    lamp.name=name
    lamp.data.energy=energy
    lamp.data.size=size
    lamp.data.color=color
    lamp.rotation_euler=(target-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(40,-80,31))
camera=bpy.context.object
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'
camera.data.ortho_scale=42
scene.camera=camera
scene.render.filepath=str(SOURCE / 'distorted_preview.png')
texture.image.pack()
bpy.ops.object.select_all(action='DESELECT')
facade.select_set(True)
bpy.context.view_layer.objects.active=facade
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'flat_Lshape_vertical_stripes_distorted.blend'))
bpy.ops.render.render(write_still=True)
(SOURCE / 'geometry_report.json').write_text(json.dumps(counts,indent=2))
print('DISTORTED_EXPORT',str(fbx),counts)

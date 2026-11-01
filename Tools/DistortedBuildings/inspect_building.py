import bpy
import json
from collections import Counter
from mathutils import Vector
from pathlib import Path

ROOT = Path('E:/WIP/LD59Kurt')
OUT = ROOT / 'ArtSource/DistortedBuildings'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/Decor/URBAN/Buildings Prebuild/flat_Lshape_vertical_stripes.fbx'))
obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
obj.location = (0, 0, 0)
mesh = obj.data
planes = Counter()
for face in mesh.polygons:
    n = tuple(round(a, 2) for a in face.normal)
    c = tuple(round(a, 1) for a in face.center)
    if abs(face.normal.y) > .99:
        planes[('Y', c[1], n)] += face.area
    if abs(face.normal.x) > .99:
        planes[('X', c[0], n)] += face.area
print('PLANES', planes.most_common(18))
mat = mesh.materials[0]
mat.use_nodes = True
nodes = mat.node_tree.nodes
shader = nodes.get('Principled BSDF')
texture = nodes.new('ShaderNodeTexImage')
texture.image = bpy.data.images.load(str(ROOT / 'Assets/Decor/URBAN/textures/houses.png'))
texture.interpolation = 'Closest'
mat.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
shader.inputs['Roughness'].default_value = .87
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.world.color = (.3, .3, .3)
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'Medium High Contrast'
target = Vector((10, -4, 14))
for name, location, energy, size in [('Key', (5, -35, 52), 18000, 30), ('Fill', (40, 15, 35), 9000, 25)]:
    bpy.ops.object.light_add(type='AREA', location=location)
    lamp = bpy.context.object
    lamp.name = name
    lamp.data.energy = energy
    lamp.data.size = size
    lamp.rotation_euler = (target - lamp.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.object.camera_add(location=(49, -65, 38))
cam = bpy.context.object
cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 46
scene.camera = cam
scene.render.filepath = str(OUT / 'original.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'inspection.blend'))
bpy.ops.render.render(write_still=True)

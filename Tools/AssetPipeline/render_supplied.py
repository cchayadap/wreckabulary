"""Render a critique sheet of actual supplied FBX-derived web models, not concept art."""
import bpy
import json
import os
import math
from mathutils import Vector

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(REPO, 'Web/public/art')
bpy.ops.wm.read_factory_settings(use_empty=True)
wardrobe = json.load(open(os.path.join(ART, 'manifest.json')))['wardrobe']


def import_asset(relative, position, yaw=0):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(ART, relative))
    objects = [o for o in bpy.context.scene.objects if o not in before]
    wrapper = bpy.data.objects.new('Showcase_' + relative.replace('/','_'), None)
    bpy.context.scene.collection.objects.link(wrapper)
    for obj in objects:
        if obj.parent is None: obj.parent = wrapper
    wrapper.location = position
    wrapper.rotation_euler.z = yaw
    return objects


import_asset('items/SOFA.glb', (-.62, .55, 0), -.04)
import_asset('items/TABLE.glb', (-.64, -.60, 0), -.13)
import_asset('items/CHAIR.glb', (-1.65, -.48, 0), -.28)
import_asset('items/LAMP.glb', (-1.50, .96, 0))
import_asset('items/PLANT.glb', (1.15, .80, 0))
import_asset('items/BOMB.glb', (1.15, -.83, 0))
import_asset('items/BAT.glb', (1.55, -.49, 0), .15)
avatar = import_asset('avatar.glb', (.76, -.02, 0), -.12)
default_meshes = {'SK_Head'} | {p['mesh'] for p in wardrobe['pieces']
    if p['id'] in wardrobe['default']['pieces'].values()}
for obj in avatar:
    if obj.type == 'MESH': obj.hide_render = obj.name not in default_meshes
    if obj.type == 'ARMATURE':
        if obj.animation_data:
            for track in obj.animation_data.nla_tracks: track.mute = True
            obj.animation_data.action = next((a for a in bpy.data.actions if a.name == 'Idle'), None)
bpy.context.scene.frame_set(1)
for i, letter in enumerate('WRECK'):
    import_asset('letters/' + letter + '.glb', (-.63+i*.30, -1.55, 0), -.025+i*.02)

floor = bpy.data.materials.new('Showcase_WarmPaper')
floor.use_nodes = True
shader = floor.node_tree.nodes.get('Principled BSDF')
shader.inputs['Base Color'].default_value = (.67, .65, .58, 1)
shader.inputs['Roughness'].default_value = .96
bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.005))
bpy.context.object.data.materials.append(floor)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 96
scene.cycles.use_denoising = False
scene.cycles.max_bounces = 5
scene.render.threads_mode = 'FIXED'
scene.render.threads = 8
scene.render.resolution_x = 1440
scene.render.resolution_y = 960
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'AgX'
world = bpy.data.worlds.new('Supplied_StudioWorld')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (.78,.82,.85,1)
world.node_tree.nodes['Background'].inputs[1].default_value = .4
scene.world = world


def area(name, location, energy, size, color):
    light = bpy.data.lights.new(name, 'AREA')
    light.energy = energy
    light.shape = 'DISK'
    light.size = size
    light.color = color
    obj = bpy.data.objects.new(name, light)
    scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0,0,.5))-obj.location).to_track_quat('-Z','Y').to_euler()


area('Key',(-3,-4,5),500,4,(1,.88,.73))
area('Fill',(4,-1,4),300,3.5,(.76,.85,1))
area('Rim',(0,4,4),400,3,(1,.92,.78))
cam = bpy.data.cameras.new('Supplied_Camera')
obj = bpy.data.objects.new('Supplied_Camera', cam)
scene.collection.objects.link(obj)
obj.location = (3.35,-6.0,3.5)
obj.rotation_euler = (Vector((0,-.1,.53))-obj.location).to_track_quat('-Z','Y').to_euler()
cam.type='ORTHO'
cam.ortho_scale=4.65
scene.camera=obj
scene.render.image_settings.file_format='PNG'
scene.render.filepath=os.path.join(ART,'supplied-showcase.png')
bpy.ops.render.render(write_still=True)
print('SUPPLIED_SHOWCASE_COMPLETE ' + scene.render.filepath)

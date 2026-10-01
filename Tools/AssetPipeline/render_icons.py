"""Render transparent 3D inventory/HUD icons from actual supplied GLB meshes."""
import bpy
import json
import os
import argparse
import sys
from mathutils import Vector

REPO=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART=os.path.join(REPO,'Web/public/art')
manifest=json.load(open(os.path.join(ART,'manifest.json')))
argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
parser=argparse.ArgumentParser()
parser.add_argument('--only',default='')
args=parser.parse_args(argv)
selected={word.strip() for word in args.only.split(',') if word.strip()}
os.makedirs(os.path.join(ART,'icons'),exist_ok=True)
for word,item in manifest['items'].items():
    if selected and word not in selected:continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(REPO,'Web/public',item['path']))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    lo=Vector([min(p[k] for p in points) for k in range(3)])
    hi=Vector([max(p[k] for p in points) for k in range(3)])
    center=(lo+hi)/2
    world=bpy.data.worlds.new('Icon_Studio')
    world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.75,.80,.85,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.6
    scene=bpy.context.scene
    scene.world=world
    for name,offset,energy,size,color in [('Key',(-3,-4,4),420,3,(1,.86,.72)),
                                          ('Fill',(3,-1,3),230,3,(.72,.84,1)),
                                          ('Rim',(0,3,4),300,2.5,(1,.91,.75))]:
        light=bpy.data.lights.new(name,'AREA')
        light.energy=energy;light.size=size;light.color=color
        obj=bpy.data.objects.new(name,light);scene.collection.objects.link(obj)
        obj.location=center+Vector(offset)
        obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
    camera=bpy.data.cameras.new('Icon_Camera')
    obj=bpy.data.objects.new('Icon_Camera',camera);scene.collection.objects.link(obj)
    obj.location=center+Vector((2.3,-3.6,2.3))
    obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
    camera.type='ORTHO'
    # Fit actual camera-projected vertices, rather than a world-axis bounding
    # cube; low wide props such as STOOL otherwise clip their diagonal corners.
    inverse=obj.rotation_euler.to_quaternion().inverted()
    projected=[inverse@(point-center) for point in points]
    spans=[max(p[k] for p in projected)-min(p[k] for p in projected) for k in (0,1)]
    camera.ortho_scale=max(spans)*1.18
    scene.camera=obj;scene.render.engine='CYCLES'
    scene.cycles.device='CPU';scene.cycles.samples=96 if word=='CLOCK' else 24;scene.cycles.use_denoising=False
    scene.cycles.max_bounces=4;scene.render.threads_mode='FIXED';scene.render.threads=6
    scene.render.resolution_x=192;scene.render.resolution_y=192;scene.render.resolution_percentage=100
    scene.render.film_transparent=True;scene.view_settings.view_transform='AgX'
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    scene.render.filepath=os.path.join(ART,'icons',word+'.png')
    bpy.ops.render.render(write_still=True)
    item['icon']='art/icons/'+word+'.png'
    print('ITEM_ICON '+word,flush=True)
json.dump(manifest,open(os.path.join(ART,'manifest.json'),'w'),indent=2)
print('ITEM_ICONS_COMPLETE '+str(len(manifest['items'])))

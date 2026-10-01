"""Same-camera animated full/mobile comparisons for default and hoodie+hood outfits."""
import bpy
import hashlib
import json
import os
from mathutils import Vector

REPO=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART=os.path.join(REPO,'Web/public/art')
manifest=json.load(open(os.path.join(ART,'manifest.json')))
bpy.ops.wm.read_factory_settings(use_empty=True)
records=[]
for index,(file,hoodie) in enumerate([('avatar.glb',False),('avatar-mobile.glb',False),
                                     ('avatar.glb',True),('avatar-mobile.glb',True)]):
    before=set(bpy.context.scene.objects);before_actions=set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=os.path.join(ART,file))
    objects=[o for o in bpy.context.scene.objects if o not in before]
    actions=[a for a in bpy.data.actions if a not in before_actions]
    pieces=dict(manifest['wardrobe']['default']['pieces'])
    if hoodie:pieces.update(Top='Hoodie',Headwear='Hood')
    visible={'SK_Head'}|{p['mesh'] for p in manifest['wardrobe']['pieces'] if p['id'] in pieces.values()}
    wrapper=bpy.data.objects.new('Comparison_'+str(index),None);bpy.context.scene.collection.objects.link(wrapper)
    for obj in objects:
        if obj.parent is None:obj.parent=wrapper
        if obj.type=='MESH':
            # Importing a second model suffixes names; original primitive names
            # remain recognizable and must not activate every wardrobe module.
            name=obj.name.split('.')[0];obj.hide_render=name not in visible
        if obj.type=='ARMATURE':
            for track in obj.animation_data.nla_tracks:track.mute=True
            obj.animation_data.action=next(a for a in actions if a.name.startswith('Run_InPlace'))
    wrapper.location=((index-1.5)*1.05,0,0)
    records.append({'column':index+1,'model':file,'outfit':'hoodie+hood' if hoodie else 'default',
                    'clip':'Run_InPlace','frame':6,
                    'sha256':hashlib.sha256(open(os.path.join(ART,file),'rb').read()).hexdigest()})
scene=bpy.context.scene;scene.frame_set(6)
floor=bpy.data.materials.new('LOD_Backdrop');floor.use_nodes=True
floor.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.67,.65,.58,1)
floor.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.96
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.005));bpy.context.object.data.materials.append(floor)
world=bpy.data.worlds.new('LOD_Studio');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.78,.82,.85,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.4;scene.world=world
for name,location,energy,size,color in [('Key',(-3,-4,5),500,4,(1,.88,.73)),
                                       ('Fill',(4,-1,4),300,3.5,(.76,.85,1)),
                                       ('Rim',(0,4,4),400,3,(1,.92,.78))]:
    light=bpy.data.lights.new(name,'AREA');light.energy=energy;light.size=size;light.color=color
    obj=bpy.data.objects.new(name,light);scene.collection.objects.link(obj);obj.location=location
    obj.rotation_euler=(Vector((0,0,.5))-obj.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.cameras.new('LOD_Camera');obj=bpy.data.objects.new('LOD_Camera',cam);scene.collection.objects.link(obj)
obj.location=(2.5,-8,3.2);obj.rotation_euler=(Vector((0,0,.55))-obj.location).to_track_quat('-Z','Y').to_euler()
cam.type='ORTHO';cam.ortho_scale=4.95;scene.camera=obj
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=96;scene.cycles.use_denoising=False
scene.cycles.max_bounces=5;scene.render.threads_mode='FIXED';scene.render.threads=8
scene.render.resolution_x=1600;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
scene.render.filepath=os.path.join(ART,'avatar-mobile-comparison.png')
bpy.ops.render.render(write_still=True)
json.dump({'camera':'identical orthographic camera and lighting','comparison':records},
          open(os.path.join(ART,'avatar-mobile-comparison.json'),'w'),indent=2)
print('MOBILE_COMPARISON_RENDER_COMPLETE')

import bpy
import json
import os
import re
import argparse
import sys
from mathutils import Vector

REPO=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART=os.path.join(REPO,'Web/public/art')
manifest=json.load(open(os.path.join(ART,'manifest.json')))
PALETTE=json.load(open(os.path.join(REPO,'Assets/_Project/Resources/Art/item_palette.json'),encoding='utf-8'))
LIBRARY={m['name']:m for m in json.load(open(os.path.join(REPO,'Assets/_Project/Data/Generated/materials.json'),encoding='utf-8'))['materials']}
CLASSIC=re.compile(r'^(?P<family>.+)_Classic(\.\d{3})?$')
WORLD_COLOUR=(.9,.9,.9,1)
WORLD_STRENGTH=.35
LIGHTS=[('Key',(-3,-4,4),420,3,(1,.95,.88)),
        ('Fill',(3,-1,3),150,3,(.92,.95,1)),
        ('Rim',(0,3,4),300,2.5,(1,.95,.85))]
argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
parser=argparse.ArgumentParser()
parser.add_argument('--only',default='')
parser.add_argument('--exposure',type=float,default=0.0)
parser.add_argument('--out',default='')
args=parser.parse_args(argv)


def item_look(word):
    parts=PALETTE['items'].get(word,{})
    for material in bpy.data.materials:
        match=CLASSIC.match(material.name)
        if not match or not material.node_tree:continue
        target=parts.get(match.group('family'),{}).get('to')
        spec=LIBRARY.get((target or '')+'_Classic')
        if not spec:continue
        shader=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if not shader:continue
        base=shader.inputs['Base Color']
        for link in list(base.links):material.node_tree.links.remove(link)
        base.default_value=(*spec['baseColor'][:3],1)
        shader.inputs['Roughness'].default_value=spec['roughness']
        shader.inputs['Metallic'].default_value=spec['metallic']
        print('ITEM_LOOK '+word+' '+match.group('family')+' -> '+target,flush=True)

selected={word.strip() for word in args.only.split(',') if word.strip()}
OUT=os.path.abspath(args.out) if args.out else os.path.join(ART,'icons')
os.makedirs(OUT,exist_ok=True)
for word,item in manifest['items'].items():
    if selected and word not in selected:continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(REPO,'Web/public',item['path']))
    item_look(word)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    lo=Vector([min(p[k] for p in points) for k in range(3)])
    hi=Vector([max(p[k] for p in points) for k in range(3)])
    center=(lo+hi)/2
    world=bpy.data.worlds.new('Icon_Studio')
    world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=WORLD_COLOUR
    world.node_tree.nodes['Background'].inputs[1].default_value=WORLD_STRENGTH
    scene=bpy.context.scene
    scene.world=world
    for name,offset,energy,size,color in LIGHTS:
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
    inverse=obj.rotation_euler.to_quaternion().inverted()
    projected=[inverse@(point-center) for point in points]
    spans=[max(p[k] for p in projected)-min(p[k] for p in projected) for k in (0,1)]
    camera.ortho_scale=max(spans)*1.18
    scene.camera=obj;scene.render.engine='CYCLES'
    scene.cycles.device='CPU';scene.cycles.samples=96 if word=='CLOCK' else 24;scene.cycles.use_denoising=False
    scene.cycles.max_bounces=4;scene.render.threads_mode='FIXED';scene.render.threads=6
    scene.render.resolution_x=192;scene.render.resolution_y=192;scene.render.resolution_percentage=100
    scene.render.film_transparent=True;scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=args.exposure
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    scene.render.filepath=os.path.join(OUT,word+'.png')
    bpy.ops.render.render(write_still=True)
    item['icon']='art/icons/'+word+'.png'
    print('ITEM_ICON '+word,flush=True)
if not args.out:json.dump(manifest,open(os.path.join(ART,'manifest.json'),'w'),indent=2)
print('ITEM_ICONS_COMPLETE '+str(len(manifest['items'])))

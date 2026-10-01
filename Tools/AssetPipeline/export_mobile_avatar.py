"""Create a separate audited mobile avatar LOD; preserve supplied FBXs and facial morphs."""
import argparse
import bpy
import hashlib
import json
import math
import os
import sys

argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
parser=argparse.ArgumentParser()
parser.add_argument('--repo',default='.')
parser.add_argument('--ratio',type=float,default=.42)
args=parser.parse_args(argv)
REPO=os.path.abspath(args.repo)
OUT=os.path.join(REPO,'Web/public/art')
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from gltf_materials import restore,read_glb
from gltf_avatar_animation import preserve

manifest=json.load(open(os.path.join(OUT,'manifest.json')))
specs={m['name']:m for m in json.load(open(os.path.join(REPO,'Assets/_Project/Data/Generated/materials.json')))['materials']}
source=os.path.join(OUT,'avatar.glb')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
# Blender's glTF importer creates an 80-triangle Icosphere as a viewport bone
# display helper. It is not present in the supplied GLB or the playable avatar.
helpers=set()
for rig in (o for o in bpy.context.scene.objects if o.type=='ARMATURE'):
    for bone in rig.pose.bones:
        if bone.custom_shape:
            helpers.add(bone.custom_shape)
            bone.custom_shape=None
for helper in helpers:bpy.data.objects.remove(helper,do_unlink=True)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
if {o.name for o in meshes}!=set(manifest['avatar']['meshes']):
    raise RuntimeError('Mobile import contains unexpected/lost wardrobe meshes')
default_meshes={'SK_Head'}|{p['mesh'] for p in manifest['wardrobe']['pieces']
    if p['id'] in manifest['wardrobe']['default']['pieces'].values()}
facts=[]
for obj in meshes:
    obj.data.calc_loop_triangles()
    before=len(obj.data.loop_triangles)
    morphs=len(obj.data.shape_keys.key_blocks)-1 if obj.data.shape_keys else 0
    # Head expression targets are useful authored data. Retain all of them rather
    # than dropping blink/smile for an indiscriminate triangle target.
    if obj.name!='SK_Head' and not morphs:
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True);bpy.context.view_layer.objects.active=obj
        modifier=obj.modifiers.new('Mobile clothing LOD','DECIMATE')
        modifier.ratio=args.ratio
        modifier.use_collapse_triangulate=True
        while obj.modifiers.find(modifier.name)>0:
            bpy.ops.object.modifier_move_up(modifier=modifier.name)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        # Collapse changes corners. Recompute mesh normals rather than exporting
        # custom loop normals attached to the original denser topology.
        obj.data.update()
    obj.data.calc_loop_triangles()
    facts.append({'mesh':obj.name,'before':before,'after':len(obj.data.loop_triangles),
                  'morph_targets_retained':morphs,'default':obj.name in default_meshes})

actions=[a for a in bpy.data.actions if any('pose.bones[' in c.data_path for c in a.fcurves)]
for action in actions:
    imported=action.name.split('|')[-1].split('.')[0]
    action.name=next((name for name in manifest['avatar']['animations']
        if imported==name or imported.startswith(name+'_')), imported)
for rig in (o for o in bpy.context.scene.objects if o.type=='ARMATURE'):
    rig.animation_data_create()
    rig.animation_data.action=None
    for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
    for action in actions:
        track=rig.animation_data.nla_tracks.new();track.name=action.name
        strip=track.strips.new(action.name,int(action.frame_range[0]),action)
        strip.extrapolation='NOTHING';track.mute=True
bpy.context.scene.frame_set(1)
bpy.ops.object.select_all(action='SELECT')
destination=os.path.join(OUT,'avatar-mobile.glb')
bpy.ops.export_scene.gltf(filepath=destination,export_format='GLB',use_selection=True,
    export_yup=True,export_apply=False,export_animations=True,export_animation_mode='NLA_TRACKS',
    export_rest_position_armature=True,export_frame_range=False,export_force_sampling=True,
    export_cameras=False,export_lights=False,export_tangents=True)
material_checks=restore(destination,specs,REPO)
animation_checks=preserve(source,destination)
document,_=read_glb(destination)
clips=sorted(a['name'] for a in document.get('animations',[]))
if clips!=sorted(manifest['avatar']['animations']):raise RuntimeError('Mobile clip set changed')
bones={document['nodes'][i]['name'] for skin in document.get('skins',[]) for i in skin['joints']}
if bones!=set(manifest['avatar']['bones']):raise RuntimeError('Mobile bone set changed')
result={'generator':'Tools/AssetPipeline/export_mobile_avatar.py','ratio':args.ratio,
        'source_sha256':hashlib.sha256(open(source,'rb').read()).hexdigest(),
        'output_sha256':hashlib.sha256(open(destination,'rb').read()).hexdigest(),
        'before_triangles':sum(f['before'] for f in facts),'after_triangles':sum(f['after'] for f in facts),
        'default_before':sum(f['before'] for f in facts if f['default']),
        'default_after':sum(f['after'] for f in facts if f['default']),
        'meshes':facts,'clips':clips,'bones':sorted(bones),'material_validation':material_checks,
        'animation_preservation':animation_checks}
json.dump(result,open(os.path.join(OUT,'avatar-mobile-audit.json'),'w'),indent=2)
print('MOBILE_AVATAR_EXPORTED '+json.dumps({k:result[k] for k in ['before_triangles','after_triangles','default_before','default_after']}))
# The candidate is intentionally not promoted into manifest.json here. A separate
# independent structural verifier and comparison render must accept it first.

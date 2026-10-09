import argparse
import hashlib
import json
import math
import os
import sys
import time

import bpy
from mathutils import Matrix, Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gltf_materials import restore as restore_glb_materials

argv = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument('--repo', default='.')
parser.add_argument('--only', default='')
args = parser.parse_args(argv)
REPO = os.path.abspath(args.repo)
OUT = os.path.join(REPO, 'Web/public/art')
os.makedirs(OUT, exist_ok=True)
report = json.load(open(os.path.join(REPO, 'Assets/_Project/Data/Generated/build_report.json')))
material_specs = json.load(open(os.path.join(REPO, 'Assets/_Project/Data/Generated/materials.json')))['materials']
specs = {m['name']: m for m in material_specs}
wardrobe = json.load(open(os.path.join(REPO, 'Assets/_Project/Data/Config/wardrobe.json')))
audit = []
manifest = {'version': 1, 'coordinates': 'metres, Y-up, forward +Z',
            'items': {}, 'letters': {}, 'environment': {}, 'vfx': {},
            'wardrobe': wardrobe, 'materialSkins': {}, 'avatar': None}
only = {kind.strip() for kind in args.only.split(',') if kind.strip()}
if only and os.path.exists(os.path.join(OUT, 'manifest.json')):
    manifest = json.load(open(os.path.join(OUT, 'manifest.json')))
    manifest['wardrobe'] = wardrobe
    if os.path.exists(os.path.join(OUT, 'audit.json')):
        audit = [record for record in json.load(open(os.path.join(OUT, 'audit.json')))['files']
                 if record['kind'] not in only]
for s in material_specs:
    if s.get('skin'):
        manifest['materialSkins'].setdefault(s['family'], {})[s['skin']] = {
            k: s[k] for k in ('baseColor', 'metallic', 'roughness', 'emissive')}


def clean_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1


def restore_materials():
    unknown = []
    for mat in bpy.data.materials:
        name = mat.name
        s = specs.get(name)
        if not s:
            unknown.append(name)
            continue
        mat.use_nodes = True
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        nodes.clear()
        output = nodes.new('ShaderNodeOutputMaterial')
        shader = nodes.new('ShaderNodeBsdfPrincipled')
        links.new(shader.outputs['BSDF'], output.inputs['Surface'])
        shader.inputs['Base Color'].default_value = s['baseColor']
        shader.inputs['Metallic'].default_value = s['metallic']
        shader.inputs['Roughness'].default_value = s['roughness']
        shader.inputs['Emission Color'].default_value = (*s['emissive'], 1)
        shader.inputs['Emission Strength'].default_value = 1
        shader.inputs['Alpha'].default_value = s['baseColor'][3]
        mat.use_backface_culling = not s.get('doubleSided', False)
        for field, socket, noncolor in [('baseMap', 'Base Color', False),
                                         ('normalMap', None, True),
                                         ('emissionMap', 'Emission Color', False)]:
            path = s.get(field)
            if not path:
                continue
            image = bpy.data.images.load(os.path.join(REPO, path), check_existing=True)
            image.colorspace_settings.name = 'Non-Color' if noncolor else 'sRGB'
            image.pack()
            tex = nodes.new('ShaderNodeTexImage')
            tex.image = image
            if field == 'normalMap':
                normal = nodes.new('ShaderNodeNormalMap')
                links.new(tex.outputs['Color'], normal.inputs['Color'])
                links.new(normal.outputs['Normal'], shader.inputs['Normal'])
            else:
                tint = s['baseColor'] if field == 'baseMap' else (*s['emissive'], 1)
                multiply = nodes.new('ShaderNodeMixRGB')
                multiply.blend_type = 'MULTIPLY'
                multiply.inputs[0].default_value = 1
                multiply.inputs[2].default_value = tint
                links.new(tex.outputs['Color'], multiply.inputs[1])
                links.new(multiply.outputs['Color'], shader.inputs[socket])
    return unknown


def bounds(objects):
    points = [o.matrix_world @ v.co for o in objects for v in o.data.vertices]
    lo = [min(p[k] for p in points) for k in range(3)]
    hi = [max(p[k] for p in points) for k in range(3)]
    return lo, hi


def action_curves(action):
    if hasattr(action, 'fcurves'):
        return list(action.fcurves)
    return [curve for layer in action.layers for strip in layer.strips
            for bag in strip.channelbags for curve in bag.fcurves]


def canonical_actions():
    kept = []
    for action in list(bpy.data.actions):
        if not any('pose.bones[' in curve.data_path for curve in action_curves(action)):
            bpy.data.actions.remove(action)
            continue
        action.name = action.name.split('|')[-1]
        action.use_fake_user = True
        kept.append(action)
    for rig in (o for o in bpy.context.scene.objects if o.type == 'ARMATURE'):
        rig.animation_data_create()
        rig.animation_data.action = None
        for action in kept:
            track = rig.animation_data.nla_tracks.new()
            track.name = action.name
            strip = track.strips.new(action.name, int(action.frame_range[0]), action)
            strip.extrapolation = 'NOTHING'
            track.mute = True
    bpy.context.scene.frame_set(1)
    return sorted(a.name for a in kept)


def mesh_facts(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    zero, opposed, invalid = 0, 0, 0
    normals = [n.vector.copy() for n in mesh.corner_normals]
    for normal in normals:
        if not all(math.isfinite(v) for v in normal) or abs(normal.length-1) > .001:
            invalid += 1
    for tri in mesh.loop_triangles:
        a, b, c = (mesh.vertices[i].co for i in tri.vertices)
        cross = (b-a).cross(c-a)
        if cross.length < 2e-12:
            zero += 1
            continue
        average = sum((normals[i] for i in tri.loops), Vector())
        if average.length and cross.normalized().dot(average.normalized()) < -.2:
            opposed += 1
    vertex_groups = {group.name for group in obj.vertex_groups}
    weighted = sum(any(g.weight > 0 for g in vertex.groups) for vertex in mesh.vertices)
    return {'name': obj.name, 'triangles': len(mesh.loop_triangles),
            'vertices': len(mesh.vertices), 'materials': [m.name for m in mesh.materials if m],
            'degenerate_triangles': zero, 'opposed_corner_normals': opposed,
            'invalid_unit_normals': invalid, 'uv_layers': len(mesh.uv_layers),
            'weighted_vertices': weighted, 'weight_groups': sorted(vertex_groups)}


start = time.monotonic()
for source in report['files']:
    if only and source['kind'] not in only:
        continue
    clean_scene()
    path = os.path.join(REPO, source['output'])
    if open(path, 'rb').read(50).startswith(b'version https://git-lfs'):
        raise RuntimeError('LFS pointer is not hydrated: ' + path)
    bpy.ops.import_scene.fbx(filepath=path, use_custom_normals=True)
    unknown = restore_materials()
    objects = list(bpy.context.scene.objects)
    meshes = [o for o in objects if o.type == 'MESH']
    facts = [mesh_facts(o) for o in meshes]
    lo, hi = bounds(meshes)
    unity_lo, unity_hi = [lo[0], lo[2], lo[1]], [hi[0], hi[2], hi[1]]
    animations = canonical_actions() if source['kind'] == 'avatar' else []
    record = {'kind': source['kind'], 'name': source['name'], 'source': source['output'],
              'sha256': hashlib.sha256(open(path, 'rb').read()).hexdigest(),
              'triangles': sum(f['triangles'] for f in facts), 'meshes': facts,
              'bounds_min_unity': unity_lo, 'bounds_max_unity': unity_hi,
              'unknown_materials': unknown, 'animations': animations,
              'count_matches_source': sum(f['triangles'] for f in facts) == source['triangles'],
              'bounds_match_source': all(abs(a-b) <= .001 for a,b in zip(unity_lo+unity_hi, source['bounds_min']+source['bounds_max']))}
    if not record['count_matches_source'] or not record['bounds_match_source'] or unknown:
        raise RuntimeError('Supplied source/pipeline mismatch: ' + json.dumps(record))
    wrapper = bpy.data.objects.new('Wreckabulary_ExportRoot', None)
    bpy.context.scene.collection.objects.link(wrapper)
    wrapper.rotation_euler.z = math.pi
    for obj in objects:
        if obj.parent is None:
            obj.parent = wrapper
            obj.matrix_parent_inverse = Matrix.Identity(4)
    bpy.context.view_layer.update()
    glb_lo, glb_hi = bounds(meshes)
    web_lo = [glb_lo[0], glb_lo[2], -glb_hi[1]]
    web_hi = [glb_hi[0], glb_hi[2], -glb_lo[1]]
    kind, name = source['kind'], source['name']
    folder = {'item':'items', 'letter':'letters', 'environment':'environment', 'vfx':'vfx', 'avatar':''}[kind]
    filename = 'avatar.glb' if kind == 'avatar' else name + '.glb'
    relative = '/'.join(p for p in ('art', folder, filename) if p)
    destination = os.path.join(OUT, folder, filename)
    os.makedirs(os.path.dirname(destination), exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=destination, export_format='GLB',
        use_selection=True, export_yup=True, export_apply=False,
        export_animations=kind == 'avatar', export_animation_mode='NLA_TRACKS',
        export_anim_single_armature=True, export_rest_position_armature=True,
        export_frame_range=False, export_force_sampling=True,
        export_cameras=False, export_lights=False, export_tangents=True)
    record['material_validation'] = restore_glb_materials(destination, specs, REPO)
    item = {'path': relative, 'size': [round(b-a, 6) for a,b in zip(web_lo, web_hi)],
            'boundsMin': [round(x,6) for x in web_lo], 'boundsMax': [round(x,6) for x in web_hi],
            'triangles': record['triangles'], 'bytes': os.path.getsize(destination),
            'sha256': hashlib.sha256(open(destination,'rb').read()).hexdigest()}
    if kind == 'item':
        grip = next(o for o in objects if o.name.startswith('Grip_R'))
        p = grip.matrix_world.translation
        item['grip'] = [round(p.x,6), round(p.z,6), round(-p.y,6)]
        manifest['items'][name] = item
    elif kind == 'avatar':
        item['animations'] = animations
        if source.get('locomotion'):
            item['locomotion'] = source['locomotion']
        item['meshes'] = [m.name for m in meshes]
        item['bones'] = source['bones']
        default_meshes = {'SK_Head'} | {p['mesh'] for p in wardrobe['pieces'] if p['id'] in wardrobe['default']['pieces'].values()}
        item['defaultTriangles'] = sum(f['triangles'] for f in facts if f['name'] in default_meshes)
        manifest['avatar'] = item
    else:
        manifest[folder][name] = item
    record['export'] = item
    audit.append(record)
    print('WEB_ASSET ' + name + ' ' + str(record['triangles']), flush=True)

with open(os.path.join(OUT, 'manifest.json'), 'w') as f:
    json.dump(manifest, f, indent=2)
with open(os.path.join(OUT, 'audit.json'), 'w') as f:
    json.dump({'generator': 'Tools/AssetPipeline/export_web.py', 'blender': bpy.app.version_string,
               'seconds': round(time.monotonic()-start,2), 'files': audit}, f, indent=2)
print('WEB_EXPORT_COMPLETE ' + str(len(audit)), flush=True)

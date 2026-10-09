import argparse
import hashlib
import json
import math
from pathlib import Path
import struct

parser=argparse.ArgumentParser()
parser.add_argument('--repo',default='.')
args=parser.parse_args()
repo=Path(args.repo).resolve()
root=repo/'Web/public/art'
manifest=json.loads((root/'manifest.json').read_text())
source=json.loads((repo/'Assets/_Project/Data/Generated/build_report.json').read_text())
specs={m['name']:m for m in json.loads((repo/'Assets/_Project/Data/Generated/materials.json').read_text())['materials']}
mobile_audit=root/'avatar-mobile-audit.json'
mobile_item=None
if mobile_audit.exists():
    mobile=json.loads(mobile_audit.read_text())
    base=next(entry for entry in source['files'] if entry['kind']=='avatar')
    derivative=dict(base)
    derivative['name']='Avatar-Mobile'
    derivative['triangles']=mobile['after_triangles']
    derivative['mobile_derivative']=True
    source['files'].append(derivative)
    mobile_item=dict(manifest['avatar'])
    mobile_item.update(path='art/avatar-mobile.glb',bytes=(root/'avatar-mobile.glb').stat().st_size,
                       sha256=mobile['output_sha256'])
types={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}
components={5120:('b',1),5121:('B',1),5122:('h',2),5123:('H',2),5125:('I',4),5126:('f',4)}
results=[]


def identity(): return [[float(i==j) for j in range(4)] for i in range(4)]


def multiply(a,b): return [[sum(a[i][k]*b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def node_matrix(node):
    if 'matrix' in node: return [[node['matrix'][j*4+i] for j in range(4)] for i in range(4)]
    x,y,z,w=node.get('rotation',[0,0,0,1])
    q=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w),0],
       [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w),0],
       [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y),0],[0,0,0,1]]
    scale=node.get('scale',[1,1,1]);translation=node.get('translation',[0,0,0])
    for i in range(3):
        for j in range(3):q[i][j]*=scale[j]
        q[i][3]=translation[i]
    return q


for entry in source['files']:
    group={'item':'items','letter':'letters','environment':'environment','vfx':'vfx','avatar':'avatar'}[entry['kind']]
    item=mobile_item if entry.get('mobile_derivative') else manifest['avatar'] if group=='avatar' else manifest[group][entry['name']]
    path=repo/'Web/public'/item['path']
    data=path.read_bytes()
    issues=[]
    header=struct.unpack_from('<III',data)
    if header!=(0x46546C67,2,len(data)):issues.append('invalid GLB header/size')
    offset,document,binary=12,None,b''
    while offset<len(data):
        length,kind=struct.unpack_from('<II',data,offset)
        chunk=data[offset+8:offset+8+length]
        if length%4:issues.append('unaligned chunk')
        if kind==0x4E4F534A:document=json.loads(chunk)
        if kind==0x004E4942:binary=chunk
        offset+=8+length
    if item['sha256']!=hashlib.sha256(data).hexdigest():issues.append('manifest SHA-256 mismatch')
    if item['bytes']!=len(data):issues.append('manifest byte-size mismatch')
    if not document or document['asset']['version']!='2.0':raise RuntimeError('Missing glTF2 document')

    def accessor(index):
        a=document['accessors'][index]
        fmt,size=components[a['componentType']];width=types[a['type']]
        rows=[(0,)*width for _ in range(a['count'])]
        def read_view(view_index,offset,count,component,width):
            view=document['bufferViews'][view_index]
            format,size=components[component];stride=view.get('byteStride',size*width)
            start=view.get('byteOffset',0)+offset
            end=start+max(0,count-1)*stride+size*width
            if count and (end>view.get('byteOffset',0)+view['byteLength'] or end>len(binary)):
                raise RuntimeError('Out-of-bounds accessor')
            return [struct.unpack_from('<'+format*width,binary,start+n*stride) for n in range(count)]
        if 'bufferView' in a:
            rows=read_view(a['bufferView'],a.get('byteOffset',0),a['count'],a['componentType'],width)
        if 'sparse' in a:
            sparse=a['sparse'];indices=sparse['indices'];values=sparse['values']
            keys=[v[0] for v in read_view(indices['bufferView'],indices.get('byteOffset',0),sparse['count'],indices['componentType'],1)]
            replacements=read_view(values['bufferView'],values.get('byteOffset',0),sparse['count'],a['componentType'],width)
            if keys!=sorted(set(keys)) or any(i>=a['count'] for i in keys):
                raise RuntimeError('Invalid sparse accessor indices')
            for key,value in zip(keys,replacements):rows[key]=value
        return rows

    for view in document.get('bufferViews',[]):
        if view.get('buffer',0)!=0 or view.get('byteOffset',0)+view['byteLength']>len(binary):issues.append('invalid bufferView')
    for ai in range(len(document.get('accessors',[]))):
        if any(not math.isfinite(v) for row in accessor(ai) for v in row):
            issues.append('nonfinite accessor '+str(ai))
    image_checks=0
    for material in document.get('materials',[]):
        spec=specs[material['name']]
        pbr=material.get('pbrMetallicRoughness',{})
        for field,actual,expected in [('baseColorFactor',pbr.get('baseColorFactor',[1]*4),spec['baseColor']),
                                       ('metallicFactor',pbr.get('metallicFactor',1),spec['metallic']),
                                       ('roughnessFactor',pbr.get('roughnessFactor',1),spec['roughness'])]:
            left=actual if isinstance(actual,list) else [actual]
            right=expected if isinstance(expected,list) else [expected]
            if any(abs(a-b)>1e-6 for a,b in zip(left,right)):issues.append(material['name']+' authored '+field+' mismatch')
        strength=material.get('extensions',{}).get('KHR_materials_emissive_strength',{}).get('emissiveStrength',1)
        if any(abs(a*strength-b)>1e-6 for a,b in zip(material.get('emissiveFactor',[0]*3),spec['emissive'])):
            issues.append(material['name']+' authored emission mismatch')
        for source_field,texture in [('baseMap',pbr.get('baseColorTexture')),('normalMap',material.get('normalTexture')),
                                     ('emissionMap',material.get('emissiveTexture'))]:
            if not spec.get(source_field):continue
            if not texture:issues.append(material['name']+' missing '+source_field);continue
            image=document['images'][document['textures'][texture['index']]['source']]
            view=document['bufferViews'][image['bufferView']]
            begin=view.get('byteOffset',0)
            actual=binary[begin:begin+view['byteLength']]
            if hashlib.sha256(actual).digest()!=hashlib.sha256((repo/spec[source_field]).read_bytes()).digest():
                issues.append(material['name']+' changed '+source_field+' image')
            image_checks+=1
    parents={child:i for i,node in enumerate(document.get('nodes',[])) for child in node.get('children',[])}
    cache={}
    def world(index):
        if index not in cache:
            local=node_matrix(document['nodes'][index])
            cache[index]=multiply(world(parents[index]),local) if index in parents else local
        return cache[index]
    triangle_count=0;points=[];bad_normals=0;bad_weights=0;bad_joints=0;degenerate=0;opposed=0
    mesh_triangles={};mesh_material_slots={}
    for ni,node in enumerate(document.get('nodes',[])):
        if 'mesh' not in node:continue
        matrix=world(ni)
        for primitive in document['meshes'][node['mesh']]['primitives']:
            if primitive.get('mode',4)!=4:issues.append('nontriangle primitive');continue
            vertices=accessor(primitive['attributes']['POSITION'])
            for morph in primitive.get('targets',[]):
                if any(len(accessor(a))!=len(vertices) for a in morph.values()):
                    issues.append('morph accessor vertex count mismatch')
            normals=accessor(primitive['attributes']['NORMAL'])
            bad_normals+=sum(not all(math.isfinite(v) for v in n) or abs(sum(v*v for v in n)-1)>.002 for n in normals)
            if 'WEIGHTS_0' in primitive['attributes']:
                bad_weights+=sum(abs(sum(w)-1)>.002 for w in accessor(primitive['attributes']['WEIGHTS_0']))
                if 'skin' not in node:issues.append('weighted mesh has no skin')
                else:
                    joint_count=len(document['skins'][node['skin']]['joints'])
                    bad_joints+=sum(any(j>=joint_count for j in row) for row in accessor(primitive['attributes']['JOINTS_0']))
            indices=[i[0] for i in accessor(primitive['indices'])]
            triangle_count+=len(indices)//3
            mesh_name=document['meshes'][node['mesh']]['name']
            mesh_triangles[mesh_name]=mesh_triangles.get(mesh_name,0)+len(indices)//3
            mesh_material_slots[mesh_name]=mesh_material_slots.get(mesh_name,0)+1
            if len(indices)%3:issues.append('partial triangle')
            if indices and max(indices)>=len(vertices):issues.append('invalid triangle index')
            for v in vertices:points.append([sum(matrix[i][k]*v[k] for k in range(3))+matrix[i][3] for i in range(3)])
            for at in range(0,len(indices),3):
                ids=indices[at:at+3];a,b,c=(vertices[i] for i in ids)
                ab=[b[i]-a[i] for i in range(3)];ac=[c[i]-a[i] for i in range(3)]
                cross=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
                magnitude=math.sqrt(sum(v*v for v in cross))
                if magnitude<2e-12:degenerate+=1;continue
                avg=[sum(normals[j][i] for j in ids) for i in range(3)]
                norm=math.sqrt(sum(v*v for v in avg))
                if norm and sum(cross[i]*avg[i] for i in range(3))/(magnitude*norm)<-.2:opposed+=1
    if triangle_count!=entry['triangles']:issues.append('triangle count differs from source')
    if bad_normals:issues.append('nonfinite/nonunit normals: '+str(bad_normals))
    if bad_weights:issues.append('unnormalized skin weights: '+str(bad_weights))
    if bad_joints:issues.append('out-of-range joint references: '+str(bad_joints))
    if degenerate:issues.append('degenerate triangles: '+str(degenerate))
    if opposed:issues.append('opposed corner normals: '+str(opposed))
    lo=[min(p[k] for p in points) for k in range(3)];hi=[max(p[k] for p in points) for k in range(3)]
    bound_tolerance=.015 if entry.get('mobile_derivative') else .001
    if any(abs(a-b)>bound_tolerance for a,b in zip(lo+hi,item['boundsMin']+item['boundsMax'])):issues.append('transformed bounds differ from manifest/full-detail reference')
    clips=[a.get('name','') for a in document.get('animations',[])]
    default_triangles=None;default_slots=None
    if entry['kind']=='avatar':
        default_meshes={'SK_Head'}|{p['mesh'] for p in manifest['wardrobe']['pieces']
            if p['id'] in manifest['wardrobe']['default']['pieces'].values()}
        default_triangles=sum(mesh_triangles.get(n,0) for n in default_meshes)
        default_slots=sum(mesh_material_slots.get(n,0) for n in default_meshes)
        if default_triangles!=(mobile['default_after'] if entry.get('mobile_derivative') else item['defaultTriangles']):
            issues.append('default outfit triangle count mismatch')
        if sorted(clips)!=sorted(entry['actions']):issues.append('avatar canonical animation set differs')
        bone_names={document['nodes'][i]['name'] for skin in document.get('skins',[]) for i in skin['joints']}
        if bone_names!=set(entry['bones']):issues.append('avatar bone set differs')
        if entry.get('mobile_derivative'):
            original_data=(root/'avatar.glb').read_bytes()
            json_length=struct.unpack_from('<I',original_data,12)[0]
            original_doc=json.loads(original_data[20:20+json_length])
            if hashlib.sha256(original_data).hexdigest()!=mobile['source_sha256']:
                issues.append('mobile audit references a different full-detail source')
            if manifest['avatar'].get('mobilePath'):
                for key,expected in [('mobilePath','art/avatar-mobile.glb'),('mobileTriangles',triangle_count),
                                     ('mobileDefaultTriangles',default_triangles),('mobileBytes',len(data)),
                                     ('mobileSha256',hashlib.sha256(data).hexdigest())]:
                    if manifest['avatar'].get(key)!=expected:issues.append('mobile manifest '+key+' mismatch')
            original_bin_length=struct.unpack_from('<I',original_data,20+json_length)[0]
            original_binary=original_data[28+json_length:28+json_length+original_bin_length]
            def stream(doc,payload,index):
                a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
                _,size=components[a['componentType']];width=types[a['type']]
                stride=v.get('byteStride',size*width)
                start=v.get('byteOffset',0)+a.get('byteOffset',0)
                return (a['componentType'],a['type'],a['count'],
                        b''.join(payload[start+i*stride:start+i*stride+size*width] for i in range(a['count'])))
            original_clip_lengths={a['name']:max(original_doc['accessors'][s['input']]['max'][0]
                for s in a['samplers']) for a in original_doc['animations']}
            original_animations={a['name']:a for a in original_doc['animations']}
            for animation in document['animations']:
                duration=max(document['accessors'][s['input']]['max'][0] for s in animation['samplers'])
                if abs(duration-original_clip_lengths[animation['name']])>.001:
                    issues.append(animation['name']+' mobile animation duration changed')
                def channels(doc,clip):
                    return {(doc['nodes'][c['target']['node']]['name'],c['target']['path']):clip['samplers'][c['sampler']]
                            for c in clip['channels']}
                original_channels=channels(original_doc,original_animations[animation['name']])
                mobile_channels=channels(document,animation)
                if original_channels.keys()!=mobile_channels.keys():
                    issues.append(animation['name']+' mobile animation channel targets changed');continue
                for key,original_sampler in original_channels.items():
                    mobile_sampler=mobile_channels[key]
                    if original_sampler.get('interpolation','LINEAR')!=mobile_sampler.get('interpolation','LINEAR'):
                        issues.append(animation['name']+' mobile interpolation changed: '+str(key))
                    for field in ('input','output'):
                        if stream(original_doc,original_binary,original_sampler[field])!=stream(document,binary,mobile_sampler[field]):
                            issues.append(animation['name']+' original animation '+field+' bytes changed: '+str(key))
            original_nodes={n['name']:n for n in original_doc['nodes']}
            for node in document['nodes']:
                if node['name'] not in original_nodes:issues.append('new mobile node '+node['name']);continue
                a=node_matrix(node);b=node_matrix(original_nodes[node['name']])
                if max(abs(a[i][j]-b[i][j]) for i in range(4) for j in range(4))>1e-7:
                    issues.append(node['name']+' mobile rest transform changed')
            for original_skin,mobile_skin in zip(original_doc['skins'],document['skins']):
                if stream(original_doc,original_binary,original_skin['inverseBindMatrices'])!=stream(document,binary,mobile_skin['inverseBindMatrices']):
                    issues.append('original inverse-bind bytes changed')
            original_targets={m['name']:len(m['primitives'][0].get('targets',[])) for m in original_doc['meshes']}
            for mesh in document['meshes']:
                if len(mesh['primitives'][0].get('targets',[]))!=original_targets.get(mesh['name'],0):
                    issues.append(mesh['name']+' mobile facial morph target count changed')
    elif clips:issues.append('static model unexpectedly has animation')
    results.append({'name':entry['name'],'kind':entry['kind'],'triangles':triangle_count,'issues':issues,
                    'unchanged_texture_checks':image_checks,'animations':clips,
                    'bounds_min':lo,'bounds_max':hi,'bounds_tolerance_m':bound_tolerance,
                    'mesh_triangles':mesh_triangles,'default_visible_triangles':default_triangles,
                    'default_material_slots':default_slots,
                    'maximum_reference_bounds_delta_m':max(abs(a-b) for a,b in zip(lo+hi,item['boundsMin']+item['boundsMax']))})

output={'checked':len(results),'failed':sum(bool(r['issues']) for r in results),'results':results}
(root/'verify.json').write_text(json.dumps(output,indent=2)+'\n')
print(json.dumps({'checked':output['checked'],'failed':output['failed'],
                  'first_failures':[r for r in results if r['issues']][:4]}))
sys_exit=1 if output['failed'] else 0
raise SystemExit(sys_exit)

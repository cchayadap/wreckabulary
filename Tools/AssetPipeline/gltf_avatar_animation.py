from copy import deepcopy
import hashlib
import json
from pathlib import Path
import struct

from gltf_materials import read_glb


def local_matrix(node):
    if 'matrix' in node:
        return [[node['matrix'][j*4+i] for j in range(4)] for i in range(4)]
    x,y,z,w=node.get('rotation',[0,0,0,1])
    matrix=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w),0],
            [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w),0],
            [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y),0],[0,0,0,1]]
    scale=node.get('scale',[1,1,1]);translation=node.get('translation',[0,0,0])
    for i in range(3):
        for j in range(3):matrix[i][j]*=scale[j]
        matrix[i][3]=translation[i]
    return matrix


def preserve(source_path,destination_path):
    source,source_chunks=read_glb(source_path)
    target,target_chunks=read_glb(destination_path)
    source_binary=next(data for kind,data in source_chunks if kind==0x004E4942)
    target_binary=bytearray(next(data for kind,data in target_chunks if kind==0x004E4942))
    source_names={n['name']:i for i,n in enumerate(source['nodes'])}
    target_names={n['name']:i for i,n in enumerate(target['nodes'])}
    if len(source_names)!=len(source['nodes']) or len(target_names)!=len(target['nodes']):
        raise ValueError('Ambiguous duplicate node names')
    if source_names.keys()!=target_names.keys():raise ValueError('Mobile hierarchy node set changed')
    maximum_delta=0
    for name,si in source_names.items():
        ti=target_names[name];original=source['nodes'][si];mobile=target['nodes'][ti]
        if {source['nodes'][i]['name'] for i in original.get('children',[])}!=\
                {target['nodes'][i]['name'] for i in mobile.get('children',[])}:
            raise ValueError('Mobile hierarchy parent changed: '+name)
        a=local_matrix(original);b=local_matrix(mobile)
        delta=max(abs(a[i][j]-b[i][j]) for i in range(4) for j in range(4))
        maximum_delta=max(maximum_delta,delta)
        if delta>2e-6:raise ValueError('Mobile rest basis changed: '+name)
        for field in ('matrix','translation','rotation','scale'):
            mobile.pop(field,None)
            if field in original:mobile[field]=deepcopy(original[field])
    view_map={};accessor_map={}
    def copy_view(index):
        if index not in view_map:
            view=deepcopy(source['bufferViews'][index])
            if view.get('buffer',0)!=0:raise ValueError('External animation buffer unsupported')
            start=view.get('byteOffset',0)
            data=source_binary[start:start+view['byteLength']]
            if len(data)!=view['byteLength']:raise ValueError('Invalid source animation view')
            target_binary.extend(b'\0'*((-len(target_binary))%4))
            view['buffer']=0;view['byteOffset']=len(target_binary)
            target_binary.extend(data)
            view_map[index]=len(target['bufferViews']);target['bufferViews'].append(view)
        return view_map[index]
    def copy_accessor(index):
        if index not in accessor_map:
            accessor=deepcopy(source['accessors'][index])
            if 'sparse' in accessor or 'bufferView' not in accessor:
                raise ValueError('Sparse/external animation accessor unsupported')
            accessor['bufferView']=copy_view(accessor['bufferView'])
            accessor_map[index]=len(target['accessors']);target['accessors'].append(accessor)
        return accessor_map[index]
    if len(source['skins'])!=len(target['skins']):raise ValueError('Mobile skin count changed')
    for original,mobile in zip(source['skins'],target['skins']):
        if [source['nodes'][i]['name'] for i in original['joints']]!=\
                [target['nodes'][i]['name'] for i in mobile['joints']]:
            raise ValueError('Mobile joint index order changed')
        mobile['inverseBindMatrices']=copy_accessor(original['inverseBindMatrices'])
    target['animations']=deepcopy(source['animations'])
    for animation in target['animations']:
        for sampler in animation['samplers']:
            sampler['input']=copy_accessor(sampler['input'])
            sampler['output']=copy_accessor(sampler['output'])
        for channel in animation['channels']:
            old_node=channel['target']['node']
            channel['target']['node']=target_names[source['nodes'][old_node]['name']]
    used_accessors=set()
    for mesh in target['meshes']:
        for primitive in mesh['primitives']:
            used_accessors.update(primitive.get('attributes',{}).values())
            if 'indices' in primitive:used_accessors.add(primitive['indices'])
            for morph in primitive.get('targets',[]):used_accessors.update(morph.values())
    for skin in target['skins']:used_accessors.add(skin['inverseBindMatrices'])
    for animation in target['animations']:
        for sampler in animation['samplers']:used_accessors.update((sampler['input'],sampler['output']))
    accessor_indices={old:new for new,old in enumerate(sorted(used_accessors))}
    used_views=set()
    for old in used_accessors:
        accessor=target['accessors'][old]
        if 'bufferView' in accessor:used_views.add(accessor['bufferView'])
        if 'sparse' in accessor:
            used_views.update(accessor['sparse'][field]['bufferView'] for field in ('indices','values'))
    used_views.update(image['bufferView'] for image in target.get('images',[]) if 'bufferView' in image)
    view_indices={old:new for new,old in enumerate(sorted(used_views))}
    compact_binary=bytearray();compact_views=[]
    for old in sorted(used_views):
        view=deepcopy(target['bufferViews'][old]);start=view.get('byteOffset',0)
        compact_binary.extend(b'\0'*((-len(compact_binary))%4))
        view['byteOffset']=len(compact_binary)
        compact_binary.extend(target_binary[start:start+view['byteLength']]);compact_views.append(view)
    compact_accessors=[]
    for old in sorted(used_accessors):
        accessor=deepcopy(target['accessors'][old])
        if 'bufferView' in accessor:accessor['bufferView']=view_indices[accessor['bufferView']]
        if 'sparse' in accessor:
            for field in ('indices','values'):
                sparse=accessor['sparse'][field];sparse['bufferView']=view_indices[sparse['bufferView']]
        compact_accessors.append(accessor)
    for mesh in target['meshes']:
        for primitive in mesh['primitives']:
            primitive['attributes']={k:accessor_indices[v] for k,v in primitive['attributes'].items()}
            if 'indices' in primitive:primitive['indices']=accessor_indices[primitive['indices']]
            for morph in primitive.get('targets',[]):
                for k,v in list(morph.items()):morph[k]=accessor_indices[v]
    for skin in target['skins']:skin['inverseBindMatrices']=accessor_indices[skin['inverseBindMatrices']]
    for animation in target['animations']:
        for sampler in animation['samplers']:
            sampler['input']=accessor_indices[sampler['input']];sampler['output']=accessor_indices[sampler['output']]
    for image in target.get('images',[]):
        if 'bufferView' in image:image['bufferView']=view_indices[image['bufferView']]
    target['accessors']=compact_accessors;target['bufferViews']=compact_views
    target['buffers']=[{'byteLength':len(compact_binary)}]
    encoded=json.dumps(target,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
    compact_binary.extend(b'\0'*((-len(compact_binary))%4))
    body=struct.pack('<II',len(encoded),0x4E4F534A)+encoded
    body+=struct.pack('<II',len(compact_binary),0x004E4942)+compact_binary
    Path(destination_path).write_bytes(struct.pack('<III',0x46546C67,2,len(body)+12)+body)
    return {'clips_copied_without_resampling':len(target['animations']),
            'channels_copied':sum(len(a['channels']) for a in target['animations']),
            'maximum_rest_basis_roundtrip_delta':maximum_delta,
            'timing_and_interpolation':'original accessor bytes and interpolation preserved'}


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser();parser.add_argument('--repo',default='.')
    args=parser.parse_args();root=Path(args.repo).resolve()/'Web/public/art'
    checks=preserve(root/'avatar.glb',root/'avatar-mobile.glb')
    report=json.loads((root/'avatar-mobile-audit.json').read_text())
    report['animation_preservation']=checks
    report['output_sha256']=hashlib.sha256((root/'avatar-mobile.glb').read_bytes()).hexdigest()
    (root/'avatar-mobile-audit.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(checks))

"""Preserve authored glTF material factors when exporting the named-material FBXs.

Blender 4.3 drops a legacy Multiply-node constant while retaining its unchanged
texture. Restore factors from the authoritative generated material library, and
assert embedded texture bytes were not baked/recoloured before doing so.
"""
import hashlib
import json
from pathlib import Path
import struct


def read_glb(path):
    data = Path(path).read_bytes()
    magic, version, size = struct.unpack_from('<III', data)
    if magic != 0x46546C67 or version != 2 or size != len(data):
        raise ValueError('Invalid GLB container: ' + str(path))
    offset, document, chunks = 12, None, []
    while offset < len(data):
        length, kind = struct.unpack_from('<II', data, offset)
        chunk = data[offset+8:offset+8+length]
        if kind == 0x4E4F534A:
            document = json.loads(chunk)
        else:
            chunks.append((kind, chunk))
        offset += 8+length
    if document is None:
        raise ValueError('Missing GLB JSON')
    return document, chunks


def restore(path, specs, repo):
    document, chunks = read_glb(path)
    binary = next((data for kind,data in chunks if kind == 0x004E4942), b'')
    checked_images = 0
    for material in document.get('materials', []):
        spec = specs[material['name']]
        pbr = material.setdefault('pbrMetallicRoughness', {})
        for source_field, texture in (
            ('baseMap', pbr.get('baseColorTexture')),
            ('normalMap', material.get('normalTexture')),
            ('emissionMap', material.get('emissiveTexture')),
        ):
            expected = spec.get(source_field)
            if not expected:
                continue
            if not texture:
                raise ValueError(f"{path}: {material['name']} missing {source_field}")
            image = document['images'][document['textures'][texture['index']]['source']]
            view = document['bufferViews'][image['bufferView']]
            offset = view.get('byteOffset',0)
            actual = binary[offset:offset+view['byteLength']]
            original = (Path(repo)/expected).read_bytes()
            if hashlib.sha256(actual).digest() != hashlib.sha256(original).digest():
                raise ValueError(f"{path}: texture changed; cannot apply factor twice safely: {material['name']} {source_field}")
            checked_images += 1
        pbr['baseColorFactor'] = spec['baseColor']
        pbr['metallicFactor'] = spec['metallic']
        pbr['roughnessFactor'] = spec['roughness']
        emission = spec['emissive']
        strength = max(1, max(emission))
        material['emissiveFactor'] = [v/strength for v in emission]
        if strength > 1:
            material.setdefault('extensions',{})['KHR_materials_emissive_strength'] = {'emissiveStrength':strength}
            extensions = document.setdefault('extensionsUsed',[])
            if 'KHR_materials_emissive_strength' not in extensions:
                extensions.append('KHR_materials_emissive_strength')
        material['alphaMode'] = spec.get('alphaMode','OPAQUE')
        if material['alphaMode'] == 'MASK': material['alphaCutoff'] = spec.get('alphaCutoff',.5)
        material['doubleSided'] = spec.get('doubleSided',False)
    encoded = json.dumps(document,separators=(',',':')).encode('utf8')
    encoded += b' '*((-len(encoded))%4)
    body = struct.pack('<II',len(encoded),0x4E4F534A)+encoded
    for kind,chunk in chunks:
        body += struct.pack('<II',len(chunk),kind)+chunk
    Path(path).write_bytes(struct.pack('<III',0x46546C67,2,len(body)+12)+body)
    return {'materials':len(document.get('materials',[])), 'unchanged_images_checked':checked_images}


if __name__ == '__main__':
    import argparse
    parser=argparse.ArgumentParser()
    parser.add_argument('--repo',default='.')
    args=parser.parse_args()
    repo=Path(args.repo).resolve()
    root=repo/'Web/public/art'
    specs={m['name']:m for m in json.loads((repo/'Assets/_Project/Data/Generated/materials.json').read_text())['materials']}
    manifest=json.loads((root/'manifest.json').read_text())
    audit=json.loads((root/'audit.json').read_text())
    records=[]
    for source in audit['files']:
        item=source['export']
        path=repo/'Web/public'/item['path']
        source['material_validation']=restore(path,specs,repo)
        item['bytes']=path.stat().st_size
        item['sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
        kind=source['kind']
        if kind=='avatar': manifest['avatar']=item
        else:manifest[{'item':'items','letter':'letters','environment':'environment','vfx':'vfx'}[kind]][source['name']]=item
        records.append(source['material_validation'])
    (root/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (root/'audit.json').write_text(json.dumps(audit,indent=2)+'\n')
    print(json.dumps({'glbs':len(records),'material_checks':sum(r['materials'] for r in records),
                     'unchanged_texture_checks':sum(r['unchanged_images_checked'] for r in records)}))

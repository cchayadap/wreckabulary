"""Read-only original GLB geometry preflight; no Blender, no asset writes."""
import hashlib
import ast
import json
import math
from pathlib import Path
import struct
import sys
import numpy as np

path = Path(sys.argv[1])
data = path.read_bytes()
chunks = {}
offset = 12
while offset < len(data):
    size, kind = struct.unpack_from('<II', data, offset)
    chunks[kind] = data[offset+8:offset+8+size]
    offset += size+8
gltf = json.loads(chunks[0x4e4f534a])
binary = chunks[0x004e4942]


def accessor(index):
    a = gltf['accessors'][index]
    b = gltf['bufferViews'][a['bufferView']]
    dtype = {5126: '<f4', 5125: '<u4', 5123: '<u2'}[a['componentType']]
    count = {'VEC3': 3, 'SCALAR': 1}[a['type']]
    start = a.get('byteOffset', 0) + b.get('byteOffset', 0)
    step = b.get('byteStride', np.dtype(dtype).itemsize*count)
    return np.ndarray((a['count'], count), dtype=dtype, buffer=binary, offset=start,
                      strides=(step, np.dtype(dtype).itemsize)).copy()


def transform(node):
    if 'matrix' in node:
        return np.array(node['matrix']).reshape(4,4).T
    x,y,z,w = node.get('rotation', [0,0,0,1])
    result = np.eye(4)
    result[:3,:3] = np.array(((1-2*y*y-2*z*z, 2*x*y-2*z*w, 2*x*z+2*y*w),
                              (2*x*y+2*z*w, 1-2*x*x-2*z*z, 2*y*z-2*x*w),
                              (2*x*z-2*y*w, 2*y*z+2*x*w, 1-2*x*x-2*y*y))) @ np.diag(node.get('scale', [1,1,1]))
    result[:3,3] = node.get('translation', [0,0,0])
    return result


world = {}
def visit(index, parent):
    node = gltf['nodes'][index]
    current = parent @ transform(node)
    if 'mesh' in node:
        world[node['mesh']] = current
    for child in node.get('children', []):
        visit(child, current)
for root in gltf['scenes'][gltf.get('scene', 0)]['nodes']:
    visit(root, np.eye(4))

primitive = next(p for p in gltf['meshes'][0]['primitives']
                 if gltf['materials'][p['material']]['name'] == 'green_Classic')
vertices = accessor(primitive['attributes']['POSITION']).astype(float)
faces = accessor(primitive['indices']).reshape(-1,3)
keys = [tuple(np.round(v,6)) for v in vertices]
parent = {v:v for v in keys}
def find(key):
    while parent[key] != key:
        parent[key] = parent[parent[key]]
        key = parent[key]
    return key
for face in faces:
    first = find(keys[face[0]])
    for index in face[1:]:
        parent[find(keys[index])] = first
groups = {}
for face in faces:
    groups.setdefault(find(keys[face[0]]), []).append(face)
conversion = np.array(((1,0,0,0), (0,0,-1,0), (0,1,0,0), (0,0,0,1)))
matrix = conversion @ world[0]
vertices = (np.c_[vertices, np.ones(len(vertices))] @ matrix.T)[:,:3]


def ray(origin, direction, triangles):
    edge1, edge2 = triangles[:,1]-triangles[:,0], triangles[:,2]-triangles[:,0]
    h = np.cross(np.broadcast_to(direction, edge2.shape), edge2)
    divisor = np.einsum('ij,ij->i', edge1, h)
    valid = np.abs(divisor) > 1e-12
    inverse = np.divide(1, divisor, where=valid, out=np.zeros_like(divisor))
    delta = origin-triangles[:,0]
    u = inverse*np.einsum('ij,ij->i', delta,h)
    q = np.cross(delta,edge1)
    v = inverse*(q @ direction)
    distance = inverse*np.einsum('ij,ij->i',edge2,q)
    valid &= (u>=-1e-9)&(v>=-1e-9)&(u+v<=1+1e-9)&(distance>=0)
    if not np.any(valid):
        raise ValueError('Measured section ray missed')
    return origin+direction*np.min(distance[valid])


def nearest(probes, triangles):
    a,b,c = triangles[:,0],triangles[:,1],triangles[:,2]
    ab,ac = b-a,c-a
    raw_normal = np.cross(ab,ac)
    normal = raw_normal / np.linalg.norm(raw_normal,axis=1)[:,None]
    dot00, dot01, dot11 = np.sum(ab*ab,axis=1),np.sum(ab*ac,axis=1),np.sum(ac*ac,axis=1)
    denominator = dot00*dot11-dot01*dot01
    result = []
    for probe in probes:
        delta = probe-a
        distance = np.sum(delta*normal,axis=1)
        plane = probe-distance[:,None]*normal
        direction = plane-a
        dot20,dot21 = np.sum(direction*ab,axis=1),np.sum(direction*ac,axis=1)
        u=(dot11*dot20-dot01*dot21)/denominator
        v=(dot00*dot21-dot01*dot20)/denominator
        inside=(u>=0)&(v>=0)&(u+v<=1)
        candidates = []
        candidates.append(np.where(inside[:,None],plane,np.inf))
        for start,end in ((a,b),(b,c),(c,a)):
            edge=end-start
            fraction=np.clip(np.sum((probe-start)*edge,axis=1)/np.sum(edge*edge,axis=1),0,1)
            candidates.append(start+fraction[:,None]*edge)
        candidate=np.stack(candidates)
        d2=np.sum((candidate-probe)**2,axis=2)
        kind,triangle=np.unravel_index(np.argmin(d2),d2.shape)
        point=candidate[kind,triangle]
        value=math.sqrt(float(d2[kind,triangle]))
        result.append(value if (probe-point)@normal[triangle]>=0 else -value)
    return np.array(result)


results=[]
for group in groups.values():
    indices=sorted({int(i) for face in group for i in face})
    points=vertices[indices]
    lo,hi=points.min(axis=0),points.max(axis=0)
    center=(lo+hi)/2
    if not (abs(center[0])<.6 and .5<center[2]<.8 and hi[0]-lo[0]<.7):
        continue
    triangles=vertices[np.array(group)]
    center=points.mean(axis=0)
    _,axes=np.linalg.eigh(np.cov((points-center).T))
    horizontal=axes[:,np.argmax(abs(axes[0]))]
    horizontal*=1 if horizontal[0]>0 else -1
    vertical=axes[:,np.argmax(abs(axes[2]))]
    vertical*=1 if vertical[2]>0 else -1
    normal=np.cross(horizontal,vertical);normal/=np.linalg.norm(normal)
    half=[max(abs((points-center)@axis)) for axis in (horizontal,vertical,normal)]
    if '--material-profile' in sys.argv:
        builder=Path(__file__).resolve().parents[2]/'Tools/AssetPipeline/build_wordbound_original_pilot.py'
        function=next(node for node in ast.parse(builder.read_text(encoding='utf-8')).body if isinstance(node,ast.FunctionDef) and node.name=='seam_profile_metrics')
        namespace={'np':np,'math':math}
        exec(compile(ast.Module(body=[function],type_ignores=[]),str(builder),'exec'),namespace)
        metrics=namespace['seam_profile_metrics']()
        combined=metrics['maximum_profile_gradient']+max(abs(metrics['minimum_equivalent_relief_metres']),abs(metrics['maximum_equivalent_relief_metres']))*1.5/(half[2]*.3)
        tilt=math.degrees(math.atan(combined))
        results.append({'side':'left' if center[0]<0 else 'right','source_triangles':len(triangles),
                        'half_depth_metres':half[2],'profile':metrics,'conservative_combined_tilt_degrees':tilt,
                        'material_profile_pass':bool(tilt<=6),'scope':'Numerical intended height profile and front fade, not shader execution or mesh contact'})
        continue
    plane_center=center+normal*(half[2]*.55)
    reach=max(half[:2])*2+.02
    section=[]
    for i in range(256):
        angle=math.tau*i/256
        radial=horizontal*(half[0]*math.cos(angle))+vertical*(half[1]*math.sin(angle))
        radial/=np.linalg.norm(radial)
        section.append(ray(plane_center+radial*reach,-radial,triangles))
    section=np.array(section)
    projected=np.c_[(section-plane_center)@horizontal,(section-plane_center)@vertical]
    offset=(projected.min(axis=0)+projected.max(axis=0))/2
    projected-=offset;plane_center+=horizontal*offset[0]+vertical*offset[1]
    width,height=np.abs(projected).max(axis=0)
    best_error=float('inf')
    for r in np.linspace(.002,min(width,height)*.95,600):
        delta=np.abs(projected)-np.array((width-r,height-r))
        residual=np.linalg.norm(np.maximum(delta,0),axis=1)+np.minimum(np.maximum(delta[:,0],delta[:,1]),0)-r
        error=np.mean(residual*residual)
        if error<best_error:
            best_radius,best_error,best_residual=float(r),float(error),float(max(abs(residual)))
    radius=.0013
    corners=((width-best_radius,height-best_radius,0),(-width+best_radius,height-best_radius,90),
             (-width+best_radius,-height+best_radius,180),(width-best_radius,-height+best_radius,270))
    values=[]
    for index,(x,z,start) in enumerate(corners):
        for i in range(65):
            angle=math.radians(start+i*90/64)
            outward=horizontal*math.cos(angle)+vertical*math.sin(angle)
            values.append(plane_center+horizontal*x+vertical*z+outward*(best_radius+radius*.30))
        end=values[-1]
        nx,nz,nstart=corners[(index+1)%4]
        next_angle=math.radians(nstart)
        next_outward=horizontal*math.cos(next_angle)+vertical*math.sin(next_angle)
        next_start=plane_center+horizontal*nx+vertical*nz+next_outward*(best_radius+radius*.30)
        for i in range(1,64):
            values.append(end+(next_start-end)*(i/64))
    values=np.array(values,dtype=np.float32).astype(float)
    probes=[]
    for i,p in enumerate(values):
        for fraction in (0,.25,.5,.75):
            probes.append(p+(values[(i+1)%len(values)]-p)*fraction)
    distances=nearest(np.array(probes),triangles)
    directions=np.roll(values,-1,axis=0)-values
    directions/=np.linalg.norm(directions,axis=1)[:,None]
    turns=np.degrees(np.arccos(np.clip(np.sum(directions*np.roll(directions,1,axis=0),axis=1),-1,1)))
    results.append({'side':'left' if center[0]<0 else 'right','source_triangles':len(triangles),
                    'corner_radius_m':best_radius,'max_section_fit_residual_m':best_residual,
                    'points':len(values),'probes':len(probes),'max_turn_degrees':float(max(turns)),
                    'min_signed_distance_m':float(min(distances)),'max_signed_distance_m':float(max(distances)),
                    'max_absolute_distance_m':float(max(abs(distances))),'allowed_distance_m':radius*.75,
                    'contact_pass':bool(max(abs(distances))<=radius*.75),'turn_pass':bool(max(turns)<=1.5)})
method = 'Direct GLB dimensions plus analytical material profile/front-fade bound; no Blender/shader execution' if '--material-profile' in sys.argv else 'Direct GLB triangles, vectorized ray/closest-triangle math, float32 emitted points; no Blender'
print(json.dumps({'source_sha256':hashlib.sha256(data).hexdigest(),'method':method,'results':results},indent=2))
if len(results)!=2 or any(not r['material_profile_pass'] if '--material-profile' in sys.argv else not r['contact_pass'] or not r['turn_pass'] for r in results):
    sys.exit(1)

from pathlib import Path
import struct, zlib, json, hashlib, statistics

folder = Path(__file__).resolve().parent
run = folder / '20261026T114839701Z-4992eb60'
controls = folder.parent / 'portrait-controls/capture-04'

def read_png(path):
    data = path.read_bytes(); at = 8; chunks = []; compressed = bytearray()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    while at < len(data):
        size = struct.unpack('>I', data[at:at+4])[0]; tag = data[at+4:at+8]; value = data[at+8:at+8+size]; at += size + 12
        chunks.append(tag.decode())
        if tag == b'IHDR': w,h,depth,color,_,_,interlace = struct.unpack('>IIBBBBB', value)
        if tag == b'IDAT': compressed.extend(value)
    assert depth == 8 and color in (2,6) and interlace == 0
    count = 3 if color == 2 else 4; stride = w * count; raw = zlib.decompress(compressed); previous = bytearray(stride); pixels = []; at=0
    def paeth(a,b,c):
        p=a+b-c; x=abs(p-a); y=abs(p-b); z=abs(p-c)
        return a if x<=y and x<=z else b if y<=z else c
    for row in range(h):
        mode=raw[at]; at+=1; current=bytearray(raw[at:at+stride]); at+=stride
        for i in range(stride):
            a=current[i-count] if i>=count else 0; b=previous[i]; c=previous[i-count] if i>=count else 0
            predictor = (0,a,b,(a+b)//2,paeth(a,b,c))[mode]
            current[i]=(current[i]+predictor)&255
        pixels.extend(tuple(current[i:i+3]) for i in range(0,stride,count)); previous=current
    return {'width':w,'height':h,'channels':count,'chunks':chunks,'sha256':hashlib.sha256(data).hexdigest()}, pixels

def region(pixels, bounds):
    left,top,right,bottom=bounds
    return [pixels[y*256+x] for y in range(top,bottom) for x in range(left,right)]

report={'scope':'Read-only PNG decoding and fixed pixel/rectangle comparisons; no image edits, color correction or semantic segmentation.', 'pairs':[]}
for name in ('sunroom','courier','candy','arcade','lantern','winter'):
    oldmeta,old=read_png(run/'Previous'/f'{name}.png'); newmeta,new=read_png(run/'Rendered'/f'{name}.png')
    entry={'id':name,'previous':oldmeta,'rendered':newmeta,'samples':[]}
    for x,y in ((0,0),(128,120),(100,185),(128,200),(100,275)):
        entry['samples'].append({'xy':[x,y],'old':old[y*256+x],'new':new[y*256+x]})
    oldroi=region(old,(92,185,164,190)); newroi=region(new,(92,185,164,190))
    entry['torso_roi_92_185_164_190']={'old_mean_rgb':[round(statistics.mean(p[c] for p in oldroi),2) for c in range(3)],'new_mean_rgb':[round(statistics.mean(p[c] for p in newroi),2) for c in range(3)]}
    report['pairs'].append(entry)
_,current=read_png(run/'Rendered/courier.png')
report['courier_controls']=[]
for name in ('A-original-lit','H-head-recalculate-normals'):
    metadata,pixels=read_png(controls/f'{name}.png')
    bodya=region(pixels,(0,150,256,320)); bodyb=region(current,(0,150,256,320))
    report['courier_controls'].append({'control':name,'metadata':metadata,'full_changed_pixels':sum(a!=b for a,b in zip(pixels,current)),
        'body_y150_plus_changed_pixels':sum(a!=b for a,b in zip(bodya,bodyb)), 'body_pixels':len(bodya),
        'body_max_channel_delta':max(abs(a[c]-b[c]) for a,b in zip(bodya,bodyb) for c in range(3))})
(folder/'pixel-comparison-20261026.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'pairs':[{'id':p['id'],'torso':p['torso_roi_92_185_164_190'],'samples':p['samples']} for p in report['pairs']], 'courier_controls':report['courier_controls']},indent=2))

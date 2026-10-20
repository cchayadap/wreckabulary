import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import crypto from 'node:crypto';


if(process.argv.length!==4)throw Error('Usage: node verify_wordbound_detail.mjs <DetailV2-folder> <original-folder>');
const folder=path.resolve(process.argv[2]), original=path.resolve(process.argv[3]);
if(folder===original)throw Error('The detail revision must remain separate from the original');
const expected=['BAT','BROOM','HAMMER','SPEAR','SHIELD','PLATE','TABLE','SOFA','BED','STOOL','FAN','CLOCK'];
const json=file=>JSON.parse(fs.readFileSync(path.join(folder,file),'utf8'));
const digest=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
const checks=[];
function assert(value,message){if(!value)throw Error(message);}
function file(relative,hash){
  const target=path.resolve(folder,relative);
  assert(target.startsWith(folder+path.sep),`Path outside collection: ${relative}`);
  const bytes=fs.readFileSync(target);
  assert(bytes.length>0,`Empty file: ${relative}`);
  if(hash)assert(digest(bytes)===hash,`Hash mismatch: ${relative}`);
  return bytes;
}
function png(bytes){
  assert(bytes.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10])),'Invalid PNG signature');
  return {width:bytes.readUInt32BE(16),height:bytes.readUInt32BE(20),bitDepth:bytes[24],colorType:bytes[25]};
}
function rgba(bytes){
  const info=png(bytes);
  assert(info.bitDepth===8 && info.colorType===6,'Texture must be 8-bit RGBA');
  assert(bytes[28]===0,'Expected non-interlaced icon');
  const compressed=[];
  for(let position=8;position<bytes.length;){
    const length=bytes.readUInt32BE(position);
    const type=bytes.toString('ascii',position+4,position+8);
    if(type==='IDAT')compressed.push(bytes.subarray(position+8,position+8+length));
    position+=length+12;
  }
  const decoded=zlib.inflateSync(Buffer.concat(compressed));
  const rowBytes=info.width*4;
  assert(decoded.length===(rowBytes+1)*info.height,'PNG scanline length mismatch');
  let previous=Buffer.alloc(rowBytes),transparent=0,visible=0; const pixels=Buffer.alloc(rowBytes*info.height);
  const paeth=(a,b,c)=>{
    const p=a+b-c,pa=Math.abs(p-a),pb=Math.abs(p-b),pc=Math.abs(p-c);
    return pa<=pb&&pa<=pc?a:pb<=pc?b:c;
  };
  for(let y=0;y<info.height;y++){
    const offset=y*(rowBytes+1),filter=decoded[offset],row=Buffer.alloc(rowBytes);
    assert(filter>=0&&filter<=4,'Invalid PNG filter');
    for(let i=0;i<rowBytes;i++){
      const a=i>=4?row[i-4]:0,b=previous[i],c=i>=4?previous[i-4]:0;
      const predictor=[0,a,b,Math.floor((a+b)/2),paeth(a,b,c)][filter];
      row[i]=(decoded[offset+1+i]+predictor)&255;
    }
    for(let i=3;i<rowBytes;i+=4){
      if(row[i]===0)transparent++;
      if(row[i]>0)visible++;
    }
    row.copy(pixels,y*rowBytes); previous=row;
  }
  return {...info,pixels,transparentPixels:transparent,visiblePixels:visible};
}

const prompts=json('prompts.json'),provenance=json('provenance.json');
const manifest=json('manifest.json'),verification=json('export-verification.json');
const prior=JSON.parse(fs.readFileSync(path.join(original,'manifest.json'),'utf8'));
assert(manifest.revision==='DetailV2' && manifest.prior_source_sha256===prior.source.sha256,'Wrong original revision binding');
assert(digest(fs.readFileSync(path.join(original,prior.source.path)))===prior.source.sha256,'Original source changed');
for(const name of ['prompts.json','provenance.json'])assert(file(name).equals(fs.readFileSync(path.join(original,name))),`${name} changed`);
assert(prompts.entries.length===12 && new Set(prompts.entries.map(entry=>entry.id)).size===12,'Expected twelve unique prompts');
assert(provenance.images.length===12 && new Set(provenance.images.map(entry=>entry.id)).size===12,'Expected twelve unique generated concepts');
assert(manifest.assets.length===12 && new Set(manifest.assets.map(entry=>entry.id)).size===12,'Expected twelve unique models');
assert(verification.passed && verification.checks.length===24,'Expected 24 passing Blender export checks');
const source=file(manifest.source.path,manifest.source.sha256);
const sourceData=source.readUInt32LE(0)===0xFD2FB528?zlib.zstdDecompressSync(source):source;
assert(sourceData.toString('ascii',0,7)==='BLENDER','Editable source is not a native blend file');
const atlas=png(file(manifest.palette.path,manifest.palette.sha256));
assert(atlas.width===512&&atlas.height===256,'Detail atlas dimensions differ');
assert(manifest.material_contract?.base_color==='sRGB' && manifest.material_contract?.normal==='linear tangent-space +Y','Color/normal contract differs');
const maps={};
for(const [key,name] of Object.entries({normal:'Normal',mask:'Mask',roughness:'Roughness'})){
  const record=manifest.detail_maps?.[key];
  assert(record?.path===`Exports/WordboundPvP${name}.png`,`${key} path differs`);
  maps[key]=rgba(file(record.path,record.sha256));
  assert(maps[key].width===atlas.width&&maps[key].height===atlas.height,`${key} dimensions differ`);
}
const roughnessRanges=Array.from({length:4},()=>[255,0]);
for(let i=0;i<maps.mask.pixels.length;i+=4){
  const mask=maps.mask.pixels,rough=maps.roughness.pixels,normal=maps.normal.pixels;
  assert(mask[i]===0&&mask[i+1]===0&&mask[i+2]===0,'Paint mask must remain nonmetallic');
  assert(rough[i]===rough[i+1]&&rough[i]===rough[i+2]&&rough[i+3]===255,'Roughness must be opaque grayscale');
  assert(Math.abs(mask[i+3]+rough[i]-255)<=2,'Unity smoothness alpha differs from 1 - Blender roughness');
  assert(normal[i+2]>=230&&normal[i+3]===255,'Tangent normal is not outward facing/opaque');
  const row=Math.floor(i/4/atlas.width/64),range=roughnessRanges[row];
  range[0]=Math.min(range[0],rough[i]); range[1]=Math.max(range[1],rough[i]);
}
for(const [row,[minimum,maximum]] of roughnessRanges.entries()){
  assert(maximum>minimum,`Surface ${row} has no roughness detail`);
  const expected=[[.34,.42],[.18,.24],[.79,.87],[.38,.52]][row];
  assert(minimum>=expected[0]*255-2&&maximum<=expected[1]*255+2,`Surface ${row} has incorrect linear roughness`);
}
for(const id of expected){
  const prompt=prompts.entries.find(entry=>entry.id===id);
  const image=provenance.images.find(entry=>entry.id===id);
  const model=manifest.assets.find(entry=>entry.id===id);
  assert(prompt&&image&&model,`Missing asset ${id}`);
  const concept=file(image.path,image.sha256),conceptInfo=png(concept);
  assert(concept.equals(fs.readFileSync(path.join(original,image.path))),`${id} original concept bytes changed`);
  const oldModel=prior.assets.find(entry=>entry.id===id);
  assert(oldModel && JSON.stringify(oldModel.size_unity_metres)===JSON.stringify(model.size_unity_metres)&&
    JSON.stringify(oldModel.grip_unity_metres)===JSON.stringify(model.grip_unity_metres)&&oldModel.held_scale===model.held_scale,`${id} original item binding changed`);
  assert(conceptInfo.width===image.width&&conceptInfo.height===image.height,`${id} concept dimension mismatch`);
  assert(image.copiedWithoutModification && image.inspection.startsWith('Inspected:'),`${id} concept not reviewed`);
  assert(digest(concept)===model.concept_sha256,`${id} model references another concept`);
  assert(model.triangles>0&&model.triangles<=6000,`${id} triangle budget`);
  assert(model.materials.length<=2&&model.has_uv,`${id} material/UV contract`);
  assert(JSON.stringify(model.size_unity_metres)===JSON.stringify(prompt.sizeUnityMetres),`${id} changed dimensions`);
  assert(JSON.stringify(model.grip_unity_metres)===JSON.stringify(prompt.gripUnityMetres),`${id} changed grip`);
  assert(model.held_scale===prompt.heldScale,`${id} changed held scale`);
  const fbx=file(model.exports.fbx.path,model.exports.fbx.sha256);
  assert(fbx.toString('ascii',0,18)==='Kaydara FBX Binary',`${id} invalid FBX header`);
  const glb=file(model.exports.glb.path,model.exports.glb.sha256);
  assert(glb.toString('ascii',0,4)==='glTF'&&glb.readUInt32LE(4)===2&&glb.readUInt32LE(8)===glb.length,`${id} invalid GLB header`);
  assert(model.previews?.length===4,`${id} missing preview set`);
  for(const preview of model.previews)png(file(preview.path,preview.sha256));
  const iconRecord=model.previews.find(preview=>preview.view==='icon');
  assert(iconRecord,`${id} missing icon`);
  const icon=rgba(file(iconRecord.path,iconRecord.sha256));
  assert(icon.width===512&&icon.height===512&&icon.transparentPixels>0&&icon.visiblePixels>0,`${id} invalid icon alpha`);
  for(const format of ['fbx','glb'])assert(verification.checks.some(check=>check.id===id&&check.format===format&&check.passed),`${id} missing export verdict`);
  checks.push({id,passed:true,triangles:model.triangles,conceptSize:[conceptInfo.width,conceptInfo.height],
    iconTransparentPixels:icon.transparentPixels,iconVisiblePixels:icon.visiblePixels});
}
const report={passed:true,concepts:12,models:12,exports:24,previews:36,icons:12,
  revision:'DetailV2',materialMaps:3,roughnessRangesTopToBottom:roughnessRanges,
  maximumTriangles:Math.max(...checks.map(check=>check.triangles)),checks,
  nativeUnityValidation:'NOT RUN',humanOwnerAcceptance:'NOT RUN'};
fs.writeFileSync(path.join(folder,'delivery-verification.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report,null,2));

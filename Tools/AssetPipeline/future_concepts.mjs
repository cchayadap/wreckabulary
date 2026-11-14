import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';

const stage=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const folder=path.join(stage,'ArtSource/Collections/WordboundPvP/FutureConcepts');
const details={
  ROPE:'One coiled length of thick warm-cream braided household rope with two small sage-wrapped ends and simple muted-terracotta bands holding a loose compact coil. No hooks, carabiners, machinery or other equipment. Keep the two rope ends visible and physically continuous with the coil. Show a believable large simple rope coil that an artist could later model.',
  MAGNET:'One chunky rounded horseshoe magnet, painted muted terracotta with warm-cream blunt pole caps and a small sage grip inset on the curved bridge. Restrained brass-colored seam at the pole caps. No separate metal pieces or attached props. The classic open U-shaped magnet silhouette must read immediately.'
};
function prompt(id,detail){
  return `Use case: stylized-concept. Asset type: individual future game-equipment design study for Wreckabulary Wordbound. Subject: ${detail} This is the future ${id} concept only, not a screenshot or claim of a working game mechanic. Layout: top two-thirds contain three well-separated full-color views of exactly the same selected design, ordered front, side, and three-quarter from left to right. Bottom third contains three small black silhouette alternatives of this same object type, each distinct but recognizable. All views show the complete object, with no cropping or overlap. The three full-color views must agree in geometry, component count, materials and proportions. Style: cozy tactile toy-house miniature for plush roommates, rounded bevels, broad simple forms, clean readable silhouette, restrained crafted detail. Palette: honey-oak wood, warm cream ceramic or cloth, sage painted accents, muted terracotta accents and restrained brass. Neutral ivory background, soft warm overhead-left studio lighting, no floor scene. No text, letters, numbers, labels, logos, watermarks, characters, hands, visual effects, magnetic field lines, mechanics diagrams or unrelated props.`;
}
const [command,id,source,inspection]=process.argv.slice(2);
if(command==='init'){
  fs.mkdirSync(folder,{recursive:true});
  const entries=Object.entries(details).map(([id,detail])=>({id,prompt:prompt(id,detail),tool:'built-in image_gen',transparentBackground:false,destination:`${id}-concept-v1.png`,status:'future-concept-only'}));
  fs.writeFileSync(path.join(folder,'prompts.json'),JSON.stringify({collection:'WordboundPvP FutureConcepts',date:'2026-10-20',entries},null,2)+'\n');
  if(!fs.existsSync(path.join(folder,'provenance.json')))fs.writeFileSync(path.join(folder,'provenance.json'),JSON.stringify({status:'in-progress',images:[]},null,2)+'\n');
  fs.writeFileSync(path.join(folder,'README.md'),'# Future equipment concepts\n\nROPE and MAGNET are later design studies only. No working mechanic, model, Unity prefab or catalogue entry is delivered here. These concepts are excluded from the actual twelve-asset production manifest and delivery validator.\n\nGenerated with the built-in OpenAI image tool, one call per item. Exact prompts and the original file hashes are recorded alongside untouched generated PNGs. Human owner acceptance is pending.\n');
  console.log(JSON.stringify({folder,entries:entries.length}));
}else if(command==='prompt'){
  if(!details[id])throw Error('Unknown future item');
  console.log(prompt(id,details[id]));
}else if(command==='record'){
  if(!details[id])throw Error('Unknown future item');
  const bytes=fs.readFileSync(source);
  if(!bytes.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10])))throw Error('Expected original PNG');
  const destination=path.join(folder,`${id}-concept-v1.png`);
  if(fs.existsSync(destination)&&!fs.readFileSync(destination).equals(bytes))throw Error('Refusing to replace original concept');
  fs.copyFileSync(source,destination);
  const row={id,tool:'built-in image_gen',date:'2026-10-20',sourcePath:path.resolve(source),path:`${id}-concept-v1.png`,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),bytes:bytes.length,width:bytes.readUInt32BE(16),height:bytes.readUInt32BE(20),copiedWithoutModification:true,status:'future-concept-only',inspection:inspection??'Pending visual inspection'};
  const data=JSON.parse(fs.readFileSync(path.join(folder,'provenance.json'),'utf8'));
  data.images=data.images.filter(image=>image.id!==id).concat(row);
  data.status=data.images.length===2?'two-future-concepts-generated':'in-progress';
  fs.writeFileSync(path.join(folder,'provenance.json'),JSON.stringify(data,null,2)+'\n');
  console.log(JSON.stringify({id,path:destination,sha256:row.sha256}));
}else throw Error('Use init, prompt ITEM, or record ITEM SOURCE INSPECTION');

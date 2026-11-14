import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';

const stage = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const folder = path.join(stage, 'ArtSource/Collections/WordboundPvP');
const specifications = [
  ['BAT', 'One chunky wooden baseball bat, an elongated rounded honey-oak barrel tapering into a cream cloth-wrapped lower handle with a softly rounded knob; simple sage collar above the grip. Its upper barrel remains plain and readable.', [.172,1.018,.172], [0,.17,0], .5894],
  ['BROOM', 'One household push broom with a long honey-oak handle, broad horizontal muted-terracotta brush head and dense short cream bristles; a sage binding at the handle socket. Broad sweep silhouette, practical proportions.', [.3,1.0212,.095], [0,.6212,0], .5876],
  ['HAMMER', 'One short chunky toy-house mallet, a broad horizontal cream padded striking head capped with restrained brass plates, honey-oak handle and sage wrapped lower grip. Rounded blunt ends, clear heavy striking silhouette.', [.345,.635,.175], [0,.13,0], .5669],
  ['SPEAR', 'One slim long toy spear with a honey-oak shaft, cream wrapped mid grip, a small sage collar and a broad blunt cream-and-brass toy arrowhead. Soft rounded edges, no realistic blade or dangerous detail.', [.17,1.235,.068], [0,.465,0], .4858],
  ['SHIELD', 'One compact toy-house shield, a softly rounded sage face in a thick cream frame, restrained brass edge fittings and a shallow raised geometric diamond inset. Show the rear wooden handle in the side view. Readable solid block surface, no heraldry or text.', [.46,.57,.1877], [0,.29,-.11], .6316],
  ['PLATE', 'One shallow round cream ceramic dinner plate with a softly rolled rim, one restrained sage inner ring and a narrow brass-colored outer accent. Plain generous eating surface, no food, handles or extra decoration; unmistakably a plate.', [.54,.073,.54], [0,.0365,0], .6667],
  ['TABLE', 'One low rectangular honey-oak coffee table with four chunky rounded legs, thick cream-edged tabletop and a useful lower wooden shelf. Small restrained sage corner insets. Stable household furniture, clear open space between legs.', [1.2,.72,.691], [0,.36,0], .3],
  ['SOFA', 'One small two-seat sofa with three clearly separate padded seat/back sections, rounded muted-terracotta upholstery, cream piping and short honey-oak legs. Soft generous armrests, sage accent on the side panels, no loose cushions or accessories.', [1.52,.93,.813], [0,.465,.0165], .2368],
  ['BED', 'One single toy-house bed with a thick cream bounce mattress on a honey-oak frame, simple rounded headboard and footboard, sage top quilt panel and muted-terracotta band at the foot. Built-in padded bounce affordance, no floating effects or exposed springs.', [1.035,.818,1.58], [0,.409,0], .2278],
  ['STOOL', 'One short round wooden stool with a thick cream padded circular seat, sage seat edge, four rounded honey-oak legs and a compact lower brace. Clearly stable and suitable for stepping, no backrest or accessories.', [.52,.466,.52], [0,.233,0], .6923],
  ['FAN', 'One tabletop electric fan with a chunky sage pedestal and cream base, three broad visible sage blades, a simple sparse brass protective grille and cream central hub. Readable directional fan head, small sage control knob, no cord or wind effects.', [.522,.821,.25], [0,.4105,0], .4385],
  ['CLOCK', 'One compact round tabletop clock with a honey-oak outer case, cream dial, two bold dark-sage hands and twelve simple short brass tick marks with no numerals. Shallow body and small integrated rounded feet; readable time-tool silhouette, no bells or effects.', [.48,.48,.122], [0,.24,.031], .75],
];

function prompt(item, detail) {
  return `Use case: stylized-concept. Asset type: individual game-equipment production concept sheet for Wreckabulary Wordbound. Subject: ${detail} Make a clear design sheet for this one object type, ${item}, that an artist can build as an editable low-poly 3D mesh. Layout: top two-thirds contain three well-separated full-color views of exactly the same selected design, ordered front, side, and three-quarter from left to right. Bottom third contains three small black silhouette alternatives of this same object type, each distinct but recognizable. All views show the complete object, with no cropping or overlap. The three full-color views must agree in geometry, component count, materials and proportions. Style: cozy tactile toy-house miniature for plush roommates, rounded bevels, broad simple forms, clean readable silhouette, restrained crafted detail, visually suitable for a 2,000–6,000 triangle game object. Palette: honey-oak wood, warm cream ceramic or cloth, sage painted accents, muted terracotta accents and restrained brass. Neutral ivory background, soft warm overhead-left studio lighting, no floor scene. The design must remain readable at small gameplay scale. No text, letters, numbers, labels, logos, watermarks, characters, hands, visual effects, unrelated props, tiny decorative clutter or photographic weapon realism.`;
}

const [command, item, source, inspection] = process.argv.slice(2);
if (command === 'init') {
  const entries = specifications.map(([id,detail,size,grip,heldScale]) => ({
    id, prompt: prompt(id,detail), requestedTool:'built-in image_gen',
    transparentBackground:false, sizeUnityMetres:size, gripUnityMetres:grip, heldScale,
    destination:`Concepts/${id}-concept-v1.png`
  }));
  fs.writeFileSync(path.join(folder,'prompts.json'), JSON.stringify({collection:'WordboundPvP',date:'2026-10-20',entries},null,2)+'\n');
  if (!fs.existsSync(path.join(folder,'provenance.json'))) fs.writeFileSync(path.join(folder,'provenance.json'), JSON.stringify({collection:'WordboundPvP',status:'in-progress',images:[]},null,2)+'\n');
  console.log(JSON.stringify({initialized:entries.length,folder}));
} else if (command === 'prompt') {
  const data=JSON.parse(fs.readFileSync(path.join(folder,'prompts.json'),'utf8'));
  const entry=data.entries.find(row=>row.id===item);
  if (!entry) throw Error(`Unknown item ${item}`);
  console.log(entry.prompt);
} else if (command === 'record') {
  if (!specifications.some(row=>row[0]===item)) throw Error(`Unknown item ${item}`);
  const bytes=fs.readFileSync(source);
  if (!bytes.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10]))) throw Error('Expected original PNG');
  const destination=path.join(folder,'Concepts',`${item}-concept-v1.png`);
  if (fs.existsSync(destination) && !fs.readFileSync(destination).equals(bytes)) throw Error('Refusing to overwrite prior concept');
  fs.copyFileSync(source,destination);
  const data=JSON.parse(fs.readFileSync(path.join(folder,'provenance.json'),'utf8'));
  const row={id:item,tool:'built-in image_gen',date:'2026-10-20',sourcePath:path.resolve(source),path:`Concepts/${item}-concept-v1.png`,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),bytes:bytes.length,width:bytes.readUInt32BE(16),height:bytes.readUInt32BE(20),bitDepth:bytes[24],colorType:bytes[25],copiedWithoutModification:true,status:'concept-only',inspection:inspection??'Pending visual inspection'};
  data.images=data.images.filter(entry=>entry.id!==item).concat(row).sort((a,b)=>specifications.findIndex(s=>s[0]===a.id)-specifications.findIndex(s=>s[0]===b.id));
  data.status=data.images.length===12?'concepts-generated-meshes-pending':'in-progress';
  fs.writeFileSync(path.join(folder,'provenance.json'),JSON.stringify(data,null,2)+'\n');
  fs.appendFileSync(path.join(folder,'BuildNotes.md'),`\n- ${item}: original generated PNG preserved; ${row.width}×${row.height}; SHA-256 ${row.sha256}. ${row.inspection}\n`);
  console.log(JSON.stringify({id:item,path:destination,sha256:row.sha256,completed:data.images.length,width:row.width,height:row.height}));
} else {
  throw Error('Use init, prompt ITEM, or record ITEM SOURCE INSPECTION');
}

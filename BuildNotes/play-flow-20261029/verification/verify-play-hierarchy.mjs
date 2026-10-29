import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {argumentsFrom,lastGate} from '../../redesign-20261028/windows-audit/verify-windows-delivery.mjs';
import {pngSize} from '../../creator-clarity-20261029/workshop/extra-capture/verify-inspector-windows.mjs';

export const names=['play_practice_house','play_timerun','play_freestyle','play_escape_random','play_create_tools'];
const practice=['Mode Dibs','Mode Duos','Mode MovingOut','Mode MovingDay','Mode Escape','Solo TimeRun','Solo Freestyle'];
const create=['Create house','Create Wordbound','Mode Tutorial'];
const slash=s=>s.replaceAll('\\','/');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
function under(root,value){
 assert.equal(typeof value,'string');const full=path.resolve(root,value), rel=path.relative(root,full);
 assert(rel&&!rel.startsWith('..')&&!path.isAbsolute(rel),'Evidence escapes root');
 let at=root;for(const segment of rel.split(path.sep)){at=path.join(at,segment);if(fs.existsSync(at))assert(!fs.lstatSync(at).isSymbolicLink(),'Linked evidence ancestry');}
 return full;
}
function proof(root,value){
 const full=under(root,value), before=fs.statSync(full);assert(before.isFile()&&before.size>0,'Missing evidence');
 const bytes=fs.readFileSync(full), after=fs.statSync(full);assert.equal(before.size,after.size);assert.equal(before.mtimeMs,after.mtimeMs);
 return{path:slash(path.relative(root,full)),bytes:bytes.length,sha256:crypto.createHash('sha256').update(bytes).digest('hex')};
}
function rebound(root,value){assert(value&&/^[a-f0-9]{64}$/.test(value.sha256));assert.deepEqual(proof(root,value.path),value,'Delegated evidence changed: '+value.path);}
const rect=r=>Array.isArray(r)&&r.length===4&&r.every(Number.isFinite)&&r[2]>0&&r[3]>0;
export function validateHierarchyProbes(p){
 assert.equal(p.play_selection_preserved,true);assert.equal(p.navigation_preferences_unchanged,true);
 assert.deepEqual(p.hierarchy_owned_stack_cleanup,{errors:[],failure:'none',work_budget_seconds:40,outer_budget_seconds:45});
 for(const key of ['practice','TimeRun','Freestyle','create']){
  const data=p['hierarchy_'+key];assert(data,'Missing hierarchy state: '+key);
  assert.deepEqual(data.targets,key==='create'?create:practice);
  assert.deepEqual(data.queue_captions,['Practice (Offline)','Matchmaking','Create']);
  assert.equal(data.obsolete_family_controls,0);
  assert(rect(data.mode_row)&&rect(data.queue_row)&&rect(data.panel),'Invalid hierarchy geometry');
  assert(data.mode_row[1]+data.mode_row[3]<=data.queue_row[1]+2,'Modes are above category row');
  assert(Math.abs(data.mode_row[0]+data.mode_row[2]/2-data.panel[0]-data.panel[2]/2)<=12,'Modes not centered');
  assert.equal(data.selected,key==='practice'?'Mode Dibs':key==='create'?'Create Wordbound':'Solo '+key);
  assert(Array.isArray(data.captions)&&data.captions.length===data.targets.length&&data.captions.every(c=>typeof c==='string'&&c.length>0));
  if(key!=='create')assert.deepEqual(data.captions.slice(-2),['Time Trial','Freeplay']);
 }
 assert(Array.isArray(p.house_maps)&&p.house_maps.length>0&&p.house_maps.every(id=>id.startsWith('Map ')));
 assert.deepEqual(p.solo_maps_TimeRun,p.solo_maps_Freestyle);
 assert.deepEqual(p.solo_maps_TimeRun,['Solo map LostAddress','Solo map ParcelHall','Solo map ParcelService']);
 let reference;
 for(const key of ['house','TimeRun','Freestyle','Escape','Create']){
  const maps=p['map_footprints_'+key];assert(Array.isArray(maps)&&maps.length>0,'Missing map footprints: '+key);
  if(key==='house')assert.deepEqual(maps.map(m=>m.id),p.house_maps);
  if(key==='TimeRun'||key==='Freestyle')assert.deepEqual(maps.map(m=>m.id).sort(),p['solo_maps_'+key]);
  if(key==='Escape')assert.deepEqual(maps.map(m=>m.id),['Map random']);
  if(key==='Create')assert.deepEqual(maps.map(m=>m.id),['Wordbound maps']);
  for(const map of maps){assert(rect(map.rect)&&rect(map.viewport));assert.equal(map.visible_at_focus,true,'Focused map was not revealed');reference??=map.rect;assert(Math.abs(map.rect[2]-reference[2])<=2&&Math.abs(map.rect[3]-reference[3])<=2,'Map card footprint differs');
   assert(map.rect[0]>=map.viewport[0]-2&&map.rect[1]>=map.viewport[1]-2&&map.rect[0]+map.rect[2]<=map.viewport[0]+map.viewport[2]+2&&map.rect[1]+map.rect[3]<=map.viewport[1]+map.viewport[3]+2,'Focused map outside viewport');}
 }
}
export async function verifyPlayHierarchy(options){
 const keys=['root','build','source-commit','base-audit','run','gate','out'];
 for(const k of keys)assert(options[k],'Missing --'+k);for(const k of Object.keys(options))assert(keys.includes(k),'Unknown --'+k);
 assert(/^\d{8}T\d{6}Z$/.test(options.build));assert(/^[a-f0-9]{40}$/.test(options['source-commit']));
 const root=fs.realpathSync(options.root), out=under(root,options.out), baseFile=under(root,options['base-audit']);assert(!fs.existsSync(out),'Refusing to overwrite evidence');
 const base=read(baseFile), build=base.build;assert.equal(base.schema,2);assert.equal(base.status,'BOUNDED_LOBBY_SOLO_WORKSHOP_WINDOWS_PASS');
 assert.equal(build.id,options.build);assert.equal(build.head,options['source-commit']);assert.equal(build.ok,true);assert.equal(build.kind,'release-qa');assert.equal(build.development,false);
 assert.equal(base.source.commit,build.head);assert.equal(base.source.productionDiffEmpty,true);assert.equal(base.source.untrackedNonIgnoredProductionEmpty,true);
 for(const field of ['verifier','buildReceipt','buildGate','buildLog','executable','packedManifest'])rebound(root,base[field]);
 const git=(...args)=>execFileSync('git',args,{cwd:root,encoding:'utf8',windowsHide:true}).trim();
 const roots=['Assets','Packages','ProjectSettings'];assert.equal(git('diff','--name-only',build.head,'--',...roots),'','Production changed after build');assert.equal(git('ls-files','--others','--exclude-standard','--',...roots),'','Untracked production input');
 const dir=under(root,options.run), runId=path.basename(dir), prefix=slash(path.relative(root,dir));assert.equal(prefix,'Docs/qa/runs/'+runId);
 assert(/^\d{8}T\d{6}Z-play_hierarchy$/.test(runId));
 const gateFile=under(root,options.gate), gate=lastGate(gateFile), smoke=gate.smoke;
 assert.equal(gate.rung,'R4');assert.equal(gate.cmd,'smoke --build '+build.id+' --scenario play_hierarchy');
 assert.equal(smoke.run_id,runId);assert.equal(smoke.scenario,'play_hierarchy');assert.equal(slash(smoke.build),slash(build.dir));assert.equal(smoke.kind,build.kind);
 for(const key of ['stripped','timed_out'])assert.equal(smoke[key],false);assert.equal(smoke.exit,0);
 const checksFile=path.join(dir,'checks.json'), checks=read(checksFile);assert(gate.evidence.includes(prefix+'/checks.json'));
 assert.equal(checks.schema,1);assert.equal(checks.run_id,runId);assert.equal(checks.build_id,build.id);assert.equal(checks.scenario,'play_hierarchy');assert.equal(checks.platform,'player');assert.equal(checks.result,'PASS');
 assert.equal(checks.seed,smoke.seed);assert.equal(checks.development_build,false);assert.equal(checks.unity_version,build.unity_version);
 if(checks.commit!==null)assert.equal(checks.commit,build.head);if(checks.tree!==null)assert.equal(checks.tree,build.tree);
 assert(Number.isFinite(Date.parse(checks.started_utc))&&Date.parse(checks.started_utc)>=Date.parse(build.utc));assert(Date.parse(checks.ended_utc)>=Date.parse(checks.started_utc));
 for(const key of ['errors','exceptions','warnings'])assert.equal(checks.console[key],0);assert.equal(checks.console.first_exception,null);
 assert.deepEqual(checks.steps.map(s=>s.name),['centered_play_flow']);const step=checks.steps[0];assert.equal(step.status,'PASS');assert.deepEqual(step.assert_failures,[]);
 validateHierarchyProbes(step.probes);
 assert.deepEqual(step.captures.map(slash),names.map(n=>prefix+'/'+n+'.png'));assert.deepEqual(checks.captures.map(c=>c.name),names);
 assert.deepEqual(checks.captures.map(c=>slash(c.path)),names.map(n=>prefix+'/'+n+'.png'));assert(checks.captures.every(c=>c.kind==='png'&&c.camera==='screen'));
 assert.deepEqual(fs.readdirSync(dir).filter(n=>n.toLowerCase().endsWith('.png')).sort(),names.map(n=>n+'.png').sort());
 let previousFrame=0;const captures=[];
 for(const name of names){const p=step.probes[name+'_capture'];assert.equal(p.focused,true);assert.equal(p.varied_pixels,true);assert(Number.isInteger(p.rendered_frame)&&p.rendered_frame>previousFrame);previousFrame=p.rendered_frame;
  const file=prefix+'/'+name+'.png', size=pngSize(under(root,file));assert.deepEqual(size,{width:p.width,height:p.height});captures.push({...proof(root,file),...size,renderedFrame:p.rendered_frame,focused:true});}
 const logFile=under(root,gate.log), log=fs.readFileSync(logFile,'utf8');assert.equal(slash(path.relative(root,logFile)),'Logs/player/'+runId+'.log');assert(log.includes('[GSK] QA play_hierarchy PASS'));assert(!/^(?:Crash!!!|Fatal error|Unhandled Exception|[A-Za-z]+Exception:)/m.test(log));
 const preservationFile=under(root,base.preservation.receipt.path), preservation=read(preservationFile);rebound(root,base.preservation.receipt);assert.equal(preservation.status,'PASS');assert.deepEqual(preservation.changed,[]);assert(Date.parse(preservation.utc)>=Date.parse(checks.ended_utc),'Final preservation must follow this scenario too');
 const receipt={schema:1,utc:new Date().toISOString(),status:'BOUNDED_PLAY_HIERARCHY_WINDOWS_PASS',build,source:{commit:build.head,currentHead:git('rev-parse','HEAD'),productionDiffEmpty:true,untrackedNonIgnoredProductionEmpty:true},verifier:proof(root,fileURLToPath(import.meta.url)),delegatedBaseAudit:proof(root,baseFile),pngVerifier:proof(root,'BuildNotes/creator-clarity-20261029/workshop/extra-capture/verify-inspector-windows.mjs'),scenarioSources:['LobbyExperienceQa.cs','WordboundSoloQa.cs'].map(n=>proof(root,'Assets/_Project/Scripts/WordboundQa/'+n)),run:{id:runId,scenario:'play_hierarchy',checks:proof(root,checksFile),gate:proof(root,gateFile),log:proof(root,logFile),steps:checks.steps,captures},preservation:base.preservation,scope:{captures:5,steps:1,visualAcceptance:'PENDING direct inspection of these exact full screenshots',limits:['Logical UI Submit selection and measured card/row geometry; no physical keyboard/controller or mouse-input acceptance.','Existing45-image audit delegates exact build/executable/packed-inventory/source preservation and original flow evidence.','No real public/private matchmaking, House networking, map completion, BELT/audio implementation, performance or production-release acceptance.']}};
 assert.equal(git('diff','--name-only',build.head,'--',...roots),'');assert.equal(git('ls-files','--others','--exclude-standard','--',...roots),'');
 fs.writeFileSync(out,JSON.stringify(receipt,null,2)+'\n',{flag:'wx'});return{status:receipt.status,build:build.id,captures:5,output:slash(path.relative(root,out))};
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 try{console.log(JSON.stringify(await verifyPlayHierarchy(argumentsFrom(process.argv.slice(2)))));}catch(error){console.error('Play hierarchy evidence rejected: '+error.message);process.exitCode=1;}
}

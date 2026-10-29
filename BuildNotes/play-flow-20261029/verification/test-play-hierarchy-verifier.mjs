import assert from 'node:assert/strict';
import fs from 'node:fs';
import {validateHierarchyProbes} from './verify-play-hierarchy.mjs';
const practice=['Mode Dibs','Mode Duos','Mode MovingOut','Mode MovingDay','Mode Escape','Solo TimeRun','Solo Freestyle'];
const create=['Create house','Create Wordbound','Mode Tutorial'];
const solo=['Solo map LostAddress','Solo map ParcelHall','Solo map ParcelService'];
const probes={play_selection_preserved:true,navigation_preferences_unchanged:true,hierarchy_owned_stack_cleanup:{errors:[],failure:'none',work_budget_seconds:40,outer_budget_seconds:45},house_maps:['Map flat'],solo_maps_TimeRun:solo,solo_maps_Freestyle:solo};
for(const key of ['practice','TimeRun','Freestyle','create'])probes['hierarchy_'+key]={targets:key==='create'?create:practice,captions:key==='create'?['Home builder','Map editor','Tutorial']:['Dibs','Duos','Moving Out','Moving Day','Escape','Time Trial','Freeplay'],queue_captions:['Practice (Offline)','Matchmaking','Create'],obsolete_family_controls:0,selected:key==='practice'?'Mode Dibs':key==='create'?'Create Wordbound':'Solo '+key,mode_row:[100,450,800,100],queue_row:[100,600,800,50],panel:[100,100,800,600]};
for(const [key,ids] of [['house',['Map flat']],['TimeRun',solo],['Freestyle',solo],['Escape',['Map random']],['Create',['Wordbound maps']]])probes['map_footprints_'+key]=ids.map((id,index)=>({id,rect:[100+index*210,110,200,300],viewport:[100,100,800,400],visible_at_focus:true}));
validateHierarchyProbes(probes);
const cases=[
 ['selection changed',p=>p.play_selection_preserved=false],
 ['old queue caption',p=>p.hierarchy_practice.queue_captions[0]='Local'],
 ['obsolete family control',p=>p.hierarchy_practice.obsolete_family_controls=1],
 ['off-center mode row',p=>p.hierarchy_TimeRun.mode_row[0]+=20],
 ['mode above categories',p=>p.hierarchy_practice.mode_row[1]=610],
 ['wrong focused mode',p=>p.hierarchy_Freestyle.selected='Solo TimeRun'],
 ['different mode map catalogue',p=>p.solo_maps_Freestyle=['Solo map LostAddress']],
 ['non-uniform card',p=>p.map_footprints_Create[0].rect[2]+=8],
 ['clipped focused card',p=>p.map_footprints_Create[0].rect[1]=90],
 ['cleanup failure',p=>p.hierarchy_owned_stack_cleanup.errors=['failed']]
];
for(const [name,mutate] of cases){const copy=structuredClone(probes);mutate(copy);assert.throws(()=>validateHierarchyProbes(copy),undefined,name);}
const receipt={utc:new Date().toISOString(),status:'PASS',kind:'Synthetic probe-contract rejection checks only; no Windows/game/UI acceptance claim',positive:1,rejectedFaults:cases.map(([name])=>name)};
fs.writeFileSync(new URL('./verifier-contract-tests.json',import.meta.url),JSON.stringify(receipt,null,2)+'\n');console.log(JSON.stringify(receipt));

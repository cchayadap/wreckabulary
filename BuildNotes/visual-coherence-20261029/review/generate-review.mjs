import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '../../..');
const spec = {
  lobby: ['lobby_experience', 24], solo: ['wordbound_solo', 13], workshop: ['wordbound_workshop_flow', 8],
  parcel: ['wordbound_parcel_service', 9], 'parcel-workshop': ['wordbound_parcel_workshop', 9]
};
const previewKeys = ['before-map', 'after-map'];
const required = ['build-id', ...Object.keys(spec), ...previewKeys, 'out'];
const { values: args } = parseArgs({ options: Object.fromEntries([
  ...required.map(key => [key, { type: 'string' }]), ['help', { type: 'boolean' }]
]) });
if (args.help) {
  console.log('node generate-review.mjs --build-id BUILD --lobby RUN --solo RUN --workshop RUN --parcel RUN --parcel-workshop RUN --before-map CAPTURE --after-map CAPTURE --out NAME/REVIEW.html\nAll IDs are exact. Runs resolve beneath Docs/qa/runs; previews beneath map-previews/captures. Before/current Parcel Service previews must share the exact course, seed, mode and camera pose. Output resolves beneath this review folder and must be new. Original screenshots are linked, never changed.');
  process.exit(0);
}
for (const key of required) if (!args[key]) throw new Error(`Missing --${key}.`);
if (!/^\d{8}T\d{6}Z$/.test(args['build-id'])) throw new Error('Invalid exact build ID.');
const output = path.resolve(here, args.out);
const inside = (root, value) => {
  const relative = path.relative(root, value);
  return relative !== '' && relative !== '..' && !relative.startsWith('..' + path.sep) && !path.isAbsolute(relative);
};
if (!inside(here, output) || path.basename(output) !== 'REVIEW.html') throw new Error('Choose a fresh NAME/REVIEW.html within this review folder.');
const manifestPath = path.join(path.dirname(output), 'review-manifest.json');
const hash = data => createHash('sha256').update(data).digest('hex');
const esc = value => String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const href = value => esc(pathToFileURL(value).href);
const same = (a, b) => path.resolve(a).toLowerCase() === path.resolve(b).toLowerCase();
async function fresh(filename) {
  try { await fs.lstat(filename); throw new Error(`Refusing existing output: ${filename}`); }
  catch (error) { if (error.code !== 'ENOENT') throw error; }
}
await fresh(output); await fresh(manifestPath);
async function read(filename) {
  const real = await fs.realpath(filename);
  if (!(await fs.stat(real)).isFile()) throw new Error(`Expected file: ${filename}`);
  const data = await fs.readFile(real);
  return { path: real, bytes: data.length, sha256: hash(data), data };
}
const fact = ({ data, ...rest }) => rest;
async function png(filename) {
  const value = await read(filename), bytes = value.data;
  if (bytes.length < 24 || !bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])) || bytes.toString('ascii', 12, 16) !== 'IHDR')
    throw new Error(`Invalid original PNG: ${filename}`);
  const width = bytes.readUInt32BE(16), height = bytes.readUInt32BE(20);
  if (width < 1 || height < 1) throw new Error(`Invalid PNG dimensions: ${filename}`);
  return { ...fact(value), width, height };
}
async function loadRun(key) {
  const [scenario, count] = spec[key], id = args[key];
  if (!new RegExp(`^\\d{8}T\\d{6}Z-${scenario}$`).test(id)) throw new Error(`Invalid ${key} run ID.`);
  const root = await fs.realpath(path.join(project, 'Docs/qa/runs'));
  const folder = await fs.realpath(path.join(root, id));
  if (!inside(root, folder)) throw new Error(`Run leaves QA root: ${id}`);
  const source = await read(path.join(folder, 'checks.json')), checks = JSON.parse(source.data);
  const start = Date.parse(checks.started_utc), end = Date.parse(checks.ended_utc);
  if (checks.run_id !== id || checks.build_id !== args['build-id'] || checks.scenario !== scenario || checks.platform !== 'player' ||
      checks.result !== 'PASS' || !Number.isFinite(start) || !Number.isFinite(end) || end < start ||
      !checks.steps?.length || checks.steps.some(step => step.status !== 'PASS' || step.assert_failures?.length) ||
      checks.console?.errors !== 0 || checks.console?.exceptions !== 0 || checks.captures?.length !== count)
    throw new Error(`Expected complete matching player PASS with ${count} PNGs and no errors/exceptions: ${id}`);
  const images = [], seen = new Set();
  for (const capture of checks.captures) {
    if (capture.kind !== 'png' || capture.camera !== 'screen' || !/^[A-Za-z0-9_-]+$/.test(capture.name) || seen.has(capture.name))
      throw new Error(`Invalid or duplicate declared screen capture: ${id}`);
    seen.add(capture.name);
    const image = await png(path.resolve(project, capture.path));
    if (!same(image.path, path.join(folder, capture.name + '.png'))) throw new Error(`Capture does not belong to its exact run: ${image.path}`);
    images.push({ name: capture.name, ...image });
  }
  const actual = (await fs.readdir(folder)).filter(name => name.toLowerCase().endsWith('.png')).sort();
  if (JSON.stringify(actual) !== JSON.stringify(images.map(image => image.name + '.png').sort())) throw new Error(`Declared/actual PNG roster mismatch: ${id}`);
  for (const step of checks.steps) for (const filename of step.captures ?? []) {
    const real = await fs.realpath(path.resolve(project, filename));
    if (!images.some(image => same(image.path, real))) throw new Error(`Step references undeclared PNG: ${filename}`);
  }
  return { id, build: checks.build_id, commit: checks.commit, tree: checks.tree, scenario, endedUtc: checks.ended_utc,
    console: checks.console, checks: fact(source), images };
}
async function loadPreview(key) {
  const id = args[key];
  if (!/^\d{8}T\d{6}Z-[a-f0-9]{8}$/.test(id)) throw new Error(`Invalid ${key} capture ID.`);
  const root = await fs.realpath(path.resolve(here, '../map-previews/captures'));
  const folder = await fs.realpath(path.join(root, id));
  if (!inside(root, folder)) throw new Error('Preview leaves capture root.');
  const source = await read(path.join(folder, 'receipt.json')), receipt = JSON.parse(source.data);
  if (receipt.status !== 'CAPTURED_NOT_VISUALLY_APPROVED' || !Array.isArray(receipt.errors) || receipt.errors.length ||
      !['filesPreserved', 'memoryPreserved', 'scenesPreserved', 'globalsPreserved', 'tactileReleased', 'sourcePreserved'].every(key => receipt[key] === true))
    throw new Error(`Preview is not a preserved completed candidate: ${id}`);
  const frames = receipt.frames?.filter(frame => frame.filename === 'ParcelService.png');
  const sets = receipt.sets?.filter(set => set.map === 'ParcelService');
  if (frames?.length !== 1 || sets?.length !== 1) throw new Error(`Missing unique Parcel Service preview: ${id}`);
  const set = sets[0], pose = set.pose;
  if (!/^[a-f0-9]{16}$/.test(receipt.seed) || !['TimeRun', 'Freestyle'].includes(receipt.mode) ||
      set.seed !== receipt.seed || set.mode !== receipt.mode ||
      !['sourceHash', 'manifestHash', 'geometryHash', 'catalogueHash'].every(key => /^[a-f0-9]{64}$/.test(set[key])) ||
      !pose || typeof pose.Module !== 'string' || !pose.Module.length ||
      ![pose.Position, pose.Target].every(vector => Array.isArray(vector) && vector.length === 3 && vector.every(Number.isFinite)) ||
      !Number.isFinite(pose.FieldOfView) || pose.FieldOfView <= 0 || pose.FieldOfView >= 180)
    throw new Error(`Missing valid course identity or camera pose: ${id}`);
  const image = await png(path.join(folder, 'ParcelService.png')), frame = frames[0];
  if (!same(image.path, path.join(folder, frame.filename)) || image.sha256 !== frame.sha256 || image.bytes !== frame.bytes ||
      image.width !== frame.width || image.height !== frame.height || image.width !== 960 || image.height !== 540 || frame.windowsGameplay !== false)
    throw new Error(`Preview image/receipt mismatch: ${id}`);
  return { id, receipt: fact(source), scope: receipt.scope, seed: receipt.seed, mode: receipt.mode, set, image };
}
const runs = Object.fromEntries(await Promise.all(Object.keys(spec).map(async key => [key, await loadRun(key)])));
const previews = Object.fromEntries(await Promise.all(previewKeys.map(async key => [key, await loadPreview(key)])));
for (const field of ['seed', 'mode'])
  if (new Set(Object.values(previews).map(preview => preview[field])).size !== 1) throw new Error(`Map comparison differs in ${field}.`);
for (const field of ['sourceHash', 'manifestHash', 'geometryHash', 'catalogueHash'])
  if (new Set(Object.values(previews).map(preview => preview.set[field])).size !== 1) throw new Error(`Map comparison differs in ${field}.`);
if (previews['before-map'].id === previews['after-map'].id) throw new Error('Choose distinct before/current capture receipts.');
if (JSON.stringify(previews['before-map'].set.pose) !== JSON.stringify(previews['after-map'].set.pose))
  throw new Error('Wall detail and lighting comparison must retain the exact before camera pose.');
for (const field of ['commit', 'tree']) {
  const values = Object.values(runs).map(run => run[field]).filter(Boolean);
  if (new Set(values).size > 1) throw new Error(`Fresh runs disagree on their recorded ${field}.`);
}
const tabs = ['Map comparison', 'Home', 'Play', 'Shop', 'Customize', 'Settings', 'Equipment', 'World', 'Workshop'];
function area(name, kind) {
  if (name.includes('settings')) return 'Settings';
  if (name.includes('customize')) return 'Customize';
  if (name.includes('shop')) return 'Shop';
  if (name.endsWith('_menu')) return 'Equipment';
  if (name.endsWith('_world')) return 'World';
  if (name.includes('home') || name.includes('initial_lobby') || name === 'parcel_workshop_lobby') return 'Home';
  if (kind.includes('workshop')) return 'Workshop';
  return 'Play';
}
const entries = Object.entries(runs).flatMap(([kind, run]) => run.images.map(image => ({ kind, run, image, area: area(image.name, kind) })));
const title = name => name.replace(/^lobby_|^wordbound_|^solo_|^parcel_service_|^parcel_workshop_/, '').replaceAll('_', ' ');
function figure(image, label, detail) {
  return `<figure><figcaption><strong>${esc(label)}</strong><small>${esc(detail)}</small></figcaption><a href="${href(image.path)}" target="_blank" rel="noopener"><img src="${href(image.path)}" width="${image.width}" height="${image.height}" loading="lazy" alt="${esc(label)} — original full screenshot"></a></figure>`;
}
const previewLabels = ['Before', 'Current'];
const previewFigures = previewKeys.map((key, i) => figure(previews[key].image, previewLabels[i], 'Actual map preview · ' + previews[key].id)).join('');
const history = [];
for (const [label, relative] of [['Earlier review', '../../play-organization-20261029/review/REVIEW.html'],
  ['Progress', '../../../progress.md'], ['Material review', '../map-composition/material-review/render-review.md']]) {
  const filename = path.resolve(here, relative);
  try { history.push({ label, ...fact(await read(filename)) }); }
  catch (error) { if (error.code !== 'ENOENT') throw error; }
}
const checks = Object.entries(runs).map(([key, run]) => `<a href="${href(run.checks.path)}">${esc(key)} · ${run.images.length} frames</a>`).join('');
const html = `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Wreckabulary · Actual build review</title><style>
:root{color-scheme:dark;--bg:#161b20;--panel:#20272e;--ink:#f1f3f4;--muted:#b1bbc4;--line:#3b4651;--focus:#e7ba76}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:15px/1.5 system-ui,"Segoe UI",sans-serif}header,main,footer,nav{max-width:1600px;margin:auto;padding:18px 24px}h1{font-size:25px;margin:0}p{margin:6px 0;color:var(--muted)}a{color:var(--focus)}.links,.checks{display:flex;gap:10px 20px;flex-wrap:wrap;margin:12px 0}details{font-size:12px;color:var(--muted)}nav{display:flex;gap:5px;flex-wrap:wrap;border-block:1px solid var(--line)}button{font:inherit;border:1px solid transparent;border-radius:5px;padding:8px 12px;background:transparent;color:var(--muted);cursor:pointer}button[aria-selected=true]{border-color:var(--line);background:var(--panel);color:var(--ink)}button:focus-visible,a:focus-visible{outline:3px solid var(--focus);outline-offset:3px}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:22px}.comparison{grid-template-columns:repeat(auto-fit,minmax(min(440px,100%),1fr))}figure{margin:0;min-width:0;background:var(--panel);border:1px solid var(--line);border-radius:6px;overflow:hidden}figcaption{padding:10px 12px;display:flex;justify-content:space-between;gap:8px;flex-wrap:wrap}strong{text-transform:capitalize;font-weight:600}small{font-size:11px;color:var(--muted)}img{display:block;width:100%;height:auto}section>p{margin:0 0 14px}footer{font-size:12px;color:var(--muted);border-top:1px solid var(--line)}[hidden]{display:none!important}@media(max-width:800px){header,main,footer,nav{padding:14px}.grid{grid-template-columns:1fr}h1{font-size:22px}nav{gap:2px}button{padding:7px 10px}}
</style></head><body><header><h1>Wreckabulary review</h1><p>Build ${esc(args['build-id'])} · Review candidate</p><div class="links">${history.map(link => `<a href="${href(link.path)}">${esc(link.label)}</a>`).join('')}<a href="${href(manifestPath)}">Evidence</a></div><details><summary>Capture receipts</summary><div class="checks">${checks}</div></details></header>
<nav role="tablist" aria-label="Review area">${tabs.map((tab, i) => `<button type="button" role="tab" id="tab-${i}" aria-controls="panel-${i}" aria-selected="${i === 0}" tabindex="${i ? -1 : 0}">${esc(tab)}</button>`).join('')}</nav>
<main>${tabs.map((tab, i) => `<section role="tabpanel" id="panel-${i}" aria-labelledby="tab-${i}"${i ? ' hidden' : ''}>${i === 0 ? '<p>Same map, seed and camera. Wall detail and lighting comparison; static previews.</p><div class="grid comparison">' + previewFigures + '</div>' : '<div class="grid">' + entries.filter(entry => entry.area === tab).map(({ image, run, kind }) => figure(image, title(image.name), `${kind} · ${image.width} × ${image.height}`)).join('') + '</div>'}</section>`).join('')}</main>
<footer>All ${entries.length} Windows screenshots and two actual map previews are shown unchanged. Click any image for the full original, including trial labels. Scenario PASS does not establish full gameplay, multiplayer, production readiness or owner visual acceptance. Earlier review files are preserved.</footer>
<script>const tabs=[...document.querySelectorAll('[role=tab]')];function choose(tab,focus=false){for(const item of tabs){const on=item===tab;item.setAttribute('aria-selected',String(on));item.tabIndex=on?0:-1;document.getElementById(item.getAttribute('aria-controls')).hidden=!on}if(focus)tab.focus();history.replaceState(null,'','#'+tab.id)}for(const tab of tabs){tab.onclick=()=>choose(tab);tab.onkeydown=e=>{let i=tabs.indexOf(tab);if(e.key==='ArrowRight')i=(i+1)%tabs.length;else if(e.key==='ArrowLeft')i=(i+tabs.length-1)%tabs.length;else if(e.key==='Home')i=0;else if(e.key==='End')i=tabs.length-1;else return;e.preventDefault();choose(tabs[i],true)}}const selected=tabs.find(tab=>'#'+tab.id===location.hash);if(selected)choose(selected);</script></body></html>`;
const manifest = { schema: 1, status: 'REVIEW_CANDIDATE_NOT_OWNER_ACCEPTED', createdUtc: new Date().toISOString(),
  buildId: args['build-id'], windowsFrames: entries.length, staticMapFrames: previewKeys.length,
  originalImagesUnmodified: true, htmlSha256: hash(Buffer.from(html)), generatorSha256: hash(await fs.readFile(fileURLToPath(import.meta.url))),
  history, runs, previews };
await fs.mkdir(path.dirname(output), { recursive: true });
const realParent = await fs.realpath(path.dirname(output));
if (!inside(await fs.realpath(here), path.join(realParent, path.basename(output)))) throw new Error('Output resolves outside review folder.');
await fs.writeFile(manifestPath, JSON.stringify(manifest, null, 2) + '\n', { flag: 'wx' });
await fs.writeFile(output, html, { flag: 'wx' });
console.log(`Created ${output}; ${entries.length} Windows images + two static map previews. Review candidate, not visually approved.`);

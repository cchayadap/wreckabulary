import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '../../..');
const scenarios = { lobby: 'lobby_experience', solo: 'wordbound_solo', workshop: 'wordbound_workshop_flow' };
const preferred = { build: '20261026T232801Z', lobby: '20261026T233250Z-lobby_experience',
  solo: '20261026T233412Z-wordbound_solo', workshop: '20261026T233042Z-wordbound_workshop_flow' };
const rejected = { build: '20261028T155755Z', lobby: '20261028T155953Z-lobby_experience',
  solo: '20261028T160051Z-wordbound_solo', workshop: '20261028T160232Z-wordbound_workshop_flow' };
const { values: args } = parseArgs({ options: {
  'build-id': { type: 'string' }, lobby: { type: 'string' }, solo: { type: 'string' }, workshop: { type: 'string' },
  'include-rejected': { type: 'boolean' }, exe: { type: 'string' }, out: { type: 'string' }, help: { type: 'boolean' }
}});
if (args.help) {
  console.log('node generate-review.mjs --build-id BUILD --lobby RUN_ID --solo RUN_ID --workshop RUN_ID [--include-rejected] [--exe PATH] [--out PATH/REVIEW.html]\nRun IDs resolve only beneath Docs/qa/runs. All runs must be complete player PASS with matching build/scenario identity. All declared screenshots are included unchanged. Outputs must be new.');
  process.exit(0);
}
for (const key of ['build-id', ...Object.keys(scenarios)]) if (!args[key]) throw new Error(`Missing --${key}.`);
if (!/^\d{8}T\d{6}Z$/.test(args['build-id']) || [preferred.build, rejected.build].includes(args['build-id']))
  throw new Error('Supply the actual new build ID, distinct from both historical builds.');

const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const esc = value => String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const href = value => esc(pathToFileURL(value).href);
const same = (a, b) => path.resolve(a).toLowerCase() === path.resolve(b).toLowerCase();
async function inspectFile(filename) {
  const real = await fs.realpath(filename);
  if (!(await fs.stat(real)).isFile()) throw new Error(`Expected file: ${real}`);
  const bytes = await fs.readFile(real);
  return { path: real, bytes: bytes.length, sha256: sha(bytes), data: bytes };
}
async function run(kind, id, build) {
  if (!new RegExp(`^\\d{8}T\\d{6}Z-${scenarios[kind]}$`).test(id)) throw new Error(`Invalid ${kind} run ID: ${id}`);
  const folder = await fs.realpath(path.join(project, 'Docs/qa/runs', id));
  const source = await inspectFile(path.join(folder, 'checks.json'));
  const checks = JSON.parse(source.data.toString('utf8'));
  const start = Date.parse(checks.started_utc), end = Date.parse(checks.ended_utc);
  if (checks.run_id !== id || checks.build_id !== build || checks.scenario !== scenarios[kind] || checks.platform !== 'player' ||
      checks.result !== 'PASS' || !Number.isFinite(start) || !Number.isFinite(end) || end < start ||
      !checks.steps?.length || checks.steps.some(step => step.status !== 'PASS' || step.assert_failures?.length))
    throw new Error(`Run is not a complete matching player PASS: ${source.path}`);
  if (!checks.captures?.length) throw new Error(`Run declares no screenshots: ${source.path}`);
  const images = {}, paths = new Set();
  for (const capture of checks.captures) {
    if (capture.kind !== 'png' || capture.camera !== 'screen' || !/^[A-Za-z0-9_-]+$/.test(capture.name) || images[capture.name])
      throw new Error(`Expected unique unchanged screen PNG capture: ${id}/${capture.name}`);
    const image = await inspectFile(path.resolve(project, capture.path));
    if (!same(image.path, path.join(folder, capture.name + '.png')) || paths.has(image.path.toLowerCase()))
      throw new Error(`Capture does not uniquely belong to its declared run: ${image.path}`);
    if (image.data.length < 24 || !image.data.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])))
      throw new Error(`Invalid PNG: ${image.path}`);
    const width = image.data.readUInt32BE(16), height = image.data.readUInt32BE(20);
    if (width < 1 || height < 1) throw new Error(`Invalid image size: ${image.path}`);
    paths.add(image.path.toLowerCase());
    images[capture.name] = { path: image.path, bytes: image.bytes, sha256: image.sha256, width, height };
  }
  for (const step of checks.steps) for (const filename of step.captures ?? []) {
    const actual = await fs.realpath(path.resolve(project, filename));
    if (!paths.has(actual.toLowerCase())) throw new Error(`Step references an undeclared screenshot: ${filename}`);
  }
  return { id, build, scenario: checks.scenario, result: checks.result, endedUtc: checks.ended_utc,
    checks: { path: source.path, sha256: source.sha256 }, images };
}
async function version(spec) {
  return Object.fromEntries(await Promise.all(Object.keys(scenarios).map(async kind => [kind, await run(kind, spec[kind], spec.build)])));
}
const versions = { preferred: await version(preferred), current: await version({ ...args, build: args['build-id'] }) };
if (args['include-rejected']) versions.rejected = await version(rejected);

const output = path.resolve(project, args.out ?? path.join(here, 'REVIEW.html'));
if (path.basename(output) !== 'REVIEW.html') throw new Error('Output must be a fresh REVIEW.html path.');
const manifestPath = path.join(path.dirname(output), 'review-manifest.json');
for (const filename of [output, manifestPath]) {
  try { await fs.access(filename); throw new Error(`Refusing to overwrite ${filename}`); }
  catch (error) { if (error.code !== 'ENOENT') throw error; }
}
const links = [];
for (const [label, filename] of [['Progress', 'progress.md'], ['Unity editing guide', 'Docs/wordbound/LOBBY-EDITING-GUIDE.md']]) {
  const source = await inspectFile(path.resolve(project, filename));
  links.push({ label, path: source.path, sha256: source.sha256 });
}
if (args.exe) {
  if (path.extname(args.exe).toLowerCase() !== '.exe') throw new Error('--exe must name the actual Windows executable.');
  const source = await inspectFile(path.resolve(project, args.exe));
  links.unshift({ label: 'Windows build', path: source.path, sha256: source.sha256 });
}
const tabs = ['Play', 'Customize', 'Settings', 'Equipment', 'World', 'Shop', 'Workshop'];
function area(name, kind) {
  if (/settings/.test(name)) return 'Settings';
  if (/equipment|_menu$/.test(name)) return 'Equipment';
  if (/customize/.test(name)) return 'Customize';
  if (/shop/.test(name)) return 'Shop';
  if (/_world$|local_ready/.test(name)) return 'World';
  if (kind === 'workshop' && /workshop/.test(name)) return 'Workshop';
  return 'Play';
}
const entries = Object.entries(versions.current).flatMap(([kind, value]) =>
  Object.keys(value.images).map(name => ({ kind, name, area: area(name, kind) })));
function figure(image, label, build, extra = '') {
  if (!image) return `<div class="missing ${extra}">No matching ${esc(label.toLowerCase())} capture.</div>`;
  return `<figure class="${extra}"><figcaption><strong>${esc(label)}</strong><small>${esc(build)} · ${image.width} × ${image.height}</small></figcaption><a href="${href(image.path)}" target="_blank" rel="noopener"><img src="${href(image.path)}" alt="${esc(label)} full screenshot" width="${image.width}" height="${image.height}" loading="lazy"></a></figure>`;
}
function card({ kind, name }) {
  const title = name.replace(/^lobby_|^wordbound_/, '').replaceAll('_', ' ');
  return `<article><h3>${esc(title)}</h3><div class="pair">${figure(versions.preferred[kind].images[name], 'Preferred before', preferred.build)}${figure(versions.current[kind].images[name], 'New', args['build-id'])}${versions.rejected ? figure(versions.rejected[kind].images[name], 'Rejected revision', rejected.build, 'rejected') : ''}</div></article>`;
}
const evidenceLinks = Object.entries(versions).flatMap(([label, runs]) => Object.entries(runs).map(([kind, value]) =>
  `<a href="${href(value.checks.path)}" target="_blank" rel="noopener">${esc(label)} ${esc(kind)} · PASS · ${Object.keys(value.images).length} frames</a>`)).join('');
const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Wreckabulary · Play organization review</title>
<style>
:root{color-scheme:light;--paper:#f6f5ef;--ink:#252c2a;--muted:#626b66;--line:#d8ddd5;--accent:#176b61}*{box-sizing:border-box}body{margin:0;color:var(--ink);background:var(--paper);font:16px/1.5 system-ui,"Segoe UI",sans-serif}header,main,footer,.toolbar{max-width:1720px;margin:auto;padding:20px}h1{font-size:28px;margin:0 0 6px}h2{font-size:22px;margin:0 0 18px}h3{font-size:17px;margin:0 0 12px;font-weight:600;text-transform:capitalize}p{margin:6px 0;color:var(--muted)}a{color:var(--accent)}a:focus-visible,button:focus-visible,input:focus-visible{outline:3px solid var(--accent);outline-offset:3px}.links,.checks{display:flex;gap:8px 20px;flex-wrap:wrap;margin-top:12px}.checks{font-size:13px}.toolbar{border-block:1px solid var(--line);display:flex;align-items:center;justify-content:space-between;gap:16px}nav{display:flex;flex-wrap:wrap;gap:3px}button{font:inherit;border:0;background:transparent;padding:9px 13px;border-radius:6px;cursor:pointer;color:var(--muted)}button[aria-selected=true]{background:#e1eae2;color:var(--accent);font-weight:600}label{font-size:13px;white-space:nowrap}article{padding:16px;margin-bottom:24px;border:1px solid var(--line);border-radius:10px;background:white}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}.rejected{display:none}.show-rejected .rejected{display:block}.show-rejected .pair{grid-template-columns:repeat(3,minmax(0,1fr))}figure{margin:0;min-width:0}figcaption{display:flex;justify-content:space-between;flex-wrap:wrap;gap:4px;margin-bottom:8px;font-size:13px}small,.missing{color:var(--muted)}img{display:block;width:100%;height:auto}footer{font-size:13px;border-top:1px solid var(--line)}[hidden]{display:none!important}@media(max-width:900px){.toolbar{display:block}.pair,.show-rejected .pair{grid-template-columns:1fr}header,main,footer,.toolbar{padding:14px}.toolbar label{display:block;margin-top:10px}}
</style></head><body>
<header><h1>Play organization review</h1><p>Preferred before → new build ${esc(args['build-id'])}. Visual review pending.</p><div class="links">${links.map(link => `<a href="${href(link.path)}">${esc(link.label)}</a>`).join('')}<a href="${href(manifestPath)}">SHA evidence</a></div><details><summary>Verified capture checks</summary><div class="checks">${evidenceLinks}</div></details></header>
<div class="toolbar"><nav role="tablist" aria-label="Review area">${tabs.map((tab, i) => `<button role="tab" type="button" id="tab-${tab}" aria-controls="panel-${tab}" aria-selected="${i === 0}" tabindex="${i ? -1 : 0}">${tab}</button>`).join('')}</nav>${versions.rejected ? '<label><input id="rejected" type="checkbox"> Show rejected155755 revision</label>' : ''}</div>
<main>${tabs.map((tab, i) => `<section role="tabpanel" id="panel-${tab}" aria-labelledby="tab-${tab}" tabindex="0"${i ? ' hidden' : ''}><h2>${tab}</h2>${entries.filter(entry => entry.area === tab).map(card).join('') || '<p>No capture in the supplied runs.</p>'}</section>`).join('')}</main>
<footer>All ${entries.length} declared new screenshots are shown unchanged, including trial labels. Open an image for its full original file. PASS describes these automated scenarios; it does not establish visual acceptance, full multiplayer verification or production readiness. Missing historical views are labelled, never invented.</footer>
<script>
const tabs=[...document.querySelectorAll('[role=tab]')];
function choose(button,focus=false){for(const tab of tabs){const on=tab===button;tab.setAttribute('aria-selected',String(on));tab.tabIndex=on?0:-1;document.getElementById(tab.getAttribute('aria-controls')).hidden=!on}if(focus)button.focus();history.replaceState(null,'','#'+button.id.slice(4))}
for(const tab of tabs){tab.onclick=()=>choose(tab);tab.onkeydown=e=>{let i=tabs.indexOf(tab);if(e.key==='ArrowRight')i=(i+1)%tabs.length;else if(e.key==='ArrowLeft')i=(i+tabs.length-1)%tabs.length;else if(e.key==='Home')i=0;else if(e.key==='End')i=tabs.length-1;else return;e.preventDefault();choose(tabs[i],true)}}
const first=tabs.find(tab=>tab.id==='tab-'+location.hash.slice(1));if(first)choose(first);
const rejected=document.getElementById('rejected');if(rejected)rejected.onchange=()=>document.body.classList.toggle('show-rejected',rejected.checked);
</script></body></html>`;
const manifest = { schema: 1, createdUtc: new Date().toISOString(), status: 'VISUAL_REVIEW_PENDING',
  buildId: args['build-id'], currentFrames: entries.length, fullScreenshotsUnchanged: true,
  htmlSha256: sha(Buffer.from(html)), generatorSha256: sha(await fs.readFile(fileURLToPath(import.meta.url))), links, versions };
await fs.mkdir(path.dirname(output), { recursive: true });
await fs.writeFile(manifestPath, JSON.stringify(manifest, null, 2) + '\n', { flag: 'wx' });
await fs.writeFile(output, html, { flag: 'wx' });
console.log(`Created ${output}; ${entries.length} declared current screenshots. Visual review remains pending.`);

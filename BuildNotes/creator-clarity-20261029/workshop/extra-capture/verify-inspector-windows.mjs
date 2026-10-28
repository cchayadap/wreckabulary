import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { inflateSync } from 'node:zlib';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { argumentsFrom, lastGate } from '../../../redesign-20261028/windows-audit/verify-windows-delivery.mjs';

export const scenario = 'wordbound_parcel_workshop_inspector';
export const expected = {
  parcel_workshop_sources: ['parcel_workshop_assigned'],
  parcel_workshop_save_and_focus: ['parcel_workshop_saved', 'parcel_workshop_sorting_close'],
  parcel_workshop_inspector_views: ['parcel_workshop_new_puzzle', 'parcel_workshop_room_routes'],
  parcel_workshop_exact_playtest: ['parcel_workshop_playtest_world', 'parcel_workshop_playtest_menu'],
  parcel_workshop_return_and_history: ['parcel_workshop_returned', 'parcel_workshop_assigned_returned', 'parcel_workshop_parcel_returned', 'parcel_workshop_lobby']
};
const courseId = 'wordbound-parcel-service-v1';
const slash = value => value.replaceAll('\\', '/');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const digest = value => crypto.createHash('sha256').update(value).digest('hex');
const hashPattern = /^[a-f0-9]{64}$/;
function under(root, value) {
  assert.equal(typeof value, 'string', 'Evidence path missing');
  const full = path.resolve(root, value), relative = path.relative(root, full);
  assert(relative && !relative.startsWith('..') && !path.isAbsolute(relative), 'Path escapes evidence root: ' + value);
  let current = root;
  for (const part of relative.split(path.sep)) {
    current = path.join(current, part);
    if (fs.existsSync(current)) assert(!fs.lstatSync(current).isSymbolicLink(), 'Symlink/reparse evidence is unsupported');
  }
  return full;
}
async function proof(root, value) {
  const file = under(root, value), before = fs.statSync(file), hash = crypto.createHash('sha256');
  assert(before.isFile() && before.size > 0, 'Missing/empty evidence file: ' + value);
  for await (const block of fs.createReadStream(file)) hash.update(block);
  const after = fs.statSync(file);
  assert.equal(after.size, before.size); assert.equal(after.mtimeMs, before.mtimeMs, 'Evidence changed during hashing');
  return { path: slash(path.relative(root, file)), bytes: after.size, sha256: hash.digest('hex') };
}
async function unchanged(root, record) {
  assert(record && hashPattern.test(record.sha256), 'Missing delegated evidence hash');
  assert.deepEqual(await proof(root, record.path), record, 'Delegated evidence changed: ' + record.path);
}
function fields(value, names, wanted = true) {
  for (const name of names) assert.equal(value?.[name], wanted, 'Missing/incorrect probe: ' + name);
}
function hashes(value, names) {
  for (const name of names) assert(hashPattern.test(value?.[name]), 'Missing/invalid identity: ' + name);
}
const crcTable = Array.from({ length: 256 }, (_, n) => {
  for (let bit = 0; bit < 8; bit++) n = n & 1 ? 0xedb88320 ^ (n >>> 1) : n >>> 1;
  return n >>> 0;
});
function crc32(bytes) {
  let crc = 0xffffffff;
  for (const byte of bytes) crc = crcTable[(crc ^ byte) & 255] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}
export function pngSize(file) {
  const stat = fs.statSync(file); assert(stat.size >= 45 && stat.size <= 128 * 1024 * 1024, 'PNG size outside bounded capture range');
  const bytes = fs.readFileSync(file); assert.equal(bytes.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
  let offset = 8, width, height, channels, ended = false, idatEnded = false;
  const compressed = [];
  while (offset < bytes.length) {
    assert(offset + 12 <= bytes.length, 'Truncated PNG chunk');
    const length = bytes.readUInt32BE(offset), tag = bytes.toString('ascii', offset + 4, offset + 8), end = offset + 12 + length;
    assert(end <= bytes.length, 'PNG chunk exceeds file');
    assert.equal(crc32(bytes.subarray(offset + 4, end - 4)), bytes.readUInt32BE(end - 4), 'PNG CRC mismatch: ' + tag);
    const data = bytes.subarray(offset + 8, end - 4);
    if (offset === 8) {
      assert.equal(tag, 'IHDR'); assert.equal(length, 13); width = data.readUInt32BE(0); height = data.readUInt32BE(4);
      assert(width >= 640 && height >= 360 && width <= 16384 && height <= 16384, 'Invalid capture dimensions');
      assert.equal(data[8], 8); assert([2, 6].includes(data[9]), 'Expected RGB/RGBA capture');
      channels = data[9] === 6 ? 4 : 3; assert.deepEqual([...data.subarray(10)], [0, 0, 0]);
    } else if (tag === 'IDAT') {
      assert(!idatEnded, 'Noncontiguous PNG IDAT'); compressed.push(data);
    } else {
      if (compressed.length) idatEnded = true;
      if (tag === 'IEND') { assert.equal(length, 0); assert.equal(end, bytes.length); ended = true; }
      else assert(tag === 'PLTE' || (bytes[offset + 4] & 32) !== 0, 'Unknown/duplicate critical PNG chunk: ' + tag);
    }
    offset = end;
  }
  assert(ended && compressed.length > 0, 'Incomplete PNG');
  const stride = width * channels + 1, expectedBytes = stride * height;
  assert(expectedBytes <= 256 * 1024 * 1024, 'PNG inflation budget exceeded');
  const raw = inflateSync(Buffer.concat(compressed), { maxOutputLength: expectedBytes });
  assert.equal(raw.length, expectedBytes, 'PNG decoded scanline length mismatch');
  for (let row = 0; row < height; row++) assert(raw[row * stride] <= 4, 'Invalid PNG scanline filter');
  return { width, height };
}
async function workshopProbes(root, dir, checks) {
  const [open, edit, inspect, play, returned] = checks.steps.map(step => step.probes);
  for (const step of checks.steps) {
    const p = step.probes.parcel_workshop_preservation;
    fields(p, ['sources_preferences_devices_preserved']); assert.equal(p.failure, 'none');
    assert.equal(p.work_budget_seconds, 120); assert.equal(p.outer_budget_seconds, 180);
    assert(!Object.keys(step.probes).some(key => /(?:abandoned_cleanup|abort_error|stop_error|leave_error|restore_error|iterator_disposal_errors)$/.test(key)), 'Unexpected cleanup failure probe');
  }
  assert.equal(edit.parcel_workshop_focus?.room, 'Sorting'); assert.deepEqual(edit.parcel_workshop_focus.selected, ['Sorting']);
  assert(Number.isFinite(edit.parcel_workshop_focus.camera_distance) && edit.parcel_workshop_focus.camera_distance > 0);
  const game = play.parcel_workshop_exact_playtest;
  hashes(game, ['source_hash', 'manifest_hash', 'catalogue_hash']);
  assert.equal(game.mode, 'TimeRun'); assert.equal(game.runner, 'Single'); assert(game.canonical_ticks > 5);
  fields(game, ['saved_draft_unchanged']);
  fields(returned.parcel_workshop_histories, ['assigned_history_same', 'parcel_history_same', 'assigned_undo_redo_exact', 'parcel_undo_redo_exact', 'separate_autosaves']);
  for (const step of checks.steps) for (const name of expected[step.name]) {
    if (name.includes('playtest_') || name === 'parcel_workshop_lobby') continue;
    const render = step.probes[name + '_render']; fields(render, ['active']);
    assert.equal(render.screen_camera, false); assert(render.camera_frames >= 3);
  }
  const render = play.parcel_workshop_playtest_render; fields(render, ['active', 'screen_camera']); assert(render.camera_frames >= 3);
  fields(inspect.parcel_workshop_inspector_preservation, ['fields_history_seed_autosaves_sources_preserved']);
  assert.deepEqual(inspect.parcel_workshop_inspector_preservation.cleanup_errors, []);
  for (const [name, section, panel] of [['parcel_workshop_new_puzzle', 'Puzzles', 'New puzzle'],
    ['parcel_workshop_room_routes', 'Room', 'Routes & checkpoints / Safe']]) {
    const view = inspect[name + '_view'], capture = inspect[name + '_capture'];
    assert.equal(view?.section, section); assert.equal(view.panel, panel); assert.equal(view.source_course_id, courseId);
    assert.deepEqual(view.selected_rooms, ['Sorting']); assert(typeof view.reference_id === 'string' && view.reference_id.length > 0);
    assert(Number.isFinite(view.scroll) && view.scroll >= 0);
    assert(Array.isArray(view.inspector_rect) && view.inspector_rect.length === 4 && view.inspector_rect.every(Number.isFinite));
    const [x, y, w, h] = view.inspector_rect;
    assert(x >= 0 && y >= 0 && w > 0 && h > 0 && x + w <= capture.width && y + h <= capture.height, 'Inspector rect lies outside captured window');
  }
  const storage = under(path.resolve(os.tmpdir(), 'wbqa'), open.isolated_workshop_storage);
  assert(/^[a-f0-9]{32}$/.test(path.basename(storage)), 'Workshop storage must be an isolated QA GUID');
  const saves = [];
  for (const [p, key, parcel] of [[open, 'parcel_workshop_assigned_autosave', false], [edit, 'parcel_workshop_parcel_autosave', true], [returned, 'parcel_workshop_returned_autosave', true]]) {
    const note = p[key]; hashes(note, ['recovery_sha256']); assert.equal(note.used_backup, false);
    const evidence = path.join(dir, key + '.json'); assert.equal(under(root, note.evidence), evidence);
    const text = fs.readFileSync(evidence, 'utf8').replace(/^\uFEFF/, ''), saved = JSON.parse(text), course = JSON.parse(saved.course);
    assert.equal(digest(text), note.recovery_sha256); assert.equal(saved.format, 'wreckabulary.workshop'); assert.equal(saved.schema, 1);
    assert(typeof course.id === 'string' && /^[A-Za-z0-9_-]+$/.test(course.id));
    const primary = under(storage, (parcel ? 'local-courses/ParcelServiceV1/' : '') + course.id + '.autosave.json');
    assert.equal(path.resolve(note.primary), primary);
    const evidenceProof = await proof(root, evidence), primaryProof = await proof(storage, primary);
    assert.equal(primaryProof.sha256, evidenceProof.sha256, 'Isolated primary differs from captured save');
    assert.deepEqual(fs.readdirSync(path.dirname(primary)).filter(name => name.startsWith(path.basename(primary) + '.') && name.endsWith('.tmp')), []);
    if (parcel) {
      assert.equal(course.id, courseId); assert.equal(course.title, 'Parcel Service · QA edit');
      assert.equal(saved.seed, '0314159265358979'); assert.equal(saved.seedLocked, true);
      assert.equal(digest(saved.course), game.source_hash, 'Playtest source differs from exact edited draft');
    } else assert(course.title.endsWith(' · QA retained'));
    saves.push({ ...evidenceProof, primary: { path: primary, sha256: primaryProof.sha256 }, recoveryHash: note.recovery_sha256 });
  }
  assert.equal(saves[1].sha256, saves[2].sha256, 'Parcel save changed after playtest/source switching');
  return saves;
}
export async function verifyRun(root, directory, gatePath, build) {
  const dir = under(root, directory), runId = path.basename(dir), prefix = slash(path.relative(root, dir));
  assert(new RegExp('^\\d{8}T\\d{6}Z-' + scenario + '$').test(runId));
  assert.equal(prefix, 'Docs/qa/runs/' + runId, 'Use the canonical exact run directory');
  const gateFile = under(root, gatePath), gate = lastGate(gateFile), smoke = gate.smoke;
  assert.equal(gate.cmd, `smoke --build ${build.id} --scenario ${scenario}`);
  assert.equal(slash(path.relative(root, under(root, gate.log))), 'Logs/player/' + runId + '.log');
  assert.equal(smoke?.run_id, runId); assert.equal(smoke.scenario, scenario); assert.equal(slash(smoke.build), slash(build.dir));
  assert.equal(smoke.kind, build.kind); fields(smoke, ['stripped', 'timed_out'], false); assert.equal(smoke.exit, 0);
  const checksFile = path.join(dir, 'checks.json'), checks = read(checksFile);
  assert(gate.evidence.includes(prefix + '/checks.json'));
  assert.equal(checks.schema, 1); assert.equal(checks.run_id, runId); assert.equal(checks.build_id, build.id);
  assert.equal(checks.scenario, scenario); assert.equal(checks.platform, 'player'); assert.equal(checks.result, 'PASS');
  assert.equal(checks.seed, smoke.seed); assert.equal(checks.development_build, false); assert.equal(checks.unity_version, build.unity_version);
  if (checks.commit != null) assert.equal(checks.commit, build.head);
  if (checks.tree != null) assert.equal(checks.tree, build.tree);
  assert(Number.isFinite(Date.parse(checks.started_utc)) && Date.parse(checks.started_utc) >= Date.parse(build.utc));
  assert(Date.parse(checks.ended_utc) >= Date.parse(checks.started_utc));
  fields(checks.console, ['errors', 'exceptions', 'warnings'], 0); assert.equal(checks.console.first_exception, null);
  assert.deepEqual(checks.steps.map(step => step.name), Object.keys(expected));
  const captures = []; let previousFrame = 0;
  for (const step of checks.steps) {
    assert.equal(step.status, 'PASS'); assert.deepEqual(step.assert_failures, []);
    const names = expected[step.name];
    assert.deepEqual(step.captures.map(slash), names.map(name => prefix + '/' + name + '.png'));
    for (const name of names) {
      const info = step.probes[name + '_capture']; fields(info, ['focused']);
      assert(Number.isInteger(info.rendered_frame) && info.rendered_frame > previousFrame, 'Missing/nonadvancing actual rendered frame'); previousFrame = info.rendered_frame;
      const file = prefix + '/' + name + '.png', size = pngSize(under(root, file));
      assert.deepEqual(size, { width: info.width, height: info.height }); assert(size.width >= 640 && size.height >= 360);
      captures.push({ ...await proof(root, file), ...size, step: step.name, frame: info.rendered_frame, focused: true });
    }
  }
  const names = Object.values(expected).flat(); assert.equal(names.length, 11);
  assert.deepEqual(checks.captures.map(item => item.name), names);
  assert.deepEqual(checks.captures.map(item => slash(item.path)), names.map(name => prefix + '/' + name + '.png'));
  assert(checks.captures.every(item => item.kind === 'png' && item.camera === 'screen'));
  assert.deepEqual(fs.readdirSync(dir).filter(name => name.toLowerCase().endsWith('.png')).sort(), names.map(name => name + '.png').sort());
  const saves = await workshopProbes(root, dir, checks);
  const log = fs.readFileSync(under(root, gate.log), 'utf8');
  assert(log.includes('[GSK] QA ' + scenario + ' PASS'));
  assert(!/^(?:Crash!!!|Fatal error|Unhandled Exception|[A-Za-z]+Exception:)/m.test(log));
  return { id: runId, scenario, status: 'PASS', startedUtc: checks.started_utc, endedUtc: checks.ended_utc,
    checks: await proof(root, checksFile), gate: await proof(root, gateFile), gateResult: gate, log: await proof(root, gate.log),
    sourceBinding: { buildId: checks.build_id, embeddedCommit: checks.commit ?? null, embeddedTree: checks.tree ?? null },
    stepResults: checks.steps, captures, saves };
}
export async function verifyInspector(options) {
  const keys = ['root', 'build', 'source-commit', 'base-audit', 'run', 'gate', 'out'];
  for (const key of keys) assert(options[key], 'Missing --' + key);
  for (const key of Object.keys(options)) assert(keys.includes(key), 'Unknown --' + key);
  const root = fs.realpathSync(options.root), out = under(root, options.out), baseFile = under(root, options['base-audit']);
  assert(!fs.existsSync(out), 'Refusing to overwrite audit evidence');
  assert(/^\d{8}T\d{6}Z$/.test(options.build)); assert(/^[a-f0-9]{40}$/.test(options['source-commit']));
  const base = read(baseFile), build = base.build;
  assert.equal(base.schema, 2); assert.equal(base.status, 'BOUNDED_LOBBY_SOLO_WORKSHOP_WINDOWS_PASS');
  assert.equal(build.id, options.build); assert.equal(build.head, options['source-commit']); assert.equal(build.ok, true);
  assert.equal(build.kind, 'release-qa'); assert.equal(build.development, false);
  assert.equal(base.source.commit, build.head); fields(base.source, ['productionDiffEmpty', 'untrackedNonIgnoredProductionEmpty']);
  assert.equal(base.scope.captures, 45);
  assert.deepEqual(base.runs.map(run => [run.scenario, run.status, run.captures.length]),
    [['lobby_experience', 'PASS', 24], ['wordbound_solo', 'PASS', 13], ['wordbound_workshop_flow', 'PASS', 8]]);
  assert.equal(base.verifier.path, 'BuildNotes/redesign-20261028/windows-audit/verify-windows-delivery.mjs');
  for (const record of [base.verifier, base.buildReceipt, base.buildGate, base.executable, base.packedManifest, ...base.scenarioSources]) await unchanged(root, record);
  assert.deepEqual(read(under(root, base.buildReceipt.path)), build);
  assert.deepEqual(lastGate(under(root, base.buildGate.path)).build, build);
  const git = args => execFileSync('git', args, { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  const roots = ['Assets', 'Packages', 'ProjectSettings'];
  assert.equal(git(['rev-parse', '--verify', build.head + '^{commit}']), build.head);
  assert.equal(git(['diff', '--name-only', build.head, '--', ...roots]), '', 'Production source differs from audited build');
  assert.equal(git(['ls-files', '--others', '--exclude-standard', '--', ...roots]), '', 'Untracked production source is not bound to build');
  const run = await verifyRun(root, options.run, options.gate, build);
  await unchanged(root, base.preservation.receipt); await unchanged(root, base.preservation.baseline);
  const preservation = read(under(root, base.preservation.receipt.path));
  assert.equal(preservation.status, 'PASS'); assert.deepEqual(preservation.changed, []);
  assert.equal(preservation.utc, base.preservation.receiptUtc);
  assert.equal(preservation.baseline_sha256, base.preservation.baseline.sha256);
  assert.equal(preservation.files, base.preservation.verifiedFileCount);
  assert(Number.isFinite(Date.parse(preservation.utc)) && Date.parse(preservation.utc) >= Date.parse(run.endedUtc),
    'Final authored preservation must follow the inspector run');
  const scenarioSources = [];
  for (const name of ['WordboundParcelWorkshopQa.cs']) scenarioSources.push(await proof(root, 'Assets/_Project/Scripts/WordboundQa/' + name));
  const receipt = { schema: 1, utc: new Date().toISOString(), status: 'BOUNDED_WORKSHOP_INSPECTOR_WINDOWS_PASS', build,
    source: { commit: build.head, currentHead: git(['rev-parse', 'HEAD']), productionDiffEmpty: true, untrackedNonIgnoredProductionEmpty: true },
    verifier: await proof(root, fileURLToPath(import.meta.url)), delegatedBaseAudit: await proof(root, baseFile), scenarioSources, run, preservation: base.preservation,
    scope: { captures: 11, steps: 5, visualAcceptance: 'PENDING independent inspection of these exact full screenshots',
      limits: [
        'Build, packed inventory and original 45-image evidence are delegated to the unchanged successful base audit. Its verifier/build/QA/executable/inventory fingerprints are rebound here, not every packed binary or old image.',
        'The exact built commit matches current tracked and nonignored production inputs. Embedded run commit/tree may be absent; no historic per-run executable hash is available. Ignored production inputs are outside Git comparison.',
        'Normal Windows mode, cleanup and source/UI preservation rely on assertions in the exact source-bound game scenario. Frames and focus are recorded. View-only section/field navigation does not prove physical mouse/controller input.',
        'PNG chunk CRCs, RGB/RGBA structure, inflated scanline sizes/filter tags, dimensions and SHA256 are checked. Pixel appearance and nonuniformity are not reimplemented; the game scenario decodes and rejects uniform images.',
        'New puzzle and Room/Safe route screens do not prove every section, scroll position, recipe, authoring action, layout fit, full gameplay, network matchmaking, performance or production acceptance.',
        'Authored file preservation is delegated to the unchanged final base receipt, which must follow this run. Captured autosaves and their current isolated primary bytes are verified separately.'
      ] } };
  assert.equal(git(['diff', '--name-only', build.head, '--', ...roots]), '', 'Production changed during verification');
  assert.equal(git(['ls-files', '--others', '--exclude-standard', '--', ...roots]), '', 'Untracked production appeared during verification');
  fs.writeFileSync(out, JSON.stringify(receipt, null, 2) + '\n', { flag: 'wx' });
  return { output: slash(path.relative(root, out)), status: receipt.status, build: build.id, captures: 11 };
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { console.log(JSON.stringify(await verifyInspector(argumentsFrom(process.argv.slice(2))))); }
  catch (error) { console.error('Inspector evidence rejected: ' + error.message); process.exitCode = 1; }
}

import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import zlib from 'node:zlib';
import { execFileSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import assert from 'node:assert/strict';
import test from 'node:test';
import { expected, argumentsFrom } from './verify-windows-delivery.mjs';

const parent = path.dirname(fileURLToPath(import.meta.url));
const digest = value => crypto.createHash('sha256').update(value).digest('hex');
function png() {
  const chunk = (name, data) => {
    const body = Buffer.concat([Buffer.from(name), data]); let crc = 0xffffffff;
    for (const byte of body) { crc ^= byte; for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0); }
    const result = Buffer.alloc(body.length + 8); result.writeUInt32BE(data.length); body.copy(result, 4); result.writeUInt32BE((crc ^ 0xffffffff) >>> 0, result.length - 4); return result;
  };
  const header = Buffer.alloc(13); header.writeUInt32BE(640, 0); header.writeUInt32BE(360, 4); header[8] = 8; header[9] = 2;
  return Buffer.concat([Buffer.from('89504e470d0a1a0a', 'hex'), chunk('IHDR', header), chunk('IDAT', zlib.deflateSync(Buffer.alloc((640 * 3 + 1) * 360))), chunk('IEND', Buffer.alloc(0))]);
}

async function fixture() {
  const root = fs.mkdtempSync(path.join(parent, '.synthetic-delivery-'));
  const write = (name, value) => { const full = path.join(root, name); fs.mkdirSync(path.dirname(full), { recursive: true }); fs.writeFileSync(full, value); };
  const json = (name, value) => write(name, JSON.stringify(value));
  const read = name => JSON.parse(fs.readFileSync(path.join(root, name), 'utf8'));
  write('Assets/Presentation.asset', 'SYNTHETIC PRESERVATION INPUT; NOT A GAME ASSET\n');
  execFileSync('git', ['init', '--quiet', root]);
  execFileSync('git', ['config', '--local', 'core.autocrlf', 'false'], { cwd: root });
  execFileSync('git', ['add', 'Assets'], { cwd: root });
  execFileSync('git', ['-c', 'user.name=Synthetic delivery test', '-c', 'user.email=synthetic@example.invalid', '-c', 'core.hooksPath=.no-hooks', 'commit', '--quiet', '-m', 'Synthetic receipt fixture only'], { cwd: root });
  const commit = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim();
  write('verify.mjs', fs.readFileSync(path.join(parent, 'verify-windows-delivery.mjs')));
  const verify = (await import(pathToFileURL(path.join(root, 'verify.mjs')).href)).verifyDelivery;
  const build = { schema: 1, id: '20990101T000000Z', kind: 'release-qa', dir: 'Builds/qa-20990101T000000Z', exe: 'Wreckabulary.exe',
    profile: 'Assets/Settings/Build Profiles/Win-QA.asset', development: false, unity_version: 'SYNTHETIC', head: commit,
    tree: 'a'.repeat(64), ok: true };
  const artifactNames = ['Wreckabulary.exe', 'Wreckabulary_Data/Managed/Wreckabulary.dll', 'Wreckabulary_Data/Managed/Wreckabulary.Online.Client.dll', 'Wreckabulary_Data/Managed/Wreckabulary.Wordbound.Qa.dll'];
  for (const name of artifactNames) write(build.dir + '/' + name, 'SYNTHETIC; NOT EXECUTABLE\n');
  json(build.dir + '/build.json', build);
  json(build.dir + '/packed_assets.json', { build_id: build.id, kind: build.kind, kind_source: 'binary', gsk_qa: true, development: false,
    unity_version: build.unity_version, profile: build.profile, output_dir: build.dir,
    files: artifactNames.map(name => ({ path: name, size: fs.statSync(path.join(root, build.dir, name)).size })) });
  write('build.log', 'SYNTHETIC BUILD LOG\n');
  const gate = { cmd: 'synthetic fixture', ok: true, exit: 0, verdict: 'PASS', errors: [], failures: [] };
  json('build-gate.txt', { ...gate, build, log: 'build.log' });
  json('preservation-before.json', { head: commit, scope: 'SYNTHETIC preservation only', files: [{ path: 'Assets/Presentation.asset', sha256: digest(fs.readFileSync(path.join(root, 'Assets/Presentation.asset'))) }] });
  json('preservation.json', { status: 'PASS', changed: [], files: 1, baseline: 'preservation-before.json',
    baseline_sha256: digest(fs.readFileSync(path.join(root, 'preservation-before.json'))), scope: 'SYNTHETIC preservation only' });
  const options = { root, build: build.id, 'source-commit': commit, 'build-gate': 'build-gate.txt', preservation: 'preservation.json', out: 'result.json' };
  const image = png();
  for (const scenario of Object.keys(expected)) {
    const id = '20990101T000100Z-' + scenario, dir = 'Docs/qa/runs/' + id, captures = [];
    const steps = Object.entries(expected[scenario]).map(([name, names]) => {
      const files = names.map(value => dir + '/' + value + '.png');
      const probes = { [scenario === 'lobby_experience' ? 'navigation_preferences_unchanged' : 'solo_source_preferences_preserved']: true };
      if (name === 'local_choices' || name === 'create_choices') probes[name === 'local_choices' ? 'local_owned_stack_cleanup' : 'create_owned_stack_cleanup'] = {
        failure: 'none', errors: [], work_budget_seconds: name === 'local_choices' ? 25 : 15, outer_budget_seconds: name === 'local_choices' ? 30 : 20 };
      if (name === 'open_wordbound') Object.assign(probes, { party_frame_ready: true, party_frame_focused: true, preserved_local_selection: ['Practice', 'Dibs', 'pinwheel'] });
      if (scenario === 'wordbound_solo' && name !== 'solo_selection') {
        const mode = name === 'time_run' ? 'TimeRun' : 'Freestyle';
        probes['solo_render_readiness_' + mode] = { expected_markers: mode === 'TimeRun' ? 1 : 0, rendered_markers: mode === 'TimeRun' ? 1 : 0,
          camera_frames: 3, canonical_ticks: 6, guide_fault: '', scope: 'Guide count matches the current canonical mode plan. Freestyle may have no nearby or selected practice station.' };
        probes.solo_cleanup = { failure: 'none', work_budget_seconds: 145, stop_budget_seconds: 25, outer_budget_seconds: 180, client_released: true, bindings_restored: true };
        probes['solo_' + mode] = { source: 'b'.repeat(64), manifest: 'c'.repeat(64), mode, physical_sites: 72, humans: 1, runner: 'Single', actual_camera_frames: 3,
          canonical_ticks_observed: 6, journal_entries: 0, return_label: 'Return to lobby' };
      }
      names.forEach((value, index) => { write(files[index], image); probes[value + '_capture'] = { width: 640, height: 360, focused: true, rendered_frame: 10, varied_pixels: true };
        captures.push({ name: value, path: files[index], kind: 'png' }); });
      return { name, status: 'PASS', assert_failures: [], probes, captures: files };
    });
    json(dir + '/checks.json', { schema: 1, run_id: id, build_id: build.id, scenario, platform: 'player', result: 'PASS', seed: 1,
      development_build: false, unity_version: build.unity_version, commit: null, tree: null, started_utc: '2099-01-01T00:01:00Z', ended_utc: '2099-01-01T00:02:00Z',
      console: { errors: 0, exceptions: 0, warnings: 0, first_exception: null }, steps, captures });
    write('Logs/player/' + id + '.log', 'SYNTHETIC PLAYER LOG\n');
    json(scenario + '-gate.txt', { ...gate, log: 'Logs/player/' + id + '.log', smoke: { run_id: id, scenario, build: build.dir, kind: build.kind, seed: 1, stripped: false, exit: 0, timed_out: false } });
    const prefix = scenario === 'lobby_experience' ? 'lobby' : 'solo'; options[prefix + '-run'] = dir; options[prefix + '-gate'] = scenario + '-gate.txt';
  }
  return { root, options, verify, read, json, write, close: () => {
    assert(path.dirname(root) === parent && path.basename(root).startsWith('.synthetic-delivery-')); fs.rmSync(root, { recursive: true, force: true });
  } };
}

test('synthetic complete receipt passes with 20+9 captures and refuses overwrite', async () => {
  const value = await fixture();
  try {
    const result = await value.verify(value.options); assert.equal(result.captures, 29);
    const receipt = value.read('result.json'); assert.equal(receipt.runs[0].captures.length, 20); assert.equal(receipt.runs[1].captures.length, 9);
    assert.equal(receipt.runs[0].sourceBinding.embeddedCommit, null); assert.equal(receipt.scope.visualAcceptance.startsWith('PENDING'), true);
    const before = fs.readFileSync(path.join(value.root, 'result.json'));
    await assert.rejects(value.verify(value.options), /overwrite/); assert.deepEqual(fs.readFileSync(path.join(value.root, 'result.json')), before);
  } finally { value.close(); }
});

for (const [name, mutate, error] of [
  ['failed gate', value => { const gate = value.read('wordbound_solo-gate.txt'); gate.verdict = 'FAIL'; value.json('wordbound_solo-gate.txt', gate); }, /Gate did not PASS/],
  ['missing capture', value => fs.unlinkSync(path.join(value.root, value.options['lobby-run'], 'lobby_create_wordbound.png')), /ENOENT/],
  ['wrong source commit', value => { value.options['source-commit'] = '0'.repeat(40); }, /./],
  ['changed production file', value => value.write('Assets/Presentation.asset', 'CHANGED'), /Production files differ/],
  ['preservation baseline hash mismatch', value => { const record = value.read('preservation.json'); record.baseline_sha256 = '0'.repeat(64); value.json('preservation.json', record); }, /Preservation baseline changed/],
  ['preservation file hash mismatch', value => { const record = value.read('preservation-before.json'); record.files[0].sha256 = '0'.repeat(64); value.json('preservation-before.json', record);
    const verifier = value.read('preservation.json'); verifier.baseline_sha256 = digest(fs.readFileSync(path.join(value.root, 'preservation-before.json'))); value.json('preservation.json', verifier); }, /Authored file no longer matches/],
  ['preservation baseline used as result', value => { value.options.preservation = 'preservation-before.json'; }, /Preservation verifier did not pass/],
  ['wrong smoke build', value => { const gate = value.read('wordbound_solo-gate.txt'); gate.smoke.build = 'Builds/qa-20980101T000000Z'; value.json('wordbound_solo-gate.txt', gate); }, /./],
  ['unrelated player log', value => { value.write('Logs/player/stale-unrelated.log', 'SYNTHETIC STALE LOG'); const gate = value.read('wordbound_solo-gate.txt'); gate.log = 'Logs/player/stale-unrelated.log'; value.json('wordbound_solo-gate.txt', gate); }, /Player log is not bound/],
  ['missing journal count', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); delete record.steps[1].probes.solo_TimeRun.journal_entries; value.json(file, record); }, /Journal count observation missing/],
  ['old run missing mode readiness', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); delete record.steps[2].probes.solo_render_readiness_Freestyle; value.json(file, record); }, /Exact mode guide readiness is missing/],
  ['guide count mismatch', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[2].probes.solo_render_readiness_Freestyle.rendered_markers = 1; value.json(file, record); }, /differs from the mode plan/],
  ['nonfinite expected guide count', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[1].probes.solo_render_readiness_TimeRun.expected_markers = Infinity; value.json(file, record); }, /Exact mode guide readiness is missing/],
  ['TimeRun empty guide plan', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); Object.assign(record.steps[1].probes.solo_render_readiness_TimeRun, { expected_markers: 0, rendered_markers: 0 }); value.json(file, record); }, /TimeRun needs an active planned guide/],
  ['Freestyle negative guide plan', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); Object.assign(record.steps[2].probes.solo_render_readiness_Freestyle, { expected_markers: -1, rendered_markers: -1 }); value.json(file, record); }, /Exact mode guide readiness is missing/],
  ['mode readiness stale camera', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[1].probes.solo_render_readiness_TimeRun.camera_frames = 2; value.json(file, record); }, /lacks fresh camera frames/],
  ['mode readiness stale ticks', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[1].probes.solo_render_readiness_TimeRun.canonical_ticks = 5; value.json(file, record); }, /lacks advancing canonical ticks/],
  ['mode readiness guide fault', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[2].probes.solo_render_readiness_Freestyle.guide_fault = 'synthetic guide fault'; value.json(file, record); }, /Guide readiness reports a fault/],
  ['missing cleanup proof', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); delete record.steps[1].probes.solo_cleanup; value.json(file, record); }, /clean completion/],
  ['abandoned cleanup', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[1].probes.solo_abandoned_cleanup = {}; value.json(file, record); }, /Abandoned solo cleanup/],
  ['PNG dimension mismatch', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file); record.steps[0].probes.lobby_local_dibs_capture.width = 1280; value.json(file, record); }, /dimensions differ/],
  ['packed size mismatch', value => value.write('Builds/qa-20990101T000000Z/Wreckabulary.exe', 'CHANGED SYNTHETIC FILE LENGTH'), /Packed artifact size changed/],
  ['packed path escape', value => { const file = 'Builds/qa-20990101T000000Z/packed_assets.json', record = value.read(file); record.files[0].path = '../escape'; value.json(file, record); }, /escapes its root/],
  ['missing preservation boolean', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file); delete record.steps[1].probes.navigation_preferences_unchanged; value.json(file, record); }, /Preservation not proved/],
  ['merged retry gates', value => { const file = path.join(value.root, 'wordbound_solo-gate.txt'); fs.appendFileSync(file, '\n' + fs.readFileSync(file, 'utf8')); }, /exactly one terminal/],
  ['truncated terminal gate', value => fs.appendFileSync(path.join(value.root, 'wordbound_solo-gate.txt'), '\n{"cmd":"incomplete'), /Malformed JSON-looking/]
]) test('synthetic rejection: ' + name, async () => {
  const value = await fixture();
  try { mutate(value); await assert.rejects(value.verify(value.options), error); assert(!fs.existsSync(path.join(value.root, 'result.json')), 'Rejected evidence must not publish a receipt'); }
  finally { value.close(); }
});

test('explicit argument parser rejects missing/duplicate inputs', () => {
  assert.throws(() => argumentsFrom(['--root']), /pairs/);
  assert.throws(() => argumentsFrom(['--build', 'one', '--build', 'two']), /Duplicate/);
});

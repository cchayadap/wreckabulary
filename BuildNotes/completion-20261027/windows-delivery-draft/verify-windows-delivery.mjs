import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

export const expected = {
  lobby_experience: {
    local_choices: ['lobby_local_dibs', 'lobby_local_duos', 'lobby_local_escape', 'lobby_solo_timerun', 'lobby_solo_freestyle', 'lobby_solo_two_seats'],
    create_choices: ['lobby_create_learn', 'lobby_create_build', 'lobby_create_wordbound'],
    customize_and_shop: ['lobby_customize', 'lobby_shop'],
    settings_navigation: ['lobby_settings_profile', 'lobby_settings_audio', 'lobby_settings_gameplay', 'lobby_settings_graphics', 'lobby_settings_controls', 'lobby_settings_controls_bottom', 'lobby_settings_help', 'lobby_home_returned'],
    open_wordbound: ['lobby_online_ready']
  },
  wordbound_solo: {
    solo_selection: ['solo_initial_lobby'],
    time_run: ['solo_timerun_selected', 'solo_timerun_world', 'solo_timerun_menu', 'solo_timerun_returned'],
    freestyle: ['solo_freestyle_selected', 'solo_freestyle_world', 'solo_freestyle_menu', 'solo_freestyle_returned']
  }
};

const slash = value => value.replaceAll('\\', '/');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const shaPattern = /^[a-f0-9]{64}$/;
const has = (value, key) => Object.prototype.hasOwnProperty.call(value, key);

function under(root, value) {
  assert.equal(typeof value, 'string', 'Missing evidence path');
  const full = path.resolve(root, value);
  const relative = path.relative(root, full);
  assert(relative && !relative.startsWith('..') && !path.isAbsolute(relative), 'Evidence path escapes its root: ' + value);
  let cursor = root;
  for (const part of relative.split(path.sep)) {
    cursor = path.join(cursor, part);
    if (fs.existsSync(cursor)) assert(!fs.lstatSync(cursor).isSymbolicLink(), 'Symlink/reparse evidence is not supported: ' + cursor);
  }
  return full;
}

async function proof(root, file) {
  const absolute = under(root, file);
  const before = fs.statSync(absolute);
  assert(before.isFile() && before.size > 0, 'Evidence file is empty/non-file: ' + file);
  const digest = crypto.createHash('sha256');
  for await (const block of fs.createReadStream(absolute)) digest.update(block);
  const after = fs.statSync(absolute);
  assert.equal(after.size, before.size, 'Evidence changed while hashing: ' + file);
  assert.equal(after.mtimeMs, before.mtimeMs, 'Evidence changed while hashing: ' + file);
  return { path: slash(path.relative(root, absolute)), bytes: after.size, sha256: digest.digest('hex') };
}

export function lastGate(file) {
  const values = fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '').split(/\r?\n/).filter(line => line.trim().startsWith('{')).flatMap(line => {
    try { const value = JSON.parse(line); return has(value, 'verdict') ? [value] : []; }
    catch { throw new Error('Malformed JSON-looking gate output: ' + file); }
  });
  assert.equal(values.length, 1, 'Expected exactly one terminal gate result, not a combined/retried log: ' + file);
  const value = values[0];
  assert.equal(value.ok, true, 'Gate not successful');
  assert.equal(value.verdict, 'PASS', 'Gate did not PASS');
  assert.equal(value.exit, 0, 'Gate nonzero exit');
  assert.deepEqual(value.errors, [], 'Gate errors are missing/nonempty');
  assert.deepEqual(value.failures, [], 'Gate failures are missing/nonempty');
  return value;
}

function pngSize(file) {
  const fd = fs.openSync(file, 'r');
  try {
    const head = Buffer.alloc(24), tail = Buffer.alloc(12), length = fs.fstatSync(fd).size;
    assert(length >= 45 && fs.readSync(fd, head, 0, 24, 0) === 24, 'Truncated PNG: ' + file);
    assert.equal(head.subarray(0, 8).toString('hex'), '89504e470d0a1a0a', 'Not PNG: ' + file);
    assert.equal(head.readUInt32BE(8), 13, 'Invalid PNG IHDR length');
    assert.equal(head.toString('ascii', 12, 16), 'IHDR', 'Missing PNG IHDR');
    fs.readSync(fd, tail, 0, 12, length - 12);
    assert.equal(tail.toString('hex'), '0000000049454e44ae426082', 'Missing final PNG IEND');
    return { width: head.readUInt32BE(16), height: head.readUInt32BE(20) };
  } finally { fs.closeSync(fd); }
}

function cleanup(value, budgets, name) {
  assert(value && value.failure === 'none', name + ' did not confirm clean completion');
  for (const [key, expectedValue] of Object.entries(budgets)) assert.equal(value[key], expectedValue, name + ' changed ' + key);
  if (has(value, 'errors')) assert.deepEqual(value.errors, [], name + ' cleanup errors');
}

export async function verifyRun(root, directory, gatePath, build, scenario) {
  const dir = under(root, directory), runId = path.basename(dir);
  assert(new RegExp('^\\d{8}T\\d{6}Z-' + scenario + '$').test(runId), 'Invalid explicit run directory');
  assert.equal(slash(path.relative(root, dir)), 'Docs/qa/runs/' + runId, 'Run must be its canonical Docs/qa/runs directory');
  const gateFile = under(root, gatePath), gate = lastGate(gateFile), smoke = gate.smoke;
  assert.equal(slash(path.relative(root, under(root, gate.log))), 'Logs/player/' + runId + '.log', 'Player log is not bound to the selected run');
  assert(smoke, 'Missing smoke result');
  assert.equal(smoke.run_id, runId); assert.equal(smoke.scenario, scenario);
  assert.equal(slash(smoke.build), slash(build.dir)); assert.equal(smoke.kind, build.kind);
  assert.equal(smoke.stripped, false); assert.equal(smoke.exit, 0); assert.equal(smoke.timed_out, false);
  const checksFile = path.join(dir, 'checks.json'), checks = read(checksFile);
  assert.equal(checks.schema, 1); assert.equal(checks.run_id, runId); assert.equal(checks.build_id, build.id);
  assert.equal(checks.scenario, scenario); assert.equal(checks.platform, 'player'); assert.equal(checks.result, 'PASS');
  assert.equal(checks.seed, smoke.seed); assert.equal(checks.development_build, build.development);
  assert.equal(checks.unity_version, build.unity_version);
  if (checks.commit != null) assert.equal(checks.commit, build.head, 'Embedded run source commit mismatch');
  if (checks.tree != null) assert.equal(checks.tree, build.tree, 'Embedded run metadata freshness fingerprint mismatch');
  assert(Number.isFinite(Date.parse(checks.started_utc)) && Date.parse(checks.ended_utc) >= Date.parse(checks.started_utc), 'Missing/invalid terminal run times');
  for (const key of ['errors', 'exceptions', 'warnings']) assert.equal(checks.console?.[key], 0, 'Console ' + key);
  assert.equal(checks.console.first_exception, null);
  assert.deepEqual(checks.steps.map(step => step.name), Object.keys(expected[scenario]), 'Step names/order/count changed');
  const captures = [];
  for (const step of checks.steps) {
    assert.equal(step.status, 'PASS', 'Failed/missing step: ' + step.name);
    assert.deepEqual(step.assert_failures, [], 'Missing/nonempty assertion list: ' + step.name);
    const names = expected[scenario][step.name];
    const files = names.map(name => slash(path.relative(root, dir)) + '/' + name + '.png');
    assert.deepEqual(step.captures.map(slash), files, 'Missing, extra or reordered captures: ' + step.name);
    const probes = step.probes;
    assert.equal(probes?.[scenario === 'lobby_experience' ? 'navigation_preferences_unchanged' : 'solo_source_preferences_preserved'], true, 'Preservation not proved: ' + step.name);
    if (step.name === 'local_choices') {
      cleanup(probes.local_owned_stack_cleanup, { work_budget_seconds: 25, outer_budget_seconds: 30 }, 'Local');
      assert.deepEqual(probes.local_owned_stack_cleanup.errors, []);
    }
    if (step.name === 'create_choices') {
      cleanup(probes.create_owned_stack_cleanup, { work_budget_seconds: 15, outer_budget_seconds: 20 }, 'Create');
      assert.deepEqual(probes.create_owned_stack_cleanup.errors, []);
    }
    if (step.name === 'open_wordbound') {
      assert.equal(probes.party_frame_ready, true);
      assert.equal(probes.party_frame_focused, true);
      assert(Array.isArray(probes.preserved_local_selection) && probes.preserved_local_selection.length === 3, 'Online local-selection proof missing');
    }
    if (scenario === 'wordbound_solo') {
      assert(!has(probes, 'solo_abandoned_cleanup'), 'Abandoned solo cleanup is not confirmed cleanup');
      if (step.name !== 'solo_selection') {
        cleanup(probes.solo_cleanup, { work_budget_seconds: 145, stop_budget_seconds: 25, outer_budget_seconds: 180, client_released: true, bindings_restored: true }, 'Solo');
        const mode = step.name === 'time_run' ? 'TimeRun' : 'Freestyle', data = probes['solo_' + mode];
        const readiness = probes['solo_render_readiness_' + mode];
        assert(readiness && Number.isFinite(readiness.expected_markers) && Number.isInteger(readiness.expected_markers) && readiness.expected_markers >= 0,
          'Exact mode guide readiness is missing/invalid: ' + mode);
        assert(Number.isFinite(readiness.rendered_markers) && Number.isInteger(readiness.rendered_markers), 'Rendered guide count is missing/invalid');
        assert.equal(readiness.rendered_markers, readiness.expected_markers, 'Rendered guide count differs from the mode plan');
        if (mode === 'TimeRun') assert(readiness.expected_markers > 0, 'TimeRun needs an active planned guide');
        assert(Number.isFinite(readiness.camera_frames) && Number.isInteger(readiness.camera_frames) && readiness.camera_frames >= 3, 'Guide readiness lacks fresh camera frames');
        assert(Number.isFinite(readiness.canonical_ticks) && Number.isInteger(readiness.canonical_ticks) && readiness.canonical_ticks > 5, 'Guide readiness lacks advancing canonical ticks');
        assert.equal(readiness.guide_fault, '', 'Guide readiness reports a fault or lacks fault observation');
        assert(data && shaPattern.test(data.source) && shaPattern.test(data.manifest), 'Installed flagship identities missing');
        assert.equal(data.mode, mode); assert.equal(data.physical_sites, 72); assert.equal(data.humans, 1); assert.equal(data.runner, 'Single');
        assert(data.actual_camera_frames >= 3 && data.canonical_ticks_observed > 5, 'Fresh physical/camera observation missing');
        assert(Number.isInteger(data.journal_entries) && data.journal_entries >= 0, 'Journal count observation missing/invalid');
        assert.equal(data.return_label, 'Return to lobby');
      }
    }
    for (let index = 0; index < names.length; index++) {
      const info = probes[names[index] + '_capture'];
      assert(info && info.focused === true && Number.isInteger(info.rendered_frame) && info.rendered_frame > 0, 'Capture frame/focus proof missing');
      if (scenario === 'lobby_experience') assert.equal(info.varied_pixels, true);
      const size = pngSize(under(root, files[index]));
      assert.deepEqual(size, { width: info.width, height: info.height }, 'PNG/observed dimensions differ');
      assert(size.width >= 640 && size.height >= 360, 'Capture unexpectedly small');
      captures.push({ ...await proof(root, files[index]), ...size, step: step.name, frame: info.rendered_frame });
    }
  }
  const flat = Object.values(expected[scenario]).flat().map(name => slash(path.relative(root, dir)) + '/' + name + '.png');
  assert.deepEqual(checks.captures.map(item => slash(item.path)), flat, 'Top-level capture list differs');
  assert.equal(new Set(flat).size, flat.length);
  return { id: runId, scenario, status: 'PASS', gate: await proof(root, gateFile), gateResult: gate,
    log: await proof(root, gate.log), checks: await proof(root, checksFile), stepResults: checks.steps,
    sourceBinding: { buildId: checks.build_id, embeddedCommit: checks.commit ?? null, embeddedTree: checks.tree ?? null }, captures };
}

export async function verifyDelivery(options) {
  const required = ['root', 'build', 'source-commit', 'build-gate', 'lobby-run', 'lobby-gate', 'solo-run', 'solo-gate', 'preservation', 'out'];
  for (const key of required) assert(options[key], 'Missing --' + key);
  for (const key of Object.keys(options)) assert(required.includes(key), 'Unknown option --' + key);
  const root = fs.realpathSync(options.root), out = under(root, options.out);
  assert(!fs.existsSync(out), 'Refusing to overwrite output: ' + out);
  assert(/^\d{8}T\d{6}Z$/.test(options.build), 'Invalid exact build ID');
  assert(/^[a-f0-9]{40}$/.test(options['source-commit']), 'Full source commit required');
  const git = args => execFileSync('git', args, { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  const source = options['source-commit'];
  assert.equal(git(['rev-parse', '--verify', source + '^{commit}']), source);
  const productionRoots = ['Assets', 'Packages', 'ProjectSettings'];
  assert.equal(git(['diff', '--name-only', source, '--', ...productionRoots]), '', 'Production files differ from the built source commit');
  assert.equal(git(['ls-files', '--others', '--exclude-standard', '--', ...productionRoots]), '', 'Untracked production files are not bound to the source commit');
  const buildDir = under(root, 'Builds/qa-' + options.build), buildFile = path.join(buildDir, 'build.json');
  const build = read(buildFile), buildGateFile = under(root, options['build-gate']), buildGate = lastGate(buildGateFile);
  assert.equal(build.id, options.build); assert.equal(build.head, source); assert.equal(build.ok, true);
  assert.equal(build.kind, 'release-qa'); assert.equal(build.development, false); assert(shaPattern.test(build.tree), 'Missing harness metadata freshness fingerprint');
  assert.equal(slash(build.dir), slash(path.relative(root, buildDir))); assert.equal(build.exe, 'Wreckabulary.exe');
  assert.deepEqual(buildGate.build, build, 'Build gate and build receipt differ');
  const packedFile = path.join(buildDir, 'packed_assets.json'), packed = read(packedFile);
  for (const [key, value] of Object.entries({ build_id: build.id, kind: build.kind, kind_source: 'binary', gsk_qa: true,
    development: false, unity_version: build.unity_version, profile: build.profile, output_dir: build.dir })) assert.equal(packed[key], value, 'Packed build mismatch: ' + key);
  assert(Array.isArray(packed.files) && packed.files.length > 0, 'Missing packed file inventory');
  const artifacts = [], seen = new Set();
  for (const item of packed.files) {
    const file = under(buildDir, item.path), relative = slash(path.relative(root, file));
    assert(!seen.has(relative.toLowerCase()), 'Duplicate packed artifact'); seen.add(relative.toLowerCase());
    const record = await proof(root, file); assert.equal(record.bytes, item.size, 'Packed artifact size changed: ' + relative);
    artifacts.push(record);
  }
  const executable = await proof(root, path.join(buildDir, build.exe));
  const assemblyNames = ['Wreckabulary.dll', 'Wreckabulary.Online.Client.dll', 'Wreckabulary.Wordbound.Qa.dll'];
  for (const name of assemblyNames) assert(artifacts.some(item => item.path.endsWith('/Managed/' + name)), 'Packed managed code absent: ' + name);
  const preservationFile = under(root, options.preservation), preservation = read(preservationFile);
  assert.equal(preservation.status, 'PASS', 'Preservation verifier did not pass');
  assert.deepEqual(preservation.changed, []);
  assert(Number.isInteger(preservation.files) && preservation.files > 0, 'Preservation verified file count missing');
  const baselineFile = under(root, preservation.baseline), baselineProof = await proof(root, baselineFile), baseline = read(baselineFile);
  assert(shaPattern.test(preservation.baseline_sha256), 'Preservation baseline fingerprint missing');
  assert.equal(baselineProof.sha256, preservation.baseline_sha256, 'Preservation baseline changed');
  assert.equal(baseline.head, source, 'Preservation baseline belongs to another source commit');
  assert.equal(preservation.scope, baseline.scope, 'Preservation scope differs from its baseline');
  assert(Array.isArray(baseline.files) && baseline.files.length === preservation.files, 'Preservation inventory/count differs');
  const preserved = [], preservedPaths = new Set();
  for (const item of baseline.files) {
    assert(shaPattern.test(item.sha256), 'Preservation reference hash missing');
    const record = await proof(root, item.path);
    assert(!preservedPaths.has(record.path.toLowerCase()), 'Duplicate preservation path'); preservedPaths.add(record.path.toLowerCase());
    assert.equal(record.sha256, item.sha256, 'Authored file no longer matches preservation receipt: ' + item.path);
    preserved.push(record);
  }
  const runs = [];
  runs.push(await verifyRun(root, options['lobby-run'], options['lobby-gate'], build, 'lobby_experience'));
  runs.push(await verifyRun(root, options['solo-run'], options['solo-gate'], build, 'wordbound_solo'));
  assert.equal(runs.reduce((sum, run) => sum + run.captures.length, 0), 29);
  const receipt = { schema: 1, utc: new Date().toISOString(), status: 'BOUNDED_LOBBY_AND_SOLO_WINDOWS_PASS', build,
    source: { commit: source, currentHead: git(['rev-parse', 'HEAD']), productionRoots, productionDiffEmpty: true, untrackedNonIgnoredProductionEmpty: true,
      scope: 'Tracked Git diff and non-ignored untracked files only. Ignored production inputs are excluded. build.tree is a path/size/mtime metadata freshness fingerprint, not a content hash.' },
    verifier: await proof(root, fileURLToPath(import.meta.url)), buildReceipt: await proof(root, buildFile),
    buildGate: await proof(root, buildGateFile), buildGateResult: buildGate, buildLog: await proof(root, buildGate.log),
    executable, packedManifest: await proof(root, packedFile), buildArtifacts: artifacts, runs,
    preservation: { receipt: await proof(root, preservationFile), baseline: baselineProof, originalScope: baseline.scope, rehashedFiles: preserved,
      scope: 'Only the explicit disk files listed here. Solo smoke separately checks loaded launch/camera/visual/UI/story objects and item/rule text, selected theme, typed preferences and input binding restoration. These runtime checks are not additional disk baseline hashes.' },
    scope: { captures: 29, lobbySteps: 5, soloSteps: 3, visualAcceptance: 'PENDING independent inspection of these exact images',
      limits: [
        'Current binary hashes and exact build IDs/gates are recorded. The smoke format has no per-run executable hash; historic byte identity is not independently proved.',
        'PNG headers, endings and observed dimensions/focus are verified; image decode/visual quality still requires the actual images and reviewer.',
        'Solo proves installed flagship TimeRun/Freestyle launch, fresh simulation/camera, an observed journal count (which may be zero), and actual registered return handler. It does not prove story-introduction delivery, full completion or physical keyboard/controller interaction.',
        'Lobby proves selection/navigation including the Wordbound creator card. It does not launch the creator or touch user autosaves.',
        'Online frame is the actual frontend only. No fresh Connect, public/private matchmaking, complete network match, team/bot bridge or production release acceptance is claimed.',
        'Runtime cleanup receipts apply while the player loop advances. Process termination or a frame exceeding the external deadline is outside this guarantee.'
      ] } };
  fs.writeFileSync(out, JSON.stringify(receipt, null, 2) + '\n', { flag: 'wx' });
  return { output: slash(path.relative(root, out)), status: receipt.status, build: build.id, captures: 29 };
}

export function argumentsFrom(argv) {
  assert.equal(argv.length % 2, 0, 'Use explicit --name value pairs');
  const options = {};
  for (let index = 0; index < argv.length; index += 2) {
    assert(argv[index].startsWith('--') && argv[index + 1], 'Use explicit --name value pairs');
    const key = argv[index].slice(2); assert(!has(options, key), 'Duplicate option --' + key); options[key] = argv[index + 1];
  }
  return options;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { console.log(JSON.stringify(await verifyDelivery(argumentsFrom(process.argv.slice(2))))); }
  catch (error) { console.error('Delivery evidence rejected: ' + error.message); process.exitCode = 1; }
}

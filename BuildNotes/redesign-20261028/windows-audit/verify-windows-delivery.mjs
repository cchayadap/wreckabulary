import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import os from 'node:os';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

export const expected = {
  lobby_experience: {
    local_choices: ['lobby_local_dibs', 'lobby_local_duos', 'lobby_local_escape', 'lobby_solo_timerun', 'lobby_solo_freestyle', 'lobby_solo_two_seats'],
    create_choices: ['lobby_create_learn', 'lobby_create_build', 'lobby_create_wordbound'],
    customize_and_shop: ['lobby_customize', 'lobby_customize_finishes', 'lobby_customize_equipment', 'lobby_shop'],
    settings_navigation: ['lobby_settings_profile', 'lobby_settings_audio', 'lobby_settings_gameplay', 'lobby_settings_graphics', 'lobby_settings_controls', 'lobby_settings_controls_bottom', 'lobby_settings_wordbound_controls_mid', 'lobby_settings_wordbound_controls_bottom', 'lobby_settings_help', 'lobby_home_returned'],
    open_wordbound: ['lobby_online_ready']
  },
  wordbound_solo: {
    solo_selection: ['solo_initial_lobby'],
    time_run: ['solo_timerun_selected', 'solo_timerun_world', 'solo_timerun_menu', 'solo_timerun_returned'],
    freestyle: ['solo_freestyle_selected', 'solo_freestyle_world', 'solo_freestyle_menu', 'solo_freestyle_returned'],
    parcel_hall: ['solo_parcelhall_freestyle_selected', 'solo_parcelhall_freestyle_world', 'solo_parcelhall_freestyle_menu', 'solo_parcelhall_freestyle_returned']
  },
  wordbound_workshop_flow: {
    open_wordbound: ['wordbound_party'],
    edit_workshop: ['wordbound_workshop', 'wordbound_workshop_range_height', 'wordbound_workshop_range_floors'],
    exact_playtest: ['wordbound_local_ready', 'wordbound_local_world', 'wordbound_equipment_menu'],
    return_to_workshop: ['wordbound_returned_workshop'],
    undo_redo_preserved: []
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

function requireFields(value, fields, expectedValue, label) {
  for (const key of fields) assert.equal(value?.[key], expectedValue, label + ': ' + key);
}

function workshopCapture(checks, step, name) {
  const labels = { wordbound_party: 'party', wordbound_workshop: 'workshop', wordbound_local_ready: 'local_ready',
    wordbound_workshop_range_height: 'wordbound_workshop_range_height', wordbound_workshop_range_floors: 'wordbound_workshop_range_floors' };
  const label = labels[name], probes = step.probes;
  const first = checks.steps[0].probes;
  const info = { width: first.party_capture_width, height: first.party_capture_height,
    scope: 'Same-window dimensions from party observation; no per-image frame/focus/pixel probe is recorded' };
  if (label) {
    info.width = probes[label + '_capture_width']; info.height = probes[label + '_capture_height'];
    assert.equal(probes[label + '_capture_varied_rgb'], true, 'Workshop blank/uniform capture: ' + name);
    for (const key of ['nonblack_pixels', 'nonzero_alpha_pixels']) {
      const count = probes[label + '_capture_' + key];
      assert(Number.isInteger(count) && count > 0 && count <= info.width * info.height, 'Invalid workshop pixel observation: ' + name);
    }
    info.scope = 'Per-capture dimensions and decoded nonblank/varied pixel observation; no per-image frame/focus probe';
  }
  return info;
}

async function verifyWorkshop(root, dir, checks) {
  const [open, edit, play, returned, undo] = checks.steps.map(step => step.probes);
  requireFields(open, ['party_frame_focused', 'party_frame_splash_finished', 'party_frame_menu_active',
    'party_frame_menu_visible', 'party_frame_camera_active', 'party_frame_ready'], true, 'Workshop party readiness');
  assert.equal(open.party_frame_camera_target, 'none'); assert.equal(open.party_frame_camera_display, 0);
  assert(open.party_frame_repaints_after_splash >= 2 && open.party_frame_camera_callbacks >= 1);
  requireFields(edit, ['workshop_autosave_verified', 'workshop_edit_completed', 'workshop_autosave_exact',
    'workshop_autosave_draft_unchanged', 'workshop_ranges_fields_restored', 'workshop_ranges_history_same',
    'workshop_ranges_snapshot_same', 'workshop_ranges_revision_same', 'workshop_ranges_undo_redo_same',
    'workshop_ranges_primary_same', 'workshop_ranges_manifest_same', 'workshop_ranges_preserved'], true, 'Workshop preservation');
  assert.equal(edit.workshop_autosave_used_backup, false);
  assert.equal(edit.workshop_autosave_temporary_files, 0); assert.deepEqual(edit.workshop_ranges_cleanup_errors, []);
  assert(shaPattern.test(edit.manifest_hash));
  assert.equal(edit.seed, '0314159265358979'); assert.equal(edit.workshop_recovered_seed, edit.seed);
  const evidence = slash(path.relative(root, dir)) + '/workshop-autosave.json';
  assert.equal(slash(edit.workshop_autosave_evidence), evidence);
  const saved = read(under(root, evidence)), savedProof = await proof(root, evidence);
  assert.equal(savedProof.bytes, edit.workshop_autosave_bytes);
  assert.equal(saved.format, 'wreckabulary.workshop'); assert.equal(saved.schema, 1);
  assert.equal(saved.seed, edit.seed); assert.equal(saved.seedLocked, true);
  const course = JSON.parse(saved.course);
  assert.equal(course.id, 'wordbound-lost-address'); assert.equal(course.title, 'Wordbound playtest draft');
  const zone = course.zones.find(item => item.id === edit.workshop_ranges_zone);
  assert(zone && zone.moduleId === edit.workshop_ranges_owner, 'Range view does not match saved owner/zone');
  assert.equal(saved.activeLayer, edit.workshop_ranges_active_floor);
  assert.deepEqual(edit.workshop_ranges_height_mm, [zone.bounds.min[1], zone.bounds.max[1]]);
  assert.deepEqual(edit.workshop_ranges_yaws, zone.yaws);
  assert.deepEqual(edit.workshop_ranges_allowed_floors, zone.layerIds);
  for (const name of ['wordbound_workshop_range_height', 'wordbound_workshop_range_floors']) {
    assert(Number.isFinite(edit[name + '_scroll_actual']) && edit[name + '_scroll_actual'] > 0);
    assert.equal(edit[name + '_scroll_requested'], edit[name + '_scroll_actual'], 'Range scroll did not reach requested position');
  }
  const storage = under(path.resolve(os.tmpdir(), 'wbqa'), edit.workshop_storage_folder);
  assert(/^[a-f0-9]{32}$/.test(path.basename(storage)), 'Workshop storage is not isolated QA temporary storage');
  const primary = under(storage, 'templates/Flagship/wordbound-lost-address.autosave.json');
  assert.equal(path.resolve(edit.workshop_autosave_primary), primary, 'Unexpected workshop primary path');
  const primaryProof = await proof(storage, primary);
  assert.equal(primaryProof.sha256, savedProof.sha256, 'Current primary differs from captured autosave');
  assert.deepEqual(fs.readdirSync(path.dirname(primary)).filter(name => name.startsWith(path.basename(primary) + '.') && name.endsWith('.tmp')), []);
  requireFields(play, ['playtest_has_client', 'playtest_has_controller', 'playtest_controller_active', 'playtest_has_frame',
    'playtest_has_world', 'playtest_ready_condition', 'playtest_camera_exists', 'playtest_camera_active',
    'playtest_menu_open', 'playtest_focused', 'exact_manifest', 'draft_unchanged'], true, 'Workshop playtest');
  requireFields(play, ['playtest_paused', 'playtest_gameplay_fault', 'playtest_guide_fault', 'playtest_presentation_fault'], false, 'Workshop playtest');
  assert.equal(play.playtest_observation, 'playtest_returned'); assert.equal(play.playtest_stage, 'completed');
  assert.equal(play.playtest_post_ready_waited_frames, 90); assert.equal(play.playtest_time_scale, 1);
  assert(play.playtest_ready_frame > play.playtest_start_frame && play.playtest_current_frame >= play.playtest_ready_frame + 90);
  assert(play.playtest_canonical_tick > play.playtest_ready_canonical_tick && play.playtest_ready_canonical_tick > 0);
  assert(play.playtest_marker_count >= 1 && play.markers >= 1 && play.playtest_visual_count > 0);
  requireFields(returned, ['same_history', 'draft_unchanged', 'client_destroyed', 'camera_destroyed', 'editor_visible'], true, 'Workshop return');
  assert.equal(undo.undo_redo_exact, true);
}

export async function verifyRun(root, directory, gatePath, build, scenario) {
  const dir = under(root, directory), runId = path.basename(dir);
  assert(new RegExp('^\\d{8}T\\d{6}Z-' + scenario + '$').test(runId), 'Invalid explicit run directory');
  assert.equal(slash(path.relative(root, dir)), 'Docs/qa/runs/' + runId, 'Run must be its canonical Docs/qa/runs directory');
  const gateFile = under(root, gatePath), gate = lastGate(gateFile), smoke = gate.smoke;
  assert.equal(gate.cmd, `smoke --build ${build.id} --scenario ${scenario}`, 'Smoke command identity differs');
  assert.equal(slash(path.relative(root, under(root, gate.log))), 'Logs/player/' + runId + '.log', 'Player log is not bound to the selected run');
  assert(smoke, 'Missing smoke result');
  assert.equal(smoke.run_id, runId); assert.equal(smoke.scenario, scenario);
  assert.equal(slash(smoke.build), slash(build.dir)); assert.equal(smoke.kind, build.kind);
  assert.equal(smoke.stripped, false); assert.equal(smoke.exit, 0); assert.equal(smoke.timed_out, false);
  const checksFile = path.join(dir, 'checks.json'), checks = read(checksFile);
  assert(gate.evidence.includes(slash(path.relative(root, checksFile))), 'Gate does not list exact checks receipt');
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
  const captures = [], roster = [];
  if (scenario === 'wordbound_workshop_flow') await verifyWorkshop(root, dir, checks);
  for (const step of checks.steps) {
    assert.equal(step.status, 'PASS', 'Failed/missing step: ' + step.name);
    assert.deepEqual(step.assert_failures, [], 'Missing/nonempty assertion list: ' + step.name);
    const names = expected[scenario][step.name];
    const files = names.map(name => slash(path.relative(root, dir)) + '/' + name + '.png');
    const stepCaptures = !has(step, 'captures') && names.length === 0 ? [] : step.captures;
    assert(Array.isArray(stepCaptures), 'Missing, extra or reordered captures: ' + step.name);
    assert.deepEqual(stepCaptures.map(slash), files, 'Missing, extra or reordered captures: ' + step.name);
    const probes = step.probes;
    if (scenario !== 'wordbound_workshop_flow')
      assert.equal(probes?.[scenario === 'lobby_experience' ? 'navigation_preferences_unchanged' : 'solo_source_preferences_preserved'], true, 'Preservation not proved: ' + step.name);
    if (step.name === 'local_choices') {
      cleanup(probes.local_owned_stack_cleanup, { work_budget_seconds: 25, outer_budget_seconds: 30 }, 'Local');
      assert.deepEqual(probes.local_owned_stack_cleanup.errors, []);
      for (const mode of ['Dibs', 'Duos', 'MovingOut', 'MovingDay', 'Escape']) {
        const observation = probes['local_' + mode];
        assert.equal(observation?.humans, 1, 'Lobby human roster differs: ' + mode);
        assert.equal(observation.selected, 'Mode ' + mode);
        assert.equal(typeof observation.map, 'string'); assert(observation.map.length > 0);
        assert.equal(typeof observation.summary, 'string');
        assert.equal(observation.summary.toLowerCase().includes('bot'), mode === 'Dibs' || mode === 'Duos');
        roster.push({ mode, humans: observation.humans, scope: 'Lobby selection observation; bots are a displayed mode promise, not spawned bot evidence' });
      }
    }
    if (step.name === 'create_choices') {
      cleanup(probes.create_owned_stack_cleanup, { work_budget_seconds: 15, outer_budget_seconds: 20 }, 'Create');
      assert.deepEqual(probes.create_owned_stack_cleanup.errors, []);
    }
    if (step.name === 'settings_navigation') {
      const preservation = probes.controls_legend_bindings_preserved;
      assert(preservation, 'Control legend binding preservation proof missing');
      for (const key of ['shared_source_same', 'shared_map_same', 'overrides_unchanged', 'version_unchanged',
        'mouse_settings_unchanged', 'map_enabled_unchanged', 'restored_house_legend'])
        assert.equal(preservation[key], true, 'Control legend changed or omitted ' + key);
      assert(Number.isInteger(preservation.before_version) && preservation.before_version >= 0, 'Control legend binding version missing/invalid');
      assert.equal(preservation.after_version, preservation.before_version, 'Control legend binding version changed');
      assert.equal(preservation.listening, false, 'Control legend left a rebind active');
      const views = [
        ['mid', 'Rebind Spell a word', [
          ['Aim', 'Aim (hold)', 'RB / R1'], ['Jump', 'Jump', 'A / Cross'],
          ['Interact', 'Pick up / use', 'RT / R2'], ['Spell', 'Open crafting', 'Y / Triangle']]],
        ['bottom', 'Rebind Pause', [
          ['Hand 1', 'Equipment 1', '—'], ['Hand 2', 'Equipment 2', '—'],
          ['Bag', 'Change equipment', 'R3 cycles gear'], ['Pause', 'Menu', 'Start / Options']]]
      ];
      for (const [position, selected, rows] of views) {
        const data = probes['controls_legend_Wordbound_' + position];
        assert(data, 'Wordbound control legend view proof missing: ' + position);
        assert.equal(data.family, 'Wordbound', 'Control legend family differs');
        assert.equal(data.header_text, 'WORDBOUND CONTROLS', 'Visible control legend header missing/incorrect');
        assert.equal(data.capture, 'lobby_settings_wordbound_controls_' + position, 'Control legend capture identity differs');
        assert.equal(data.selected_control, selected, 'Control legend selected row differs');
        assert.equal(data.active_binding_count, 14, 'Wordbound must keep its supported numbered equipment rows');
        assert.equal(data.listening, false, 'Control legend view began rebinding');
        assert.equal(data.input_lease_active, true, 'Control legend view lost its input lease');
        assert.deepEqual(data.rows, rows.map(([action, label, controller]) => ({ action, label, controller, fully_visible: true })),
          'Control legend row labels, controller mappings or visibility differ: ' + position);
      }
    }
    if (step.name === 'open_wordbound' && scenario === 'lobby_experience') {
      assert.equal(probes.party_frame_ready, true);
      assert.equal(probes.party_frame_focused, true);
      assert(Array.isArray(probes.preserved_local_selection) && probes.preserved_local_selection.length === 3, 'Online local-selection proof missing');
    }
    if (scenario === 'wordbound_solo') {
      assert(!has(probes, 'solo_abandoned_cleanup'), 'Abandoned solo cleanup is not confirmed cleanup');
      if (step.name !== 'solo_selection') {
        cleanup(probes.solo_cleanup, { work_budget_seconds: 145, stop_budget_seconds: 25, outer_budget_seconds: 180, client_released: true, bindings_restored: true }, 'Solo');
        const mode = step.name === 'time_run' ? 'TimeRun' : 'Freestyle';
        const tag = (step.name === 'parcel_hall' ? 'parcelhall_' : '') + mode.toLowerCase();
        const data = probes['solo_' + tag], readiness = probes['solo_render_readiness_' + tag];
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
        const input = probes['solo_input_' + tag], released = probes['solo_input_released_' + tag];
        assert(input && ['DesktopBinding', 'GamepadBinding'].includes(input.source), 'Solo invoking binding proof missing');
        assert.equal(input.borrowed_exact_source, true);
        assert.equal(typeof input.craft_hint, 'string'); assert(input.craft_hint.length > 0);
        assert.equal(typeof input.menu_hint, 'string'); assert(input.menu_hint.length > 0);
        assert.equal(released?.wrapper_disposed, true); assert.equal(released.source_preserved, true);
        roster.push({ mode, map: step.name === 'parcel_hall' ? 'ParcelHall' : 'LostAddress', humans: data.humans,
          runner: data.runner, source: data.source, manifest: data.manifest, input });
      }
    }
    for (let index = 0; index < names.length; index++) {
      const info = scenario === 'wordbound_workshop_flow' ? workshopCapture(checks, step, names[index]) : probes[names[index] + '_capture'];
      if (scenario !== 'wordbound_workshop_flow')
        assert(info && info.focused === true && Number.isInteger(info.rendered_frame) && info.rendered_frame > 0, 'Capture frame/focus proof missing');
      if (scenario === 'lobby_experience') assert.equal(info.varied_pixels, true);
      const size = pngSize(under(root, files[index]));
      assert.deepEqual(size, { width: info.width, height: info.height }, 'PNG/observed dimensions differ');
      assert(size.width >= 640 && size.height >= 360, 'Capture unexpectedly small');
      captures.push({ ...await proof(root, files[index]), ...size, step: step.name, frame: info.rendered_frame ?? null,
        observation: scenario === 'wordbound_workshop_flow' ? info.scope : 'Per-capture dimensions, focus and rendered frame' });
    }
  }
  const flat = Object.values(expected[scenario]).flat().map(name => slash(path.relative(root, dir)) + '/' + name + '.png');
  assert.deepEqual(checks.captures.map(item => slash(item.path)), flat, 'Top-level capture list differs');
  assert.deepEqual(checks.captures.map(item => item.name), Object.values(expected[scenario]).flat());
  assert(checks.captures.every(item => item.kind === 'png' && item.camera === 'screen'), 'Wrong capture kind/camera');
  assert.deepEqual(fs.readdirSync(dir).filter(name => name.toLowerCase().endsWith('.png')).sort(),
    Object.values(expected[scenario]).flat().map(name => name + '.png').sort(), 'Unexpected or missing PNG files in exact run');
  assert.equal(new Set(flat).size, flat.length);
  const logText = fs.readFileSync(under(root, gate.log), 'utf8');
  assert(logText.includes('[GSK] QA ' + scenario + ' PASS'), 'Player log lacks exact scenario completion');
  assert(!/^(?:Crash!!!|Fatal error|Unhandled Exception|[A-Za-z]+Exception:)/m.test(logText), 'Player log reports a crash/exception');
  return { id: runId, scenario, status: 'PASS', gate: await proof(root, gateFile), gateResult: gate,
    log: await proof(root, gate.log), checks: await proof(root, checksFile), stepResults: checks.steps,
    sourceBinding: { buildId: checks.build_id, embeddedCommit: checks.commit ?? null, embeddedTree: checks.tree ?? null },
    roster, rosterScope: scenario === 'wordbound_workshop_flow' ? 'Workshop receipt has no independent roster fields; no roster claim' : 'Observed human seats only',
    startedUtc: checks.started_utc, endedUtc: checks.ended_utc, captures };
}

export async function verifyDelivery(options) {
  const required = ['root', 'build', 'source-commit', 'build-gate', 'lobby-run', 'lobby-gate', 'solo-run', 'solo-gate', 'workshop-run', 'workshop-gate', 'preservation', 'out'];
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
  assert.equal(buildGate.cmd, 'build --profile release-qa');
  assert.equal(build.id, options.build); assert.equal(build.head, source); assert.equal(build.ok, true);
  assert.equal(build.kind, 'release-qa'); assert.equal(build.development, false); assert(shaPattern.test(build.tree), 'Missing harness metadata freshness fingerprint');
  assert.equal(slash(build.dir), slash(path.relative(root, buildDir))); assert.equal(build.exe, 'Wreckabulary.exe');
  assert.deepEqual(buildGate.build, build, 'Build gate and build receipt differ');
  for (const name of ['build.json', 'packed_assets.json']) assert(buildGate.evidence.includes(slash(build.dir) + '/' + name), 'Build gate evidence missing ' + name);
  const packedFile = path.join(buildDir, 'packed_assets.json'), packed = read(packedFile);
  for (const [key, value] of Object.entries({ build_id: build.id, kind: build.kind, kind_source: 'binary', gsk_qa: true,
    development: false, unity_version: build.unity_version, profile: build.profile, output_dir: build.dir })) assert.equal(packed[key], value, 'Packed build mismatch: ' + key);
  assert(Array.isArray(packed.files) && packed.files.length > 0, 'Missing packed file inventory');
  const artifacts = [], seen = new Set();
  for (const item of packed.files) {
    const file = under(buildDir, item.path), relative = slash(path.relative(root, file));
    assert(!seen.has(relative.toLowerCase()), 'Duplicate packed artifact'); seen.add(relative.toLowerCase());
    const record = await proof(root, file); assert.equal(record.bytes, item.size, 'Packed artifact size changed: ' + relative);
    if (has(item, 'sha256')) assert.equal(record.sha256, item.sha256, 'Packed artifact recorded content hash changed: ' + relative);
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
  assert(/^[a-f0-9]{40}$/.test(baseline.head), 'Preservation baseline source commit missing');
  git(['merge-base', '--is-ancestor', baseline.head, source]);
  assert.equal(preservation.scope, baseline.scope, 'Preservation scope differs from its baseline');
  assert(Array.isArray(baseline.files) && baseline.files.length === preservation.files, 'Preservation inventory/count differs');
  const preservedPaths = new Set();
  for (const item of baseline.files) {
    assert(shaPattern.test(item.sha256), 'Preservation reference hash missing');
    assert.equal(typeof item.path, 'string');
    const relative = path.relative(root, path.resolve(root, item.path));
    assert(relative && !relative.startsWith('..') && !path.isAbsolute(relative), 'Preservation inventory path escapes root');
    const file = slash(relative);
    assert(!preservedPaths.has(file.toLowerCase()), 'Duplicate preservation path'); preservedPaths.add(file.toLowerCase());
  }
  const runs = [];
  runs.push(await verifyRun(root, options['lobby-run'], options['lobby-gate'], build, 'lobby_experience'));
  runs.push(await verifyRun(root, options['solo-run'], options['solo-gate'], build, 'wordbound_solo'));
  runs.push(await verifyRun(root, options['workshop-run'], options['workshop-gate'], build, 'wordbound_workshop_flow'));
  assert.equal(runs.reduce((sum, run) => sum + run.captures.length, 0), 45);
  assert(Number.isFinite(Date.parse(preservation.utc)) && runs.every(run => Date.parse(preservation.utc) >= Date.parse(run.endedUtc)),
    'Final preservation receipt must follow all three selected runs');
  const scenarioSources = [];
  for (const file of ['LobbyExperienceQa.cs', 'WordboundSoloQa.cs', 'WordboundWorkshopQa.cs'])
    scenarioSources.push(await proof(root, 'Assets/_Project/Scripts/WordboundQa/' + file));
  const receipt = { schema: 2, utc: new Date().toISOString(), status: 'BOUNDED_LOBBY_SOLO_WORKSHOP_WINDOWS_PASS', build,
    source: { commit: source, currentHead: git(['rev-parse', 'HEAD']), productionRoots, productionDiffEmpty: true, untrackedNonIgnoredProductionEmpty: true,
      scope: 'Tracked Git diff and non-ignored untracked files only. Ignored production inputs are excluded. build.tree is a path/size/mtime metadata freshness fingerprint, not a content hash.' },
    verifier: await proof(root, fileURLToPath(import.meta.url)), buildReceipt: await proof(root, buildFile),
    buildGate: await proof(root, buildGateFile), buildGateResult: buildGate, buildLog: await proof(root, buildGate.log),
    executable, packedManifest: await proof(root, packedFile), buildArtifacts: artifacts, scenarioSources, runs,
    preservation: { receipt: await proof(root, preservationFile), baseline: baselineProof, originalScope: baseline.scope,
      verifiedFileCount: preservation.files, baselineCommit: baseline.head, baselineIsAncestorOfBuild: true, receiptUtc: preservation.utc,
      scope: 'Root final PASS receipt is referenced and bound to its baseline hash/count/scope. Its listed authored files are not rehashed again by this audit. Runtime preservation probes are checked separately.' },
    scope: { captures: 45, lobbySteps: 5, soloSteps: 4, workshopSteps: 5, visualAcceptance: 'PENDING independent inspection of these exact images',
      limits: [
        'Current binary hashes and exact build IDs/gates are recorded. The smoke format has no per-run executable hash; historic byte identity is not independently proved.',
        'PNG headers, endings and observed dimensions/focus are verified; image decode/visual quality still requires the actual images and reviewer.',
        'Solo proves installed Lost Address TimeRun/Freestyle and separate Parcel Hall Freestyle launch, one-human Single runner, fresh simulation/camera, journal count observation, borrowed binding release and actual return handler. It does not prove full completion or physical controls.',
        'Lobby proves selection/navigation including the Wordbound creator card and actual House/Wordbound control legend selectors, with original shared bindings preserved. The two Wordbound views prove row visibility and recorded mode-specific labels; they do not prove physical controller operation. It does not launch the creator or touch user autosaves.',
        'Online frame is the actual frontend only. No fresh Connect, public/private matchmaking, complete network match, team/bot bridge or production release acceptance is claimed.',
        'Workshop validates exact edit/autosave readback, range-view preservation, live playtest, return and undo/redo. Three workshop PNGs have no per-image pixel/frame/focus probe; dimensions and bytes are verified against the same presented window, and this absence is recorded.',
        'Runtime cleanup receipts apply while the player loop advances. Process termination or a frame exceeding the external deadline is outside this guarantee.'
      ] } };
  fs.writeFileSync(out, JSON.stringify(receipt, null, 2) + '\n', { flag: 'wx' });
  return { output: slash(path.relative(root, out)), status: receipt.status, build: build.id, captures: 45 };
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

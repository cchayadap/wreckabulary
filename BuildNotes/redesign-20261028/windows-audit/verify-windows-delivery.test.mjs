import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import os from 'node:os';
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
  for (const name of ['LobbyExperienceQa.cs', 'WordboundSoloQa.cs', 'WordboundWorkshopQa.cs'])
    write('Assets/_Project/Scripts/WordboundQa/' + name, 'SYNTHETIC SOURCE IDENTITY FIXTURE; NOT GAME CODE\n');
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
  json('build-gate.txt', { ...gate, cmd: 'build --profile release-qa', build, log: 'build.log', evidence: [build.dir + '/build.json', build.dir + '/packed_assets.json'] });
  json('preservation-before.json', { head: commit, scope: 'SYNTHETIC preservation only', files: [{ path: 'Assets/Presentation.asset', sha256: digest(fs.readFileSync(path.join(root, 'Assets/Presentation.asset'))) }] });
  json('preservation.json', { status: 'PASS', utc: '2099-01-01T00:03:00Z', changed: [], files: 1, baseline: 'preservation-before.json',
    baseline_sha256: digest(fs.readFileSync(path.join(root, 'preservation-before.json'))), scope: 'SYNTHETIC preservation only' });
  const options = { root, build: build.id, 'source-commit': commit, 'build-gate': 'build-gate.txt', preservation: 'preservation.json', out: 'result.json' };
  const image = png();
  const storage = path.resolve(os.tmpdir(), 'wbqa', crypto.randomBytes(16).toString('hex'));
  for (const scenario of Object.keys(expected)) {
    const id = '20990101T000100Z-' + scenario, dir = 'Docs/qa/runs/' + id, captures = [];
    const steps = Object.entries(expected[scenario]).map(([name, names]) => {
      const files = names.map(value => dir + '/' + value + '.png');
      const probes = { [scenario === 'lobby_experience' ? 'navigation_preferences_unchanged' : 'solo_source_preferences_preserved']: true };
      if (name === 'local_choices' || name === 'create_choices') probes[name === 'local_choices' ? 'local_owned_stack_cleanup' : 'create_owned_stack_cleanup'] = {
        failure: 'none', errors: [], work_budget_seconds: name === 'local_choices' ? 25 : 15, outer_budget_seconds: name === 'local_choices' ? 30 : 20 };
      if (name === 'local_choices') for (const mode of ['Dibs', 'Duos', 'MovingOut', 'MovingDay', 'Escape'])
        probes['local_' + mode] = { humans: 1, selected: 'Mode ' + mode, map: 'synthetic', summary: mode === 'Dibs' || mode === 'Duos' ? 'Synthetic bot label' : 'Synthetic solo label' };
      if (name === 'settings_navigation') {
        probes.controls_legend_bindings_preserved = { shared_source_same: true, shared_map_same: true, overrides_unchanged: true,
          version_unchanged: true, before_version: 4, after_version: 4, mouse_settings_unchanged: true, map_enabled_unchanged: true,
          listening: false, restored_house_legend: true };
        const views = [
          ['mid', 'Rebind Spell a word', [['Aim', 'Aim (hold)', 'RB / R1'], ['Jump', 'Jump', 'A / Cross'],
            ['Interact', 'Pick up / use', 'RT / R2'], ['Spell', 'Open crafting', 'Y / Triangle']]],
          ['bottom', 'Rebind Pause', [['Hand 1', 'Equipment 1', '—'], ['Hand 2', 'Equipment 2', '—'],
            ['Bag', 'Change equipment', 'R3 cycles gear'], ['Pause', 'Menu', 'Start / Options']]]
        ];
        for (const [position, selected, rows] of views) probes['controls_legend_Wordbound_' + position] = {
          family: 'Wordbound', capture: 'lobby_settings_wordbound_controls_' + position, header_text: 'WORDBOUND CONTROLS', selected_control: selected,
          active_binding_count: 14, listening: false, input_lease_active: true,
          rows: rows.map(([action, label, controller]) => ({ action, label, controller, fully_visible: true })) };
      }
      if (name === 'open_wordbound') Object.assign(probes, { party_frame_ready: true, party_frame_focused: true, preserved_local_selection: ['Practice', 'Dibs', 'pinwheel'] });
      if (scenario === 'wordbound_solo' && name !== 'solo_selection') {
        const mode = name === 'time_run' ? 'TimeRun' : 'Freestyle';
        const tag = (name === 'parcel_hall' ? 'parcelhall_' : '') + mode.toLowerCase();
        probes['solo_render_readiness_' + tag] = { expected_markers: mode === 'TimeRun' ? 1 : 0, rendered_markers: mode === 'TimeRun' ? 1 : 0,
          camera_frames: 3, canonical_ticks: 6, guide_fault: '', scope: 'Guide count matches the current canonical mode plan. Freestyle may have no nearby or selected practice station.' };
        probes.solo_cleanup = { failure: 'none', work_budget_seconds: 145, stop_budget_seconds: 25, outer_budget_seconds: 180, client_released: true, bindings_restored: true };
        probes['solo_' + tag] = { source: 'b'.repeat(64), manifest: 'c'.repeat(64), mode, physical_sites: 72, humans: 1, runner: 'Single', actual_camera_frames: 3,
          canonical_ticks_observed: 6, journal_entries: 0, return_label: 'Return to lobby' };
        probes['solo_input_' + tag] = { source: 'DesktopBinding', borrowed_exact_source: true, craft_hint: 'Tab', menu_hint: 'Esc' };
        probes['solo_input_released_' + tag] = { wrapper_disposed: true, source_preserved: true };
      }
      if (scenario === 'wordbound_workshop_flow') {
        if (name === 'open_wordbound') Object.assign(probes, { party_frame_splash_finished: true, party_frame_menu_active: true,
          party_frame_menu_visible: true, party_frame_camera_active: true, party_frame_camera_target: 'none', party_frame_camera_display: 0,
          party_frame_repaints_after_splash: 3, party_frame_camera_callbacks: 3 });
        if (name === 'edit_workshop') {
          const primary = path.join(storage, 'templates/Flagship/wordbound-lost-address.autosave.json');
          const zone = { id: 'Props_Attic_BookSpring', moduleId: 'Attic_BookSpring', bounds: { min: [0,16000,0], max: [1000,16000,1000] }, yaws: [0,90,180,270], layerIds: ['Attic'] };
          const saved = { format: 'wreckabulary.workshop', schema: 1, seed: '0314159265358979', seedLocked: true, activeLayer: 'Attic',
            course: JSON.stringify({ id: 'wordbound-lost-address', title: 'Wordbound playtest draft', zones: [zone] }) };
          const encoded = JSON.stringify(saved); fs.mkdirSync(path.dirname(primary), { recursive: true }); fs.writeFileSync(primary, encoded);
          write(dir + '/workshop-autosave.json', encoded);
          Object.assign(probes, { workshop_autosave_verified: true, workshop_edit_completed: true, workshop_autosave_exact: true,
            workshop_autosave_draft_unchanged: true, workshop_autosave_used_backup: false, workshop_autosave_temporary_files: 0,
            workshop_ranges_cleanup_errors: [], manifest_hash: 'd'.repeat(64), seed: saved.seed, workshop_recovered_seed: saved.seed,
            workshop_autosave_evidence: dir + '/workshop-autosave.json', workshop_autosave_bytes: Buffer.byteLength(encoded),
            workshop_ranges_zone: zone.id, workshop_ranges_owner: zone.moduleId, workshop_ranges_active_floor: saved.activeLayer,
            workshop_ranges_height_mm: [16000,16000], workshop_ranges_yaws: zone.yaws, workshop_ranges_allowed_floors: zone.layerIds,
            workshop_storage_folder: storage, workshop_autosave_primary: primary });
          for (const key of ['fields_restored','history_same','snapshot_same','revision_same','undo_redo_same','primary_same','manifest_same','preserved'])
            probes['workshop_ranges_' + key] = true;
          for (const value of ['wordbound_workshop_range_height','wordbound_workshop_range_floors'])
            Object.assign(probes, { [value + '_scroll_actual']: 100, [value + '_scroll_requested']: 100 });
        }
        if (name === 'exact_playtest') {
          for (const key of ['has_client','has_controller','controller_active','has_frame','has_world','ready_condition','camera_exists','camera_active','menu_open','focused']) probes['playtest_' + key] = true;
          for (const key of ['paused','gameplay_fault','guide_fault','presentation_fault']) probes['playtest_' + key] = false;
          Object.assign(probes, { exact_manifest: true, draft_unchanged: true, playtest_observation: 'playtest_returned', playtest_stage: 'completed',
            playtest_post_ready_waited_frames: 90, playtest_time_scale: 1, playtest_start_frame: 1, playtest_ready_frame: 3,
            playtest_current_frame: 95, playtest_canonical_tick: 130, playtest_ready_canonical_tick: 20, playtest_marker_count: 1, markers: 1, playtest_visual_count: 93 });
        }
        if (name === 'return_to_workshop') for (const key of ['same_history','draft_unchanged','client_destroyed','camera_destroyed','editor_visible']) probes[key] = true;
        if (name === 'undo_redo_preserved') probes.undo_redo_exact = true;
        const labels = { wordbound_party: 'party', wordbound_workshop: 'workshop', wordbound_local_ready: 'local_ready',
          wordbound_workshop_range_height: 'wordbound_workshop_range_height', wordbound_workshop_range_floors: 'wordbound_workshop_range_floors' };
        for (const value of names) if (labels[value]) Object.assign(probes, { [labels[value] + '_capture_width']: 640,
          [labels[value] + '_capture_height']: 360, [labels[value] + '_capture_varied_rgb']: true,
          [labels[value] + '_capture_nonblack_pixels']: 100, [labels[value] + '_capture_nonzero_alpha_pixels']: 100 });
      }
      names.forEach((value, index) => { write(files[index], image); probes[value + '_capture'] = { width: 640, height: 360, focused: true, rendered_frame: 10, varied_pixels: true };
        captures.push({ name: value, path: files[index], kind: 'png', camera: 'screen' }); });
      return { name, status: 'PASS', assert_failures: [], probes, captures: files };
    });
    json(dir + '/checks.json', { schema: 1, run_id: id, build_id: build.id, scenario, platform: 'player', result: 'PASS', seed: 1,
      development_build: false, unity_version: build.unity_version, commit: null, tree: null, started_utc: '2099-01-01T00:01:00Z', ended_utc: '2099-01-01T00:02:00Z',
      console: { errors: 0, exceptions: 0, warnings: 0, first_exception: null }, steps, captures });
    write('Logs/player/' + id + '.log', 'SYNTHETIC PLAYER LOG\n[GSK] QA ' + scenario + ' PASS\n');
    json(scenario + '-gate.txt', { ...gate, cmd: `smoke --build ${build.id} --scenario ${scenario}`, evidence: [dir + '/checks.json'],
      log: 'Logs/player/' + id + '.log', smoke: { run_id: id, scenario, build: build.dir, kind: build.kind, seed: 1, stripped: false, exit: 0, timed_out: false } });
    const prefix = scenario === 'lobby_experience' ? 'lobby' : scenario === 'wordbound_solo' ? 'solo' : 'workshop'; options[prefix + '-run'] = dir; options[prefix + '-gate'] = scenario + '-gate.txt';
  }
  return { root, options, verify, read, json, write, close: () => {
    assert(path.dirname(root) === parent && path.basename(root).startsWith('.synthetic-delivery-')); fs.rmSync(root, { recursive: true, force: true });
    assert(path.dirname(storage) === path.resolve(os.tmpdir(), 'wbqa') && /^[a-f0-9]{32}$/.test(path.basename(storage)));
    if (fs.existsSync(storage)) fs.rmSync(storage, { recursive: true, force: true });
  } };
}

test('synthetic complete receipt passes with 24+13+8 captures and refuses overwrite', async () => {
  const value = await fixture();
  try {
    const result = await value.verify(value.options); assert.equal(result.captures, 45);
    const receipt = value.read('result.json'); assert.equal(receipt.runs[0].captures.length, 24); assert.equal(receipt.runs[1].captures.length, 13); assert.equal(receipt.runs[2].captures.length, 8);
    assert.equal(receipt.runs[0].sourceBinding.embeddedCommit, null); assert.equal(receipt.scope.visualAcceptance.startsWith('PENDING'), true);
    const before = fs.readFileSync(path.join(value.root, 'result.json'));
    await assert.rejects(value.verify(value.options), /overwrite/); assert.deepEqual(fs.readFileSync(path.join(value.root, 'result.json')), before);
  } finally { value.close(); }
});

for (const [name, mutate, error] of [
  ['old twenty-capture lobby', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    record.steps[3].captures = record.steps[3].captures.filter(name => !name.includes('wordbound_controls_')); value.json(file, record); }, /Missing, extra or reordered captures/],
  ['missing Wordbound legend view', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    delete record.steps[3].probes.controls_legend_Wordbound_mid; value.json(file, record); }, /legend view proof missing/],
  ['missing visible Wordbound header', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    delete record.steps[3].probes.controls_legend_Wordbound_mid.header_text; value.json(file, record); }, /Visible control legend header missing/],
  ['wrong control legend family', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    record.steps[3].probes.controls_legend_Wordbound_mid.family = 'House'; value.json(file, record); }, /legend family differs/],
  ['wrong Wordbound controller mapping', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    record.steps[3].probes.controls_legend_Wordbound_mid.rows[0].controller = 'Right stick'; value.json(file, record); }, /controller mappings or visibility differ/],
  ['clipped required legend row', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    record.steps[3].probes.controls_legend_Wordbound_bottom.rows[3].fully_visible = false; value.json(file, record); }, /controller mappings or visibility differ/],
  ['missing binding preservation', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    delete record.steps[3].probes.controls_legend_bindings_preserved; value.json(file, record); }, /binding preservation proof missing/],
  ['changed shared binding overrides', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    record.steps[3].probes.controls_legend_bindings_preserved.overrides_unchanged = false; value.json(file, record); }, /changed or omitted overrides_unchanged/],
  ['changed shared binding version', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    record.steps[3].probes.controls_legend_bindings_preserved.after_version++; value.json(file, record); }, /binding version changed/],
  ['unsupported omission of numbered slots', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    record.steps[3].probes.controls_legend_Wordbound_bottom.active_binding_count = 12; value.json(file, record); }, /supported numbered equipment rows/],
  ['lost controls input lease', value => { const file = value.options['lobby-run'] + '/checks.json', record = value.read(file);
    record.steps[3].probes.controls_legend_Wordbound_bottom.input_lease_active = false; value.json(file, record); }, /lost its input lease/],
  ['failed gate', value => { const gate = value.read('wordbound_solo-gate.txt'); gate.verdict = 'FAIL'; value.json('wordbound_solo-gate.txt', gate); }, /Gate did not PASS/],
  ['missing capture', value => fs.unlinkSync(path.join(value.root, value.options['lobby-run'], 'lobby_create_wordbound.png')), /ENOENT/],
  ['wrong source commit', value => { value.options['source-commit'] = '0'.repeat(40); }, /./],
  ['changed production file', value => value.write('Assets/Presentation.asset', 'CHANGED'), /Production files differ/],
  ['preservation baseline hash mismatch', value => { const record = value.read('preservation.json'); record.baseline_sha256 = '0'.repeat(64); value.json('preservation.json', record); }, /Preservation baseline changed/],
  ['invalid preservation inventory hash', value => { const record = value.read('preservation-before.json'); record.files[0].sha256 = 'invalid'; value.json('preservation-before.json', record);
    const verifier = value.read('preservation.json'); verifier.baseline_sha256 = digest(fs.readFileSync(path.join(value.root, 'preservation-before.json'))); value.json('preservation.json', verifier); }, /Preservation reference hash missing/],
  ['preservation baseline used as result', value => { value.options.preservation = 'preservation-before.json'; }, /Preservation verifier did not pass/],
  ['wrong smoke build', value => { const gate = value.read('wordbound_solo-gate.txt'); gate.smoke.build = 'Builds/qa-20980101T000000Z'; value.json('wordbound_solo-gate.txt', gate); }, /./],
  ['unrelated player log', value => { value.write('Logs/player/stale-unrelated.log', 'SYNTHETIC STALE LOG'); const gate = value.read('wordbound_solo-gate.txt'); gate.log = 'Logs/player/stale-unrelated.log'; value.json('wordbound_solo-gate.txt', gate); }, /Player log is not bound/],
  ['missing journal count', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); delete record.steps[1].probes.solo_timerun.journal_entries; value.json(file, record); }, /Journal count observation missing/],
  ['old run missing mode readiness', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); delete record.steps[2].probes.solo_render_readiness_freestyle; value.json(file, record); }, /Exact mode guide readiness is missing/],
  ['guide count mismatch', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[2].probes.solo_render_readiness_freestyle.rendered_markers = 1; value.json(file, record); }, /differs from the mode plan/],
  ['nonfinite expected guide count', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[1].probes.solo_render_readiness_timerun.expected_markers = Infinity; value.json(file, record); }, /Exact mode guide readiness is missing/],
  ['TimeRun empty guide plan', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); Object.assign(record.steps[1].probes.solo_render_readiness_timerun, { expected_markers: 0, rendered_markers: 0 }); value.json(file, record); }, /TimeRun needs an active planned guide/],
  ['Freestyle negative guide plan', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); Object.assign(record.steps[2].probes.solo_render_readiness_freestyle, { expected_markers: -1, rendered_markers: -1 }); value.json(file, record); }, /Exact mode guide readiness is missing/],
  ['mode readiness stale camera', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[1].probes.solo_render_readiness_timerun.camera_frames = 2; value.json(file, record); }, /lacks fresh camera frames/],
  ['mode readiness stale ticks', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[1].probes.solo_render_readiness_timerun.canonical_ticks = 5; value.json(file, record); }, /lacks advancing canonical ticks/],
  ['mode readiness guide fault', value => { const file = value.options['solo-run'] + '/checks.json', record = value.read(file); record.steps[2].probes.solo_render_readiness_freestyle.guide_fault = 'synthetic guide fault'; value.json(file, record); }, /Guide readiness reports a fault/],
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

function changeRun(value, lane, mutation) {
  const file = value.options[lane + '-run'] + '/checks.json', record = value.read(file);
  mutation(record); value.json(file, record);
}

test('serializer may omit captures only on a stage whose expected roster is empty', async () => {
  const value = await fixture();
  try {
    changeRun(value, 'workshop', record => {
      assert.deepEqual(record.steps[4].captures, []);
      delete record.steps[4].captures;
    });
    await value.verify(value.options);
  } finally { value.close(); }
});

for (const [name, mutate, error] of [
  ['omitted captures on captured stage', value => changeRun(value, 'lobby', record => delete record.steps[0].captures), /Missing, extra or reordered captures/],
  ['null captures on empty stage', value => changeRun(value, 'workshop', record => record.steps[4].captures = null), /Missing, extra or reordered captures/],
  ['extra captures on empty stage', value => changeRun(value, 'workshop', record => record.steps[4].captures = ['unexpected.png']), /Missing, extra or reordered captures/],
  ['missing new finish view', value => changeRun(value, 'lobby', record => record.steps[2].captures.splice(1, 1)), /Missing, extra or reordered captures/],
  ['old solo without Parcel Hall', value => changeRun(value, 'solo', record => record.steps.pop()), /Step names/],
  ['lobby roster changed', value => changeRun(value, 'lobby', record => record.steps[0].probes.local_Dibs.humans = 2), /human roster differs/],
  ['solo runner changed', value => changeRun(value, 'solo', record => record.steps[3].probes.solo_parcelhall_freestyle.runner = 'Shared'), /./],
  ['solo borrowed binding missing', value => changeRun(value, 'solo', record => delete record.steps[3].probes.solo_input_parcelhall_freestyle), /binding proof missing/],
  ['solo wrapper retained', value => changeRun(value, 'solo', record => record.steps[1].probes.solo_input_released_timerun.wrapper_disposed = false), /./],
  ['embedded source mismatch', value => changeRun(value, 'solo', record => record.commit = '0'.repeat(40)), /source commit mismatch/],
  ['workshop undo false', value => changeRun(value, 'workshop', record => record.steps[4].probes.undo_redo_exact = false), /./],
  ['workshop range cleanup error', value => changeRun(value, 'workshop', record => record.steps[1].probes.workshop_ranges_cleanup_errors = ['synthetic failure']), /./],
  ['workshop range belongs elsewhere', value => changeRun(value, 'workshop', record => record.steps[1].probes.workshop_ranges_owner = 'wrong'), /saved owner/],
  ['workshop stale primary', value => { const record = value.read(value.options['workshop-run'] + '/checks.json'); fs.appendFileSync(record.steps[1].probes.workshop_autosave_primary, 'changed'); }, /Current primary differs/],
  ['workshop camera retained', value => changeRun(value, 'workshop', record => record.steps[3].probes.camera_destroyed = false), /Workshop return/],
  ['workshop stale tick', value => changeRun(value, 'workshop', record => record.steps[2].probes.playtest_canonical_tick = 10), /./],
  ['workshop uniform capture', value => changeRun(value, 'workshop', record => record.steps[1].probes.workshop_capture_varied_rgb = false), /blank\/uniform/],
  ['unexpected PNG', value => value.write(value.options['solo-run'] + '/extra.png', png()), /Unexpected or missing PNG/],
  ['wrong smoke command identity', value => { const record = value.read('wordbound_solo-gate.txt'); record.cmd = 'smoke other'; value.json('wordbound_solo-gate.txt', record); }, /command identity differs/],
  ['preservation predates run completion', value => { const record = value.read('preservation.json'); record.utc = '2098-01-01T00:00:00Z'; value.json('preservation.json', record); }, /must follow/],
  ['new untracked production file', value => value.write('Assets/untracked.asset', 'synthetic'), /Untracked production files/],
  ['player log lacks completion', value => { const gate = value.read('wordbound_workshop_flow-gate.txt'); value.write(gate.log, 'SYNTHETIC incomplete'); }, /lacks exact scenario/]
]) test('extended synthetic rejection: ' + name, async () => {
  const value = await fixture();
  try { mutate(value); await assert.rejects(value.verify(value.options), error); assert(!fs.existsSync(path.join(value.root, 'result.json'))); }
  finally { value.close(); }
});


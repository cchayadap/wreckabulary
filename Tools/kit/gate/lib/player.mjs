// Rung R4: the thing that ships (P6). `build` makes a player with `unity build --profile` into Builds/<id>/ (release),
// Builds/qa-<id>/ (release-qa, GSK_QA) or Builds/dev-<id>/ (development; carries the Pipeline runtime, never shipped),
// with build.json. `smoke` launches that player normally (windowed, no -batchmode) and judges checks.json / perf.json and
// Player.log (OWNER_DEFAULTS C-16: QA scenarios run on release-qa or dev builds; `--stripped` smokes the shipped build).
import fs from 'node:fs';
import path from 'node:path';
import { UsageError, note } from '../../lib/cli.mjs';
import { readJsonOr, readText, writeJsonAtomic } from '../../lib/jsonio.mjs';
import { isInside, normalizeForCompare } from '../../lib/paths.mjs';
import { run } from '../../lib/proc.mjs';
import { loadSchema, validate, formatErrors } from '../../lib/schema.mjs';
import { now, utcFileStamp, utcIso } from '../../lib/time.mjs';
import { liveChildren } from './cold.mjs';
import { resolveEditor, readProjectVersion } from './editor.mjs';
import { BUILD_FAILED, exceptionLines, infraCause, notableLines, parseCompilerErrors } from './logscan.mjs';
import { addError, freshnessTree, gitHead, relU, setVerdict, uniqueLog } from './result.mjs';
import { classifyCliExit, cli } from './unitycli.mjs';
import { acquireUnity, editorHoldsProject, snapshotEditorLogs } from './unitylock.mjs';

export const KIND_DIR = Object.freeze({ release: '', 'release-qa': 'qa-', dev: 'dev-' });

/** The player executable name: gate.config.json exeName, else ProjectSettings productName + .exe, else Game.exe. */
export function exeNameOf(ctx) {
  if (ctx.cfg.exeName) return ctx.cfg.exeName;
  try {
    const m = /^\s*productName:\s*(.+)$/m.exec(readText(ctx.paths.projectSettings));
    const name = m ? m[1].trim().replace(/[<>:"/\\|?*]/g, '') : '';
    if (name && !/^com\.unity\.template/.test(name)) return `${name}.exe`;
  } catch { /* no settings */ }
  return 'Game.exe';
}

const KINDS = Object.keys(KIND_DIR);

/**
 * Resolve --profile / --kind / --dev into {kind, profile}. A profile given as an asset path takes the kind it is configured
 * for in gate.config.json buildProfiles (paths compared resolved and case-insensitively, any separator); a path that is
 * configured for no kind needs an explicit --kind (or --dev), so a development profile never lands as a release build.
 * @param {object} ctx
 * @param {{profile?: string, kind?: string, dev?: boolean}} opts
 */
export function resolveProfile(ctx, opts) {
  const bp = { ...(ctx.cfg.buildProfiles || {}) };
  if (!bp.release && ctx.cfg.buildProfile) bp.release = ctx.cfg.buildProfile;
  if (opts.kind !== undefined && !KINDS.includes(opts.kind)) throw new UsageError(`--kind takes ${KINDS.join(', ')} (got ${opts.kind})`);
  if (opts.dev && opts.kind && opts.kind !== 'dev') throw new UsageError(`--dev and --kind ${opts.kind} disagree`);
  const explicit = opts.dev ? 'dev' : opts.kind || null;
  if (!opts.profile || KINDS.includes(opts.profile)) {
    if (opts.kind && opts.profile && opts.kind !== opts.profile) throw new UsageError(`--profile ${opts.profile} and --kind ${opts.kind} disagree`);
    const kind = opts.dev ? 'dev' : opts.profile || explicit || 'release';
    const profile = bp[kind] || '';
    if (!profile) throw new UsageError(`no build profile for "${kind}": set gate.config.json buildProfiles.${kind}`);
    return { kind, profile };
  }
  const profile = opts.profile;
  const want = normalizeForCompare(path.resolve(ctx.U, profile));
  const hits = KINDS.filter((k) => bp[k] && normalizeForCompare(path.resolve(ctx.U, bp[k])) === want);
  // A missing asset is reported by cmdBuild (NOT-RUN no_build_profile) before the kind matters.
  if (!fs.existsSync(path.resolve(ctx.U, profile))) return { kind: explicit || hits[0] || 'release', profile };
  if (explicit) {
    if (hits.length && !hits.includes(explicit)) throw new UsageError(`${profile} is the configured ${hits.join('/')} profile, not ${explicit}`);
    return { kind: explicit, profile };
  }
  if (hits.length === 1) return { kind: hits[0], profile };
  if (hits.length > 1) throw new UsageError(`${profile} is configured for ${hits.join(' and ')}: give --kind to say which build this is`);
  throw new UsageError(`--profile ${profile} is not in gate.config.json buildProfiles: give --kind release|release-qa|dev so the build lands in the right folder (dev builds carry the Pipeline runtime and never ship)`);
}

/** The build output says the project failed (C# errors or a build-failure marker), not the machine. */
export function projectBuildFailure(text) {
  return BUILD_FAILED.test(text) || parseCompilerErrors(text).some((e) => /^CS\d+$/.test(e.code || ''));
}

/** `build [--profile release|release-qa|dev|<asset path> [--kind K]] [--dev] [--allow-dirty]`. */
export async function cmdBuild(ctx, res, opts, flags) {
  res.rung = 'R4';
  const { kind, profile } = resolveProfile(ctx, opts);
  const profileAbs = path.resolve(ctx.U, profile);
  if (!fs.existsSync(profileAbs)) return setVerdict(res, 'NOT-RUN', { exit: 2, infra: 'no_build_profile', message: `build profile not found: ${profile}` });
  const acq = await acquireUnity(ctx, `build ${kind}`);
  if (!acq.ok) return setVerdict(res, 'BLOCKED', { infra: acq.blocked, message: acq.detail });
  try {
    if (acq.served || editorHoldsProject(ctx).held) {
      return setVerdict(res, 'BLOCKED', { infra: 'editor_holds_project', message: 'a build starts its own batchmode editor, but an editor holds the project: run `game_gate unserve` (or ask the human to close the editor) and build again' });
    }
    // Build ids are second stamps (build.json schema); never reuse an existing folder.
    let id = utcFileStamp(now());
    for (let i = 0; i < 3 && fs.existsSync(path.join(ctx.U, 'Builds', `${KIND_DIR[kind]}${id}`)); i++) {
      await new Promise((r) => setTimeout(r, 1100));
      id = utcFileStamp(now());
    }
    const dirRel = `Builds/${KIND_DIR[kind]}${id}`;
    const dirAbs = path.join(ctx.U, ...dirRel.split('/'));
    if (fs.existsSync(dirAbs)) return setVerdict(res, 'BLOCKED', { infra: 'build_dir_exists', message: `${dirRel} already exists (is GSK_NOW pinned?)` });
    const exe = exeNameOf(ctx);
    const log = uniqueLog(ctx, `build_${kind}`);
    res.log = relU(ctx, log);
    res.route = 'cold';
    const timeoutS = flags.timeoutS ?? Math.round(ctx.cfg.timeoutMs / 1000);
    const editor = await resolveEditor(ctx);
    snapshotEditorLogs(ctx);
    fs.mkdirSync(dirAbs, { recursive: true });
    const args = ['build', ctx.U, '--profile', profileAbs, '-o', path.join(dirAbs, exe), '--timeout', String(timeoutS), '--no-tail', '--log-file', log, '--format', 'json'];
    if (editor.exe) args.push('-e', editor.exe);
    if (opts.allowDirty) args.push('--allow-dirty-build');
    const t0 = Date.now();
    const buildText = (x) => {
      let t = '';
      try { t = readText(log); } catch { /* the CLI may not have written it */ }
      return `${t}\n${x.stdout || ''}\n${x.stderr || ''}`;
    };
    let r = await cli(ctx, args, { timeoutMs: (timeoutS + 120) * 1000 });
    let text = buildText(r);
    // Retry an exit 6 once only when nothing says why: a failure in the project (compile errors, shaders, a build
    // processor's BuildFailedException, a missing scene) or a held project is deterministic and is judged as it is.
    if (classifyCliExit(r.exit) === 'command_failed' && !projectBuildFailure(text) && !infraCause(text)) {
      note('gate: unity build exited 6 without a build-failure marker or an infrastructure message; retrying once');
      r = await cli(ctx, args, { timeoutMs: (timeoutS + 120) * 1000 });
      text = buildText(r);
    }
    const secs = Math.round((Date.now() - t0) / 100) / 10;
    for (const e of parseCompilerErrors(text, { baseDir: ctx.U }).slice(0, 100)) addError(res, e);
    const notable = notableLines(text);
    if (notable.length) res.messages = notable;
    const exeAbs = path.join(dirAbs, exe);
    const ok = r.exit === 0 && fs.existsSync(exeAbs);
    const pv = readProjectVersion(ctx.paths.projectVersion);
    const buildJson = {
      schema: 1,
      id,
      kind,
      dir: dirRel,
      exe,
      profile: profile.replace(/\\/g, '/'),
      utc: utcIso(now()),
      development: kind === 'dev',
      unity_version: pv?.version ?? editor.version ?? 'unknown',
      head: await gitHead(ctx),
      tree: freshnessTree(ctx),
      secs,
      ok,
    };
    writeJsonAtomic(path.join(dirAbs, 'build.json'), buildJson);
    res.build = buildJson;
    res.evidence.push(`${dirRel}/build.json`);
    if (fs.existsSync(path.join(dirAbs, 'packed_assets.json'))) res.evidence.push(`${dirRel}/packed_assets.json`);
    else if (ok) res.messages = [...(res.messages || []), 'packed_assets.json is missing: BuildReportExport (com.gamestudiokit.core) did not run, so the licence join (game-ship) cannot use this build'];
    res.tree = buildJson.tree;
    res.head = buildJson.head;
    const cls = classifyCliExit(r.exit);
    if (ok) return setVerdict(res, 'PASS');
    if (r.exit === 0) return setVerdict(res, 'FAIL', { message: `unity build reported success but ${dirRel}/${exe} is missing` });
    if (r.exit === null) return setVerdict(res, 'INFRA', { infra: 'no_cli', message: r.error || 'the Unity CLI is not installed' });
    if (r.timedOut) return setVerdict(res, 'FAIL', { message: `the build timed out after ${timeoutS} s` });
    const ic = infraCause(text);
    if (ic?.kind === 'blocked') return setVerdict(res, 'BLOCKED', { infra: ic.reason, message: ic.line });
    if (res.errors.some((e) => /^CS\d+$/.test(e.code || '')) || cls === 'tests_failed') return setVerdict(res, 'FAIL');
    if (BUILD_FAILED.test(text)) {
      const why = text.split(/\r?\n/).map((l) => l.trim()).find((l) => BUILD_FAILED.test(l) && !/^Build Finished, Result: Failure/.test(l)) || 'Build Finished, Result: Failure';
      return setVerdict(res, 'FAIL', { message: `the player build failed in the project: ${why.slice(0, 300)} (see the log)` });
    }
    // INFRA only with an infrastructure message, or for the CLI's own error, argument, auth and precondition exits (1-4).
    if (ic) return setVerdict(res, 'INFRA', { infra: ic.reason, message: ic.line });
    if (cls === 'infra') return setVerdict(res, 'INFRA', { infra: `unity_cli_exit_${r.exit}`, message: `unity build exited ${r.exit}: ${(r.stderr || r.stdout || '').trim().slice(-400)}` });
    return setVerdict(res, 'FAIL', { message: `unity build exited ${r.exit} without an infrastructure message: see the log (${res.log})` });
  } finally {
    acq.release();
  }
}

const BUILD_DIR_RE = /^Builds\/((qa|dev)-)?\d{8}T\d{6}Z$/;

/**
 * Why a build.json cannot be trusted to name a player to launch, or null. build.json is a file an agent can write, so
 * `smoke` runs only a bare executable name inside the build's own folder, and the folder, kind and id must agree.
 * @param {object} b build.json content
 * @param {string} folder the Builds/ sub-folder it was read from
 */
export function buildEntryProblem(b, folder) {
  if (!b || typeof b !== 'object') return 'not a JSON object';
  if (!Object.hasOwn(KIND_DIR, b.kind)) return `kind ${JSON.stringify(b.kind)} is not release, release-qa or dev`;
  if (typeof b.dir !== 'string' || !BUILD_DIR_RE.test(b.dir)) return `dir ${JSON.stringify(b.dir)} is not Builds/[qa-|dev-]<id>`;
  if (b.dir !== `Builds/${folder}`) return `dir ${b.dir} is not the folder it was read from (Builds/${folder})`;
  if (b.dir !== `Builds/${KIND_DIR[b.kind]}${b.id}`) return `dir ${b.dir} does not match kind ${b.kind} and id ${b.id}`;
  if (typeof b.exe !== 'string' || !b.exe || /[\\/:*?"<>|\0]/.test(b.exe) || b.exe === '.' || b.exe === '..' || b.exe.includes('..')) return `exe ${JSON.stringify(b.exe)} is not a bare file name`;
  return null;
}

/**
 * Every trustworthy build.json under U/Builds, newest first; with `refused`, also the entries that were left out and why.
 * @param {object} ctx
 * @param {{refused?: object[]}} [out]
 */
export function listBuilds(ctx, out = {}) {
  let dirs = [];
  try {
    dirs = fs.readdirSync(ctx.paths.builds, { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name);
  } catch { /* no builds */ }
  const ok = [];
  for (const d of dirs) {
    const b = readJsonOr(path.join(ctx.paths.builds, d, 'build.json'), null, { onInvalid: 'fallback' });
    if (!b) continue;
    const why = buildEntryProblem(b, d);
    if (why) out.refused?.push({ folder: `Builds/${d}`, id: b.id ?? null, why });
    else ok.push(b);
  }
  return ok.sort((a, b) => String(b.id).localeCompare(String(a.id)) || String(b.utc).localeCompare(String(a.utc)));
}

function validateWith(ctx, file, data, pointer) {
  const schemaFile = path.join(ctx.paths.schemas, file);
  if (!fs.existsSync(schemaFile)) return { valid: true, skipped: true, errors: [] };
  const schema = loadSchema(schemaFile);
  return validate(schema, data, pointer ? { pointer } : {});
}

/** `smoke [--build <id>|latest] [--scenario smoke_frontend] [--seconds 90] [--seed N] [--stripped]`. */
export async function cmdSmoke(ctx, res, opts, flags) {
  res.rung = 'R4';
  res.route = 'none';
  const stripped = Boolean(opts.stripped);
  const want = stripped ? ['release'] : ['release-qa', 'dev'];
  const refused = [];
  const builds = listBuilds(ctx, { refused });
  for (const x of refused) note(`gate: ignored ${x.folder}/build.json: ${x.why}`);
  const wantId = opts.build && opts.build !== 'latest' ? opts.build : null;
  const b = wantId
    ? builds.find((x) => x.id === wantId || path.posix.basename(x.dir) === wantId)
    : builds.find((x) => want.includes(x.kind) && x.ok !== false);
  if (!b) {
    const bad = wantId ? refused.find((x) => x.id === wantId || path.posix.basename(x.folder) === wantId) : null;
    if (bad) return setVerdict(res, 'NOT-RUN', { exit: 2, infra: 'bad_build_json', message: `refused ${bad.folder}/build.json: ${bad.why}` });
    return setVerdict(res, 'NOT-RUN', { exit: 2, infra: 'no_build', message: wantId ? `no build ${wantId} under Builds/` : `no ${want.join(' or ')} build under Builds/: run game_gate build --profile ${want[0]} first` });
  }
  if (!want.includes(b.kind)) {
    return setVerdict(res, 'NOT-RUN', { exit: 2, infra: 'wrong_build_kind', message: stripped ? `--stripped smokes the shipped release build; ${b.dir} is ${b.kind}` : `${b.dir} is a release build without QaRunner: smoke a release-qa or dev build, or use --stripped` });
  }
  const exeAbs = path.resolve(ctx.U, ...b.dir.split('/'), b.exe);
  if (!isInside(exeAbs, path.join(ctx.paths.builds, path.posix.basename(b.dir)))) {
    return setVerdict(res, 'NOT-RUN', { exit: 2, infra: 'bad_build_json', message: `refused ${b.dir}/build.json: the player path leaves its build folder` });
  }
  if (!fs.existsSync(exeAbs)) return setVerdict(res, 'NOT-RUN', { exit: 2, infra: 'no_build', message: `the player is missing: ${b.dir}/${b.exe}` });
  const seconds = Number(opts.seconds ?? ctx.cfg.smokeSeconds);
  if (!Number.isFinite(seconds) || seconds < 1) throw new UsageError('--seconds needs a positive number');
  const scenario = opts.scenario || 'smoke_frontend';
  const seed = Number(opts.seed ?? ctx.cfg.smokeSeed);
  // A fresh run folder every time: evidence from an earlier run in the same second must never be read as this run's.
  const base = `${utcFileStamp(now())}-${stripped ? 'stripped' : scenario}`.replace(/[^A-Za-z0-9_.-]/g, '_');
  let runId = base;
  for (let i = 2; fs.existsSync(path.join(ctx.paths.qaRuns, runId)) || fs.existsSync(path.join(ctx.paths.logs, 'player', `${runId}.log`)); i++) runId = `${base}-${i}`;
  const qaOut = path.join(ctx.paths.qaRuns, runId);
  const playerLog = path.join(ctx.paths.logs, 'player', `${runId}.log`);
  fs.mkdirSync(qaOut, { recursive: true });
  fs.mkdirSync(path.dirname(playerLog), { recursive: true });
  const args = [...ctx.cfg.playerArgs];
  if (stripped) args.push('-qa-perf', '-qa-perf-seconds', String(seconds), '-qa-out', qaOut, '-logFile', playerLog);
  else args.push('-qa-scenario', scenario, '-qa-seed', String(seed), '-qa-out', qaOut, '-logFile', playerLog);
  const timeoutMs = seconds * 1000 + ctx.cfg.smokeGraceMs;
  res.log = relU(ctx, playerLog);
  res.smoke = { build: b.dir, kind: b.kind, run_id: runId, stripped, scenario: stripped ? null : scenario, seed: stripped ? null : seed };
  let pid = null;
  const r = await run(exeAbs, args, { cwd: path.dirname(exeAbs), timeoutMs, onSpawn: (p) => { pid = p; liveChildren.add(p); } });
  if (pid) liveChildren.delete(pid);
  if (r.exit === null && !r.timedOut && r.error) return setVerdict(res, 'INFRA', { infra: 'player_start', message: `cannot start the player: ${r.error}` });
  let log = '';
  try { log = readText(playerLog); } catch { /* no log */ }
  const exc = exceptionLines(log);
  for (const l of exc) addError(res, { message: l });
  if (!log) addError(res, 'Player.log was not written');
  res.smoke.exit = r.exit;
  res.smoke.timed_out = r.timedOut;
  if (stripped) {
    const perfFile = path.join(qaOut, 'perf.json');
    const perf = readJsonOr(perfFile, null, { onInvalid: 'fallback' });
    if (perf) res.evidence.push(relU(ctx, perfFile));
    const alive = r.timedOut;
    const exitedClean = !r.timedOut && r.exit === 0;
    if (!alive && !exitedClean) addError(res, `the player exited ${r.exit} by itself (a crash on a normal start)`);
    if (!perf) addError(res, 'perf.json was not written (PerfProbe with -qa-perf)');
    else {
      const v = validateWith(ctx, 'checks.schema.json', perf, '#/$defs/perf');
      if (!v.valid) addError(res, `perf.json does not validate: ${formatErrors(v.errors)}`);
    }
    return setVerdict(res, (alive || exitedClean) && !exc.length && perf && log && res.errors.length === 0 ? 'PASS' : 'FAIL');
  }
  const checksFile = path.join(qaOut, 'checks.json');
  const checks = readJsonOr(checksFile, null, { onInvalid: 'fallback' });
  if (checks) res.evidence.push(relU(ctx, checksFile));
  if (r.timedOut) addError(res, `the player did not finish within ${Math.round(timeoutMs / 1000)} s; its process tree was killed`);
  if (!checks) addError(res, 'checks.json was not written');
  else {
    const v = validateWith(ctx, 'checks.schema.json', checks);
    if (!v.valid) addError(res, `checks.json does not validate: ${formatErrors(v.errors)}`);
    if (checks.result !== 'PASS') addError(res, `checks.json result is ${checks.result}`);
    for (const s of (Array.isArray(checks.steps) ? checks.steps : []).filter((x) => x && x.status && x.status !== 'PASS').slice(0, 20)) {
      res.failures.push({ name: String(s.name || 'step'), message: (s.assert_failures || []).map(String).join('; ') || String(s.status) });
    }
  }
  return setVerdict(res, !r.timedOut && checks && checks.result === 'PASS' && !exc.length && log && res.errors.length === 0 ? 'PASS' : 'FAIL');
}

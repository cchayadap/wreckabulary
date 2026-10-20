// compile / test / exec: routed warm (R2, a live Pipeline editor) or cold (R3, batchmode), under the Unity lock.
// Routing: --cold forces R3; a live editor for this project (editor_status ready + warm-up eval) takes R2; an editor that
// holds the project without answering (Safe Mode, modal, no Pipeline) is BLOCKED 75 with its compile errors, never killed;
// otherwise R3. A warm command that gets CLI exit 7 re-probes first (7 is transient during domain reloads) and re-routes
// only when the descriptor's editor is gone. Warm test and exec recompile first, so they never run stale assemblies.
import fs from 'node:fs';
import path from 'node:path';
import { UsageError, note } from '../../lib/cli.mjs';
import { readText } from '../../lib/jsonio.mjs';
import { firstAssetsLocation, parseNUnitXml, parseUtpLog } from '../parse_nunit.mjs';
import { loadAsmdefs } from './asmdefs.mjs';
import { runEditorBatch } from './cold.mjs';
import { cmdCheck } from './dotnet.mjs';
import { currentCompileErrors, hasCompileErrors, infraCause, notableLines, parseCompilerErrors, prefixLines, threwException } from './logscan.mjs';
import { addError, newResult, relU, setVerdict, uniqueLog, uniqueStamped } from './result.mjs';
import {
  classifyCliExit, cli, commandArgs, editorAlive, evalCode, live, parseRecompile, parseRunTests, parseTestStatus, retryOn7,
  safeModeCount, settle,
} from './unitycli.mjs';
import { acquireUnity, currentEditorLog, descriptorState, editorHoldsProject } from './unitylock.mjs';
import { utcFileStamp, now } from '../../lib/time.mjs';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const tail = (s, n = 2 * 1024 * 1024) => (s.length > n ? s.slice(s.length - n) : s);

/** Append a warm transcript (CLI args, exit, output) to the run's log file. */
function logWarm(logFile, label, r) {
  try {
    fs.appendFileSync(logFile, `### ${label} exit=${r.exit} secs=${r.secs}\n${r.stdout || ''}${r.stderr ? `\n[stderr]\n${r.stderr}` : ''}\n`, 'utf8');
  } catch { /* the log is evidence, not a requirement */ }
}

/** Fill res from a log's prefix and notable lines. */
function addLogMessages(ctx, res, text) {
  const lines = [...prefixLines(text, [ctx.cfg.prefix, '[GSK]']), ...notableLines(text)];
  if (lines.length) res.messages = [...(res.messages || []), ...lines].slice(0, 100);
}

/** The current compile errors in the log of the editor that holds the project (its last finished compile cycle). */
function editorLogErrors(ctx) {
  const logFile = currentEditorLog(ctx);
  if (!logFile) return [];
  let text = '';
  try { text = tail(readText(logFile)); } catch { /* unreadable */ }
  return currentCompileErrors(text, { baseDir: ctx.U, max: 60 });
}

/**
 * An editor holds the project but its Pipeline server does not answer: BLOCKED 75, with the Safe Mode count, the
 * compile errors from its log and (for compile) an R0 result, so the agent can fix the code without Unity.
 */
async function blockedByEditor(ctx, res, lv, withR0) {
  const safe = await safeModeCount(ctx);
  res.safe_mode_instances = safe;
  for (const e of editorLogErrors(ctx)) addError(res, e);
  if (withR0) {
    const r0 = newResult('check all', 'R0');
    try {
      await cmdCheck(ctx, r0, 'all');
    } catch (e) {
      r0.verdict = 'INFRA';
      r0.errors.push({ message: e.message });
    }
    res.r0 = { verdict: r0.verdict, log: r0.log, errors: r0.errors.slice(0, 30) };
    for (const e of r0.errors.slice(0, 30)) addError(res, e);
  }
  const why = safe > 0 ? 'safe_mode' : lv?.reason === 'modal_dialog' ? 'modal_dialog' : 'editor_holds_project';
  const msg = why === 'safe_mode'
    ? `an editor holds the project in Safe Mode (compile errors at boot, so the Pipeline server cannot start): fix the errors below (R0 check needs no Unity), then restart that editor by PID`
    : why === 'modal_dialog'
      ? 'the editor that holds the project shows a modal dialog; a human must answer it'
      : 'an editor holds the project and does not answer through the Pipeline (not installed, still booting, or hung); use it through the Unity CLI when it is ready, or ask the human to close it. Never kill editors by name.';
  setVerdict(res, 'BLOCKED', { infra: why, message: msg });
}

/**
 * Take the Unity lock and route one job.
 * @param {object} ctx
 * @param {object} res
 * @param {{cold?: boolean, warm?: boolean}} flags
 * @param {string} purpose
 * @param {{warm?: () => Promise<{fallbackCold?: boolean}|void>, cold: () => Promise<void>, coldOnly?: boolean, r0OnBlock?: boolean}} impl
 */
export async function unityJob(ctx, res, flags, purpose, impl) {
  let forceCold = Boolean(flags.cold || impl.coldOnly);
  for (let attempt = 0; attempt < 2; attempt++) {
    const acq = await acquireUnity(ctx, purpose);
    if (acq.stale) res.stale_pid = acq.stale.stale_pid;
    if (!acq.ok) {
      setVerdict(res, 'BLOCKED', { infra: acq.blocked, message: acq.detail });
      return;
    }
    try {
      let lv = null;
      if (!forceCold) {
        lv = await live(ctx);
        if (lv.live) {
          res.route = 'warm';
          res.rung = 'R2';
          const out = await impl.warm();
          if (!out?.fallbackCold) return;
          note('gate: the warm editor stopped answering (exit 7 after a retry, or its process is gone); routing again');
          res.errors = [];
          res.messages = [];
          forceCold = true;
          continue;
        }
        if (lv.reason === 'stale_descriptor') {
          const ds = descriptorState(ctx);
          if (ds.why) res.messages = [...(res.messages || []), `ignored a stale Pipeline descriptor: ${ds.why}`];
        }
      }
      if (flags.warm) {
        setVerdict(res, 'NOT-RUN', { exit: 3, infra: 'no_live_editor', message: `--warm: no live Pipeline editor for this project (${lv?.reason || 'cold forced'})` });
        return;
      }
      const held = acq.served ? { held: true } : editorHoldsProject(ctx);
      if (held.held) {
        if (impl.coldOnly) {
          setVerdict(res, 'BLOCKED', { infra: 'editor_holds_project', message: 'this command needs a cold batchmode editor, but an editor holds the project: run `game_gate unserve` or ask the human to close it' });
          return;
        }
        await blockedByEditor(ctx, res, lv, impl.r0OnBlock);
        return;
      }
      res.route = 'cold';
      res.rung = 'R3';
      await impl.cold();
      return;
    } finally {
      acq.release();
    }
  }
}

/** Map a finished cold batch run to INFRA/BLOCKED when Unity itself could not work; returns true when it did. */
function coldInfra(ctx, res, b) {
  if (!b.started) {
    setVerdict(res, 'INFRA', { infra: 'no_editor', message: b.infra });
    return true;
  }
  if (b.timedOut || hasCompileErrors(b.log)) return false;
  const ic = infraCause(b.log);
  if (ic && !b.clean) {
    setVerdict(res, ic.kind === 'blocked' ? 'BLOCKED' : 'INFRA', { infra: ic.reason, message: ic.line });
    return true;
  }
  return false;
}

function coldCommon(ctx, res, b) {
  res.log = relU(ctx, b.logFile);
  res.crashAfterQuit = b.crashAfterQuit;
  res.refreshSecs = b.refreshSecs;
  for (const e of parseCompilerErrors(b.log, { baseDir: ctx.U }).slice(0, 200)) addError(res, e);
  addLogMessages(ctx, res, b.log);
  if (b.crashAfterQuit) res.messages = [...(res.messages || []), `Unity crashed during shutdown (exit ${b.exit}) after "Batchmode quit successfully invoked"; counted as a clean quit`];
}

// ---------------------------------------------------------------- compile

/**
 * The warm compile step: `unity recompile` (it refreshes the AssetDatabase first, so it picks up edits an unfocused or
 * headless editor has not seen), then wait for the domain reload and warm up again. On compile errors res is FAIL with
 * them; on a CLI failure res is INFRA.
 * @returns {Promise<{outcome:'ok'|'failed'|'infra'|'reroute', settled?: boolean}>}
 */
async function warmRecompile(ctx, res, log, timeoutS) {
  const args = ['recompile', '--project-path', ctx.U, '--format', 'json', '--timeout', String(Math.round(timeoutS))];
  const call = () => cli(ctx, args, { timeoutMs: (timeoutS + 60) * 1000 });
  const onResult = (x, label) => logWarm(log, label === 'first' ? 'recompile' : `recompile (${label})`, x);
  let { r, gone } = await retryOn7(ctx, call, onResult);
  if (gone || r.exit === 7) return { outcome: 'reroute' };
  let cls = classifyCliExit(r.exit);
  let pr = parseRecompile(r.json);
  if (cls === 'command_failed' && !pr.failed) {
    note('gate: unity recompile exited 6 without compile errors; retrying once');
    ({ r, gone } = await retryOn7(ctx, call, (x) => logWarm(log, 'recompile (retry after exit 6)', x)));
    if (gone || r.exit === 7) return { outcome: 'reroute' };
    cls = classifyCliExit(r.exit);
    pr = parseRecompile(r.json);
  }
  if (cls === 'ok' && !pr.failed) {
    const s = await settle(ctx);
    return { outcome: 'ok', settled: s.ready };
  }
  if (pr.failed || (cls === 'command_failed' && pr.errors.length)) {
    for (const e of pr.errors) addError(res, e);
    // Stale-error quirk: a non-C# failure makes the CLI fall back to old console errors. Cross-check the editor log.
    const missing = pr.errors.filter((e) => e.file && !fs.existsSync(path.resolve(ctx.U, e.file)));
    if (!pr.errors.length || missing.length) {
      for (const e of editorLogErrors(ctx)) addError(res, e);
      res.messages = [...(res.messages || []), `recompile reported ${pr.errors.length} error(s)${missing.length ? `, ${missing.length} naming files that no longer exist` : ''}: errors from the editor log are included (stale-error quirk; read Editor.log for asmdef or package errors)`];
    }
    await settle(ctx);
    setVerdict(res, 'FAIL');
    return { outcome: 'failed' };
  }
  setVerdict(res, 'INFRA', { infra: `unity_cli_exit_${r.exit}`, message: `unity recompile exited ${r.exit}: ${(r.stderr || r.stdout || r.error || '').trim().slice(0, 400)}` });
  return { outcome: 'infra' };
}

/** `compile` (R2 → R3). */
export async function cmdCompile(ctx, res, flags) {
  const timeoutS = flags.timeoutS ?? ctx.cfg.recompileTimeoutS;
  await unityJob(ctx, res, flags, 'compile', {
    r0OnBlock: true,
    warm: async () => {
      const log = uniqueLog(ctx, 'compile');
      res.log = relU(ctx, log);
      const rc = await warmRecompile(ctx, res, log, timeoutS);
      if (rc.outcome === 'reroute') return { fallbackCold: true };
      if (rc.outcome === 'ok') {
        if (!rc.settled) res.messages = [...(res.messages || []), `after the recompile the editor did not report ready within ${Math.round(ctx.cfg.readyTimeoutMs / 1000)} s`];
        setVerdict(res, 'PASS');
      }
      return {};
    },
    cold: async () => {
      const b = await runEditorBatch(ctx, { args: ['-quit'], logName: 'compile', timeoutMs: flags.timeoutMs ?? ctx.cfg.timeoutMs });
      if (coldInfra(ctx, res, b)) return;
      coldCommon(ctx, res, b);
      if (b.timedOut) setVerdict(res, 'FAIL', { message: `Unity timed out after ${Math.round((flags.timeoutMs ?? ctx.cfg.timeoutMs) / 1000)} s; the process tree was killed` });
      else if (hasCompileErrors(b.log)) setVerdict(res, 'FAIL', res.errors.length ? {} : { message: 'Scripts have compiler errors (see the log)' });
      else if (b.clean) setVerdict(res, 'PASS');
      else setVerdict(res, 'FAIL', { message: `Unity exited ${b.exit} before "Batchmode quit successfully invoked" (a crash before the quit is a real failure)` });
    },
  });
}

// ---------------------------------------------------------------- test

/** Test assemblies of the project: gate.config.json testAssemblies, else every test asmdef under the code roots. */
export function projectTestAssemblies(ctx, mode) {
  if (ctx.cfg.testAssemblies?.length) return { names: ctx.cfg.testAssemblies, source: 'config' };
  const model = loadAsmdefs(ctx.U, { codeRoots: ctx.cfg.codeRoots, checkExcludes: ctx.cfg.checkExcludes });
  const tests = model.list.filter((a) => a.isTest);
  const forMode = tests.filter((a) => (mode === 'editmode' ? a.editorOnly : !a.editorOnly));
  return { names: (forMode.length ? forMode : tests).map((a) => a.name), source: 'asmdefs' };
}

function commonPrefix(names) {
  if (!names.length) return '';
  let p = names[0];
  for (const n of names) while (!n.toLowerCase().startsWith(p.toLowerCase())) p = p.slice(0, -1);
  return p.replace(/[._-]+$/, '');
}

/** The one filter run_tests accepts (filter + filter_type testName|assembly|category). */
export function warmTestFilter(opts, assemblies, mode) {
  if (opts.filter) return { filter: opts.filter, type: 'testName', note: assemblies.length ? 'warm run_tests takes one filter: the name filter wins over the assembly scope' : null };
  if (opts.category) return { filter: opts.category, type: 'category', note: null };
  if (!assemblies.length) return null;
  const tag = mode === 'editmode' ? /edit/i : /play/i;
  const matching = assemblies.filter((a) => tag.test(a));
  if (assemblies.length === 1) return { filter: assemblies[0], type: 'assembly', note: null };
  if (matching.length === 1) return { filter: matching[0], type: 'assembly', note: null };
  const pre = commonPrefix(matching.length ? matching : assemblies);
  if (pre.length >= 4) return { filter: pre, type: 'assembly', note: null };
  return { filter: null, type: null, note: 'warm run_tests could not scope to the project test assemblies; package tests may be included (use --cold for exact assembly filtering)' };
}

/** Verdict of a warm test run from parsed counts (a run_tests Summary or a completed test_status payload). */
function judgeWarmTests(res, pr) {
  res.tests = { total: pr.total, passed: pr.passed, failed: pr.failed, skipped: pr.skipped };
  res.failures = pr.failures.map((f) => ({ name: f.name, message: f.message, location: firstAssetsLocation(f.stack) }));
  if (pr.failed > 0) setVerdict(res, 'FAIL');
  else if (pr.total === 0) setVerdict(res, 'FAIL', { message: 'zero tests ran (zero tests is a FAIL): check the filter and that the test assembly compiles' });
  else setVerdict(res, 'PASS');
}

/** A Pipeline run_tests error response (success:false + Error): the message decides, never "zero tests ran". */
function runTestsError(res, error) {
  if (/timed out/i.test(error)) setVerdict(res, 'FAIL', { message: `run_tests in the editor: ${error}` });
  else setVerdict(res, 'INFRA', { infra: 'run_tests_error', message: `run_tests failed in the editor: ${error}` });
}

/**
 * PlayMode on the warm route (Pipeline 0.8.0-exp.1 refuses a synchronous PlayMode run_tests: entering play mode reloads the
 * domain and drops the HTTP request): start it with --async_tests, then poll test_status, tolerating exit 7 and network
 * errors during the play-mode domain reloads. The package ignores the timeout in async mode, so the gate enforces it
 * and sends cancel_tests when it passes.
 */
async function warmPlaymode(ctx, res, log, params, timeoutS) {
  const start = () => cli(ctx, commandArgs(ctx, 'run_tests', 60, params), { timeoutMs: 120000 });
  const { r, gone } = await retryOn7(ctx, start, (x, label) => logWarm(log, label === 'first' ? 'run_tests --async_tests' : `run_tests --async_tests (${label})`, x));
  if (gone || r.exit === 7) return { fallbackCold: true };
  const pr = parseRunTests(r.json);
  if (pr.errorResponse) {
    runTestsError(res, pr.error);
    await settle(ctx);
    return {};
  }
  if (!pr.running) {
    if (pr.found) {
      judgeWarmTests(res, pr);
      await settle(ctx);
      return {};
    }
    setVerdict(res, 'INFRA', { infra: `unity_cli_exit_${r.exit}`, message: `run_tests did not start the play-mode run (exit ${r.exit}): ${(pr.error || r.stderr || r.stdout || r.error || '').trim().slice(0, 400)}` });
    return {};
  }
  const deadline = Date.now() + (timeoutS + 60) * 1000;
  let missingSince = null;
  let polls = 0;
  let q = null;
  let ts = null;
  for (;;) {
    await sleep(ctx.cfg.pollMs);
    q = await cli(ctx, commandArgs(ctx, 'test_status', 20), { timeoutMs: 45000 });
    polls++;
    if (q.exit === null) {
      setVerdict(res, 'INFRA', { infra: 'no_cli', message: q.error || 'the Unity CLI stopped working during the play-mode run' });
      return {};
    }
    if (q.exit === 0) {
      ts = parseTestStatus(q.json);
      if (ts.state === 'completed') break;
      if (['error', 'cancelled', 'no_tests'].includes(ts.state)) {
        logWarm(log, `test_status (${polls} polls)`, q);
        setVerdict(res, 'INFRA', { infra: 'run_tests_error', message: `the play-mode test run ended as ${ts.state}${ts.message ? `: ${ts.message}` : ''}` });
        await settle(ctx);
        return {};
      }
    }
    // Not finished, or not answering (exit 7 / network error while play mode reloads the domain): wait while the editor lives.
    const ds = descriptorState(ctx).state;
    if (ds === 'dead' || ds === 'stale') return { fallbackCold: true };
    if (!editorAlive(ctx)) {
      missingSince ??= Date.now();
      if (Date.now() - missingSince > 15000) return { fallbackCold: true };
    } else missingSince = null;
    if (Date.now() >= deadline) {
      logWarm(log, `test_status (${polls} polls, last)`, q);
      const c = await cli(ctx, commandArgs(ctx, 'cancel_tests', 30), { timeoutMs: 60000 });
      logWarm(log, 'cancel_tests', c);
      setVerdict(res, 'FAIL', { message: `the play-mode test run did not finish within ${timeoutS} s; cancel_tests was sent` });
      await settle(ctx);
      return {};
    }
  }
  logWarm(log, `test_status (${polls} polls)`, q);
  const json = uniqueStamped(ctx.paths.testResults, 'playmode', '.json');
  try { fs.writeFileSync(json, q.stdout || '', 'utf8'); res.evidence.push(relU(ctx, json)); } catch { /* evidence only */ }
  judgeWarmTests(res, ts);
  await settle(ctx);
  return {};
}

/** `test editmode|playmode [--filter F] [--category C] [--assemblies A;B]`. */
export async function cmdTest(ctx, res, mode, opts, flags) {
  if (!['editmode', 'playmode'].includes(mode)) throw new UsageError('test needs editmode or playmode');
  const asm = opts.assemblies ? { names: opts.assemblies.split(/[;,]/).map((s) => s.trim()).filter(Boolean), source: 'flag' } : projectTestAssemblies(ctx, mode);
  const timeoutS = flags.timeoutS ?? Math.round(ctx.cfg.timeoutMs / 1000);
  await unityJob(ctx, res, flags, `test ${mode}`, {
    warm: async () => {
      const log = uniqueLog(ctx, `test_${mode}`);
      res.log = relU(ctx, log);
      const rc = await warmRecompile(ctx, res, log, ctx.cfg.recompileTimeoutS);
      if (rc.outcome === 'reroute') return { fallbackCold: true };
      if (rc.outcome === 'failed') {
        addError(res, 'the project does not compile, so no tests ran');
        return {};
      }
      if (rc.outcome === 'infra') return {};
      const wf = warmTestFilter(opts, asm.names, mode);
      const filterParams = wf?.filter ? ['--filter', wf.filter, '--filter_type', wf.type] : [];
      if (wf?.note) res.messages = [...(res.messages || []), wf.note];
      if (mode === 'playmode') {
        return warmPlaymode(ctx, res, log, ['--mode', 'playmode', '--async_tests', 'true', '--timeout', String(timeoutS), ...filterParams], timeoutS);
      }
      const params = ['--mode', 'editor', '--timeout', String(timeoutS), ...filterParams];
      const call = () => cli(ctx, commandArgs(ctx, 'run_tests', timeoutS + 30, params), { timeoutMs: (timeoutS + 90) * 1000 });
      let { r, gone } = await retryOn7(ctx, call, (x, label) => logWarm(log, label === 'first' ? 'run_tests' : `run_tests (${label})`, x));
      if (gone || r.exit === 7) return { fallbackCold: true };
      let cls = classifyCliExit(r.exit);
      let pr = parseRunTests(r.json);
      const deterministic = pr.errorResponse && /timed out|Invalid (mode|filterType)/i.test(pr.error || '');
      if (!pr.found && !deterministic && cls === 'command_failed') {
        note('gate: run_tests exited 6 without a test summary; retrying once');
        ({ r, gone } = await retryOn7(ctx, call, (x) => logWarm(log, 'run_tests (retry after exit 6)', x)));
        if (gone || r.exit === 7) return { fallbackCold: true };
        cls = classifyCliExit(r.exit);
        pr = parseRunTests(r.json);
      }
      const json = uniqueStamped(ctx.paths.testResults, mode, '.json');
      try { fs.writeFileSync(json, r.stdout || '', 'utf8'); res.evidence.push(relU(ctx, json)); } catch { /* evidence only */ }
      if (pr.errorResponse) {
        runTestsError(res, pr.error);
        await settle(ctx);
        return {};
      }
      if (!pr.found) {
        if (cls === 'tests_failed') {
          setVerdict(res, 'FAIL', { message: 'unity reported failed tests (exit 8) but no summary could be read' });
          return {};
        }
        setVerdict(res, 'INFRA', { infra: `unity_cli_exit_${r.exit}`, message: `run_tests gave no test summary (exit ${r.exit}): ${(pr.error || r.stderr || r.stdout || r.error || '').trim().slice(0, 400)}` });
        return {};
      }
      await settle(ctx);
      judgeWarmTests(res, pr);
      return {};
    },
    cold: async () => {
      const platform = mode === 'editmode' ? 'EditMode' : 'PlayMode';
      const xml = uniqueStamped(ctx.paths.testResults, mode, '.xml');
      const args = ['-runTests', '-testPlatform', platform, '-testResults', xml];
      if (opts.filter) args.push('-testFilter', opts.filter);
      if (opts.category) args.push('-testCategory', opts.category);
      if (asm.names.length) args.push('-assemblyNames', asm.names.join(';'));
      const b = await runEditorBatch(ctx, { args, logName: `test_${mode}`, timeoutMs: flags.timeoutMs ?? ctx.cfg.timeoutMs });
      if (coldInfra(ctx, res, b)) return;
      coldCommon(ctx, res, b);
      let pr = null;
      if (fs.existsSync(xml)) {
        pr = parseNUnitXml(readText(xml), { assemblies: asm.names });
        res.evidence.push(relU(ctx, xml));
      } else {
        const u = parseUtpLog(b.log);
        if (u.total) pr = u;
      }
      if (pr) {
        res.tests = { total: pr.total, passed: pr.passed, failed: pr.failed, skipped: pr.skipped };
        res.failures = pr.failures;
      }
      if (b.timedOut) setVerdict(res, 'FAIL', { message: `the test run timed out after ${Math.round((flags.timeoutMs ?? ctx.cfg.timeoutMs) / 1000)} s; the process tree was killed` });
      else if (!fs.existsSync(xml)) setVerdict(res, 'FAIL', { message: `no results file (Unity exit ${b.exit}): compile errors or a run failure; see the log` });
      else if (hasCompileErrors(b.log)) setVerdict(res, 'FAIL');
      else if (pr.failed > 0) setVerdict(res, 'FAIL');
      else if (pr.total === 0) setVerdict(res, 'FAIL', { message: 'zero tests ran (zero tests is a FAIL): check the filter, the category and the assembly names' });
      else setVerdict(res, 'PASS');
    },
  });
}

// ---------------------------------------------------------------- exec

export const METHOD_RE = /^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$/;

/**
 * Namespaces exec never runs: engine, editor, runtime and library code (e.g. UnityEditor.EditorPrefs.DeleteAll wipes the
 * user's preferences for every project), and the exec door itself. KitCli.Resolve applies the same rule in C#, and also
 * requires a public static void method in a project or kit assembly (Library/ScriptAssemblies).
 */
export const EXEC_BLOCKED = /^(UnityEngine|UnityEditor|Unity|System|Microsoft|Mono|Newtonsoft|NUnit|JetBrains)\.|^GameStudioKit\.Editor\.KitCli\./i;

/**
 * Unity command-line options a raw exec must not add: they would change the project, the log, what runs or what gets
 * built (legacy `exec <M> --raw -- <method's own flags>` is for flags the project's method reads, e.g. -kwCommand ping).
 */
export const RAW_DENIED = /^--?(projectpath|createproject|importpackage|exportpackage|executemethod|logfile|buildtarget|standalonebuildsubtarget|activebuildprofile|build\w*|quit|batchmode|nographics|runtests|testresults|testplatform|testfilter|testcategory|assemblynames|username|password|serial|returnlicense|manuallicensefile|createmanuallicensefile|openfile|crash-report-folder|accept-apiupdate|cacheserver\w*|enablecacheserver|force-[\w-]+|upm\w*|noupm|disable-assembly-updater|ignorecompilererrors|deepprofiling|profiler-[\w-]+|vcsmode|skipmissing\w*|setdefaultplatformtextureformat|silent-crashes|enablecodecoverage|coverage\w*|debugcodeoptimization|job-worker-count|gc-helper-count|stacktracelogtype|timestamps)$/i;

/** Refuse an exec target or raw arguments the gate must not run (UsageError). */
export function validateExec(method, raw, rest) {
  if (!method || !METHOD_RE.test(method)) throw new UsageError('exec needs a static method as Namespace.Class.Method');
  if (EXEC_BLOCKED.test(method)) throw new UsageError(`exec runs the project's own entry points only; ${method} is engine, editor, runtime or library code`);
  if (raw) {
    for (const a of rest) {
      if (/[\r\n\0]/.test(a)) throw new UsageError('raw exec arguments must not contain line breaks');
      if (a.startsWith('-') && RAW_DENIED.test(a.split('=')[0])) throw new UsageError(`raw exec passes the method's own flags only; ${a} is a Unity command-line option`);
    }
  }
}

/**
 * Read and judge a KitCli result envelope {ok, method, utc, result, error}. With `method` and `notBeforeMs`, an envelope
 * that another job wrote (another method, or a time before this job started) is rejected, never judged as this run's.
 * @param {string} outFile
 * @param {{method?: string, notBeforeMs?: number}} [expect]
 */
export function judgeExecOut(outFile, expect = {}) {
  const missing = { ok: false, reason: 'the exec result file was not written (is com.gamestudiokit.core installed, and does the method exist?)' };
  if (!fs.existsSync(outFile)) return missing;
  let text;
  try {
    text = readText(outFile);
  } catch (e) {
    return { ok: false, reason: `the exec result file cannot be read: ${e.message}` };
  }
  if (!text.trim()) return missing;
  let j;
  try {
    j = JSON.parse(text);
  } catch (e) {
    return { ok: false, reason: `the exec result file is not JSON: ${e.message}` };
  }
  if (expect.method && j?.method !== expect.method) return { ok: false, reason: `the result file is not from this run (it names ${JSON.stringify(j?.method ?? null)}, not ${expect.method})` };
  if (typeof expect.notBeforeMs === 'number') {
    const t = Date.parse(j?.utc);
    if (!Number.isFinite(t) || t < Math.floor(expect.notBeforeMs / 1000) * 1000 - 1000) return { ok: false, reason: `the result file is not from this run (utc ${JSON.stringify(j?.utc ?? null)} is before the job started)` };
  }
  if (j?.ok !== true) return { ok: false, reason: `the entry point failed: ${j?.error || 'ok is not true'}`, envelope: j };
  const verdict = j.result && typeof j.result === 'object' ? String(j.result.verdict || '').toUpperCase() : '';
  if (verdict === 'FAIL' || verdict === 'ERROR') return { ok: false, reason: `the entry point returned verdict ${verdict}`, envelope: j };
  if (j.result && typeof j.result === 'object' && j.result.ok === false) return { ok: false, reason: 'the entry point returned ok:false', envelope: j };
  return { ok: true, envelope: j };
}

/**
 * Reserve U/Logs/exec/<stamp>[-n].json for one exec run, after the Unity lock is held: an exclusive create, so two jobs
 * never share a result file even when they start in the same second. The empty placeholder counts as "not written".
 */
export function reserveExecOut(ctx) {
  const dir = path.join(ctx.paths.logs, 'exec');
  fs.mkdirSync(dir, { recursive: true });
  const stamp = utcFileStamp(now());
  for (let i = 1; ; i++) {
    const p = path.join(dir, i === 1 ? `${stamp}.json` : `${stamp}-${i}.json`);
    try {
      fs.closeSync(fs.openSync(p, 'wx'));
      return p;
    } catch (e) {
      if (e.code !== 'EEXIST' || i > 999) throw e;
    }
  }
}

/** Read exec arguments from `--args-file <path>` or `--args-file -` (stdin): the PowerShell-safe form. */
async function readArgsFile(spec) {
  if (spec === '-') {
    if (process.stdin.isTTY) throw new UsageError('--args-file - reads the JSON arguments from stdin, but stdin is a terminal');
    const chunks = [];
    for await (const c of process.stdin) chunks.push(Buffer.isBuffer(c) ? c : Buffer.from(c));
    const b = Buffer.concat(chunks);
    if (b.length >= 2 && b[0] === 0xff && b[1] === 0xfe) return b.subarray(2).toString('utf16le');
    return b.toString('utf8').replace(/^﻿/, '');
  }
  try {
    return readText(path.resolve(spec));
  } catch (e) {
    throw new UsageError(`cannot read --args-file ${spec}: ${e.code || e.message}`);
  }
}

const csString = (s) => JSON.stringify(String(s));

/**
 * `exec <Ns.Class.Method> [-- <json> | --args-file <path|->]` (KitCli contract) or `exec <Method> --raw -- <flags>`
 * (legacy, cold only). On Windows PowerShell 5.1 the JSON after `--` loses its double quotes, so use --args-file there.
 */
export async function cmdExec(ctx, res, method, rest, flags) {
  const raw = Boolean(flags.raw) || (!flags.argsFile && rest.length > 0 && rest[0].startsWith('-'));
  validateExec(method, raw, rest);
  let argsJson = '{}';
  if (flags.argsFile !== undefined) {
    if (raw) throw new UsageError('--args-file gives JSON arguments to a KitCli entry point; it does not go with --raw');
    if (rest.length) throw new UsageError('give the exec arguments either after -- or with --args-file, not both');
    argsJson = (await readArgsFile(flags.argsFile)).trim() || '{}';
  } else if (!raw && rest.length) {
    argsJson = rest.join(' ');
  }
  if (!raw) {
    try {
      JSON.parse(argsJson);
    } catch (e) {
      throw new UsageError(`exec arguments must be JSON (${e.message}); on Windows PowerShell pass them with --args-file <file> or --args-file - (stdin); use --raw for Unity-style flags`);
    }
  }
  const timeoutS = flags.timeoutS ?? Math.round(ctx.cfg.timeoutMs / 1000);
  let outFile = null;
  let startMs = 0;
  const reserve = () => {
    outFile = reserveExecOut(ctx);
    startMs = Date.now();
    return outFile;
  };
  const finishFromOut = (extraFail) => {
    const j = judgeExecOut(outFile, { method, notBeforeMs: startMs });
    if (fs.existsSync(outFile) && fs.statSync(outFile).size > 0) res.evidence.push(relU(ctx, outFile));
    if (j.envelope) {
      const text = JSON.stringify(j.envelope.result ?? null);
      res.exec = { method, ok: j.envelope.ok === true, error: j.envelope.error ?? null, ...(text.length <= 16384 ? { result: j.envelope.result ?? null } : { result_truncated: true }) };
    }
    if (extraFail) setVerdict(res, 'FAIL', { message: extraFail });
    else if (!j.ok) setVerdict(res, 'FAIL', { message: j.reason });
    else setVerdict(res, 'PASS');
  };
  const written = () => Boolean(outFile && fs.existsSync(outFile) && fs.statSync(outFile).size > 0);
  await unityJob(ctx, res, flags, `exec ${method}`, {
    coldOnly: raw,
    warm: async () => {
      const log = uniqueLog(ctx, `exec_${method.split('.').pop()}`);
      res.log = relU(ctx, log);
      const rc = await warmRecompile(ctx, res, log, ctx.cfg.recompileTimeoutS);
      if (rc.outcome === 'reroute') return { fallbackCold: true };
      if (rc.outcome === 'failed') {
        addError(res, `the project does not compile, so ${method} was not run`);
        return {};
      }
      if (rc.outcome === 'infra') return {};
      reserve();
      const code = `return GameStudioKit.Editor.KitCli.Run(${csString(method)}, ${csString(argsJson)}, ${csString(outFile)});`;
      // A retry after exit 7 is safe only when the method did not run: never twice when it already wrote its result.
      const { r, gone } = await retryOn7(ctx, () => evalCode(ctx, code, timeoutS), (x, label) => logWarm(log, label === 'first' ? 'eval KitCli.Run' : `eval KitCli.Run (${label})`, x), { canRetry: () => !written() });
      if ((gone || r.exit === 7) && !written()) return { fallbackCold: true };
      await settle(ctx);
      const text = `${r.stdout}\n${r.stderr}`;
      const cls = classifyCliExit(r.exit);
      if (!written() && /KitCli|CS0234|CS0103|CS0246/.test(text) && r.exit !== 0) {
        setVerdict(res, 'FAIL', { message: `the eval could not call GameStudioKit.Editor.KitCli (is com.gamestudiokit.core installed?): ${text.trim().slice(0, 300)}` });
        return {};
      }
      if (!written() && cls !== 'ok' && cls !== 'command_failed') {
        setVerdict(res, 'INFRA', { infra: `unity_cli_exit_${r.exit}`, message: `unity command eval exited ${r.exit}: ${text.trim().slice(0, 300)}` });
        return {};
      }
      finishFromOut(threwException(text) ? `${method} threw an exception` : null);
      return {};
    },
    cold: async () => {
      if (!raw) reserve();
      const args = raw ? ['-quit', '-executeMethod', method, ...rest] : ['-quit', '-executeMethod', 'GameStudioKit.Editor.KitCli.RunFromEnv'];
      const env = raw ? undefined : { GSK_EXEC_METHOD: method, GSK_EXEC_ARGS: argsJson, GSK_EXEC_OUT: outFile };
      const b = await runEditorBatch(ctx, { args, logName: `exec_${method.split('.').pop()}`, timeoutMs: flags.timeoutMs ?? ctx.cfg.timeoutMs, env });
      if (coldInfra(ctx, res, b)) return;
      coldCommon(ctx, res, b);
      if (b.timedOut) return setVerdict(res, 'FAIL', { message: `Unity timed out after ${timeoutS} s; the process tree was killed` });
      if (hasCompileErrors(b.log)) return setVerdict(res, 'FAIL', res.errors.length ? {} : { message: 'Scripts have compiler errors' });
      if (threwException(b.log)) return raw ? setVerdict(res, 'FAIL', { message: `${method} threw an exception (see the log)` }) : finishFromOut(`${method} threw an exception (see the log)`);
      if (!b.clean) return setVerdict(res, 'FAIL', { message: `Unity exited ${b.exit} before "Batchmode quit successfully invoked"` });
      if (raw) return setVerdict(res, 'PASS');
      return finishFromOut(null);
    },
  });
}

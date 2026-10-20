// The warm rung (R2): a running editor with com.unity.pipeline, driven through the official Unity CLI.
// Facts used here (research_unity_mcp.md §3.1, §4 and its fact-check; Pipeline 0.8.0-exp.1 command signatures):
// - `unity command editor_status` result: {status:"ready"|"compiling"|…, compiling, domainReloadInProgress, playMode, dialog?}.
//   `unity status` hung with batch editors, so it is never used.
// - The first eval after every boot, recompile, import or play-mode toggle can hit the 5000 ms main-thread timeout:
//   warm up with a throwaway eval and retry.
// - `run_tests` returns success:true even when tests fail: the verdict is Summary.Failed (and Total > 0).
// - CLI exit codes: 0 ok, 1 error, 2 bad args, 3 auth, 4 precondition, 6 command failure, 7 no instance, 8 tests failed.
// - The descriptor Library/Pipeline/.unity-pipeline-port holds the eval token: read pid/port only, never print it.
import fs from 'node:fs';
import { normalizeForCompare } from '../../lib/paths.mjs';
import { run } from '../../lib/proc.mjs';
import { cliEnv, unityCliArgv } from './config.mjs';
import { descriptorState } from './unitylock.mjs';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * Parse JSON that may be surrounded by other output: the whole text, else the last line that parses, else the span from
 * the first `{` to the last `}`. Returns null when nothing parses.
 * @param {string} text
 */
export function parseJsonLoose(text) {
  const t = String(text || '').trim();
  if (!t) return null;
  try {
    return JSON.parse(t);
  } catch { /* fall through */ }
  const lines = t.split(/\r?\n/);
  for (let i = lines.length - 1; i >= 0; i--) {
    const l = lines[i].trim();
    if (!(l.startsWith('{') || l.startsWith('['))) continue;
    try {
      return JSON.parse(l);
    } catch { /* keep looking */ }
  }
  const a = t.indexOf('{');
  const b = t.lastIndexOf('}');
  if (a >= 0 && b > a) {
    try {
      return JSON.parse(t.slice(a, b + 1));
    } catch { /* give up */ }
  }
  return null;
}

/**
 * Depth-first search for the first object (including the root) for which pred(obj) is true.
 * @param {any} obj
 * @param {(o: object) => boolean} pred
 * @param {number} [maxDepth]
 */
export function deepFind(obj, pred, maxDepth = 8) {
  const seen = new Set();
  const walk = (o, d) => {
    if (!o || typeof o !== 'object' || d > maxDepth || seen.has(o)) return null;
    seen.add(o);
    if (!Array.isArray(o) && pred(o)) return o;
    for (const v of Array.isArray(o) ? o : Object.values(o)) {
      const hit = walk(v, d + 1);
      if (hit) return hit;
    }
    return null;
  };
  return walk(obj, 0);
}

/** Case-insensitive property read. */
export function prop(o, name) {
  if (!o || typeof o !== 'object') return undefined;
  if (name in o) return o[name];
  const k = Object.keys(o).find((x) => x.toLowerCase() === name.toLowerCase());
  return k === undefined ? undefined : o[k];
}

/**
 * How the gate reads a Unity CLI exit code.
 * @param {number|null} exit
 * @returns {'ok'|'fail'|'tests_failed'|'command_failed'|'no_instance'|'infra'|'not_started'}
 */
export function classifyCliExit(exit) {
  if (exit === 0) return 'ok';
  if (exit === null || exit === undefined) return 'not_started';
  if (exit === 8) return 'tests_failed';
  if (exit === 7) return 'no_instance';
  if (exit === 6) return 'command_failed';
  return 'infra'; // 1-4 (error, bad args, auth, precondition), 130/143 (interrupted), anything else
}

/**
 * Run the Unity CLI. Resolves {exit, json, stdout, stderr, secs, timedOut, error}; exit null when the CLI is missing.
 * @param {ReturnType<import('./config.mjs').loadContext>} ctx
 * @param {string[]} args
 * @param {{timeoutMs?: number, env?: NodeJS.ProcessEnv}} [opts]
 */
export async function cli(ctx, args, opts = {}) {
  const argv = unityCliArgv(opts.env ?? process.env);
  if (!argv) return { exit: null, json: null, stdout: '', stderr: '', secs: 0, timedOut: false, error: 'the Unity CLI (unity) is not installed' };
  const r = await run(argv[0], [...argv.slice(1), ...args], { env: cliEnv(opts.env ?? process.env), cwd: ctx.U, timeoutMs: opts.timeoutMs ?? 60000 });
  return { exit: r.exit, json: parseJsonLoose(r.stdout), stdout: r.stdout, stderr: r.stderr, secs: r.secs, timedOut: r.timedOut, error: r.error };
}

/** `unity command <name> --project-path U --format json --timeout S [-- params]`. */
export function commandArgs(ctx, name, timeoutS, params = []) {
  const a = ['command', name, '--project-path', ctx.U, '--format', 'json', '--timeout', String(Math.max(1, Math.round(timeoutS)))];
  return params.length ? [...a, '--', ...params] : a;
}

/** The descriptor's pid/port/mode/version (never the eval token), or null. */
export function readDescriptor(ctx) {
  let j;
  try {
    j = JSON.parse(fs.readFileSync(ctx.paths.descriptor, 'utf8').replace(/^﻿/, ''));
  } catch {
    return null;
  }
  if (!j || typeof j !== 'object') return null;
  return {
    pid: Number.isInteger(j.pid) ? j.pid : Number(j.pid) || null,
    port: Number.isInteger(j.port) ? j.port : Number(j.port) || null,
    mode: typeof j.mode === 'string' ? j.mode : null,
    unityVersion: typeof j.unityVersion === 'string' ? j.unityVersion : null,
  };
}

/** True while the descriptor names a Unity editor that is (or may be) running: the job should wait for it, not go cold. */
export function editorAlive(ctx) {
  const s = descriptorState(ctx).state;
  return s === 'live' || s === 'unknown' || s === 'unreadable';
}

/**
 * One editor_status probe. No descriptor file means no Pipeline editor for this project, and a descriptor whose PID is
 * dead or reused is stale (both skip the CLI call).
 * @returns {Promise<{reachable:boolean, ready:boolean, status:string|null, reloading:boolean, compiling:boolean, dialog:any, exit:number|null, reason:string|null}>}
 */
export async function editorStatus(ctx) {
  const none = (reason, exit = null) => ({ reachable: false, ready: false, status: null, reloading: false, compiling: false, dialog: null, exit, reason });
  const ds = descriptorState(ctx);
  if (ds.state === 'none') return none('no_descriptor');
  if (ds.state === 'dead' || ds.state === 'stale') return none('stale_descriptor');
  const r = await cli(ctx, commandArgs(ctx, 'editor_status', 20), { timeoutMs: 45000 });
  if (r.exit === null) return none(r.error ? 'no_cli' : 'not_started');
  if (r.exit !== 0) return none(classifyCliExit(r.exit), r.exit);
  const st = deepFind(r.json, (o) => typeof prop(o, 'status') === 'string' && ('domainReloadInProgress' in o || 'compiling' in o || 'playMode' in o)) || deepFind(r.json, (o) => typeof prop(o, 'status') === 'string' && prop(o, 'status') !== 'success');
  if (!st) return { ...none('unparsed', 0), reachable: true };
  const status = String(prop(st, 'status'));
  const reloading = Boolean(prop(st, 'domainReloadInProgress'));
  const compiling = Boolean(prop(st, 'compiling'));
  const dialog = prop(st, 'dialog') ?? null;
  return { reachable: true, ready: status === 'ready' && !reloading && !compiling && !dialog, status, reloading, compiling, dialog, exit: 0, reason: dialog ? 'modal_dialog' : null };
}

/**
 * Poll editor_status until ready (status ready, no reload, not compiling, no modal dialog) or timeout. A request sent
 * during a domain reload fails transiently, so unreachable counts as "not yet" while the descriptor exists.
 * @returns {Promise<{ready:boolean, polls:number, last:any, secs:number}>}
 */
export async function waitReady(ctx, timeoutMs) {
  const t0 = Date.now();
  let polls = 0;
  let last = null;
  let missingSince = null;
  for (;;) {
    last = await editorStatus(ctx);
    polls++;
    if (last.ready) return { ready: true, polls, last, secs: (Date.now() - t0) / 1000 };
    if (last.reason === 'no_cli' || last.reason === 'stale_descriptor') return { ready: false, polls, last, secs: (Date.now() - t0) / 1000 };
    // The server may rewrite its descriptor across a reload; only a descriptor gone for 15 s means the editor is gone.
    if (last.reason === 'no_descriptor') {
      missingSince ??= Date.now();
      if (Date.now() - missingSince > 15000) return { ready: false, polls, last, secs: (Date.now() - t0) / 1000 };
    } else missingSince = null;
    if (Date.now() - t0 >= timeoutMs) return { ready: false, polls, last, secs: (Date.now() - t0) / 1000 };
    await sleep(ctx.cfg.pollMs);
  }
}

/**
 * Evaluate C# in the warm editor: `unity command eval -- --code <code> --timeout <ms>`.
 * @returns {Promise<{ok:boolean, exit:number|null, json:any, stdout:string, stderr:string, secs:number}>}
 */
export async function evalCode(ctx, code, timeoutS = 60) {
  const r = await cli(ctx, commandArgs(ctx, 'eval', timeoutS, ['--code', code, '--timeout', String(Math.round(timeoutS * 1000))]), { timeoutMs: (timeoutS + 30) * 1000 });
  const failed = r.json && (prop(r.json, 'success') === false);
  return { ok: r.exit === 0 && !failed, exit: r.exit, json: r.json, stdout: r.stdout, stderr: r.stderr, secs: r.secs };
}

/**
 * Throwaway eval with retries (the first eval after a reload can time out on the main thread). Exit 7 is the transient
 * "network or service" failure of a domain reload while the descriptor's editor still runs, so it is retried then too.
 */
export async function warmup(ctx) {
  const tries = Math.max(1, ctx.cfg.warmupTries);
  let last = null;
  for (let i = 1; i <= tries; i++) {
    last = await evalCode(ctx, 'return 1;', 30);
    if (last.ok) return { ok: true, tries: i };
    if (last.exit === null || (last.exit === 7 && !editorAlive(ctx))) break;
    if (i < tries) await sleep(ctx.cfg.warmupDelayMs);
  }
  return { ok: false, tries, exit: last?.exit ?? null, detail: (last?.stderr || last?.stdout || '').trim().slice(0, 300) };
}

/**
 * Run one warm CLI call; on exit 7 re-probe instead of assuming the editor went away (research_unity_mcp fact-check:
 * 7 is "network or transient service failure", seen during domain reloads). While the descriptor names a live editor, wait
 * for ready, warm up and retry the call once. Returns the last result; `gone` is true when the descriptor or its PID is
 * gone, the only case in which a job may re-route cold.
 * @param {object} ctx
 * @param {() => Promise<{exit:number|null}>} call
 * @param {(r: object, label: string) => void} [onResult]
 * @param {{canRetry?: () => boolean}} [opts] canRetry false keeps the first result (e.g. an exec that already wrote its result)
 */
export async function retryOn7(ctx, call, onResult, opts = {}) {
  let r = await call();
  onResult?.(r, 'first');
  if (r.exit !== 7) return { r, gone: false };
  if (opts.canRetry && !opts.canRetry()) return { r, gone: false };
  if (!editorAlive(ctx)) return { r, gone: true };
  const w = await waitReady(ctx, ctx.cfg.readyTimeoutMs);
  if (w.ready) await warmup(ctx);
  r = await call();
  onResult?.(r, 'retry after exit 7');
  return { r, gone: r.exit === 7 && !editorAlive(ctx) };
}

/**
 * Is a Pipeline editor for this project live and warm? Waits for readiness if it is reachable but reloading or compiling,
 * then warms it up. Never used when the descriptor is missing, dead or stale (a reused PID).
 * @returns {Promise<{live:boolean, reason:string|null, status:any, warmup?:any}>}
 */
export async function live(ctx) {
  const first = await editorStatus(ctx);
  let st = first;
  if (!first.ready) {
    if (['no_descriptor', 'stale_descriptor', 'no_cli', 'not_started'].includes(first.reason)) return { live: false, reason: first.reason, status: first };
    // Unreachable with a descriptor whose editor is alive = a reload in progress (or a boot): wait.
    if (!first.reachable && !editorAlive(ctx)) return { live: false, reason: 'stale_descriptor', status: first };
    const w = await waitReady(ctx, ctx.cfg.readyTimeoutMs);
    st = w.last;
    if (!w.ready) return { live: false, reason: st.reason || `not ready (${st.status || 'unreachable'})`, status: st };
  }
  const wu = await warmup(ctx);
  if (!wu.ok) return { live: false, reason: 'warmup_failed', status: st, warmup: wu };
  return { live: true, reason: null, status: st, warmup: wu };
}

/** After a command that reloads the domain (recompile, play-mode tests, exec): wait for ready, then warm up again. */
export async function settle(ctx) {
  const w = await waitReady(ctx, ctx.cfg.readyTimeoutMs);
  if (!w.ready) return { ready: false, polls: w.polls };
  const wu = await warmup(ctx);
  return { ready: wu.ok, polls: w.polls, warmup: wu };
}

const PATH_KEYS = ['projectPath', 'project_path', 'projectRoot', 'project'];

/** Is one `unity pipeline list` instance in Safe Mode? (boolean flag, a nested status object, or a mode/status string) */
function instanceInSafeMode(inst) {
  for (const [k, v] of Object.entries(inst)) {
    if (!/safe_?mode/i.test(k)) continue;
    if (typeof v === 'boolean') return v;
    if (typeof v === 'string') return /^(true|yes|active|detected|safe)/i.test(v);
    if (v && typeof v === 'object') {
      for (const f of ['detected', 'inSafeMode', 'isSafeMode', 'active', 'enabled']) if (prop(v, f) === true) return true;
      const s = prop(v, 'status') ?? prop(v, 'state');
      return typeof s === 'string' && /safe|detected|active/i.test(s) && !/^(none|off|inactive|not)/i.test(s);
    }
  }
  const mode = prop(inst, 'mode') ?? prop(inst, 'status') ?? prop(inst, 'state');
  return typeof mode === 'string' && /safe/i.test(mode);
}

/**
 * Safe Mode editors of THIS project from `unity pipeline list --format json` (it has no --project-path and lists every
 * instance on the machine): the data.instances[] entries whose project path is U. The machine-wide
 * summary.instancesInSafeMode is used only when the instances carry no project path. Null when unknown.
 */
export async function safeModeCount(ctx) {
  const r = await cli(ctx, ['pipeline', 'list', '--format', 'json'], { timeoutMs: 30000 });
  if (r.exit !== 0) return null;
  return safeModeFromList(r.json, ctx.U);
}

/** The pure part of safeModeCount (exported for tests). */
export function safeModeFromList(json, U) {
  const holder = deepFind(json, (o) => Array.isArray(prop(o, 'instances')));
  const instances = holder ? prop(holder, 'instances').filter((x) => x && typeof x === 'object') : [];
  const pathOf = (inst) => {
    for (const k of PATH_KEYS) {
      const v = prop(inst, k);
      if (typeof v === 'string' && v) return v;
      if (v && typeof v === 'object' && typeof prop(v, 'path') === 'string') return prop(v, 'path');
    }
    return null;
  };
  const withPath = instances.filter((i) => pathOf(i));
  if (withPath.length) {
    const want = normalizeForCompare(U);
    return withPath.filter((i) => normalizeForCompare(pathOf(i)) === want && instanceInSafeMode(i)).length;
  }
  const s = deepFind(json, (o) => 'instancesInSafeMode' in o);
  const n = s ? Number(s.instancesInSafeMode) : NaN;
  return Number.isFinite(n) ? n : null;
}

/** Counts and failures from a Summary/summary holder (a run_tests response or a completed test_status payload). */
function summaryOf(holder) {
  const s = prop(holder, 'summary');
  const n = (k) => { const v = Number(prop(s, k)); return Number.isFinite(v) ? v : 0; };
  const results = prop(holder, 'results');
  const failures = (Array.isArray(results) ? results : [])
    .filter((r) => r && /^fail/i.test(String(prop(r, 'status') || '')))
    .map((r) => ({ name: String(prop(r, 'fullName') || prop(r, 'name') || '?'), message: String(prop(r, 'message') || '').trim(), stack: String(prop(r, 'stackTrace') || '') }));
  return { total: n('total'), passed: n('passed'), failed: Math.max(n('failed'), failures.length), skipped: n('skipped'), inconclusive: n('inconclusive'), failures };
}

const ZERO = { total: 0, passed: 0, failed: 0, skipped: 0, inconclusive: 0, failures: [] };

/**
 * Parse a warm run_tests response (Pipeline 0.8.0-exp.1 TestExecutionResponse): the Summary object and Results.
 * Every Pipeline error path (bad mode or filterType, synchronous PlayMode, "Test execution timed out", "Failed to run
 * tests: …") returns success:false with an Error and an all-zero Summary: that is `errorResponse` with the message, never
 * a run of zero tests. An async start (result "running", a StatusPath) is `running`: poll test_status.
 * @param {any} json
 * @returns {{found:boolean, errorResponse:boolean, running:boolean, total:number, passed:number, failed:number, skipped:number, inconclusive:number, failures:{name:string,message:string,stack:string}[], error:string|null}}
 */
export function parseRunTests(json) {
  const holder = deepFind(json, (o) => { const s = prop(o, 'summary'); return s && typeof s === 'object' && prop(s, 'total') !== undefined; });
  const errorHolder = deepFind(json, (o) => typeof prop(o, 'error') === 'string' && prop(o, 'error'));
  const error = errorHolder ? String(prop(errorHolder, 'error')) : null;
  if (holder && prop(holder, 'success') === false) {
    const own = prop(holder, 'error');
    return { ...ZERO, found: false, errorResponse: true, running: false, error: typeof own === 'string' && own ? own : error || 'run_tests reported success:false' };
  }
  // An async start: result "running" (or "playmode_running" for mode all) with a StatusPath; its Summary is all zeros.
  const runningHolder = deepFind(json, (o) => {
    const r = prop(o, 'result');
    return r === 'running' || r === 'playmode_running' || (typeof prop(o, 'statusPath') === 'string' && Boolean(prop(o, 'statusPath')) && r !== 'completed');
  });
  if (runningHolder && prop(runningHolder, 'success') !== false) {
    return { ...(holder ? summaryOf(holder) : ZERO), found: false, errorResponse: false, running: true, error: null };
  }
  if (!holder) return { ...ZERO, found: false, errorResponse: false, running: false, error };
  return { ...summaryOf(holder), found: true, errorResponse: false, running: false, error };
}

const TEST_STATES = new Set(['running', 'completed', 'error', 'cancelled', 'no_tests', 'idle', 'malformed', 'unreadable']);

/**
 * Parse a `unity command test_status` payload (the Pipeline's Temp/pipeline_test_status.json, or {status:"running"} /
 * {status:"no_tests"}): {state, ...counts, failures, message}. state 'unparsed' when nothing recognisable came back.
 * @param {any} json
 */
export function parseTestStatus(json) {
  const holder = deepFind(json, (o) => {
    const s = prop(o, 'status');
    if (typeof s !== 'string' || !TEST_STATES.has(s.toLowerCase())) return false;
    return s.toLowerCase() !== 'completed' || (prop(o, 'summary') && typeof prop(o, 'summary') === 'object');
  });
  if (!holder) return { ...ZERO, state: 'unparsed', message: null };
  const state = String(prop(holder, 'status')).toLowerCase();
  const message = typeof prop(holder, 'message') === 'string' ? prop(holder, 'message') : null;
  return { ...(state === 'completed' ? summaryOf(holder) : ZERO), state, message };
}

/**
 * Compile errors from a warm `unity recompile --format json` result (data.errors[] of {file,line,column,code,message}).
 * @returns {{failed:boolean, errors:{message:string, code?:string, file?:string, line?:number, col?:number, severity:'error'}[]}}
 */
export function parseRecompile(json) {
  const holder = deepFind(json, (o) => Array.isArray(prop(o, 'errors')) && ('compilationFailed' in o || 'failed' in o || 'summary' in o || 'status' in o));
  const errs = holder ? prop(holder, 'errors') : [];
  const failed = Boolean(holder && (prop(holder, 'compilationFailed') || prop(holder, 'failed'))) || (Array.isArray(errs) && errs.length > 0);
  const errors = (errs || []).map((e) => ({
    message: String(prop(e, 'message') || prop(e, 'raw') || 'compile error'),
    ...(prop(e, 'code') ? { code: String(prop(e, 'code')) } : {}),
    ...(prop(e, 'file') ? { file: String(prop(e, 'file')).replace(/\\/g, '/') } : {}),
    ...(Number.isInteger(prop(e, 'line')) ? { line: prop(e, 'line') } : {}),
    ...(Number.isInteger(prop(e, 'column')) ? { col: prop(e, 'column') } : {}),
    severity: 'error',
  }));
  return { failed, errors };
}

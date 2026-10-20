#!/usr/bin/env node
// net_matrix.mjs: the co-op join/leave/reconnect/host-quit/late-join x seats x seeds x lag-profile matrix (ARCHITECTURE
// §3.6, CONTRACTS.md §4.5). Installed as <W>/Tools/kit/net/net_matrix.mjs.
//   expand <matrix.json> [--format cases|filter] [--json]            the case list, or one PlayMode --filter string
//   run    <matrix.json> [--assemblies A;B] [--gate <game_gate.mjs>] [--timeout <s>] [--route cold|auto|warm] [--json]
//                                                                     writes the expanded cases, runs
//                                                                     `game_gate test playmode --filter … --cold`, then reports
//   report <matrix.json> --results <TestResults xml | run_tests json | gate --json line file>… [--json]
//                                                                     per-case verdicts and a seats x lag grid
// Additive flags: --profiles <lagsim_profiles.json>, --fixture <Ns.Class>, --write <cases.json>, --out <dir>,
// --only-action/--only-seats/--only-lag <a,b> (narrow the matrix), --max-filter <chars>, --route, --lock-wait <s>.
//
// Case ids (the contract with the C# CoopTestBase template, which names each TestCaseData with it):
//   mx_<action>_n<seats>_s<seed>_<lag_profile>        e.g. mx_reconnect_n4_s2_coop_150ms_2pct
// The PlayMode filter is a regular expression over test full names ("…Fixture.<id>"), factored per action so a full
// product stays short. Unity's -testFilter (the gate's cold route) treats a pattern as a regex over full names and
// splits on ';' [V, research_qa_harness.md], and case ids never contain ';'. `run` therefore passes --cold by default:
// the warm route hands the filter to `run_tests --filter_type testName`, and whether that is a regex is unverified
// (--route auto allows it; the report then warns when cases are missing). Results are read from both gate evidence
// shapes: NUnit XML (cold) and the run_tests JSON (warm, results[] with fullName and status).
// The expanded cases, with each profile's numbers and a run token, are written to Docs/qa/net/matrix_cases.json for
// the tests to read. `run` holds Docs/qa/net/matrix.lock from that write until its last gate chunk has finished, and
// re-reads the token after every chunk, so a second run cannot swap the case list under a gate that is still waiting
// for unity.lock.
// Lag numbers: rtt_ms is the round trip. UnityTransport's debug simulator delays each send, so the per-side packet delay
// is rtt_ms / 2; loss_pct maps to the simulator's integer drop percentage [C, NGO UnityTransport.SetDebugSimulatorParameters].
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, CliError, UsageError, emit, isMain, note, parseCli, runCli } from '../lib/cli.mjs';
import { findWorkspace, kitPaths, relPosix, toPosix } from '../lib/paths.mjs';
import { readText, writeJsonAtomic, writeTextAtomic } from '../lib/jsonio.mjs';
import { lock, tryLock } from '../lib/locks.mjs';
import { run } from '../lib/proc.mjs';
import { utcFileStamp, utcIso } from '../lib/time.mjs';

export const ACTIONS = Object.freeze(['join', 'leave', 'reconnect', 'host_quit', 'late_join']);
export const ROUTES = Object.freeze(['cold', 'auto', 'warm']);
const EXCLUDE_KEYS = Object.freeze({ action: 'actions', seats: 'seats', seed: 'seeds', lag_profile: 'lag_profiles' });
/** gate.config.json defaults for the fields the run budget needs (unity-verify-ladder lib/config.mjs DEFAULTS). */
const GATE_DEFAULTS = Object.freeze({ lockWaitMs: 60 * 60 * 1000, timeoutMs: 30 * 60 * 1000, readyTimeoutMs: 180000 });
const NEEDS_TWO = new Set(['leave', 'reconnect', 'late_join']);
const NAME_RE = /^[A-Za-z0-9_]+$/;
const ID_RE = /mx_(join|leave|reconnect|host_quit|late_join)_n(\d+)_s(\d+)_([A-Za-z0-9_]+?)(?=$|[^A-Za-z0-9_])/;

export const caseId = (c) => `mx_${c.action}_n${c.seats}_s${c.seed}_${c.lag_profile}`;

function readJsonFile(file, what) {
  let text;
  try {
    text = readText(file);
  } catch (e) {
    throw new UsageError(`${what} not readable: ${file} (${e.code ?? e.message})`);
  }
  try {
    return JSON.parse(text);
  } catch (e) {
    throw new UsageError(`${what} is not valid JSON: ${file} (${e.message})`);
  }
}

/** Validate matrix.json; returns a normalised copy or throws UsageError listing every problem. */
export function validateMatrix(m) {
  const errs = [];
  if (!m || typeof m !== 'object' || Array.isArray(m)) throw new UsageError('matrix: not an object');
  if (m.schema !== 1) errs.push('schema must be 1');
  const ints = (k, lo, hi) => {
    const v = m[k];
    if (!Array.isArray(v) || !v.length) { errs.push(`${k} must be a non-empty array`); return []; }
    v.forEach((x) => { if (!Number.isInteger(x) || x < lo || x > hi) errs.push(`${k}: ${JSON.stringify(x)} is not an integer in ${lo}..${hi}`); });
    if (new Set(v).size !== v.length) errs.push(`${k} has duplicates`);
    return v;
  };
  const seats = ints('seats', 1, 64);
  const seeds = ints('seeds', 0, 2147483647);
  const names = (k, allowed) => {
    const v = m[k];
    if (!Array.isArray(v) || !v.length) { errs.push(`${k} must be a non-empty array`); return []; }
    v.forEach((x) => {
      if (typeof x !== 'string' || !NAME_RE.test(x)) errs.push(`${k}: ${JSON.stringify(x)} must match ${NAME_RE}`);
      else if (allowed && !allowed.includes(x)) errs.push(`${k}: unknown action ${x} (known: ${allowed.join(', ')})`);
    });
    if (new Set(v).size !== v.length) errs.push(`${k} has duplicates`);
    return v;
  };
  const actions = names('actions', ACTIONS);
  const lag = names('lag_profiles');
  const exclude = m.exclude ?? [];
  if (!Array.isArray(exclude)) errs.push('exclude must be an array');
  else exclude.forEach((rule, i) => excludeErrors(rule, `exclude[${i}]`, { actions, seats, seeds, lag_profiles: lag }).forEach((e) => errs.push(e)));
  if (errs.length) throw new UsageError(`matrix invalid: ${errs.join('; ')}`);
  return { schema: 1, seats, seeds, actions, lag_profiles: lag, exclude };
}

/**
 * Problems of one exclude rule: a non-empty object whose keys are action, seats, seed or lag_profile, each a value (or a
 * non-empty array of values) of the right type that the matrix lists. A typo must not silently exclude nothing, and an
 * empty rule must not exclude everything.
 */
function excludeErrors(rule, at, lists) {
  if (!rule || typeof rule !== 'object' || Array.isArray(rule)) return [`${at} must be an object like {"action":"join","seats":8}`];
  const keys = Object.keys(rule);
  if (!keys.length) return [`${at} is empty (it would exclude every case)`];
  const errs = [];
  for (const k of keys) {
    const listKey = EXCLUDE_KEYS[k];
    if (!listKey) { errs.push(`${at}: unknown key ${JSON.stringify(k)} (known: ${Object.keys(EXCLUDE_KEYS).join(', ')})`); continue; }
    const raw = rule[k];
    if (Array.isArray(raw) && !raw.length) { errs.push(`${at}.${k} is an empty array`); continue; }
    const wantInt = k === 'seats' || k === 'seed';
    for (const v of Array.isArray(raw) ? raw : [raw]) {
      if (wantInt ? !Number.isInteger(v) : typeof v !== 'string') errs.push(`${at}.${k}: ${JSON.stringify(v)} must be ${wantInt ? 'an integer' : 'a string'}`);
      else if (!lists[listKey].includes(v)) errs.push(`${at}.${k}: ${JSON.stringify(v)} is not in the matrix ${listKey}`);
    }
  }
  return errs;
}

/** Validate lagsim_profiles.json ({schema:1, profiles:{name:{rtt_ms, jitter_ms, loss_pct}}}). */
export function validateProfiles(p) {
  const errs = [];
  if (!p || p.schema !== 1 || typeof p.profiles !== 'object' || Array.isArray(p.profiles)) throw new UsageError('lag profiles: expected {schema:1, profiles:{…}}');
  for (const [name, v] of Object.entries(p.profiles)) {
    if (!NAME_RE.test(name)) errs.push(`profile name ${JSON.stringify(name)}`);
    for (const k of ['rtt_ms', 'jitter_ms', 'loss_pct']) {
      if (typeof v?.[k] !== 'number' || v[k] < 0 || (k === 'loss_pct' && v[k] > 100)) errs.push(`${name}.${k}`);
    }
  }
  if (errs.length) throw new UsageError(`lag profiles invalid: ${errs.join(', ')}`);
  return p.profiles;
}

/** Transport numbers for one profile. */
export function transportFor(p) {
  return {
    packet_delay_ms: Math.round(p.rtt_ms / 2),
    packet_jitter_ms: Math.round(p.jitter_ms),
    drop_rate_pct: Math.round(p.loss_pct),
  };
}

const matches = (rule, c) => Object.entries(rule).every(([k, v]) => (Array.isArray(v) ? v.includes(c[k]) : c[k] === v));

/**
 * Expand a validated matrix. Order: action, seats, lag profile, seed (as listed in the file).
 * @returns {{cases: object[], skipped: {id: string, reason: string}[]}}
 */
export function expand(m, profiles = null, only = {}) {
  const cases = [];
  const skipped = [];
  const pick = (list, sel) => (sel ? list.filter((x) => sel.includes(x)) : list);
  for (const action of pick(m.actions, only.action)) {
    for (const seats of pick(m.seats, only.seats)) {
      for (const lag_profile of pick(m.lag_profiles, only.lag)) {
        for (const seed of m.seeds) {
          const c = { id: '', seats, seed, action, lag_profile };
          c.id = caseId(c);
          if (NEEDS_TWO.has(action) && seats < 2) { skipped.push({ id: c.id, reason: `${action} needs at least 2 seats` }); continue; }
          if (m.exclude.some((r) => matches(r, c))) { skipped.push({ id: c.id, reason: 'excluded by matrix.exclude' }); continue; }
          if (profiles) {
            const p = profiles[lag_profile];
            c.lag = { rtt_ms: p.rtt_ms, jitter_ms: p.jitter_ms, loss_pct: p.loss_pct, transport: transportFor(p) };
          }
          cases.push(c);
        }
      }
    }
  }
  return { cases, skipped };
}

const reEsc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const alt = (xs) => (xs.length === 1 ? reEsc(String(xs[0])) : `(?:${xs.map((x) => reEsc(String(x))).join('|')})`);

/**
 * One regex matching exactly the given cases' test full names. Per action group: a factored
 * `<action>_n(?:…)_s(?:…)_(?:…)` when the group is a full product, else the ids themselves.
 */
export function filterFor(cases, fixture = null) {
  const groups = new Map();
  for (const c of cases) {
    if (!groups.has(c.action)) groups.set(c.action, []);
    groups.get(c.action).push(c);
  }
  const parts = [];
  for (const [action, cs] of groups) {
    const uniq = (k) => [...new Set(cs.map((c) => c[k]))];
    const seats = uniq('seats');
    const seeds = uniq('seed');
    const lags = uniq('lag_profile');
    if (seats.length * seeds.length * lags.length === cs.length) parts.push(`${action}_n${alt(seats)}_s${alt(seeds)}_${alt(lags)}`);
    else parts.push(...cs.map((c) => reEsc(c.id.slice(3))));
  }
  const body = `mx_${parts.length === 1 ? parts[0] : `(?:${parts.join('|')})`}$`;
  return fixture ? `^${reEsc(fixture)}\\.${body}` : `(?:^|\\.)${body}`;
}

/** Split cases into chunks whose filters stay under maxLen (whole action groups first, then halves). */
export function filterChunks(cases, fixture, maxLen) {
  const whole = filterFor(cases, fixture);
  if (whole.length <= maxLen || cases.length <= 1) return [{ cases, filter: whole }];
  const mid = Math.ceil(cases.length / 2);
  return [...filterChunks(cases.slice(0, mid), fixture, maxLen), ...filterChunks(cases.slice(mid), fixture, maxLen)];
}

const xmlUnescape = (s) => String(s).replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&#(\d+);/g, (_, d) => String.fromCharCode(Number(d))).replace(/&amp;/g, '&');

/** NUnit 3 result XML → [{name, fullname, result, message}] (test-case elements only). */
export function parseNunitCases(xml) {
  const out = [];
  const re = /<test-case\b([^>]*?)(\/>|>([\s\S]*?)<\/test-case>)/g;
  for (const m of String(xml).matchAll(re)) {
    const attrs = {};
    for (const a of m[1].matchAll(/([\w-]+)\s*=\s*"([^"]*)"/g)) attrs[a[1]] = xmlUnescape(a[2]);
    const body = m[3] ?? '';
    const msg = /<message>\s*(?:<!\[CDATA\[([\s\S]*?)\]\]>|([\s\S]*?))\s*<\/message>/.exec(body);
    out.push({ name: attrs.name ?? '', fullname: attrs.fullname ?? attrs.name ?? '', result: attrs.result ?? '', message: msg ? (msg[1] ?? xmlUnescape(msg[2] ?? '')).trim() : '' });
  }
  return out;
}

/** Case id inside a test name or full name, or null. */
export function idFromName(name) {
  const m = ID_RE.exec(String(name));
  return m ? m[0] : null;
}

/** Case-insensitive property read (the Unity CLI JSON uses PascalCase, the fakes and docs camelCase). */
function prop(o, name) {
  if (!o || typeof o !== 'object') return undefined;
  if (name in o) return o[name];
  const k = Object.keys(o).find((x) => x.toLowerCase() === name.toLowerCase());
  return k === undefined ? undefined : o[k];
}

/** First object (depth-first, arrays walked) that satisfies pred. */
function deepFind(obj, pred, depth = 0, seen = new Set()) {
  if (!obj || typeof obj !== 'object' || depth > 10 || seen.has(obj)) return null;
  seen.add(obj);
  if (!Array.isArray(obj) && pred(obj)) return obj;
  for (const v of Array.isArray(obj) ? obj : Object.values(obj)) {
    const hit = deepFind(v, pred, depth + 1, seen);
    if (hit) return hit;
  }
  return null;
}

/** JSON from a captured CLI stdout: the whole text, else the last line that parses, else the outermost {...}. */
export function parseJsonLoose(text) {
  const s = String(text ?? '').replace(/^﻿/, '');
  try { return JSON.parse(s); } catch { /* not one document */ }
  const lines = s.split(/\r?\n/).filter((l) => l.trim());
  for (let i = lines.length - 1; i >= 0; i--) {
    try { return JSON.parse(lines[i]); } catch { /* keep looking */ }
  }
  const a = s.indexOf('{');
  const b = s.lastIndexOf('}');
  if (a >= 0 && b > a) {
    try { return JSON.parse(s.slice(a, b + 1)); } catch { /* not JSON */ }
  }
  return null;
}

/** NUnit result name for a run_tests status (Passed, Failed, Skipped, Inconclusive, ...). */
function resultOf(status) {
  const s = String(status ?? '');
  if (/^pass/i.test(s) || /^success/i.test(s)) return 'Passed';
  if (/^fail/i.test(s) || /^error/i.test(s)) return 'Failed';
  return s || 'Unknown';
}

/**
 * Test cases from a warm `run_tests` response, which the gate saves as TestResults/<mode>_<stamp>.json (its evidence on
 * the warm route): the object holding a `results` array of {fullName|name, status, message}; nested `children`
 * arrays are walked. Returns null when the document has no such array.
 * @returns {{name: string, fullname: string, result: string, message: string}[]|null}
 */
export function parseRunTestsCases(json) {
  const holder = deepFind(json, (o) => Array.isArray(prop(o, 'results')) && (prop(o, 'summary') !== undefined || prop(o, 'results').some((r) => prop(r, 'status') !== undefined)));
  if (!holder) return null;
  const out = [];
  const walk = (items) => {
    for (const r of items) {
      if (!r || typeof r !== 'object') continue;
      const kids = prop(r, 'children');
      if (Array.isArray(kids) && kids.length) { walk(kids); continue; }
      const full = String(prop(r, 'fullName') ?? prop(r, 'name') ?? '');
      out.push({ name: String(prop(r, 'name') ?? full), fullname: full, result: resultOf(prop(r, 'status') ?? prop(r, 'result')), message: String(prop(r, 'message') ?? '').trim() });
    }
  };
  walk(prop(holder, 'results'));
  return out;
}

/**
 * Join expected cases with test results. Status per case: passed | failed | skipped | missing.
 * @returns {{verdict: 'PASS'|'FAIL', totals: object, cases: object[], grid: object[]}}
 */
export function judgeResults(cases, results, lagOrder = null) {
  const byId = new Map();
  for (const r of results) {
    const id = idFromName(r.fullname) ?? idFromName(r.name);
    if (!id) continue;
    const prev = byId.get(id);
    // a case run twice (a chunked or repeated run): any failure wins, then a pass
    if (!prev || r.result === 'Failed' || (prev.result !== 'Failed' && r.result === 'Passed')) byId.set(id, r);
  }
  const rows = cases.map((c) => {
    const r = byId.get(c.id);
    const status = !r ? 'missing' : r.result === 'Passed' ? 'passed' : r.result === 'Failed' ? 'failed' : 'skipped';
    return { id: c.id, action: c.action, seats: c.seats, seed: c.seed, lag_profile: c.lag_profile, status, ...(r?.message ? { message: r.message.slice(0, 500) } : {}) };
  });
  const totals = { cases: rows.length, passed: 0, failed: 0, skipped: 0, missing: 0 };
  rows.forEach((r) => { totals[r.status]++; });
  const cells = new Map();
  for (const r of rows) {
    const k = `${r.action}|${r.seats}|${r.lag_profile}`;
    if (!cells.has(k)) cells.set(k, { action: r.action, seats: r.seats, lag_profile: r.lag_profile, passed: 0, total: 0, failed: 0, missing: 0, skipped: 0 });
    const cell = cells.get(k);
    cell.total++;
    cell[r.status]++;
  }
  const lagIdx = (l) => (lagOrder ? lagOrder.indexOf(l) : 0);
  const grid = [...cells.values()]
    .map((c) => ({ ...c, status: c.passed === c.total ? 'PASS' : c.failed ? 'FAIL' : 'INCOMPLETE' }))
    .sort((a, b) => ACTIONS.indexOf(a.action) - ACTIONS.indexOf(b.action) || a.seats - b.seats || lagIdx(a.lag_profile) - lagIdx(b.lag_profile));
  const verdict = rows.length > 0 && totals.passed === totals.cases ? 'PASS' : 'FAIL';
  return { verdict, totals, cases: rows, grid };
}

/** Markdown report: a seats x lag grid per action, then the failing and missing cases. */
export function reportMarkdown(j, meta) {
  const lags = meta.lag_profiles;
  const lines = [
    `# Net matrix ${j.verdict}`,
    '',
    `- matrix: \`${meta.matrix}\``,
    `- generated: ${meta.utc}`,
    `- cases: ${j.totals.cases} (passed ${j.totals.passed}, failed ${j.totals.failed}, skipped ${j.totals.skipped}, missing ${j.totals.missing})`,
    ...(meta.results.length ? [`- results: ${meta.results.map((r) => `\`${r}\``).join(', ')}`] : []),
    '',
  ];
  for (const action of ACTIONS) {
    const cells = j.grid.filter((g) => g.action === action);
    if (!cells.length) continue;
    const seats = [...new Set(cells.map((c) => c.seats))].sort((a, b) => a - b);
    lines.push(`## ${action}`, '', `| seats | ${lags.join(' | ')} |`, `|---|${lags.map(() => '---').join('|')}|`);
    for (const s of seats) {
      const row = lags.map((l) => {
        const c = cells.find((x) => x.seats === s && x.lag_profile === l);
        return c ? `${c.status} ${c.passed}/${c.total}` : 'n/a';
      });
      lines.push(`| ${s} | ${row.join(' | ')} |`);
    }
    lines.push('');
  }
  const bad = j.cases.filter((c) => c.status !== 'passed');
  if (bad.length) {
    lines.push('## Not passed', '', '| case | status | message |', '|---|---|---|');
    for (const c of bad) lines.push(`| \`${c.id}\` | ${c.status} | ${(c.message ?? '').replace(/\|/g, '\\|').replace(/\r?\n/g, ' ').slice(0, 200)} |`);
    lines.push('');
  }
  return lines.join('\n');
}

/** Lag profiles: --profiles, matrix.profiles_file (relative to the matrix), or lagsim_profiles.json beside the matrix. */
function loadProfiles(opt, matrixFile, raw) {
  let file = null;
  if (opt) file = path.resolve(opt);
  else if (typeof raw.profiles_file === 'string') file = path.resolve(path.dirname(matrixFile), raw.profiles_file);
  else if (fs.existsSync(path.join(path.dirname(matrixFile), 'lagsim_profiles.json'))) file = path.join(path.dirname(matrixFile), 'lagsim_profiles.json');
  if (!file) return { profiles: null, file: null };
  if ((opt || raw.profiles_file) && !fs.existsSync(file)) throw new UsageError(`lag profiles file not found: ${file}`);
  return { profiles: validateProfiles(readJsonFile(file, 'lag profiles')), file };
}

const csv = (s, num) => (s === undefined ? undefined : String(s).split(',').map((x) => x.trim()).filter(Boolean).map((x) => (num ? Number(x) : x)));

/** Shared front half of every subcommand. */
function prepare(values, positionals) {
  const matrixArg = positionals[1];
  if (!matrixArg) throw new UsageError('missing <matrix.json>');
  const matrixFile = path.resolve(matrixArg);
  const raw = readJsonFile(matrixFile, 'matrix');
  const m = validateMatrix(raw);
  const { profiles, file: profilesFile } = loadProfiles(values.profiles, matrixFile, raw);
  const warnings = [];
  if (profiles) {
    const unknown = m.lag_profiles.filter((l) => !profiles[l]);
    if (unknown.length) throw new UsageError(`lag profile(s) not in ${profilesFile}: ${unknown.join(', ')}`);
    for (const l of m.lag_profiles) if (!Number.isInteger(profiles[l].loss_pct)) warnings.push(`${l}: loss_pct ${profiles[l].loss_pct} is rounded to ${Math.round(profiles[l].loss_pct)}% for UnityTransport`);
  } else {
    warnings.push('no lag profiles file found (pass --profiles or put lagsim_profiles.json beside the matrix): profile names are not checked');
  }
  const only = { action: csv(values['only-action']), seats: csv(values['only-seats'], true), lag: csv(values['only-lag']) };
  const { cases, skipped } = expand(m, profiles, only);
  if (!cases.length) throw new UsageError('the matrix expands to no cases');
  const W = findWorkspace(path.dirname(matrixFile)) ?? findWorkspace(process.cwd());
  const rel = (f) => (W ? relPosix(W, f) : toPosix(f));
  return { matrixFile, m, profiles, profilesFile, cases, skipped, warnings, W, rel };
}

/** Write the expanded cases for the tests to read; returns the run token stamped into the file. */
function writeCases(file, p) {
  const runToken = `${process.pid}-${Date.now()}-${crypto.randomBytes(4).toString('hex')}`;
  writeJsonAtomic(file, {
    schema: 1,
    matrix: p.rel(p.matrixFile),
    profiles: p.profilesFile ? p.rel(p.profilesFile) : null,
    generated_utc: utcIso(),
    run_token: runToken,
    cases: p.cases,
    skipped: p.skipped,
  });
  return runToken;
}

/** The lock that guards a cases file (Docs/qa/net/matrix.lock for the default one). */
const casesLock = (casesFile) => path.join(path.dirname(casesFile), 'matrix.lock');

/**
 * Upper bound for one gate chunk, longer than every wait the gate bounds itself: it may wait for unity.lock twice (a
 * warm command whose editor goes away re-routes cold and takes the lock again), retry a warm run_tests once, and wait
 * for the editor to settle. The gate's --timeout covers only the Unity job, never the lock wait.
 * @param {{lockWaitMs?: number, timeoutMs?: number, readyTimeoutMs?: number}} cfg gate.config.json values
 * @param {number|undefined} timeoutS the --timeout passed to the gate
 */
export function gateBudgetMs(cfg = {}, timeoutS = undefined) {
  const num = (v, d) => (Number.isFinite(Number(v)) && Number(v) > 0 ? Number(v) : d);
  const lockWait = num(cfg.lockWaitMs, GATE_DEFAULTS.lockWaitMs);
  const job = timeoutS ? timeoutS * 1000 : num(cfg.timeoutMs, GATE_DEFAULTS.timeoutMs);
  const ready = num(cfg.readyTimeoutMs, GATE_DEFAULTS.readyTimeoutMs);
  return 2 * lockWait + 3 * (job + 90000) + ready + 10 * 60 * 1000;
}

/** U/Tools/kit/gate/gate.config.json (defaults when absent), plus a warning when it does not parse. */
function readGateConfig(U) {
  const file = path.join(U, 'Tools', 'kit', 'gate', 'gate.config.json');
  if (!fs.existsSync(file)) return { cfg: {}, warning: null };
  try {
    const j = JSON.parse(readText(file));
    return { cfg: j && typeof j === 'object' ? j : {}, warning: null };
  } catch (e) {
    return { cfg: {}, warning: `gate.config.json does not parse (${e.message}); using the gate defaults for the run budget` };
  }
}

/** The Unity project a gate script works on: through the workspace that holds it, else three folders up (U/Tools/kit/gate). */
export function unityForGate(gate) {
  const ws = findWorkspace(path.dirname(gate));
  if (ws) return kitPaths(ws).unity;
  return path.resolve(path.dirname(gate), '..', '..', '..');
}

function outDir(values, p) {
  if (values.out) return path.resolve(values.out);
  if (p.W) return path.join(p.W, 'Docs', 'qa', 'net');
  return path.dirname(p.matrixFile);
}

function finishReport(values, p, results, resultFiles, extra = {}, warnings = []) {
  const j = judgeResults(p.cases, results, p.m.lag_profiles);
  const allWarnings = [...p.warnings, ...warnings];
  if (extra.warmRuns && j.totals.missing) {
    allWarnings.push(`${j.totals.missing} case(s) missing after ${extra.warmRuns} warm gate run(s): the warm route passes the filter to run_tests --filter_type testName, which may not be a regular expression; rerun with --route cold`);
  }
  delete extra.warmRuns;
  const utc = utcIso();
  const dir = outDir(values, p);
  const stamp = utcFileStamp();
  const md = path.join(dir, `net_matrix_${stamp}.md`);
  const js = path.join(dir, `net_matrix_${stamp}.json`);
  const meta = { matrix: p.rel(p.matrixFile), utc, results: resultFiles.map(p.rel), lag_profiles: p.m.lag_profiles };
  writeTextAtomic(md, reportMarkdown(j, meta) + '\n');
  const body = { schema: 1, utc, matrix: meta.matrix, verdict: j.verdict, totals: j.totals, grid: j.grid, cases: j.cases, skipped: p.skipped, results: meta.results, warnings: allWarnings, ...extra };
  writeJsonAtomic(js, body);
  const ok = j.verdict === 'PASS';
  return {
    result: {
      ok,
      exit: ok ? EXIT.OK : EXIT.FAIL,
      verdict: j.verdict,
      totals: j.totals,
      grid: j.grid,
      failures: j.cases.filter((c) => c.status !== 'passed').map((c) => ({ id: c.id, status: c.status, ...(c.message ? { message: c.message } : {}) })),
      files: { md: p.rel(md), json: p.rel(js) },
      warnings: allWarnings,
      ...extra,
    },
    text: [...allWarnings.map((w) => `WARN ${w}`), `${j.verdict}: ${j.totals.passed}/${j.totals.cases} cases passed (failed ${j.totals.failed}, missing ${j.totals.missing}, skipped ${j.totals.skipped})`, `report: ${p.rel(md)}`],
  };
}

const isGateLine = (j) => Boolean(j) && typeof j === 'object' && !Array.isArray(j) && ('evidence' in j || 'failures' in j) && ('verdict' in j || 'cmd' in j || 'exit' in j);

/**
 * Test results from NUnit XML files, warm run_tests JSON files, or gate --json line files. A gate line's evidence is
 * followed (U-relative, else relative to the line file): NUnit XML on the cold route, run_tests JSON on the warm route.
 * @param {string[]} files
 * @param {string|null} base the Unity project the gate evidence is relative to
 * @returns {{results: object[], used: string[], warnings: string[], lines: object[]}}
 */
export function loadResults(files, base) {
  const results = [];
  const used = [];
  const warnings = [];
  const lines = [];
  const readEvidence = (file, label) => {
    const text = readText(file);
    if (/^\s*</.test(text.replace(/^﻿/, ''))) return parseNunitCases(text);
    const cases = parseRunTestsCases(parseJsonLoose(text));
    if (!cases) warnings.push(`${label}: no test results in it (neither NUnit XML nor a run_tests response with a results list)`);
    return cases ?? [];
  };
  for (const f of files) {
    const abs = path.resolve(f);
    if (!fs.existsSync(abs)) throw new UsageError(`results file not found: ${f}`);
    const text = readText(abs);
    if (/^\s*</.test(text.replace(/^﻿/, ''))) {
      results.push(...parseNunitCases(text));
      used.push(abs);
      continue;
    }
    const j = parseJsonLoose(text);
    if (!isGateLine(j)) {
      const direct = parseRunTestsCases(j);
      if (!direct) throw new UsageError(`results file is neither NUnit XML, a run_tests response nor a gate --json line: ${f}`);
      results.push(...direct);
      used.push(abs);
      continue;
    }
    lines.push(j);
    for (const ev of j.evidence ?? []) {
      if (!/\.(xml|json)$/i.test(ev)) continue;
      const cands = [...(base ? [path.resolve(base, ev)] : []), path.resolve(path.dirname(abs), ev)];
      const hit = cands.find((c) => fs.existsSync(c));
      if (!hit) { warnings.push(`gate evidence not found: ${ev} (looked in ${cands.map(toPosix).join(' and ')})`); continue; }
      results.push(...readEvidence(hit, toPosix(ev)));
      used.push(hit);
    }
    for (const fl of j.failures ?? []) {
      const id = idFromName(fl.name);
      if (id) results.push({ name: fl.name, fullname: fl.name, result: 'Failed', message: fl.message ?? '' });
    }
  }
  return { results, used, warnings, lines };
}

export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    format: { type: 'string', default: 'cases' },
    profiles: { type: 'string' },
    fixture: { type: 'string' },
    write: { type: 'string' },
    out: { type: 'string' },
    results: { type: 'string', multiple: true },
    assemblies: { type: 'string' },
    gate: { type: 'string' },
    timeout: { type: 'string' },
    'only-action': { type: 'string' },
    'only-seats': { type: 'string' },
    'only-lag': { type: 'string' },
    'max-filter': { type: 'string', default: '4000' },
    route: { type: 'string', default: 'cold' },
    'lock-wait': { type: 'string' },
  });
  const sub = positionals[0];
  if (!['expand', 'run', 'report'].includes(sub ?? '')) throw new UsageError('usage: net_matrix.mjs expand|run|report <matrix.json> [--json]');
  if (values.fixture && !/^[A-Za-z_][\w.]*$/.test(values.fixture)) throw new UsageError('--fixture must be a dotted C# type name');
  const maxFilter = Number(values['max-filter']);
  if (!Number.isInteger(maxFilter) || maxFilter < 200) throw new UsageError('--max-filter must be an integer >= 200');
  if (!ROUTES.includes(values.route)) throw new UsageError(`--route must be one of ${ROUTES.join(', ')}`);
  if (values.timeout !== undefined && !(Number(values.timeout) > 0)) throw new UsageError('--timeout must be a number of seconds > 0');
  if (values['lock-wait'] !== undefined && !(Number(values['lock-wait']) >= 0)) throw new UsageError('--lock-wait must be a number of seconds >= 0');
  const p = prepare(values, positionals);

  if (sub === 'expand') {
    if (!['cases', 'filter'].includes(values.format)) throw new UsageError('--format must be cases or filter');
    if (values.write) {
      const file = path.resolve(values.write);
      const l = tryLock(casesLock(file), { purpose: 'net_matrix expand --write' });
      if (!l.ok) throw new CliError(`a net_matrix run holds ${toPosix(casesLock(file))} and its tests read ${toPosix(file)}; wait for it to finish`, EXIT.BLOCKED, { holder: l.holder ?? null });
      try { writeCases(file, p); } finally { l.release(); }
    }
    if (values.format === 'cases') {
      emit({ ok: true, exit: 0, cases: p.cases, skipped: p.skipped, warnings: p.warnings }, { json: values.json, text: p.cases.map((c) => c.id) });
      return EXIT.OK;
    }
    const chunks = filterChunks(p.cases, values.fixture ?? null, maxFilter);
    const res = { ok: true, exit: 0, filter: filterFor(p.cases, values.fixture ?? null), count: p.cases.length, warnings: p.warnings };
    if (chunks.length > 1) res.filters = chunks.map((c) => c.filter);
    emit(res, { json: values.json, text: res.filters ?? [res.filter] });
    return EXIT.OK;
  }

  if (sub === 'report') {
    if (!values.results?.length) throw new UsageError('report needs --results <file>…');
    const U = p.W ? kitPaths(p.W).unity : null;
    const { results, used, warnings, lines } = loadResults(values.results, U);
    const warmRuns = lines.filter((l) => l.route === 'warm').length;
    const { result, text } = finishReport(values, p, results, used, warmRuns ? { warmRuns } : {}, warnings);
    emit(result, { json: values.json, text });
    return result.exit;
  }

  // run
  if (!p.W && !values.gate) throw new CliError('no kit workspace found (Tools/kit/kit.json) and no --gate given', EXIT.INFRA);
  const gate = path.resolve(values.gate ?? path.join(p.W, 'Tools', 'kit', 'gate', 'game_gate.mjs'));
  if (!fs.existsSync(gate)) throw new CliError(`gate not found: ${gate}`, EXIT.INFRA);
  const U = values.gate ? unityForGate(gate) : kitPaths(p.W).unity;
  const { cfg, warning: cfgWarning } = readGateConfig(U);
  const budgetMs = gateBudgetMs(cfg, values.timeout ? Number(values.timeout) : undefined);
  const runWarnings = cfgWarning ? [cfgWarning] : [];
  const casesFile = path.resolve(values.write ?? (p.W ? path.join(p.W, 'Docs', 'qa', 'net', 'matrix_cases.json') : path.join(path.dirname(p.matrixFile), 'matrix_cases.json')));
  const lockWaitMs = values['lock-wait'] !== undefined ? Number(values['lock-wait']) * 1000 : Number(cfg.lockWaitMs) > 0 ? Number(cfg.lockWaitMs) : GATE_DEFAULTS.lockWaitMs;
  let held;
  try {
    held = await lock(casesLock(casesFile), { waitMs: lockWaitMs, pollMs: 500, purpose: 'net_matrix run' });
  } catch (e) {
    if (e.code !== 'ELOCKTIMEOUT') throw e;
    throw new CliError(`another net_matrix run still holds ${toPosix(casesLock(casesFile))} after ${Math.round(lockWaitMs / 1000)} s`, EXIT.BLOCKED, { holder: e.holder ?? null });
  }
  const chunks = filterChunks(p.cases, values.fixture ?? null, maxFilter);
  const lines = [];
  const resultFiles = [];
  try {
    const token = writeCases(casesFile, p);
    for (const [i, ch] of chunks.entries()) {
      const args = [gate, 'test', 'playmode', '--filter', ch.filter, '--json'];
      if (values.route === 'cold') args.push('--cold');
      if (values.route === 'warm') args.push('--warm');
      if (values.assemblies) args.push('--assemblies', values.assemblies);
      if (values.timeout) args.push('--timeout', values.timeout);
      note(`net_matrix: chunk ${i + 1}/${chunks.length}, ${ch.cases.length} case(s), route ${values.route}`);
      const r = await run(process.execPath, args, { cwd: p.W ?? path.dirname(p.matrixFile), timeoutMs: budgetMs });
      const last = r.stdout.split(/\r?\n/).filter((l) => l.trim()).pop() ?? '';
      let j = null;
      try { j = JSON.parse(last); } catch { /* handled below */ }
      if (!j) throw new CliError(`gate printed no --json line (exit ${r.exit}${r.timedOut ? `, killed after the ${Math.round(budgetMs / 1000)} s budget` : ''}): ${r.stderr.trim().slice(-400)}`, EXIT.INFRA);
      lines.push(j);
      let now = null;
      try { now = JSON.parse(readText(casesFile)).run_token ?? null; } catch { /* reported below */ }
      if (now !== token) {
        throw new CliError(`${toPosix(casesFile)} changed while gate chunk ${i + 1} ran (something wrote it without ${toPosix(casesLock(casesFile))}): the tests may have read another case list, so this run is not reported`, EXIT.INFRA, { gate: j });
      }
      if (j.exit === EXIT.BLOCKED || j.exit === EXIT.INFRA || j.exit === EXIT.USAGE) {
        const hint = j.exit === EXIT.BLOCKED && values.route === 'cold' ? ' (an editor holds the project: `game_gate unserve`, close it, or pass --route auto to use it; the warm filter semantics are unverified)' : '';
        throw new CliError(`gate ${j.verdict ?? 'did not run'} (exit ${j.exit}): ${j.infra ?? j.error ?? ''}${hint}`.trim(), j.exit, { gate: j });
      }
      const lineFile = path.join(outDir(values, p), `gate_${utcFileStamp()}_${i + 1}.json`);
      writeJsonAtomic(lineFile, j);
      resultFiles.push(lineFile);
    }
  } finally {
    held.release();
  }
  const { results, used, warnings } = loadResults(resultFiles, U);
  const warmRuns = lines.filter((l) => l.route === 'warm').length;
  const extra = {
    cases_file: p.rel(casesFile),
    route: values.route,
    gate_runs: lines.map((l) => ({ verdict: l.verdict, exit: l.exit, route: l.route ?? null, tests: l.tests ?? null, log: l.log ?? null })),
    ...(warmRuns ? { warmRuns } : {}),
  };
  const { result, text } = finishReport(values, p, results, [...resultFiles, ...used], extra, [...runWarnings, ...warnings]);
  emit(result, { json: values.json, text });
  return result.exit;
}

if (isMain(import.meta.url)) await runCli(main);

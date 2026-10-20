// Shared helpers for the kit hooks (Tools/kit/hooks/*.mjs) and harness tools (Tools/kit/harness/*.mjs).
// Contract: CONTRACTS.md §4.2, §5, §6, §7.2. Hooks import only ../lib/*.mjs and this file; nothing here spawns a process
// except gitHeadCli(), which only gate_close calls.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { now, utcIso, parseUtc } from '../../lib/time.mjs';
import { readText, readJsonOr, readJsonl, readStdinJson, appendJsonl } from '../../lib/jsonio.mjs';
import {
  findWorkspace, readKitConfig, normalizeUnitySubdir, fromShellPath, findSubagentDir, subagentDir,
  findClaudeProjectDirs, parseLaneMarker,
} from '../../lib/paths.mjs';

/** Policy used when .harness/policy.json is missing or partial (mirrors the installer's template). */
export const DEFAULT_POLICY = Object.freeze({
  schema: 1,
  git: { commit: 'ask', init: 'ask', push: 'ask', lfs_migrate: 'ask' },
  write_roots: [],
  provider_hosts: ['queue.fal.run', 'fal.run', 'api.tripo3d.ai', 'openapi.tripo3d.ai', 'api.meshy.ai', 'api.higgsfield.ai',
    'api.scenario.com', 'external.api.recraft.ai', 'api.elevenlabs.io'],
  pinned_packages: { npx: [], uvx: [] },
  inbox: { skip_prompts: [], skip_prefixes: ['GSK-SUPERVISOR:'] },
  pause: { allow_commands: ['node Tools/kit/gate/game_gate.mjs status', 'node Tools/kit/gate/game_gate.mjs history', 'node Tools/kit/harness/next_action.mjs'] },
  ttl_min: { override: 30, unlock: 60, backup_bundle: 10 },
  kit_writers: ['node Tools/kit/harness/gate_close.mjs', 'node Tools/kit/gate/game_gate.mjs', 'node Tools/kit/qa/probe_report.mjs',
    'uv run --project Tools/kit/assetgen assetgen', 'Tools/kit/bin/assetgen'],
  secret_env_names: ['FAL_KEY', 'MESHY_API_KEY', 'HF_TOKEN', 'TRIPO_API_KEY'],
  retention: { keep_builds: 3, session_log_days: 7 },
  owner: { noninteractive_entrypoints: ['sdk-cli', 'sdk-ts', 'sdk-py'] },
});

export const isObj = (v) => v !== null && typeof v === 'object' && !Array.isArray(v);

/** Deep merge: objects merge, everything else (arrays included) replaces. */
export function deepMerge(a, b) {
  if (!isObj(a) || !isObj(b)) return b === undefined ? a : b;
  const out = { ...a };
  for (const [k, v] of Object.entries(b)) out[k] = isObj(v) && isObj(a[k]) ? deepMerge(a[k], v) : v;
  return out;
}

const clone = (v) => JSON.parse(JSON.stringify(v));

/** Hook input as a plain object ({} for malformed stdin; `invalid` says so). */
export function asInput(raw) {
  if (!isObj(raw) || raw.__invalid) return { input: {}, invalid: true };
  return { input: raw, invalid: false };
}

/** W for a hook call: CLAUDE_PROJECT_DIR, else the kit workspace above stdin cwd, else stdin cwd, else process cwd. */
export function resolveWorkspace(input = {}) {
  const env = process.env.CLAUDE_PROJECT_DIR;
  if (env) {
    const p = path.resolve(fromShellPath(env));
    if (fs.existsSync(p)) return p;
  }
  const cwd = typeof input.cwd === 'string' && input.cwd ? fromShellPath(input.cwd) : null;
  const found = findWorkspace(cwd || process.cwd());
  if (found) return found;
  return path.resolve(cwd || process.cwd());
}

/** The Unity subdir from kit.json ('.' when absent or invalid). */
export function unitySubdirOf(ws) {
  try {
    return normalizeUnitySubdir(readKitConfig(ws)?.unity_subdir ?? '.');
  } catch {
    return '.';
  }
}

export function unityRoot(ws) {
  const sub = unitySubdirOf(ws);
  return sub === '.' ? ws : path.join(ws, sub);
}

const H = (ws, ...p) => path.join(ws, '.harness', ...p);
const D = (ws, ...p) => path.join(ws, 'Docs', ...p);

/**
 * .harness/policy.json merged over DEFAULT_POLICY. Never throws: an unreadable or invalid file gives the defaults
 * and an `error` string (so protection keeps working when the file is broken).
 * @returns {{policy: object, error: string|null, exists: boolean}}
 */
export function loadPolicy(ws) {
  const file = H(ws, 'policy.json');
  if (!fs.existsSync(file)) return { policy: clone(DEFAULT_POLICY), error: null, exists: false };
  try {
    const p = JSON.parse(readText(file));
    if (!isObj(p)) throw new Error('policy.json is not an object');
    return { policy: deepMerge(clone(DEFAULT_POLICY), p), error: null, exists: true };
  } catch (e) {
    return { policy: clone(DEFAULT_POLICY), error: `policy.json unreadable: ${e.message}`, exists: true };
  }
}

/** Append an internal hook error (never throws). Guard errors use guard_errors.jsonl (guardErrorLine). */
export function logHookError(ws, hook, err, extra = {}) {
  const msg = String(err && err.stack ? err.message : err).slice(0, 2000);
  try { process.stderr.write(`[${hook}] internal error: ${msg}\n`); } catch { /* ignore */ }
  if (!ws) return;
  try {
    if (hook === 'guard') appendJsonl(H(ws, 'guard_errors.jsonl'), { utc: utcIso(now()), error: msg, ...extra });
    else appendJsonl(H(ws, 'hook_errors.jsonl'), { utc: utcIso(now()), hook, error: msg, ...extra });
  } catch { /* logging must never break a hook */ }
}

/**
 * Run a hook body with the kit's hook rules: stdin parsed with readStdinJson (never throws), any internal error logged
 * and turned into "no output, exit 0". The body returns null (allow, no output) or {stdout?: object, stderr?: string,
 * exit?: number}. Only cs_check_async returns exit 2.
 * @param {string} name
 * @param {(input: object, ctx: {ws: string, invalid: boolean, raw: any}) => Promise<object|null>|object|null} body
 */
export async function runHook(name, body) {
  let raw = {};
  let ws = null;
  let res = null;
  try {
    raw = await readStdinJson({ timeoutMs: 5000 });
  } catch {
    raw = { __invalid: true };
  }
  try {
    const { input, invalid } = asInput(raw);
    ws = resolveWorkspace(input);
    res = await body(input, { ws, invalid, raw });
  } catch (e) {
    logHookError(ws, name, e, name === 'guard' ? { fallback: 'none' } : {});
    res = null;
  }
  try {
    if (res && res.stdout !== undefined && res.stdout !== null) {
      process.stdout.write((typeof res.stdout === 'string' ? res.stdout : JSON.stringify(res.stdout)) + '\n');
    }
    if (res && res.stderr) process.stderr.write(res.stderr.endsWith('\n') ? res.stderr : res.stderr + '\n');
  } catch { /* a closed pipe must not crash the hook */ }
  process.exitCode = res && Number.isInteger(res.exit) ? res.exit : 0;
}

// ---------------------------------------------------------------------------------------------------------------
// Small file helpers

export function exists(p) {
  try { fs.accessSync(p); return true; } catch { return false; }
}

export function mtimeMs(p) {
  try { return Math.floor(fs.statSync(p).mtimeMs); } catch { return null; }
}

export function sizeOf(p) {
  try { return fs.statSync(p).size; } catch { return null; }
}

export function safeText(p) {
  try { return readText(p); } catch { return null; }
}

/** Last `bytes` of a text file (UTF-8), starting at a line boundary. Empty string when missing. */
export function tailText(p, bytes = 256 * 1024) {
  let fd;
  try {
    const size = fs.statSync(p).size;
    if (size <= bytes) return safeText(p) ?? '';
    fd = fs.openSync(p, 'r');
    const buf = Buffer.alloc(bytes);
    fs.readSync(fd, buf, 0, bytes, size - bytes);
    const s = buf.toString('utf8');
    const nl = s.indexOf('\n');
    return nl >= 0 ? s.slice(nl + 1) : s;
  } catch {
    return '';
  } finally {
    if (fd !== undefined) try { fs.closeSync(fd); } catch { /* ignore */ }
  }
}

/** First `bytes` of a text file. */
export function headText(p, bytes = 256 * 1024) {
  let fd;
  try {
    fd = fs.openSync(p, 'r');
    const buf = Buffer.alloc(bytes);
    const n = fs.readSync(fd, buf, 0, bytes, 0);
    let s = buf.subarray(0, n).toString('utf8');
    if (s.charCodeAt(0) === 0xfeff) s = s.slice(1);
    return s;
  } catch {
    return '';
  } finally {
    if (fd !== undefined) try { fs.closeSync(fd); } catch { /* ignore */ }
  }
}

export const clip = (s, n) => {
  const t = String(s ?? '');
  return t.length > n ? t.slice(0, Math.max(0, n - 1)) + '…' : t;
};

// ---------------------------------------------------------------------------------------------------------------
// Run window (.harness/run.json)

export function readRun(ws) {
  const r = readJsonOr(H(ws, 'run.json'), null, { onInvalid: 'fallback' });
  return isObj(r) ? r : null;
}

/**
 * Status of a run.json document at time t.
 * @returns {{active: boolean, armed: boolean, expired: boolean, started_utc: string|null, window_end_utc: string|null,
 *   elapsed_pct: number|null, remaining_min: number|null, freeze: boolean, freeze_at_pct: number, name: string|null, skills: string[]}}
 */
export function runStatus(run, t = now()) {
  const start = parseUtc(run?.started_utc);
  const end = parseUtc(run?.window_end_utc);
  const freezeAt = typeof run?.freeze_at_pct === 'number' ? run.freeze_at_pct : 75;
  const flagged = run?.active === true;
  const armed = flagged && Boolean(end) && t.getTime() < end.getTime();
  let elapsed = null;
  let remaining = null;
  if (start && end && end > start) {
    elapsed = Math.round(Math.min(100, Math.max(0, ((t - start) / (end - start)) * 100)) * 10) / 10;
    remaining = Math.max(0, Math.round((end - t) / 60000));
  }
  return {
    active: armed,
    armed,
    expired: flagged && Boolean(end) && !armed,
    started_utc: run?.started_utc ?? null,
    window_end_utc: run?.window_end_utc ?? null,
    elapsed_pct: elapsed,
    remaining_min: remaining,
    freeze: armed && elapsed !== null && elapsed >= freezeAt,
    freeze_at_pct: freezeAt,
    name: typeof run?.name === 'string' ? run.name : null,
    skills: Array.isArray(run?.skills) ? run.skills.filter((s) => typeof s === 'string') : [],
  };
}

// ---------------------------------------------------------------------------------------------------------------
// Docs parsers (CONTRACTS.md §7.2)

const INBOX_HEAD = /^## (\S+) #(\d+)[ \t]*$/;

/** Owner inbox entries: [{id, utc, status, text}] in file order. */
export function parseInbox(text) {
  const out = [];
  if (!text) return out;
  const lines = text.split(/\r?\n/);
  let cur = null;
  const close = () => {
    if (!cur) return;
    let status = 'new';
    for (let i = cur.body.length - 1; i >= 0; i--) {
      const m = /^status:\s*(\S+)/.exec(cur.body[i]);
      if (m) { status = m[1]; cur.body.splice(i, 1); break; }
    }
    out.push({ id: cur.id, utc: cur.utc, status, text: cur.body.join('\n').trim() });
    cur = null;
  };
  for (const l of lines) {
    const m = INBOX_HEAD.exec(l);
    if (m) {
      close();
      cur = { id: Number(m[2]), utc: m[1], body: [] };
    } else if (cur) cur.body.push(l);
  }
  close();
  return out;
}

/** Inbox ids acknowledged in Docs/owner_acks.md (`- #<n> <UTC> <ack>`). */
export function parseAcks(text) {
  const ids = new Set();
  for (const m of String(text || '').matchAll(/^\s*-\s*#(\d+)\b/gm)) ids.add(Number(m[1]));
  return ids;
}

/** Owner inbox entries with status new and no ack line. */
export function unackedInbox(ws) {
  const inbox = parseInbox(safeText(D(ws, 'OWNER_INBOX.md')) || '');
  const acks = parseAcks(safeText(D(ws, 'owner_acks.md')) || '');
  return inbox.filter((e) => e.status === 'new' && !acks.has(e.id));
}

/** TODO items `- [ ] <id> <task> (owner: x) (gate: y)`. */
export function parseTodo(text) {
  const out = [];
  for (const m of String(text || '').matchAll(/^\s*-\s*\[( |x|X)\]\s+(\S+)\s*(.*)$/gm)) {
    const rest = m[3] || '';
    const gate = /\(gate:\s*([^)]+)\)/.exec(rest)?.[1]?.trim() ?? null;
    const owner = /\(owner:\s*([^)]+)\)/.exec(rest)?.[1]?.trim() ?? null;
    out.push({ done: m[1] !== ' ', id: m[2], text: rest.trim(), gate, owner });
  }
  return out;
}

/** KNOWN_ISSUES table rows `| KI-n | severity | found_at_gate | recheck_at_gate | status | evidence |`. */
export function parseKnownIssues(text) {
  const out = [];
  for (const line of String(text || '').split(/\r?\n/)) {
    if (!line.trim().startsWith('|')) continue;
    const cells = line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map((c) => c.trim());
    if (!/^KI-\d+$/.test(cells[0] || '')) continue;
    out.push({ id: cells[0], severity: cells[1] || '', found_at_gate: cells[2] || '', recheck_at_gate: cells[3] || '', status: (cells[4] || '').toLowerCase(), evidence: cells[5] || '' });
  }
  return out;
}

const DEVLOG_HEAD = /^## (\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z)[ \t]*(.*)$/gm;

/**
 * Newest `## <UTC> <title>` entry of Docs/DEVLOG.md (tail read; falls back to the newest Docs/devlog/<n>.md).
 * With opts.notAfter (a Date), headings dated after it are ignored and listed in `future` of the result.
 * @param {string} ws
 * @param {{notAfter?: Date}} [opts]
 */
export function lastDevlogEntry(ws, opts = {}) {
  const limit = opts.notAfter instanceof Date ? opts.notAfter.getTime() : null;
  const future = [];
  const pick = (text) => {
    let best = null;
    for (const m of String(text || '').matchAll(DEVLOG_HEAD)) {
      const d = parseUtc(m[1]);
      if (!d) continue;
      if (limit !== null && d.getTime() > limit) { future.push(m[1]); continue; }
      if (!best || d >= best.date) best = { utc: m[1], title: m[2].trim(), date: d, future };
    }
    return best;
  };
  const cur = pick(tailText(D(ws, 'DEVLOG.md'), 512 * 1024));
  if (cur) return cur;
  try {
    const dir = D(ws, 'devlog');
    const files = fs.readdirSync(dir).filter((f) => f.endsWith('.md')).map((f) => path.join(dir, f));
    files.sort((a, b) => (mtimeMs(b) ?? 0) - (mtimeMs(a) ?? 0));
    for (const f of files) {
      const e = pick(tailText(f, 512 * 1024));
      if (e) return e;
    }
  } catch { /* no archive */ }
  return null;
}

// ---------------------------------------------------------------------------------------------------------------
// Gate ledger

export function readGates(ws) {
  const g = readJsonOr(D(ws, 'qa', 'gates.json'), null, { onInvalid: 'fallback' });
  return isObj(g) && Array.isArray(g.milestones) ? g : null;
}

/** Latest ledger line per row id (file order: the ledger is append-only). */
export function latestVerdicts(ws, rows = null) {
  const lines = rows ?? readJsonl(D(ws, 'qa', 'gates.jsonl'));
  const m = new Map();
  for (const r of lines) if (isObj(r) && typeof r.row === 'string') m.set(r.row, r);
  return m;
}

/** The first open milestone of gates.json (array order), or null. */
export function currentMilestone(gates) {
  if (!gates) return null;
  return gates.milestones.find((m) => isObj(m) && m.status !== 'closed') ?? null;
}

// ---------------------------------------------------------------------------------------------------------------
// Waves

/** The most recently updated Docs/waves/*.json (by mtime) as {file, manifest}, or null. */
export function newestWaveManifest(ws) {
  let files = [];
  try {
    files = fs.readdirSync(D(ws, 'waves')).filter((f) => f.endsWith('.json')).map((f) => D(ws, 'waves', f));
  } catch {
    return null;
  }
  let best = null;
  for (const f of files) {
    const t = mtimeMs(f) ?? 0;
    if (!best || t > best.t) best = { f, t };
  }
  if (!best) return null;
  const doc = readJsonOr(best.f, null, { onInvalid: 'fallback' });
  if (!isObj(doc)) return null;
  // Readers spread units/contracts: a hand-damaged manifest must not crash a hook.
  const manifest = { ...doc, units: Array.isArray(doc.units) ? doc.units : [], contracts: Array.isArray(doc.contracts) ? doc.contracts : [] };
  return { file: best.f, manifest };
}

export function unitStatusMap(manifest) {
  const out = {};
  const arr = (v) => (Array.isArray(v) ? v : []);
  for (const u of [...arr(manifest?.contracts), ...arr(manifest?.units)]) {
    if (isObj(u) && typeof u.id === 'string') out[u.id] = ['todo', 'running', 'done', 'blocked', 'failed'].includes(u.status) ? u.status : 'todo';
  }
  return out;
}

// ---------------------------------------------------------------------------------------------------------------
// Alerts (.harness/alerts/<name>, health.json alerts)

const ALERT_LEVEL = { disk_warn: 'warn', disk_stop: 'stop', disk_emergency: 'emergency', commit_high: 'stop', ram_low: 'warn',
  unity_restart_due: 'warn', pagefile_growth: 'info', secret_in_prompt: 'warn', secret_in_transcript: 'warn', waiting_for_owner: 'warn' };

/** Alerts from .harness/alerts/* files and health.json, deduplicated: [{name, level, message, utc}]. */
export function listAlerts(ws) {
  const byName = new Map();
  try {
    for (const name of fs.readdirSync(H(ws, 'alerts'))) {
      if (name.startsWith('.')) continue;
      const doc = readJsonOr(H(ws, 'alerts', name), null, { onInvalid: 'fallback' });
      byName.set(name, {
        name,
        level: isObj(doc) && typeof doc.level === 'string' ? doc.level : (ALERT_LEVEL[name] || 'warn'),
        message: isObj(doc) && typeof doc.message === 'string' ? doc.message : '',
        utc: isObj(doc) && typeof doc.utc === 'string' ? doc.utc : null,
      });
    }
  } catch { /* no alerts folder */ }
  const health = readJsonOr(H(ws, 'health.json'), null, { onInvalid: 'fallback' });
  if (isObj(health) && Array.isArray(health.alerts)) {
    for (const n of health.alerts) {
      if (typeof n === 'string' && !byName.has(n)) byName.set(n, { name: n, level: ALERT_LEVEL[n] || 'warn', message: '', utc: health.utc ?? null });
    }
  }
  return [...byName.values()];
}

// ---------------------------------------------------------------------------------------------------------------
// next_action (ARCHITECTURE §7.4 step 1)

/**
 * The single next thing the main loop should do.
 * Priority: stop/emergency alerts > PAUSE > unacknowledged owner inbox > freeze (≥ freeze_at_pct of the run window)
 * > failing gate rows (fix) > a wave with todo/failed units > the open milestone's gate > the first open TODO > idle.
 * @returns {{kind: string, detail: string, refs: string[], milestone: string|null, wave: string|null}}
 */
export function nextAction(ws, t = now()) {
  const res = (kind, detail, refs = [], extra = {}) => ({ kind, detail, refs, milestone: extra.milestone ?? null, wave: extra.wave ?? null });
  const gates = readGates(ws);
  const cur = currentMilestone(gates);
  const mid = cur?.id ?? null;

  const hard = listAlerts(ws).filter((a) => a.level === 'stop' || a.level === 'emergency');
  if (hard.length) {
    return res('alert', `Health alert: ${hard.map((a) => `${a.name}${a.message ? ` (${a.message})` : ''}`).join('; ')}. Stop heavy lanes and follow the runbook before new work.`,
      hard.map((a) => `.harness/alerts/${a.name}`), { milestone: mid });
  }
  if (exists(H(ws, 'PAUSE'))) {
    return res('paused', '.harness/PAUSE exists (owner). Read-only work only until the owner types OWNER: resume.', ['.harness/PAUSE'], { milestone: mid });
  }
  const un = unackedInbox(ws);
  if (un.length) {
    const ids = un.slice(0, 8).map((e) => `#${e.id}`).join(', ');
    return res('owner', `Owner inbox: ${un.length} unacknowledged (${ids}). Read Docs/OWNER_INBOX.md, act on it or log a DECISION or CHANGE_REQUEST, then ack in Docs/owner_acks.md.`,
      ['Docs/OWNER_INBOX.md', 'Docs/owner_acks.md'], { milestone: mid });
  }
  const run = runStatus(readRun(ws), t);
  if (run.freeze) {
    const wrap = gates?.milestones?.find((m) => m?.id === 'wrapup');
    if (wrap?.status === 'closed') {
      return res('freeze', 'Wrap-up gate is closed. Create .harness/ALLOW_STOP and write the final DEVLOG entry.', ['.harness/ALLOW_STOP'], { milestone: 'wrapup' });
    }
    return res('freeze', `Freeze: ${run.elapsed_pct}% of the run window elapsed (freeze at ${run.freeze_at_pct}%). No new features. Sequence: build, smoke, perf, captures, trailer cut, report_gen, then node Tools/kit/harness/gate_close.mjs wrapup.`,
      ['.harness/run.json', 'Docs/qa/gates.json'], { milestone: 'wrapup' });
  }
  const latest = gates ? latestVerdicts(ws) : new Map();
  if (cur) {
    const failing = (cur.requires || []).filter((r) => isObj(r) && ['FAIL', 'BLOCKED'].includes(latest.get(r.id)?.verdict));
    if (failing.length) {
      return res('fix', `${cur.id}: fix failing rows ${failing.map((r) => `${r.id} (${latest.get(r.id).verdict})`).join(', ')}.`,
        ['Docs/qa/gates.jsonl', cur.card].filter(Boolean), { milestone: cur.id });
    }
  }
  const wave = newestWaveManifest(ws);
  if (wave) {
    const all = [...(wave.manifest.contracts || []), ...(wave.manifest.units || [])].filter(isObj);
    const pending = all.filter((u) => u.status === 'todo' || u.status === 'failed');
    if (pending.length) {
      const w = wave.manifest.wave;
      return res('wave', `Wave ${w}: ${pending.length} unit(s) todo or failed (${pending.slice(0, 8).map((u) => u.id).join(', ')}). Run node Tools/kit/harness/wave_manifest.mjs args ${w} and launch build_wave with those args.`,
        [path.relative(ws, wave.file).split(path.sep).join('/')], { milestone: wave.manifest.milestone ?? mid, wave: w });
    }
  }
  if (cur) {
    const rows = (cur.requires || []).filter(isObj);
    const open = rows.filter((r) => r.evidence !== 'human' && r.evidence !== 'file' && latest.get(r.id)?.verdict !== 'PASS');
    if (open.length) {
      return res('gate', `${cur.id}: rows without a PASS: ${open.map((r) => r.id).join(', ')}. Run them (game_gate --row, probe_report --row).`,
        [cur.card].filter(Boolean), { milestone: cur.id });
    }
    return res('gate', `${cur.id}: every machine row has a PASS. Run node Tools/kit/harness/gate_close.mjs ${cur.id}.`, [cur.card].filter(Boolean), { milestone: cur.id });
  }
  const todo = parseTodo(safeText(D(ws, 'TODO.md')) || '').find((x) => !x.done);
  if (todo) return res('fix', `TODO ${todo.id}: ${clip(todo.text, 300)}`, ['Docs/TODO.md'], { milestone: mid });
  return res('idle', 'Nothing queued: no open milestone, wave or TODO. Plan the next milestone (Docs/PLAN.md) or ask the owner for a human-only step.', ['Docs/PLAN.md'], { milestone: mid });
}

// ---------------------------------------------------------------------------------------------------------------
// Secrets (owner_inbox redaction; CONTRACTS.md §5.2)

const SECRET_PATTERNS = [
  ['pem', /-----BEGIN [A-Z0-9 ]*(?:PRIVATE )?KEY-----[\s\S]*?(?:-----END [A-Z0-9 ]*KEY-----|$)/g],
  ['sk', /\bsk-[A-Za-z0-9_-]{16,}/g],
  ['hf', /\bhf_[A-Za-z0-9]{16,}/g],
  ['msy', /\bmsy_[A-Za-z0-9_-]{12,}/g],
  ['aws', /\bAKIA[0-9A-Z]{16}\b/g],
  ['github', /\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{20,}\b|\bgithub_pat_[A-Za-z0-9_]{20,}/g],
  ['slack', /\bxox[abprs]-[A-Za-z0-9-]{10,}/g],
  ['google', /\bAIza[0-9A-Za-z_-]{30,}/g],
  ['bearer', /\bBearer\s+[A-Za-z0-9._~+/=-]{8,}/gi],
  ['fal', /\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}:[0-9a-f]{16,}\b/gi],
];

/** True when an env var name is a secret name for G14 and the inbox redaction. */
export function isSecretName(name, policy = DEFAULT_POLICY) {
  const n = String(name || '').toUpperCase();
  if (!n) return false;
  if (/_KEY$|_TOKEN$|SECRET|PASSWORD/.test(n)) return true;
  return (policy?.secret_env_names || []).some((x) => String(x).toUpperCase() === n);
}

/**
 * Replace secrets with [REDACTED:<kind>]: known key formats, PEM blocks, and the live value (≥ 8 chars) of any env var
 * with a secret name. Values are never returned.
 * @returns {{text: string, kinds: string[]}}
 */
export function redactSecrets(text, env = process.env, policy = DEFAULT_POLICY) {
  let out = String(text ?? '');
  const kinds = new Set();
  const values = [];
  for (const [k, v] of Object.entries(env || {})) {
    if (typeof v === 'string' && v.length >= 8 && isSecretName(k, policy)) values.push([k, v]);
  }
  values.sort((a, b) => b[1].length - a[1].length);
  for (const [k, v] of values) {
    if (out.includes(v)) {
      out = out.split(v).join(`[REDACTED:env:${k}]`);
      kinds.add(`env:${k}`);
    }
  }
  for (const [kind, re] of SECRET_PATTERNS) {
    out = out.replace(re, () => {
      kinds.add(kind);
      return `[REDACTED:${kind}]`;
    });
  }
  return { text: out, kinds: [...kinds] };
}

// ---------------------------------------------------------------------------------------------------------------
// Owner commands (owner_inbox applies them; gate_close and wave_manifest resolve verify them in transcripts)

/** Gate row ids (`M0-05`, `CR-3`) and guard-hit ids (`GH-001`) the owner can confirm. */
export const OWNER_ROW_ID = /^([A-Za-z][A-Za-z0-9_]{0,31}-[0-9]{2,3}[a-z]?|CR-[0-9]+|GH-[0-9]+)$/;

/**
 * OWNER: / OWNER-OVERRIDE: commands found on the prompt's lines (trimmed line starts with the prefix). The one parser
 * both the inbox hook and the transcript cross-check use, so what the inbox applies is what gate_close accepts.
 * @param {string} prompt
 * @returns {{cmd: string, row?: string, command?: string, line: string}[]}
 */
export function parseCommands(prompt) {
  const out = [];
  for (const raw of String(prompt).split(/\r?\n/)) {
    const line = raw.trim();
    let m = /^OWNER-OVERRIDE:\s*(.+)$/.exec(line);
    if (m) { out.push({ cmd: 'override', command: m[1].trim(), line }); continue; }
    m = /^OWNER:\s*(.*)$/.exec(line);
    if (!m) continue;
    const rest = m[1].trim();
    const words = rest.split(/\s+/);
    const verb = (words[0] || '').toLowerCase();
    if (verb === 'pause' && words.length === 1) out.push({ cmd: 'pause', line });
    else if (verb === 'resume' && words.length === 1) out.push({ cmd: 'resume', line });
    else if (verb === 'stop' && words.length === 1) out.push({ cmd: 'stop', line });
    else if (verb === 'confirm' && words[1] && OWNER_ROW_ID.test(words[1])) out.push({ cmd: 'confirm', row: words[1], line });
    else if (verb === 'unlock' && (words[1] || '').toLowerCase() === 'harness') out.push({ cmd: 'unlock', line });
    else if (verb === 'lock' && (words[1] || '').toLowerCase() === 'harness') out.push({ cmd: 'lock', line });
    else out.push({ cmd: 'unknown', line });
  }
  return out;
}

/** Rows confirmed by a text (parseCommands confirm commands). */
export function confirmedRows(text) {
  return parseCommands(text).filter((c) => c.cmd === 'confirm').map((c) => c.row);
}

// ---------------------------------------------------------------------------------------------------------------
// Transcripts

/** User-typed texts of one transcript line (CONTRACTS.md §4.2): string content, each text part, and the parts joined. */
export function userTexts(line) {
  if (!isObj(line) || line.type !== 'user' || !isObj(line.message) || line.message.role !== 'user') return [];
  const c = line.message.content;
  if (typeof c === 'string') return [c];
  if (!Array.isArray(c)) return [];
  const parts = c.filter((p) => isObj(p) && p.type === 'text' && typeof p.text === 'string').map((p) => p.text);
  if (!parts.length) return [];
  return parts.length > 1 ? [...parts, parts.join('\n')] : parts;
}

/** The first user-typed text of a JSONL transcript (read from the head only), or ''. */
export function firstUserText(file, bytes = 256 * 1024) {
  const head = headText(file, bytes);
  for (const l of head.split(/\r?\n/)) {
    if (!l.trim()) continue;
    let o;
    try { o = JSON.parse(l); } catch { continue; }
    const t = userTexts(o);
    if (t.length) return t[t.length - 1];
  }
  return '';
}

/**
 * The transcript of a subagent: stdin agent_transcript_path, else `<session>/subagents/agent-<id>.jsonl` next to the
 * main transcript (found, hinted, or built), else in any project folder of W. Null when none exists.
 */
export function findAgentTranscript(input, ws) {
  const cands = [];
  if (typeof input.agent_transcript_path === 'string' && input.agent_transcript_path) cands.push(path.resolve(fromShellPath(input.agent_transcript_path)));
  const sid = typeof input.session_id === 'string' ? input.session_id : null;
  const aid = typeof input.agent_id === 'string' ? input.agent_id : null;
  if (sid && aid && /^[A-Za-z0-9_.-]+$/.test(aid)) {
    const file = `agent-${aid}.jsonl`;
    const dirs = [];
    try {
      const d = findSubagentDir(sid, { cwd: ws, hint: typeof input.transcript_path === 'string' ? input.transcript_path : null });
      if (d) dirs.push(d);
    } catch { /* ignore */ }
    if (typeof input.transcript_path === 'string' && input.transcript_path) {
      dirs.push(path.join(path.dirname(path.resolve(fromShellPath(input.transcript_path))), sid, 'subagents'));
    }
    dirs.push(subagentDir(sid, ws));
    for (const d of findClaudeProjectDirs(ws)) dirs.push(path.join(d, sid, 'subagents'));
    for (const d of dirs) cands.push(path.join(d, file));
  }
  for (const c of cands) if (exists(c)) return c;
  return null;
}

/** The GSK-LANE marker in the first user message of a subagent transcript, or null. */
export function laneOfAgent(input, ws) {
  const t = findAgentTranscript(input, ws);
  if (!t) return null;
  const m = parseLaneMarker(firstUserText(t));
  return m ? { ...m, transcript: t } : null;
}

// ---------------------------------------------------------------------------------------------------------------
// Git (read without spawning; gate_close uses the CLI)

/** HEAD commit id read from .git files (loose or packed ref), or null when there is no repo. */
export function gitHead(ws) {
  let gitDir = path.join(ws, '.git');
  try {
    const st = fs.statSync(gitDir);
    if (st.isFile()) {
      const m = /^gitdir:\s*(.+)$/m.exec(fs.readFileSync(gitDir, 'utf8'));
      if (!m) return null;
      gitDir = path.resolve(ws, m[1].trim());
    }
  } catch {
    return null;
  }
  try {
    const head = fs.readFileSync(path.join(gitDir, 'HEAD'), 'utf8').trim();
    if (/^[0-9a-f]{40,64}$/.test(head)) return head;
    const ref = /^ref:\s*(.+)$/.exec(head)?.[1]?.trim();
    if (!ref) return null;
    const loose = path.join(gitDir, ...ref.split('/'));
    if (exists(loose)) return fs.readFileSync(loose, 'utf8').trim() || null;
    const packed = safeText(path.join(gitDir, 'packed-refs')) || '';
    for (const l of packed.split(/\r?\n/)) {
      const [sha, name] = l.split(' ');
      if (name === ref && /^[0-9a-f]{40,64}$/.test(sha)) return sha;
    }
    return null;
  } catch {
    return null;
  }
}

/** `git rev-parse HEAD` in ws, or null (no git, no repo, or no commit yet). */
export function gitHeadCli(ws) {
  try {
    const r = spawnSync('git', ['rev-parse', 'HEAD'], { cwd: ws, encoding: 'utf8', windowsHide: true, timeout: 20000 });
    const s = (r.stdout || '').trim();
    return r.status === 0 && /^[0-9a-f]{40,64}$/.test(s) ? s : null;
  } catch {
    return null;
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Usage-limit reset times ("resets 4:10am (Asia/Hong_Kong)", "resets Mon 12:00am")

const DAYS = ['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat'];

function zoneParts(tz, ms) {
  const f = new Intl.DateTimeFormat('en-US', { timeZone: tz, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', weekday: 'short' });
  const o = {};
  for (const p of f.formatToParts(new Date(ms))) o[p.type] = p.value;
  return { y: +o.year, mo: +o.month, d: +o.day, h: +o.hour % 24, mi: +o.minute, s: +o.second, wd: DAYS.indexOf(String(o.weekday).slice(0, 3).toLowerCase()) };
}

/** UTC ms of the wall-clock time y-mo-d h:mi in tz. */
function zonedToUtc(tz, y, mo, d, h, mi) {
  let guess = Date.UTC(y, mo - 1, d, h, mi);
  for (let i = 0; i < 3; i++) {
    const p = zoneParts(tz, guess);
    const asUtc = Date.UTC(p.y, p.mo - 1, p.d, p.h, p.mi, p.s);
    const diff = asUtc - Date.UTC(y, mo - 1, d, h, mi);
    if (diff === 0) break;
    guess -= diff;
  }
  return guess;
}

/**
 * Parse `resets <time> (<zone>)` out of a message and return the next such moment after `t`.
 * @returns {{resets_text: string|null, resets_utc: string|null}}
 */
export function parseResets(message, t = now()) {
  const text = String(message || '');
  const m = /resets\s+(?:(mon|tue|wed|thu|fri|sat|sun)[a-z]*\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)?(?:\s*\(([A-Za-z_]+(?:\/[A-Za-z0-9_+-]+)*)\))?/i.exec(text);
  if (!m) return { resets_text: null, resets_utc: null };
  const resetsText = m[0].trim();
  let h = Number(m[2]);
  const mi = Number(m[3] || 0);
  const ap = (m[4] || '').toLowerCase();
  if (ap === 'pm' && h < 12) h += 12;
  if (ap === 'am' && h === 12) h = 0;
  if (h > 23 || mi > 59) return { resets_text: resetsText, resets_utc: null };
  let tz = m[5] || null;
  if (!tz) {
    try { tz = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'; } catch { tz = 'UTC'; }
  }
  try {
    zoneParts(tz, t.getTime());
  } catch {
    return { resets_text: resetsText, resets_utc: null };
  }
  const wd = m[1] ? DAYS.indexOf(m[1].toLowerCase().slice(0, 3)) : -1;
  const today = zoneParts(tz, t.getTime());
  for (let add = 0; add <= 8; add++) {
    const base = new Date(Date.UTC(today.y, today.mo - 1, today.d + add));
    const cand = zonedToUtc(tz, base.getUTCFullYear(), base.getUTCMonth() + 1, base.getUTCDate(), h, mi);
    if (cand <= t.getTime()) continue;
    if (wd >= 0 && zoneParts(tz, cand).wd !== wd) continue;
    return { resets_text: resetsText, resets_utc: utcIso(new Date(cand)) };
  }
  return { resets_text: resetsText, resets_utc: null };
}

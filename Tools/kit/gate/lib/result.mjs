// The gate's verdict record: the one --json line (gate-json-line.schema.json), the history line, the gates.jsonl row
// and the human summary. The verdict fixes the exit code: PASS 0, FAIL 1, INFRA 3, BLOCKED 75, NOT-RUN 2|3|75.
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, emit, note } from '../../lib/cli.mjs';
import { sha256File, treeHash, TREE_EXCLUDES, TREE_ROOTS } from '../../lib/hash.mjs';
import { appendJsonl, readJsonOr } from '../../lib/jsonio.mjs';
import { relPosix, toPosix } from '../../lib/paths.mjs';
import { run } from '../../lib/proc.mjs';
import { now, utcFileStamp, utcIso } from '../../lib/time.mjs';

export const VERDICT_EXIT = Object.freeze({ PASS: EXIT.OK, FAIL: EXIT.FAIL, INFRA: EXIT.INFRA, BLOCKED: EXIT.BLOCKED });

/**
 * A fresh result for `cmd` (the subcommand and its arguments as run).
 * @param {string} cmd
 * @param {'R0'|'R1'|'R2'|'R3'|'R4'|null} rung
 */
export function newResult(cmd, rung) {
  return {
    cmd,
    rung,
    ok: false,
    exit: EXIT.FAIL,
    verdict: 'FAIL',
    utc: utcIso(now()),
    secs: 0,
    route: 'none',
    errors: [],
    failures: [],
    log: null,
    evidence: [],
    infra: null,
    slot: null,
  };
}

/**
 * Set the verdict and derive ok/exit. NOT-RUN takes an explicit exit (2 usage or missing prerequisite, 3 infra, 75 held).
 * @param {object} res
 * @param {'PASS'|'FAIL'|'INFRA'|'BLOCKED'|'NOT-RUN'} verdict
 * @param {{exit?: number, infra?: string|null, message?: string}} [opts]
 */
export function setVerdict(res, verdict, opts = {}) {
  res.verdict = verdict;
  res.ok = verdict === 'PASS';
  if (verdict === 'NOT-RUN') res.exit = [EXIT.USAGE, EXIT.INFRA, EXIT.BLOCKED].includes(opts.exit) ? opts.exit : EXIT.USAGE;
  else res.exit = VERDICT_EXIT[verdict];
  if (opts.infra !== undefined) res.infra = opts.infra;
  if (opts.message) addError(res, opts.message);
  return res;
}

/** Append an error object (or message) once. */
export function addError(res, e) {
  const obj = typeof e === 'string' ? { message: e } : e;
  if (!obj || !obj.message) return;
  if (!res.errors.some((x) => x.message === obj.message && x.file === obj.file && x.line === obj.line)) res.errors.push(obj);
}

/** U-relative POSIX path of an absolute path (the gate's `log` and `evidence` fields). */
export function relU(ctx, abs) {
  return abs ? toPosix(relPosix(ctx.U, abs)) : null;
}

/** W-relative POSIX path (ledger rows). */
export function relW(ctx, abs) {
  return abs ? toPosix(relPosix(ctx.W, abs)) : null;
}

/** A log path `U/Logs/gate_<name>_<stamp>.log` that does not exist yet (GSK_NOW can pin the stamp). */
export function uniqueLog(ctx, name) {
  fs.mkdirSync(ctx.paths.logs, { recursive: true });
  const safe = String(name).replace(/[^A-Za-z0-9_.-]+/g, '_');
  const stamp = utcFileStamp(now());
  let p = path.join(ctx.paths.logs, `gate_${safe}_${stamp}.log`);
  for (let i = 2; fs.existsSync(p); i++) p = path.join(ctx.paths.logs, `gate_${safe}_${stamp}-${i}.log`);
  return p;
}

/** A unique stamped file path in dir: `<base>_<stamp><ext>`. */
export function uniqueStamped(dir, base, ext) {
  fs.mkdirSync(dir, { recursive: true });
  const stamp = utcFileStamp(now());
  let p = path.join(dir, `${base}_${stamp}${ext}`);
  for (let i = 2; fs.existsSync(p); i++) p = path.join(dir, `${base}_${stamp}-${i}${ext}`);
  return p;
}

/** `git rev-parse HEAD` in W when W is a git work tree; null otherwise. Never writes anything. */
export async function gitHead(ctx) {
  if (!fs.existsSync(path.join(ctx.W, '.git'))) return null;
  const r = await run('git', ['rev-parse', 'HEAD'], { cwd: ctx.W, timeoutMs: 15000 });
  const h = (r.stdout || '').trim();
  return r.exit === 0 && /^[0-9a-f]{40}([0-9a-f]{24})?$/.test(h) ? h : null;
}

/** The freshness tree hash with the roots and excludes of Docs/qa/gates.json (defaults when absent). */
export function freshnessTree(ctx) {
  const g = readJsonOr(ctx.paths.gatesJson, null, { onInvalid: 'fallback' });
  const roots = Array.isArray(g?.freshness?.tree_roots) && g.freshness.tree_roots.length ? g.freshness.tree_roots : [...TREE_ROOTS];
  const excludes = Array.isArray(g?.freshness?.tree_excludes) ? g.freshness.tree_excludes : [...TREE_EXCLUDES];
  return treeHash(ctx.U, roots, { excludes });
}

/** Append the history line (gate-json-line.schema.json#/$defs/historyLine). Never throws. */
export function appendHistory(ctx, res, extra = {}) {
  const line = {
    utc: res.utc,
    cmd: res.cmd,
    rung: res.rung ?? null,
    secs: res.secs,
    exit: res.exit,
    verdict: res.verdict,
    route: res.route || 'none',
    crashAfterQuit: Boolean(extra.crashAfterQuit),
    refreshSecs: typeof extra.refreshSecs === 'number' ? extra.refreshSecs : null,
    head: extra.head ?? null,
    tree: extra.tree ?? null,
  };
  // Additive: a fresh serve boot's seconds (U-08), only on that line.
  if (typeof extra.bootSecs === 'number' && Number.isFinite(extra.bootSecs)) line.bootSecs = extra.bootSecs;
  try {
    appendJsonl(ctx.paths.history, line);
  } catch (e) {
    note(`gate: could not append ${ctx.paths.history}: ${e.message}`);
  }
  return line;
}

export const ROW_RE = /^[A-Za-z][A-Za-z0-9_]{0,31}-[0-9]{2,3}[a-z]?$/;

/**
 * Append a gates.jsonl row (gates.schema.json#/$defs/ledgerRow) for --row. INFRA is recorded as BLOCKED.
 * Evidence paths are W-relative; evidence_sha256[i] is the sha256 of evidence[i] now.
 */
export function appendLedgerRow(ctx, res, rowId, facts) {
  const files = [res.log, ...(res.evidence || [])]
    .filter(Boolean)
    .map((p) => path.resolve(ctx.U, p))
    .filter((abs, i, all) => all.indexOf(abs) === i && fs.existsSync(abs) && fs.statSync(abs).isFile());
  const verdict = res.verdict === 'INFRA' ? 'BLOCKED' : res.verdict;
  const row = {
    utc: res.utc,
    milestone: rowId.slice(0, rowId.lastIndexOf('-')),
    row: rowId,
    verdict,
    head: facts.head ?? null,
    tree: facts.tree ?? null,
    evidence: files.map((f) => relW(ctx, f)),
    evidence_sha256: files.map((f) => sha256File(f)),
    by: 'gate',
    run: `${utcFileStamp(new Date(res.utc))}-${res.cmd.split(' ')[0]}`,
    secs: res.secs,
    cmd: res.cmd,
  };
  appendJsonl(ctx.paths.gatesJsonl, row);
  return row;
}

/**
 * Print the result: one JSON line with --json (human text went to stderr), else a short human summary.
 * @param {object} ctx
 * @param {object} res
 * @param {boolean} json
 */
export function printResult(ctx, res, json) {
  if (json) {
    emit(res, { json: true });
    return;
  }
  const p = ctx?.cfg?.prefix || '[GSK]';
  const lines = [];
  for (const m of (res.messages || []).slice(0, 80)) lines.push(`  ${m}`);
  for (const e of res.errors.slice(0, 60)) {
    const where = e.file ? `${e.file}${e.line ? `(${e.line}${e.col ? `,${e.col}` : ''})` : ''}: ` : '';
    lines.push(`  ${where}${e.code ? `${e.code}: ` : ''}${e.message}`);
  }
  for (const f of res.failures.slice(0, 40)) {
    lines.push(`  FAIL ${f.name}${f.location ? `  at ${f.location}` : ''}`);
    if (f.message) lines.push(`       ${String(f.message).split('\n').slice(0, 6).join('\n       ')}`);
  }
  const t = res.tests ? ` ${res.tests.passed}/${res.tests.total} passed, ${res.tests.failed} failed, ${res.tests.skipped} skipped;` : '';
  const route = res.route && res.route !== 'none' ? ` [${res.route}]` : '';
  lines.push(`${p} ${res.cmd} ${res.rung || ''}${route}: ${res.verdict}${res.infra ? ` (${res.infra})` : ''}${t} ${res.secs}s, exit ${res.exit}${res.log ? `, log ${res.log}` : ''}`);
  if (res.tests && res.tests.total === 0) lines.push(`${p} zero tests ran: check the filter, the category and that the test assembly is found (zero tests is a FAIL).`);
  if (res.verdict === 'BLOCKED') lines.push(`${p} BLOCKED: stop and report; do not kill editors or delete locks by hand.`);
  emit(res, { json: false, text: lines });
}

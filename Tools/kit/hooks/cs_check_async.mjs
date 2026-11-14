// PostToolUse hook (async + asyncRewake) for .cs edits: a debounced, single-flight R0 type-check that wakes Claude
// (exit 2, errors on stderr) only for errors in files this session touched. Skips edits made inside a GSK-LANE
// subagent (builders run R0 themselves). ARCHITECTURE §6.3 (critic C-11); CONTRACTS.md §5.2.
import fs from 'node:fs';
import path from 'node:path';
import { now, utcIso, parseUtc } from '../lib/time.mjs';
import { readJsonOr, writeJsonAtomic } from '../lib/jsonio.mjs';
import { tryLock } from '../lib/locks.mjs';
import { run } from '../lib/proc.mjs';
import { fromShellPath, wsRel, toPosix } from '../lib/paths.mjs';
import { isMain } from '../lib/cli.mjs';
import { runHook, laneOfAgent, safeText, unitySubdirOf, mtimeMs, isObj } from './lib/harness.mjs';

const EDITS = new Set(['Edit', 'Write', 'MultiEdit']);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const MAX_RERUNS = 2;
const T0 = Date.now();

function debounceMs() {
  const v = Number(process.env.GSK_CS_DEBOUNCE_MS);
  return Number.isFinite(v) && v >= 0 ? v : 20000;
}

/**
 * Total time this hook may use, well inside its registered 300 s timeout, so it always writes cs_check.last.json and
 * releases the lock itself (a hook killed by Claude Code leaves the lock and the check's process tree behind).
 * GSK_CS_BUDGET_MS overrides it (tests).
 */
function budgetMs() {
  const v = Number(process.env.GSK_CS_BUDGET_MS);
  return Number.isFinite(v) && v > 0 ? v : 270000;
}

function checkCommand() {
  const raw = process.env.GSK_CS_CHECK_CMD;
  if (raw) {
    try {
      const a = JSON.parse(raw);
      if (Array.isArray(a) && a.length && a.every((x) => typeof x === 'string')) return a;
    } catch { /* fall back to the gate */ }
  }
  return ['node', 'Tools/kit/gate/game_gate.mjs', 'check', 'all', '--json'];
}

/** Last JSON line of a gate run's stdout, or null. */
function lastJsonLine(stdout) {
  const lines = String(stdout || '').split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
  for (let i = lines.length - 1; i >= 0; i--) {
    try {
      const o = JSON.parse(lines[i]);
      if (isObj(o)) return o;
    } catch { /* keep looking */ }
  }
  return null;
}

/** W-relative candidates for an error's file (absolute, W-relative, or U-relative). */
function relCandidates(ws, sub, file) {
  const f = toPosix(fromShellPath(String(file || '')));
  if (!f) return [];
  if (path.isAbsolute(f) || /^[A-Za-z]:\//.test(f)) {
    const r = wsRel(ws, f);
    return r ? [r] : [];
  }
  const c = [f.replace(/^\.\//, '')];
  if (sub !== '.') c.push(`${sub}/${c[0]}`);
  return c;
}

const norm = (p) => (process.platform === 'win32' ? p.toLowerCase() : p);

export async function main() {
  await runHook('cs_check_async', async (input, { ws, invalid }) => {
    if (invalid || !EDITS.has(input.tool_name)) return null;
    const ti = isObj(input.tool_input) ? input.tool_input : {};
    const file = typeof ti.file_path === 'string' ? ti.file_path : null;
    if (!file || !/\.cs$/i.test(file)) return null;
    if (typeof input.agent_id === 'string' && input.agent_id && laneOfAgent(input, ws)) return null;

    const H = (...p) => path.join(ws, '.harness', ...p);
    const deb = debounceMs();
    const lastFile = H('cs_check.last.json');
    const last = readJsonOr(lastFile, null, { onInvalid: 'fallback' });
    const lastUtc = isObj(last) ? parseUtc(last.utc) : null;
    if (lastUtc && deb > 0 && now().getTime() - lastUtc.getTime() < deb) return null;

    const pendingFile = H('cs_check.pending');
    const lk = tryLock(H('cs_check.lock'), { purpose: 'cs_check' });
    if (!lk.ok) {
      try { fs.mkdirSync(H(), { recursive: true }); fs.writeFileSync(pendingFile, utcIso(now()) + '\n'); } catch { /* best effort */ }
      return null;
    }
    try {
      const budget = budgetMs();
      const deadline = T0 + budget;
      const margin = Math.min(5000, Math.floor(budget * 0.05));
      const rerunMin = Math.min(60000, Math.floor(budget * 0.25));
      const left = () => deadline - Date.now() - margin;
      if (deb > 0) await sleep(Math.max(0, Math.min(deb, left() - rerunMin)));
      const sid = typeof input.session_id === 'string' && /^[A-Za-z0-9._-]+$/.test(input.session_id) ? input.session_id : 'unknown';
      const sub = unitySubdirOf(ws);
      let result = null;
      let lastStart = null;
      for (let pass = 0; pass <= MAX_RERUNS; pass++) {
        const remaining = left();
        if (remaining <= 0 || (pass > 0 && remaining < rerunMin)) break;
        const started = Date.now();
        lastStart = started;
        const [cmd, ...args] = checkCommand();
        const r = await run(cmd, args, { cwd: ws, env: process.env, timeoutMs: remaining });
        const line = lastJsonLine(r.stdout);
        const errors = Array.isArray(line?.errors) ? line.errors.filter(isObj) : [];
        const touched = new Set((safeText(H('touched', `${sid}.txt`)) || '').split(/\r?\n/).map((l) => norm(toPosix(l.trim()))).filter(Boolean));
        const mine = errors.filter((e) => relCandidates(ws, sub, e.file).some((c) => touched.has(norm(c))));
        result = { r, line, errors, mine };
        writeJsonAtomic(lastFile, {
          utc: utcIso(now()),
          ok: r.timedOut ? false : (line ? line.ok === true : r.exit === 0),
          exit: r.exit,
          errors_in_touched: mine.length,
          secs: r.secs,
          ...(r.timedOut ? { timed_out: true } : {}),
        });
        const pend = mtimeMs(pendingFile);
        if (pend === null || pend < started) break;
      }
      // A pending request newer than the last run stays for the next edit event when the budget ran out.
      const pend = mtimeMs(pendingFile);
      if (pend !== null && lastStart !== null && pend < lastStart) { try { fs.rmSync(pendingFile, { force: true }); } catch { /* ignore */ } }
      if (result && result.mine.length) {
        const lines = result.mine.slice(0, 30).map((e) => {
          const rel = relCandidates(ws, sub, e.file)[0] ?? e.file;
          return `${rel}(${e.line ?? '?'},${e.col ?? '?'}): ${e.code ?? 'error'} ${String(e.message ?? '').replace(/\s+/g, ' ').slice(0, 300)}`;
        });
        const more = result.mine.length > 30 ? `\n… ${result.mine.length - 30} more` : '';
        return { stderr: `C# check (R0, game_gate check all) found ${result.mine.length} error(s) in files this session edited:\n${lines.join('\n')}${more}\n`, exit: 2 };
      }
      return null;
    } finally {
      lk.release();
    }
  });
}

if (isMain(import.meta.url)) await main();

// The only gate closer (ARCHITECTURE §6.2 steps 1–8, critic C-1): a milestone closes only when every card row has a
// fresh PASS with intact evidence, owner rows carry a confirmation the session transcript proves was typed by the
// owner, no KNOWN_ISSUE recheck is due, the DEVLOG is current and no guard hit is open.
// CLI: node Tools/kit/harness/gate_close.mjs <milestone> [--dry-run] [--no-report] [--json]
// Exit: 0 PASS (closed written unless --dry-run), 1 FAIL, 2 usage, 3 no Docs/qa/gates.json.
import fs from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';
import { fileURLToPath } from 'node:url';
import { now, utcIso, parseUtc } from '../lib/time.mjs';
import { readJson, readJsonl, writeJsonAtomic, appendJsonl } from '../lib/jsonio.mjs';
import { sha256File, sha256Text, treeHash, listFiles, TREE_ROOTS, TREE_EXCLUDES } from '../lib/hash.mjs';
import { findWorkspace, findTranscript, matchesAny, toPosix } from '../lib/paths.mjs';
import { validate, formatErrors } from '../lib/schema.mjs';
import { EXIT, CliError, UsageError, parseCli, emit, isMain, runCli } from '../lib/cli.mjs';
import {
  loadPolicy, latestVerdicts, parseKnownIssues, lastDevlogEntry, safeText, unityRoot, gitHeadCli, gitHead, userTexts, isObj, exists,
  confirmedRows, redactSecrets,
} from '../hooks/lib/harness.mjs';

/** A DEVLOG heading may be at most this far in the future (clock skew); later ones are ignored. */
const DEVLOG_SKEW_MS = 5 * 60 * 1000;

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SCHEMA_FILE = path.join(HERE, '..', 'schemas', 'gates.schema.json');

function loadGatesSchema() {
  try { return readJson(SCHEMA_FILE); } catch { return null; }
}

/** Absolute path of an evidence entry: W-relative first, then U-relative. */
function evidencePath(ws, U, p) {
  const s = String(p || '');
  if (!s) return null;
  if (path.isAbsolute(s)) return s;
  const w = path.join(ws, s);
  if (exists(w)) return w;
  const u = path.join(U, s);
  if (exists(u)) return u;
  return w;
}

/** Newest mtime (ms) of the W-relative files matching the globs, or null. */
function newestCovered(ws, globs) {
  let newest = null;
  const skip = ['.git', 'node_modules', 'Library', 'Temp', 'Obj', 'Logs', '.harness', '.kit', 'Builds'];
  for (const g of globs) {
    const parts = toPosix(g).split('/');
    const baseParts = [];
    for (const p of parts) { if (/[*?[\]]/.test(p)) break; baseParts.push(p); }
    const exact = baseParts.length === parts.length;
    const base = path.join(ws, ...baseParts);
    let st = null;
    try { st = fs.statSync(base); } catch { continue; }
    if (st.isFile()) { if (exact || matchesAny(baseParts.join('/'), [g])) newest = Math.max(newest ?? 0, st.mtimeMs); continue; }
    const prefix = baseParts.length ? baseParts.join('/') + '/' : '';
    for (const rel of listFiles(base, { skipDirs: skip })) {
      const full = prefix + rel;
      if (!matchesAny(full, [g])) continue;
      try { newest = Math.max(newest ?? 0, fs.statSync(path.join(base, rel)).mtimeMs); } catch { /* raced away */ }
    }
  }
  return newest;
}

/**
 * sha256 → rows confirmed by that user-typed text, for every text of a transcript that holds an OWNER: confirm command
 * (the owner_inbox parser decides, so spelling variants the inbox accepts are accepted here too). Non-interactive lines
 * are skipped.
 */
async function transcriptConfirmHashes(file, nonInteractive) {
  const map = new Map();
  const rl = readline.createInterface({ input: fs.createReadStream(file, { encoding: 'utf8' }), crlfDelay: Infinity });
  for await (const line of rl) {
    if (!line.includes('OWNER:')) continue;
    let o;
    try { o = JSON.parse(line); } catch { continue; }
    if (typeof o.entrypoint === 'string' && nonInteractive.includes(o.entrypoint)) continue;
    for (const t of userTexts(o)) {
      const rows = confirmedRows(t);
      if (rows.length) map.set(sha256Text(t), rows);
    }
  }
  return map;
}

/**
 * Verify owner confirmations for a row against the session transcripts (CONTRACTS.md §4.2; fixture transcripts/).
 * @returns {Promise<{status: 'none'|'unverified'|'verified', verified: object[], why: string[]}>}
 */
export async function verifyConfirmations(ws, row, lines, nonInteractive, cache = new Map()) {
  const mine = lines.filter((c) => isObj(c) && c.row === row);
  if (!mine.length) return { status: 'none', verified: [], why: [] };
  const verified = [];
  const why = [];
  for (const c of mine) {
    if (typeof c.entrypoint === 'string' && nonInteractive.includes(c.entrypoint)) { why.push(`${c.utc}: non-interactive entrypoint ${c.entrypoint}`); continue; }
    if (typeof c.session_id !== 'string' || typeof c.prompt_sha256 !== 'string') { why.push(`${c.utc}: no session_id or prompt_sha256`); continue; }
    const t = findTranscript(c.session_id, { cwd: ws, hint: typeof c.transcript_path === 'string' ? c.transcript_path : null });
    if (!t) { why.push(`${c.utc}: transcript of session ${c.session_id} not found`); continue; }
    if (!cache.has(t)) cache.set(t, await transcriptConfirmHashes(t, nonInteractive));
    const rows = cache.get(t).get(c.prompt_sha256);
    if (rows !== undefined && rows.includes(row)) verified.push(c);
    else why.push(`${c.utc}: no user-typed transcript entry matches this confirmation`);
  }
  return { status: verified.length ? 'verified' : 'unverified', verified, why };
}

/**
 * Evaluate (and unless dryRun, close) milestone M.
 * @returns {Promise<{cmd: string, milestone: string, verdict: 'PASS'|'FAIL', ok: boolean, exit: number, blocking: object[], closed: object|null, dry_run: boolean}>}
 */
export async function gateClose(ws, M, opts = {}) {
  const t = opts.now ?? now();
  const gatesFile = path.join(ws, 'Docs', 'qa', 'gates.json');
  if (!exists(gatesFile)) throw new CliError(`no Docs/qa/gates.json in ${ws} (kit not installed or no gate cards yet)`, EXIT.INFRA);
  let gates;
  try { gates = readJson(gatesFile); } catch (e) { throw new CliError(e.message, EXIT.INFRA); }
  const schema = loadGatesSchema();
  if (schema) {
    const v = validate(schema, gates);
    if (!v.valid) throw new CliError(`Docs/qa/gates.json does not match gates.schema.json: ${formatErrors(v.errors)}`, EXIT.INFRA);
  }
  const ms = (gates.milestones || []).find((m) => isObj(m) && m.id === M);
  if (!ms) throw new UsageError(`milestone ${M} is not in Docs/qa/gates.json (have: ${(gates.milestones || []).map((m) => m.id).join(', ')})`);
  const { policy } = loadPolicy(ws);
  const nonInteractive = policy.owner?.noninteractive_entrypoints || ['sdk-cli', 'sdk-ts', 'sdk-py'];
  const U = unityRoot(ws);
  const blocking = [];
  const block = (row, code, detail) => blocking.push({ row, code, detail });
  const ledger = readJsonl(path.join(ws, 'Docs', 'qa', 'gates.jsonl'));
  const latest = latestVerdicts(ws, ledger);
  const fresh = gates.freshness || { mode: 'tree-hash' };
  let curHead;
  let curTree;
  const head = () => { if (curHead === undefined) curHead = gitHeadCli(ws) ?? gitHead(ws); return curHead; };
  const tree = () => {
    if (curTree === undefined) curTree = treeHash(U, fresh.tree_roots ?? TREE_ROOTS, { excludes: fresh.tree_excludes ?? TREE_EXCLUDES });
    return curTree;
  };

  const rows = (ms.requires || []).filter(isObj);
  let latestEvidence = null;
  const noteTime = (u) => { const d = parseUtc(u); if (d && (!latestEvidence || d > latestEvidence)) latestEvidence = d; };
  for (const r of rows) if (r.evidence !== 'human' && latest.get(r.id)) noteTime(latest.get(r.id).utc);

  const humanRows = [];
  for (const r of rows) {
    if (r.evidence === 'human') { humanRows.push(r); continue; }
    const l = latest.get(r.id);
    if (!l) {
      if (r.evidence === 'file' && r.path) {
        const f = evidencePath(ws, U, r.path);
        if (!f || !exists(f)) { block(r.id, 'evidence_missing', `${r.path} does not exist`); continue; }
        if (Array.isArray(r.covers) && r.covers.length) {
          const nc = newestCovered(ws, r.covers);
          const fm = fs.statSync(f).mtimeMs;
          if (nc !== null && fm < nc) block(r.id, 'evidence_older', `${r.path} is older than the newest file it covers (${r.covers.join(', ')})`);
        }
        continue;
      }
      block(r.id, 'no_verdict', 'no line in Docs/qa/gates.jsonl for this row');
      continue;
    }
    if (l.verdict !== 'PASS') { block(r.id, 'not_pass', `latest verdict ${l.verdict} at ${l.utc}${l.notes ? ` (${l.notes})` : ''}`); continue; }
    if (r.fresh === true) {
      if (fresh.mode === 'git-head') {
        const h = head();
        if (!h || l.head !== h) block(r.id, 'stale_head', `verdict head ${l.head ?? 'none'} != current HEAD ${h ?? 'none (no git commit)'}`);
      } else if (l.tree !== tree()) block(r.id, 'stale_tree', `verdict tree ${String(l.tree).slice(0, 12)} != current tree ${tree().slice(0, 12)} (files under ${(fresh.tree_roots ?? TREE_ROOTS).join(', ')} changed since the verdict)`);
    }
    const ev = Array.isArray(l.evidence) ? [...l.evidence] : [];
    const recorded = ev.length;
    const hs = Array.isArray(l.evidence_sha256) ? l.evidence_sha256 : [];
    const files = [];
    if (r.evidence === 'file' && r.path && !ev.includes(r.path)) ev.push(r.path);
    for (let i = 0; i < ev.length; i++) {
      const f = evidencePath(ws, U, ev[i]);
      if (!f || !exists(f)) { block(r.id, 'evidence_missing', `${ev[i]} does not exist`); continue; }
      files.push(f);
      if (i < recorded) {
        if (typeof hs[i] !== 'string') { block(r.id, 'evidence_hash_mismatch', `${ev[i]} has no evidence_sha256 recorded`); continue; }
        let h;
        try { h = sha256File(f); } catch (e) { block(r.id, 'evidence_missing', `${ev[i]}: ${e.message}`); continue; }
        if (h !== hs[i]) block(r.id, 'evidence_hash_mismatch', `${ev[i]} changed after the verdict (sha256 ${h.slice(0, 12)} != ${String(hs[i]).slice(0, 12)})`);
      }
    }
    if (Array.isArray(r.covers) && r.covers.length) {
      const nc = newestCovered(ws, r.covers);
      if (nc !== null) {
        const vd = parseUtc(l.utc);
        const evTimes = files.map((f) => fs.statSync(f).mtimeMs);
        const evTime = evTimes.length ? Math.min(...evTimes) : (vd ? vd.getTime() : 0);
        if (evTime < nc) block(r.id, 'evidence_older', `evidence is older than the newest file it covers (${r.covers.join(', ')})`);
      }
    }
  }

  const confirmations = readJsonl(path.join(ws, '.harness', 'owner_confirmations.jsonl'));
  const cache = new Map();
  const humanOk = [];
  for (const r of humanRows) {
    const v = await verifyConfirmations(ws, r.id, confirmations, nonInteractive, cache);
    if (v.status === 'none') { block(r.id, 'no_owner_confirmation', `no OWNER: confirm ${r.id} line; the owner types "OWNER: confirm ${r.id}" in the session`); continue; }
    if (v.status === 'unverified') { block(r.id, 'confirmation_unverified', `${v.why.slice(0, 3).join('; ')}. Ask the owner to type "OWNER: confirm ${r.id}" again.`); continue; }
    const newer = v.verified.filter((c) => { const d = parseUtc(c.utc); return d && (!latestEvidence || d >= latestEvidence); });
    if (!newer.length) { block(r.id, 'no_owner_confirmation', `the owner's confirmation is older than the latest evidence (${utcIso(latestEvidence)}); ask the owner to confirm again after reviewing it`); continue; }
    humanOk.push({ row: r, conf: newer[newer.length - 1] });
  }

  for (const ki of parseKnownIssues(safeText(path.join(ws, 'Docs', 'KNOWN_ISSUES.md')) || '')) {
    if (ki.status === 'open' && ki.recheck_at_gate === M) block(ki.id, 'known_issue_recheck_due', `${ki.id} (${ki.severity}) is open with recheck_at_gate ${M}; recheck it and set its status`);
  }

  if (latestEvidence) {
    const dev = lastDevlogEntry(ws, { notAfter: new Date(t.getTime() + DEVLOG_SKEW_MS) });
    if (!dev || dev.date < latestEvidence) {
      const all = lastDevlogEntry(ws);
      const fut = all && all.date.getTime() > t.getTime() + DEVLOG_SKEW_MS ? `; newest entry is dated in the future (${all.utc}) and does not count` : '';
      block('DEVLOG', 'devlog_stale', `Docs/DEVLOG.md has no entry at or after the latest evidence (${utcIso(latestEvidence)}); newest valid entry: ${dev ? dev.utc : 'none'}${fut}`);
    }
  }

  for (const h of readJsonl(path.join(ws, 'Docs', 'qa', 'guard_hits.jsonl'))) {
    if (!isObj(h) || h.milestone !== M || h.resolved === true) continue;
    const cmd = redactSecrets(String(h.command ?? ''), process.env, policy).text.slice(0, 160);
    const how = typeof h.id === 'string' ? ` To clear it: the owner reviews it and types "OWNER: confirm ${h.id}", then node Tools/kit/harness/wave_manifest.mjs resolve ${h.wave ?? '<wave>'} ${h.id} --reason "<why>".` : '';
    block(h.id || h.row || 'guard', 'guard_hits', `wave audit found a guard-class command (${h.rule ?? '?'}) in ${h.source ?? 'a transcript'}: ${cmd}.${how}`);
  }

  const pass = blocking.length === 0;
  const result = { cmd: 'gate_close', milestone: M, verdict: pass ? 'PASS' : 'FAIL', ok: pass, exit: pass ? EXIT.OK : EXIT.FAIL, blocking, closed: ms.closed ?? null, dry_run: Boolean(opts.dryRun) };
  if (ms.status === 'closed') result.already_closed = true;
  if (!pass || opts.dryRun || ms.status === 'closed') return result;

  const h = head();
  let tr = null;
  try { tr = tree(); } catch { tr = null; }
  const closed = { utc: utcIso(t), head: h && /^[0-9a-f]{40}([0-9a-f]{24})?$/.test(h) ? h : null, tree: tr, rows: rows.map((r) => r.id) };
  const next = JSON.parse(JSON.stringify(gates));
  const target = next.milestones.find((m) => m.id === M);
  target.status = 'closed';
  target.closed = closed;
  if (schema) {
    const v = validate(schema, next);
    if (!v.valid) throw new CliError(`refusing to write an invalid gates.json: ${formatErrors(v.errors)}`, EXIT.FAIL);
  }
  writeJsonAtomic(gatesFile, next);
  for (const { row, conf } of humanOk) {
    appendJsonl(path.join(ws, 'Docs', 'qa', 'gates.jsonl'), {
      utc: utcIso(t), milestone: M, row: row.id, verdict: 'PASS', head: closed.head, tree: closed.tree, evidence: [], evidence_sha256: [],
      by: 'owner', run: null, secs: null, notes: `owner confirmation verified against the session transcript (inbox #${conf.inbox_id ?? '?'})`,
    });
  }
  result.closed = closed;
  if (!opts.noReport) {
    try {
      const { generateReport } = await import('./report_gen.mjs');
      await generateReport(ws, { now: t });
      result.report = 'Docs/REPORT.md';
    } catch (e) {
      result.report_error = e.message;
    }
  }
  const devlog = path.join(ws, 'Docs', 'DEVLOG.md');
  const pre = exists(devlog) ? '' : '# DEVLOG\n';
  fs.mkdirSync(path.dirname(devlog), { recursive: true });
  fs.appendFileSync(devlog, `${pre}\n## ${utcIso(t)} Gate ${M} closed (PASS)\nrows: ${closed.rows.join(', ')}\nfreshness: ${closed.head ? `head ${closed.head}` : `tree ${String(closed.tree).slice(0, 16)}`}\n`, 'utf8');
  return result;
}

export async function main(argv) {
  const { values, positionals } = parseCli(argv, { 'dry-run': { type: 'boolean', default: false }, 'no-report': { type: 'boolean', default: false } });
  if (positionals.length !== 1) throw new UsageError('usage: gate_close.mjs <milestone> [--dry-run] [--no-report] [--json]');
  const ws = findWorkspace() ?? process.cwd();
  const r = await gateClose(ws, positionals[0], { dryRun: values['dry-run'], noReport: values['no-report'] });
  const text = [`gate_close ${r.milestone}: ${r.verdict}${r.dry_run ? ' (dry run)' : ''}${r.already_closed ? ' (already closed)' : ''}`];
  for (const b of r.blocking) text.push(`- ${b.row} ${b.code}: ${b.detail}`);
  if (r.verdict === 'PASS' && r.closed && !r.dry_run) text.push(`closed ${r.closed.utc}; REPORT regenerated; DEVLOG entry appended.`);
  emit(r, { json: values.json, text });
  return r.exit;
}

if (isMain(import.meta.url)) await runCli(main);

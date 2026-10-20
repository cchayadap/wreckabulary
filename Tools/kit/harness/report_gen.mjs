// Generates Docs/REPORT.md and Docs/STATS.json from the ledgers (gates, generation, run window, transcripts), so the
// report can never go stale the way WoO's froze at M2. Never hand-edit either file.
// CLI: node Tools/kit/harness/report_gen.mjs [--no-tokens] [--json]   → {ok, files}; exit 0, 1 on a write failure.
import path from 'node:path';
import { now, utcIso } from '../lib/time.mjs';
import { readJsonOr, readJsonl, writeJsonAtomic, writeTextAtomic } from '../lib/jsonio.mjs';
import { findWorkspace } from '../lib/paths.mjs';
import { CliError, EXIT, parseCli, emit, isMain, runCli } from '../lib/cli.mjs';
import {
  readGates, latestVerdicts, readRun, runStatus, parseKnownIssues, unackedInbox, parseInbox, safeText, newestWaveManifest,
  lastDevlogEntry, nextAction, gitHead, isObj, clip,
} from '../hooks/lib/harness.mjs';
import { transcriptFiles, sumUsage } from './token_usage.mjs';

const md = (s) => String(s ?? '').replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');

/**
 * Write Docs/STATS.json and Docs/REPORT.md.
 * @param {string} ws
 * @param {{now?: Date, noTokens?: boolean}} [opts]
 * @returns {Promise<{ok: true, files: string[], stats: object}>}
 */
export async function generateReport(ws, opts = {}) {
  const t = opts.now ?? now();
  const gates = readGates(ws);
  const ledger = readJsonl(path.join(ws, 'Docs', 'qa', 'gates.jsonl'));
  const latest = latestVerdicts(ws, ledger);
  const counts = { pass: 0, fail: 0, blocked: 0, not_run: 0 };
  const milestones = [];
  for (const m of (gates?.milestones || []).filter(isObj)) {
    const rows = (m.requires || []).filter(isObj);
    let pass = 0;
    for (const r of rows) {
      const v = latest.get(r.id)?.verdict;
      if (v === 'PASS') { counts.pass++; pass++; } else if (v === 'FAIL') counts.fail++; else if (v === 'BLOCKED') counts.blocked++; else counts.not_run++;
    }
    milestones.push({ id: m.id, title: m.title, status: m.status, closed_utc: m.closed?.utc ?? null, rows: rows.length, rows_pass: pass, time_box_utc: m.time_box_utc ?? null });
  }
  const generation = readJsonOr(path.join(ws, 'Docs', 'STATS', 'generation.json'), null, { onInvalid: 'fallback' });
  let tokens = null;
  if (!opts.noTokens) {
    try {
      const files = transcriptFiles(ws);
      tokens = files.length ? await sumUsage(files) : null;
    } catch { tokens = null; }
  }
  const run = readRun(ws);
  const stats = {
    schema: 1,
    utc: utcIso(t),
    milestones: milestones.map(({ id, status, closed_utc: c }) => ({ id, status, closed_utc: c })),
    gates: counts,
    generation: isObj(generation) ? generation : null,
    tokens,
    git: { head: gitHead(ws) },
    run: run ?? null,
  };
  const statsFile = path.join(ws, 'Docs', 'STATS.json');
  const reportFile = path.join(ws, 'Docs', 'REPORT.md');

  const L = [];
  L.push('# Build report', '', `Generated ${utcIso(t)} by Tools/kit/harness/report_gen.mjs from the ledgers. Do not edit by hand; rerun the script.`, '');
  const rs = runStatus(run, t);
  L.push('## Run window', '');
  L.push(run ? `${rs.name ?? 'run'}: ${rs.active ? `active, ends ${rs.window_end_utc} (${rs.elapsed_pct}% elapsed${rs.freeze ? ', freeze' : ''})` : `not armed${rs.window_end_utc ? `, window end ${rs.window_end_utc}` : ''}`}` : 'No autonomous run window.', '');
  const na = nextAction(ws, t);
  L.push('## Next action', '', `${na.kind}: ${na.detail}`, '');
  L.push('## Milestones', '', '| Milestone | Title | Status | Rows PASS | Closed (UTC) | Time box |', '|---|---|---|---|---|---|');
  for (const m of milestones) L.push(`| ${md(m.id)} | ${md(m.title)} | ${md(m.status)} | ${m.rows_pass}/${m.rows} | ${m.closed_utc ?? ''} | ${m.time_box_utc ?? ''} |`);
  if (!milestones.length) L.push('| (no Docs/qa/gates.json) | | | | | |');
  L.push('', `Row verdicts (latest per row): PASS ${counts.pass}, FAIL ${counts.fail}, BLOCKED ${counts.blocked}, not run ${counts.not_run}.`, '');
  const failing = [];
  for (const m of (gates?.milestones || []).filter(isObj)) {
    if (m.status === 'closed') continue;
    for (const r of (m.requires || []).filter(isObj)) {
      const l = latest.get(r.id);
      if (l && l.verdict !== 'PASS') failing.push(`| ${md(r.id)} | ${md(l.verdict)} | ${md(l.utc)} | ${md(clip(r.what, 80))} | ${md(clip(l.notes ?? '', 80))} |`);
    }
  }
  if (failing.length) L.push('## Open rows that are not PASS', '', '| Row | Verdict | UTC | What | Notes |', '|---|---|---|---|---|', ...failing, '');
  const recent = ledger.filter(isObj).slice(-15).reverse();
  if (recent.length) {
    L.push('## Latest gate ledger lines', '', '| UTC | Row | Verdict | By | Evidence |', '|---|---|---|---|---|');
    for (const r of recent) L.push(`| ${md(r.utc)} | ${md(r.row)} | ${md(r.verdict)} | ${md(r.by)} | ${md((r.evidence || []).slice(0, 2).join(', '))} |`);
    L.push('');
  }
  const kis = parseKnownIssues(safeText(path.join(ws, 'Docs', 'KNOWN_ISSUES.md')) || '').filter((k) => k.status === 'open');
  if (kis.length) {
    L.push('## Open known issues', '', '| Id | Severity | Found at | Recheck at | Evidence |', '|---|---|---|---|---|');
    for (const k of kis) L.push(`| ${md(k.id)} | ${md(k.severity)} | ${md(k.found_at_gate)} | ${md(k.recheck_at_gate)} | ${md(k.evidence)} |`);
    L.push('');
  }
  const wave = newestWaveManifest(ws);
  if (wave) {
    const units = [...(wave.manifest.contracts || []), ...(wave.manifest.units || [])].filter(isObj);
    L.push(`## Wave ${md(wave.manifest.wave)} (${md(wave.manifest.milestone)})`, '', '| Unit | Status | Attempts | Last failure |', '|---|---|---|---|');
    for (const u of units) L.push(`| ${md(u.id)} | ${md(u.status)} | ${u.attempts ?? 0} | ${md(u.last_failure ?? '')} |`);
    L.push('');
  }
  const inbox = parseInbox(safeText(path.join(ws, 'Docs', 'OWNER_INBOX.md')) || '');
  L.push('## Owner inbox', '', `${inbox.length} item(s) recorded; ${unackedInbox(ws).length} not acknowledged.`, '');
  if (isObj(generation)) {
    L.push('## Generation spend (Docs/STATS/generation.json)', '', '| Provider | Unit | Spent | Reserved | Hard cap | Calls |', '|---|---|---|---|---|---|');
    for (const [p, v] of Object.entries(generation.providers || {})) if (isObj(v)) L.push(`| ${md(p)} | ${md(v.unit)} | ${v.spent ?? ''} | ${v.reserved ?? ''} | ${v.hard ?? ''} | ${v.calls ?? ''} |`);
    L.push('');
  }
  if (tokens) {
    L.push('## Tokens (session transcripts, deduplicated by message.id)', '', `${tokens.assistant_messages} assistant messages in ${tokens.sessions} session(s): input ${tokens.input_tokens}, output ${tokens.output_tokens}, cache read ${tokens.cache_read_input_tokens}, cache write ${tokens.cache_creation_input_tokens}.`, '');
  }
  const dev = lastDevlogEntry(ws);
  if (dev) L.push('## Last DEVLOG entry', '', `${dev.utc} ${md(dev.title)}`, '');
  try {
    writeJsonAtomic(statsFile, stats);
    writeTextAtomic(reportFile, L.join('\n') + '\n');
  } catch (e) {
    throw new CliError(`cannot write Docs/REPORT.md or Docs/STATS.json: ${e.message}`, EXIT.FAIL);
  }
  return { ok: true, files: ['Docs/REPORT.md', 'Docs/STATS.json'], stats };
}

export async function main(argv) {
  const { values } = parseCli(argv, { 'no-tokens': { type: 'boolean', default: false } });
  const ws = findWorkspace() ?? process.cwd();
  const r = await generateReport(ws, { noTokens: values['no-tokens'] });
  emit({ ok: r.ok, files: r.files }, { json: values.json, text: [`wrote ${r.files.join(', ')}`] });
  return 0;
}

if (isMain(import.meta.url)) await runCli(main);

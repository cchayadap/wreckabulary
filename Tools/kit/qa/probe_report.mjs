#!/usr/bin/env node
// probe_report.mjs: turn one QA run folder into a verdict, summary.md and gate-ledger rows (CONTRACTS.md §4.9, §7.1, §7.3).
//   node Tools/kit/qa/probe_report.mjs <run_dir> [--row <rowId>]... [--config <probes.config.json>] [--dry-run] [--json]
// Reads, when present: checks.json (checks_validate rules), perf.json (budgets; only release and release-qa numbers
// gate), every visual*.json below the folder (route_to_reviewer never fails; "error" means missing evidence), and other
// probe result files ({results:[probeResult]} from WorldAudit, {verdict} from AvatarProbe). Writes <run_dir>/summary.md.
// Each --row appends a gates.schema.json#/$defs/ledgerRow (by "probe", evidence + sha256, head and tree) to
// Docs/qa/gates.jsonl. Verdict: PASS, FAIL, or NOT-RUN when the folder holds no evidence.
// Result: {ok, run_id, verdict, rows:[{row, verdict}], failures[], routed[], evidence[], summary}. Exit 0 PASS, 1 FAIL,
// 2 usage or NOT-RUN, 3 when --row is given but no kit is installed.
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, parseCli, emit, isMain, runCli, UsageError, CliError } from '../lib/cli.mjs';
import { readJson, readJsonOr, writeTextAtomic, appendJsonl } from '../lib/jsonio.mjs';
import { validate, formatErrors } from '../lib/schema.mjs';
import { sha256File, treeHash, TREE_ROOTS, TREE_EXCLUDES } from '../lib/hash.mjs';
import { run } from '../lib/proc.mjs';
import { utcIso } from '../lib/time.mjs';
import { validateChecks } from './checks_validate.mjs';
import { loadKitSchema, roots, loadProbesConfig, defaultProbesConfig, judgeProbe, effectiveThreshold, satisfies, relOrAbs, round } from './lib/qa_common.mjs';

const ROW_RE = /^[A-Za-z][A-Za-z0-9_]{0,31}-[0-9]{2,3}[a-z]?$/;
const GATE_KINDS = ['release', 'release-qa'];

/** Milestone id of a row id: everything before the last -<nn>. @param {string} row */
export function milestoneOf(row) {
  return row.replace(/-[0-9]{2,3}[a-z]?$/, '');
}

function findVisualFiles(dir, depth = 0, out = []) {
  let ents = [];
  try { ents = fs.readdirSync(dir, { withFileTypes: true }); } catch { return out; }
  for (const e of ents) {
    const p = path.join(dir, e.name);
    if (e.isDirectory() && depth < 3) findVisualFiles(p, depth + 1, out);
    else if (e.isFile() && /^visual.*\.json$/i.test(e.name)) out.push(p);
  }
  return out.sort();
}

/**
 * Evaluate a run folder.
 * @param {string} runDir absolute
 * @param {{ws: string|null, config: object|null}} ctx
 */
export function evaluateRun(runDir, ctx) {
  const failures = [];
  const routed = [];
  const notes = [];
  const evidence = [];
  const probes = [];
  let checks = null;
  let perf = null;
  const checksFile = path.join(runDir, 'checks.json');
  if (fs.existsSync(checksFile)) {
    evidence.push(checksFile);
    let doc = null;
    try { doc = readJson(checksFile); } catch (e) { failures.push({ source: 'checks.json', message: e.message }); }
    if (doc) {
      const v = validateChecks(doc, { ws: ctx.ws || process.cwd(), config: ctx.config, file: checksFile });
      checks = { result: doc.result, scenario: doc.scenario, run_id: doc.run_id, console: doc.console, steps: doc.steps, started_utc: doc.started_utc, ended_utc: doc.ended_utc, probes: v.probes };
      for (const e of v.errors) failures.push({ source: 'checks.json', message: `${e.path}: ${e.message}` });
      for (const m of v.missing_evidence) failures.push({ source: 'checks.json', message: `missing evidence ${m}` });
      if (doc.result !== 'PASS') failures.push({ source: 'checks.json', message: `result ${doc.result}${doc.console?.first_exception ? ` (first exception: ${String(doc.console.first_exception).slice(0, 200)})` : ''}` });
      for (const s of Array.isArray(doc.steps) ? doc.steps : []) {
        if (s && (s.status === 'FAIL' || s.status === 'ERROR')) failures.push({ source: 'checks.json', message: `step ${s.name} ${s.status}: ${(s.assert_failures || []).join('; ')}` });
      }
      for (const j of v.judged) probes.push({ source: 'checks.json', ...j });
      if (!ctx.config) for (const pr of doc.probe_results || []) probes.push({ source: 'checks.json', probe: pr.probe, metric: pr.metric, body: pr.body, clip: pr.clip, value: pr.value, verdict: pr.verdict, threshold: pr.threshold });
    }
  }

  const perfFile = path.join(runDir, 'perf.json');
  if (fs.existsSync(perfFile)) {
    evidence.push(perfFile);
    let doc = null;
    try { doc = readJson(perfFile); } catch (e) { failures.push({ source: 'perf.json', message: e.message }); }
    if (doc) {
      const r = validate(loadKitSchema('checks.schema.json', 'game-qa-probes'), doc, { pointer: '#/$defs/perf' });
      if (!r.valid) failures.push({ source: 'perf.json', message: `invalid: ${formatErrors(r.errors)}` });
      perf = doc;
      if (!GATE_KINDS.includes(doc.kind)) {
        notes.push(`perf.json kind "${doc.kind}" is not gate-grade (only release and release-qa builds count); budgets not applied`);
      } else if (ctx.config) {
        for (const [metric, value] of [['frame_time_p95_ms', doc.frame_ms_p95], ['worst_frame_after_warmup_ms', doc.worst_frame_ms], ['first_visit_hitch_ms', doc.first_visit_hitch_ms]]) {
          const t = effectiveThreshold(ctx.config, metric);
          if (!t || typeof value !== 'number') continue;
          const ok = satisfies(t.op, value, t.value);
          probes.push({ source: 'perf.json', probe: 'PerfProbe', metric, value, verdict: ok ? 'PASS' : 'FAIL', threshold: t });
          if (!ok) failures.push({ source: 'perf.json', message: `${metric} ${value} breaks ${t.op} ${round(t.value)} ${t.unit}` });
        }
      }
    }
  }

  for (const vf of findVisualFiles(runDir)) {
    evidence.push(vf);
    let doc = null;
    try { doc = readJson(vf); } catch (e) { failures.push({ source: relOrAbs(ctx.ws, vf), message: e.message }); continue; }
    const r = validate(loadKitSchema('checks.schema.json', 'game-qa-probes'), doc, { pointer: '#/$defs/visual' });
    if (!r.valid) { failures.push({ source: relOrAbs(ctx.ws, vf), message: `invalid visual.json: ${formatErrors(r.errors)}` }); continue; }
    if (doc.verdict === 'error') failures.push({ source: relOrAbs(ctx.ws, vf), message: `visual diff did not run: ${doc.error || 'tool error'}` });
    else if (doc.verdict === 'route_to_reviewer') routed.push({ current: doc.current, baseline: doc.baseline, changed_pct: doc.changed_pct, diff_png: doc.diff_png });
  }

  let others = [];
  try { others = fs.readdirSync(runDir).filter((n) => /\.json$/i.test(n) && !/^(checks|perf)\.json$/i.test(n) && !/^visual/i.test(n)).sort(); } catch { /* none */ }
  for (const name of others) {
    const f = path.join(runDir, name);
    const doc = readJsonOr(f, null, { onInvalid: 'fallback' });
    if (!doc || typeof doc !== 'object') continue;
    if (Array.isArray(doc.results)) {
      evidence.push(f);
      doc.results.forEach((pr, i) => {
        if (!pr || typeof pr.metric !== 'string') return;
        if (ctx.config) {
          const j = judgeProbe(ctx.config, pr);
          for (const p of j.problems) failures.push({ source: name, message: `results/${i} ${p.code}: ${p.message}` });
          probes.push({ source: name, probe: pr.probe, metric: pr.metric, body: pr.body, clip: pr.clip, value: pr.value, verdict: j.verdict, threshold: j.threshold });
          if (j.verdict === 'FAIL') failures.push({ source: name, message: `${pr.probe || 'probe'} ${pr.metric}${pr.body ? ` (${pr.body}${pr.clip ? `/${pr.clip}` : ''})` : ''} = ${pr.value} fails` });
          if (j.verdict === 'ROUTE') routed.push({ source: name, metric: pr.metric, value: pr.value });
        } else {
          probes.push({ source: name, probe: pr.probe, metric: pr.metric, body: pr.body, clip: pr.clip, value: pr.value, verdict: pr.verdict, threshold: pr.threshold });
          if (pr.verdict === 'FAIL') failures.push({ source: name, message: `${pr.probe || 'probe'} ${pr.metric} = ${pr.value} reported FAIL` });
        }
      });
    } else if (doc.verdict === 'PASS' || doc.verdict === 'FAIL') {
      evidence.push(f);
      probes.push({ source: name, probe: doc.asset ? `AvatarProbe ${doc.asset}` : name, metric: 'verdict', value: doc.verdict, verdict: doc.verdict });
      if (doc.verdict === 'FAIL') failures.push({ source: name, message: 'reported verdict FAIL' });
    }
  }

  const verdict = evidence.length === 0 ? 'NOT-RUN' : failures.length ? 'FAIL' : 'PASS';
  return { verdict, failures, routed, notes, evidence, probes, checks, perf };
}

function renderSummary(runId, runDirRel, ev, rows) {
  const L = [];
  L.push(`# QA run ${runId}`, '');
  L.push(`- Verdict: **${ev.verdict}**`);
  L.push(`- Folder: \`${runDirRel}\``);
  L.push(`- Generated: ${utcIso()} by \`Tools/kit/qa/probe_report.mjs\` (do not edit; re-run the tool)`);
  if (rows.length) L.push(`- Ledger rows: ${rows.map((r) => `${r.row} ${r.verdict}`).join(', ')}`);
  if (ev.checks) {
    const c = ev.checks;
    L.push(`- Scenario: ${c.scenario ?? '?'}; checks.json result ${c.result}; console errors ${c.console?.errors ?? '?'}, exceptions ${c.console?.exceptions ?? '?'}, warnings ${c.console?.warnings ?? '?'}`);
  }
  if (ev.perf) {
    const p = ev.perf;
    L.push(`- Perf (${p.kind}, ${p.machine}): p50 ${p.frame_ms_p50} ms, p95 ${p.frame_ms_p95} ms, worst ${p.worst_frame_ms ?? '?'} ms, first-visit hitch ${p.first_visit_hitch_ms ?? '?'} ms`);
  }
  L.push('');
  L.push('## Failures', '');
  if (ev.failures.length) for (const f of ev.failures) L.push(`- ${f.source}: ${f.message}`);
  else L.push('- none');
  L.push('', '## Routed to the reviewer (never auto-failed)', '');
  if (ev.routed.length) {
    for (const r of ev.routed) {
      if (r.current) L.push(`- visual: \`${r.current}\` vs \`${r.baseline}\`: ${r.changed_pct}% changed${r.diff_png ? `, diff \`${r.diff_png}\`` : ''}`);
      else L.push(`- ${r.source}: ${r.metric} = ${r.value}`);
    }
  } else L.push('- none');
  L.push('', '## Probe results', '');
  if (ev.probes.length) {
    L.push('| Source | Probe | Metric | Body/clip | Value | Threshold | Verdict |', '|---|---|---|---|---|---|---|');
    for (const p of ev.probes) {
      const thr = p.threshold ? `${p.threshold.op} ${round(p.threshold.value)}${p.threshold.unit ? ` ${p.threshold.unit}` : ''}` : '';
      L.push(`| ${p.source} | ${p.probe ?? ''} | ${p.metric} | ${[p.body, p.clip].filter(Boolean).join('/')} | ${JSON.stringify(p.value)} | ${thr} | ${p.verdict ?? '?'} |`);
    }
  } else L.push('- none');
  if (ev.notes.length) {
    L.push('', '## Notes', '');
    for (const n of ev.notes) L.push(`- ${n}`);
  }
  L.push('', '## Evidence', '');
  for (const e of ev.evidenceRel) L.push(`- \`${e}\``);
  if (!ev.evidenceRel.length) L.push('- none (NOT-RUN)');
  return L.join('\n') + '\n';
}

/**
 * Both freshness facts, as game_gate records them, so either gates.json freshness mode can close the row: head is
 * `git rev-parse HEAD` run in W (the same command gate_close compares with, so a W inside a larger repository works),
 * tree is the freshness tree hash of U.
 */
export async function freshness(ws, unity) {
  const gates = readJsonOr(path.join(ws, 'Docs', 'qa', 'gates.json'), null, { onInvalid: 'fallback' });
  const f = gates?.freshness || {};
  const h = await run('git', ['rev-parse', 'HEAD'], { cwd: ws, timeoutMs: 15000, drainMs: 200 });
  const out = String(h.stdout || '').trim();
  const head = h.exit === 0 && /^[0-9a-f]{40}([0-9a-f]{24})?$/.test(out) ? out : null;
  return { head, tree: treeHash(unity, f.tree_roots ?? TREE_ROOTS, { excludes: f.tree_excludes ?? TREE_EXCLUDES }) };
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    row: { type: 'string', multiple: true, default: [] },
    config: { type: 'string' },
    'no-config': { type: 'boolean', default: false },
    'dry-run': { type: 'boolean', default: false },
  });
  if (positionals.length !== 1) throw new UsageError('usage: probe_report.mjs <run_dir> [--row <rowId>]... [--json]');
  const runDir = path.resolve(positionals[0]);
  if (!fs.existsSync(runDir) || !fs.statSync(runDir).isDirectory()) throw new UsageError(`run folder not found: ${runDir}`);
  for (const r of values.row) if (!ROW_RE.test(r)) throw new UsageError(`--row ${r} is not a row id like M2-07`);
  const { ws, unity } = roots(runDir);
  if (values.row.length && !ws) throw new CliError('no Tools/kit/kit.json above the run folder: the kit is not installed, so there is no Docs/qa/gates.jsonl to write', EXIT.INFRA);

  let config = null;
  const cfgFile = values['no-config'] ? null : values.config ? path.resolve(values.config) : defaultProbesConfig(ws);
  if (cfgFile) {
    if (!fs.existsSync(cfgFile)) throw new UsageError(`--config file not found: ${cfgFile}`);
    const loaded = loadProbesConfig(cfgFile);
    if (loaded.errors.length) throw new UsageError(`probes.config.json is invalid: ${loaded.errors[0]}`);
    config = loaded.config;
  }
  const ev = evaluateRun(runDir, { ws, config });
  if (!config && ev.probes.length) ev.notes.push('no probes.config.json: probe verdicts taken as reported');
  ev.evidenceRel = ev.evidence.map((e) => relOrAbs(ws, e));
  const runId = ev.checks?.run_id || path.basename(runDir);
  const secs = ev.checks && Date.parse(ev.checks.ended_utc) >= Date.parse(ev.checks.started_utc)
    ? Math.round((Date.parse(ev.checks.ended_utc) - Date.parse(ev.checks.started_utc)) / 100) / 10
    : null;

  const rows = values.row.map((row) => ({ row, verdict: ev.verdict }));
  const written = [];
  if (rows.length && !values['dry-run']) {
    const fresh = await freshness(ws, unity);
    const hashes = ev.evidence.map((e) => sha256File(e));
    const gatesSchema = loadKitSchema('gates.schema.json', 'game-studio-run');
    const notes = [
      ev.failures.length ? `${ev.failures.length} failure(s): ${ev.failures.slice(0, 3).map((f) => f.message).join(' | ').slice(0, 400)}` : null,
      ev.routed.length ? `${ev.routed.length} item(s) routed to the reviewer` : null,
    ].filter(Boolean).join('; ');
    for (const r of rows) {
      const line = {
        utc: utcIso(),
        milestone: milestoneOf(r.row),
        row: r.row,
        verdict: ev.verdict,
        head: fresh.head,
        tree: fresh.tree,
        evidence: ev.evidenceRel,
        evidence_sha256: hashes,
        by: 'probe',
        run: runId,
        secs,
        cmd: 'probe_report',
        ...(notes ? { notes } : {}),
      };
      const v = validate(gatesSchema, line, { pointer: '#/$defs/ledgerRow' });
      if (!v.valid) throw new CliError(`ledger row for ${r.row} is invalid: ${formatErrors(v.errors)}`, EXIT.FAIL);
      appendJsonl(path.join(ws, 'Docs', 'qa', 'gates.jsonl'), line);
      written.push(line);
    }
  }
  const summaryFile = path.join(runDir, 'summary.md');
  writeTextAtomic(summaryFile, renderSummary(runId, relOrAbs(ws, runDir), ev, rows));

  const exit = ev.verdict === 'PASS' ? EXIT.OK : ev.verdict === 'FAIL' ? EXIT.FAIL : EXIT.USAGE;
  const result = {
    ok: ev.verdict === 'PASS',
    exit,
    run_id: runId,
    verdict: ev.verdict,
    rows,
    ledger_written: written.length,
    failures: ev.failures,
    routed: ev.routed,
    notes: ev.notes,
    evidence: ev.evidenceRel,
    summary: relOrAbs(ws, summaryFile),
  };
  const text = [
    `${ev.verdict}: ${runId} (${ev.evidence.length} evidence file(s), ${ev.failures.length} failure(s), ${ev.routed.length} routed)`,
    ...ev.failures.map((f) => `  FAIL ${f.source}: ${f.message}`),
    ...ev.routed.map((r) => `  ROUTE ${r.current || r.metric}`),
    `  summary: ${result.summary}`,
    ...(written.length ? [`  appended ${written.length} row(s) to Docs/qa/gates.jsonl`] : []),
  ];
  emit(result, { json: values.json, text });
  return exit;
}

if (isMain(import.meta.url)) await runCli(main);

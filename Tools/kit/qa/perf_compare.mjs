#!/usr/bin/env node
// perf_compare.mjs: compare a built player's perf.json with a baseline perf.json (CONTRACTS.md §4.9; ARCHITECTURE §3.12).
//   node Tools/kit/qa/perf_compare.mjs <perf.json> --baseline <perf.json> [--budget-ms <n>] [--config <probes.config.json>]
//        [--bisect-pct <n>] [--fail-pct <n>] [--json]
// Only release and release-qa numbers count (editor FPS and development players never do). Same kind (build profile),
// machine and scenario as the baseline. p95 frame-time regression: above --bisect-pct (probes.config perf_regression_p95_pct, default 5) is
// BISECT, above --fail-pct (default 10) is FAIL. Budgets (probes.config): p95 frame time (--budget-ms overrides),
// worst frame after warm-up, first-visit hitch.
// Result: {verdict: PASS|FAIL|BISECT, p95_delta_pct, worst_frame_ms, failures[], ...}. Exit 0 PASS, 1 FAIL or BISECT.
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, parseCli, emit, isMain, runCli, UsageError } from '../lib/cli.mjs';
import { readJson } from '../lib/jsonio.mjs';
import { validate, formatErrors } from '../lib/schema.mjs';
import { loadKitSchema, roots, loadProbesConfig, defaultProbesConfig, effectiveThreshold, satisfies, round, relOrAbs } from './lib/qa_common.mjs';

const GATE_KINDS = ['release', 'release-qa'];
export const DEFAULT_FAIL_PCT = 10;
export const DEFAULT_BISECT_PCT = 5;

const pct = (cur, base) => (base > 0 ? ((cur - base) / base) * 100 : null);

/**
 * Compare two perf documents (checks.schema.json#/$defs/perf).
 * @param {object} cur
 * @param {object} base
 * @param {{config?: object|null, budgetMs?: number|null, bisectPct?: number|null, failPct?: number|null}} [opts]
 * @returns {{verdict: 'PASS'|'FAIL'|'BISECT', p95_delta_pct: number|null, p50_delta_pct: number|null, worst_frame_ms: number|null,
 *   failures: {code: string, message: string}[], limits: object}}
 */
export function comparePerf(cur, base, opts = {}) {
  const failures = [];
  const bisect = [];
  const cfg = opts.config || null;
  const regT = cfg ? effectiveThreshold(cfg, 'perf_regression_p95_pct') : null;
  const bisectPct = opts.bisectPct ?? (typeof regT?.value === 'number' ? regT.value : DEFAULT_BISECT_PCT);
  const failPct = Math.max(opts.failPct ?? regT?.params?.fail_pct ?? DEFAULT_FAIL_PCT, bisectPct);
  const budgetT = cfg ? effectiveThreshold(cfg, 'frame_time_p95_ms') : null;
  const budgetMs = opts.budgetMs ?? (typeof budgetT?.value === 'number' ? budgetT.value : null);
  const worstT = cfg ? effectiveThreshold(cfg, 'worst_frame_after_warmup_ms') : null;
  const hitchT = cfg ? effectiveThreshold(cfg, 'first_visit_hitch_ms') : null;
  const worstMs = typeof worstT?.value === 'number' ? worstT.value : 33;
  const hitchMs = typeof hitchT?.value === 'number' ? hitchT.value : 50;

  if (!GATE_KINDS.includes(cur.kind)) failures.push({ code: 'kind_not_release', message: `perf.json kind is "${cur.kind}"; only release or release-qa builds count (editor and dev numbers never gate)` });
  if (!GATE_KINDS.includes(base.kind)) failures.push({ code: 'baseline_kind_not_release', message: `baseline kind is "${base.kind}"; record the baseline from a release or release-qa build` });
  // release and release-qa are different binaries (QaRunner and GSK_QA code in one only, OWNER_DEFAULTS C-16 option b).
  else if (GATE_KINDS.includes(cur.kind) && cur.kind !== base.kind) failures.push({ code: 'kind_mismatch', message: `perf.json kind "${cur.kind}" differs from the baseline's "${base.kind}"; keep one baseline per build profile and compare like with like` });
  if (cur.machine !== base.machine) failures.push({ code: 'machine_mismatch', message: `machine "${cur.machine}" differs from the baseline's "${base.machine}"; baselines are per machine, record one on this machine` });
  if (cur.scenario && base.scenario && cur.scenario !== base.scenario) failures.push({ code: 'scenario_mismatch', message: `scenario "${cur.scenario}" differs from the baseline's "${base.scenario}"` });

  const p95 = pct(cur.frame_ms_p95, base.frame_ms_p95);
  const p50 = pct(cur.frame_ms_p50, base.frame_ms_p50);
  if (p95 !== null) {
    if (p95 > failPct) failures.push({ code: 'p95_regression', message: `p95 frame time ${cur.frame_ms_p95} ms is ${round(p95)}% slower than the baseline's ${base.frame_ms_p95} ms (fail above ${failPct}%)` });
    else if (p95 > bisectPct) bisect.push({ code: 'p95_regression_bisect', message: `p95 frame time is ${round(p95)}% slower than the baseline (bisect above ${bisectPct}%, fail above ${failPct}%)` });
  }
  if (budgetMs !== null && !satisfies('<=', cur.frame_ms_p95, budgetMs)) failures.push({ code: 'over_budget', message: `p95 frame time ${cur.frame_ms_p95} ms is over the ${budgetMs} ms budget` });
  if (typeof cur.worst_frame_ms === 'number' && !satisfies(worstT?.op ?? '<=', cur.worst_frame_ms, worstMs)) failures.push({ code: 'worst_frame', message: `worst frame after warm-up ${cur.worst_frame_ms} ms is over ${worstMs} ms` });
  if (typeof cur.first_visit_hitch_ms === 'number' && !satisfies(hitchT?.op ?? '<=', cur.first_visit_hitch_ms, hitchMs)) failures.push({ code: 'first_visit_hitch', message: `first-visit hitch ${cur.first_visit_hitch_ms} ms is over ${hitchMs} ms` });

  const verdict = failures.length ? 'FAIL' : bisect.length ? 'BISECT' : 'PASS';
  return {
    verdict,
    p95_delta_pct: p95 === null ? null : round(p95),
    p50_delta_pct: p50 === null ? null : round(p50),
    worst_frame_ms: typeof cur.worst_frame_ms === 'number' ? cur.worst_frame_ms : null,
    failures: [...failures, ...bisect],
    limits: { bisect_pct: bisectPct, fail_pct: failPct, budget_ms: budgetMs, worst_frame_ms: worstMs, first_visit_hitch_ms: hitchMs },
  };
}

function loadPerf(file, label) {
  if (!fs.existsSync(file)) throw new UsageError(`${label} not found: ${file}`);
  const doc = readJson(file);
  const schema = loadKitSchema('checks.schema.json', 'game-qa-probes');
  const r = validate(schema, doc, { pointer: '#/$defs/perf' });
  if (!r.valid) throw new UsageError(`${label} is not a valid perf.json: ${formatErrors(r.errors)}`);
  return doc;
}

function num(v, name) {
  if (v === undefined) return null;
  const n = Number(v);
  if (!Number.isFinite(n) || n < 0) throw new UsageError(`--${name} must be a non-negative number`);
  return n;
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    baseline: { type: 'string' },
    'budget-ms': { type: 'string' },
    config: { type: 'string' },
    'no-config': { type: 'boolean', default: false },
    'bisect-pct': { type: 'string' },
    'fail-pct': { type: 'string' },
  });
  if (positionals.length !== 1 || !values.baseline) throw new UsageError('usage: perf_compare.mjs <perf.json> --baseline <perf.json> [--budget-ms <n>] [--json]');
  const curFile = path.resolve(positionals[0]);
  const baseFile = path.resolve(values.baseline);
  const cur = loadPerf(curFile, 'perf.json');
  const base = loadPerf(baseFile, 'baseline perf.json');
  const { ws } = roots(path.dirname(curFile));
  let config = null;
  const cfgFile = values['no-config'] ? null : values.config ? path.resolve(values.config) : defaultProbesConfig(ws);
  if (cfgFile) {
    if (!fs.existsSync(cfgFile)) throw new UsageError(`--config file not found: ${cfgFile}`);
    const loaded = loadProbesConfig(cfgFile);
    if (loaded.errors.length) throw new UsageError(`probes.config.json is invalid: ${loaded.errors[0]}`);
    config = loaded.config;
  }
  const r = comparePerf(cur, base, {
    config,
    budgetMs: num(values['budget-ms'], 'budget-ms'),
    bisectPct: num(values['bisect-pct'], 'bisect-pct'),
    failPct: num(values['fail-pct'], 'fail-pct'),
  });
  const exit = r.verdict === 'PASS' ? EXIT.OK : EXIT.FAIL;
  const result = {
    ok: r.verdict === 'PASS',
    exit,
    ...r,
    current: { file: relOrAbs(ws, curFile), build_id: cur.build_id, kind: cur.kind, machine: cur.machine, frame_ms_p50: cur.frame_ms_p50, frame_ms_p95: cur.frame_ms_p95 },
    baseline: { file: relOrAbs(ws, baseFile), build_id: base.build_id, kind: base.kind, machine: base.machine, frame_ms_p50: base.frame_ms_p50, frame_ms_p95: base.frame_ms_p95 },
  };
  const text = [
    `${r.verdict}: p95 ${base.frame_ms_p95} -> ${cur.frame_ms_p95} ms (${r.p95_delta_pct ?? 'n/a'}%), p50 ${base.frame_ms_p50} -> ${cur.frame_ms_p50} ms`,
    ...r.failures.map((f) => `  ${f.code}: ${f.message}`),
  ];
  emit(result, { json: values.json, text });
  return exit;
}

if (isMain(import.meta.url)) await runCli(main);

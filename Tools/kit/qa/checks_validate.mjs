#!/usr/bin/env node
// checks_validate.mjs: validate a QaRunner checks.json (CONTRACTS.md §4.9, §7.3).
//   node Tools/kit/qa/checks_validate.mjs <checks.json> [--config Docs/qa/probes.config.json] [--no-config] [--json]
// Checks: checks.schema.json; every capture path exists (W-relative); every probe_results row re-judged against
// probes.config.json (unknown metric, threshold or k that differs from the config, verdict that the numbers do not
// support); result PASS while a re-judged probe FAILs; ended_utc before started_utc.
// Result: {ok, errors:[{path, message}], missing_evidence:[path], warnings[], probes:{pass, fail, route}}. Exit 0 ok, 1 not.
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, parseCli, emit, isMain, runCli, UsageError } from '../lib/cli.mjs';
import { readJson } from '../lib/jsonio.mjs';
import { validate } from '../lib/schema.mjs';
import { loadKitSchema, roots, loadProbesConfig, defaultProbesConfig, judgeProbe, satisfies, effectiveThreshold, relOrAbs } from './lib/qa_common.mjs';

/**
 * Validate a parsed checks.json document.
 * @param {any} doc
 * @param {{ws?: string|null, config?: object|null, file?: string}} [ctx] ws resolves capture paths; config enables threshold checks
 * @returns {{ok: boolean, errors: {path: string, message: string}[], missing_evidence: string[], warnings: string[],
 *   probes: {pass: number, fail: number, route: number}, judged: object[]}}
 */
export function validateChecks(doc, ctx = {}) {
  const errors = [];
  const warnings = [];
  const missing = [];
  const judged = [];
  const probes = { pass: 0, fail: 0, route: 0 };
  const schema = loadKitSchema('checks.schema.json', 'game-qa-probes');
  const r = validate(schema, doc);
  for (const e of r.errors) errors.push({ path: e.path, message: `${e.keyword}: ${e.message}` });
  if (!doc || typeof doc !== 'object' || Array.isArray(doc)) {
    return { ok: false, errors: errors.length ? errors : [{ path: '/', message: 'checks.json must be an object' }], missing_evidence: missing, warnings, probes, judged };
  }

  if (typeof doc.started_utc === 'string' && typeof doc.ended_utc === 'string') {
    const s = Date.parse(doc.started_utc);
    const e = Date.parse(doc.ended_utc);
    if (Number.isFinite(s) && Number.isFinite(e) && e < s) errors.push({ path: '/ended_utc', message: 'ended_utc is before started_utc' });
  }

  const base = ctx.ws || process.cwd();
  const seen = new Set();
  const checkPath = (p, where) => {
    if (typeof p !== 'string' || !p) return;
    if (path.isAbsolute(p)) {
      errors.push({ path: where, message: `evidence path must be workspace-relative, got absolute ${p}` });
      if (!fs.existsSync(p)) missing.push(p);
      return;
    }
    if (seen.has(p)) return;
    seen.add(p);
    if (!fs.existsSync(path.join(base, p))) missing.push(p);
  };
  (Array.isArray(doc.steps) ? doc.steps : []).forEach((s, i) => {
    (Array.isArray(s?.captures) ? s.captures : []).forEach((c, j) => checkPath(c, `/steps/${i}/captures/${j}`));
  });
  (Array.isArray(doc.captures) ? doc.captures : []).forEach((c, i) => checkPath(c?.path, `/captures/${i}/path`));

  const results = Array.isArray(doc.probe_results) ? doc.probe_results : [];
  if (ctx.config) {
    results.forEach((pr, i) => {
      if (!pr || typeof pr.metric !== 'string') return;
      const j = judgeProbe(ctx.config, pr);
      judged.push({ index: i, probe: pr.probe, metric: pr.metric, body: pr.body, clip: pr.clip, value: pr.value, verdict: j.verdict, threshold: j.threshold });
      for (const p of j.problems) errors.push({ path: `/probe_results/${i}`, message: `${p.code}: ${p.message}` });
      if (j.verdict === 'PASS') probes.pass++;
      else if (j.verdict === 'ROUTE') probes.route++;
      else if (j.verdict === 'FAIL') probes.fail++;
    });
    const consoleChecks = [
      ['console_exceptions', doc.console?.exceptions],
      ['console_errors', doc.console?.errors],
    ];
    for (const [metric, value] of consoleChecks) {
      const t = effectiveThreshold(ctx.config, metric);
      if (t && typeof value === 'number' && !satisfies(t.op, value, t.value) && doc.result !== 'FAIL' && doc.result !== 'ERROR') {
        errors.push({ path: '/console', message: `${metric}=${value} breaks ${t.op} ${t.value} but result is ${doc.result}` });
      }
    }
  } else {
    for (const pr of results) {
      if (pr?.verdict === 'PASS') probes.pass++;
      else if (pr?.verdict === 'ROUTE') probes.route++;
      else if (pr?.verdict === 'FAIL') probes.fail++;
    }
    if (results.length) warnings.push('no probes.config.json: probe verdicts were taken as reported, not re-judged');
  }
  if (doc.result === 'PASS' && probes.fail > 0) {
    errors.push({ path: '/result', message: `result is PASS but ${probes.fail} probe result(s) fail their threshold` });
  }

  if (ctx.file && typeof doc.run_id === 'string') {
    const folder = path.basename(path.dirname(path.resolve(ctx.file)));
    if (/^\d{8}T\d{6}Z-/.test(folder) && folder !== doc.run_id) warnings.push(`run_id ${doc.run_id} differs from its folder name ${folder}`);
  }
  return { ok: errors.length === 0 && missing.length === 0, errors, missing_evidence: missing, warnings, probes, judged };
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    config: { type: 'string' },
    'no-config': { type: 'boolean', default: false },
  });
  if (positionals.length !== 1) throw new UsageError('usage: checks_validate.mjs <checks.json> [--config <probes.config.json>] [--no-config] [--json]');
  const file = path.resolve(positionals[0]);
  const { ws } = roots(path.dirname(file));
  const base = ws || process.cwd();
  let result;
  if (!fs.existsSync(file)) {
    result = { ok: false, errors: [{ path: '/', message: `checks.json not found: ${relOrAbs(ws, file)}` }], missing_evidence: [], warnings: [], probes: { pass: 0, fail: 0, route: 0 } };
  } else {
    let doc;
    try {
      doc = readJson(file);
    } catch (e) {
      doc = undefined;
      result = { ok: false, errors: [{ path: '/', message: e.message }], missing_evidence: [], warnings: [], probes: { pass: 0, fail: 0, route: 0 } };
    }
    if (doc !== undefined) {
      let config = null;
      const warnings = [];
      const cfgFile = values['no-config'] ? null : values.config ? path.resolve(values.config) : defaultProbesConfig(ws);
      if (cfgFile) {
        if (!fs.existsSync(cfgFile)) throw new UsageError(`--config file not found: ${cfgFile}`);
        const loaded = loadProbesConfig(cfgFile);
        if (loaded.errors.length) warnings.push(`probes.config.json is invalid (${loaded.errors[0]}); thresholds not checked`);
        else config = loaded.config;
      }
      const v = validateChecks(doc, { ws: base, config, file });
      result = { ...v, warnings: [...warnings, ...v.warnings] };
      delete result.judged;
      result.result = doc?.result ?? null;
    }
  }
  result = { ok: result.ok, exit: result.ok ? EXIT.OK : EXIT.FAIL, file: relOrAbs(ws, file), ...result };
  const text = [
    `${result.ok ? 'OK' : 'INVALID'}: ${result.file}`,
    ...result.errors.map((e) => `  error ${e.path}: ${e.message}`),
    ...result.missing_evidence.map((m) => `  missing evidence: ${m}`),
    ...result.warnings.map((w) => `  warning: ${w}`),
  ];
  emit(result, { json: values.json, text });
  return result.exit;
}

if (isMain(import.meta.url)) await runCli(main);

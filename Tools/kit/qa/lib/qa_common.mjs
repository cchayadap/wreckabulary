// Shared helpers for Tools/kit/qa/*.mjs: schema lookup, workspace roots, and the probes.config.json threshold engine
// (CONTRACTS.md §7.3, §7.4; ARCHITECTURE §3.12 threshold table and the body-class scaling rule).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { findWorkspace, normalizeUnitySubdir, readKitConfig, toPosix, isInside, relPosix } from '../../lib/paths.mjs';
import { readJson } from '../../lib/jsonio.mjs';
import { validate, formatErrors } from '../../lib/schema.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));

/**
 * Locate a kit schema file: the installed copy (Tools/kit/schemas/) first, then the source repo layout
 * (skills/<owner>/assets/schemas/), then the workspace's installed copy.
 * @param {string} file e.g. 'checks.schema.json'
 * @param {string} owner owner skill, e.g. 'game-qa-probes'
 * @returns {string}
 */
export function schemaFile(file, owner) {
  const candidates = [
    path.join(HERE, '..', '..', 'schemas', file),
    path.join(HERE, '..', '..', '..', '..', '..', owner, 'assets', 'schemas', file),
  ];
  const ws = findWorkspace(process.cwd());
  if (ws) candidates.push(path.join(ws, 'Tools', 'kit', 'schemas', file));
  const hit = candidates.find((c) => fs.existsSync(c));
  if (!hit) {
    const e = new Error(`schema ${file} not found (looked in ${candidates.map(toPosix).join(', ')})`);
    e.code = 'ENOSCHEMA';
    throw e;
  }
  return hit;
}

const schemaCache = new Map();
/** Parsed schema (cached). @param {string} file @param {string} owner */
export function loadKitSchema(file, owner) {
  const p = schemaFile(file, owner);
  if (!schemaCache.has(p)) schemaCache.set(p, readJson(p));
  return schemaCache.get(p);
}

/**
 * Workspace (W) and Unity project (U) roots for a path, from Tools/kit/kit.json. Null ws when no kit is installed above it.
 * @param {string} start
 */
export function roots(start) {
  const ws = findWorkspace(start) || null;
  if (!ws) return { ws: null, unity: null, unitySubdir: '.' };
  const sub = normalizeUnitySubdir(readKitConfig(ws)?.unity_subdir ?? '.');
  return { ws, unity: sub === '.' ? ws : path.join(ws, sub), unitySubdir: sub };
}

/** W-relative POSIX path when abs lies inside ws; otherwise the absolute path, POSIX-separated. @param {string|null} ws @param {string} abs */
export function relOrAbs(ws, abs) {
  const a = path.resolve(abs);
  if (ws && isInside(a, ws)) return relPosix(ws, a);
  return toPosix(a);
}

// ---------------------------------------------------------------------------------------------------------------
// Thresholds

/**
 * Read and validate Docs/qa/probes.config.json (probes.config.schema.json).
 * @param {string} file
 * @returns {{config: object, errors: string[]}}
 */
export function loadProbesConfig(file) {
  const config = readJson(file);
  const r = validate(loadKitSchema('probes.config.schema.json', 'game-qa-probes'), config);
  return { config, errors: r.valid ? [] : [formatErrors(r.errors)] };
}

/** Default config path for a workspace, or null when absent. @param {string|null} ws */
export function defaultProbesConfig(ws) {
  if (!ws) return null;
  const p = path.join(ws, 'Docs', 'qa', 'probes.config.json');
  return fs.existsSync(p) ? p : null;
}

/**
 * k = body.height_m / reference_height_m for a body id from config.bodies. Null when the body is unknown.
 * @param {object} config
 * @param {string} [bodyId]
 */
export function bodyScale(config, bodyId) {
  if (!bodyId) return null;
  const body = (config.bodies || []).find((b) => b.id === bodyId);
  if (!body) return null;
  return body.height_m / (config.reference_height_m || 1.8);
}

/**
 * The threshold that applies to a metric (and body): the default, replaced by the newest matching override.
 * A global override (no body) replaces the base value and is then scaled by k like the default; a body-specific
 * override is already in that body's units and is not scaled again.
 * @param {object} config
 * @param {string} metric
 * @param {string} [bodyId]
 * @returns {{op: string, value: number|boolean, unit: string, on_fail: string, tag: string, k: number|null,
 *   scaled: boolean, source: 'default'|'override'|'body_override', params: object}|null} null when the metric is unknown
 */
export function effectiveThreshold(config, metric, bodyId) {
  const base = config.thresholds?.[metric];
  if (!base) return null;
  const newest = (list) => list.sort((a, b) => String(a.utc).localeCompare(String(b.utc))).pop();
  const overrides = (config.overrides || []).filter((o) => o.metric === metric);
  const bodyOv = bodyId ? newest(overrides.filter((o) => o.body === bodyId)) : undefined;
  const globalOv = newest(overrides.filter((o) => !o.body));
  const k = base.scales_with_body ? bodyScale(config, bodyId) : null;
  let value = base.value;
  let source = 'default';
  let scaled = false;
  if (bodyOv) {
    value = bodyOv.value;
    source = 'body_override';
  } else {
    if (globalOv) { value = globalOv.value; source = 'override'; }
    if (base.scales_with_body && typeof value === 'number' && k !== null) { value *= k; scaled = true; }
  }
  return { op: base.op, value, unit: base.unit, on_fail: base.on_fail, tag: base.tag, k, scaled, source, params: base.params || {} };
}

const EPS = (t) => 1e-9 * Math.max(1, Math.abs(t));

/**
 * Does value satisfy `value <op> threshold`? Numbers compare with a tiny tolerance; booleans only with ==.
 * @param {string} op
 * @param {number|boolean} value
 * @param {number|boolean} thr
 */
export function satisfies(op, value, thr) {
  if (typeof thr === 'boolean' || typeof value === 'boolean') return op === '==' && value === thr;
  if (typeof value !== 'number' || !Number.isFinite(value)) return false;
  switch (op) {
    case '<=': return value <= thr + EPS(thr);
    case '<': return value < thr;
    case '>=': return value >= thr - EPS(thr);
    case '>': return value > thr;
    case '==': return Math.abs(value - thr) <= EPS(thr);
    case 'abs<=': return Math.abs(value) <= thr + EPS(thr);
    default: return false;
  }
}

/** Verdict for a failed threshold by its on_fail policy: ROUTE never fails (visual diff); FAIL, BISECT, REVERT fail. */
export function failVerdict(onFail) {
  return onFail === 'ROUTE' ? 'ROUTE' : 'FAIL';
}

/** Two thresholds agree within display rounding (probes may round to 2 decimals). */
export function closeEnough(a, b) {
  if (typeof a !== 'number' || typeof b !== 'number') return a === b;
  return Math.abs(a - b) <= Math.max(0.005, 0.01 * Math.abs(b));
}

/**
 * Re-judge one probe result against the config.
 * @param {object} config
 * @param {{metric: string, value: number|boolean, body?: string, threshold?: {op: string, value: number|boolean}, k?: number, verdict?: string}} r
 * @returns {{verdict: 'PASS'|'FAIL'|'ROUTE'|null, threshold: object|null, problems: {code: string, message: string}[]}}
 */
export function judgeProbe(config, r) {
  const problems = [];
  const eff = effectiveThreshold(config, r.metric, r.body);
  if (!eff) return { verdict: null, threshold: null, problems: [{ code: 'unknown_metric', message: `metric "${r.metric}" is not in probes.config.json thresholds` }] };
  const base = config.thresholds[r.metric];
  if (base.scales_with_body && eff.k === null && eff.source !== 'body_override') {
    problems.push({ code: 'unknown_body', message: `metric "${r.metric}" scales with body size but body "${r.body ?? ''}" is not in probes.config.json bodies` });
  }
  let thrValue = eff.value;
  if (r.threshold) {
    if (r.threshold.op !== eff.op) problems.push({ code: 'threshold_mismatch', message: `op ${r.threshold.op} differs from the config's ${eff.op}` });
    if (!closeEnough(r.threshold.value, eff.value)) {
      problems.push({ code: 'threshold_mismatch', message: `threshold ${r.threshold.value} differs from the config's ${round(eff.value)}${eff.scaled ? ` (k=${round(eff.k)})` : ''}` });
    } else if (typeof r.threshold.value === 'number') {
      thrValue = r.threshold.value;
    }
  }
  if (typeof r.k === 'number' && eff.k !== null && !closeEnough(r.k, eff.k)) {
    problems.push({ code: 'k_mismatch', message: `k ${r.k} differs from body_height/reference_height = ${round(eff.k)}` });
  }
  const verdict = satisfies(eff.op, r.value, thrValue) ? 'PASS' : failVerdict(eff.on_fail);
  if (r.verdict && r.verdict !== verdict) {
    problems.push({ code: 'verdict_mismatch', message: `probe reported ${r.verdict}, but ${JSON.stringify(r.value)} ${eff.op} ${round(thrValue)} gives ${verdict}` });
  }
  return { verdict, threshold: { ...eff, value: thrValue }, problems };
}

/** Round for messages. @param {unknown} v */
export function round(v) {
  return typeof v === 'number' ? Math.round(v * 10000) / 10000 : v;
}

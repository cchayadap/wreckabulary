#!/usr/bin/env node
// visual_diff.mjs: golden-image diff that never fails a gate (CONTRACTS.md §4.9; research_qa_harness §1.7).
//   node Tools/kit/qa/visual_diff.mjs <baseline.png> <current.png> [--out <dir>] [--threshold 0.1] [--max-pct 0.5]
//        [--tool auto|odiff|builtin|pillow] [--count-aa] [--config <probes.config.json>] [--json]
// Writes <out>/visual.json (checks.schema.json#/$defs/visual) and <out>/diff_<current name>.png. --out defaults to
// <current image's folder>/visual/<current name>/, one folder per pair, so diffing several captures of one run never
// overwrites a routed result (probe_report reads every visual*.json below the run folder). changed_pct <= max-pct
// (probes.config visual_diff_changed_pct, default 0.5) is "unchanged"; anything above, a size change included, is
// "route_to_reviewer". The verdict is never FAIL.
// Tool order (auto): odiff pinned in .harness/policy.json pinned_packages.npx (odiff-bin@<ver>, run through npx), else
// odiff on PATH, else the kit's built-in diff for PNG (pixelmatch-compatible, tool "pixelmatch"), else Pillow through
// `uv run --no-config --with pillow==<pin> --with numpy==<pin>` for other formats (JPEG): UV_PINS, or the pillow== and
// numpy== entries of .harness/policy.json pinned_packages.uv; uv's package-index variables are cleared for that run.
// Nothing is npm-installed into the kit.
// Exit 0 (unchanged or routed), 2 usage, 3 tool error (visual.json is still written with verdict "error").
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { EXIT, parseCli, emit, isMain, runCli, UsageError } from '../lib/cli.mjs';
import { readJsonOr, writeJsonAtomic } from '../lib/jsonio.mjs';
import { validate, formatErrors } from '../lib/schema.mjs';
import { run, which } from '../lib/proc.mjs';
import { utcIso } from '../lib/time.mjs';
import { isPng, pngSize, decodePng, encodePng } from './lib/png.mjs';
import { diffImages, meanSsim } from './lib/imgdiff.mjs';
import { loadKitSchema, roots, loadProbesConfig, defaultProbesConfig, effectiveThreshold, relOrAbs, round } from './lib/qa_common.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const ODIFF_EXIT = { MATCH: 0, LAYOUT: 21, PIXELS: 22 };
/** Exact versions for the Pillow fallback (wheels for CPython 3.12-3.14 on win_amd64). */
export const UV_PINS = Object.freeze({ pillow: 'pillow==12.3.0', numpy: 'numpy==2.5.3' });
/** Environment variables that would point uv at another package index. */
export const UV_INDEX_ENV = Object.freeze(['UV_INDEX', 'UV_INDEX_URL', 'UV_DEFAULT_INDEX', 'UV_EXTRA_INDEX_URL', 'UV_FIND_LINKS', 'PIP_INDEX_URL', 'PIP_EXTRA_INDEX_URL', 'PIP_FIND_LINKS']);

/**
 * The exact pillow and numpy specs for uv: .harness/policy.json pinned_packages.uv entries "pillow==x" and "numpy==x"
 * override UV_PINS; an entry that is not an exact == pin is ignored.
 * @param {string|null} ws
 * @returns {[string, string]}
 */
export function uvPins(ws) {
  const policy = ws ? readJsonOr(path.join(ws, '.harness', 'policy.json'), null, { onInvalid: 'fallback' }) : null;
  const list = Array.isArray(policy?.pinned_packages?.uv) ? policy.pinned_packages.uv : [];
  const pick = (name) => list.map((s) => String(s).trim()).find((s) => new RegExp(`^${name}==\\d[\\w.]*$`, 'i').test(s)) || UV_PINS[name];
  return [pick('pillow'), pick('numpy')];
}

/** Default --out: <folder of current>/visual/<current name>/ (one folder per pair). @param {string} current */
export function defaultOut(current) {
  return path.join(path.dirname(current), 'visual', path.basename(current).replace(/\.[^.]+$/, ''));
}

/** odiff-bin@<ver> from .harness/policy.json pinned_packages.npx, or null. @param {string|null} ws */
export function pinnedOdiff(ws) {
  if (!ws) return null;
  const policy = readJsonOr(path.join(ws, '.harness', 'policy.json'), null, { onInvalid: 'fallback' });
  const list = policy?.pinned_packages?.npx;
  if (!Array.isArray(list)) return null;
  return list.find((s) => typeof s === 'string' && /^odiff-bin@\d+\.\d+\.\d+$/.test(s)) || null;
}

/**
 * Parse odiff's --parsable-stdout output: "0" (match, exit 0), "layout" (exit 21), "<count>;<pct>[;lines][;cols]" (exit 22).
 * @param {number|null} exit
 * @param {string} stdout
 * @returns {{kind: 'match'|'layout'|'pixels', changed: number, pct: number}|null} null when the output is not understood
 */
export function parseOdiff(exit, stdout) {
  const line = String(stdout || '').trim().split(/\r?\n/).pop() || '';
  if (exit === ODIFF_EXIT.MATCH) return { kind: 'match', changed: 0, pct: 0 };
  if (exit === ODIFF_EXIT.LAYOUT) return { kind: 'layout', changed: 0, pct: 100 };
  if (exit === ODIFF_EXIT.PIXELS) {
    const m = /^(\d+);([0-9.]+)/.exec(line);
    if (m) return { kind: 'pixels', changed: Number(m[1]), pct: Number(m[2]) };
  }
  return null;
}

function verdictFor(changedPct, maxPct) {
  return changedPct <= maxPct ? 'unchanged' : 'route_to_reviewer';
}

async function viaOdiff(cmd, preArgs, base, cur, diffPng, threshold, countAA, env) {
  const args = [...preArgs, base, cur, diffPng, `--threshold=${threshold}`, '--parsable-stdout', '--fail-on-layout'];
  if (!countAA) args.push('--antialiasing');
  const r = await run(cmd, args, { timeoutMs: 300000, env });
  const parsed = parseOdiff(r.exit, r.stdout);
  if (!parsed) return { error: `odiff failed (exit ${r.exit}${r.error ? `, ${r.error}` : ''}): ${(r.stderr || r.stdout || '').trim().slice(-400)}` };
  const sa = safeSize(base);
  const sb = safeSize(cur);
  if (parsed.kind === 'layout') {
    const area = Math.max(sa ? sa.width * sa.height : 0, sb ? sb.width * sb.height : 0);
    return { changed: area, total: area, pct: 100, layout: true, sizes: [sa, sb] };
  }
  let total = sa ? sa.width * sa.height : parsed.pct > 0 ? Math.round((parsed.changed * 100) / parsed.pct) : 0;
  if (parsed.kind === 'match') return { changed: 0, total, pct: 0, diffWritten: false };
  const pct = total > 0 ? (parsed.changed / total) * 100 : parsed.pct;
  return { changed: parsed.changed, total, pct, diffWritten: fs.existsSync(diffPng) };
}

function safeSize(file) {
  try {
    const fd = fs.openSync(file, 'r');
    const b = Buffer.alloc(32);
    fs.readSync(fd, b, 0, 32, 0);
    fs.closeSync(fd);
    return pngSize(b);
  } catch {
    return null;
  }
}

function viaBuiltin(base, cur, diffPng, threshold, countAA) {
  const a = decodePng(fs.readFileSync(base));
  const b = decodePng(fs.readFileSync(cur));
  if (a.width !== b.width || a.height !== b.height) {
    const area = Math.max(a.width * a.height, b.width * b.height);
    return { changed: area, total: area, pct: 100, layout: true, sizes: [{ width: a.width, height: a.height }, { width: b.width, height: b.height }] };
  }
  const d = diffImages(a, b, { threshold, countAA, diff: true });
  fs.mkdirSync(path.dirname(diffPng), { recursive: true });
  fs.writeFileSync(diffPng, encodePng({ width: a.width, height: a.height, data: d.diff }));
  return { changed: d.changed, antialiased: d.antialiased, total: d.total, pct: (d.changed / d.total) * 100, ssim: meanSsim(a, b), diffWritten: true };
}

async function viaPillow(base, cur, diffPng, threshold, countAA, env, ws) {
  if (!which('uv', { env })) return { error: 'the image is not a PNG and uv is not on PATH (needed for the Pillow fallback)' };
  const script = path.join(HERE, 'visual_diff_pil.py');
  const [pillow, numpy] = uvPins(ws);
  const args = ['run', '--no-config', '--no-project', '--with', pillow, '--with', numpy, 'python', script, base, cur, diffPng, String(threshold), countAA ? '1' : '0'];
  const uvEnv = { ...env };
  for (const k of Object.keys(uvEnv)) if (UV_INDEX_ENV.includes(k.toUpperCase())) delete uvEnv[k];
  const r = await run('uv', args, { timeoutMs: 600000, env: uvEnv });
  const line = String(r.stdout || '').trim().split(/\r?\n/).pop() || '';
  let j = null;
  try { j = JSON.parse(line); } catch { /* reported below */ }
  if (r.exit !== 0 || !j || j.ok !== true) return { error: `Pillow diff failed (exit ${r.exit}): ${(j?.error || r.stderr || r.stdout || '').trim().slice(-400)}` };
  if (j.layout) {
    const area = Math.max(j.sizes[0].width * j.sizes[0].height, j.sizes[1].width * j.sizes[1].height);
    return { changed: area, total: area, pct: 100, layout: true, sizes: j.sizes };
  }
  return { changed: j.changed, total: j.total, pct: j.total ? (j.changed / j.total) * 100 : 0, diffWritten: fs.existsSync(diffPng) };
}

/**
 * Diff two images and write visual.json.
 * @param {{baseline: string, current: string, out: string, threshold: number, maxPct: number, tool: string, countAA: boolean,
 *   ws: string|null, env?: NodeJS.ProcessEnv}} o
 * @returns {Promise<{visual: object, exit: number, extra: object}>}
 */
export async function visualDiff(o) {
  const env = o.env ?? process.env;
  const stem = path.basename(o.current).replace(/\.[^.]+$/, '');
  const diffPng = path.join(o.out, `diff_${stem}.png`);
  fs.mkdirSync(o.out, { recursive: true });
  const headA = fs.readFileSync(o.baseline).subarray(0, 8);
  const headB = fs.readFileSync(o.current).subarray(0, 8);
  const bothPng = isPng(headA) && isPng(headB);
  const pin = pinnedOdiff(o.ws);
  let tool = o.tool;
  if (tool === 'auto') tool = pin || which('odiff', { env }) ? 'odiff' : bothPng ? 'pixelmatch' : 'pillow';
  if (tool === 'builtin') tool = 'pixelmatch';
  let res;
  let impl = null;
  if (tool === 'odiff') {
    if (pin) { res = await viaOdiff('npx', ['-y', pin], o.baseline, o.current, diffPng, o.threshold, o.countAA, env); impl = `npx ${pin}`; }
    else if (which('odiff', { env })) { res = await viaOdiff('odiff', [], o.baseline, o.current, diffPng, o.threshold, o.countAA, env); impl = 'odiff on PATH'; }
    else res = { error: 'odiff is neither pinned in .harness/policy.json (pinned_packages.npx odiff-bin@<ver>) nor on PATH' };
  } else if (tool === 'pixelmatch') {
    if (!bothPng) res = { error: 'the built-in diff reads PNG only; use --tool pillow (uv) or odiff for other formats' };
    else {
      try { res = viaBuiltin(o.baseline, o.current, diffPng, o.threshold, o.countAA); impl = 'kit built-in (pixelmatch-compatible YIQ + anti-alias detection)'; }
      catch (e) { res = { error: `built-in diff failed: ${e.message}` }; }
    }
  } else if (tool === 'pillow') {
    res = await viaPillow(o.baseline, o.current, diffPng, o.threshold, o.countAA, env, o.ws);
    impl = `uv run --with ${uvPins(o.ws).join(' --with ')} (no anti-alias detection)`;
  } else {
    throw new UsageError(`--tool must be auto, odiff, builtin or pillow, got ${o.tool}`);
  }
  const visual = {
    schema: 1,
    baseline: relOrAbs(o.ws, o.baseline),
    current: relOrAbs(o.ws, o.current),
    diff_png: res.diffWritten ? relOrAbs(o.ws, diffPng) : null,
    tool,
    threshold: o.threshold,
    changed_pixels: res.error ? 0 : res.changed,
    total_pixels: res.error ? 0 : res.total,
    changed_pct: res.error ? 0 : Math.min(100, round(res.pct)),
    verdict: res.error ? 'error' : res.layout ? 'route_to_reviewer' : verdictFor(res.pct, o.maxPct),
    utc: utcIso(),
    max_changed_pct: o.maxPct,
    impl,
  };
  if (res.antialiased !== undefined) visual.antialiased_pixels = res.antialiased;
  if (typeof res.ssim === 'number') visual.ssim_mean = Math.round(res.ssim * 1e6) / 1e6;
  if (res.layout) visual.layout = { baseline: res.sizes?.[0] ?? null, current: res.sizes?.[1] ?? null };
  if (res.error) visual.error = res.error;
  const schema = loadKitSchema('checks.schema.json', 'game-qa-probes');
  const v = validate(schema, visual, { pointer: '#/$defs/visual' });
  if (!v.valid) throw new Error(`visual.json would be invalid: ${formatErrors(v.errors)}`);
  const outFile = path.join(o.out, 'visual.json');
  writeJsonAtomic(outFile, visual);
  return { visual, exit: res.error ? EXIT.INFRA : EXIT.OK, extra: { out: relOrAbs(o.ws, outFile) } };
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    out: { type: 'string' },
    threshold: { type: 'string' },
    'max-pct': { type: 'string' },
    tool: { type: 'string', default: 'auto' },
    'count-aa': { type: 'boolean', default: false },
    config: { type: 'string' },
  });
  if (positionals.length !== 2) throw new UsageError('usage: visual_diff.mjs <baseline.png> <current.png> [--out <dir>] [--threshold 0.1] [--json]');
  const [baseline, current] = positionals.map((p) => path.resolve(p));
  for (const f of [baseline, current]) if (!fs.existsSync(f) || !fs.statSync(f).isFile()) throw new UsageError(`image not found: ${f}`);
  const { ws } = roots(path.dirname(current));
  let pixelThreshold = 0.1;
  let maxPct = 0.5;
  const cfgFile = values.config ? path.resolve(values.config) : defaultProbesConfig(ws);
  if (cfgFile) {
    if (!fs.existsSync(cfgFile)) throw new UsageError(`--config file not found: ${cfgFile}`);
    const loaded = loadProbesConfig(cfgFile);
    if (!loaded.errors.length) {
      const t = effectiveThreshold(loaded.config, 'visual_diff_changed_pct');
      if (typeof t?.value === 'number') maxPct = t.value;
      if (typeof t?.params?.pixel_threshold === 'number') pixelThreshold = t.params.pixel_threshold;
    }
  }
  if (values.threshold !== undefined) {
    pixelThreshold = Number(values.threshold);
    if (!Number.isFinite(pixelThreshold) || pixelThreshold < 0 || pixelThreshold > 1) throw new UsageError('--threshold must be between 0 and 1');
  }
  if (values['max-pct'] !== undefined) {
    maxPct = Number(values['max-pct']);
    if (!Number.isFinite(maxPct) || maxPct < 0 || maxPct > 100) throw new UsageError('--max-pct must be between 0 and 100');
  }
  const out = values.out ? path.resolve(values.out) : defaultOut(current);
  const { visual, exit, extra } = await visualDiff({ baseline, current, out, threshold: pixelThreshold, maxPct, tool: values.tool, countAA: values['count-aa'], ws });
  const result = { ok: exit === EXIT.OK, exit, ...visual, ...extra };
  const text = visual.verdict === 'error'
    ? `ERROR: ${visual.error}`
    : `${visual.verdict}: ${visual.changed_pixels}/${visual.total_pixels} px changed (${visual.changed_pct}%, limit ${maxPct}%) via ${visual.tool}; wrote ${extra.out}`;
  emit(result, { json: values.json, text });
  return exit;
}

if (isMain(import.meta.url)) await runCli(main);

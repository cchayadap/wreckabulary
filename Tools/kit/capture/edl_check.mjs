// edl_check.mjs: check a trailer edit decision list (shots.json, CONTRACTS.md §7.7) before anything is recorded or cut.
//
//   node Tools/kit/capture/edl_check.mjs <shots.json> [--trailer gameplay|cinematic|teaser] [--check-files] [--json]
//
// shots.json: {schema:1, title, duration_s, trailer?, shots:[{id, source, in_s, out_s, kind, note}], music_cue:{path, lufs}}
// with kind one of gameplay | cinematic | ui | title. Result: {ok, duration_s, gameplay_share_pct, shots, failures[], …};
// exit 1 on any failure.
// Rules for the gameplay trailer (Steam slot 1; research_ship.md §3.3, §5.1): at least 60% gameplay [P], gameplay on
// screen within the first 5 s [practice, CLAIMED], 45–90 s long (warning only). All trailers: the declared duration
// matches the shot list within 0.5 s, shots are well formed, and a measured music cue sits at -16 ±1 LUFS (the kit's
// music target, audio_check.py). More than 3 cuts in any second is flagged as a photosensitivity risk (WCAG 2.3.1).
// --check-files resolves each source and the cue against W, then against U with a warning (a U-relative path).
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, CliError, UsageError, emit, isMain, parseCli, runCli } from '../lib/cli.mjs';
import { readText } from '../lib/jsonio.mjs';
import { findWorkspace, kitPaths } from '../lib/paths.mjs';

export const KINDS = ['gameplay', 'cinematic', 'ui', 'title'];
export const PROFILES = {
  gameplay: { min_gameplay_pct: 60, max_first_gameplay_s: 5, warn_len: [45, 90] },
  cinematic: { min_gameplay_pct: 0, max_first_gameplay_s: null, warn_len: [30, 120] },
  teaser: { min_gameplay_pct: 30, max_first_gameplay_s: null, warn_len: [10, 60] },
};
export const MUSIC_LUFS = [-17, -15];
const DURATION_TOLERANCE_S = 0.5;
const MIN_SHOT_S = 0.3;
const MAX_CUTS_PER_S = 3;

const num = (v) => typeof v === 'number' && Number.isFinite(v);
const r2 = (v) => Math.round(v * 100) / 100;

/**
 * --check-files lookup: W-relative first (the §1.1 rule), then U-relative with a warning (KNOTWELL has U = W/KNOTWELL,
 * and a cue under Assets/ is often written from the Unity project's point of view).
 * @param {string} p
 * @param {{base?: string, unity?: string}} opts
 * @param {string[]} warnings
 * @param {string} tag
 */
function mediaExists(p, opts, warnings, tag) {
  const base = opts.base || '.';
  if (fs.existsSync(path.resolve(base, p))) return true;
  if (opts.unity && !path.isAbsolute(p) && path.resolve(opts.unity) !== path.resolve(base) && fs.existsSync(path.resolve(opts.unity, p))) {
    warnings.push(`${tag}: '${p}' is Unity-project-relative; write it relative to the workspace`);
    return true;
  }
  return false;
}

/**
 * Check a parsed EDL.
 * @param {any} edl
 * @param {{trailer?: string, base?: string, unity?: string, checkFiles?: boolean}} [opts] base: W, the folder
 *   --check-files paths are relative to; unity: U, tried second
 */
export function checkEdl(edl, opts = {}) {
  const failures = [];
  const warnings = [];
  const out = { ok: false, title: '', trailer: '', duration_s: 0, declared_duration_s: null, gameplay_share_pct: 0, time_to_first_gameplay_s: null, shots: 0, kinds: {}, failures, warnings };
  if (!edl || typeof edl !== 'object' || Array.isArray(edl)) {
    failures.push('edl: not a JSON object');
    return out;
  }
  if (edl.schema !== 1) failures.push(`schema: expected 1, got ${JSON.stringify(edl.schema)}`);
  out.title = typeof edl.title === 'string' ? edl.title : '';
  if (!out.title) warnings.push('title: missing');
  const trailer = opts.trailer || edl.trailer || 'gameplay';
  if (!PROFILES[trailer]) failures.push(`trailer: '${trailer}' is not one of ${Object.keys(PROFILES).join(', ')}`);
  out.trailer = trailer;
  const prof = PROFILES[trailer] || PROFILES.gameplay;
  const shots = Array.isArray(edl.shots) ? edl.shots : [];
  if (!shots.length) failures.push('shots: the list is empty');
  out.shots = shots.length;
  const ids = new Set();
  let t = 0;
  const cutTimes = [];
  for (const [i, s] of shots.entries()) {
    const tag = `shots[${i}]${s && s.id ? ` (${s.id})` : ''}`;
    if (!s || typeof s !== 'object') { failures.push(`${tag}: not an object`); continue; }
    if (typeof s.id !== 'string' || !s.id) failures.push(`${tag}: id missing`);
    else if (ids.has(s.id)) failures.push(`${tag}: duplicate id`);
    else ids.add(s.id);
    if (!KINDS.includes(s.kind)) failures.push(`${tag}: kind '${s.kind}' is not one of ${KINDS.join(', ')}`);
    if (typeof s.source !== 'string' || !s.source) failures.push(`${tag}: source missing`);
    if (!num(s.in_s) || !num(s.out_s) || s.in_s < 0 || s.out_s <= s.in_s) {
      failures.push(`${tag}: needs 0 <= in_s < out_s (got ${s.in_s} → ${s.out_s})`);
      continue;
    }
    const len = s.out_s - s.in_s;
    if (len < MIN_SHOT_S) warnings.push(`${tag}: ${r2(len)} s is too short to read`);
    if (s.kind === 'gameplay' && out.time_to_first_gameplay_s === null) out.time_to_first_gameplay_s = r2(t);
    if (KINDS.includes(s.kind)) out.kinds[s.kind] = r2((out.kinds[s.kind] || 0) + len);
    if (opts.checkFiles && typeof s.source === 'string' && s.source && !mediaExists(s.source, opts, warnings, tag)) {
      failures.push(`${tag}: source not found: ${s.source}`);
    }
    if (i > 0) cutTimes.push(t);
    t += len;
  }
  out.duration_s = r2(t);
  if (num(edl.duration_s)) {
    out.declared_duration_s = edl.duration_s;
    if (Math.abs(edl.duration_s - t) > DURATION_TOLERANCE_S) failures.push(`duration_s: declared ${edl.duration_s} s but the shots add up to ${r2(t)} s`);
  } else warnings.push('duration_s: not declared');
  const gp = out.kinds.gameplay || 0;
  out.gameplay_share_pct = t > 0 ? r2((gp / t) * 100) : 0;
  if (gp / Math.max(t, 1e-9) * 100 < prof.min_gameplay_pct) failures.push(`gameplay_share: ${out.gameplay_share_pct}% below ${prof.min_gameplay_pct}% for a ${trailer} trailer`);
  if (prof.max_first_gameplay_s !== null && shots.length) {
    if (out.time_to_first_gameplay_s === null) failures.push('first_gameplay: no gameplay shot at all');
    else if (out.time_to_first_gameplay_s > prof.max_first_gameplay_s) failures.push(`first_gameplay: gameplay starts at ${out.time_to_first_gameplay_s} s; show it within ${prof.max_first_gameplay_s} s`);
  }
  if (t > 0 && (t < prof.warn_len[0] || t > prof.warn_len[1])) warnings.push(`length: ${r2(t)} s is outside the usual ${prof.warn_len[0]}–${prof.warn_len[1]} s for a ${trailer} trailer`);
  for (let i = 0; i < cutTimes.length; i++) {
    let j = i;
    while (j + 1 < cutTimes.length && cutTimes[j + 1] - cutTimes[i] < 1) j++;
    if (j - i + 1 > MAX_CUTS_PER_S) { warnings.push(`cuts: ${j - i + 1} cuts within 1 s from ${r2(cutTimes[i])} s (photosensitivity risk)`); break; }
  }
  const cue = edl.music_cue;
  if (cue !== undefined && cue !== null) {
    if (typeof cue !== 'object' || typeof cue.path !== 'string' || !cue.path) failures.push('music_cue: needs a path');
    else if (opts.checkFiles && !mediaExists(cue.path, opts, warnings, 'music_cue')) failures.push(`music_cue: not found: ${cue.path}`);
    if (cue && typeof cue === 'object') {
      if (!num(cue.lufs)) warnings.push('music_cue: lufs not measured (run Tools/kit/audio/audio_check.py --class music)');
      else if (cue.lufs < MUSIC_LUFS[0] || cue.lufs > MUSIC_LUFS[1]) failures.push(`music_cue: ${cue.lufs} LUFS outside [${MUSIC_LUFS[0]}, ${MUSIC_LUFS[1]}]`);
    }
  } else warnings.push('music_cue: none');
  out.ok = failures.length === 0;
  return out;
}

export async function main(argv) {
  const { values, positionals } = parseCli(argv, { trailer: { type: 'string' }, 'check-files': { type: 'boolean', default: false } });
  if (positionals.length !== 1) throw new UsageError('usage: edl_check.mjs <shots.json> [--trailer gameplay|cinematic|teaser] [--check-files] [--json]');
  const file = positionals[0];
  if (!fs.existsSync(file)) throw new UsageError(`not found: ${file}`);
  let edl;
  try {
    edl = JSON.parse(readText(file));
  } catch (e) {
    edl = undefined;
    const res = { ok: false, duration_s: 0, gameplay_share_pct: 0, shots: 0, failures: [`json: ${e.message}`], warnings: [] };
    emit(res, { json: values.json, text: ['FAIL', ...res.failures] });
    return EXIT.FAIL;
  }
  const ws = findWorkspace();
  const base = ws ?? process.cwd();
  let unity = base;
  if (ws) {
    try { unity = kitPaths(ws).unity; } catch (e) { throw new CliError(`kit.json: ${e.message}`, EXIT.INFRA); }
  }
  const res = checkEdl(edl, { trailer: values.trailer, base, unity, checkFiles: values['check-files'] });
  emit(res, {
    json: values.json,
    text: [
      `${res.ok ? 'PASS' : 'FAIL'} ${res.title || file}: ${res.duration_s} s, ${res.shots} shots, ${res.gameplay_share_pct}% gameplay, first gameplay at ${res.time_to_first_gameplay_s ?? '-'} s`,
      ...res.failures.map((f) => `FAIL ${f}`),
      ...res.warnings.map((w) => `warn ${w}`),
    ],
  });
  return res.ok ? EXIT.OK : EXIT.FAIL;
}

if (isMain(import.meta.url)) await runCli(main);

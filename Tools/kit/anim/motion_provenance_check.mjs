#!/usr/bin/env node
// motion_provenance_check.mjs: the motion sourcing denylist and provenance rules as a check (CONTRACTS.md §4.5;
// research_motion.md §8-§9 and its fact-check; ARCHITECTURE §3.8, critic C-18). Installed as Tools/kit/anim/.
//   node Tools/kit/anim/motion_provenance_check.mjs [--csv Docs/licenses.csv] [--motion Docs/motion_sources.csv] [--json]
// Additive flags: --release <Docs/ship/release.json> (public_repo), --project <W>.
// Exit 1 on any violation. The three model rules (noncommercial_rig, noncommercial_motion, unconfirmed_licence) look
// only where a model is named: motion_sources.csv rows, and licenses.csv rows for generated assets or covering a motion
// clip (generator_model and provider). Every other rule applies to every row. Rule ids:
//   denylist_host             the Hugging Face Mixamo mirror (huggingface.co/datasets/Linzhan/*), a research mirror
//                             with no licence chain for a shipped game
//   hunyuan_motion            fal-ai/hunyuan-motion* and HY-Motion output (Tencent licence excludes the EU, UK and KR)
//   noncommercial_rig         RigAnything (non-commercial licence)
//   noncommercial_motion      GVHMR, SMPL/SMPL-H/SMPL-X and AMASS/HumanML3D/KIT-ML derived output (MDM, MotionGPT,
//                             MoMask, T2M-GPT, WHAM, 4D-Humans), Kimodo-SMPLX (R&D licence), raw BONES-SEED data
//   freemium_motion           DeepMotion or QuickMagic free/freemium output (personal, non-commercial)
//   unconfirmed_licence       Plask output (no commercial terms published)
//   nc_licence                a CC-BY-NC or other -NC licence id
//   free_tier_vendor_owned    Tripo free-tier output (Tripo keeps all rights)
//   free_tier_no_attribution  generated on a free plan (Meshy, Uthana: CC BY 4.0) with no attribution text
//   mixamo_not_own_account    Mixamo-Adobe rows that were not downloaded from mixamo.com under the owner's account
//   quaternius_licence_date   Quaternius CC0 claimed for a download on or after 2026-08-28, or QAL without embedded_only
//   redistribution_public_repo  raw clips that may not be redistributed (forbidden, embedded_only) in a public repo
//   motion_without_licence    a motion_sources.csv clip with no licenses.csv row covering its path
//   licence_mismatch          motion row and licence row disagree on licence_id
//   unknown_source            a motion row with no source_kind or licence id (or licence id Unknown)
//   sha_mismatch              the clip on disk no longer matches the recorded sha256
//   csv_invalid               a ledger that does not parse or has the wrong header
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, UsageError, emit, isMain, note, parseCli, runCli } from '../lib/cli.mjs';
import { parseCsv } from '../lib/csv.mjs';
import { findWorkspace } from '../lib/paths.mjs';
import { readText } from '../lib/jsonio.mjs';
import { sha256File } from '../lib/hash.mjs';

export const MOTION_HEADER = Object.freeze(['clip_path', 'body', 'source_kind', 'source_url', 'licence_id', 'route', 'fps', 'loop', 'retrieved_at_utc', 'sha256', 'notes']);
const QAL_EFFECTIVE = Date.parse('2026-08-28T00:00:00Z');

/**
 * Source rules [rule, regex, message], matched on every row of both ledgers, in the identifying fields (never notes):
 * these name hosts and services specific enough that a match is never an accident.
 */
export const HOST_RULES = Object.freeze([
  ['denylist_host', /huggingface\.co\/datasets\/Linzhan\b|\bMixamo-Animations-Characters\b/i, 'Hugging Face Mixamo mirror: "for research use", no Adobe licence reaches the downloader; take clips through mixamo_intake.py instead'],
  ['hunyuan_motion', /fal-ai\/hunyuan-motion|\bHY-?Motion\b|Tencent-Hunyuan\/HY-Motion|\bhunyuan[ _-]?motion\b/i, 'HY-Motion output may not be used or displayed in the EU, UK or South Korea: never ship it in a globally sold game'],
]);

/**
 * Model and dataset rules [rule, regex, message]. Their tokens are short (MDM, WHAM, SMPL), so they are matched only
 * where a model is named: on motion_sources.csv rows (route, source_url, licence_id), and on licences.csv rows for
 * generated assets or rows covering a motion clip, in generator_model and provider only. A CC0 sound effect whose URL
 * says "wham" is not a motion model.
 */
export const MODEL_RULES = Object.freeze([
  ['noncommercial_rig', /\bRig-?Anything\b/i, 'RigAnything is non-commercial: re-rig through another route before ship'],
  ['noncommercial_motion', /\bGVHMR\b|\bSMPL(?:-?[HX])?\b|\bAMASS\b|\bHumanML3D\b|\bKIT-ML\b|motion-diffusion-model|\bMDM\b|\bMotionGPT\b|\bMoMask\b|momask-codes|\bT2M-GPT\b|\bKimodo-SMPLX\b|\bWHAM\b|\b4D-?Humans\b|\bBONES-SEED\b|bones-studio\/seed/i, 'non-commercial model or data (research licence): research and reference only'],
  ['unconfirmed_licence', /\bplask\b/i, 'Plask output: no commercial terms are published; do not ship until a human confirms them'],
]);

/** Every rule, host rules first (kept for callers of the earlier single list). */
export const DENY = Object.freeze([...HOST_RULES, ...MODEL_RULES]);

const ID_FIELDS = ['source_url', 'generator_model', 'provider', 'licence_id', 'route', 'account_or_plan', 'source_kind'];
const MODEL_FIELDS_LICENCE = ['generator_model', 'provider'];
const MODEL_FIELDS_MOTION = ['route', 'source_url', 'licence_id'];
const FREE_PLAN = /^(free|freemium|trial|starter_free|free_tier)$/i;
const RAW_MOTION = /\.(fbx|bvh|glb|gltf|anim|npz|blend|zip)$|\/$/i;

function readLedger(file, header, label, violations, warnings, explicit) {
  if (!fs.existsSync(file)) {
    if (explicit) throw new UsageError(`${label} not found: ${file}`);
    warnings.push({ rule: 'ledger_missing', path: label, message: `${label} does not exist yet; nothing to check there` });
    return [];
  }
  let parsed;
  try {
    parsed = parseCsv(readText(file));
  } catch (e) {
    violations.push({ rule: 'csv_invalid', path: label, message: e.message });
    return [];
  }
  if (header && parsed.header.join(',') !== header.join(',')) {
    violations.push({ rule: 'csv_invalid', path: label, message: `header is "${parsed.header.join(',')}", expected "${header.join(',')}"` });
    return [];
  }
  return parsed.rows.map((r, i) => ({ ...r, __line: i + 2 }));
}

const host = (u) => {
  try { return new URL(u).hostname.toLowerCase(); } catch { return ''; }
};

/**
 * Check both ledgers. Pure apart from reading clip files for sha256 (ws given).
 * @param {{licences: object[], motion: object[], release: object|null, ws: string|null, labels: {csv: string, motion: string}}} o
 * @returns {{violations: object[], warnings: object[]}}
 */
export function checkProvenance(o) {
  const violations = [];
  const warnings = [];
  const add = (rule, row, label, message) => violations.push({ rule, path: row.clip_path ?? row.path ?? '', message, source: `${label}:${row.__line}` });
  const publicRepo = o.release?.public_repo === true;
  const clips = o.motion.map((m) => String(m.clip_path ?? '')).filter(Boolean);
  const coversClip = (p) => clips.some((c) => c === p || (p.endsWith('/') && c.startsWith(p)));

  const common = (row, label, isMotionLedger) => {
    const ident = ID_FIELDS.map((k) => row[k] ?? '').join(' ');
    for (const [rule, re, msg] of HOST_RULES) if (re.test(ident)) add(rule, row, label, msg);
    const generated = row.ai_generated === 'true' || row.source_kind === 'generated';
    const modelFields = isMotionLedger ? MODEL_FIELDS_MOTION : generated || coversClip(String(row.path ?? '')) ? MODEL_FIELDS_LICENCE : [];
    const named = modelFields.map((k) => row[k] ?? '').join(' ');
    if (named.trim()) for (const [rule, re, msg] of MODEL_RULES) if (re.test(named)) add(rule, row, label, msg);
    const lic = String(row.licence_id ?? '');
    if (/(^|[-_ ])NC([-_ .]|$)/i.test(lic)) add('nc_licence', row, label, `${lic} forbids commercial use`);
    const prov = `${row.provider ?? ''} ${row.source_url ?? ''} ${row.generator_model ?? ''}`.toLowerCase();
    const plan = String(row.account_or_plan ?? '');
    if (/deepmotion|quickmagic/.test(prov) && (FREE_PLAN.test(plan) || !plan)) add('freemium_motion', row, label, 'DeepMotion and QuickMagic free plans are personal, non-commercial: record the paid plan used, or regenerate on one');
    if (/tripo/.test(prov) && FREE_PLAN.test(plan)) add('free_tier_vendor_owned', row, label, 'Tripo free-tier output belongs to Tripo: regenerate on a paid plan');
    if (generated && FREE_PLAN.test(plan) && !/tripo|deepmotion|quickmagic/.test(prov) && !String(row.attribution_text ?? '').trim()) {
      add('free_tier_no_attribution', row, label, 'free-plan generations (Meshy, Uthana) are CC BY 4.0: add the attribution text or regenerate on a paid plan');
    }
    const isMixamo = lic === 'Mixamo-Adobe' || /mixamo/.test(prov);
    if (isMixamo && !HOST_RULES[0][1].test(ident)) {
      const h = host(row.source_url ?? '');
      if (row.source_kind !== 'own_account_download' || !(h === 'mixamo.com' || h.endsWith('.mixamo.com'))) {
        add('mixamo_not_own_account', row, label, 'Mixamo clips are licensed only to the Adobe ID that downloaded them from mixamo.com: record source_kind=own_account_download and a mixamo.com source_url');
      }
    }
    const quat = /quaternius/i.test(`${row.source_url ?? ''} ${row.author ?? ''} ${row.provider ?? ''} ${row.notes ?? ''}`);
    if (quat) {
      const t = Date.parse(row.retrieved_at_utc ?? '');
      if (lic === 'CC0-1.0' && (Number.isNaN(t) || t >= QAL_EFFECTIVE)) add('quaternius_licence_date', row, label, 'Quaternius assets obtained on or after 2026-08-28 are QAL-1.0, not CC0 (the licence in force when obtained governs)');
      if (lic === 'QAL-1.0' && row.redistribution !== undefined && row.redistribution !== 'embedded_only') add('quaternius_licence_date', row, label, 'QAL-1.0 forbids redistributing the assets themselves: redistribution must be embedded_only');
    }
    const p = String(row.clip_path ?? row.path ?? '');
    const restricted = row.redistribution === 'forbidden' || row.redistribution === 'embedded_only' || lic === 'Mixamo-Adobe' || lic === 'QAL-1.0';
    if (publicRepo && restricted && RAW_MOTION.test(p)) add('redistribution_public_repo', row, label, 'raw clips that may not be redistributed sit in a public repository: keep them in a private repo or LFS store');
  };

  for (const r of o.licences) common(r, o.labels.csv, false);
  const covering = (clip) => {
    let best = null;
    for (const l of o.licences) {
      const lp = String(l.path ?? '');
      if (lp === clip || (lp.endsWith('/') && clip.startsWith(lp))) if (!best || lp.length > best.path.length) best = l;
    }
    return best;
  };
  for (const m of o.motion) {
    common(m, o.labels.motion, true);
    const clip = String(m.clip_path ?? '');
    if (!m.source_kind || !m.licence_id || /^unknown$/i.test(m.licence_id)) add('unknown_source', m, o.labels.motion, 'every clip needs a source_kind and a licence id');
    const lic = covering(clip);
    if (!lic) add('motion_without_licence', m, o.labels.motion, `no ${o.labels.csv} row covers ${clip}: add one with license_row.mjs`);
    else if (m.licence_id && lic.licence_id !== m.licence_id) add('licence_mismatch', m, o.labels.motion, `motion row says ${m.licence_id}, ${o.labels.csv} says ${lic.licence_id}`);
    if (o.ws && m.sha256 && clip) {
      const abs = path.join(o.ws, clip);
      if (fs.existsSync(abs) && fs.statSync(abs).isFile()) {
        if (sha256File(abs) !== m.sha256) add('sha_mismatch', m, o.labels.motion, 'the clip changed after its provenance row was written: re-check its source and update the row');
      } else {
        warnings.push({ rule: 'file_missing', path: clip, message: 'clip listed but not on disk', source: `${o.labels.motion}:${m.__line}` });
      }
    }
  }
  return { violations, warnings };
}

export async function main(argv) {
  const { values } = parseCli(argv, {
    csv: { type: 'string' },
    motion: { type: 'string' },
    release: { type: 'string' },
    project: { type: 'string' },
  });
  const ws = values.project ? path.resolve(values.project) : findWorkspace(process.cwd());
  const base = ws ?? process.cwd();
  const resolve = (p, dflt) => path.resolve(base, p ?? dflt);
  const csvFile = resolve(values.csv, 'Docs/licenses.csv');
  const motionFile = resolve(values.motion, 'Docs/motion_sources.csv');
  const releaseFile = resolve(values.release, 'Docs/ship/release.json');
  const label = (f) => {
    const r = path.relative(base, f).replace(/\\/g, '/');
    return r.startsWith('..') ? f.replace(/\\/g, '/') : r;
  };
  const violations = [];
  const warnings = [];
  const licences = readLedger(csvFile, null, label(csvFile), violations, warnings, Boolean(values.csv));
  const motion = readLedger(motionFile, [...MOTION_HEADER], label(motionFile), violations, warnings, Boolean(values.motion));
  let release = null;
  if (fs.existsSync(releaseFile)) {
    try { release = JSON.parse(readText(releaseFile)); } catch (e) { violations.push({ rule: 'csv_invalid', path: label(releaseFile), message: `release.json: ${e.message}` }); }
  } else if (values.release) {
    throw new UsageError(`--release not found: ${values.release}`);
  }
  const res = checkProvenance({ licences, motion, release, ws: base, labels: { csv: label(csvFile), motion: label(motionFile) } });
  violations.push(...res.violations);
  warnings.push(...res.warnings);
  const ok = violations.length === 0;
  const result = {
    ok,
    exit: ok ? EXIT.OK : EXIT.FAIL,
    checked: { licence_rows: licences.length, motion_rows: motion.length, public_repo: release?.public_repo === true },
    violations,
    warnings,
  };
  note(`motion_provenance_check: ${licences.length} licence row(s), ${motion.length} motion row(s)`);
  emit(result, {
    json: values.json,
    text: [
      ...violations.map((v) => `FAIL ${v.rule} ${v.path}${v.source ? ` (${v.source})` : ''}: ${v.message}`),
      ...warnings.map((w) => `WARN ${w.rule} ${w.path}: ${w.message}`),
      `${ok ? 'PASS' : 'FAIL'}: ${violations.length} violation(s), ${warnings.length} warning(s)`,
    ],
  });
  return result.exit;
}

if (isMain(import.meta.url)) await runCli(main);

// Licence policy rules for licenses_check.mjs and build_join.mjs (ARCHITECTURE §3.13 validator failures, Critique C-4;
// research_ship.md §4.2-§4.4; research_generation_apis.md §4.5 and §5). Facts verified 2026-09-29 unless noted.
// Severity: a rule that bites a shipping row is FAIL; the same finding on a row that does not ship is WARN ("never ship
// this"). Ledger integrity (schema, duplicate path) is always FAIL.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { sha256File } from '../../lib/hash.mjs';
import { readJsonOr, readText } from '../../lib/jsonio.mjs';
import { normRowPath, pathKey, rowErrors, resolveRowFile } from './ledger.mjs';

/** Quaternius Asset License v1.0 is dated 28 Aug 2026; the version in effect when an asset was obtained governs it. */
export const QAL_EFFECTIVE_UTC = '2026-08-28T00:00:00Z';
/** FreePD has been offline since 2025 (research_generation_apis #58); rows retrieved after this cannot be genuine. */
export const FREEPD_OFFLINE_UTC = '2025-01-01T00:00:00Z';

/** Every rule id licenses_check can emit (CONTRACTS.md §4.9, plus the additive ids at the end). */
export const RULE_IDS = Object.freeze([
  'denylist_host', 'nc_licence', 'share_alike', 'gpl_art', 'sampling_plus', 'cc_by_no_attribution', 'cc_by_drm',
  'research_only_endpoint', 'hunyuan_family', 'elevenlabs_music_platforms', 'free_tier_plan', 'meshy_free_attribution',
  'higgsfield_watermark', 'quaternius_licence_date', 'qal_redistribution', 'pixabay_cc0', 'freepd_source', 'sonniss_terms',
  'mia_rig_route', 'noncommercial_motion', 'tool_binary', 'unknown_commercial', 'redistribution_public_repo', 'sha_mismatch',
  'anklebreaker_credit', 'coplaydev_notice',
  // additive (qa-ship unit)
  'schema', 'duplicate_path', 'provider_ship_blocked', 'release_missing', 'missing_file', 'licence_not_allowlisted',
  'embedded_only_raw_file', 'unlicensed_file',
]);

// ----- licence id families -----
export const isCc0 = (id) => /^CC0\b/i.test(id);
export const isCcBy = (id) => /^CC[-_ ]?BY\b/i.test(id);
export const isNc = (id) => /^CC[-_ ]?BY[-_ ]?NC\b/i.test(id);
export const isSa = (id) => /^CC[-_ ]?BY(?:[-_ ]?NC)?[-_ ]?SA\b/i.test(id);
export const isOgaBy = (id) => /^OGA[-_ ]?BY\b/i.test(id);
export const isGpl = (id) => /^(A|L)?GPL\b/i.test(id);
export const isLgpl = (id) => /^LGPL\b/i.test(id);
export const isSamplingPlus = (id) => /sampling[-_ ]?(\+|plus)/i.test(id);
export const isQal = (id) => /^QAL\b/i.test(id);
/** Licences whose text or credit must travel with the game. */
export const needsAttribution = (id) => isCcBy(id) || isOgaBy(id);

const KNOWN_IDS = [
  /^CC0-1\.0$/, /^CC-BY-(2\.0|2\.5|3\.0|4\.0)$/, /^OGA-BY-(3\.0|4\.0)$/, /^MIT$/, /^Apache-2\.0$/, /^BSD-(2|3)-Clause$/, /^Zlib$/,
  /^OFL-1\.1$/, /^Unlicense$/, /^MPL-2\.0$/, /^QAL-1\.0$/, /^Pixabay-Content-License$/, /^Sonniss-GDC$/, /^Mixamo-Adobe$/,
  /^Own-Work$/, /^AI-generated:[^:]+:.+$/, /^Unity-(Asset-Store-EULA|Companion-License)$/, /^Steamworks-SDK$/, /^Unknown$/,
];

// ----- provider and plan helpers -----
const HOSTS = [
  ['tripo', /tripo3d\.ai/i], ['recraft', /recraft\.ai/i], ['scenario', /scenario\.com/i], ['elevenlabs', /elevenlabs\.io/i],
  ['blockade', /blockadelabs\.com/i], ['meshy', /meshy\.ai/i], ['higgsfield', /higgsfield\.ai/i], ['fal', /(^|\/\/|\.)fal\.(ai|run)/i],
  ['quaternius', /quaternius\.com/i], ['pixabay', /pixabay\.com/i], ['freepd', /freepd\.com/i], ['sonniss', /sonniss\.com/i],
];

/** Lower-case provider id: the provider column, else AI-generated:<provider>:…, else the source_url host. @param {Record<string,string>} row */
export function providerOf(row) {
  const p = String(row.provider || '').trim().toLowerCase();
  if (p) return p.replace(/[\s.]+/g, '').replace(/^fal(ai)?$/, 'fal').replace(/^eleven(labs)?$/, 'elevenlabs');
  const m = /^AI-generated:([^:]+):/i.exec(row.licence_id || '');
  if (m) return m[1].toLowerCase();
  for (const [name, re] of HOSTS) if (re.test(row.source_url || '')) return name;
  return '';
}

/** Normalised plan: lower case, separators to "_". @param {string} plan */
export function normPlan(plan) {
  return String(plan || '').trim().toLowerCase().replace(/[\s-]+/g, '_').replace(/_?plan$/, '');
}
const isFreePlan = (p) => /^(free|trial)(_|$)/.test(p);

/** Free or low tiers whose outputs never ship (research_generation_apis §5; Blockade CLAIMED). */
export const FREE_TIER_BLOCKS = Object.freeze({
  tripo: { test: (p) => isFreePlan(p) || p === 'basic', why: 'Tripo keeps all rights in Free-User outputs ("Public Models · Non-Commercial Use")' },
  recraft: { test: isFreePlan, why: 'Recraft owns Free Tier outputs and forbids their commercial use' },
  scenario: { test: isFreePlan, why: 'Scenario Free outputs are for personal and evaluation use only' },
  elevenlabs: { test: isFreePlan, why: 'ElevenLabs Free carries no commercial licence' },
  blockade: { test: (p) => isFreePlan(p) || p === 'essential', why: 'Blockade Labs free/Essential outputs are not cleared for commercial use (CLAIMED)' },
});

/** providers.json endpoint entry for a row (provider + generator_model, or the licence id), or null. */
export function endpointOf(providers, row) {
  if (!providers || !Array.isArray(providers.endpoints)) return null;
  const prov = providerOf(row);
  let ep = String(row.generator_model || '').trim();
  const m = /^AI-generated:([^:]+):(.+)$/i.exec(row.licence_id || '');
  if (!ep && m) ep = m[2];
  if (!ep) return null;
  return providers.endpoints.find((e) => e.endpoint === ep && (!prov || e.provider === prov || e.id === `${prov}:${ep}`)) || null;
}

const hay = (row, ...cols) => cols.map((c) => String(row[c] || '')).join(' ');
const isElevenMusic = (row) => /(^|\/)music_v\d/i.test(row.generator_model || '') || (/eleven\s*labs|elevenlabs/i.test(hay(row, 'provider', 'generator_model', 'source_url')) && /music/i.test(row.generator_model || ''));
const isHunyuan = (row) => /hunyuan/i.test(hay(row, 'generator_model', 'source_url', 'licence_id'));
const isDenylisted = (row) => /huggingface\.co\/datasets\/linzhan\//i.test(row.source_url || '') || /fal-ai\/hunyuan-motion/i.test(hay(row, 'generator_model', 'source_url'));
const isNoncommercialMotion = (row) => /\b(riganything|gvhmr)\b|\bsmpl(-?x|-?h)?\b/i.test(hay(row, 'generator_model', 'source_url', 'licence_id', 'notes'));
const TOOL_BINARY = /^(ffmpeg|ffprobe|ffplay|gifski)([-_.].*)?$|^unity\.pipeline/i;
/** True for a file name that is a dev-only tool binary (ffmpeg GPL, gifski AGPL, the Pipeline runtime). @param {string} name */
export const isToolBinaryName = (name) => TOOL_BINARY.test(path.posix.basename(String(name).replace(/\\/g, '/')));

function gitIgnoredFn(ws) {
  const cache = new Map();
  return (rel) => {
    if (cache.has(rel)) return cache.get(rel);
    let ignored = false;
    try {
      const r = spawnSync('git', ['check-ignore', '-q', '--', rel], { cwd: ws, windowsHide: true, timeout: 10000 });
      ignored = r.status === 0;
    } catch { ignored = false; }
    cache.set(rel, ignored);
    return ignored;
  };
}

/**
 * Index ArtSource/_gen/<asset>/asset.json rig routes: pathKey(ledger path) → {asset_id, rig_route}.
 * @param {string} ws
 */
export function assetIndex(ws) {
  const idx = new Map();
  const root = path.join(ws, 'ArtSource', '_gen');
  let ents = [];
  try { ents = fs.readdirSync(root, { withFileTypes: true }); } catch { return idx; }
  for (const e of ents) {
    if (!e.isDirectory()) continue;
    const doc = readJsonOr(path.join(root, e.name, 'asset.json'), null, { onInvalid: 'fallback' });
    if (!doc || typeof doc !== 'object') continue;
    const info = { asset_id: doc.asset_id || e.name, rig_route: doc.rig_route || null, class: doc.class || null, ai_outputs_at_accept: doc.ai_outputs_at_accept || null };
    for (const p of Array.isArray(doc.licence_rows) ? doc.licence_rows : []) idx.set(pathKey(p), info);
    for (const a of Array.isArray(doc.accepted) ? doc.accepted : []) if (a?.path) idx.set(pathKey(a.path), info);
  }
  return idx;
}

/**
 * Evaluate the ledger rows in scope.
 * @param {Record<string,string>[]} rows
 * @param {{ws: string, unity: string, release: object|null, releaseMissing?: boolean, providers: object|null,
 *   shipSet?: Set<object>|null, checkSha?: boolean, assets?: Map<string, object>, allRows?: Record<string,string>[]}} ctx
 *   shipSet: rows that ship (build join); without it a row ships when ships == "true"
 * @returns {{rule: string, path: string, severity: 'FAIL'|'WARN', message: string}[]}
 */
export function checkRows(rows, ctx) {
  const out = [];
  const push = (rule, row, severity, message) => out.push({ rule, path: row ? normRowPath(row.path) : '', severity, message });
  const release = ctx.release || null;
  const platforms = Array.isArray(release?.platforms) ? release.platforms : null;
  const assets = ctx.assets || new Map();
  const ignored = release?.public_repo ? gitIgnoredFn(ctx.ws) : null;
  const seen = new Map();
  let shippingCcBy = false;

  for (const row of rows) {
    const rp = normRowPath(row.path);
    const lid = String(row.licence_id || '').trim();
    const shipping = ctx.shipSet ? ctx.shipSet.has(row) : row.ships === 'true';
    const sev = shipping ? 'FAIL' : 'WARN';
    const never = shipping ? '' : ' (the row is not in the ship scope: it must never ship)';
    const errs = rowErrors(row);
    if (errs.length) push('schema', row, 'FAIL', `ledger line ${row.__line ?? '?'} fails licenses.schema.json: ${errs.slice(0, 3).join('; ')}`);
    const key = pathKey(rp);
    if (seen.has(key)) push('duplicate_path', row, 'FAIL', `path appears on ledger lines ${seen.get(key)} and ${row.__line ?? '?'}; license_row.mjs add --replace keeps one row per path`);
    else seen.set(key, row.__line ?? '?');

    const prov = providerOf(row);
    const plan = normPlan(row.account_or_plan);

    if (isDenylisted(row)) push('denylist_host', row, sev, `source ${row.source_url || row.generator_model} is denylisted (the Hugging Face Mixamo mirror is "for research use"; HY-Motion output is barred in the EU, UK and South Korea): BLOCKED for ship; re-download through the owner's own account or replace${never}`);
    if (isNc(lid)) push('nc_licence', row, sev, `${lid} forbids commercial use: BLOCKED${never}`);
    if (isSa(lid)) push('share_alike', row, sev, `${lid} is share-alike and would spread to the game: BLOCKED; use CC0, CC-BY or OGA-BY${never}`);
    if (isGpl(lid)) {
      const code = row.source_kind === 'code_dependency';
      const fontException = /font[-_ ]?exception|-FE$/i.test(lid);
      if (code && isLgpl(lid)) push('gpl_art', row, 'WARN', `${lid} code: allowed only as a dynamically linked, replaceable library with its licence text shipped`);
      else if (fontException) push('gpl_art', row, 'WARN', `${lid}: GPL font with the font exception; ship the licence text`);
      else push('gpl_art', row, sev, `${lid} ${code ? 'code' : 'art, audio or font'} would put the game under the GPL: BLOCKED${never}`);
    }
    if (isSamplingPlus(lid)) push('sampling_plus', row, sev, `${lid} is the retired Freesound Sampling+ licence: BLOCKED; pick a CC0 or CC-BY sound${never}`);

    const meshyFree = prov === 'meshy' && isFreePlan(plan);
    const ccByLike = isCcBy(lid) || meshyFree;
    if ((isCcBy(lid) || isOgaBy(lid)) && !String(row.attribution_text || '').trim()) {
      push('cc_by_no_attribution', row, sev, `${lid} needs attribution text (title, author, source, licence) in attribution_text`);
    }
    if (ccByLike && shipping) shippingCcBy = true;
    if (ccByLike && release?.steam_drm_wrapper === true) {
      push('cc_by_drm', row, sev, `${meshyFree ? 'Meshy free output (CC BY 4.0)' : lid} cannot ship inside the SteamStub DRM wrapper (CC BY 4.0 §2(a)(5)(B)); turn steam_drm_wrapper off or replace it with CC0 or OGA-BY${never}`);
    }

    const ep = endpointOf(ctx.providers, row);
    if (ep?.badge === 'research_only') push('research_only_endpoint', row, sev, `${ep.provider}:${ep.endpoint} carries fal's "Research only" badge (non-commercial)${never}`);
    if (isHunyuan(row)) push('hunyuan_family', row, sev, `Hunyuan-family output (${row.generator_model || row.source_url || lid}) is blocked for ship until H11 decides: the Tencent Hunyuan licence excludes the EU, UK and South Korea (U-30)${never}`);
    else if (ep?.ship?.blocked) push('provider_ship_blocked', row, sev, `providers.json blocks ${ep.id} for ship: ${ep.ship.reason || 'no reason given'}${ep.ship.pending ? ` (pending ${ep.ship.pending})` : ''}${never}`);

    if (isElevenMusic(row)) {
      if (!platforms) push('elevenlabs_music_platforms', row, 'WARN', 'ElevenLabs Music output: Docs/ship/release.json is missing, so the platform count cannot be checked (more than 1 platform needs the full Enterprise Music plan)');
      else if (platforms.length > 1 && plan !== 'enterprise_music') push('elevenlabs_music_platforms', row, sev, `ElevenLabs Music on ${platforms.length} platforms (${platforms.join(', ')}) needs the full Enterprise Music plan; plan is "${row.account_or_plan || 'unknown'}" (Enterprise Music Lite keeps the Studio Games exclusion)${never}`);
    }

    const freeRule = FREE_TIER_BLOCKS[prov];
    const provEntry = ctx.providers?.providers?.[prov];
    if (freeRule && freeRule.test(plan)) push('free_tier_plan', row, sev, `${prov} plan "${row.account_or_plan}": ${freeRule.why}: BLOCKED${never}`);
    else if (plan && Array.isArray(provEntry?.blocked_plans) && provEntry.blocked_plans.map(normPlan).includes(plan)) push('free_tier_plan', row, sev, `providers.json lists ${prov} plan "${row.account_or_plan}" as blocked for ship${never}`);
    if (meshyFree && !String(row.attribution_text || '').trim()) push('meshy_free_attribution', row, sev, 'Meshy free-plan output is CC BY 4.0: it ships only with attribution text crediting Meshy (and never inside the DRM wrapper)');
    else if (plan && prov !== 'meshy' && Array.isArray(provEntry?.attribution_plans) && provEntry.attribution_plans.map(normPlan).includes(plan) && !String(row.attribution_text || '').trim()) {
      push('cc_by_no_attribution', row, sev, `providers.json says ${prov} plan "${row.account_or_plan}" ships only with attribution`);
    }
    if (prov === 'higgsfield') {
      const wm = Array.isArray(provEntry?.watermark_plans) && provEntry.watermark_plans.map(normPlan).includes(plan);
      if (wm || /watermark/i.test(`${row.account_or_plan} ${row.notes}`)) push('higgsfield_watermark', row, sev, `Higgsfield output from a watermarking plan ("${row.account_or_plan || 'noted in notes'}") carries an embedded watermark: regenerate on a plan without watermarks${never}`);
    }

    const quaternius = prov === 'quaternius' || /quaternius/i.test(hay(row, 'source_url', 'author', 'provider')) || isQal(lid);
    if (quaternius) {
      const when = Date.parse(row.retrieved_at_utc);
      const afterQal = !Number.isFinite(when) || when >= Date.parse(QAL_EFFECTIVE_UTC);
      if (!/^(QAL-1\.0|CC0-1\.0)$/i.test(lid)) push('quaternius_licence_date', row, sev, `Quaternius rows must say QAL-1.0 or CC0-1.0 (by download date), not "${lid}"`);
      else if (isCc0(lid) && afterQal) push('quaternius_licence_date', row, sev, `downloaded ${row.retrieved_at_utc || '(unknown date)'}, on or after ${QAL_EFFECTIVE_UTC.slice(0, 10)}, so the Quaternius Asset License v1.0 governs it, not CC0: set licence_id QAL-1.0 and redistribution embedded_only`);
    }
    if (isQal(lid) && row.redistribution !== 'embedded_only') push('qal_redistribution', row, sev, `QAL forbids redistributing the assets themselves: redistribution must be embedded_only, not "${row.redistribution}"`);

    if (/pixabay/i.test(hay(row, 'source_url', 'provider', 'author')) || prov === 'pixabay') {
      if (lid !== 'Pixabay-Content-License') push('pixabay_cc0', row, sev, `Pixabay content is under the Pixabay Content License (no standalone redistribution), not "${lid}": set licence_id Pixabay-Content-License`);
      else if (row.redistribution === 'allowed') push('pixabay_cc0', row, 'WARN', 'Pixabay Content License forbids standalone redistribution: set redistribution embedded_only');
    }
    if (/freepd\.com/i.test(row.source_url || '')) {
      const when = Date.parse(row.retrieved_at_utc);
      if (!Number.isFinite(when) || when >= Date.parse(FREEPD_OFFLINE_UTC)) push('freepd_source', row, sev, `freepd.com has been offline since 2025, so a download dated ${row.retrieved_at_utc || '(unknown)'} cannot come from it: find the file's real source`);
      else push('freepd_source', row, 'WARN', 'freepd.com is offline; keep the original download receipt for this row');
    }
    if (/sonniss/i.test(hay(row, 'source_url', 'licence_id', 'provider', 'author'))) {
      if (row.redistribution !== 'embedded_only' || row.training_ban !== 'true') push('sonniss_terms', row, sev, `Sonniss GDC sounds may be used only synchronised in the project and never for AI training: set redistribution embedded_only and training_ban true (now ${row.redistribution}/${row.training_ban})`);
    }

    const a = assets.get(key);
    if ((a && a.rig_route === 'mia') || /rig_route\s*[=:]\s*mia\b/i.test(row.notes || '')) {
      push('mia_rig_route', row, sev, `rigged through Make-It-Animatable (asset ${a?.asset_id ?? 'per notes'}): its output licence is unknown (U-14), so it cannot ship until that is decided${never}`);
    }
    if (isNoncommercialMotion(row)) push('noncommercial_motion', row, sev, `RigAnything, GVHMR and SMPL-derived motion are non-commercial: BLOCKED${never}`);
    if (isToolBinaryName(rp)) push('tool_binary', row, sev, `${path.posix.basename(rp)} is a dev-only tool (ffmpeg is GPL, gifski AGPL; the Unity Pipeline runtime only belongs in development players)${never}`);

    if (row.commercial_ok !== 'true') {
      if (shipping) push('unknown_commercial', row, 'FAIL', row.commercial_ok === 'unknown' ? 'commercial_ok is unknown: a human must classify this row before it ships' : 'commercial_ok is false: this file cannot ship in a paid game');
      else if (row.commercial_ok === 'unknown') push('unknown_commercial', row, 'WARN', 'commercial_ok is unknown (fine while it never ships)');
    }

    if (release?.public_repo === true && (row.redistribution === 'forbidden' || row.redistribution === 'embedded_only') && rp && !ignored(rp)) {
      const rule = isQal(lid) ? 'qal_redistribution' : 'redistribution_public_repo';
      push(rule, row, 'FAIL', `release.json says the repo is public, and this ${row.redistribution} file is not gitignored: raw files would be redistributed`);
    }

    if (lid && !KNOWN_IDS.some((re) => re.test(lid)) && !isNc(lid) && !isSa(lid) && !isGpl(lid) && !isSamplingPlus(lid)) {
      push('licence_not_allowlisted', row, 'WARN', `licence id "${lid}" is not on the kit allowlist; check it by hand and prefer an SPDX id`);
    }

    if (ctx.checkSha !== false) {
      const file = resolveRowFile(ctx, rp);
      if (rp && !rp.endsWith('/')) {
        if (!file) push('missing_file', row, 'WARN', 'no file at this path; the row may be stale or the file was moved');
        else if (!row.sha256) push('sha_mismatch', row, 'WARN', 'no sha256 recorded; run license_row.mjs add --replace to record it');
        else {
          const actual = sha256File(file);
          if (actual !== row.sha256) push('sha_mismatch', row, sev, `sha256 on disk ${actual.slice(0, 12)}… differs from the ledger's ${row.sha256.slice(0, 12)}…: the file changed after its licence row was written; re-check the source and update the row`);
        }
      }
    }
  }

  if (!release && ctx.releaseMissing !== false) {
    out.push({ rule: 'release_missing', path: 'Docs/ship/release.json', severity: 'WARN', message: 'Docs/ship/release.json is missing: the DRM, platform and public-repo rules were not evaluated' });
  } else if (release && shippingCcBy && release.steam_drm_wrapper === undefined) {
    out.push({ rule: 'cc_by_drm', path: 'Docs/ship/release.json', severity: 'WARN', message: 'CC BY content ships; release.json must state steam_drm_wrapper' });
  }
  return out;
}

/**
 * Project-level attribution rules: AnkleBreaker needs its credit line; CoplayDev's runtime assembly needs its MIT notice.
 * @param {{ws: string, unity: string}} ctx
 * @param {Record<string,string>[]} rows all ledger rows
 * @param {{coplayInBuild?: boolean, buildNotices?: string|null}} [build] coplayInBuild: MCPForUnity found in the player
 */
export function projectChecks(ctx, rows, build = {}) {
  const out = [];
  const readIf = (p) => { try { return readText(p); } catch { return ''; } };
  const mcp = readIf(path.join(ctx.ws, '.mcp.json'));
  const manifest = readIf(path.join(ctx.unity, 'Packages', 'manifest.json'));
  const credits = readIf(path.join(ctx.ws, 'Docs', 'ship', 'CREDITS.md')) + readIf(path.join(ctx.ws, 'Docs', 'ship', 'credits.json'));
  const ankle = /anklebreaker/i.test(mcp) || /anklebreaker/i.test(manifest) || rows.some((r) => /anklebreaker/i.test(hay(r, 'source_url', 'author', 'notes', 'path')));
  if (ankle) {
    const credited = rows.some((r) => /(made with|powered by) anklebreaker mcp/i.test(r.attribution_text || '')) || /(made with|powered by) anklebreaker mcp/i.test(credits);
    if (!credited) out.push({ rule: 'anklebreaker_credit', path: '.mcp.json', severity: 'FAIL', message: 'AnkleBreaker MCP was used: every distributed build must show "Made with AnkleBreaker MCP" or "Powered by AnkleBreaker MCP" (logo where feasible); add a ledger row with that attribution_text, then run notices_gen.mjs' });
  }
  const coplayRow = rows.find((r) => /mcpforunity|coplaydev\/unity-mcp/i.test(hay(r, 'path', 'source_url')) && /^MIT$/i.test(r.licence_id || ''));
  const noticeText = readIf(path.join(ctx.ws, 'Docs', 'ship', 'THIRD_PARTY_NOTICES.txt')) + (build.buildNotices || '');
  const noticed = Boolean(coplayRow) || /mcpforunity|coplaydev/i.test(noticeText);
  if (build.coplayInBuild && !noticed) {
    out.push({ rule: 'coplaydev_notice', path: 'MCPForUnity.Runtime', severity: 'FAIL', message: 'MCPForUnity.Runtime (CoplayDev unity-mcp, MIT) is compiled into the player without its notice: add an MIT ledger row for it and run notices_gen.mjs, or remove the package before release builds' });
  } else if (!build.coplayInBuild && /coplaydev|unity-mcp|mcpforunity/i.test(manifest) && !noticed) {
    out.push({ rule: 'coplaydev_notice', path: 'Packages/manifest.json', severity: 'WARN', message: 'CoplayDev unity-mcp is installed and its runtime assembly compiles into players: remove it for release builds or add its MIT notice (checked against the build with --scope build:<dir>)' });
  }
  return out;
}

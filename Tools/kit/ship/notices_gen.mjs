#!/usr/bin/env node
// notices_gen.mjs: credits and third-party notices from the licence ledger (CONTRACTS.md §4.9; research_ship.md §4.2-§4.4).
//   node Tools/kit/ship/notices_gen.mjs [--out Docs/ship] [--game-json Docs/ship/game.json] [--build <Builds/dir>]
//        [--csv Docs/licenses.csv] [--json]
// Scope: the rows a build ships (--build, via the BuildReport join), else the rows with ships=true.
// Writes <out>/CREDITS.md, <out>/THIRD_PARTY_NOTICES.txt (copy it into the build root) and <out>/credits.json (for the
// in-game credits screen). The Unity notice ("<Game> was made with Unity®…", "Copyright © 2005-<year> Unity
// Technologies…", Unity Software Terms §2.12) is always added, as are "Made with AnkleBreaker MCP" when AnkleBreaker was
// used and CoplayDev's MIT notice when its row is in scope. Licence texts come from a LICENSE/OFL/NOTICE file in the
// asset's own pack folder when one exists and its text is that licence (it carries the real copyright line; W, U,
// Assets, Packages and Assets/Plugins are never searched), else from licence_texts/<id>.txt. CC BY and OGA-BY blocks
// are the TASL credit with the licence link.
// game.json: {"name", "company"?, "engine"? ("unity" default), "credits"?: [{"role", "names": []}]}.
// Exit 0; 1 when a licence that must travel with the game has no text or no copyright holder (files are still written).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { EXIT, parseCli, emit, isMain, runCli, UsageError, CliError } from '../lib/cli.mjs';
import { readJsonOr, readText, writeJsonAtomic, writeTextAtomic } from '../lib/jsonio.mjs';
import { isInside, normalizeForCompare } from '../lib/paths.mjs';
import { utcIso, now } from '../lib/time.mjs';
import { shipContext, readLedger, normRowPath, relOrAbs } from './lib/ledger.mjs';
import { isCc0, isCcBy, isOgaBy, providerOf } from './lib/rules.mjs';
import { resolveBuildDir, joinBuild, LICENCE_FILE } from './lib/build.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const BUNDLED = { MIT: 'MIT.txt', 'BSD-2-Clause': 'BSD-2-Clause.txt', 'BSD-3-Clause': 'BSD-3-Clause.txt', Zlib: 'Zlib.txt', 'Apache-2.0': 'Apache-2.0.txt', 'OFL-1.1': 'OFL-1.1.txt' };
/** Licences whose full text must travel with the game. */
export const TEXT_REQUIRED = /^(MIT|BSD-[23]-Clause|Zlib|Apache-2\.0|OFL-1\.1|MPL-2\.0|LGPL.*|GPL.*font.*)$/i;
// A local licence file is used only when its text is the licence the row names; anything else (the game's own
// LICENSE, a different pack's terms) falls back to the bundled text.
const TEXT_MATCH = [
  [/^MIT$/i, /permission is hereby granted/i],
  [/^BSD-[23]-Clause$/i, /redistribution and use in source and binary forms/i],
  [/^Zlib$/i, /provided ['‘’]as-is['‘’]|altered source versions must be plainly marked/i],
  [/^Apache-2\.0$/i, /apache license/i],
  [/^OFL-1\.1$/i, /sil open font license/i],
  [/^MPL-2\.0$/i, /mozilla public license/i],
  [/^L?GPL/i, /gnu (lesser |library )?general public license/i],
];

/** True when a text is plausibly the licence `id` (see TEXT_MATCH). @param {string} id @param {string} text */
export function licenceTextMatches(id, text) {
  const m = TEXT_MATCH.find(([re]) => re.test(id));
  return Boolean(m && m[1].test(text));
}

/** True for a ledger row that appears in the credits at all (not own work, not uncredited AI output). */
function isThirdParty(r) {
  if (['own_work', 'procedural', 'engine'].includes(r.source_kind) || r.licence_id === 'Own-Work') return false;
  return !(r.ai_generated === 'true' && !r.attribution_text);
}

/**
 * True when a ledger row needs a block in THIRD_PARTY_NOTICES.txt (the same selection buildNotices makes).
 * @param {Record<string,string>} r
 */
export function needsNotice(r) {
  return isThirdParty(r) && (isCcBy(r.licence_id) || isOgaBy(r.licence_id) || TEXT_REQUIRED.test(r.licence_id || ''));
}
const CC_URL = (id) => {
  const m = /^CC-BY(-SA)?-(\d\.\d)$/i.exec(id);
  return m ? `https://creativecommons.org/licenses/by${m[1] ? '-sa' : ''}/${m[2]}/` : '';
};

/** The Unity Software Terms §2.12 notice lines. @param {string} game @param {number} year */
export function unityNotice(game, year) {
  return [
    `${game} was made with Unity®. Unity is a trademark or registered trademark of Unity Technologies.`,
    `Copyright © 2005-${year} Unity Technologies. All rights reserved.`,
  ];
}

function gameName(ctx, gameJson, warnings) {
  if (gameJson?.name) return String(gameJson.name);
  try {
    const m = /^\s*productName:\s*(.+)$/m.exec(readText(path.join(ctx.unity, 'ProjectSettings', 'ProjectSettings.asset')));
    const name = m ? m[1].trim() : '';
    if (name && !/^com\.unity\.template/i.test(name)) return name;
    if (name) warnings.push(`ProjectSettings productName is still the template default (${name}); set it, or give "name" in game.json`);
  } catch { /* no Unity project */ }
  warnings.push('no game name in game.json or ProjectSettings: using the workspace folder name');
  return path.basename(ctx.ws);
}

/**
 * Folders a licence-file search never reads and never climbs past: W, U and every folder between them, and the
 * Unity roots whose licence files belong to the game or to some other pack (Assets, Packages, Assets/Plugins).
 * @param {{ws: string, unity: string}} ctx
 * @param {string} dir absolute
 */
export function isLicenceSearchStop(ctx, dir) {
  if (!isInside(dir, ctx.ws) || isInside(ctx.unity, dir)) return true;
  const d = normalizeForCompare(dir);
  return [ctx.unity, ctx.ws].some((base) => ['Assets', 'Packages', 'Assets/Plugins'].some((rel) => normalizeForCompare(path.join(base, rel)) === d));
}

/**
 * The licence file of the pack an asset came from: its own folder and up to two folders above, never a stop folder
 * (isLicenceSearchStop), and never above a folder row. Only a file whose text is the row's licence is returned.
 * @param {{ws: string, unity: string}} ctx
 * @param {string} rowPath
 * @param {string} licenceId
 * @returns {string|null}
 */
export function findLicenceFile(ctx, rowPath, licenceId) {
  const p = normRowPath(rowPath);
  const folderRow = p.endsWith('/');
  const candidates = [path.join(ctx.ws, p)];
  if (ctx.unity !== ctx.ws) candidates.push(path.join(ctx.unity, p));
  for (const c of candidates) {
    let dir = folderRow ? c.replace(/[\\/]+$/, '') : path.dirname(c);
    for (let up = 0; up < 3 && !isLicenceSearchStop(ctx, dir); up++) {
      let names = [];
      try { names = fs.readdirSync(dir, { withFileTypes: true }).filter((e) => e.isFile() && LICENCE_FILE.test(e.name)).map((e) => e.name).sort(); } catch { /* not a folder */ }
      for (const n of names) {
        const f = path.join(dir, n);
        let text = '';
        try { text = readText(f); } catch { continue; }
        if (licenceTextMatches(licenceId, text)) return f;
      }
      if (folderRow) break;
      const next = path.dirname(dir);
      if (next === dir) break;
      dir = next;
    }
  }
  return null;
}

/**
 * Build the credit and notice documents.
 * @param {{ws: string, unity: string}} ctx
 * @param {Record<string,string>[]} rows rows in ship scope
 * @param {Record<string,string>[]} allRows every ledger row (AnkleBreaker detection)
 * @param {object|null} gameJson
 */
export function buildNotices(ctx, rows, allRows, gameJson) {
  const warnings = [];
  const missing = [];
  const game = gameName(ctx, gameJson, warnings);
  const year = now().getUTCFullYear();
  const groups = new Map();
  for (const r of rows) {
    if (!isThirdParty(r)) continue;
    const key = `${r.licence_id}|${r.source_url || r.author || normRowPath(r.path)}`;
    if (!groups.has(key)) groups.set(key, { licence: r.licence_id, licence_url: r.licence_url, author: r.author, source_url: r.source_url, attributions: new Set(), paths: [], provider: providerOf(r) });
    const g = groups.get(key);
    g.paths.push(normRowPath(r.path));
    if (r.attribution_text) g.attributions.add(r.attribution_text.trim());
    if (!g.author && r.author) g.author = r.author;
    if (!g.licence_url && r.licence_url) g.licence_url = r.licence_url;
  }
  const title = (g) => (g.paths.length === 1 ? path.posix.basename(g.paths[0].replace(/\/$/, '')).replace(/\.[^.]+$/, '') : `${g.paths.length} files`);
  const tasl = (g) => `"${title(g)}" by ${g.author || 'unknown author'}${g.source_url ? ` (${g.source_url})` : ''}, licensed under ${g.licence}${g.licence_url || CC_URL(g.licence) ? ` (${g.licence_url || CC_URL(g.licence)})` : ''}`;

  const attributed = [];
  const thanks = [];
  const noticeBlocks = [];
  for (const g of [...groups.values()].sort((a, b) => `${a.licence}${title(a)}`.localeCompare(`${b.licence}${title(b)}`))) {
    const needsCredit = isCcBy(g.licence) || isOgaBy(g.licence) || g.attributions.size > 0 || TEXT_REQUIRED.test(g.licence);
    if (needsCredit) attributed.push(...(g.attributions.size ? [...g.attributions] : [tasl(g)]));
    else if (isCc0(g.licence) || g.licence) thanks.push(`${g.author || g.source_url || title(g)} (${g.paths.length} file${g.paths.length === 1 ? '' : 's'}, ${g.licence})`);
    if (isCcBy(g.licence) || isOgaBy(g.licence) || TEXT_REQUIRED.test(g.licence)) {
      // CC BY and OGA-BY blocks are always the TASL credit plus the licence link, never a local file's text.
      const ccLike = isCcBy(g.licence) || isOgaBy(g.licence);
      const local = ccLike ? null : g.paths.slice(0, 5).map((p) => findLicenceFile(ctx, p, g.licence)).find(Boolean) || null;
      let body = '';
      if (local) body = readText(local).trim();
      else if (BUNDLED[g.licence]) {
        const holder = [...g.attributions].find((a) => /copyright|©|\(c\)/i.test(a)) || (g.author ? `Copyright (c) ${g.author}` : '');
        if (!holder) missing.push(`${g.licence} component ${title(g)}: no copyright holder (set author or attribution_text)`);
        body = `${holder}\n\n${fs.readFileSync(path.join(HERE, 'licence_texts', BUNDLED[g.licence]), 'utf8').trim()}`;
      } else if (isCcBy(g.licence) || isOgaBy(g.licence)) {
        body = `${[...g.attributions][0] || tasl(g)}\nLicence: ${g.licence_url || CC_URL(g.licence) || g.licence}\nThis work may have been modified (for example resized, re-encoded or re-coloured) for use in ${game}.`;
      } else {
        missing.push(`${g.licence} component ${title(g)}: no licence text; put its LICENSE file next to the asset`);
        body = `(licence text missing: ${g.licence})`;
      }
      noticeBlocks.push([
        '='.repeat(72),
        `${title(g)}${g.author ? ` by ${g.author}` : ''}`,
        `Licence: ${g.licence}${local ? ` (text from ${relOrAbs(ctx.ws, local)})` : ''}`,
        ...(g.source_url ? [`Source: ${g.source_url}`] : []),
        `Files: ${g.paths.slice(0, 8).join(', ')}${g.paths.length > 8 ? `, and ${g.paths.length - 8} more` : ''}`,
        '',
        body,
        '',
      ].join('\n'));
    }
  }

  const readIf = (p) => { try { return readText(p); } catch { return ''; } };
  const ankle = /anklebreaker/i.test(readIf(path.join(ctx.ws, '.mcp.json'))) || /anklebreaker/i.test(readIf(path.join(ctx.unity, 'Packages', 'manifest.json'))) || allRows.some((r) => /anklebreaker/i.test(`${r.source_url} ${r.author} ${r.notes} ${r.path}`));
  const tools = [];
  if (ankle && !attributed.some((a) => /(made with|powered by) anklebreaker mcp/i.test(a))) tools.push('Made with AnkleBreaker MCP');

  const engineUnity = !gameJson?.engine || /^unity$/i.test(gameJson.engine);
  const unity = engineUnity ? unityNotice(game, year) : [];
  const team = Array.isArray(gameJson?.credits) ? gameJson.credits.filter((c) => c && c.role && Array.isArray(c.names)) : [];

  const md = [`# Credits: ${game}`, '', `Generated ${utcIso()} by Tools/kit/ship/notices_gen.mjs from Docs/licenses.csv; edit the ledger or game.json, not this file.`, ''];
  if (team.length) {
    md.push('## Team', '');
    for (const c of team) md.push(`- **${c.role}:** ${c.names.join(', ')}`);
    md.push('');
  }
  md.push('## Third-party assets and software', '');
  md.push(...(attributed.length ? attributed.map((a) => `- ${a}`) : ['- none in scope']), '');
  if (thanks.length) md.push('## Also thanks to (no attribution required)', '', ...thanks.map((t) => `- ${t}`), '');
  if (tools.length) md.push('## Tools', '', ...tools.map((t) => `- ${t}`), '');
  if (unity.length) md.push('## Engine', '', ...unity.map((u) => `${u}  `), '');

  const txt = [
    `THIRD-PARTY NOTICES: ${game}`,
    `Generated ${utcIso()} by game-studio-kit notices_gen.mjs from Docs/licenses.csv. Ship this file in the build root.`,
    '',
    ...unity,
    ...(tools.length ? ['', ...tools] : []),
    '',
    ...(noticeBlocks.length ? noticeBlocks : ['No third-party component in scope needs a licence notice.', '']),
  ].join('\n');

  const creditsJson = {
    schema: 1,
    game,
    generated_utc: utcIso(),
    sections: [
      ...team.map((c) => ({ title: c.role, lines: c.names })),
      ...(attributed.length ? [{ title: 'Third-party assets and software', lines: attributed }] : []),
      ...(thanks.length ? [{ title: 'Also thanks to', lines: thanks }] : []),
      ...(tools.length ? [{ title: 'Tools', lines: tools }] : []),
      ...(unity.length ? [{ title: 'Engine', lines: unity }] : []),
    ],
    unity_notice: unity,
  };
  return { game, md: md.join('\n'), txt: txt.endsWith('\n') ? txt : `${txt}\n`, creditsJson, warnings, missing, components: groups.size, attributed: attributed.length };
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    out: { type: 'string' },
    'game-json': { type: 'string' },
    build: { type: 'string' },
    csv: { type: 'string' },
  });
  if (positionals.length) throw new UsageError('usage: notices_gen.mjs [--out Docs/ship] [--game-json <path>] [--build <dir>] [--json]');
  const ctx = shipContext({ csv: values.csv });
  const ledger = readLedger(ctx.csv);
  const warnings = [];
  if (!ledger.exists) warnings.push('Docs/licenses.csv does not exist: only the Unity notice is written');
  let rows = ledger.rows.filter((r) => r.ships === 'true');
  if (values.build) {
    const dir = resolveBuildDir(ctx, values.build);
    if (!dir) throw new UsageError(`build folder not found: ${values.build}`);
    const j = joinBuild(ctx, ledger.rows, dir);
    if (!j.ok) throw new CliError(j.error, EXIT.INFRA);
    rows = ledger.rows.filter((r) => j.rowsInScope.has(r));
    if (j.unlicensed.length) warnings.push(`${j.unlicensed.length} shipped file(s) have no licence row (run build_join.mjs): they are missing from the notices`);
  }
  const gameFile = values['game-json'] ? path.resolve(values['game-json']) : path.join(ctx.docsShip, 'game.json');
  if (values['game-json'] && !fs.existsSync(gameFile)) throw new UsageError(`--game-json not found: ${gameFile}`);
  const gameJson = readJsonOr(gameFile, null, { onInvalid: 'throw' });
  const n = buildNotices(ctx, rows, ledger.rows, gameJson);
  const out = values.out ? path.resolve(values.out) : ctx.docsShip;
  const files = {
    credits_md: path.join(out, 'CREDITS.md'),
    notices: path.join(out, 'THIRD_PARTY_NOTICES.txt'),
    credits_json: path.join(out, 'credits.json'),
  };
  writeTextAtomic(files.credits_md, n.md);
  writeTextAtomic(files.notices, n.txt);
  writeJsonAtomic(files.credits_json, n.creditsJson);
  const ok = n.missing.length === 0;
  const result = {
    ok,
    exit: ok ? EXIT.OK : EXIT.FAIL,
    game: n.game,
    files: Object.values(files).map((f) => relOrAbs(ctx.ws, f)),
    rows_in_scope: rows.length,
    components: n.components,
    attributed: n.attributed,
    unity_notice: n.creditsJson.unity_notice.length > 0,
    missing: n.missing,
    warnings: [...warnings, ...n.warnings],
  };
  const text = [
    `${ok ? 'wrote' : 'wrote (INCOMPLETE)'} ${result.files.join(', ')} for ${n.game}: ${n.components} component(s), ${n.attributed} credit line(s)`,
    ...n.missing.map((m) => `  missing: ${m}`),
    ...result.warnings.map((w) => `  warning: ${w}`),
  ];
  emit(result, { json: values.json, text });
  return result.exit;
}

if (isMain(import.meta.url)) await runCli(main);

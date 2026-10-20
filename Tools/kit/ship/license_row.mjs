#!/usr/bin/env node
// license_row.mjs: the only writer of Docs/licenses.csv (CONTRACTS.md §4.9; ARCHITECTURE §3.13).
//   add --path p --source-kind k --licence-id id [--source-url u] [--licence-url u] [--author a] [--attribution t]
//       [--retrieved-at <utc>] [--account-or-plan p] [--provider p] [--generator-model m] [--job-id j] [--ai-generated]
//       [--commercial-ok true|false|unknown] [--training-ban true|false|unknown] [--redistribution allowed|embedded_only|forbidden]
//       [--ships] [--notes n] [--sha256 h] [--replace] [--csv <file>] [--json]
//   add --from-json <file|-> [--replace]      one row object, an array of them, or JSON lines; all or nothing
//   import --from Docs/LICENSES.md [--dry-run] converts a Markdown licence log (tables or bullets) into rows;
//                                             unclassifiable rows get licence_id Unknown and commercial_ok unknown;
//                                             a logged sha256 that differs from the file on disk is kept (and noted)
//   get --path p
// Rows are validated against licenses.schema.json; sha256 is computed when the file exists. Paths are stored
// W-relative: with a Unity subfolder, Assets/… becomes <unity_subdir>/Assets/…. A path already in the
// ledger exits 1 unless --replace (which rewrites the file atomically). Writes happen under Docs/licenses.csv.lock.
// Exit 0 ok, 1 refused (duplicate, sha256 mismatch, header mismatch, get: not found), 2 usage or invalid row.
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, parseCli, emit, isMain, runCli, UsageError, CliError } from '../lib/cli.mjs';
import { readText, writeTextAtomic } from '../lib/jsonio.mjs';
import { withLockSync } from '../lib/locks.mjs';
import { toCsv, toCsvLine, parseCsv } from '../lib/csv.mjs';
import { sha256File } from '../lib/hash.mjs';
import { utcIso, isUtcIso } from '../lib/time.mjs';
import { isInside, relPosix, toPosix } from '../lib/paths.mjs';
import { columns, shipContext, normRowPath, pathKey, rowErrors, resolveRowFile } from './lib/ledger.mjs';

const FLAG_TO_COL = {
  path: 'path', 'source-kind': 'source_kind', 'licence-id': 'licence_id', 'source-url': 'source_url', 'licence-url': 'licence_url',
  author: 'author', attribution: 'attribution_text', 'retrieved-at': 'retrieved_at_utc', 'account-or-plan': 'account_or_plan',
  provider: 'provider', 'generator-model': 'generator_model', 'job-id': 'job_id', 'commercial-ok': 'commercial_ok',
  'training-ban': 'training_ban', redistribution: 'redistribution', notes: 'notes', sha256: 'sha256',
};
const ALIASES = { attribution: 'attribution_text', retrieved_at: 'retrieved_at_utc', licence: 'licence_id', license_id: 'licence_id', license_url: 'licence_url' };

const COMMERCIAL_TRUE = [/^CC0-/, /^CC-BY-(\d)/, /^CC-BY-SA-/, /^OGA-BY-/, /^MIT$/, /^Apache-2\.0$/, /^BSD-/, /^Zlib$/, /^OFL-/, /^Unlicense$/, /^MPL-2\.0$/, /^QAL-/, /^Pixabay-Content-License$/, /^Sonniss-GDC$/, /^Mixamo-Adobe$/, /^Own-Work$/, /^Steamworks-SDK$/, /^Unity-/];
const EMBEDDED_ONLY = [/^QAL-/, /^Mixamo-Adobe$/, /^Sonniss-GDC$/, /^Pixabay-Content-License$/, /^Unity-Asset-Store-EULA$/];

/**
 * Default commercial_ok for a licence id: known commercial licences → true, NC → false, else unknown.
 * (Share-alike allows commercial use; licenses_check blocks it separately.)
 * @param {string} id
 */
export function defaultCommercial(id) {
  if (/^CC-BY-NC/i.test(id)) return 'false';
  return COMMERCIAL_TRUE.some((re) => re.test(id)) ? 'true' : 'unknown';
}

/**
 * Fill defaults for a partial row: every column present, strings only.
 * @param {Record<string, any>} input column-keyed values (booleans allowed for true/false columns)
 * @param {{ws: string, unity: string, unitySubdir?: string}} ctx
 * @param {{keepGivenSha?: boolean}} [opts] keepGivenSha: a given sha256 that differs from the file on disk is kept
 *   (import of a logged hash) instead of refused
 */
export function completeRow(input, ctx, opts = {}) {
  const row = {};
  const src = {};
  for (const [k, v] of Object.entries(input || {})) src[ALIASES[k] || k] = v;
  const unknownCols = Object.keys(src).filter((k) => !columns().includes(k));
  if (unknownCols.length) throw new UsageError(`unknown column(s): ${unknownCols.join(', ')}`);
  const str = (v) => (v === null || v === undefined ? undefined : typeof v === 'boolean' ? String(v) : String(v));
  for (const c of columns()) row[c] = str(src[c]);
  row.path = normalisePath(row.path, ctx);
  const lid = (row.licence_id ?? '').trim();
  row.licence_id = lid;
  if (row.ai_generated === undefined) row.ai_generated = row.source_kind === 'generated' ? 'true' : 'false';
  if (row.commercial_ok === undefined) row.commercial_ok = defaultCommercial(lid);
  if (row.training_ban === undefined) {
    const prov = String(row.provider || '').toLowerCase();
    row.training_ban = /^(Sonniss-GDC|Mixamo-Adobe)$/.test(lid) || /higgsfield|recraft/.test(prov) ? 'true' : 'unknown';
  }
  if (row.redistribution === undefined) row.redistribution = EMBEDDED_ONLY.some((re) => re.test(lid)) ? 'embedded_only' : 'allowed';
  if (row.ships === undefined) row.ships = 'false';
  if (row.retrieved_at_utc === undefined || row.retrieved_at_utc === '') row.retrieved_at_utc = utcIso();
  for (const c of columns()) if (row[c] === undefined) row[c] = '';
  const file = row.path ? resolveRowFile(ctx, row.path) : null;
  if (file) {
    const actual = sha256File(file);
    if (row.sha256 && row.sha256 !== actual) {
      if (!opts.keepGivenSha) throw new CliError(`--sha256 ${row.sha256.slice(0, 12)}… does not match ${row.path} on disk (${actual.slice(0, 12)}…)`, EXIT.FAIL);
    } else row.sha256 = actual;
  }
  return row;
}

/**
 * A ledger path is W-relative (CONTRACTS §1.1). When the Unity project is a subfolder, a U-relative spelling
 * (Assets/…, Packages/…) gets the unity_subdir prefix so one file never has two rows.
 * @param {string|undefined|null} p
 * @param {{ws: string, unity: string, unitySubdir?: string}} ctx
 */
export function normalisePath(p, ctx) {
  if (p === undefined || p === null || p === '') return '';
  let s = String(p).trim();
  if (path.isAbsolute(s)) {
    if (!isInside(s, ctx.ws)) throw new UsageError(`--path ${s} is outside the workspace ${toPosix(ctx.ws)}`);
    const trailing = /[\\/]$/.test(s);
    s = relPosix(ctx.ws, s) + (trailing ? '/' : '');
  }
  s = normRowPath(s);
  if (s && ctx.unity !== ctx.ws && ctx.unitySubdir && ctx.unitySubdir !== '.' && !fs.existsSync(path.join(ctx.ws, s))
    && (fs.existsSync(path.join(ctx.unity, s)) || /^(Assets|Packages|ProjectSettings)\//i.test(s))) {
    s = `${ctx.unitySubdir}/${s}`;
  }
  return s;
}

function readRows(csvFile) {
  if (!fs.existsSync(csvFile) || fs.statSync(csvFile).size === 0) return { header: columns(), rows: [], exists: false };
  const { header, rows } = parseCsv(readText(csvFile));
  if (header.join(',') !== columns().join(',')) throw new CliError(`header of ${csvFile} differs from the ${columns().length} licenses.schema.json x-columns; fix it before adding rows`, EXIT.FAIL);
  return { header, rows, exists: true };
}

/**
 * Validate and write rows under the ledger lock. All or nothing.
 * @param {string} csvFile
 * @param {Record<string,string>[]} newRows complete rows
 * @param {{replace?: boolean, dryRun?: boolean}} [opts]
 * @returns {{written: string[], replaced: string[]}}
 */
export function writeRows(csvFile, newRows, opts = {}) {
  const problems = [];
  newRows.forEach((r, i) => {
    const errs = rowErrors(r);
    if (!r.path) errs.unshift('path is required');
    if (errs.length) problems.push(`row ${i + 1} (${r.path || 'no path'}): ${errs.slice(0, 4).join('; ')}`);
  });
  if (problems.length) throw new UsageError(`invalid licence row(s): ${problems.join(' | ')}`);
  const keys = new Set();
  for (const r of newRows) {
    const k = pathKey(r.path);
    if (keys.has(k)) throw new UsageError(`the batch has ${r.path} twice`);
    keys.add(k);
  }
  fs.mkdirSync(path.dirname(csvFile), { recursive: true });
  return withLockSync(`${csvFile}.lock`, () => {
    const { header, rows, exists } = readRows(csvFile);
    const index = new Map(rows.map((r, i) => [pathKey(r.path), i]));
    const dups = newRows.filter((r) => index.has(pathKey(r.path)));
    if (dups.length && !opts.replace) {
      throw new CliError(`already in the ledger: ${dups.map((r) => r.path).join(', ')} (use --replace to rewrite)`, EXIT.FAIL, { duplicates: dups.map((r) => r.path) });
    }
    const written = [];
    const replaced = [];
    if (opts.dryRun) return { written: newRows.filter((r) => !index.has(pathKey(r.path))).map((r) => r.path), replaced: dups.map((r) => r.path) };
    if (dups.length) {
      const out = rows.map((r) => ({ ...r }));
      for (const r of newRows) {
        const i = index.get(pathKey(r.path));
        if (i === undefined) { out.push(r); written.push(r.path); }
        else { out[i] = r; replaced.push(r.path); }
      }
      writeTextAtomic(csvFile, toCsv(header, out));
    } else if (!exists) {
      writeTextAtomic(csvFile, toCsv(header, newRows));
      written.push(...newRows.map((r) => r.path));
    } else {
      const text = fs.readFileSync(csvFile, 'utf8');
      const lines = newRows.map((r) => toCsvLine(header.map((h) => r[h]))).join('\n') + '\n';
      fs.appendFileSync(csvFile, (text.length && !text.endsWith('\n') ? '\n' : '') + lines, 'utf8');
      written.push(...newRows.map((r) => r.path));
    }
    return { written, replaced };
  });
}

// ----- Markdown import -----
const PATH_STRONG = /^(path|paths|file|files|file path|location|installed at)$/i;
const PATH_WEAK = /^(asset|assets|folder|where)$/i;
const HEAD = {
  licence: /^(licen[cs]e|licen[cs]e id|spdx|terms)$/i,
  source: /^(source|url|link|source url|origin|from|download)$/i,
  author: /^(author|authors|creator|by|artist|owner)$/i,
  attribution: /^(attribution|credit|credits|credit line)$/i,
  retrieved: /^(retrieved|date|downloaded|added|retrieved at)$/i,
  sha: /^(sha256|sha-256|hash|sha)$/i,
  notes: /^(notes?|comments?|use|usage|purpose)$/i,
  name: /^(name|pack|title|asset|asset pack|asset name)$/i,
};
const looksLikePath = (p) => /[/\\]/.test(p) || /\.[A-Za-z0-9]{1,8}$/.test(p);

// Licence families a free-text licence can name. A string that names two of them ("CC0 or CC BY", "Quaternius
// licence, formerly CC0") is ambiguous and stays Unknown for a human. "public domain" counts only at the start: a
// remark such as "CC BY 4.0 (photo source is public domain)" must not turn an attribution licence into CC0.
const FAMILIES = [
  ['ccby', /\bcc[-_ ]?by\b|creative\s+commons\s+attribution/i],
  ['cc0', /\bcc[-_ ]?0\b|creative\s+commons\s+zero|^(public\s+domain|pd)\b/i],
  ['ogaby', /\boga[-_ ]?by\b/i],
  ['ofl', /\bofl\b|open\s+font\s+licen/i],
  ['mit', /^mit\b|\bmit\s+licen[cs]e\b/i],
  ['apache', /\bapache\b/i],
  ['gpl', /\b(a|l)?gpl/i],
  ['qal', /\bqal\b|quaternius\s+asset\s+licen/i],
  ['pixabay', /pixabay/i],
  ['sonniss', /sonniss/i],
  ['mixamo', /mixamo/i],
  ['sampling', /sampling\s*\+|sampling[- ]plus/i],
  ['own', /^(own|original|self[- ]made|in[- ]house)/i],
];

/** Map a free-text licence to an SPDX or kit id, or null when it cannot be classified. @param {string} raw */
export function normaliseLicence(raw) {
  const s = String(raw || '').trim();
  if (!s) return null;
  const v = (/(\d\.\d)/.exec(s) || [])[1];
  const hits = FAMILIES.filter(([, re]) => re.test(s)).map(([id]) => id);
  if (hits.length !== 1) return null;
  switch (hits[0]) {
    case 'ccby': {
      // NoDerivatives cannot be used in a game that edits the asset; NC and SA get their own blocked ids.
      if (/no[-_ ]?deriv|\bnd\b/i.test(s)) return null;
      const nc = /non[-_ ]?commercial|\bnc\b/i.test(s);
      const sa = /share[-_ ]?alike|\bsa\b/i.test(s);
      if (nc) return `CC-BY-NC${sa ? '-SA' : ''}-${v || '4.0'}`;
      if (sa) return `CC-BY-SA-${v || '4.0'}`;
      return `CC-BY-${v || '4.0'}`;
    }
    case 'cc0': return 'CC0-1.0';
    case 'ogaby': return `OGA-BY-${v || '3.0'}`;
    case 'ofl': return 'OFL-1.1';
    case 'mit': return 'MIT';
    case 'apache': return 'Apache-2.0';
    case 'gpl': return s.toUpperCase().replace(/\s+/g, '-').replace(/[^A-Z0-9.+-]/g, '');
    case 'qal': return 'QAL-1.0';
    case 'pixabay': return 'Pixabay-Content-License';
    case 'sonniss': return 'Sonniss-GDC';
    case 'mixamo': return 'Mixamo-Adobe';
    case 'sampling': return 'Freesound-Sampling+';
    case 'own': return 'Own-Work';
    default: return null;
  }
}

const cleanCell = (c) => String(c || '').trim().replace(/^`+|`+$/g, '').replace(/\*\*/g, '').trim();
const linkUrl = (c) => (/\[[^\]]*\]\((https?:[^)\s]+)\)/.exec(c) || /(https?:\/\/[^\s)>|]+)/.exec(c) || [])[1] || '';
const linkText = (c) => cleanCell(String(c || '').replace(/\[([^\]]*)\]\([^)]*\)/g, '$1'));

/**
 * Parse a Markdown licence log into entries.
 * @param {string} text
 * @returns {{entries: {line: number, path: string, licenceRaw: string, source_url: string, author: string, attribution: string, retrieved: string, sha256: string, notes: string}[], skipped: number}}
 */
export function parseLicencesMarkdown(text) {
  const lines = String(text).split(/\r?\n/);
  const entries = [];
  let skipped = 0;
  for (let i = 0; i < lines.length; i++) {
    const l = lines[i];
    if (/^\s*\|/.test(l) && i + 1 < lines.length && /^\s*\|?\s*:?-{2,}/.test(lines[i + 1])) {
      const heads = l.trim().replace(/^\||\|$/g, '').split('|').map((h) => cleanCell(h));
      const col = {};
      heads.forEach((h, j) => { if (col.path === undefined && PATH_STRONG.test(h)) col.path = j; });
      if (col.path === undefined) heads.forEach((h, j) => { if (col.path === undefined && PATH_WEAK.test(h)) col.path = j; });
      heads.forEach((h, j) => {
        if (j === col.path) return;
        for (const [k, re] of Object.entries(HEAD)) if (col[k] === undefined && re.test(h)) { col[k] = j; break; }
      });
      i += 2;
      for (; i < lines.length && /^\s*\|/.test(lines[i]); i++) {
        const cells = lines[i].trim().replace(/^\||\|$/g, '').split('|');
        const get = (k) => (col[k] === undefined ? '' : cells[col[k]] ?? '');
        const p = cleanCell(linkText(get('path')));
        if (!p || !looksLikePath(p)) { skipped++; continue; }
        entries.push({
          line: i + 1,
          path: p,
          licenceRaw: linkText(get('licence')),
          source_url: linkUrl(get('source')) || linkUrl(get('name')) || linkUrl(get('licence')),
          author: linkText(get('author')),
          attribution: linkText(get('attribution')),
          retrieved: cleanCell(get('retrieved')),
          sha256: cleanCell(get('sha')).toLowerCase(),
          notes: [linkText(get('name')), linkText(get('notes'))].filter(Boolean).join('; '),
        });
      }
      i--;
      continue;
    }
    const b = /^\s*[-*]\s+(.*)$/.exec(l);
    if (b) {
      const body = b[1];
      const p = (/`([^`]+)`/.exec(body) || /((?:[\w .-]+\/)*(?:Assets|Packages)\/[^\s,;|)]+)/.exec(body) || [])[1];
      if (!p) { skipped++; continue; }
      const licenceRaw = (/(CC0[\w. -]*|CC[- ]BY[\w. -]*|OGA[- ]BY[\w. -]*|OFL[\w. -]*|MIT|Apache[\w. -]*|QAL[\w. -]*|Pixabay[\w -]*|Sonniss[\w -]*|Mixamo[\w -]*|(?:A|L)?GPL[\w. -]*|Public Domain|Own[- ]work)/i.exec(body) || [])[1] || '';
      entries.push({ line: i + 1, path: cleanCell(p), licenceRaw: licenceRaw.trim(), source_url: linkUrl(body), author: (/\bby ([^,;()]+)/i.exec(linkText(body)) || [])[1]?.trim() || '', attribution: '', retrieved: (/(\d{4}-\d{2}-\d{2})/.exec(body) || [])[1] || '', sha256: (/\b([0-9a-f]{64})\b/i.exec(body) || [])[1]?.toLowerCase() || '', notes: linkText(body).replace(/`/g, '').slice(0, 300) });
    }
  }
  return { entries, skipped };
}

function importRows(mdFile, ctx) {
  const { entries, skipped } = parseLicencesMarkdown(readText(mdFile));
  const rel = toPosix(path.relative(ctx.ws, mdFile));
  const rows = [];
  const shaMismatch = [];
  let unknown = 0;
  for (const e of entries) {
    const p = normalisePath(e.path, ctx);
    const lid = normaliseLicence(e.licenceRaw);
    if (!lid) unknown++;
    let retrieved = '';
    if (isUtcIso(e.retrieved)) retrieved = e.retrieved;
    else if (/^\d{4}-\d{2}-\d{2}$/.test(e.retrieved)) retrieved = `${e.retrieved}T00:00:00Z`;
    // A hash in the log is the file the licence was checked against. When the file on disk differs, the logged hash
    // stays in the row, so licenses_check reports sha_mismatch instead of silently re-baselining a replaced file.
    const logged = /^[0-9a-f]{64}$/.test(e.sha256) ? e.sha256 : '';
    const onDisk = logged ? resolveRowFile(ctx, p) : null;
    const drift = Boolean(onDisk && sha256File(onDisk) !== logged);
    if (drift) shaMismatch.push(p);
    const notes = [`imported from ${rel} line ${e.line}`, drift ? 'sha256 from the log differs from disk' : '', retrieved ? '' : 'retrieval date unknown (import time used)', lid ? '' : `unclassified licence "${e.licenceRaw || 'none'}"`, e.notes]
      .filter(Boolean).join('; ').replace(/[\r\n]+/g, ' ').slice(0, 500);
    rows.push(completeRow({
      path: p,
      sha256: logged || undefined,
      source_kind: !lid ? 'imported_unknown' : lid === 'CC0-1.0' ? 'cc0_download' : lid === 'Own-Work' ? 'own_work' : 'library_download',
      source_url: e.source_url,
      licence_id: lid || 'Unknown',
      author: e.author,
      attribution_text: e.attribution,
      retrieved_at_utc: retrieved || undefined,
      commercial_ok: lid ? undefined : 'unknown',
      ships: /(^|\/)Assets\//.test(p) ? 'true' : 'false',
      notes,
    }, ctx, { keepGivenSha: true }));
  }
  return { rows, unknown, skipped, shaMismatch };
}

function readJsonInput(src) {
  const text = src === '-' ? fs.readFileSync(0, 'utf8') : readText(path.resolve(src));
  const t = text.replace(/^﻿/, '').trim();
  if (!t) throw new UsageError('--from-json input is empty');
  try {
    const v = JSON.parse(t);
    return Array.isArray(v) ? v : [v];
  } catch {
    try {
      return t.split(/\r?\n/).filter((l) => l.trim()).map((l) => JSON.parse(l));
    } catch (e) {
      throw new UsageError(`--from-json is not JSON, a JSON array or JSON lines: ${e.message}`);
    }
  }
}

/** @param {string[]} argv */
export async function main(argv) {
  const opts = { csv: { type: 'string' }, replace: { type: 'boolean', default: false }, 'dry-run': { type: 'boolean', default: false }, 'from-json': { type: 'string' }, from: { type: 'string' }, 'ai-generated': { type: 'boolean' }, ships: { type: 'boolean' } };
  for (const f of Object.keys(FLAG_TO_COL)) opts[f] = { type: 'string' };
  const { values, positionals } = parseCli(argv, opts);
  const cmd = positionals[0];
  if (!['add', 'import', 'get'].includes(cmd) || positionals.length !== 1) throw new UsageError('usage: license_row.mjs add … | add --from-json <file|-> | import --from Docs/LICENSES.md [--dry-run] | get --path p [--json]');
  const ctx = shipContext({ csv: values.csv });
  const csvRel = toPosix(path.relative(ctx.ws, ctx.csv));
  let result;
  if (cmd === 'get') {
    if (!values.path) throw new UsageError('get needs --path');
    const key = pathKey(normalisePath(values.path, ctx));
    const { rows } = fs.existsSync(ctx.csv) ? readRows(ctx.csv) : { rows: [] };
    const row = rows.find((r) => pathKey(r.path) === key) || null;
    result = { ok: Boolean(row), exit: row ? EXIT.OK : EXIT.FAIL, cmd, csv: csvRel, row };
    emit(result, { json: values.json, text: row ? Object.entries(row).map(([k, v]) => `${k}: ${v}`) : `not in the ledger: ${values.path}` });
    return result.exit;
  }
  if (cmd === 'import') {
    if (!values.from) throw new UsageError('import needs --from <Docs/LICENSES.md>');
    const md = path.resolve(values.from);
    if (!fs.existsSync(md)) throw new UsageError(`--from file not found: ${md}`);
    const { rows, unknown, skipped, shaMismatch } = importRows(md, ctx);
    const existing = fs.existsSync(ctx.csv) ? new Set(readRows(ctx.csv).rows.map((r) => pathKey(r.path))) : new Set();
    const fresh = [];
    const seen = new Set();
    const already = [];
    for (const r of rows) {
      const k = pathKey(r.path);
      if (existing.has(k) || seen.has(k)) { already.push(r.path); continue; }
      seen.add(k);
      fresh.push(r);
    }
    const w = fresh.length ? writeRows(ctx.csv, fresh, { dryRun: values['dry-run'] }) : { written: [], replaced: [] };
    result = { ok: true, exit: EXIT.OK, cmd, csv: csvRel, dry_run: values['dry-run'], imported: w.written.length, unknown, sha_mismatch: shaMismatch, skipped_lines: skipped, already_present: already, written: w.written, rows: values['dry-run'] ? fresh : undefined };
    emit(result, { json: values.json, text: `${values['dry-run'] ? 'would import' : 'imported'} ${w.written.length} row(s) (${unknown} unclassified, commercial_ok=unknown); ${already.length} already present; ${skipped} line(s) without a path skipped${shaMismatch.length ? `; ${shaMismatch.length} file(s) differ from the sha256 in the log (kept; licenses_check reports sha_mismatch)` : ''}` });
    return EXIT.OK;
  }
  let inputs;
  if (values['from-json']) {
    inputs = readJsonInput(values['from-json']);
    if (!inputs.every((o) => o && typeof o === 'object' && !Array.isArray(o))) throw new UsageError('--from-json rows must be objects keyed by column name');
  } else {
    if (!values.path || !values['source-kind'] || !values['licence-id']) throw new UsageError('add needs --path, --source-kind and --licence-id (or --from-json)');
    const o = {};
    for (const [flag, col] of Object.entries(FLAG_TO_COL)) if (values[flag] !== undefined) o[col] = values[flag];
    if (values['ai-generated'] !== undefined) o.ai_generated = values['ai-generated'];
    if (values.ships !== undefined) o.ships = values.ships;
    inputs = [o];
  }
  const rows = inputs.map((o) => completeRow(o, ctx));
  const w = writeRows(ctx.csv, rows, { replace: values.replace, dryRun: values['dry-run'] });
  result = { ok: true, exit: EXIT.OK, cmd, csv: csvRel, dry_run: values['dry-run'], written: w.written, replaced: w.replaced, rows };
  emit(result, { json: values.json, text: `${values['dry-run'] ? 'would write' : 'wrote'} ${w.written.length} new and ${w.replaced.length} replaced row(s) in ${csvRel}` });
  return EXIT.OK;
}

if (isMain(import.meta.url)) await runCli(main);

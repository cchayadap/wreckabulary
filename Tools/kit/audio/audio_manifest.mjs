// audio_manifest.mjs: the audio sheet ↔ files ↔ licence rows consistency check (CONTRACTS.md §4.8, §7.2).
//
//   node Tools/kit/audio/audio_manifest.mjs [--sheet Docs/audio/AUDIO_SHEET.md] [--csv Docs/licenses.csv]
//        [--audio-root <dir>]... [--json]
//
// Reads every markdown table in the sheet whose header is
//   | id | trigger | category | priority | voices | loop | 3D | bus | ducking | file | licence |
// A `file` cell holds one W-relative path, several separated by `;`, or a glob (`Assets/Audio/sfx/step_*.wav`; a glob
// matches audio files only, never `.meta` sidecars); `-` or `TBD` means not sourced yet. Result: {ok, missing_files[], unlicensed[], unused[], licence_mismatch[],
// row_errors[], warnings[]}. Exit 1 when any list except warnings is non-empty.
// - missing_files: a row without a file, or a path/glob that matches nothing;
// - unlicensed: a file with no Docs/licenses.csv row (exact path, or a folder row ending in `/` that contains it);
// - unused: an audio file under the audio roots (default <U>/Assets/Audio) that no row references;
// - licence_mismatch: the sheet's licence cell differs from the ledger's licence_id.
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, CliError, emit, isMain, parseCli, runCli } from '../lib/cli.mjs';
import { parseCsv } from '../lib/csv.mjs';
import { readText } from '../lib/jsonio.mjs';
import { findWorkspace, globToRegExp, kitPaths, relPosix, toPosix } from '../lib/paths.mjs';

export const SHEET_COLUMNS = ['id', 'trigger', 'category', 'priority', 'voices', 'loop', '3d', 'bus', 'ducking', 'file', 'licence'];
export const AUDIO_EXTS = new Set(['.wav', '.ogg', '.mp3', '.flac', '.aif', '.aiff']);
const KNOWN_CATEGORIES = new Set(['music', 'sfx', 'ui', 'vo', 'ambience', 'foley', 'stinger', 'jingle']);
const YES_NO = new Set(['yes', 'no', 'y', 'n', 'true', 'false']);
const SPACE = new Set(['yes', 'no', 'y', 'n', 'true', 'false', '2d', '3d']);
const EMPTY = new Set(['', '-', '—', 'tbd', 'todo', 'none', 'n/a']);
const SKIP_DIRS = new Set(['.git', 'node_modules', '.venv', '__pycache__', 'Library', 'Temp']);

const isEmpty = (v) => EMPTY.has(String(v ?? '').trim().toLowerCase());
const cmpKey = (p) => (process.platform === 'win32' ? toPosix(p).toLowerCase() : toPosix(p));

/** Split one markdown table row into trimmed cells (`\|` stays inside a cell; backticks are stripped). */
export function splitRow(line) {
  const body = line.trim().replace(/^\|/, '').replace(/\|$/, '');
  const cells = [];
  let cur = '';
  for (let i = 0; i < body.length; i++) {
    if (body[i] === '\\' && body[i + 1] === '|') { cur += '|'; i++; continue; }
    if (body[i] === '|') { cells.push(cur); cur = ''; continue; }
    cur += body[i];
  }
  cells.push(cur);
  return cells.map((c) => c.trim().replace(/^`(.*)`$/, '$1').trim());
}

/**
 * Rows of every sheet table (header = SHEET_COLUMNS, case-insensitive, extra columns allowed).
 * @param {string} text
 * @returns {{rows: {line: number, cells: Record<string, string>}[], tables: number}}
 */
export function parseSheet(text) {
  const lines = text.split(/\r?\n/);
  const rows = [];
  let tables = 0;
  for (let i = 0; i < lines.length; i++) {
    if (!lines[i].trim().startsWith('|')) continue;
    const header = splitRow(lines[i]).map((h) => h.toLowerCase().replace(/\s+/g, ''));
    const sep = lines[i + 1] ?? '';
    if (!/^\s*\|?\s*:?-{2,}/.test(sep) || !SHEET_COLUMNS.every((c) => header.includes(c))) continue;
    tables++;
    let j = i + 2;
    for (; j < lines.length && lines[j].trim().startsWith('|'); j++) {
      const cells = splitRow(lines[j]);
      if (cells.every((c) => c === '')) continue;
      const rec = {};
      header.forEach((h, k) => { rec[h] = cells[k] ?? ''; });
      rows.push({ line: j + 1, cells: rec });
    }
    i = j - 1;
  }
  return { rows, tables };
}

function walk(dir, out = []) {
  let entries = [];
  try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return out; }
  for (const e of entries) {
    if (e.isDirectory()) { if (!SKIP_DIRS.has(e.name)) walk(path.join(dir, e.name), out); } else if (e.isFile()) out.push(path.join(dir, e.name));
  }
  return out;
}

/** Expand one file cell to absolute paths: `;`-separated entries, each a path or a glob, tried W-side then U-side. */
function resolveCell(cell, ws, unity, warnings, id) {
  const found = [];
  const unmatched = [];
  for (const raw of cell.split(';').map((s) => s.trim()).filter(Boolean)) {
    const entry = toPosix(raw).replace(/^\.\//, '');
    let hits = [];
    for (const base of unity === ws ? [ws] : [ws, unity]) {
      if (/[*?]/.test(entry)) {
        const fixed = entry.split('/').findIndex((seg) => /[*?]/.test(seg));
        const root = path.join(base, ...entry.split('/').slice(0, fixed));
        const re = globToRegExp(entry);
        // a glob names audio files only: Unity's .meta sidecars (and any other file) never count as references
        hits = walk(root).filter((f) => AUDIO_EXTS.has(path.extname(f).toLowerCase()) && re.test(relPosix(base, f)));
      } else {
        const abs = path.isAbsolute(entry) ? entry : path.join(base, entry);
        hits = fs.existsSync(abs) && fs.statSync(abs).isFile() ? [abs] : [];
      }
      if (hits.length) {
        if (base !== ws) warnings.push(`${id}: '${entry}' is Unity-project-relative; write it relative to the workspace`);
        break;
      }
    }
    if (hits.length) found.push(...hits); else unmatched.push(entry);
  }
  return { found, unmatched };
}

function loadLedger(csvPath) {
  if (!fs.existsSync(csvPath)) return { rows: [], missing: true };
  const { rows } = parseCsv(readText(csvPath));
  return { rows, missing: false };
}

function licenceRowFor(rel, ledger) {
  const key = cmpKey(rel);
  let best = null;
  for (const r of ledger) {
    const p = cmpKey(String(r.path || '').trim());
    if (!p) continue;
    if (p === key) return r;
    if (p.endsWith('/') && key.startsWith(p) && (!best || p.length > cmpKey(best.path).length)) best = r;
  }
  return best;
}

function rowErrors(cells, line, seen) {
  const errs = [];
  const id = cells.id;
  if (isEmpty(id) || !/^[A-Za-z0-9_.:-]+$/.test(id)) errs.push({ line, id, message: `bad id '${id}' (letters, digits, _ . : -)` });
  else if (seen.has(id)) errs.push({ line, id, message: `duplicate id (first on line ${seen.get(id)})` });
  else seen.set(id, line);
  const pr = cells.priority;
  if (!/^\d+$/.test(pr) || Number(pr) > 256) errs.push({ line, id, message: `priority '${pr}' must be an integer 0-256 (Unity AudioSource)` });
  const vo = cells.voices;
  if (!/^\d+$/.test(vo) || Number(vo) < 1) errs.push({ line, id, message: `voices '${vo}' must be an integer >= 1` });
  if (!YES_NO.has(cells.loop.toLowerCase())) errs.push({ line, id, message: `loop '${cells.loop}' must be yes or no` });
  if (!SPACE.has(cells['3d'].toLowerCase())) errs.push({ line, id, message: `3D '${cells['3d']}' must be yes/no (or 2d/3d)` });
  return errs;
}

/**
 * The check as a function (tests call it directly).
 * @param {{ws: string, sheet: string, csv: string, audioRoots: string[], unity: string}} o absolute paths
 */
export function checkManifest(o) {
  const res = { ok: false, sheet: relPosix(o.ws, o.sheet), rows: 0, files_checked: 0, missing_files: [], unlicensed: [], unused: [], licence_mismatch: [], row_errors: [], warnings: [] };
  if (!fs.existsSync(o.sheet)) {
    res.missing_sheet = true;
    res.error = `audio sheet not found: ${res.sheet} (copy the AUDIO_SHEET.md template first)`;
    return res;
  }
  const { rows, tables } = parseSheet(readText(o.sheet));
  if (!tables) res.row_errors.push({ line: 0, id: '', message: `no table with the columns | ${SHEET_COLUMNS.join(' | ')} |` });
  const ledger = loadLedger(o.csv);
  if (ledger.missing) res.warnings.push(`licence ledger not found: ${relPosix(o.ws, o.csv)}`);
  const seen = new Map();
  const referenced = new Set();
  for (const { line, cells } of rows) {
    res.rows++;
    res.row_errors.push(...rowErrors(cells, line, seen));
    const cat = cells.category.toLowerCase();
    if (cat && !KNOWN_CATEGORIES.has(cat)) res.warnings.push(`${cells.id}: unknown category '${cells.category}'`);
    if (isEmpty(cells.bus)) res.warnings.push(`${cells.id}: no mixer bus`);
    if (isEmpty(cells.file)) {
      res.missing_files.push({ id: cells.id, file: cells.file || '', reason: 'not sourced yet' });
      continue;
    }
    const { found, unmatched } = resolveCell(cells.file, o.ws, o.unity, res.warnings, cells.id);
    for (const u of unmatched) res.missing_files.push({ id: cells.id, file: u, reason: /[*?]/.test(u) ? 'glob matches nothing' : 'no such file' });
    for (const abs of found) {
      const rel = relPosix(o.ws, abs);
      referenced.add(cmpKey(rel));
      res.files_checked++;
      const lic = licenceRowFor(rel, ledger.rows);
      if (!lic) { res.unlicensed.push({ id: cells.id, file: rel }); continue; }
      const want = cells.licence;
      if (!isEmpty(want) && String(lic.licence_id || '').toLowerCase() !== want.toLowerCase()) {
        res.licence_mismatch.push({ id: cells.id, file: rel, sheet: want, ledger: lic.licence_id || '' });
      }
    }
  }
  for (const root of o.audioRoots) {
    for (const f of walk(root)) {
      if (!AUDIO_EXTS.has(path.extname(f).toLowerCase())) continue;
      const rel = relPosix(o.ws, f);
      if (!referenced.has(cmpKey(rel))) res.unused.push(rel);
    }
  }
  res.unused.sort();
  res.ok = !res.missing_files.length && !res.unlicensed.length && !res.unused.length && !res.licence_mismatch.length && !res.row_errors.length;
  return res;
}

export async function main(argv) {
  const { values } = parseCli(argv, {
    sheet: { type: 'string', default: 'Docs/audio/AUDIO_SHEET.md' },
    csv: { type: 'string', default: 'Docs/licenses.csv' },
    'audio-root': { type: 'string', multiple: true },
  });
  const ws = findWorkspace() ?? process.cwd();
  let unity = ws;
  try { unity = kitPaths(ws).unity; } catch (e) { throw new CliError(`kit.json: ${e.message}`, EXIT.INFRA); }
  const abs = (p) => (path.isAbsolute(p) ? p : path.join(ws, p));
  const roots = values['audio-root']?.length ? values['audio-root'].map(abs) : [path.join(unity, 'Assets', 'Audio')];
  const res = checkManifest({ ws, unity, sheet: abs(values.sheet), csv: abs(values.csv), audioRoots: roots });
  const lines = [
    `sheet ${res.sheet}: ${res.rows} rows, ${res.files_checked} files`,
    ...res.missing_files.map((m) => `MISSING    ${m.id}: ${m.file} (${m.reason})`),
    ...res.unlicensed.map((m) => `UNLICENSED ${m.id}: ${m.file}`),
    ...res.licence_mismatch.map((m) => `MISMATCH   ${m.id}: ${m.file} sheet=${m.sheet} ledger=${m.ledger}`),
    ...res.unused.map((u) => `UNUSED     ${u}`),
    ...res.row_errors.map((r) => `ROW ${r.line}  ${r.id}: ${r.message}`),
    ...res.warnings.map((w) => `warn: ${w}`),
    res.error ? `error: ${res.error}` : res.ok ? 'ok' : 'FAIL',
  ];
  emit(res, { json: values.json, text: lines });
  return res.ok ? EXIT.OK : EXIT.FAIL;
}

if (isMain(import.meta.url)) await runCli(main);

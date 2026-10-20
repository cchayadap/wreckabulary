// The licence ledger (Docs/licenses.csv) and the ship context shared by Tools/kit/ship/*.mjs (CONTRACTS.md §4.9, §7.6).
// Only license_row.mjs writes the CSV; every other tool reads it through readLedger().
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { findWorkspace, normalizeUnitySubdir, readKitConfig, toPosix, isInside, relPosix } from '../../lib/paths.mjs';
import { readJson, readJsonOr, readText } from '../../lib/jsonio.mjs';
import { validate, formatErrors } from '../../lib/schema.mjs';
import { parseCsv } from '../../lib/csv.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));

/**
 * Locate a kit schema: installed Tools/kit/schemas/ first, then the source layout skills/<owner>/assets/schemas/.
 * @param {string} file
 * @param {string} owner
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

const cache = new Map();
/** @param {string} file @param {string} owner */
export function loadKitSchema(file, owner) {
  const p = schemaFile(file, owner);
  if (!cache.has(p)) cache.set(p, readJson(p));
  return cache.get(p);
}

/** The licences schema (one CSV row; $defs release, packedAssets). */
export function licencesSchema() {
  return loadKitSchema('licenses.schema.json', 'game-ship');
}

/** The 19 column names in header order (licenses.schema.json x-columns). */
export function columns() {
  return [...licencesSchema()['x-columns']];
}

/**
 * Paths and optional documents every ship tool needs.
 * @param {{cwd?: string, csv?: string, release?: string, providers?: string}} [opts] explicit file flags override the defaults
 */
export function shipContext(opts = {}) {
  const cwd = path.resolve(opts.cwd || process.cwd());
  const ws = findWorkspace(cwd) || cwd;
  const installed = fs.existsSync(path.join(ws, 'Tools', 'kit', 'kit.json'));
  const sub = normalizeUnitySubdir(readKitConfig(ws)?.unity_subdir ?? '.');
  const unity = sub === '.' ? ws : path.join(ws, sub);
  const csv = opts.csv ? path.resolve(cwd, opts.csv) : path.join(ws, 'Docs', 'licenses.csv');
  const releaseFile = opts.release ? path.resolve(cwd, opts.release) : path.join(ws, 'Docs', 'ship', 'release.json');
  const providersFile = opts.providers ? path.resolve(cwd, opts.providers) : path.join(ws, 'Tools', 'kit', 'assetgen', 'providers.json');
  return { cwd, ws, installed, unitySubdir: sub, unity, csv, releaseFile, providersFile, docsShip: path.join(ws, 'Docs', 'ship') };
}

/**
 * Docs/ship/release.json validated against licenses.schema.json#/$defs/release.
 * @param {string} file
 * @returns {{release: object|null, errors: string[]}} release null when the file is missing or invalid
 */
export function loadRelease(file) {
  if (!fs.existsSync(file)) return { release: null, errors: [] };
  let doc;
  try { doc = readJson(file); } catch (e) { return { release: null, errors: [e.message] }; }
  const r = validate(licencesSchema(), doc, { pointer: '#/$defs/release' });
  return r.valid ? { release: doc, errors: [] } : { release: null, errors: [`release.json is invalid: ${formatErrors(r.errors)}`] };
}

/** providers.json (assetgen) or null. Not validated here: assetgen owns it. @param {string} file */
export function loadProviders(file) {
  return readJsonOr(file, null, { onInvalid: 'fallback' });
}

/** Normalise a ledger path: POSIX, no leading ./ or /. Folder rows keep their trailing slash. @param {string} p */
export function normRowPath(p) {
  let s = toPosix(String(p ?? '').trim());
  while (s.startsWith('./')) s = s.slice(2);
  return s;
}

/** Case-insensitive on Windows. @param {string} p */
export function pathKey(p) {
  const s = normRowPath(p);
  return process.platform === 'win32' ? s.toLowerCase() : s;
}

/**
 * Read Docs/licenses.csv.
 * @param {string} file
 * @returns {{exists: boolean, header: string[], rows: Record<string, string>[], headerErrors: string[]}}
 *   rows carry `__line` (1-based CSV record number, header = 1)
 */
export function readLedger(file) {
  if (!fs.existsSync(file)) return { exists: false, header: columns(), rows: [], headerErrors: [] };
  const { header, rows } = parseCsv(readText(file));
  const cols = columns();
  const headerErrors = [];
  if (header.length && header.join(',') !== cols.join(',')) headerErrors.push(`header differs from the ${cols.length} x-columns of licenses.schema.json`);
  rows.forEach((r, i) => { Object.defineProperty(r, '__line', { value: i + 2, enumerable: false }); });
  return { exists: true, header: header.length ? header : cols, rows, headerErrors };
}

/**
 * Schema errors for one row (licenses.schema.json root).
 * @param {Record<string, string>} row
 * @returns {string[]}
 */
export function rowErrors(row) {
  const plain = {};
  for (const c of Object.keys(row)) plain[c] = row[c];
  const r = validate(licencesSchema(), plain);
  return r.valid ? [] : r.errors.map((e) => `${e.path || '/'}: ${e.message}`);
}

/** W-relative POSIX path when inside ws, else absolute POSIX. @param {string} ws @param {string} abs */
export function relOrAbs(ws, abs) {
  const a = path.resolve(abs);
  return isInside(a, ws) ? relPosix(ws, a) : toPosix(a);
}

/**
 * Resolve a ledger row path to a file on disk: W-relative first; when the Unity project is a subfolder, a U-relative
 * spelling (Assets/…) is also tried.
 * @param {{ws: string, unity: string}} ctx
 * @param {string} rowPath
 * @returns {string|null}
 */
export function resolveRowFile(ctx, rowPath) {
  const p = normRowPath(rowPath);
  if (!p || p.endsWith('/')) return null;
  const a = path.join(ctx.ws, p);
  if (fs.existsSync(a)) return a;
  if (ctx.unity !== ctx.ws) {
    const b = path.join(ctx.unity, p);
    if (fs.existsSync(b)) return b;
  }
  return null;
}

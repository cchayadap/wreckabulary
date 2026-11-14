// RFC 4180 CSV for the kit's ledgers (Docs/licenses.csv, Docs/motion_sources.csv). Writes LF line endings, reads LF or CRLF.
import fs from 'node:fs';
import path from 'node:path';
import { withLockSync } from './locks.mjs';

/**
 * Parse CSV text into records. Strips a UTF-8 BOM. Quoted fields may hold commas, quotes ("") and newlines.
 * Throws on an unterminated quoted field.
 * @param {string} text
 * @returns {string[][]}
 */
export function parseCsvRecords(text) {
  const s = String(text).replace(/^﻿/, '');
  const records = [];
  let rec = [];
  let field = '';
  let i = 0;
  let inQuotes = false;
  let sawAny = false;
  while (i < s.length) {
    const c = s[i];
    sawAny = true;
    if (inQuotes) {
      if (c === '"') {
        if (s[i + 1] === '"') { field += '"'; i += 2; continue; }
        inQuotes = false; i++; continue;
      }
      field += c; i++; continue;
    }
    if (c === '"' && field === '') { inQuotes = true; i++; continue; }
    if (c === ',') { rec.push(field); field = ''; i++; continue; }
    if (c === '\r' && s[i + 1] === '\n') { rec.push(field); records.push(rec); rec = []; field = ''; i += 2; sawAny = false; continue; }
    if (c === '\n') { rec.push(field); records.push(rec); rec = []; field = ''; i++; sawAny = false; continue; }
    field += c; i++;
  }
  if (inQuotes) throw new Error('csv: unterminated quoted field');
  if (sawAny || field !== '' || rec.length) { rec.push(field); records.push(rec); }
  return records.filter((r) => !(r.length === 1 && r[0] === ''));
}

/**
 * Parse CSV with a header row into objects keyed by column name. Short rows are padded with ''.
 * Rows with more fields than the header throw (the ledger would be ambiguous).
 * @param {string} text
 * @returns {{header: string[], rows: Record<string, string>[]}}
 */
export function parseCsv(text) {
  const recs = parseCsvRecords(text);
  if (!recs.length) return { header: [], rows: [] };
  const header = recs[0].map((h) => h.trim());
  const rows = recs.slice(1).map((r, idx) => {
    if (r.length > header.length) throw new Error(`csv: row ${idx + 2} has ${r.length} fields, header has ${header.length}`);
    const o = {};
    header.forEach((h, j) => { o[h] = r[j] ?? ''; });
    return o;
  });
  return { header, rows };
}

/** Quote one field when needed (comma, quote, CR/LF, or leading/trailing space). @param {unknown} v */
export function csvField(v) {
  const s = v === null || v === undefined ? '' : String(v);
  return /[",\r\n]|^\s|\s$/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

/** One CSV line (no line ending). @param {unknown[]} values */
export function toCsvLine(values) {
  return values.map(csvField).join(',');
}

/**
 * Serialise rows in header order, LF endings, trailing LF.
 * @param {string[]} header
 * @param {Record<string, unknown>[]} rows
 */
export function toCsv(header, rows) {
  return [toCsvLine(header), ...rows.map((r) => toCsvLine(header.map((h) => r[h])))].join('\n') + '\n';
}

/**
 * Append one row under `<file>.lock`. Creates the file with the header when missing; refuses when the existing header differs.
 * @param {string} file
 * @param {string[]} header
 * @param {Record<string, unknown>} row keys outside the header throw
 */
export function appendCsvRow(file, header, row) {
  const extra = Object.keys(row).filter((k) => !header.includes(k));
  if (extra.length) throw new Error(`csv: unknown column(s) ${extra.join(', ')}`);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  withLockSync(`${file}.lock`, () => {
    if (!fs.existsSync(file) || fs.statSync(file).size === 0) {
      fs.writeFileSync(file, toCsvLine(header) + '\n', 'utf8');
    } else {
      const firstLine = fs.readFileSync(file, 'utf8').replace(/^﻿/, '').split(/\r?\n/, 1)[0];
      const have = parseCsvRecords(firstLine)[0] || [];
      if (have.join(',') !== header.join(',')) throw new Error(`csv: header mismatch in ${file}`);
      const text = fs.readFileSync(file, 'utf8');
      if (text.length && !text.endsWith('\n')) fs.appendFileSync(file, '\n', 'utf8');
    }
    fs.appendFileSync(file, toCsvLine(header.map((h) => row[h])) + '\n', 'utf8');
  });
}

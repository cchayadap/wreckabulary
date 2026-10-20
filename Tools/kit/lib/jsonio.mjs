// JSON, JSONL and text I/O with the kit's rules: UTF-8 without BOM, LF, atomic replace, append-only ledgers.
// Contract: CONTRACTS.md §1.4 (file rules) and §2 (lib API).
import fs from 'node:fs';
import path from 'node:path';

const sleepSync = (ms) => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);

function stripBom(text) {
  return text.charCodeAt(0) === 0xfeff ? text.slice(1) : text;
}

/**
 * Read a text file the way kit readers must: UTF-8 (BOM stripped), or UTF-16LE/BE when the file starts with that BOM
 * (Windows PowerShell 5.1 `>`/`Out-File` output), so such a file is read instead of silently parsing as nothing.
 * @param {string} file
 * @returns {string}
 */
export function readText(file) {
  const b = fs.readFileSync(file);
  if (b.length >= 2 && b[0] === 0xff && b[1] === 0xfe) return b.subarray(2).toString('utf16le');
  if (b.length >= 2 && b[0] === 0xfe && b[1] === 0xff) {
    const le = Buffer.from(b.subarray(2));
    le.swap16();
    return le.toString('utf16le');
  }
  return stripBom(b.toString('utf8'));
}

/**
 * Read and parse a JSON file (readText encodings). Throws with the path in the message when it is missing or invalid.
 * @param {string} file
 * @returns {any}
 */
export function readJson(file) {
  let text;
  try {
    text = readText(file);
  } catch (e) {
    const err = new Error(`readJson: cannot read ${file}: ${e.code || e.message}`);
    err.code = e.code;
    throw err;
  }
  try {
    return JSON.parse(stripBom(text));
  } catch (e) {
    const err = new Error(`readJson: invalid JSON in ${file}: ${e.message}`);
    err.code = 'EJSONPARSE';
    throw err;
  }
}

/**
 * Read JSON, returning `fallback` when the file is missing. Invalid JSON throws unless `onInvalid: 'fallback'`.
 * @param {string} file
 * @param {any} fallback
 * @param {{onInvalid?: 'throw'|'fallback'}} [opts]
 */
export function readJsonOr(file, fallback, opts = {}) {
  try {
    return readJson(file);
  } catch (e) {
    if (e.code === 'ENOENT' || e.code === 'ENOTDIR') return fallback;
    if (opts.onInvalid === 'fallback') return fallback;
    throw e;
  }
}

/** Serialise with sorted object keys (for hashing and stable diffs). @param {any} value @param {number} [indent] */
export function stableStringify(value, indent) {
  const sortKeys = (v) => {
    if (Array.isArray(v)) return v.map(sortKeys);
    if (v && typeof v === 'object' && v.constructor === Object) {
      const out = {};
      for (const k of Object.keys(v).sort()) out[k] = sortKeys(v[k]);
      return out;
    }
    return v;
  };
  return JSON.stringify(sortKeys(value), null, indent);
}

/**
 * Write text atomically: temp file in the same folder, then rename over the target (retried on Windows sharing errors).
 * Creates parent folders.
 * @param {string} file
 * @param {string} text
 */
export function writeTextAtomic(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const tmp = `${file}.tmp-${process.pid}-${Math.random().toString(36).slice(2, 8)}`;
  fs.writeFileSync(tmp, text, 'utf8');
  for (let i = 0; ; i++) {
    try {
      fs.renameSync(tmp, file);
      return;
    } catch (e) {
      if (i >= 8 || !['EPERM', 'EBUSY', 'EACCES'].includes(e.code)) {
        try { fs.unlinkSync(tmp); } catch { /* already gone */ }
        throw e;
      }
      sleepSync(25 * (i + 1));
    }
  }
}

/**
 * Write JSON atomically with 2-space indent and a trailing LF. Key order is preserved (not sorted).
 * @param {string} file
 * @param {any} value
 * @param {{indent?: number}} [opts]
 */
export function writeJsonAtomic(file, value, opts = {}) {
  writeTextAtomic(file, JSON.stringify(value, null, opts.indent ?? 2) + '\n');
}

/**
 * Append one object as one JSONL line (single write, O_APPEND). Creates parent folders.
 * @param {string} file
 * @param {object} obj
 */
export function appendJsonl(file, obj) {
  if (obj === null || typeof obj !== 'object' || Array.isArray(obj)) throw new TypeError('appendJsonl: expects a plain object');
  const line = JSON.stringify(obj);
  if (line.length > 1 << 20) throw new RangeError(`appendJsonl: line over 1 MiB for ${file}`);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.appendFileSync(file, line + '\n', 'utf8');
}

/**
 * Read a JSONL file into an array of objects (readText encodings). Missing file → []. Blank lines are skipped.
 * Invalid lines are skipped unless `strict` (then it throws with the 1-based line number).
 * @param {string} file
 * @param {{strict?: boolean}} [opts]
 * @returns {any[]}
 */
export function readJsonl(file, opts = {}) {
  let text;
  try {
    text = readText(file);
  } catch (e) {
    if (e.code === 'ENOENT') return [];
    throw e;
  }
  const out = [];
  const lines = text.split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    const l = lines[i].trim();
    if (!l) continue;
    try {
      out.push(JSON.parse(l));
    } catch (e) {
      if (opts.strict) {
        const err = new Error(`readJsonl: ${file}:${i + 1}: ${e.message}`);
        err.code = 'EJSONPARSE';
        throw err;
      }
    }
  }
  return out;
}

/** Last JSONL object matching `pred` (or the last object), or null. @param {string} file @param {(o:any)=>boolean} [pred] */
export function lastJsonl(file, pred = () => true) {
  const rows = readJsonl(file);
  for (let i = rows.length - 1; i >= 0; i--) if (pred(rows[i])) return rows[i];
  return null;
}

/**
 * Read all of stdin and parse it as JSON (hook input). Never throws:
 * empty stdin → {}; invalid JSON → { __invalid: true, raw }; no EOF within timeoutMs → what was read so far.
 * @param {{timeoutMs?: number, stream?: NodeJS.ReadableStream}} [opts]
 * @returns {Promise<any>}
 */
export function readStdinJson(opts = {}) {
  const stream = opts.stream || process.stdin;
  const timeoutMs = opts.timeoutMs ?? 5000;
  return new Promise((resolve) => {
    const chunks = [];
    let done = false;
    const finish = () => {
      if (done) return;
      done = true;
      clearTimeout(timer);
      const raw = stripBom(Buffer.concat(chunks).toString('utf8')).trim();
      if (!raw) return resolve({});
      try {
        const v = JSON.parse(raw);
        resolve(v && typeof v === 'object' ? v : { __invalid: true, raw });
      } catch {
        resolve({ __invalid: true, raw });
      }
    };
    const timer = setTimeout(() => {
      finish();
      try { stream.pause?.(); } catch { /* ignore */ }
    }, timeoutMs);
    if (stream.isTTY) return finish();
    stream.on('data', (c) => chunks.push(Buffer.isBuffer(c) ? c : Buffer.from(c)));
    stream.on('end', finish);
    stream.on('error', finish);
  });
}

/** The one-line JSON result a CLI prints with --json. @param {object} obj @returns {string} */
export function toJsonLine(obj) {
  return JSON.stringify(obj);
}

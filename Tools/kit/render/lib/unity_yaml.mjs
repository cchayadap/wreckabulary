// unity_yaml.mjs: a small reader for the subset of Unity's text serialisation (force-text YAML) that render_lint needs.
// It is not a YAML parser. Unity writes one object per `--- !u!<classId> &<fileId>` document, fields at fixed indents,
// sequences at the same indent as their key, and wraps long flow maps (`{fileID: 1, guid: …,\n      type: 2}`) onto
// the next line. These helpers read top-level fields, flow-map references and simple sequences from that layout.

const DOC_HEAD = /^--- !u!(\d+) &(-?\d+)(?: stripped)?[ \t]*$/gm;
export const NULL_GUID = '00000000000000000000000000000000';
/** Guid of Unity's built-in resources ("Library/unity default resources"); references into it are not project assets. */
export const BUILTIN_GUID = '0000000000000000f000000000000000';

/** Normalise line endings and drop a BOM. @param {string} text */
export function normalise(text) {
  return String(text).replace(/^﻿/, '').replace(/\r\n?/g, '\n');
}

/**
 * Split a Unity YAML file into documents.
 * @param {string} text
 * @returns {{classId: number, fileId: string, type: string, body: string, line: number}[]} line: 1-based line of the header
 */
export function splitDocs(text) {
  const t = normalise(text);
  const heads = [...t.matchAll(DOC_HEAD)];
  const docs = [];
  for (let i = 0; i < heads.length; i++) {
    const h = heads[i];
    const start = h.index + h[0].length + 1;
    const end = i + 1 < heads.length ? heads[i + 1].index : t.length;
    const body = t.slice(start, end);
    const type = /^([A-Za-z_][\w]*):/.exec(body)?.[1] ?? '';
    docs.push({ classId: Number(h[1]), fileId: h[2], type, body, line: lineAt(t, h.index) });
  }
  return docs;
}

/** 1-based line number of an offset. @param {string} text @param {number} offset */
export function lineAt(text, offset) {
  let n = 1;
  for (let i = 0; i < offset && i < text.length; i++) if (text.charCodeAt(i) === 10) n++;
  return n;
}

/**
 * Join a flow map or flow sequence that Unity wrapped over several lines.
 * @param {string[]} lines @param {number} i index of the line holding the key
 * @returns {{value: string, next: number}} the joined value and the index after its last line
 */
function joinWrapped(lines, i, value) {
  let v = value;
  let j = i + 1;
  const open = (s) => (s.match(/[{[]/g) || []).length - (s.match(/[}\]]/g) || []).length;
  while (open(v) > 0 && j < lines.length) {
    v += ' ' + lines[j].trim();
    j++;
  }
  return { value: v.trim(), next: j };
}

/**
 * Read the scalar or flow value of `key` at exactly `indent` spaces in a document body (default 2: a component's own
 * fields). Returns null when absent. Wrapped flow maps are joined.
 * @param {string} body @param {string} key @param {number} [indent]
 */
export function field(body, key, indent = 2) {
  const lines = body.split('\n');
  const pad = ' '.repeat(indent);
  const prefix = `${pad}${key}:`;
  for (let i = 0; i < lines.length; i++) {
    const l = lines[i];
    if (!l.startsWith(prefix)) continue;
    if (l.length > prefix.length && l[prefix.length] !== ' ') continue;
    return joinWrapped(lines, i, l.slice(prefix.length).trim()).value;
  }
  return null;
}

/** Numeric field or null. @param {string} body @param {string} key @param {number} [indent] */
export function num(body, key, indent = 2) {
  const v = field(body, key, indent);
  if (v === null || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

/**
 * Parse a flow-map object reference `{fileID: 11400000, guid: abc…, type: 2}`.
 * @param {string|null} v
 * @returns {{fileID: string, guid: string|null, type: number|null}|null} null for a missing value
 */
export function parseRef(v) {
  if (!v) return null;
  const m = /\{\s*fileID:\s*(-?\d+)(?:\s*,\s*guid:\s*([0-9a-fA-F]{32}))?(?:\s*,\s*type:\s*(\d+))?\s*\}/.exec(v);
  if (!m) return null;
  return { fileID: m[1], guid: m[2] ? m[2].toLowerCase() : null, type: m[3] ? Number(m[3]) : null };
}

/** True for a reference that points at nothing (`{fileID: 0}`). @param {{fileID: string}|null} r */
export function isNullRef(r) {
  return !r || r.fileID === '0';
}

/** True for a reference into Unity's built-in resources. @param {{guid: string|null}|null} r */
export function isBuiltinRef(r) {
  return !!r && r.guid === BUILTIN_GUID;
}

/**
 * Items of a block sequence under `key` (Unity writes `key:` then `- item` lines at the same indent).
 * Each item is returned as raw text with continuation lines joined; `key: []` gives [].
 * @param {string} body @param {string} key @param {number} [indent]
 * @returns {string[]|null} null when the key is absent
 */
export function seq(body, key, indent = 2) {
  const lines = body.split('\n');
  const pad = ' '.repeat(indent);
  const prefix = `${pad}${key}:`;
  for (let i = 0; i < lines.length; i++) {
    const l = lines[i];
    if (!l.startsWith(prefix) || (l.length > prefix.length && l[prefix.length] !== ' ')) continue;
    const inline = l.slice(prefix.length).trim();
    if (inline === '[]') return [];
    if (inline.startsWith('[')) {
      return joinWrapped(lines, i, inline).value.replace(/^\[|\]$/g, '').split(',').map((s) => s.trim()).filter(Boolean);
    }
    const items = [];
    let j = i + 1;
    while (j < lines.length) {
      const s = lines[j];
      if (s.startsWith(`${pad}- `)) {
        const w = joinWrapped(lines, j, s.slice(indent + 2));
        items.push(w.value);
        j = w.next;
        // continuation of a multi-line item: deeper-indented lines that are not a new item
        while (j < lines.length && lines[j].startsWith(`${pad}  `) && !lines[j].startsWith(`${pad}- `)) {
          items[items.length - 1] += '\n' + lines[j].slice(indent + 2);
          j++;
        }
        continue;
      }
      break;
    }
    return items;
  }
  return null;
}

/**
 * Items of a sequence of mappings under `key` (for example QualitySettings.m_QualitySettings). Each item becomes a
 * body whose fields sit at indent 2, so field()/seq() work on it unchanged.
 * @param {string} body @param {string} key @param {number} [indent]
 * @returns {string[]} [] when absent
 */
export function mapSeq(body, key, indent = 2) {
  const lines = body.split('\n');
  const pad = ' '.repeat(indent);
  const prefix = `${pad}${key}:`;
  const start = lines.findIndex((l) => l.startsWith(prefix) && (l.length === prefix.length || l[prefix.length] === ' '));
  if (start < 0) return [];
  const items = [];
  let cur = null;
  for (let j = start + 1; j < lines.length; j++) {
    const s = lines[j];
    if (s.startsWith(`${pad}- `)) {
      if (cur) items.push(cur.join('\n'));
      cur = [`  ${s.slice(indent + 2)}`];
      continue;
    }
    if (cur && s.startsWith(`${pad}  `)) {
      cur.push(`  ${s.slice(indent + 2)}`);
      continue;
    }
    break;
  }
  if (cur) items.push(cur.join('\n'));
  return items;
}

/** The m_Script guid of a MonoBehaviour document body, or null. @param {string} body */
export function scriptGuid(body) {
  return parseRef(field(body, 'm_Script'))?.guid ?? null;
}

/** The guid in a `.meta` file's text, or null. @param {string} text */
export function metaGuid(text) {
  return /^guid:\s*([0-9a-fA-F]{32})\s*$/m.exec(normalise(text))?.[1]?.toLowerCase() ?? null;
}

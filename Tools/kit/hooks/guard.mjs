// PreToolUse guard (ARCHITECTURE §6.3 rules G1–G14 plus G5b, G10b and CONTRACTS.md G15). Defence in depth, not a sandbox.
// stdout: nothing (pass) or one JSON object with permissionDecision deny|ask and a reason starting "[G<n>] ".
// The guard never emits "allow". Internal errors are logged to .harness/guard_errors.jsonl and pass (exit 0).
// Rule order (first match wins): G0, G12, G15, G16, G14, G5b, G6, G5, G1, G9, G10, G10b, G11, G3, G4, G7, G8, G2, G13.
// Single-use tokens (G1 override, G8 reservation) are claimed under a lock only when the final decision is pass.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import vm from 'node:vm';
import { now, utcIso, parseUtc, parseFileStamp } from '../lib/time.mjs';
import { withLockSync } from '../lib/locks.mjs';
import { readJsonl, readJsonOr, appendJsonl, writeJsonAtomic, readText } from '../lib/jsonio.mjs';
import { fromShellPath, isInside, wsRel, matchesAny, globToRegExp, kitCodeRoots, OWNER_CONTROL_FILES, scratchRoot, homeDir, toPosix } from '../lib/paths.mjs';
import { isMain } from '../lib/cli.mjs';
import { runHook, loadPolicy, isSecretName, isObj, exists } from './lib/harness.mjs';

const MAX_DEPTH = 4;
const SHELL_TOOLS = new Set(['Bash', 'PowerShell']);
const EDIT_TOOLS = new Set(['Edit', 'Write', 'MultiEdit', 'NotebookEdit']);

// =================================================================================================================
// Shell lexer

/**
 * @typedef {{text: string, quoted: boolean, start: number, end: number}} Word
 * @typedef {{op: string, fd: string|null, target: string|null, dup: boolean, input: boolean}} Redir
 * @typedef {{shell: 'bash'|'ps'|'cmd', depth: number, src: string, start: number, end: number, raw: string,
 *   words: Word[], redirs: Redir[], subst: boolean, bg: boolean, pipeNext: boolean, pipePrev: boolean,
 *   heredoc: string, parent: object|null, cwd?: string}} Segment
 */

const isWs = (c) => c === ' ' || c === '\t' || c === '\r' || c === '\f' || c === '\v';

/** Index of the `"` that closes a double-quoted string starting at j (after the opening quote). */
function scanDq(src, j, shell) {
  for (let k = j; k < src.length; k++) {
    const c = src[k];
    if (shell === 'bash' && c === '\\') { k++; continue; }
    if (shell === 'ps' && c === '`') { k++; continue; }
    if (c === '"') {
      if ((shell === 'ps') && src[k + 1] === '"') { k++; continue; }
      return k;
    }
  }
  return src.length;
}

/** Index of the `)` matching an opening `(` whose body starts at j. */
function scanParen(src, j, shell) {
  let d = 1;
  for (let k = j; k < src.length; k++) {
    const c = src[k];
    if (shell === 'bash' && c === '\\') { k++; continue; }
    if (shell === 'ps' && c === '`') { k++; continue; }
    if (c === "'" && shell !== 'cmd') {
      const e = src.indexOf("'", k + 1);
      if (e < 0) return src.length;
      k = e;
      continue;
    }
    if (c === '"') { k = scanDq(src, k + 1, shell); continue; }
    if (c === '(') d++;
    else if (c === ')') {
      d--;
      if (d === 0) return k;
    }
  }
  return src.length;
}

function scanBrace(src, j, shell) {
  let d = 1;
  for (let k = j; k < src.length; k++) {
    const c = src[k];
    if (c === "'" && shell !== 'cmd') { const e = src.indexOf("'", k + 1); if (e < 0) return src.length; k = e; continue; }
    if (c === '"') { k = scanDq(src, k + 1, shell); continue; }
    if (c === '{') d++;
    else if (c === '}') { d--; if (d === 0) return k; }
  }
  return src.length;
}

function scanBacktick(src, j) {
  for (let k = j; k < src.length; k++) {
    if (src[k] === '\\') { k++; continue; }
    if (src[k] === '`') return k;
  }
  return src.length;
}

/** Decode one bash ANSI-C escape ($'…') starting after the backslash at j: [text, chars consumed]. */
function ansiEscape(src, j) {
  const d = src[j];
  const simple = { a: '\x07', b: '\b', e: '\x1b', E: '\x1b', f: '\f', n: '\n', r: '\r', t: '\t', v: '\v', '\\': '\\', "'": "'", '"': '"', '?': '?' };
  if (d === undefined) return ['\\', 0];
  if (simple[d] !== undefined) return [simple[d], 1];
  const num = (re, max, base) => {
    let k = j + 1;
    let s = '';
    while (k < src.length && s.length < max && re.test(src[k])) s += src[k++];
    return s ? [String.fromCodePoint(Math.min(parseInt(s, base), 0x10ffff)), 1 + s.length] : null;
  };
  if (d === 'x') return num(/[0-9a-fA-F]/, 2, 16) ?? ['\\x', 1];
  if (d === 'u') return num(/[0-9a-fA-F]/, 4, 16) ?? ['\\u', 1];
  if (d === 'U') return num(/[0-9a-fA-F]/, 8, 16) ?? ['\\U', 1];
  if (/[0-7]/.test(d)) {
    let s = '';
    let k = j;
    while (k < src.length && s.length < 3 && /[0-7]/.test(src[k])) s += src[k++];
    return [String.fromCharCode(parseInt(s, 8) & 0xff), s.length];
  }
  if (d === 'c' && src[j + 1] !== undefined) return [String.fromCharCode(src[j + 1].toUpperCase().charCodeAt(0) ^ 0x40), 2];
  return ['\\' + d, 1];
}

const SEQ_RE = /^(-?\d+)\.\.(-?\d+)(?:\.\.(-?\d+))?$|^([A-Za-z])\.\.([A-Za-z])(?:\.\.(-?\d+))?$/;
const BRACE_MAX = 64;

function braceSeq(body) {
  const m = SEQ_RE.exec(body);
  if (!m) return null;
  const out = [];
  if (m[1] !== undefined) {
    const a = Number(m[1]);
    const b = Number(m[2]);
    const st = Math.abs(Number(m[3] || 1)) || 1;
    for (let v = a; (a <= b ? v <= b : v >= b) && out.length < BRACE_MAX; v += a <= b ? st : -st) out.push(String(v));
  } else {
    const a = m[4].charCodeAt(0);
    const b = m[5].charCodeAt(0);
    const st = Math.abs(Number(m[6] || 1)) || 1;
    for (let v = a; (a <= b ? v <= b : v >= b) && out.length < BRACE_MAX; v += a <= b ? st : -st) out.push(String.fromCharCode(v));
  }
  return out;
}

/**
 * Bash brace expansion of one word. toks: [char, active] pairs, where active marks an unquoted `{`, `,` or `}`.
 * Returns at most BRACE_MAX words; `budget` bounds the work on hostile input.
 */
function braceExpand(toks, budget = { n: 256 }) {
  if (--budget.n < 0) return [toks.map((t) => t[0]).join('')];
  const open = [];
  const pairs = [];
  for (let k = 0; k < toks.length; k++) {
    const [ch, act] = toks[k];
    if (!act) continue;
    if (ch === '{') open.push({ a: k, commas: [] });
    else if (ch === ',' && open.length) open[open.length - 1].commas.push(k);
    else if (ch === '}' && open.length) {
      const o = open.pop();
      pairs.push({ ...o, b: k });
    }
  }
  pairs.sort((p, q) => p.a - q.a);
  for (const p of pairs) {
    let alts;
    if (p.commas.length) {
      alts = [];
      let s = p.a + 1;
      for (const c of [...p.commas, p.b]) { alts.push(toks.slice(s, c)); s = c + 1; }
    } else {
      const seq = braceSeq(toks.slice(p.a + 1, p.b).map((t) => t[0]).join(''));
      if (!seq) continue;
      alts = seq.map((s) => [...s].map((ch) => [ch, false]));
    }
    const pre = toks.slice(0, p.a);
    const post = toks.slice(p.b + 1);
    const out = [];
    for (const alt of alts) {
      for (const w of braceExpand([...pre, ...alt, ...post], budget)) {
        out.push(w);
        if (out.length >= BRACE_MAX) return out;
      }
    }
    return out;
  }
  return [toks.map((t) => t[0]).join('')];
}

let SCRIPT_SEQ = 0;
let PIPE_SEQ = 0;
let SUB_SEQ = 0;

/**
 * Parse a shell command into segments (CONTRACTS.md §5.3): split on ; && || | newlines and a single &; the bodies of
 * $( ), <( ), >( ), backticks (bash), $( ) and @( ) (PowerShell) are segments of their own; so is the script of a
 * nested shell (bash -c, cmd /c, powershell -Command, Invoke-Expression, a heredoc fed to bash).
 * @param {string} src
 * @param {'bash'|'ps'|'cmd'} [shell]
 * @returns {Segment[]} flat list; depth 0 segments first, in order
 */
export function parseShell(src, shell = 'bash') {
  const out = [];
  lexInto(String(src ?? ''), shell, 0, null, out);
  return out;
}

function lexInto(src, shell, depth, parent, out) {
  const n = src.length;
  const segs = [];
  const deeper = [];
  let seg = null;
  let word = null;
  let pending = null;
  const heredocs = [];
  let i = 0;
  // script: this lexed text; pipe: pipeline group (a | b | c share it); subs: open bash ( … ) subshells.
  const script = ++SCRIPT_SEQ;
  let pipe = ++PIPE_SEQ;
  const subs = [];

  const mkSeg = (start) => ({ shell, depth, src, start, end: start, raw: '', words: [], redirs: [], subst: false, bg: false, pipeNext: false, pipePrev: false, heredoc: '', parent, script, pipe, subs: subs.slice(), order: 0 });
  seg = mkSeg(0);
  let order = 0;
  const ensureWord = (at) => { if (!word) word = { text: '', quoted: false, start: at, end: at, acts: null }; };
  /** Append one unquoted character; a bash unquoted `{`, `,` or `}` is remembered (by position) for brace expansion. */
  const addRaw = (ch) => {
    if (shell === 'bash' && (ch === '{' || ch === ',' || ch === '}')) (word.acts || (word.acts = new Set())).add(word.text.length);
    word.text += ch;
  };
  /** Brace-expanded spellings of a bash word (unquoted `{a,b}` / `{1..3}`), or [text]. Over-expansion only adds candidates. */
  const wordAlts = (w) => {
    if (shell !== 'bash' || !w.acts || !w.acts.size) return [w.text];
    const toks = [];
    for (let k = 0; k < w.text.length; k++) toks.push([w.text[k], w.acts.has(k)]);
    const alts = braceExpand(toks);
    return alts.length ? alts : [w.text];
  };
  const pushWord = (w) => {
    for (const alt of wordAlts(w)) seg.words.push(alt === w.text && w.alts === undefined ? w : { text: alt, quoted: w.quoted, start: w.start, end: w.end });
  };
  const endWord = (at) => {
    if (!word) return;
    word.end = at;
    if (pending) {
      const alts = wordAlts(word);
      pending.target = alts[0];
      seg.redirs.push(pending);
      for (let k = 1; k < alts.length; k++) seg.redirs.push({ ...pending, target: alts[k] });
      pending = null;
    } else if (!(shell === 'bash' && !word.quoted && (word.text === '{' || word.text === '}'))) {
      pushWord(word);
    }
    word = null;
  };
  const endSeg = (at, sep, nextStart) => {
    endWord(at);
    if (pending) { seg.redirs.push(pending); pending = null; }
    seg.end = at;
    seg.raw = src.slice(seg.start, seg.end);
    seg.order = order++;
    const has = seg.words.length > 0 || seg.redirs.length > 0;
    const prev = seg;
    if (has) segs.push(seg);
    seg = mkSeg(nextStart);
    if (sep === '|') { if (has) { prev.pipeNext = true; seg.pipePrev = true; } } else pipe = ++PIPE_SEQ;
    seg.pipe = pipe;
    if (sep === '&') { if (has) prev.bg = true; seg.bg = true; }
  };
  const subst = (body) => {
    seg.subst = true;
    if (depth + 1 < MAX_DEPTH) lexInto(body, shell, depth + 1, seg, deeper);
  };

  while (i < n) {
    const c = src[i];
    // ---- quotes
    if (c === "'" && shell !== 'cmd') {
      ensureWord(i);
      word.quoted = true;
      let e;
      if (shell === 'ps') {
        let k = i + 1;
        let t = '';
        for (; k < n; k++) {
          if (src[k] === "'") {
            if (src[k + 1] === "'") { t += "'"; k++; continue; }
            break;
          }
          t += src[k];
        }
        word.text += t;
        e = k;
      } else {
        e = src.indexOf("'", i + 1);
        if (e < 0) e = n;
        word.text += src.slice(i + 1, e);
      }
      i = e + 1;
      continue;
    }
    if (c === '"') {
      ensureWord(i);
      word.quoted = true;
      let k = i + 1;
      for (; k < n; k++) {
        const d = src[k];
        if (d === '"') {
          if (shell === 'ps' && src[k + 1] === '"') { word.text += '"'; k++; continue; }
          break;
        }
        if (shell === 'bash' && d === '\\' && k + 1 < n && '"\\$`\n'.includes(src[k + 1])) { if (src[k + 1] !== '\n') word.text += src[k + 1]; k++; continue; }
        if (shell === 'ps' && d === '`' && k + 1 < n) { word.text += src[k + 1]; k++; continue; }
        if (shell !== 'cmd' && d === '$' && src[k + 1] === '(') {
          const e = scanParen(src, k + 2, shell);
          subst(src.slice(k + 2, e));
          word.text += src.slice(k, Math.min(n, e + 1));
          k = e;
          continue;
        }
        if (shell === 'bash' && d === '`') {
          const e = scanBacktick(src, k + 1);
          subst(src.slice(k + 1, e));
          word.text += src.slice(k, Math.min(n, e + 1));
          k = e;
          continue;
        }
        word.text += d;
      }
      i = k + 1;
      continue;
    }
    // ---- PowerShell here-strings @' … '@ and @" … "@
    if (shell === 'ps' && c === '@' && (src[i + 1] === "'" || src[i + 1] === '"') && !word) {
      const q = src[i + 1];
      const nl = src.indexOf('\n', i + 2);
      if (nl >= 0 && src.slice(i + 2, nl).trim() === '') {
        const close = src.indexOf(`\n${q}@`, nl);
        const e = close < 0 ? n : close;
        ensureWord(i);
        word.quoted = true;
        word.text += src.slice(nl + 1, e).replace(/\r$/, '');
        i = close < 0 ? n : close + 3;
        continue;
      }
    }
    // ---- escapes: outside quotes bash drops the backslash and keeps the next character (so `cla\ude` == `claude`).
    if (shell === 'bash' && c === '\\') {
      const d = src[i + 1];
      if (d === '\n') { i += 2; continue; }
      if (d !== undefined) { ensureWord(i); word.text += d; word.quoted = true; i += 2; continue; }
      ensureWord(i);
      word.text += '\\';
      i++;
      continue;
    }
    if (shell === 'ps' && c === '`') {
      const d = src[i + 1];
      if (d === '\n' || (d === '\r' && src[i + 2] === '\n')) { i += d === '\r' ? 3 : 2; continue; }
      if (d !== undefined) { ensureWord(i); word.text += d; i += 2; continue; }
      i++;
      continue;
    }
    if (shell === 'cmd' && c === '^') {
      const d = src[i + 1];
      if (d !== undefined && d !== '\n') { ensureWord(i); word.text += d; i += 2; continue; }
      i += 2;
      continue;
    }
    // ---- comments
    if (c === '#' && !word && shell !== 'cmd') {
      const e = src.indexOf('\n', i);
      i = e < 0 ? n : e;
      continue;
    }
    if (shell === 'ps' && c === '<' && src[i + 1] === '#') {
      const e = src.indexOf('#>', i + 2);
      i = e < 0 ? n : e + 2;
      continue;
    }
    // ---- bash ANSI-C ($'…') and locale ($"…") quoting
    if (shell === 'bash' && c === '$' && src[i + 1] === "'") {
      ensureWord(i);
      word.quoted = true;
      let k = i + 2;
      for (; k < n; k++) {
        if (src[k] === '\\') { const [txt, used] = ansiEscape(src, k + 1); word.text += txt; k += used; continue; }
        if (src[k] === "'") break;
        word.text += src[k];
      }
      i = k + 1;
      continue;
    }
    if (shell === 'bash' && c === '$' && src[i + 1] === '"') { i++; continue; }
    // ---- $-forms
    if (c === '$' && shell !== 'cmd') {
      if (src[i + 1] === '(') {
        const e = scanParen(src, i + 2, shell);
        ensureWord(i);
        subst(src.slice(i + 2, e));
        word.text += src.slice(i, Math.min(n, e + 1));
        i = e + 1;
        continue;
      }
      if (src[i + 1] === '{') {
        const e = scanBrace(src, i + 2, shell);
        ensureWord(i);
        word.text += src.slice(i, Math.min(n, e + 1));
        i = e + 1;
        continue;
      }
    }
    if (shell === 'ps' && c === '@' && (src[i + 1] === '(' || src[i + 1] === '{')) {
      ensureWord(i);
      if (src[i + 1] === '(') {
        const e = scanParen(src, i + 2, shell);
        subst(src.slice(i + 2, e));
        word.text += src.slice(i, Math.min(n, e + 1));
        i = e + 1;
      } else {
        const e = scanBrace(src, i + 2, shell);
        word.text += src.slice(i, Math.min(n, e + 1));
        i = e + 1;
      }
      continue;
    }
    if (shell === 'bash' && c === '`') {
      const e = scanBacktick(src, i + 1);
      ensureWord(i);
      subst(src.slice(i + 1, e));
      word.text += src.slice(i, Math.min(n, e + 1));
      i = e + 1;
      continue;
    }
    if (shell === 'bash' && (c === '<' || c === '>') && src[i + 1] === '(') {
      const e = scanParen(src, i + 2, shell);
      ensureWord(i);
      subst(src.slice(i + 2, e));
      word.text += src.slice(i, Math.min(n, e + 1));
      i = e + 1;
      continue;
    }
    // ---- whitespace and separators
    if (isWs(c)) { endWord(i); i++; continue; }
    if (c === '\n') {
      endSeg(i, 'nl', i + 1);
      i++;
      if (heredocs.length) {
        for (const h of heredocs) {
          const lines = [];
          let found = false;
          while (i < n) {
            let e = src.indexOf('\n', i);
            if (e < 0) e = n;
            let line = src.slice(i, e).replace(/\r$/, '');
            i = Math.min(n, e + 1);
            const cmp = h.strip ? line.replace(/^\t+/, '') : line;
            if (cmp === h.delim) { found = true; break; }
            lines.push(line);
          }
          h.seg.heredoc += (h.seg.heredoc ? '\n' : '') + lines.join('\n');
          if (!found) break;
        }
        heredocs.length = 0;
        seg = mkSeg(i);
      }
      continue;
    }
    if (c === ';' && shell !== 'cmd') { endSeg(i, ';', i + 1); i++; continue; }
    if (c === '|') {
      if (src[i + 1] === '|') { endSeg(i, '||', i + 2); i += 2; continue; }
      const skip = shell === 'bash' && src[i + 1] === '&' ? 2 : 1;
      endSeg(i, '|', i + skip);
      i += skip;
      continue;
    }
    if (c === '&') {
      if (src[i + 1] === '&') { endSeg(i, '&&', i + 2); i += 2; continue; }
      if (shell === 'bash' && src[i + 1] === '>') {
        endWord(i);
        const app = src[i + 2] === '>';
        pending = { op: app ? '&>>' : '&>', fd: 'all', target: null, dup: false, input: false };
        i += app ? 3 : 2;
        continue;
      }
      if (shell === 'ps' && !word && seg.words.length === 0 && seg.redirs.length === 0) {
        seg.words.push({ text: '&', quoted: false, start: i, end: i + 1 });
        i++;
        continue;
      }
      endSeg(i, '&', i + 1);
      i++;
      continue;
    }
    if (c === '(' || c === ')') {
      if (shell === 'ps' && word && c === '(' && /(::|\.)[A-Za-z_][A-Za-z0-9_]*$/.test(word.text)) {
        // PowerShell method call [T]::M( … ) or $o.M( … ): the argument list stays in this segment (so .NET file API
        // checks see its string literals) and is also lexed as a nested segment.
        const e = scanParen(src, i + 1, shell);
        subst(src.slice(i + 1, e));
        word.text += src.slice(i, Math.min(n, e + 1));
        i = e + 1;
        continue;
      }
      if (shell === 'ps' && c === '(' && !word && seg.words.length > 0) {
        // A parenthesised argument, e.g. Set-Content -Path (Join-Path .harness PAUSE): keep ( … ) as this word (so it is
        // the parameter value) and lex the body as a nested segment. writeWords/G4 evaluate parenExpr(word).
        const e = scanParen(src, i + 1, shell);
        subst(src.slice(i + 1, e));
        ensureWord(i);
        word.text += src.slice(i, Math.min(n, e + 1));
        word.paren = src.slice(i + 1, e);
        i = e + 1;
        continue;
      }
      if (shell === 'bash' && word && !word.quoted && c === '(' && /^[A-Za-z_][A-Za-z0-9_]*=$/.test(word.text)) {
        // bash array assignment a=( … ): keep as one word
        const e = scanParen(src, i + 1, shell);
        word.text += src.slice(i, Math.min(n, e + 1));
        i = e + 1;
        continue;
      }
      endSeg(i, c, i + 1);
      if (shell === 'bash') {
        if (c === '(') subs.push(++SUB_SEQ);
        else if (subs.length) subs.pop();
        seg.subs = subs.slice();
      }
      i++;
      continue;
    }
    if (shell === 'ps' && (c === '{' || c === '}')) { endSeg(i, c, i + 1); i++; continue; }
    // ---- redirections
    if (c === '>' || (c === '<' && shell !== 'ps')) {
      let fd = null;
      if (word && !word.quoted && (/^\d+$/.test(word.text) || (shell === 'ps' && word.text === '*')) && word.start + word.text.length === i) {
        fd = word.text;
        word = null;
      } else endWord(i);
      if (c === '<') {
        if (shell === 'bash' && src[i + 1] === '<' && src[i + 2] === '<') {
          pending = { op: '<<<', fd, target: null, dup: false, input: true };
          i += 3;
          continue;
        }
        if (shell === 'bash' && src[i + 1] === '<') {
          let k = i + 2;
          let strip = false;
          if (src[k] === '-') { strip = true; k++; }
          while (k < n && isWs(src[k])) k++;
          let delim = '';
          while (k < n && !isWs(src[k]) && src[k] !== '\n' && src[k] !== ';' && src[k] !== '|' && src[k] !== '&' && src[k] !== '<' && src[k] !== '>' && src[k] !== ')') {
            const q = src[k];
            if (q === "'" || q === '"') {
              const e = src.indexOf(q, k + 1);
              delim += src.slice(k + 1, e < 0 ? n : e);
              k = e < 0 ? n : e + 1;
            } else if (q === '\\') {
              delim += src[k + 1] ?? '';
              k += 2;
            } else {
              delim += q;
              k++;
            }
          }
          seg.redirs.push({ op: '<<', fd, target: null, dup: false, input: true });
          if (delim) heredocs.push({ delim, strip, seg });
          i = k;
          continue;
        }
        if (src[i + 1] === '&') {
          let k = i + 2;
          while (k < n && /[0-9-]/.test(src[k])) k++;
          seg.redirs.push({ op: '<&', fd, target: null, dup: true, input: true });
          i = k;
          continue;
        }
        pending = { op: '<', fd, target: null, dup: false, input: true };
        i++;
        continue;
      }
      let op = '>';
      let k = i + 1;
      if (src[k] === '>') { op = '>>'; k++; } else if (src[k] === '|') { op = '>|'; k++; }
      if (src[k] === '&') {
        let m = k + 1;
        while (m < n && isWs(src[m])) m++;
        if (/[0-9-]/.test(src[m] ?? '')) {
          while (m < n && /[0-9-]/.test(src[m])) m++;
          seg.redirs.push({ op: op + '&', fd, target: null, dup: true, input: false });
          i = m;
          continue;
        }
        op += '&';
        k++;
      }
      pending = { op, fd, target: null, dup: false, input: false };
      i = k;
      continue;
    }
    ensureWord(i);
    addRaw(c);
    i++;
  }
  endSeg(n, 'eof', n);

  // nested shells
  const nestedFrom = [];
  for (const s of segs) {
    const ns = nestedScript(s);
    if (ns && depth + 1 < MAX_DEPTH) nestedFrom.push([s, ns]);
  }
  out.push(...segs);
  out.push(...deeper);
  for (const [s, ns] of nestedFrom) lexInto(ns.text, ns.shell, depth + 1, s, out);
}

// =================================================================================================================
// Command words

const LOWER_EXT = /\.(exe|cmd|bat|com|ps1)$/i;
export const baseName = (t) => String(t || '').replace(/\\/g, '/').split('/').pop().toLowerCase().replace(LOWER_EXT, '');
const isOpt = (w) => w && !w.quoted && /^-/.test(w.text) && w.text !== '-';

const WT_SUBS = new Set(['nt', 'new-tab', 'sp', 'split-pane', 'ft', 'focus-tab', 'mf', 'move-focus', 'mp', 'move-pane']);
const WT_VALUE_OPTS = new Set(['-w', '--window', '-p', '--profile', '-d', '--startingdirectory', '--title', '--tabcolor', '--colorscheme', '-s', '--size', '--pos', '-t', '--target']);
const PS_START_VALUE = new Set(['-argumentlist', '-args', '-workingdirectory', '-wd', '-verb', '-windowstyle', '-credential', '-redirectstandardoutput', '-redirectstandarderror', '-redirectstandardinput', '-rso', '-rse', '-rsi']);

const BASH_KEYWORDS = new Set(['if', 'then', 'elif', 'else', 'while', 'until', 'do', '!', 'time']);

/**
 * The command word of a segment after launchers (&, ., call, exec, nohup, time, sudo, env, VAR=x, start, Start-Process, wt).
 * @returns {{name: string, word: object, index: number, args: object[], viaStart: boolean}|null}
 */
export function cmdInfo(seg) {
  if (seg._cmd !== undefined) return seg._cmd;
  const w = seg.words;
  let i = 0;
  let viaStart = false;
  for (let guard = 0; guard < 40 && i < w.length; guard++) {
    const t = w[i].quoted ? null : w[i].text.toLowerCase();
    const bn = t === null ? null : baseName(w[i].text);
    if (t === '&' || t === '.' || t === 'call' || t === 'exec' || t === 'nohup' || t === 'time' || t === 'command' || t === 'builtin' || bn === 'sudo' || bn === 'gsudo' || t === 'nice') {
      if (i + 1 >= w.length) break;
      i++;
      if (t === 'nice' && w[i] && /^-n?\d*$/.test(w[i].text)) { i++; if (/^\d+$/.test(w[i]?.text ?? '')) i++; }
      continue;
    }
    if (seg.shell === 'bash' && !w[i].quoted && /^[A-Za-z_][A-Za-z0-9_]*=/.test(w[i].text) && i + 1 < w.length) { i++; continue; }
    // bash reserved words that start a command list: `if x; then rm …`, `for …; do rm …`, `! rm …`.
    if (seg.shell === 'bash' && BASH_KEYWORDS.has(t) && i + 1 < w.length) { i++; continue; }
    if (bn === 'timeout' && seg.shell !== 'cmd' && !(w[i + 1] && /^\/t$/i.test(w[i + 1].text))) {
      let j = i + 1;
      while (j < w.length && /^-/.test(w[j].text)) j += ['-s', '--signal', '-k', '--kill-after'].includes(w[j].text) ? 2 : 1;
      j++;
      if (j >= w.length) break;
      i = j;
      continue;
    }
    if (bn === 'xargs') {
      let j = i + 1;
      while (j < w.length && /^-/.test(w[j].text)) j += ['-n', '-I', '-L', '-P', '-d', '-E', '-s', '-a', '--max-args', '--replace', '--max-procs', '--delimiter', '--arg-file'].includes(w[j].text) ? 2 : 1;
      if (j >= w.length) break;
      i = j;
      continue;
    }
    if (bn === 'env') {
      let j = i + 1;
      while (j < w.length) {
        const x = w[j].text;
        if (['-u', '--unset', '-C', '--chdir', '-S', '--split-string'].includes(x)) { j += 2; continue; }
        if (/^-/.test(x) && !w[j].quoted) { j++; continue; }
        if (/^[A-Za-z_][A-Za-z0-9_]*=/.test(x)) { j++; continue; }
        break;
      }
      if (j >= w.length) break;
      i = j;
      continue;
    }
    if (bn === 'start' && seg.shell !== 'ps') {
      let j = i + 1;
      if (w[j] && w[j].quoted && j + 1 < w.length) j++;
      while (j < w.length && /^\//.test(w[j].text) && !w[j].quoted) j++;
      if (j >= w.length) break;
      i = j;
      viaStart = true;
      continue;
    }
    if (bn === 'start-process' || bn === 'saps' || (bn === 'start' && seg.shell === 'ps')) {
      let j = i + 1;
      let target = -1;
      while (j < w.length) {
        const x = w[j].text.toLowerCase();
        if (!w[j].quoted && (x === '-filepath' || x === '-file' || x === '-fi' || x === '-fil')) { target = j + 1; break; }
        if (!w[j].quoted && PS_START_VALUE.has(x)) { j += 2; continue; }
        if (!w[j].quoted && /^-/.test(x)) { j++; continue; }
        target = j;
        break;
      }
      if (target < 0 || target >= w.length) break;
      i = target;
      viaStart = true;
      continue;
    }
    if (bn === 'wt') {
      let j = i + 1;
      while (j < w.length) {
        const x = w[j].text.toLowerCase();
        if (WT_SUBS.has(x)) { j++; continue; }
        if (WT_VALUE_OPTS.has(x)) { j += 2; continue; }
        if (/^-/.test(x)) { j++; continue; }
        break;
      }
      if (j >= w.length) break;
      i = j;
      viaStart = true;
      continue;
    }
    break;
  }
  const res = i < w.length ? { name: baseName(w[i].text), word: w[i], index: i, args: w.slice(i + 1), viaStart } : null;
  seg._cmd = res;
  return res;
}

/** Text after a word to the end of its segment, outer quotes removed when it is a single quoted string. */
function rawAfter(seg, word) {
  let t = seg.src.slice(word.end, seg.end).trim();
  if (t.length >= 2 && (t[0] === '"' || t[0] === "'") && t[t.length - 1] === t[0] && t.indexOf(t[0], 1) === t.length - 1) t = t.slice(1, -1);
  return t;
}

/** The -ArgumentList / -Args value(s) of a Start-Process segment, comma-joined into one argv string. */
function startArgList(seg) {
  const w = seg.words;
  for (let k = 0; k < w.length; k++) {
    const t = w[k].text.toLowerCase();
    const m = /^-(?:argumentlist|args|argument):(.*)$/i.exec(w[k].text);
    if (m) return m[1];
    if (!w[k].quoted && ['-argumentlist', '-args', '-argument', '-arg'].includes(t)) {
      let txt = '';
      for (let j = k + 1; j < w.length; j++) {
        if (!w[j].quoted && /^-[A-Za-z]/.test(w[j].text)) break;
        txt += (txt ? ' ' : '') + w[j].text;
      }
      return txt;
    }
  }
  return '';
}

function nestedScript(seg) {
  const c = cmdInfo(seg);
  if (!c) return null;
  if (c.viaStart) {
    // Start-Process <shell> -ArgumentList '<script>' (H-5): treat the argument list as the target's argv.
    const rest = startArgList(seg).replace(/,/g, ' ').trim();
    if (rest) {
      if (['cmd'].includes(c.name)) {
        const m = /^\s*\/{1,2}[ck]\b\s*(.*)$/is.exec(rest);
        if (m) return { shell: 'cmd', text: m[1] };
      } else if (c.name === 'powershell' || c.name === 'pwsh') {
        const enc = /^\s*-(?:e|ec|en|enc|enco|encod|encode|encoded|encodedc\w*)\b\s+(\S+)/i.exec(rest);
        if (enc) { try { return { shell: 'ps', text: Buffer.from(enc[1], 'base64').toString('utf16le') }; } catch { /* not base64 */ } }
        const m = /^\s*-(?:c|co|com|comm|comma|comman|command)\b\s*(.*)$/is.exec(rest);
        return { shell: 'ps', text: m ? m[1] : rest };
      } else if (['bash', 'sh', 'zsh', 'dash', 'ksh'].includes(c.name)) {
        const m = /(?:^|\s)-[A-Za-z]*c[A-Za-z]*\s+(.*)$/is.exec(rest);
        if (m) return { shell: 'bash', text: m[1] };
      }
    }
  }
  const a = c.args;
  // bash eval joins its (already unquoted) arguments with spaces and runs the result as a script.
  if (c.name === 'eval' && seg.shell === 'bash') return a.length ? { shell: 'bash', text: a.map((w) => w.text).join(' ') } : null;
  if (['bash', 'sh', 'zsh', 'dash', 'ksh'].includes(c.name)) {
    const k = a.findIndex((x) => !x.quoted && /^-[a-zA-Z]*c[a-zA-Z]*$/.test(x.text));
    if (k >= 0 && a[k + 1]) return { shell: 'bash', text: a[k + 1].text };
    if (seg.heredoc) return { shell: 'bash', text: seg.heredoc };
    return null;
  }
  if (c.name === 'cmd') {
    const k = a.findIndex((x) => /^[/-]{1,2}[ck]$/i.test(x.text));
    if (k >= 0 && a[k + 1]) return { shell: 'cmd', text: rawAfter(seg, a[k]) };
    return null;
  }
  if (c.name === 'powershell' || c.name === 'pwsh') {
    for (let k = 0; k < a.length; k++) {
      const x = a[k].text.toLowerCase();
      if (/^-(c|co|com|comm|comma|comman|command)$/.test(x) && a[k + 1]) {
        return { shell: 'ps', text: k + 2 === a.length ? a[k + 1].text : rawAfter(seg, a[k]) };
      }
      if (/^-(e|ec|en|enc|enco|encod|encode|encoded|encodedc\w*)$/.test(x) && a[k + 1]) {
        try {
          return { shell: 'ps', text: Buffer.from(a[k + 1].text, 'base64').toString('utf16le') };
        } catch {
          return null;
        }
      }
    }
    return null;
  }
  if (c.name === 'invoke-expression' || c.name === 'iex') {
    if (!a.length) return null;
    const k = a.findIndex((x) => /^-command$/i.test(x.text));
    if (k >= 0 && a[k + 1]) return { shell: 'ps', text: a[k + 1].text };
    return { shell: 'ps', text: a.length === 1 ? a[0].text : rawAfter(seg, c.word) };
  }
  if (c.name === 'wsl') {
    let k = 0;
    while (k < a.length && /^-/.test(a[k].text)) k += ['-d', '--distribution', '-u', '--user', '--cd'].includes(a[k].text) ? 2 : 1;
    if (a[k]) return { shell: 'bash', text: seg.src.slice(a[k].start, seg.end) };
  }
  return null;
}

/** Code of an interpreter one-liner (node -e, python -c, perl -e, deno eval, or a heredoc fed to one), or null. */
function interpCode(seg) {
  const c = cmdInfo(seg);
  if (!c) return null;
  const a = c.args;
  const flagCode = (flags) => {
    for (let k = 0; k < a.length; k++) {
      const x = a[k].text;
      if (flags.includes(x) && a[k + 1]) return a[k + 1].text;
      const m = /^(--eval|--print)=(.*)$/s.exec(x);
      if (m && flags.includes(m[1])) return m[2];
    }
    return null;
  };
  let code = null;
  if (['node', 'nodejs', 'bun'].includes(c.name)) code = flagCode(['-e', '--eval', '-p', '--print']);
  else if (/^(python|python3(\.\d+)?|py|pythonw)$/.test(c.name)) code = flagCode(['-c']);
  else if (['perl', 'ruby'].includes(c.name)) code = flagCode(['-e', '-E']);
  else if (c.name === 'php') code = flagCode(['-r']);
  else if (c.name === 'deno' && a[0]?.text === 'eval') code = a[1]?.text ?? null;
  if (code === null && seg.heredoc && /^(node|nodejs|bun|python|python3(\.\d+)?|py|perl|ruby|deno)$/.test(c.name)) code = seg.heredoc;
  return code;
}

// =================================================================================================================
// Paths and write targets

const NULL_SINKS = new Set(['/dev/null', '$null', 'nul', 'nul:', '/dev/stdout', '/dev/stderr', '/dev/tty', 'con', 'con:', '&1', '&2']);
const isNullSink = (t) => NULL_SINKS.has(String(t).trim().toLowerCase()) || /^\/dev\/fd\/\d+$/.test(String(t).trim());

function envDir(name, fallback) {
  return process.env[name] || fallback;
}

/** Expand a leading home/temp/workspace variable in a path word ($HOME, $env:USERPROFILE, %TEMP%, …). */
function expandPathVars(p, ctx) {
  const tmp = () => envDir('TEMP', envDir('TMP', os.tmpdir()));
  const home = () => homeDir();
  const table = [
    [/^(\$\{?HOME\}?|\$\{?env:(HOME|USERPROFILE)\}?|%(USERPROFILE|HOME)%|\$\{?USERPROFILE\}?)(?=$|[\\/])/i, home],
    [/^(\$\{?env:(TEMP|TMP)\}?|%(TEMP|TMP)%|\$\{?(TEMP|TMP|TMPDIR)\}?)(?=$|[\\/])/i, tmp],
    [/^(\$\{?env:APPDATA\}?|%APPDATA%)(?=$|[\\/])/i, () => envDir('APPDATA', path.join(home(), 'AppData', 'Roaming'))],
    [/^(\$\{?env:LOCALAPPDATA\}?|%LOCALAPPDATA%)(?=$|[\\/])/i, () => envDir('LOCALAPPDATA', path.join(home(), 'AppData', 'Local'))],
    [/^(\$\{?env:CLAUDE_PROJECT_DIR\}?|%CLAUDE_PROJECT_DIR%|\$\{?CLAUDE_PROJECT_DIR\}?)(?=$|[\\/])/i, () => ctx.ws],
    [/^(\$PWD|\$\{PWD\}|\$pwd|%CD%|\$\(pwd\)|`pwd`|\$\(Get-Location\)|\$\(\$PWD\.Path\)|\$\{?env:CD\}?)(?=$|[\\/])/i, () => ctx.cwd],
  ];
  for (const [re, fn] of table) {
    const m = re.exec(p);
    if (m) return toPosix(fn()) + p.slice(m[0].length);
  }
  return p;
}

/**
 * Windows path aliases the file system resolves but a literal comparison misses: 8.3 short names (`CLAUDE~1`), trailing
 * dots and spaces (Win32 strips them), and NTFS alternate data streams (`file:stream`, `::$DATA`). Returns a canonical
 * absolute path: the real path of the longest existing ancestor (long names) plus the stripped remainder.
 */
const stripWinSeg = (seg, i) => (i === 0 ? seg : seg.replace(/:.*$/, '').replace(/[ .]+$/, ''));

/** realpath of the longest existing ancestor of `dir` plus the stripped remainder (long names), memoised per directory. */
function realDir(dir, cache) {
  const key = `r:${dir}`;
  if (cache && cache.has(key)) return cache.get(key);
  let cur = dir;
  const rest = [];
  let out = dir;
  for (let guard = 0; guard < 128; guard++) {
    try {
      const real = fs.realpathSync.native(cur);
      out = rest.length ? path.join(real, ...rest.slice().reverse()) : real;
      break;
    } catch (e) {
      if (e.code !== 'ENOENT' && e.code !== 'ENOTDIR') break;
      const up = path.dirname(cur);
      if (up === cur) break;
      rest.push(path.basename(cur));
      cur = up;
    }
  }
  if (cache) cache.set(key, out);
  return out;
}

/** Lower-cased entry name → isLink for one folder (null when it cannot be read), memoised per folder. */
function dirListing(dir, cache) {
  const key = `d:${dir}`;
  if (cache && cache.has(key)) return cache.get(key);
  let m = null;
  try {
    m = new Map();
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) m.set(e.name.toLowerCase(), e.isSymbolicLink());
  } catch { m = null; }
  if (cache) cache.set(key, m);
  return m;
}

/**
 * Alias-resolved absolute path: the real folder (junctions, 8.3 names) plus the basename, itself followed when it is a
 * link (junction or symlink) or an 8.3 short name. One readdir per folder decides that, so a command naming hundreds
 * of files in one folder costs one listing, not hundreds of realpath calls. `trustBase`: the basename came from readdir.
 */
function canonicalWin(abs, cache, trustBase = false) {
  if (process.platform !== 'win32') return abs;
  const resolved = path.resolve(abs);
  const dir = path.dirname(resolved);
  if (dir === resolved) return toPosix(resolved);
  const key = `${trustBase ? 't' : 'f'}:${resolved}`;
  if (cache && cache.has(key)) return cache.get(key);
  const rd = realDir(dir, cache);
  const base = stripWinSeg(path.basename(resolved), 1);
  let out = null;
  if (!trustBase && !/[*?[\]]/.test(resolved)) {
    const list = dirListing(rd, cache);
    const isLink = list ? list.get(base.toLowerCase()) : undefined;
    if (list === null || isLink === true || (isLink === undefined && /~\d/.test(base))) {
      try { out = toPosix(fs.realpathSync.native(resolved)).split('/').map(stripWinSeg).join('/'); } catch { /* does not exist yet */ }
    }
  }
  if (out === null) out = toPosix(path.join(rd, base)).split('/').map(stripWinSeg).join('/');
  if (cache) cache.set(key, out);
  return out;
}

/**
 * Resolve a path word to {abs, rel, real, realRel, foreign}. `abs` is the lexical resolution and `real` follows Windows
 * aliases and junctions/symlinks (canonicalWin); rel/realRel are their W-relative POSIX forms (null when outside W).
 * Null for null sinks and empty words.
 */
/** Evaluate a PowerShell parenthesised path argument to a path string: `(Join-Path a b [c])` → `a/b/c`, else the last
 * quoted or slash-bearing literal inside. Null when nothing path-like is found. */
function evalPsParen(inner) {
  let segs;
  try { segs = parseShell(inner, 'ps'); } catch { return null; }
  const seg0 = segs.find((s) => s.depth === 0 && s.words.length);
  if (!seg0) return null;
  const c = cmdInfo(seg0);
  if (c && c.name === 'join-path') {
    const { named, pos } = psArgs(c.args);
    const parts = [];
    if (typeof named.path === 'string') parts.push(named.path);
    parts.push(...pos);
    if (typeof named.childpath === 'string') parts.push(named.childpath);
    if (parts.length) return parts.join('/');
  }
  const lits = seg0.words.filter((w) => w.quoted || /[\\/]/.test(w.text)).map((w) => w.text);
  return lits.length ? lits[lits.length - 1] : null;
}

function resolvePath(word, cwd, ctx, { trustBase = false } = {}) {
  let s = String(word ?? '').trim();
  if (!s || isNullSink(s)) return null;
  if (s[0] === '(' && s[s.length - 1] === ')') {
    const inner = evalPsParen(s.slice(1, -1));
    if (!inner) return null;
    s = inner.trim();
    if (!s) return null;
  }
  s = expandPathVars(s, { ...ctx, cwd });
  s = fromShellPath(s);
  if (process.platform !== 'win32' && (/^[A-Za-z]:[\\/]/.test(s) || /^\\\\/.test(s))) return { abs: s, rel: null, real: s, realRel: null, foreign: true };
  const abs = path.resolve(cwd, s);
  const real = canonicalWin(abs, ctx._realCache, trustBase);
  return { abs, rel: wsRel(ctx.ws, abs), real, realRel: wsRel(ctx.ws, real), foreign: false };
}

/** W-relative POSIX forms a target may match: its lexical and its real (alias-resolved) path. */
function relsOf(t) {
  const out = [];
  if (t.rel !== null && t.rel !== undefined) out.push(t.rel);
  if (t.realRel !== null && t.realRel !== undefined && t.realRel !== t.rel) out.push(t.realRel);
  return out;
}

const PS_SWITCHES = new Set(['force', 'recurse', 'append', 'nonewline', 'whatif', 'confirm', 'passthru', 'noclobber', 'verbose', 'debug',
  'asbytestream', 'wait', 'nonewwindow', 'usenewenvironment', 'usebasicparsing', 'disablekeepalive', 'skipcertificatecheck', 'raw',
  'container', 'r', 'f', 'rf', 'fr', 'q', 's']);

/** PowerShell parameter parse: {named: {lowername: value|true}, pos: [text]}. */
function psArgs(words) {
  const named = {};
  const pos = [];
  for (let k = 0; k < words.length; k++) {
    const w = words[k];
    const m = !w.quoted ? /^-([A-Za-z][\w-]*)(?::(.*))?$/s.exec(w.text) : null;
    if (m) {
      const name = m[1].toLowerCase();
      if (m[2] !== undefined) { named[name] = m[2]; continue; }
      if (PS_SWITCHES.has(name) || /^r(e(c(u(r(se?)?)?)?)?)?$/.test(name)) { named[name] = true; continue; }
      const nx = words[k + 1];
      if (nx && !(!nx.quoted && /^-[A-Za-z]/.test(nx.text))) { named[name] = nx.text; k++; } else named[name] = true;
    } else pos.push(w.text);
  }
  return { named, pos };
}

const PATH_PARAMS = ['path', 'literalpath', 'pspath', 'lp', 'filepath', 'destination', 'outfile', 'target'];
function psPathValues(named, which = PATH_PARAMS) {
  const out = [];
  for (const [k, v] of Object.entries(named)) {
    if (typeof v !== 'string') continue;
    if (which.some((p) => p === k || (k.length >= 2 && p.startsWith(k)) || (k === 'p' && p === 'path'))) out.push(v);
  }
  return out;
}

/** Non-option args of a POSIX-style command (after `--` everything is positional). */
function posArgs(words, valueOpts = []) {
  const out = [];
  let dd = false;
  for (let k = 0; k < words.length; k++) {
    const w = words[k];
    if (!dd && !w.quoted && w.text === '--') { dd = true; continue; }
    if (!dd && !w.quoted && /^-/.test(w.text) && w.text !== '-') {
      if (valueOpts.includes(w.text)) k++;
      continue;
    }
    out.push(w.text);
  }
  return out;
}

const WRITE_API = /\b(writeFile(Sync)?|appendFile(Sync)?|createWriteStream|copyFile(Sync)?|cpSync|renameSync|rename|unlink(Sync)?|rmSync|rmdir(Sync)?|mkdir(Sync)?|truncate(Sync)?|symlink(Sync)?|write_text|write_bytes|touch|makedirs|shutil\.(copy\w*|move|rmtree)|os\.(remove|unlink|rename|replace|mkdir|rmdir)|WriteAllText|WriteAllBytes|WriteAllLines|AppendAllText|AppendAllLines|File\.(Copy|Move|Delete|Create|Replace)|Directory\.(Delete|Move|CreateDirectory)|StreamWriter|FileStream)\b|\.write\s*\(|\bopen\s*\([^)]*,\s*['"][^'"]*[wax+]/;
const STRING_LIT = /'((?:[^'\\\n]|\\.)*)'|"((?:[^"\\\n]|\\.)*)"|`([^`$\n]*)`/g;

function stringLiterals(code) {
  const out = [];
  for (const m of String(code).matchAll(STRING_LIT)) {
    const s = (m[1] ?? m[2] ?? m[3] ?? '').replace(/\\\\/g, '\\');
    if (s && !/^[a-z]+:\/\//i.test(s) && s.length < 400) out.push(s);
  }
  return out;
}

const baseNameOf = (p) => toPosix(p).replace(/[\\/]+$/, '').split('/').pop();

/**
 * Every path a segment writes, deletes, moves or creates: redirections plus the write commands of each shell. When a
 * copy/move destination is (or looks like) a directory, `<dest>/<basename(src)>` is emitted too, so a write of an
 * owner-control file through a folder destination is still seen (H-3).
 * @returns {{text: string, via: string}[]}
 */
function writeWords(seg, x) {
  if (seg._ww) return seg._ww;
  const out = [];
  for (const r of seg.redirs) if (!r.input && !r.dup && r.target !== null && r.target !== undefined) out.push({ text: r.target, via: `redirection ${r.op}` });
  const c = cmdInfo(seg);
  if (c) {
    const a = c.args;
    const nm = c.name;
    const ps = seg.shell === 'ps';
    const add = (arr, via) => { for (const t of arr) if (t) out.push({ text: t, via }); };
    const cwd = seg.cwd || x?.cwd;
    /** Glob sources expand like the shell would (a copy of stuff/* names each file); unexpandable ones stay patterns. */
    const expandSrc = (src) => {
      if (!x || !hasGlob(src)) return [src];
      const e = expandGlob(x, cwd, src, 500);
      return e && e.length ? e : [src];
    };
    /** Files a recursive copy/move of `src` brings along (relative to src); ['**'] when unknown or too many to list. */
    let treeBudget = 3000;
    const treeOf = (src) => {
      if (!x || hasGlob(src)) return ['**'];
      const r = resolvePath(src, cwd, x);
      if (!r) return [];
      let st;
      try { st = fs.statSync(r.abs); } catch { return ['**']; }
      if (!st.isDirectory()) return [];
      if (treeBudget <= 0) return ['**'];
      const { rels, overflow } = walkRel(r.abs, { cap: treeBudget });
      treeBudget -= rels.length;
      return overflow ? ['**'] : rels;
    };
    /**
     * Add a copy/move destination plus dest/<basename(src)> for every source (the destination may be a folder, or be
     * created as one), and for a recursive copy or move every file of the source tree under both dest/ and
     * dest/<basename>/ (a copy of a folder holding .harness/PAUSE is a write of .harness/PAUSE).
     */
    const addCopy = (sources, dest, via, { recursive = false } = {}) => {
      if (!dest) return;
      out.push({ text: dest, via });
      const d = toPosix(dest).replace(/[\\/]+$/, '') || '/';
      for (const s0 of sources) {
        for (const s of expandSrc(s0)) {
          const b = baseNameOf(s);
          const named = Boolean(b) && b !== '.' && b !== '..';
          if (named) out.push({ text: `${d}/${b}`, via });
          if (!recursive) continue;
          for (const r of treeOf(s)) {
            // Tree entries come from readdir (long, real names), so only their folder needs resolving.
            out.push({ text: `${d}/${r}`, via, trust: r !== '**' });
            if (named) out.push({ text: `${d}/${b}/${r}`, via, trust: r !== '**' });
          }
        }
      }
    };
    const splitPs = (v) => (typeof v === 'string' ? v.split(',').map((t) => t.trim()).filter(Boolean) : []);
    const psRecurse = (named) => Object.keys(named).some((k) => /^r(e(c(u(r(se?)?)?)?)?)?$/.test(k));
    const psWrite = (name, positionIdx, extraNames = PATH_PARAMS) => {
      const { named, pos } = psArgs(a);
      const vals = psPathValues(named, extraNames);
      if (positionIdx === 'all') vals.push(...pos);
      else if (Array.isArray(positionIdx)) for (const k of positionIdx) { if (pos[k] !== undefined) vals.push(pos[k]); }
      if (typeof named.name === 'string' && (name === 'New-Item')) {
        const base = vals[0] ?? '.';
        vals.push(`${base}/${named.name}`);
      }
      if (typeof named.newname === 'string' && name === 'Rename-Item') {
        const src = psPathValues(named, ['path', 'literalpath'])[0] ?? pos[0];
        if (src) vals.push(path.posix.join(path.posix.dirname(toPosix(src)), named.newname));
      }
      add(vals, name);
    };
    if (['set-content', 'sc', 'add-content', 'ac', 'clear-content', 'clc', 'export-csv', 'epcsv', 'export-clixml'].includes(nm) && (ps || nm.includes('-'))) psWrite(nm, [0]);
    else if (nm === 'out-file') psWrite('Out-File', [0]);
    else if (nm === 'tee-object' || (nm === 'tee' && ps)) psWrite('Tee-Object', [0]);
    else if (nm === 'new-item' || nm === 'ni' || ((nm === 'mkdir' || nm === 'md') && ps)) {
      const { named, pos } = psArgs(a);
      // Only the link path is a write here; the target (what it points to) is checked as a link target, not a write.
      if (/^(symboliclink|junction|hardlink)$/i.test(String(named.itemtype || ''))) add([psPathValues(named, ['path', 'literalpath'])[0] ?? pos[0]], 'New-Item link');
      else psWrite('New-Item', [0]);
    } else if (nm === 'copy-item' || nm === 'cpi' || ((nm === 'copy' || nm === 'cp') && ps)) {
      const { named, pos } = psArgs(a);
      const dest = psPathValues(named, ['destination'])[0] ?? pos[1];
      const srcs = splitPs(psPathValues(named, ['path', 'literalpath'])[0] ?? pos[0]);
      addCopy(srcs, dest, 'Copy-Item', { recursive: psRecurse(named) });
    } else if (nm === 'move-item' || nm === 'mi' || ((nm === 'move' || nm === 'mv') && ps)) {
      const { named, pos } = psArgs(a);
      const dest = psPathValues(named, ['destination'])[0] ?? pos[1];
      const srcs = splitPs(psPathValues(named, ['path', 'literalpath'])[0] ?? pos[0]);
      for (const s of srcs) out.push({ text: s, via: 'Move-Item' });
      addCopy(srcs, dest, 'Move-Item', { recursive: true });
    } else if (nm === 'robocopy' || nm === 'xcopy') {
      // Windows copiers, callable from every shell; switches are /x (//x in Git Bash). Both copy a folder's contents.
      const p = a.map((w) => w.text).filter((t) => !/^\/{1,2}[A-Za-z0-9?+]+(?::[^/\\]*)?$/.test(t));
      if (nm === 'robocopy' && p.length >= 2) {
        addCopy([p[0]], p[1], nm, { recursive: true });
        const d = toPosix(p[1]).replace(/[\\/]+$/, '');
        for (const f of p.slice(2)) out.push({ text: `${d}/${f}`, via: nm });
      } else if (nm === 'xcopy' && p.length >= 1) addCopy([p[0]], p[1] ?? '.', nm, { recursive: true });
    } else if (['remove-item', 'ri'].includes(nm) || (['rm', 'del', 'erase', 'rd', 'rmdir'].includes(nm) && ps)) psWrite('Remove-Item', 'all');
    else if (['rename-item', 'rni'].includes(nm) || (nm === 'ren' && ps)) psWrite('Rename-Item', [0]);
    else if (['invoke-webrequest', 'iwr', 'invoke-restmethod', 'irm'].includes(nm)) psWrite(nm, [], ['outfile']);
    else if (nm === 'start-bitstransfer') psWrite(nm, [1], ['destination']);
    else if (seg.shell === 'cmd' && nm === 'mklink') { const p = a.filter((w) => !/^\/\/?/.test(w.text)).map((w) => w.text); add(p.slice(0, 1), 'mklink'); }
    else if (seg.shell === 'cmd' && nm === 'copy') {
      const p = a.filter((w) => !/^\//.test(w.text)).map((w) => w.text);
      if (p.length >= 2) addCopy(p.slice(0, -1).flatMap((t) => t.split('+')), p[p.length - 1], nm);
    } else if (seg.shell === 'cmd' && nm === 'move') {
      const p = a.filter((w) => !/^\//.test(w.text)).map((w) => w.text);
      if (p.length >= 2) { for (const s of p.slice(0, -1)) out.push({ text: s, via: nm }); addCopy(p.slice(0, -1), p[p.length - 1], nm, { recursive: true }); } else add(p, nm);
    } else if (seg.shell === 'cmd' && (nm === 'ren' || nm === 'rename')) {
      // ren <path> <newname>: the new name lives in the source's folder.
      const p = a.filter((w) => !/^\//.test(w.text)).map((w) => w.text);
      add(p.slice(0, 1), nm);
      if (p[1]) add([path.posix.join(path.posix.dirname(toPosix(p[0])), toPosix(p[1]))], nm);
    } else if (seg.shell === 'cmd' && ['del', 'erase', 'rd', 'rmdir', 'md', 'mkdir'].includes(nm)) add(a.filter((w) => !/^\//.test(w.text)).map((w) => w.text), nm);
    else if (['touch', 'mkdir', 'rmdir', 'rm', 'unlink', 'shred', 'tee'].includes(nm)) add(posArgs(a), nm);
    else if (nm === 'truncate') add(posArgs(a, ['-s', '--size', '-r', '--reference']), nm);
    else if (nm === 'ln') { const p = posArgs(a, ['-S', '--suffix', '-t', '--target-directory']); if (p.length) add([p[p.length - 1]], 'ln'); }
    else if (['cp', 'install', 'rsync', 'scp', 'mv'].includes(nm)) {
      const mv = nm === 'mv';
      const recursive = mv || nm === 'rsync' || a.some((w) => !w.quoted && (/^-[a-zA-Z]*[rRa][a-zA-Z]*$/.test(w.text) || ['--recursive', '--archive'].includes(w.text)));
      const t = a.findIndex((w) => w.text === '-t' || w.text === '--target-directory');
      const te = a.find((w) => /^--target-directory=/.test(w.text));
      const p = posArgs(a, ['-S', '--suffix', '-m', '--mode', '-o', '-g', '-t', '--target-directory', '-e', '--rsh']);
      let srcs = p;
      let dest = null;
      if (t >= 0 && a[t + 1]) dest = a[t + 1].text;
      else if (te) dest = te.text.split('=').slice(1).join('=');
      else if (p.length >= 2) { srcs = p.slice(0, -1); dest = p[p.length - 1]; }
      if (mv) for (const s of (dest === null ? p : srcs)) out.push({ text: s, via: nm });
      if (dest !== null) addCopy(srcs, dest, nm, { recursive });
    }
    else if (nm === 'sed' || nm === 'perl') {
      if (a.some((w) => /^-[a-zA-Z]*i/.test(w.text) || /^--in-place/.test(w.text))) {
        const hasE = a.some((w) => w.text === '-e' || w.text === '-f' || w.text === '--expression');
        const p = posArgs(a, ['-e', '-f', '--expression', '--file']);
        add(hasE ? p : p.slice(1), `${nm} -i`);
      }
    } else if (nm === 'dd') add(a.filter((w) => /^of=/.test(w.text)).map((w) => w.text.slice(3)), 'dd');
    else if (nm === 'curl') {
      for (let k = 0; k < a.length; k++) {
        if ((a[k].text === '-o' || a[k].text === '--output') && a[k + 1]) add([a[k + 1].text], 'curl -o');
        const m = /^--output=(.+)$/.exec(a[k].text);
        if (m) add([m[1]], 'curl -o');
      }
    } else if (nm === 'wget') {
      for (let k = 0; k < a.length; k++) {
        if ((a[k].text === '-O' || a[k].text === '-P') && a[k + 1]) add([a[k + 1].text], 'wget');
        const m = /^--(output-document|directory-prefix)=(.+)$/.exec(a[k].text);
        if (m) add([m[2]], 'wget');
      }
    }
    const code = interpCode(seg);
    if (code && WRITE_API.test(code)) add(stringLiterals(code), `${nm} code`);
    if (ps && /\[(System\.)?IO\.(File|Directory)\]::(Write|Append|Copy|Move|Delete|Create(SymbolicLink|HardLink)?|Replace|Open(Write)?\b)|\b(System\.)?IO\.(StreamWriter|FileStream)\b/i.test(seg.raw)) add(stringLiterals(seg.raw), '.NET file API');
  }
  const seen = new Set();
  seg._ww = out.filter((o) => { const k = String(o.text); if (seen.has(k)) return false; seen.add(k); return true; });
  return seg._ww;
}

// =================================================================================================================
// Segment predicates

function segWordsNorm(seg) {
  return seg.words.map((w) => toPosix(w.text).replace(/^\.\//, ''));
}

/** A segment is clean when it has no file redirection, no substitution, no single & and no secret reference. */
function isClean(seg, policy) {
  if (seg.subst || seg.bg) return false;
  if (seg.redirs.some((r) => !r.input && !r.dup && !isNullSink(r.target ?? ''))) return false;
  if (secretRefs(seg.raw + '\n' + (seg.heredoc || ''), policy).length) return false;
  return true;
}

/** True when the segment's words start with the exempt prefix followed by whitespace or the end of the segment. */
function startsWithPrefix(seg, prefix) {
  const joined = segWordsNorm(seg).join(' ');
  const p = toPosix(prefix).trim();
  if (!joined.startsWith(p)) return false;
  const next = joined[p.length];
  return next === undefined || next === ' ';
}

function exemptBy(seg, prefixes, policy) {
  if (seg.depth !== 0) return false;
  if (!prefixes.some((p) => startsWithPrefix(seg, p))) return false;
  return isClean(seg, policy);
}

// =================================================================================================================
// Secrets (G14)

const ENV_NAME = '[A-Za-z_][A-Za-z0-9_]*';
const SECRET_REF_RES = [
  new RegExp(`\\$\\{?env:(${ENV_NAME})\\}?`, 'gi'),
  new RegExp(`\\$\\{!?(${ENV_NAME})`, 'g'),
  new RegExp(`\\$(${ENV_NAME})`, 'g'),
  new RegExp(`%(${ENV_NAME})%`, 'g'),
  new RegExp(`!(${ENV_NAME})!`, 'g'),
  new RegExp(`GetEnvironmentVariable\\s*\\(\\s*['"](${ENV_NAME})['"]`, 'gi'),
  new RegExp(`\\benv:[\\\\/]?(${ENV_NAME})`, 'gi'),
  new RegExp(`process\\.env\\.(${ENV_NAME})`, 'g'),
  new RegExp(`process\\.env\\[\\s*['"\`](${ENV_NAME})['"\`]`, 'g'),
  new RegExp(`environ\\[\\s*['"](${ENV_NAME})['"]`, 'g'),
  new RegExp(`environ\\.get\\(\\s*['"](${ENV_NAME})['"]`, 'g'),
  new RegExp(`getenv\\(\\s*['"](${ENV_NAME})['"]`, 'gi'),
  new RegExp(`Deno\\.env\\.get\\(\\s*['"](${ENV_NAME})['"]`, 'g'),
];

/** Secret variable names referenced in text (any spelling). */
function secretRefs(text, policy) {
  const found = new Set();
  const t = String(text || '');
  for (const re of SECRET_REF_RES) {
    re.lastIndex = 0;
    for (const m of t.matchAll(re)) if (isSecretName(m[1], policy)) found.add(m[1]);
  }
  return [...found];
}

const DUMP_TEXT = [
  [/GetEnvironmentVariables\s*\(/i, '[Environment]::GetEnvironmentVariables()'],
  [/\bprocess\.env\b(?!\s*\.\s*[A-Za-z_$]|\s*\[)/, 'process.env'],
  [/\bos\.environ\b(?!\s*\[|\.get\s*\()/, 'os.environ'],
  [/\/proc\/[^\s/]+\/environ\b/, '/proc/*/environ'],
  [/\bHK(?:CU|EY_CURRENT_USER)[:\\]*Environment\b/i, 'HKCU\\Environment'],
  [/Session Manager\\+Environment/i, 'HKLM Session Manager\\Environment'],
  [/\bauthorization['"]?\s*[:=]/i, 'an Authorization header'],
  [/x-api-key/i, 'an x-api-key header'],
];

function envDumpWords(seg) {
  const c = cmdInfo(seg);
  if (!c) return null;
  const a = c.args.map((w) => w.text);
  const nm = c.name;
  if (nm === 'env' && a.every((x) => /^-/.test(x))) return 'env';
  if (nm === 'printenv') return 'printenv';
  if (nm === 'set' && a.length === 0) return 'set';
  if (nm === 'set' && seg.shell === 'cmd' && a.some((x) => !x.includes('=') && !/^\//.test(x))) return 'set <prefix>';
  if ((nm === 'export' || nm === 'declare' || nm === 'typeset') && (a.length === 0 || a.every((x) => /^-[px]+$/.test(x)))) return `${nm} -p`;
  if (nm === 'compgen' && a.some((x) => x === '-e' || x === '-v')) return 'compgen -e';
  if (['get-childitem', 'gci', 'ls', 'dir', 'get-item', 'gi', 'get-content', 'gc', 'cat', 'type', 'get-itemproperty', 'gp'].includes(nm)
    && a.some((x) => /^(-path:)?env:[\\/]?\*?$/i.test(x))) return 'Get-ChildItem env:';
  return null;
}

// =================================================================================================================
// Rules

const deny = (rule, reason) => ({ decision: 'deny', rule, reason: `[${rule}] ${reason}` });
const ask = (rule, reason) => ({ decision: 'ask', rule, reason: `[${rule}] ${reason}` });

const UNITY_READONLY = new Set(['editor_status', 'console', 'console_status', 'recompile_status', 'test_status', 'list_tests', 'capture_game_view',
  'get_scene_hierarchy', 'get_performance_stats']);
const FAL_READONLY = new Set(['search_models', 'search_docs', 'get_model_schema', 'get_pricing', 'recommend_model', 'check_job', 'get_job_result']);

function isReadOnlyMcp(tool) {
  let m = /^mcp__unity__(.+)$/.exec(tool);
  if (m) return UNITY_READONLY.has(m[1]);
  m = /^mcp__fal-ai__(.+)$/.exec(tool);
  if (m) return FAL_READONLY.has(m[1]);
  if (/^mcp__meshy__list/.test(tool)) return true;
  return false;
}

function G12(x) {
  if (x.audit || !exists(path.join(x.ws, '.harness', 'PAUSE'))) return null;
  const why = 'the owner paused the run (.harness/PAUSE): read-only work only (Read, Grep, Glob, game_gate status or history, next_action). The owner types OWNER: resume to continue.';
  if (EDIT_TOOLS.has(x.tool)) return deny('G12', why);
  if (x.shell) {
    const allowed = x.policy.pause?.allow_commands || [];
    const ok = x.segs.length > 0 && x.segs.every((s) => exemptBy(s, allowed, x.policy));
    return ok ? null : deny('G12', why);
  }
  if (x.tool.startsWith('mcp__')) return isReadOnlyMcp(x.tool) ? null : deny('G12', why);
  return null;
}

function G15(x) {
  if (!x.shell) return null;
  for (const s of x.segs) {
    const c = cmdInfo(s);
    if (!c) continue;
    const a = c.args.map((w) => w.text);
    const why = 'starting Claude Code from the shell is not allowed: a nested session runs this project\'s hooks on agent-chosen text and can drop the deny rules. Only claude --version, claude mcp list and claude mcp get <name> are allowed.';
    if (c.name === 'claude') {
      if (!c.viaStart) {
        if (a.length === 1 && ['--version', '-v', '-V'].includes(a[0])) continue;
        if (a[0] === 'mcp' && ((a[1] === 'list' && a.length === 2) || (a[1] === 'get' && a.length === 3))) continue;
      }
      return deny('G15', why);
    }
    const pkgRun = ['npx', 'bunx', 'pnpx'].includes(c.name) || (['pnpm', 'yarn'].includes(c.name) && a[0] === 'dlx') || (c.name === 'npm' && ['exec', 'x'].includes(a[0]));
    if (pkgRun && a.some((t) => /^@anthropic-ai\/claude-code(@.*)?$/i.test(t) || /^--package=@anthropic-ai\/claude-code/i.test(t))) return deny('G15', why);
    if (['node', 'nodejs', 'bun', 'deno'].includes(c.name) && a.some((t) => /@anthropic-ai[\\/]claude-code/i.test(t))) return deny('G15', why);
  }
  return null;
}

const SCHTASKS_VERB = /^\/\/?(create|change|delete|run|tn)$/i;
const REG_PERSIST = /\\(Run|RunOnce|RunOnceEx|Environment|Policies)(\\|"|'|$|\s)/i;

/** G16: things that run later or elsewhere, or change persistent user settings, so their payload escapes every hook. */
function G16(x) {
  if (!x.shell) return null;
  const why = (what) => `${what} runs code outside this session's hooks or changes persistent user settings (Task Scheduler, WMI process creation, the Run key, Environment, setx). It is not allowed: it would bypass the guard in a later run and can harm the machine beyond this project.`;
  if (/\[(System\.)?Environment\]\s*::\s*SetEnvironmentVariable\s*\([^)]*,[^)]*,\s*['"]?(User|Machine)['"]?/i.test(x.command)) return deny('G16', why('SetEnvironmentVariable at User/Machine scope'));
  if (/Invoke-(Cim|Wmi)Method/i.test(x.command) && /Win32_Process/i.test(x.command) && /\bCreate\b/i.test(x.command)) return deny('G16', why('Invoke-CimMethod Win32_Process Create'));
  if (/(Register|Unregister|Set|New|Disable|Enable)-ScheduledTask(Action)?\b/i.test(x.command)) return deny('G16', why('a *-ScheduledTask cmdlet'));
  for (const s of x.segs) {
    const c = cmdInfo(s);
    if (!c) continue;
    const nm = c.name;
    const a = c.args.map((w) => w.text);
    const lower = a.map((t) => t.toLowerCase());
    if (nm === 'schtasks' && a.some((t) => SCHTASKS_VERB.test(t) && !/tn$/i.test(t))) return deny('G16', why('schtasks create/change/delete/run'));
    if (nm === 'setx') return deny('G16', why('setx (persists a user environment variable)'));
    if (nm === 'wmic' && lower.includes('process') && lower.includes('call') && lower.includes('create')) return deny('G16', why('wmic process call create'));
    // The registry key path is matched on the raw segment: the bash lexer drops unquoted backslashes from the args.
    if ((nm === 'reg' || nm === 'reg.exe') && (lower[0] === 'add' || lower[0] === 'delete') && /HK(CU|LM|EY_)/i.test(s.raw) && REG_PERSIST.test(s.raw + '\\')) return deny('G16', why('reg add/delete of a Run/Environment key'));
    if (['set-itemproperty', 'sp', 'new-itemproperty', 'remove-itemproperty', 'rp'].includes(nm) && /HK(CU|LM)/i.test(s.raw) && REG_PERSIST.test(s.raw + '\\')) return deny('G16', why('Set-ItemProperty on a Run/Environment key'));
  }
  return null;
}

function G14(x) {
  if (!x.shell) return null;
  const exempt = [...(x.policy._g14_exempt || [])];
  const top = x.segs.filter((s) => s.depth === 0);
  let text = x.command;
  const isKitDoctor = (s) => {
    const w = segWordsNorm(s);
    return w[0] === 'node' && /(^|\/)kit\.mjs$/.test(w[1] || '') && w[2] === 'doctor';
  };
  const blank = [];
  for (const s of top) {
    if (exemptBy(s, exempt, x.policy) || (s.depth === 0 && isKitDoctor(s) && isClean(s, x.policy))) blank.push([s.start, s.end]);
  }
  if (blank.length) {
    let t = '';
    let last = 0;
    for (const [a, b] of blank.sort((p, q) => p[0] - q[0])) { t += text.slice(last, a) + ' '.repeat(Math.max(0, b - a)); last = b; }
    text = t + text.slice(last);
  }
  const refs = secretRefs(text, x.policy);
  const why = (what) => `the command ${what}; keys are never read, printed or sent from the shell. Use uv run --project Tools/kit/assetgen assetgen doctor (presence only) or let assetgen use the key.`;
  if (refs.length) return deny('G14', why(`reads the secret variable ${refs.join(', ')}`));
  for (const [re, what] of DUMP_TEXT) if (re.test(text)) return deny('G14', why(`reads or sends ${what}`));
  for (const s of x.segs) {
    if (s.depth === 0 && blank.some(([a]) => a === s.start)) continue;
    const d = envDumpWords(s);
    if (d) return deny('G14', why(`dumps the environment (${d})`));
  }
  return null;
}

// =================================================================================================================
// Protected trees (delete/move/link ancestor & descendant checks) and pipeline targets

const ciNorm = (p) => (process.platform === 'win32' ? String(p).toLowerCase() : String(p));

/** Relation of rel to a protected folder: 'equal', 'inside' (rel within prot), 'ancestor' (prot within rel), or null.
 * '' / '.' is the workspace root, an ancestor of every protected tree. */
function treeRel(rel, prot) {
  if (rel === null || rel === undefined) return null;
  const a = ciNorm(rel === '' ? '.' : rel);
  const b = ciNorm(prot);
  if (a === b) return 'equal';
  if (a === '.') return 'ancestor';
  if (a.startsWith(b + '/')) return 'inside';
  if (b.startsWith(a + '/')) return 'ancestor';
  return null;
}
const treeHit = (rel, prot) => treeRel(rel, prot) !== null;

/**
 * A destructive target's kind is "active" (its reach into a folder matters) when it is a move or a link, or a recursive
 * delete. A non-recursive delete only removes named files, so it is judged by the exact owner-control-file check, not by
 * the folder tree.
 */
const activeKind = (t) => t.kind === 'move' || t.kind === 'link' || (t.kind === 'delete' && t.recursive);

/** Deny (via `rule`) when a destructive/link target reaches any of `prots` (as the folder, inside it, or its ancestor),
 * or when a glob names a protected file. `accept(relation, target)` can veto a match. Returns a deny object or null. */
function treeDeny(x, rule, prots, { fileNames = [], accept } = {}) {
  const ok = accept || (() => true);
  for (const t of destructiveTargets(x)) {
    if (activeKind(t)) {
      for (const rel of relsOf(t)) {
        for (const prot of prots) {
          const k = treeRel(rel, prot);
          if (k && !(t.globBase === 'within' && k === 'ancestor') && ok(k, t)) return deny(rule, `${t.word} (${t.kind}) reaches ${prot}`);
        }
      }
    }
    if ((t.isGlob || activeKind(t)) && fileNames.length) {
      const pats = t.isGlob ? t.globRels : relsOf(t);
      for (const pat of pats) {
        let re; try { re = shellGlobRe(pat); } catch { re = null; }
        if (re) for (const name of fileNames) if (re.test(name) || treeHit(pat, name)) return deny(rule, `${t.word} (${t.kind}) matches ${name}`);
      }
    }
  }
  return null;
}

/** True when a destructive target reaches a Unity protected tree (Assets/Packages/ProjectSettings). */
function hitsUnity(x, t) {
  const unity = unityProtRels(x);
  return relsOf(t).some((rel) => unity.some((p) => treeHit(rel, p)));
}

function unityProtRels(x) {
  let sub = '';
  try { sub = wsRel(x.ws, x.unity) || ''; } catch { sub = ''; }
  const pre = sub ? sub + '/' : '';
  return ['Assets', 'Packages', 'ProjectSettings'].map((d) => pre + d);
}

function kitProtRels(x) {
  let roots;
  try { roots = kitCodeRoots(x.ws); } catch { roots = kitCodeRoots(x.ws, { unitySubdir: '.' }); }
  return roots.filter((r) => r.endsWith('/**')).map((r) => r.replace(/\/\*\*$/, ''));
}

/** Literal directory prefix of a glob (`Assets/*` → `Assets`, `*` → ''). */
function globDir(word) {
  const base = [];
  for (const p of toPosix(word).split('/')) { if (/[*?[\]]/.test(p)) break; base.push(p); }
  return base.join('/');
}

const hasGlob = (w) => /[*?[\]]/.test(String(w));

/**
 * A shell glob as an anchored RegExp over POSIX paths: `*` and `?` stay inside one segment, `**` crosses segments and
 * `[…]` / `[!…]` are character classes (an unclosed `[` is literal). Case-insensitive on Windows.
 */
function shellGlobRe(glob) {
  const g = toPosix(String(glob));
  let re = '';
  for (let i = 0; i < g.length; i++) {
    const c = g[i];
    if (c === '*' && g[i + 1] === '*') {
      if (g[i + 2] === '/') { re += '(?:.*/)?'; i += 2; } else { re += '.*'; i += 1; }
    } else if (c === '*') re += '[^/]*';
    else if (c === '?') re += '[^/]';
    else if (c === '[') {
      const close = g.indexOf(']', i + 2);
      if (close < 0) { re += '\\['; continue; }
      let body = g.slice(i + 1, close);
      const neg = body[0] === '!' || body[0] === '^';
      if (neg) body = body.slice(1);
      re += `[${neg ? '^/' : ''}${body.replace(/[\\\]^[]/g, '\\$&')}]`;
      i = close;
    } else re += c.replace(/[.+^${}()|\\\]]/g, '\\$&');
  }
  return new RegExp(`^${re}$`, process.platform === 'win32' ? 'i' : '');
}

const WALK_SKIP = new Set(['.git', 'node_modules', 'Library', 'Temp', 'Obj', 'obj', 'Logs', '.venv', '__pycache__']);

/**
 * Entries under baseAbs as POSIX paths relative to it (files, links and, with `dirs`, folders), not following links,
 * at most `maxDepth` levels deep. `overflow` is set when more than `cap` entries exist (the caller then stays
 * conservative). Heavy folders (WALK_SKIP) are listed but not entered.
 */
function walkRel(baseAbs, { cap = 2000, maxDepth = 64, dirs = false } = {}) {
  const rels = [];
  const links = new Set();
  const stack = [['', 0]];
  while (stack.length) {
    const [rel, depth] = stack.pop();
    let ents;
    try { ents = fs.readdirSync(rel ? path.join(baseAbs, rel) : baseAbs, { withFileTypes: true }); } catch { continue; }
    for (const e of ents) {
      const r = rel ? `${rel}/${e.name}` : e.name;
      if (e.isSymbolicLink()) links.add(r);
      if (e.isDirectory()) {
        if (dirs) rels.push(r);
        if (!WALK_SKIP.has(e.name) && depth + 1 < maxDepth) stack.push([r, depth + 1]);
      } else rels.push(r);
      if (rels.length > cap) return { rels, links, overflow: true };
    }
  }
  return { rels, links, overflow: false };
}

/**
 * Expand a glob word against the file system the way the shell would (dot names included, to stay conservative).
 * Returns the matching words (the literal prefix kept), [] when the literal folder exists and nothing matches, or null
 * when the folder does not exist yet or there are too many entries to check.
 */
function expandGlob(x, cwd, word, cap = 2000) {
  const w = toPosix(String(word));
  const dir = globDir(w);
  const tail = w.slice(dir.length).replace(/^\/+/, '');
  const baseR = resolvePath(dir || '.', cwd, x);
  if (!baseR || baseR.foreign || !tail) return null;
  let st;
  try { st = fs.statSync(baseR.abs); } catch { return null; }
  if (!st.isDirectory()) return null;
  let re;
  try { re = shellGlobRe(tail); } catch { return null; }
  const depth = tail.includes('**') ? 64 : tail.split('/').length;
  const { rels, links, overflow } = walkRel(baseR.abs, { cap, maxDepth: depth, dirs: true });
  if (overflow) return null;
  const pre = (r) => (dir ? `${dir.replace(/\/+$/, '')}/${r}` : r);
  const out = rels.filter((r) => re.test(r)).map(pre);
  // Matched links (junctions, symlinks) must be resolved in full; plain entries have their real names from readdir.
  out.links = new Set([...links].map(pre));
  return out;
}

/**
 * Resolve a path or glob word for tree matching: [target…]. A glob expands to the entries it matches (like the shell);
 * when it cannot be expanded it resolves to its literal directory prefix (conservative). Glob targets also carry the full
 * W-relative pattern (globRels) so it can be tested against protected file names.
 */
function resolveTargetPath(word, cwd, x) {
  const w = String(word);
  if (hasGlob(w)) {
    const r = resolvePath(globDir(w) || '.', cwd, x);
    if (!r) return [];
    const globRels = [];
    for (const base of [r.rel, r.realRel]) {
      if (base === null || base === undefined) continue;
      const tail = toPosix(w).slice(globDir(w).length).replace(/^\//, '');
      globRels.push(base === '' ? tail : `${base}/${tail}`);
    }
    // The literal folder always counts when it is (inside) a protected folder: the glob works there whatever it matches
    // now (files made earlier in the same command included). Above a protected folder only the expansion counts.
    const expanded = expandGlob(x, cwd, w);
    const out = [{ ...r, isGlob: true, word: w, globRels, globBase: expanded === null ? 'any' : 'within' }];
    for (const e of expanded || []) { const er = resolvePath(e, cwd, x, { trustBase: !expanded.links?.has(e) }); if (er) out.push({ ...er, isGlob: false, word: e, globRels: [] }); }
    return out;
  }
  const r = resolvePath(w, cwd, x);
  return r ? [{ ...r, isGlob: false, word: w, globRels: [] }] : [];
}

const DELETE_MOVE = new Set(['remove-item', 'ri', 'rm', 'del', 'erase', 'rd', 'rmdir', 'rimraf', 'move-item', 'mi', 'rename-item', 'rni', 'mv', 'move', 'ren', 'rename']);
const PIPE_SINKS = new Set([...DELETE_MOVE, 'copy-item', 'cpi', 'cp', 'set-content', 'sc', 'add-content', 'ac', 'clear-content', 'clc', 'new-item', 'ni', 'touch', 'tee', 'tee-object', 'out-file']);

/** The segment feeding this one through a pipe (same script and pipe id, previous in order), or null. */
function pipeSource(seg, x) {
  const group = x.segs.filter((s) => s.script === seg.script && s.pipe === seg.pipe).sort((a, b) => a.order - b.order);
  const i = group.indexOf(seg);
  return i > 0 ? group[i - 1] : null;
}

/** Path words the upstream of a pipe produces (Get-ChildItem/find/echo/…): {words, recursive} or null. */
function upstreamPaths(seg, x) {
  const up = pipeSource(seg, x);
  if (!up) return null;
  const c = cmdInfo(up);
  if (!c) {
    if (up.words.length === 1 && (up.words[0].quoted || /[\\/]/.test(up.words[0].text))) return { words: [up.words[0].text], recursive: false };
    return null;
  }
  const nm = c.name;
  const a = c.args;
  if (up.shell === 'ps' && ['get-childitem', 'gci', 'get-item', 'gi', 'resolve-path', 'ls', 'dir'].includes(nm)) {
    const { named, pos } = psArgs(a);
    const words = [...pos, ...psPathValues(named, ['path', 'literalpath'])];
    const recursive = Object.keys(named).some((k) => /^r(e(c(u(r(se?)?)?)?)?)?$/.test(k)) || a.some((w) => /^-r$/i.test(w.text));
    return { words: words.length ? words : ['.'], recursive };
  }
  if (nm === 'fd' || nm === 'find') {
    const paths = [];
    for (const w of a) { if (/^[-(!]/.test(w.text)) break; paths.push(w.text); }
    return { words: paths.length ? paths : ['.'], recursive: true };
  }
  if (['ls', 'dir', 'gci', 'get-childitem', 'get-item', 'gi', 'resolve-path'].includes(nm)) {
    return { words: posArgs(a), recursive: a.some((w) => /^-([a-zA-Z]*[rR]|-recursive)/.test(w.text)) };
  }
  if (nm === 'echo' || nm === 'printf') return { words: posArgs(a), recursive: false };
  if (up.words.length === 1 && (up.words[0].quoted || /[\\/]/.test(up.words[0].text))) return { words: [up.words[0].text], recursive: false };
  return null;
}

/** {kind, recursive, words} for a delete/move segment, or null. Factored out of G4 so G5/G5b reuse it. */
function deleteMoveInfo(seg) {
  const c = cmdInfo(seg);
  if (!c) return null;
  const nm = c.name;
  const a = c.args;
  const texts = a.map((w) => w.text);
  if (seg.shell === 'ps' && ['remove-item', 'ri', 'rm', 'del', 'erase', 'rd', 'rmdir'].includes(nm)) {
    const { named, pos } = psArgs(a);
    const recursive = Object.keys(named).some((k) => /^r(e(c(u(r(se?)?)?)?)?)?$/.test(k) || /^(rf|fr)$/.test(k)) || texts.some((t) => /^-[a-z]*r[a-z]*$/i.test(t) && t.length <= 4);
    return { kind: 'delete', recursive, words: [...pos, ...psPathValues(named, ['path', 'literalpath'])] };
  }
  if (seg.shell === 'cmd' && ['rd', 'rmdir', 'del', 'erase'].includes(nm)) {
    return { kind: 'delete', recursive: texts.some((t) => /^\/\/?s$/i.test(t)), words: texts.filter((t) => !/^\/\/?/.test(t)) };
  }
  if (nm === 'rm' || nm === 'rimraf') {
    return { kind: 'delete', recursive: nm === 'rimraf' || texts.some((t) => /^-[a-zA-Z]*[rR][a-zA-Z]*$/.test(t) || t === '--recursive'), words: posArgs(a) };
  }
  if ((nm === 'rmdir' || nm === 'rd' || nm === 'del' || nm === 'erase') && texts.some((t) => /^\/\/?s$/i.test(t))) {
    return { kind: 'delete', recursive: true, words: texts.filter((t) => !/^\/\/?/.test(t)) };
  }
  if (nm === 'find' && (texts.includes('-delete') || texts.some((t, k) => t === '-exec' && ['rm', 'rmdir'].includes(baseName(texts[k + 1] || ''))))) {
    // find PATH… EXPR: only the leading operands (before the first option / `(` / `!`) are search roots.
    const paths = [];
    for (const t of texts) { if (/^[-(!]/.test(t)) break; paths.push(t); }
    return { kind: 'delete', recursive: true, words: paths.length ? paths : ['.'] };
  }
  if (nm === 'git' && texts[0] === 'rm' && texts.some((t) => /^-[a-zA-Z]*r/.test(t))) {
    return { kind: 'delete', recursive: true, words: texts.slice(1).filter((t) => !/^-/.test(t)) };
  }
  if (['mv', 'move', 'move-item', 'mi', 'ren', 'rename', 'rename-item', 'rni'].includes(nm)) {
    let words;
    if (seg.shell === 'ps' || nm.includes('-') || nm === 'mi' || nm === 'rni') {
      const { named, pos } = psArgs(a);
      words = [...pos, ...psPathValues(named, ['path', 'literalpath', 'destination'])];
    } else words = seg.shell === 'cmd' ? texts.filter((t) => !/^\/\/?/.test(t)) : posArgs(a, ['-t', '-S', '--suffix']);
    return { kind: 'move', recursive: true, words };
  }
  return null;
}

/** The pointed-to target(s) of a link creation (New-Item link, mklink, ln, .NET CreateSymbolicLink): checked against the
 * protected trees and ~/.claude, never as an ordinary write (the target may legitimately be outside the workspace). */
function linkTargetWords(seg) {
  const c = cmdInfo(seg);
  const out = [];
  if (c) {
    const nm = c.name;
    const a = c.args;
    if ((nm === 'new-item' || nm === 'ni') && seg.shell === 'ps') {
      const { named, pos } = psArgs(a);
      if (/^(symboliclink|junction|hardlink)$/i.test(String(named.itemtype || ''))) {
        const t = (typeof named.target === 'string' ? named.target : null) ?? (typeof named.value === 'string' ? named.value : null) ?? pos[1];
        if (t) out.push(t);
      }
    } else if (seg.shell === 'cmd' && nm === 'mklink') {
      const p = a.filter((w) => !/^\/\/?/.test(w.text)).map((w) => w.text);
      if (p[1]) out.push(p[1]);
    } else if (nm === 'ln') {
      const p = posArgs(a, ['-S', '--suffix', '-t', '--target-directory']);
      if (p.length >= 2) out.push(p[0]);
    }
  }
  if (/Create(SymbolicLink|HardLink)/i.test(seg.raw)) { const lits = stringLiterals(seg.raw); if (lits[1]) out.push(lits[1]); }
  return out;
}

/**
 * Delete/move/link targets of the whole call, resolved (glob-aware, alias-aware): [{rel, realRel, kind, recursive, word, seg}].
 * Includes paths borrowed from a pipe/xargs upstream.
 */
function destructiveTargets(x) {
  if (x._dtargets) return x._dtargets;
  const out = [];
  if (!x.shell) { x._dtargets = out; return out; }
  for (const s of x.segs) {
    if (exemptBy(s, x.policy.kit_writers || [], x.policy)) continue;
    const cwd = s.cwd || x.cwd;
    const push = (word, kind, recursive) => { for (const r of resolveTargetPath(word, cwd, x)) out.push({ ...r, kind, recursive, seg: s }); };
    const dm = deleteMoveInfo(s);
    if (dm) for (const w of dm.words) push(w, dm.kind, dm.recursive);
    for (const w of linkTargetWords(s)) push(w, 'link', false);
    // A delete/move fed by a pipe or xargs deletes the upstream's paths (writes are covered by targetsOf instead).
    const c = cmdInfo(s);
    if (c && s.pipePrev && DELETE_MOVE.has(c.name)) {
      const up = upstreamPaths(s, x);
      if (up) for (const w of up.words) push(w, 'delete', up.recursive || (dm && dm.recursive) || true);
    }
  }
  x._dtargets = out;
  return out;
}

/** Write targets of the whole tool call: [{abs, rel, real, realRel, foreign, via, seg}]. */
function targetsOf(x) {
  if (x._targets) return x._targets;
  const out = [];
  if (EDIT_TOOLS.has(x.tool)) {
    const p = x.ti.file_path ?? x.ti.notebook_path ?? x.ti.path;
    if (typeof p === 'string') {
      const r = resolvePath(p, x.cwd, x);
      if (r) out.push({ ...r, via: x.tool, seg: null });
    }
  } else if (x.shell) {
    for (const s of x.segs) {
      if (exemptBy(s, x.policy.kit_writers || [], x.policy)) continue;
      for (const w of writeWords(s, x)) {
        const r = resolvePath(w.text, s.cwd || x.cwd, x, { trustBase: Boolean(w.trust) });
        if (r) out.push({ ...r, via: w.via, seg: s });
      }
      // A write command fed by a pipe/xargs writes to the upstream's paths too (echo path | xargs touch).
      const c = cmdInfo(s);
      if (c && s.pipePrev && PIPE_SINKS.has(c.name) && !DELETE_MOVE.has(c.name)) {
        const up = upstreamPaths(s, x);
        if (up) for (const w of up.words) { const r = resolvePath(w, s.cwd || x.cwd, x); if (r) out.push({ ...r, via: `${c.name} (piped)`, seg: s }); }
      }
    }
  }
  x._targets = out;
  return out;
}

// Guard-local additions to OWNER_CONTROL_FILES (H-11): the guard-hit ledger gates the milestone, so only
// wave_manifest audit/resolve change it, never the audited agent's shell or Edit/Write.
const LEDGER_EXTRA = ['Docs/qa/guard_hits.jsonl'];
const OWNER_GUARDED = [...OWNER_CONTROL_FILES, ...LEDGER_EXTRA];
const OWNER_NAMES = OWNER_GUARDED.map((g) => g.replace(/\/\*\*$/, '/').toLowerCase());
// The .harness and Docs/qa trees: a delete, move or link of the tree (or an ancestor of it) removes or forges owner
// controls, so it is denied even with a valid HARNESS_UNLOCK (like the owner-control files themselves).
const OWNER_TREES = ['.harness', 'Docs/qa'];

const OWNER_FILE_NAMES = OWNER_GUARDED.map((g) => g.replace(/\/\*\*$/, ''));
const OWNER_RES = OWNER_GUARDED.map((g) => globToRegExp(g));
const anyRe = (rel, res) => { const r = toPosix(rel); return res.some((re) => re.test(r)); };
/** Kit-code root matchers for this call (compiled once). */
function rootRes(x, roots) {
  if (!x._rootRes) x._rootRes = roots.map((g) => globToRegExp(g));
  return x._rootRes;
}

/** The owner-control path a glob write target (`.harness/*`, `.harness/**`) can denote, or null. */
function globOwnerHit(x, rel) {
  let re;
  try { re = shellGlobRe(rel); } catch { return null; }
  const names = [...OWNER_FILE_NAMES, '.harness/reservations/x.json'];
  try { for (const f of fs.readdirSync(path.join(x.ws, '.harness', 'reservations'))) names.push(`.harness/reservations/${f}`); } catch { /* none */ }
  return names.find((n) => re.test(n)) ?? null;
}

/** The kit-code path a glob write target can denote (G5), or null: a glob inside a kit folder, one matching a kit file
 * name, or one whose expansion reaches kit code (an unexpandable glob above a kit folder counts as reaching it). */
function globKitHit(x, rel, roots, dirs) {
  const gd = globDir(rel);
  for (const d of dirs) { const k = treeRel(gd, d); if (k === 'equal' || k === 'inside') return d; }
  let re;
  try { re = shellGlobRe(rel); } catch { return null; }
  for (const n of ['.mcp.json', '.claude/settings.json', '.claude/settings.local.json']) if (re.test(n)) return n;
  if (!dirs.some((d) => treeRel(gd, d) === 'ancestor')) return null;
  const e = expandGlob(x, x.ws, rel, 3000);
  if (e === null) return gd || '.';
  const hit = e.find((r) => anyRe(r, rootRes(x, roots)));
  if (hit) return hit;
  return dirs.find((d) => re.test(d) || re.test(`${d}/x`)) ?? null;
}

function G5b(x) {
  const why = (what) => `${what} is an owner-control or ledger path. Only the owner's hooks and the kit's own writers (gate_close, game_gate --row, probe_report, assetgen reserve, wave_manifest audit/resolve) change it, and HARNESS_UNLOCK does not lift this.`;
  for (const t of targetsOf(x)) {
    for (const rel of relsOf(t)) {
      if (anyRe(rel, OWNER_RES)) return deny('G5b', why(rel));
      if (hasGlob(rel)) { const hit = globOwnerHit(x, rel); if (hit) return deny('G5b', why(`${rel} (matches ${hit})`)); }
    }
  }
  const td = treeDeny(x, 'G5b', OWNER_TREES, { fileNames: OWNER_FILE_NAMES });
  if (td) return deny('G5b', why(td.reason.replace(/^\[G5b\]\s*/, '')));
  if (x.shell) {
    for (const s of x.segs) {
      const code = interpCode(s);
      if (!code) continue;
      const norm = code.replace(/\\\\/g, '/').replace(/\\/g, '/').toLowerCase();
      const hit = OWNER_NAMES.find((n) => norm.includes(n));
      if (hit) return deny('G5b', `interpreter code names the owner-control file ${hit}; only the owner's hooks and the kit's writers change it.`);
    }
  }
  return null;
}

function G6(x) {
  const claudeHome = path.join(homeDir(), '.claude');
  const roots = (x.policy.write_roots || []).filter((r) => typeof r === 'string' && r).map((r) => path.resolve(x.ws, fromShellPath(r)));
  for (const t of targetsOf(x)) {
    // Both the lexical path and the alias/junction-resolved real path must land in an allowed root.
    for (const abs of [t.abs, t.real]) {
      if (!abs) continue;
      if (isInside(abs, claudeHome)) return deny('G6', `~/.claude is never written by the agent (settings, transcripts and skills live there): ${toPosix(abs)}.`);
    }
    const effective = t.real ?? t.abs;
    if (t.foreign && !isInside(t.abs, claudeHome)) return deny('G6', `write outside the workspace (${t.abs}); allowed roots are the workspace, %TEMP%/claude and policy.write_roots.`);
    const okAll = [t.abs, effective].every((abs) => !abs || isInside(abs, x.ws) || isInside(abs, scratchRoot()) || roots.some((r) => isInside(abs, r)));
    if (okAll) continue;
    return deny('G6', `write outside the workspace (${toPosix(effective)}); allowed roots are the workspace, %TEMP%/claude and policy.write_roots.`);
  }
  // A link whose target is ~/.claude lets later writes reach the agent-forbidden config: deny the link creation.
  for (const t of destructiveTargets(x)) {
    if (t.kind !== 'link') continue;
    for (const abs of [t.abs, t.real]) if (abs && isInside(abs, claudeHome)) return deny('G6', `a link pointing at ~/.claude is not allowed (${toPosix(abs)}); ~/.claude is never written by the agent.`);
  }
  return null;
}

function unlockValid(x) {
  if (x.audit) return false;
  const u = readJsonOr(path.join(x.ws, '.harness', 'HARNESS_UNLOCK'), null, { onInvalid: 'fallback' });
  const exp = isObj(u) ? parseUtc(u.expires_utc) : null;
  return Boolean(exp && exp.getTime() > x.now.getTime());
}

function G5(x) {
  let roots;
  try {
    roots = kitCodeRoots(x.ws);
  } catch {
    roots = kitCodeRoots(x.ws, { unitySubdir: '.' });
  }
  const dirs = kitProtRels(x);
  const why = (what) => `${what} is kit code (Tools/kit, .claude, .mcp.json, the kit package). Edits need the owner to type OWNER: unlock harness (valid ${x.policy.ttl_min?.unlock ?? 60} min).`;
  for (const t of targetsOf(x)) {
    for (const rel of relsOf(t)) {
      if (anyRe(rel, rootRes(x, roots))) return unlockValid(x) ? null : deny('G5', why(rel));
      if (hasGlob(rel)) {
        // A recursive glob delete at or in a Unity tree (del /s Packages*) is G4's (it denies it); other globs are checked here.
        const dm = t.seg ? deleteMoveInfo(t.seg) : null;
        const unityDelete = dm && dm.kind === 'delete' && dm.recursive && unityProtRels(x).some((p) => treeHit(globDir(rel), p));
        const hit = unityDelete ? null : globKitHit(x, rel, roots, dirs);
        if (hit) return unlockValid(x) ? null : deny('G5', why(`${rel} (reaches ${hit})`));
      }
    }
  }
  // An ancestor delete/move that is itself a Unity tree (e.g. `rm -rf Packages`, which contains the kit package) is
  // Unity's concern (G4), not a kit-code edit. equal/inside a kit dir stays G5.
  const td = treeDeny(x, 'G5', dirs, { fileNames: ['.mcp.json'], accept: (k, t) => k !== 'ancestor' || !hitsUnity(x, t) });
  if (td) return unlockValid(x) ? null : deny('G5', why(td.reason.replace(/^\[G5\]\s*/, '')));
  return null;
}

const GIT_VALUE_OPTS = new Set(['-C', '-c', '--git-dir', '--work-tree', '--namespace', '--super-prefix', '--config-env', '--exec-path']);

/** {sub, args} of a git segment, or null. */
function gitOf(seg) {
  const c = cmdInfo(seg);
  if (!c || c.name !== 'git') return null;
  const a = c.args.map((w) => w.text);
  let k = 0;
  while (k < a.length && /^-/.test(a[k])) k += GIT_VALUE_OPTS.has(a[k]) ? 2 : 1;
  if (k >= a.length) return null;
  return { sub: a[k].toLowerCase(), args: a.slice(k + 1) };
}

function gitDestructive(g) {
  const a = g.args;
  const has = (re) => a.some((t) => re.test(t));
  switch (g.sub) {
    case 'reset': return has(/^--hard$/) ? 'git reset --hard' : null;
    case 'clean': return has(/^-[a-zA-Z]*f[a-zA-Z]*$|^--force$/) ? 'git clean -f' : null;
    case 'push':
      if (has(/^(--force|--force-with-lease(=.*)?|--force-if-includes|--mirror|--delete)$/) || has(/^-[a-zA-Z]*[fd][a-zA-Z]*$/)) return 'git push --force/--delete';
      if (a.some((t) => /^\+/.test(t) || /^:[^/]/.test(t))) return 'git push +refspec';
      return null;
    case 'branch':
      if (has(/^-[a-zA-Z]*D[a-zA-Z]*$/)) return 'git branch -D';
      if ((has(/^--delete$/) || has(/^-[a-z]*d[a-z]*$/)) && (has(/^--force$/) || has(/^-[a-z]*f[a-z]*$/))) return 'git branch -D';
      return null;
    case 'checkout': {
      const dd = a.indexOf('--');
      const paths = dd >= 0 ? a.slice(dd + 1) : a.filter((t) => !/^-/.test(t));
      if (paths.some((t) => ['.', '*', ':/', ':/*', './'].includes(t))) return 'git checkout -- .';
      if (has(/^(-f|--force)$/)) return 'git checkout --force';
      return null;
    }
    case 'restore':
      if (has(/^(--source(=.*)?|-s)$/) || has(/^-s./)) return 'git restore --source';
      if (a.some((t) => ['.', '*', ':/'].includes(t)) && !(has(/^(--staged|-S)$/) && !has(/^(--worktree|-W)$/))) return 'git restore .';
      return null;
    case 'filter-branch': return 'git filter-branch';
    case 'filter-repo': return 'git filter-repo';
    case 'rebase': return 'git rebase';
    case 'reflog': return ['expire', 'delete'].includes(a[0]) ? `git reflog ${a[0]}` : null;
    case 'update-ref': return has(/^(-d|--delete)$/) ? 'git update-ref -d' : null;
    default: return null;
  }
}

function freshBundle(x) {
  const dir = path.join(x.ws, '.harness', 'backups');
  const ttl = x.policy.ttl_min?.backup_bundle ?? 10;
  let names = [];
  try { names = fs.readdirSync(dir); } catch { return false; }
  for (const n of names) {
    const m = /^backup-(\d{8}T\d{6}Z)\.bundle$/.exec(n);
    if (!m) continue;
    const stamp = parseFileStamp(m[1]);
    if (!stamp) continue;
    const ageMin = (x.now.getTime() - stamp.getTime()) / 60000;
    if (ageMin < 0 || ageMin > ttl) continue;
    const f = path.join(dir, n);
    let st;
    try { st = fs.statSync(f); } catch { continue; }
    if (!st.isFile() || st.mtimeMs > x.now.getTime() + 2000) continue;
    let head = '';
    try {
      const fd = fs.openSync(f, 'r');
      const buf = Buffer.alloc(32);
      const k = fs.readSync(fd, buf, 0, 32, 0);
      fs.closeSync(fd);
      head = buf.subarray(0, k).toString('utf8');
    } catch { continue; }
    if (/^# v[23] git bundle\r?\n/.test(head)) return true;
  }
  return false;
}

function G1(x) {
  if (!x.shell) return null;
  let what = null;
  for (const s of x.segs) {
    const g = gitOf(s);
    const d = g && gitDestructive(g);
    if (d) { what = d; break; }
  }
  if (!what) return null;
  const ttl = x.policy.ttl_min?.backup_bundle ?? 10;
  const why = `destructive git (${what}) needs an owner override for this exact command AND a git bundle backup newer than ${ttl} min: git bundle create .harness/backups/backup-<UTC file stamp>.bundle --all, then the owner types OWNER-OVERRIDE: <command>.`;
  if (x.audit) return deny('G1', why);
  const cmd = (x.grantCommand ?? x.command).trim();
  const lines = readJsonl(path.join(x.ws, '.harness', 'overrides.jsonl'));
  const consumed = new Set(lines.filter((l) => isObj(l) && l.event === 'consumed').map((l) => l.id));
  const grant = lines.find((l) => isObj(l) && l.event === 'grant' && typeof l.id === 'string' && l.command === cmd && !consumed.has(l.id)
    && parseUtc(l.expires_utc) && parseUtc(l.expires_utc).getTime() > x.now.getTime());
  if (!grant) return deny('G1', why);
  if (!freshBundle(x)) return deny('G1', `override ${grant.id} found, but no fresh git bundle: ${why}`);
  x.effects.push({ type: 'consume_override', id: grant.id });
  return null;
}

const SETLIVE_RE = /["']?SetLive["']?\s+["']?default["']?/i;

function G9(x) {
  const why = 'Steam logins with a password, Steam Guard codes and setting the default branch live are human steps (ARCHITECTURE §8 H21). Log in once by hand; the kit uploads with the cached login and never sets default live.';
  if (EDIT_TOOLS.has(x.tool)) {
    const texts = [x.ti.content, x.ti.new_string, x.ti.new_source, ...(Array.isArray(x.ti.edits) ? x.ti.edits.map((e) => e?.new_string) : [])].filter((t) => typeof t === 'string');
    if (texts.some((t) => SETLIVE_RE.test(t) || /set_steam_guard_code/i.test(t))) return deny('G9', why);
    return null;
  }
  if (!x.shell) return null;
  if (/set_steam_guard_code/i.test(x.command) || SETLIVE_RE.test(x.command)) return deny('G9', why);
  for (const s of x.segs) {
    const c = cmdInfo(s);
    if (!c || c.name !== 'steamcmd') continue;
    const a = c.args.map((w) => w.text);
    for (let k = 0; k < a.length; k++) {
      const t = a[k].toLowerCase();
      if (t === '+login') {
        let n = 0;
        for (let j = k + 1; j < a.length && !a[j].startsWith('+'); j++) n++;
        if (n >= 2) return deny('G9', why);
      }
      if (t === '+app_set_live' && a.slice(k + 1, k + 3).some((v) => v.toLowerCase() === 'default')) return deny('G9', why);
    }
  }
  return null;
}

const G10_PATTERNS = [
  [/Process\s*\.\s*Start\b|System\.Diagnostics\.Process\b|\bProcessStartInfo\b/, 'Process.Start'],
  [/\bSystem\.Net\b/, 'System.Net'],
  [/\bHttpClient\b/, 'HttpClient'],
  [/\bWebClient\b/, 'WebClient'],
  [/\bWebRequest\b|UnityWebRequest/, 'WebRequest'],
  [/\bTcpClient\b|\bSocket\b/, 'sockets'],
  [/\bRegistry\b/, 'Registry'],
  [/Environment\s*\.\s*Exit\b/, 'Environment.Exit'],
];
const DELETE_RE = /\b(?:File|Directory)\s*\.\s*Delete\s*\(\s*(@?)"((?:[^"\\]|\\.)*)"|FileUtil\s*\.\s*Delete\w*\s*\(\s*(@?)"((?:[^"\\]|\\.)*)"/g;

function csPayloadHit(code, unity) {
  for (const [re, what] of G10_PATTERNS) if (re.test(code)) return what;
  for (const m of code.matchAll(DELETE_RE)) {
    const verbatim = m[1] ?? m[3];
    let p = m[2] ?? m[4] ?? '';
    if (!verbatim) p = p.replace(/\\\\/g, '\\');
    if (/^[A-Za-z]:[\\/]|^[\\/]|^~/.test(p)) {
      const abs = path.resolve(fromShellPath(p));
      if (!isInside(abs, unity)) return `a delete outside the project (${p})`;
    }
  }
  return null;
}

function allStrings(v, out = [], depth = 0) {
  if (depth > 12 || out.length > 5000) return out;
  if (typeof v === 'string') out.push(v);
  else if (Array.isArray(v)) for (const x of v) allStrings(x, out, depth + 1);
  else if (isObj(v)) for (const x of Object.values(v)) allStrings(x, out, depth + 1);
  return out;
}

function G10(x) {
  const why = (what) => `Unity eval payload uses ${what}. Process, network, registry and exit calls, and deletes outside the project, are not allowed through eval; write an editor command and run it with game_gate exec.`;
  const unity = x.unity;
  if (/^mcp__unity__(eval\w*|run_script|batch)$/.test(x.tool)) {
    const parts = allStrings(x.ti);
    if (x.tool === 'mcp__unity__eval_file') {
      for (const s of parts) {
        if (/\.cs$/i.test(s)) {
          const r = resolvePath(s, x.cwd, x);
          try { if (r && fs.statSync(r.abs).size < 1 << 20) parts.push(readText(r.abs)); } catch { /* unreadable */ }
        }
      }
    }
    const hit = csPayloadHit(parts.join('\n'), unity);
    return hit ? deny('G10', why(hit)) : null;
  }
  if (!x.shell) return null;
  for (const s of x.segs) {
    const c = cmdInfo(s);
    if (!c || c.name !== 'unity') continue;
    const a = c.args.map((w) => w.text.toLowerCase());
    if (a[0] !== 'command' || !['eval', 'eval_file', 'run_script', 'batch', 'menu'].includes(a[1])) continue;
    let payload = s.raw + '\n' + (s.heredoc || '');
    if (a[1] === 'eval_file') {
      for (const w of c.args.slice(2)) {
        if (/\.cs$/i.test(w.text)) {
          const r = resolvePath(w.text, s.cwd || x.cwd, x);
          try { if (r && fs.statSync(r.abs).size < 1 << 20) payload += '\n' + readText(r.abs); } catch { /* unreadable */ }
        }
      }
    }
    const hit = csPayloadHit(payload, unity);
    if (hit) return deny('G10', why(hit));
  }
  return null;
}

const LOOPBACK_PORT = /(?:localhost|127\.0\.0\.1|0\.0\.0\.0|\[::1\]|\[0:0:0:0:0:0:0:1\])\s*:\s*(\d{2,5})/gi;
const isBlockedPort = (p) => (p >= 7800 && p <= 7849) || p === 9876;

function G10b(x) {
  const why = 'direct HTTP to the Unity Pipeline (ports 7800-7849) or the Blender add-on socket (9876), or reading the Pipeline eval token, bypasses the eval guard. Use node Tools/kit/gate/game_gate.mjs (exec, compile, test).';
  if (EDIT_TOOLS.has(x.tool)) {
    return targetsOf(x).some((t) => /\.unity-pipeline-port$/i.test(toPosix(t.abs))) ? deny('G10b', why) : null;
  }
  if (!x.shell) return null;
  const gate = ['node Tools/kit/gate/game_gate.mjs'];
  for (const s of x.segs) {
    if (exemptBy(s, gate, x.policy)) continue;
    const text = s.raw + '\n' + (s.heredoc || '');
    if (/\.unity-pipeline-port/i.test(text)) return deny('G10b', why);
    LOOPBACK_PORT.lastIndex = 0;
    for (const m of text.matchAll(LOOPBACK_PORT)) if (isBlockedPort(Number(m[1]))) return deny('G10b', why);
    if (/localhost|127\.0\.0\.1|::1/i.test(text)) {
      const pm = /-Port\s+(\d{2,5})/i.exec(text);
      if (pm && isBlockedPort(Number(pm[1]))) return deny('G10b', why);
    }
  }
  return null;
}

function G11(x) {
  if (!/^mcp__blender/.test(x.tool)) return null;
  const code = allStrings(x.ti).join('\n');
  const why = (what) => `Blender MCP code uses ${what}. The interactive Blender is for inspection; run scripts headless through node Tools/kit/blender/bl_run.mjs.`;
  const pats = [
    [/\bsubprocess\b/, 'subprocess'], [/\bos\s*\.\s*(system|popen|exec\w*|spawn\w*)\b/, 'os.system'], [/\burllib\b/, 'urllib'],
    [/\bimport\s+requests\b|\bfrom\s+requests\b|\brequests\s*\.\s*(get|post|put|delete|head|patch|request|Session)\b/, 'requests'],
    [/\bhttp\s*\.\s*client\b/, 'http.client'],
  ];
  for (const [re, what] of pats) if (re.test(code)) return deny('G11', why(what));
  for (const m of code.matchAll(/shutil\s*\.\s*rmtree\s*\(\s*(?:r?(['"])((?:(?!\1).)*)\1)?/g)) {
    const lit = m[2];
    if (lit === undefined) return deny('G11', why('shutil.rmtree on a path that cannot be checked'));
    const p = /^[A-Za-z]:[\\/]|^[\\/]|^~/.test(lit) ? path.resolve(fromShellPath(lit)) : null;
    if (!p || !isInside(p, scratchRoot())) return deny('G11', why(`shutil.rmtree outside scratch (${lit})`));
  }
  return null;
}

function G3(x) {
  if (!x.shell) return null;
  const why = 'killing processes by name is not allowed (it took down an unrelated editor in WoO). Kill by PID only: taskkill /PID <pid> /T /F or Stop-Process -Id <pid>.';
  if (/Win32_Process/i.test(x.command) && /name\s*=|-Name\b/i.test(x.command) && /Terminate|Remove-CimInstance|Remove-WmiObject|Stop-Process/i.test(x.command)) return deny('G3', why);
  // .Kill() on a Get-Process/gps result by name: (Get-Process Unity).Kill(), Get-Process -Name x | % { $_.Kill() }.
  // Checked on the whole command because the `(…)` and `.Kill()` land in separate segments.
  if (/\.Kill\s*\(\s*\)/.test(x.command) && /\b(Get-Process|gps)\b/i.test(x.command) && !/\b-(Id|PID)\b/i.test(x.command)) return deny('G3', why);
  // Git Bash (MSYS) rewrites a single leading `/` into a path, so the switches agents actually type are `//IM`, `//FI`.
  const sw = (t) => String(t).replace(/^\/\//, '/');
  for (const s of x.segs) {
    const c = cmdInfo(s);
    if (!c) continue;
    const a = c.args.map((w) => sw(w.text));
    const nm = c.name;
    if (nm === 'taskkill') {
      if (a.some((t) => /^[/-]im$/i.test(t))) return deny('G3', why);
      for (let k = 0; k < a.length; k++) if (/^[/-]fi$/i.test(a[k]) && /imagename|windowtitle|services/i.test(a[k + 1] || '')) return deny('G3', why);
    }
    if (nm === 'pkill' || nm === 'killall') return deny('G3', why);
    if (nm === 'stop-process' || nm === 'spps' || (nm === 'kill' && s.shell === 'ps')) {
      if (a.some((t) => /^-(n|na|nam|name|processname|pn)(:.*)?$/i.test(t))) return deny('G3', why);
      if (/\b(Get-Process|gps)\s+(?!-Id\b|-PID\b)[^\s|)-]/i.test(s.raw)) return deny('G3', why);
    }
    if (['get-process', 'gps'].includes(nm) || (nm === 'ps' && s.shell === 'ps')) {
      const { named, pos } = psArgs(c.args);
      const byName = pos.length > 0 || typeof named.name === 'string' || typeof named.processname === 'string';
      if (byName && s.pipeNext) {
        const i = x.segs.indexOf(s);
        const nx = x.segs[i + 1] ? cmdInfo(x.segs[i + 1]) : null;
        if (nx && ['stop-process', 'spps', 'kill'].includes(nx.name)) return deny('G3', why);
      }
    }
    if (nm === 'kill' && /\b(pgrep|pidof)\b/.test(s.raw)) return deny('G3', why);
    if ((nm === 'kill' || nm === 'taskkill') && /\bxargs\b/.test(s.raw) && s.pipePrev) {
      const i = x.segs.indexOf(s);
      const prev = x.segs[i - 1] ? cmdInfo(x.segs[i - 1]) : null;
      if (prev && ['pgrep', 'ps', 'grep', 'tasklist', 'findstr', 'awk'].includes(prev.name)) return deny('G3', why);
    }
    if (nm === 'wmic' && /\bwhere\b/i.test(s.raw) && /name\s*=/i.test(s.raw) && /\b(delete|terminate)\b/i.test(s.raw)) return deny('G3', why);
  }
  return null;
}

function G4(x) {
  if (!x.shell) return null;
  const why = 'recursive delete or move under Assets/, Packages/ or ProjectSettings/ (or of a folder that contains them) is not allowed from the shell (it orphans .meta files and GUIDs). Use AssetDatabase through node Tools/kit/gate/game_gate.mjs exec.';
  const td = treeDeny(x, 'G4', unityProtRels(x));
  return td ? { ...td, reason: `[G4] ${why}` } : null;
}

const HTTP_CLIENTS = new Set(['curl', 'wget', 'invoke-webrequest', 'iwr', 'invoke-restmethod', 'irm', 'start-bitstransfer', 'bitsadmin', 'http', 'https', 'xh', 'aria2c']);

function hostsIn(text) {
  const hosts = [];
  for (const m of String(text).matchAll(/(?:https?|wss?):\/\/([^/\s'"`:?#)\]]+)/gi)) hosts.push(m[1].toLowerCase());
  for (const m of String(text).matchAll(/(?:^|[\s'"=(,])((?:[a-z0-9-]+\.)+[a-z]{2,})(?=[:/\s'"),]|$)/gi)) hosts.push(m[1].toLowerCase());
  return hosts;
}

function G7(x) {
  if (!x.shell) return null;
  const list = (x.policy.provider_hosts || []).map((h) => String(h).toLowerCase());
  if (!list.length) return null;
  for (const s of x.segs) {
    const c = cmdInfo(s);
    if (!c) continue;
    const code = interpCode(s);
    if (!HTTP_CLIENTS.has(c.name) && code === null) continue;
    const text = code ?? s.raw;
    for (const h of hostsIn(text)) {
      const hit = list.find((p) => h === p || h.endsWith('.' + p));
      if (hit) return deny('G7', `raw HTTP to the paid provider ${hit}; generation goes through assetgen (uv run --project Tools/kit/assetgen assetgen run ...), which reserves budget before it submits.`);
    }
  }
  return null;
}

const G8_SERVERS = {
  'fal-ai': { provider: 'fal', mut: (t) => ['submit_job', 'run_model', 'upload_file'].includes(t) },
  higgsfield: { provider: 'higgsfield', mut: (t) => /generate|create|submit|run/.test(t) },
  meshy: { provider: 'meshy', mut: (t) => /create|generate|rig|remesh|texture|animate/.test(t) },
  scenario: { provider: 'scenario', mut: (t) => /run|generate|train/.test(t) },
};
const ENDPOINT_KEYS = new Set(['model_id', 'model', 'endpoint', 'endpoint_id', 'app_id', 'application', 'modelid']);

function endpointStrings(ti, known) {
  const out = new Set();
  const walk = (v, key, depth) => {
    if (depth > 10) return;
    if (typeof v === 'string') {
      if ((key && ENDPOINT_KEYS.has(key.toLowerCase())) || known.has(v)) out.add(v);
    } else if (Array.isArray(v)) v.forEach((e) => walk(e, key, depth + 1));
    else if (isObj(v)) for (const [k, e] of Object.entries(v)) walk(e, k, depth + 1);
  };
  walk(ti, null, 0);
  return [...out];
}

function G8(x) {
  const m = /^mcp__([^_]+(?:-[^_]+)*)__(.+)$/.exec(x.tool);
  if (!m) return null;
  const server = m[1];
  const tool = m[2];
  const def = G8_SERVERS[server];
  if (!def || !def.mut(tool)) return null;
  const providersDoc = readJsonOr(path.join(x.ws, 'Tools', 'kit', 'assetgen', 'providers.json'), null, { onInvalid: 'fallback' });
  let provider = def.provider;
  if (isObj(providersDoc?.providers)) {
    for (const [id, p] of Object.entries(providersDoc.providers)) if (isObj(p) && p.mcp_server === server) provider = id;
  }
  const endpoints = Array.isArray(providersDoc?.endpoints) ? providersDoc.endpoints.filter(isObj) : [];
  const known = new Set(endpoints.map((e) => e.endpoint).filter((e) => typeof e === 'string'));
  const found = endpointStrings(x.ti, known);
  const how = `run uv run --project Tools/kit/assetgen assetgen reserve --provider ${provider} --endpoint <endpoint> --attempt <asset/stage/aNN> --tools ${x.tool} first`;
  if (server === 'fal-ai' && tool === 'run_model') {
    const fast = found.length > 0 && found.every((f) => endpoints.some((e) => e.endpoint === f && e.provider === provider && e.fast === true));
    if (!fast) return deny('G8', `run_model is allowed only for endpoints marked fast:true in providers.json (it returns nothing until the job ends, so a timeout loses the receipt). Use submit_job and ${how}.`);
  }
  if (x.audit) return deny('G8', `${x.tool} spends money and needs an open reservation: ${how}.`);
  const dir = path.join(x.ws, '.harness', 'reservations');
  let files = [];
  try { files = fs.readdirSync(dir).filter((f) => f.endsWith('.json')).sort(); } catch { files = []; }
  const cands = [];
  for (const f of files) {
    const doc = readJsonOr(path.join(dir, f), null, { onInvalid: 'fallback' });
    if (!isObj(doc) || doc.status !== 'open' || doc.provider !== provider) continue;
    const exp = parseUtc(doc.expires_utc);
    if (!exp || exp.getTime() <= x.now.getTime()) continue;
    if (!Array.isArray(doc.tools) || !doc.tools.includes(x.tool)) continue;
    if (found.length && !found.includes(doc.endpoint)) continue;
    cands.push({ file: path.join(dir, f), doc });
  }
  if (!cands.length) return deny('G8', `${x.tool} spends money and needs an open, unexpired reservation for this provider, tool and endpoint: ${how}.`);
  cands.sort((a, b) => String(a.doc.created_utc).localeCompare(String(b.doc.created_utc)) || a.file.localeCompare(b.file));
  x.effects.push({ type: 'use_reservation', file: cands[0].file, doc: cands[0].doc });
  return null;
}

function G2(x) {
  if (!x.shell) return null;
  const git = x.policy.git || {};
  let result = null;
  for (const s of x.segs) {
    const g = gitOf(s);
    if (!g) continue;
    let key = null;
    if (['commit', 'merge', 'cherry-pick', 'revert', 'am', 'commit-tree'].includes(g.sub)) key = 'commit';
    else if (g.sub === 'init') key = 'init';
    else if (g.sub === 'push') key = 'push';
    else if (g.sub === 'lfs' && g.args[0] === 'migrate') key = 'lfs_migrate';
    if (!key) continue;
    const val = ['forbid', 'ask', 'allow'].includes(git[key]) ? git[key] : 'ask';
    if (val === 'allow') continue;
    const reason = `git ${g.sub}${key === 'lfs_migrate' ? ' migrate' : ''} is set to ${val} in .harness/policy.json (git.${key}); ${val === 'forbid' ? 'the owner decides git history (ARCHITECTURE §8 H12)' : 'the owner approves it'}.`;
    if (val === 'forbid') return deny('G2', reason);
    result = result || ask('G2', reason);
  }
  return result;
}

function pinned(policy, kind, spec) {
  return Boolean(spec) && (policy.pinned_packages?.[kind] || []).includes(spec);
}

function G13(x) {
  if (!x.shell) return null;
  for (const s of x.segs) {
    const c = cmdInfo(s);
    if (!c) continue;
    const a = c.args.map((w) => w.text);
    const nm = c.name;
    const lower = a.map((t) => t.toLowerCase());
    const q = (what) => ask('G13', `${what} installs software outside the project or runs an unpinned package; the owner approves each one (pin it in .harness/policy.json pinned_packages to skip this).`);
    const globalFlag = lower.some((t) => t === '-g' || t === '--global' || t === '--location=global');
    if (nm === 'winget' && ['install', 'upgrade', 'import', 'add'].includes(lower[0])) return q(`winget ${lower[0]}`);
    if (nm === 'npm' && ['i', 'install', 'add', 'update', 'up', 'link'].includes(lower[0]) && globalFlag) return q('npm install -g');
    if (nm === 'pnpm' && ['add', 'install', 'i', 'update'].includes(lower[0]) && globalFlag) return q('pnpm add -g');
    if (nm === 'yarn' && lower[0] === 'global' && lower[1] === 'add') return q('yarn global add');
    if (nm === 'bun' && ['add', 'install', 'i'].includes(lower[0]) && globalFlag) return q('bun add -g');
    if (/^pip(\d(\.\d+)?)?$/.test(nm) && lower[0] === 'install') return q('pip install');
    if (/^(python|python3(\.\d+)?|py|pythonw)$/.test(nm) && lower.includes('-m')) {
      const k = lower.indexOf('-m');
      if (/^pip\d?$/.test(lower[k + 1] || '') && lower[k + 2] === 'install') return q('python -m pip install');
    }
    if (nm === 'uv' && lower[0] === 'pip' && lower[1] === 'install' && lower.includes('--system')) return q('uv pip install --system');
    if (nm === 'uv' && lower[0] === 'tool' && lower[1] === 'install') return q('uv tool install');
    if (['pipx', 'cargo', 'choco', 'scoop', 'gem'].includes(nm) && lower[0] === 'install') return q(`${nm} install`);
    if (nm === 'dotnet' && lower[0] === 'tool' && lower[1] === 'install' && globalFlag) return q('dotnet tool install -g');
    if (['install-module', 'install-package', 'install-script'].includes(nm)) return q(c.word.text);
    const npxLike = ['npx', 'bunx', 'pnpx'].includes(nm) ? a : ((['pnpm', 'yarn'].includes(nm) && lower[0] === 'dlx') || (nm === 'npm' && ['exec', 'x'].includes(lower[0]))) ? a.slice(1) : null;
    if (npxLike) {
      let spec = null;
      for (let k = 0; k < npxLike.length; k++) {
        const t = npxLike[k];
        if (t === '-p' || t === '--package') { spec = npxLike[k + 1] ?? null; break; }
        const m = /^--package=(.+)$/.exec(t);
        if (m) { spec = m[1]; break; }
        if (/^-/.test(t)) continue;
        spec = t;
        break;
      }
      if (!pinned(x.policy, 'npx', spec)) return q(`${nm} ${spec ?? ''}`.trim());
    }
    const uvxArgs = nm === 'uvx' ? a : (nm === 'uv' && lower[0] === 'tool' && lower[1] === 'run') ? a.slice(2) : null;
    if (uvxArgs) {
      let spec = null;
      let from = null;
      for (let k = 0; k < uvxArgs.length; k++) {
        const t = uvxArgs[k];
        if (t === '--from') { from = uvxArgs[k + 1] ?? null; k++; continue; }
        const m = /^--from=(.+)$/.exec(t);
        if (m) { from = m[1]; continue; }
        if (['--python', '-p', '--with', '--with-requirements', '--index', '--index-url', '--extra-index-url', '-w'].includes(t)) { k++; continue; }
        if (/^-/.test(t)) continue;
        spec = t;
        break;
      }
      const s2 = from ?? spec;
      if (!pinned(x.policy, 'uvx', s2)) return q(`uvx ${s2 ?? ''}`.trim());
    }
    if (nm === 'unity' && lower[0] === 'pipeline' && lower[1] === 'install') return q('unity pipeline install');
    if (s.words.some((w) => /(^|[\\/])kit\.mjs$/i.test(w.text)) && s.words.some((w) => w.text === '--yes' || w.text === '-y')) return q('kit.mjs --yes');
  }
  return null;
}

// A run of one character this long is never a real command; it is only ever an attempt to make a scan run long enough
// to time the hook out (which would fail open). Fail closed instead. A plain loop, not a backreference regex (which is
// itself quadratic on runs just under the limit). main() also caps the whole evaluation with a timer.
const RUN_MAX = 20000;
function longestRun(s) {
  let best = 0;
  let run = 0;
  for (let i = 0; i < s.length; i++) {
    run = i > 0 && s.charCodeAt(i) === s.charCodeAt(i - 1) ? run + 1 : 1;
    if (run > best) best = run;
  }
  return best;
}
function G0(x) {
  if (!x.shell) return null;
  if (longestRun(x.command) > RUN_MAX) return deny('G0', 'the command has an extremely long run of one character; split it into smaller commands (the guard fails closed rather than risk timing out).');
  return null;
}

const CD_CMDS = new Set(['cd', 'chdir', 'pushd', 'set-location', 'sl', 'push-location']);

/**
 * Set s.cwd on every segment, following cd/chdir/pushd/Set-Location. Grouped by lexed script (depth 0, each nested
 * shell, each substitution) so a cd inside `bash -c '…'` applies to that script's later commands; a bash `( … )`
 * subshell's cd is scoped to the subshell (seg.subs) and does not leak out (K1-N2).
 */
function assignCwds(x, cwd0) {
  const scripts = new Map();
  for (const s of x.segs) { const g = scripts.get(s.script); if (g) g.push(s); else scripts.set(s.script, [s]); }
  const groups = [...scripts.values()].sort((a, b) => Math.min(...a.map((s) => s.depth)) - Math.min(...b.map((s) => s.depth)));
  for (const segs of groups) {
    segs.sort((a, b) => a.order - b.order);
    const parent = segs[0].parent;
    let cwd = parent && parent.cwd ? parent.cwd : cwd0;
    const saved = [];
    let prev = 0;
    for (const s of segs) {
      const d = s.subs.length;
      if (d > prev) for (let k = prev; k < d; k++) saved.push(cwd);
      else if (d < prev) for (let k = d; k < prev; k++) { const v = saved.pop(); if (v !== undefined) cwd = v; }
      prev = d;
      s.cwd = cwd;
      const c = cmdInfo(s);
      if (!c) continue;
      const isCd = s.shell === 'cmd' ? (c.name === 'cd' || c.name === 'chdir') : CD_CMDS.has(c.name);
      if (!isCd) continue;
      // cmd switches start with / (cd /d X); bash and PowerShell options with - (an absolute /c/… path is a target).
      const arg = c.args.find((w) => (s.shell === 'cmd' ? !/^\//.test(w.text) : !/^-/.test(w.text)));
      if (!arg) { if (s.shell !== 'cmd' && !c.args.length) cwd = homeDir(); continue; }
      const r = resolvePath(arg.text, cwd, x);
      if (r && !r.foreign) cwd = r.real ?? r.abs;
    }
  }
}

const RULES = [G0, G12, G15, G16, G14, G5b, G6, G5, G1, G9, G10, G10b, G11, G3, G4, G7, G8, G2, G13];

/**
 * Evaluate one PreToolUse call. Pure except for reading harness state; side effects are returned in `effects`.
 * @param {object} input hook stdin (tool_name, tool_input, cwd, …)
 * @param {{ws: string, now?: Date, policy?: object, audit?: boolean}} env audit: stateless (offline transcript audit)
 * @returns {{decision: 'deny'|'ask'|null, rule?: string, reason?: string, effects: object[]}}
 */
export function evaluate(input, env) {
  const ws = env.ws;
  const tool = typeof input.tool_name === 'string' ? input.tool_name : '';
  const ti = isObj(input.tool_input) ? input.tool_input : {};
  const policy = env.policy ?? loadPolicy(ws).policy;
  policy._g14_exempt = ['uv run --project Tools/kit/assetgen assetgen', 'Tools/kit/bin/assetgen'];
  const cwd0 = typeof input.cwd === 'string' && input.cwd ? path.resolve(ws, fromShellPath(input.cwd)) : ws;
  let unity = ws;
  try {
    const kit = readJsonOr(path.join(ws, 'Tools', 'kit', 'kit.json'), null, { onInvalid: 'fallback' });
    const sub = typeof kit?.unity_subdir === 'string' ? kit.unity_subdir : '.';
    if (sub && sub !== '.' && !/^[\\/]|^[A-Za-z]:|\.\./.test(sub)) unity = path.join(ws, sub);
  } catch { /* default U = W */ }
  const realCache = new Map();
  const mk = (command) => {
    const x = { ws, unity, tool, ti, policy, now: env.now ?? now(), audit: Boolean(env.audit), cwd: cwd0, effects: [], shell: false, segs: [], command: '', grantCommand: null, _realCache: realCache };
    if (command !== null) {
      x.shell = true;
      x.command = command;
      x.segs = parseShell(command, tool === 'PowerShell' ? 'ps' : 'bash');
      assignCwds(x, cwd0);
      expandScriptFiles(x, cwd0);
    }
    return x;
  };
  const isShell = SHELL_TOOLS.has(tool) && typeof ti.command === 'string';
  const x = mk(isShell ? ti.command : null);
  const first = runRules(x);
  if (first) return { ...first, effects: [] };
  if (isShell) {
    // The same command with its shell variables expanded (p=.harness; touch $p/PAUSE): a deny or ask there counts too.
    for (const v of varVariants(x)) {
      const xv = mk(v);
      xv.grantCommand = x.command;
      const d = runRules(xv);
      if (d) return { ...d, reason: `${d.reason} (after expanding the command's variables)`, effects: [] };
    }
  }
  return { decision: null, effects: x.effects };
}

function runRules(x) {
  for (const rule of RULES) {
    const d = rule(x);
    if (d) return d;
  }
  return null;
}

// ---------------------------------------------------------------------------------------------------------------
// Script files run by path, and shell variables

const SCRIPT_MAX_BYTES = 256 * 1024;
const SCRIPT_MAX_FILES = 8;

/** {word, shell} when a segment runs a shell script file (bash x.sh, source x.sh, ./x.sh, & ./x.ps1, -File x.ps1, x.bat). */
function scriptFileOf(seg) {
  const c = cmdInfo(seg);
  if (!c) return null;
  const a = c.args;
  const w = c.word.text;
  if (c.viaStart && ['powershell', 'pwsh', 'bash', 'sh', 'cmd'].includes(c.name)) {
    // Start-Process powershell -ArgumentList '-File','x.ps1' (or bash x.sh, cmd /c x.bat): the script is in the list.
    const list = startArgList(seg).replace(/,/g, ' ');
    const m = /(?:^|\s)-f(?:i(?:le?)?)?\s+("[^"]+"|'[^']+'|\S+)/i.exec(list) || /(?:^|\s)("[^"]+\.(?:ps1|sh|bash|bat|cmd)"|\S+\.(?:ps1|sh|bash|bat|cmd))(?=\s|$)/i.exec(list);
    if (m) {
      const word = m[1].replace(/^["']|["']$/g, '');
      return { word, shell: /\.ps1$/i.test(word) ? 'ps' : /\.(bat|cmd)$/i.test(word) ? 'cmd' : 'bash' };
    }
  }
  if (/\.(sh|bash)$/i.test(w) && seg.shell !== 'cmd') return { word: w, shell: 'bash' };
  if (/\.ps1$/i.test(w)) return { word: w, shell: 'ps' };
  if (/\.(bat|cmd)$/i.test(w)) return { word: w, shell: 'cmd' };
  if (['bash', 'sh', 'zsh', 'dash', 'ksh'].includes(c.name)) {
    if (seg.heredoc || a.some((x) => !x.quoted && /^-[a-zA-Z]*c[a-zA-Z]*$/.test(x.text))) return null;
    for (let k = 0; k < a.length; k++) {
      const t = a[k].text;
      if (['-o', '+o', '-O', '+O', '--rcfile', '--init-file'].includes(t)) { k++; continue; }
      if (/^[-+]/.test(t) && !a[k].quoted) continue;
      return { word: t, shell: 'bash' };
    }
    return null;
  }
  if (c.name === 'source' && seg.shell === 'bash' && a[0]) return { word: a[0].text, shell: 'bash' };
  if (c.name === 'powershell' || c.name === 'pwsh') {
    const k = a.findIndex((x) => /^-(f|fi|fil|file)$/i.test(x.text));
    if (k >= 0 && a[k + 1]) return { word: a[k + 1].text, shell: 'ps' };
    const p = a.find((x) => /\.ps1$/i.test(x.text));
    if (p && !a.some((x) => /^-(c|co|com|comm|comma|comman|command|e|ec|en|enc\w*)$/i.test(x.text))) return { word: p.text, shell: 'ps' };
  }
  return null;
}

/**
 * Lex the shell script files the command runs (only files inside the workspace or %TEMP%/claude, and not kit code,
 * which G5 already protects) as nested scripts, so a script written in one call and run in the next is still checked.
 */
function expandScriptFiles(x, cwd0) {
  let roots;
  try { roots = kitCodeRoots(x.ws); } catch { roots = kitCodeRoots(x.ws, { unitySubdir: '.' }); }
  let files = 0;
  for (let round = 0; round < MAX_DEPTH && files < SCRIPT_MAX_FILES; round++) {
    const fresh = [];
    for (const s of x.segs) {
      if (s._fileDone || files >= SCRIPT_MAX_FILES) continue;
      s._fileDone = true;
      if (s.depth + 1 >= MAX_DEPTH) continue;
      const f = scriptFileOf(s);
      if (!f) continue;
      const r = resolvePath(f.word, s.cwd || x.cwd, x);
      if (!r || r.foreign) continue;
      const inside = r.rel !== null || isInside(r.abs, scratchRoot());
      if (!inside || (r.rel !== null && matchesAny(r.rel, roots)) || (r.realRel && matchesAny(r.realRel, roots))) continue;
      let text;
      try {
        const st = fs.statSync(r.abs);
        if (!st.isFile() || st.size > SCRIPT_MAX_BYTES) continue;
        text = fs.readFileSync(r.abs, 'utf8');
      } catch { continue; }
      files++;
      lexInto(text, f.shell, s.depth + 1, s, fresh);
    }
    if (!fresh.length) break;
    x.segs.push(...fresh);
    assignCwds(x, cwd0);
  }
}

const SAFE_VALUE = /^[\w.\/\\:~+@*?[\]{},=-]+$/;
const MAX_VARIANTS = 4;

/** Shell variable assignments in the command (bash NAME=v, export/declare, for-in lists; PowerShell $n = v, $env:N = v,
 * Set-Variable; cmd set N=v): Map name → {shell, values[]}. Only plain values (no spaces, quotes or expansions). */
function assignments(x) {
  const out = new Map();
  const add = (shell, name, value) => {
    if (typeof value !== 'string' || !value || !SAFE_VALUE.test(value)) return;
    const key = `${shell}:${shell === 'bash' ? name : name.toLowerCase()}`;
    const e = out.get(key) || { shell, name, values: [] };
    if (!e.values.includes(value)) e.values.push(value);
    out.set(key, e);
  };
  for (const s of x.segs) {
    const w = s.words.filter((t) => t.text !== undefined);
    if (!w.length) continue;
    if (s.shell === 'bash') {
      let k = 0;
      if (!w[0].quoted && ['export', 'declare', 'typeset', 'local', 'readonly'].includes(w[0].text)) { k = 1; while (k < w.length && /^-/.test(w[k].text)) k++; }
      for (; k < w.length; k++) {
        const m = /^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/s.exec(w[k].text);
        if (!m) break;
        add('bash', m[1], m[2]);
      }
      if (w[0].text === 'for' && w[2]?.text === 'in') for (const v of w.slice(3)) add('bash', w[1].text, v.text);
    } else if (s.shell === 'ps') {
      const m0 = /^\$\{?((?:env:)?[A-Za-z_][\w]*)\}?(?:=(.*))?$/is.exec(w[0].text);
      if (m0 && m0[2] !== undefined && w.length === 1) add('ps', m0[1], m0[2]);
      else if (m0 && w[1]?.text === '=' && w.length === 3) add('ps', m0[1], w[2].text);
      const c = cmdInfo(s);
      if (c && ['set-variable', 'sv', 'new-variable', 'nv'].includes(c.name)) {
        const { named, pos } = psArgs(c.args);
        const name = typeof named.name === 'string' ? named.name : pos[0];
        const value = typeof named.value === 'string' ? named.value : (typeof named.name === 'string' ? pos[0] : pos[1]);
        if (name) add('ps', name, value);
      }
      if (/^foreach$/i.test(w[0].text) && w[1]?.paren) {
        const m = /^\s*\$([A-Za-z_]\w*)\s+in\s+(.+?)\s*$/is.exec(w[1].paren);
        if (m) for (const v of m[2].split(',')) add('ps', m[1], v.trim().replace(/^['"]|['"]$/g, ''));
      }
    } else if (s.shell === 'cmd') {
      const c = cmdInfo(s);
      if (c && c.name === 'set' && c.args.length) {
        const m = /^([A-Za-z_][\w]*)=(.*)$/s.exec(c.args.map((t) => t.text).join(' ').replace(/^\/[a-z]\s+/i, ''));
        if (m) add('cmd', m[1], m[2]);
      }
    }
  }
  return out;
}

/** Up to MAX_VARIANTS copies of the command with its assigned variables substituted (value i of each variable). */
function varVariants(x) {
  const vars = [...assignments(x).values()];
  if (!vars.length) return [];
  const n = Math.min(MAX_VARIANTS, Math.max(...vars.map((v) => v.values.length)));
  const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const out = [];
  for (let i = 0; i < n; i++) {
    let text = x.command;
    for (const v of vars) {
      const val = v.values[Math.min(i, v.values.length - 1)];
      const rep = () => val;
      const nm = esc(v.name);
      if (v.shell === 'bash') text = text.replace(new RegExp(`\\$\\{${nm}\\}|\\$${nm}(?![A-Za-z0-9_])`, 'g'), rep);
      else if (v.shell === 'ps') text = text.replace(new RegExp(`\\$\\{${nm}\\}|\\$${nm}(?![\\w:])`, 'gi'), rep);
      else text = text.replace(new RegExp(`%${nm}%|!${nm}!`, 'gi'), rep);
    }
    if (text !== x.command && !out.includes(text)) out.push(text);
  }
  return out;
}

/**
 * Claim the single-use tokens a passing call spends (H-10): under a lock, re-read the token and spend it only if it is
 * still unspent, so two calls issued together cannot both pass on one reservation or override. Runs only when the
 * final decision is pass (never for ask, so a declined prompt spends nothing). Returns a deny when a token was already
 * spent, or when the claim cannot be made (single use cannot be guaranteed, so it fails closed).
 */
export function claimEffects(ws, effects, input, t) {
  const extra = {
    ...(typeof input.tool_use_id === 'string' ? { tool_use_id: input.tool_use_id } : {}),
    ...(typeof input.session_id === 'string' ? { session_id: input.session_id } : {}),
  };
  for (const e of effects) {
    try {
      if (e.type === 'consume_override') {
        const file = path.join(ws, '.harness', 'overrides.jsonl');
        const ok = withLockSync(path.join(ws, '.harness', 'overrides.lock'), () => {
          if (readJsonl(file).some((l) => isObj(l) && l.event === 'consumed' && l.id === e.id)) return false;
          appendJsonl(file, { event: 'consumed', id: e.id, utc: utcIso(t), ...extra });
          return true;
        }, { waitMs: 3000, purpose: 'guard G1 claim' });
        if (!ok) return deny('G1', `override ${e.id} was already used by another call; the owner types a new OWNER-OVERRIDE: <command> for another run.`);
      } else if (e.type === 'use_reservation') {
        const ok = withLockSync(`${e.file}.lock`, () => {
          const cur = readJsonOr(e.file, null, { onInvalid: 'fallback' });
          if (!isObj(cur) || cur.status !== 'open') return false;
          writeJsonAtomic(e.file, { ...cur, status: 'used', used_utc: utcIso(t), ...(extra.tool_use_id ? { tool_use_id: extra.tool_use_id } : {}) });
          return true;
        }, { waitMs: 3000, purpose: 'guard G8 claim' });
        if (!ok) return deny('G8', `reservation ${path.basename(e.file)} was used by another call issued at the same time; run assetgen reserve again for this call.`);
      }
    } catch (err) {
      try { appendJsonl(path.join(ws, '.harness', 'guard_errors.jsonl'), { utc: utcIso(now()), error: `claim ${e.type}: ${err.message}`, tool_name: String(input.tool_name || ''), fallback: 'deny' }); } catch { /* ignore */ }
      return deny(e.type === 'use_reservation' ? 'G8' : 'G1', `the single-use token could not be claimed (${String(err.message).slice(0, 120)}); try the call again.`);
    }
  }
  return null;
}

const EVAL_FLOOR_MS = 1500;
/** Whole-hook budget from process start, inside the 10 s PreToolUse timeout (a timed-out hook does not block the call).
 * GSK_GUARD_BUDGET_MS overrides it (tests). */
function guardBudgetMs() {
  const v = Number(process.env.GSK_GUARD_BUDGET_MS);
  return Number.isFinite(v) && v > 0 ? v : 8000;
}

/**
 * evaluate() under a hard time limit (K1-N4): a watchdog stops a slow evaluation (a regex or scan an input made slow)
 * and the call is denied with G0, so a slow guard fails closed instead of timing out open.
 */
function evaluateCapped(input, env) {
  // At least EVAL_FLOOR_MS for the evaluation itself, so a machine too busy to start the hook quickly does not turn
  // every call into a G0 deny (a hostile slow input is still cut off after the floor).
  const budget = guardBudgetMs();
  const left = Math.max(Math.min(EVAL_FLOOR_MS, budget), Math.floor(budget - process.uptime() * 1000));
  try {
    return vm.runInNewContext('evaluateCall()', { evaluateCall: () => evaluate(input, env) }, { timeout: left });
  } catch (e) {
    if (e && e.code === 'ERR_SCRIPT_EXECUTION_TIMEOUT') {
      try { appendJsonl(path.join(env.ws, '.harness', 'guard_errors.jsonl'), { utc: utcIso(now()), error: `evaluation exceeded ${left} ms`, tool_name: String(input.tool_name || ''), fallback: 'deny' }); } catch { /* ignore */ }
      return { decision: 'deny', rule: 'G0', reason: '[G0] the guard could not finish checking this call in time; split the command into smaller commands.', effects: [] };
    }
    throw e;
  }
}

function sideEffects(ws, input, decision) {
  const t = now();
  try {
    fs.mkdirSync(path.join(ws, '.harness'), { recursive: true });
    fs.writeFileSync(path.join(ws, '.harness', 'heartbeat'), utcIso(t) + '\n');
  } catch { /* best effort */ }
  if (decision === 'deny' || !EDIT_TOOLS.has(input.tool_name)) return;
  const ti = isObj(input.tool_input) ? input.tool_input : {};
  const p = ti.file_path ?? ti.notebook_path;
  const sid = typeof input.session_id === 'string' && /^[A-Za-z0-9._-]+$/.test(input.session_id) ? input.session_id : 'unknown';
  if (typeof p !== 'string') return;
  const cwd = typeof input.cwd === 'string' && input.cwd ? path.resolve(ws, fromShellPath(input.cwd)) : ws;
  const rel = wsRel(ws, path.resolve(cwd, fromShellPath(p)));
  if (!rel) return;
  try {
    fs.mkdirSync(path.join(ws, '.harness', 'touched'), { recursive: true });
    fs.appendFileSync(path.join(ws, '.harness', 'touched', `${sid}.txt`), rel + '\n');
  } catch { /* best effort */ }
}

export async function main() {
  await runHook('guard', (input, { ws, invalid }) => {
    if (invalid || typeof input.tool_name !== 'string') {
      appendJsonl(path.join(ws, '.harness', 'guard_errors.jsonl'), { utc: utcIso(now()), error: invalid ? 'stdin is not a JSON object' : 'no tool_name in stdin', fallback: 'none' });
      return null;
    }
    const pol = loadPolicy(ws);
    if (pol.error) appendJsonl(path.join(ws, '.harness', 'guard_errors.jsonl'), { utc: utcIso(now()), error: pol.error, tool_name: input.tool_name, fallback: 'none' });
    let r = evaluateCapped(input, { ws, policy: pol.policy });
    if (!r.decision && r.effects.length) r = claimEffects(ws, r.effects, input, now()) ?? r;
    sideEffects(ws, input, r.decision);
    if (!r.decision) return null;
    return { stdout: { hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision: r.decision, permissionDecisionReason: r.reason } } };
  });
}

if (isMain(import.meta.url)) await main();

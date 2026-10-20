// C# source helpers for unity_rules_lint.mjs: a small lexer that blanks comments and string contents while keeping every
// offset and line number, a type/member scanner over the blanked text, and line lookup. Heuristic by design: it never
// compiles, and it only has to be right for the shapes the lint rules look at.

/**
 * @typedef {{code: string, bare: string}} Stripped
 * code: comments replaced by spaces (strings kept). bare: comments and string/char literal contents replaced by spaces
 * (quote characters and interpolation holes kept). Newlines are kept everywhere, so offsets and lines match the source.
 */

const blank = (s) => s.replace(/[^\r\n]/g, ' ');

/**
 * Strip comments and literals from C# source.
 * Handles // and /* *\/ comments, "regular" strings with escapes, @"verbatim" strings, $"interpolated" strings with
 * {holes} (the hole is code, and may itself contain strings), $@/@$ verbatim interpolated strings, and 'c' char literals.
 * Preprocessor lines (#region text and the like) are blanked in both outputs.
 * @param {string} src
 * @returns {Stripped}
 */
export function stripCs(src) {
  const s = String(src).replace(/^\uFEFF/, '');
  const code = s.split('');
  const bare = s.split('');
  const n = s.length;
  const blankRange = (arr, a, b) => {
    for (let k = a; k < b; k++) if (arr[k] !== '\n' && arr[k] !== '\r') arr[k] = ' ';
  };

  // Lex a string literal starting at i (pointing at the opening quote, prefix already consumed by the caller).
  // Returns the index just past the closing quote.
  const lexString = (i, verbatim, interp) => {
    let j = i + 1;
    while (j < n) {
      const c = s[j];
      if (!verbatim && c === '\\') { blankRange(bare, j, Math.min(j + 2, n)); j += 2; continue; }
      if (c === '"') {
        if (verbatim && s[j + 1] === '"') { blankRange(bare, j, j + 2); j += 2; continue; }
        return j + 1;
      }
      if (!verbatim && (c === '\n' || c === '\r')) return j; // unterminated regular string: stop at the line end
      if (interp && c === '{') {
        if (s[j + 1] === '{') { blankRange(bare, j, j + 2); j += 2; continue; }
        j = lexCode(j + 1, true); // hole: code until the matching }
        continue;
      }
      if (interp && c === '}' && s[j + 1] === '}') { blankRange(bare, j, j + 2); j += 2; continue; }
      blankRange(bare, j, j + 1);
      j++;
    }
    return j;
  };

  // Lex code from i. With stopAtBrace, returns just past the } that closes an interpolation hole.
  const lexCode = (i, stopAtBrace) => {
    let depth = 0;
    let lineStart = true;
    while (i < n) {
      const c = s[i];
      if (c === '\n') { lineStart = true; i++; continue; }
      if (lineStart && c === '#' && !stopAtBrace) {
        let e = s.indexOf('\n', i);
        if (e < 0) e = n;
        blankRange(code, i, e);
        blankRange(bare, i, e);
        i = e;
        continue;
      }
      if (c !== ' ' && c !== '\t' && c !== '\r') lineStart = false;
      if (c === '/' && s[i + 1] === '/') {
        let e = s.indexOf('\n', i);
        if (e < 0) e = n;
        blankRange(code, i, e);
        blankRange(bare, i, e);
        i = e;
        continue;
      }
      if (c === '/' && s[i + 1] === '*') {
        let e = s.indexOf('*/', i + 2);
        e = e < 0 ? n : e + 2;
        blankRange(code, i, e);
        blankRange(bare, i, e);
        i = e;
        continue;
      }
      if (c === '$' || c === '@') {
        const two = s.slice(i, i + 3);
        if (two === '$@"' || two === '@$"') { i = lexString(i + 2, true, true); continue; }
        if (s[i + 1] === '"') { i = lexString(i + 1, c === '@', c === '$'); continue; }
      }
      if (c === '"') { i = lexString(i, false, false); continue; }
      if (c === "'") {
        // char literal: 'x', '\n', '\'', 'A'
        let j = i + 1;
        if (s[j] === '\\') j += 2;
        else j += 1;
        while (j < n && s[j] !== "'" && s[j] !== '\n' && j - i < 10) j++;
        if (s[j] === "'") { blankRange(bare, i + 1, j); i = j + 1; continue; }
        i++;
        continue;
      }
      if (stopAtBrace) {
        if (c === '{') depth++;
        else if (c === '}') {
          if (depth === 0) return i + 1;
          depth--;
        }
      }
      i++;
    }
    return i;
  };

  lexCode(0, false);
  return { code: code.join(''), bare: bare.join('') };
}

/** Offsets of the start of every line (index 0 = line 1). @param {string} s */
export function lineStarts(s) {
  const out = [0];
  for (let i = 0; i < s.length; i++) if (s[i] === '\n') out.push(i + 1);
  return out;
}

/** 1-based line of an offset. @param {number[]} starts @param {number} off */
export function lineOf(starts, off) {
  let lo = 0;
  let hi = starts.length - 1;
  while (lo < hi) {
    const mid = (lo + hi + 1) >> 1;
    if (starts[mid] <= off) lo = mid;
    else hi = mid - 1;
  }
  return lo + 1;
}

/** Remove balanced <...> generic argument lists (so commas inside them do not split declarators). @param {string} t */
export function stripGenerics(t) {
  let prev;
  let s = t;
  do {
    prev = s;
    s = s.replace(/<[^<>]*>/g, (m) => ' '.repeat(m.length));
  } while (s !== prev);
  return s;
}

/** Remove leading [Attribute(...)] groups from a member header; returns {attrs, rest}. @param {string} h */
export function splitAttributes(h) {
  let i = 0;
  const attrs = [];
  const n = h.length;
  for (;;) {
    while (i < n && /\s/.test(h[i])) i++;
    if (h[i] !== '[') break;
    let depth = 0;
    const start = i;
    for (; i < n; i++) {
      if (h[i] === '[') depth++;
      else if (h[i] === ']') { depth--; if (depth === 0) { i++; break; } }
    }
    attrs.push(h.slice(start, i));
  }
  return { attrs, rest: h.slice(i) };
}

const TYPE_RE = /\b(class|struct|interface|record|enum)\s+(?:(?:class|struct)\s+)?([A-Za-z_]\w*)/;

/**
 * Offset of the first '=' or '=>' outside (), [] and {} in a member header (a field initialiser or an expression body),
 * or -1. Comparison and operator headers (`operator ==`) have none. Generics must already be stripped.
 * @param {string} h
 */
export function assignIndex(h) {
  if (/\boperator\b/.test(h)) return -1;
  let depth = 0;
  for (let i = 0; i < h.length; i++) {
    const c = h[i];
    if (c === '(' || c === '[' || c === '{') depth++;
    else if (c === ')' || c === ']' || c === '}') depth = Math.max(0, depth - 1);
    else if (c === '=' && depth === 0) {
      const prev = h[i - 1];
      if (prev === '=' || prev === '!' || prev === '<' || prev === '>') continue;
      if (h[i + 1] === '=') { i++; continue; }
      return i;
    }
  }
  return -1;
}

/**
 * @typedef {{kind: 'field'|'body', header: string, headerStart: number, bodyStart?: number, bodyEnd?: number,
 *   typeName: string, typePath: string}} Member
 * kind 'field': a declaration ended by ';' directly inside a type body (fields, events, expression-bodied members,
 * abstract or extern members). kind 'body': a member with a { } body directly inside a type (methods, properties,
 * constructors, operators, accessors); bodyStart/bodyEnd are the offsets of its braces.
 */

/**
 * Scan blanked C# (stripCs().bare) for type bodies and their direct members.
 * Braces opened after an '=' in a member header (collection or object initialisers, lambdas in field initialisers)
 * belong to that declaration, not to a new member.
 * @param {string} bare
 * @returns {{types: {name: string, path: string, start: number, end: number, kind: string}[], members: Member[]}}
 */
export function scanMembers(bare) {
  const types = [];
  const members = [];
  /** @type {{kind: string, name?: string, path?: string, start: number, header?: string, headerStart?: number, typeName?: string, typePath?: string, typeIdx?: number}[]} */
  const stack = [{ kind: 'file', start: 0 }];
  let segStart = 0; // start of the current header at this level
  let i = 0;
  const n = bare.length;
  const top = () => stack[stack.length - 1];
  const enclosingType = () => {
    for (let k = stack.length - 1; k >= 0; k--) if (stack[k].kind === 'type') return stack[k];
    return null;
  };
  while (i < n) {
    const c = bare[i];
    if (c === '{') {
      const t = top();
      const header = bare.slice(segStart, i);
      if (t.kind === 'init') {
        stack.push({ kind: 'init', start: i }); // nested initialiser: the declaration header stays open
        i++;
        continue;
      } else if (t.kind === 'type' || t.kind === 'file' || t.kind === 'namespace') {
        const bareHeader = stripGenerics(splitAttributes(header).rest);
        const tm = TYPE_RE.exec(bareHeader);
        const eqIdx = assignIndex(bareHeader);
        if (t.kind === 'type' && eqIdx >= 0 && !(tm && tm.index < eqIdx)) {
          // initialiser or expression body braces inside a member declaration: keep the header open
          stack.push({ kind: 'init', start: i });
          i++;
          continue;
        }
        if (tm && (eqIdx < 0 || tm.index < eqIdx) && !/\(/.test(bareHeader.slice(0, tm.index))) {
          const outer = enclosingType();
          const name = tm[2];
          const p = outer ? `${outer.path}.${name}` : name;
          const entry = { kind: 'type', name, path: p, start: i, typeKind: tm[1], typeIdx: types.length };
          types.push({ name, path: p, start: i, end: -1, kind: tm[1] });
          stack.push(entry);
        } else if (/\bnamespace\b/.test(bareHeader) && t.kind !== 'type') {
          stack.push({ kind: 'namespace', start: i });
        } else if (t.kind === 'type') {
          stack.push({ kind: 'member', start: i, header, headerStart: segStart, typeName: t.name, typePath: t.path });
        } else {
          stack.push({ kind: 'block', start: i });
        }
      } else {
        stack.push({ kind: 'block', start: i });
      }
      segStart = i + 1;
      i++;
      continue;
    }
    if (c === '}') {
      const t = stack.length > 1 ? stack.pop() : top();
      if (t.kind === 'init') { i++; continue; } // the declaration continues after an initialiser
      if (t.kind === 'type') types[t.typeIdx].end = i;
      if (t.kind === 'member') {
        members.push({ kind: 'body', header: t.header, headerStart: t.headerStart, bodyStart: t.start, bodyEnd: i, typeName: t.typeName, typePath: t.typePath });
      }
      segStart = i + 1;
      i++;
      continue;
    }
    if (c === ';') {
      const t = top();
      if (t.kind === 'type') {
        const header = bare.slice(segStart, i);
        if (header.trim()) members.push({ kind: 'field', header, headerStart: segStart, typeName: t.name, typePath: t.path });
        segStart = i + 1;
      } else if (t.kind !== 'init') {
        segStart = i + 1;
      }
      i++;
      continue;
    }
    i++;
  }
  return { types, members };
}

/** Method name of a member header, or null when the header is not a method-like member. @param {string} header */
export function methodName(header) {
  const h = stripGenerics(splitAttributes(header).rest);
  const paren = h.indexOf('(');
  if (paren < 0) return null;
  const eq = assignIndex(h);
  if (eq >= 0 && eq < paren) return null;
  const m = /([A-Za-z_]\w*)\s*$/.exec(h.slice(0, paren));
  return m ? m[1] : null;
}

/** Identifiers used in a span of blanked code. @param {string} text */
export function identifiers(text) {
  const out = new Set();
  for (const m of text.matchAll(/[A-Za-z_]\w*/g)) out.add(m[0]);
  return out;
}

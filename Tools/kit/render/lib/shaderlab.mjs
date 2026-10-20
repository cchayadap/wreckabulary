// shaderlab.mjs: a light structural reader for hand-written .shader files (ShaderLab plus HLSL/CG blocks), enough for
// render_lint's pass-tag, SRP Batcher and keyword rules. It is not a compiler: it finds the Shader name, SubShaders,
// their Tags, Passes (with Tags, Name, program text), UsePass and GrabPass statements, and the HLSLINCLUDE/CGINCLUDE
// blocks at each level. Comments are blanked first (newlines kept, so offsets map to the original line numbers), and
// program blocks are masked before braces are matched, so HLSL braces never confuse the ShaderLab structure.

/**
 * Blank // and /* *\/ comments outside string literals, keeping every newline and the text length.
 * @param {string} src
 */
export function stripComments(src) {
  const s = String(src).replace(/^﻿/, '').replace(/\r\n?/g, '\n');
  let out = '';
  let i = 0;
  while (i < s.length) {
    const c = s[i];
    const d = s[i + 1];
    if (c === '"') {
      let j = i + 1;
      while (j < s.length && s[j] !== '"' && s[j] !== '\n') j += s[j] === '\\' ? 2 : 1;
      out += s.slice(i, j + 1);
      i = j + 1;
      continue;
    }
    if (c === '/' && d === '/') {
      let j = i;
      while (j < s.length && s[j] !== '\n') j++;
      out += ' '.repeat(j - i);
      i = j;
      continue;
    }
    if (c === '/' && d === '*') {
      const end = s.indexOf('*/', i + 2);
      const j = end < 0 ? s.length : end + 2;
      out += s.slice(i, j).replace(/[^\n]/g, ' ');
      i = j;
      continue;
    }
    out += c;
    i++;
  }
  return out;
}

/** 1-based line of an offset. @param {string} text @param {number} off */
export function lineOf(text, off) {
  let n = 1;
  for (let i = 0; i < off && i < text.length; i++) if (text.charCodeAt(i) === 10) n++;
  return n;
}

const PROGRAM = /\b(HLSLPROGRAM|CGPROGRAM|HLSLINCLUDE|CGINCLUDE)\b([\s\S]*?)\b(ENDHLSL|ENDCG)\b/g;

/**
 * Mask program blocks: returns the text with each block's content replaced by spaces (newlines kept) and the list of
 * blocks with their kind, offsets and code.
 * @param {string} text comment-stripped source
 */
export function maskPrograms(text) {
  const blocks = [];
  const masked = text.replace(PROGRAM, (m, kind, code, _end, off) => {
    blocks.push({ kind, start: off, end: off + m.length, codeStart: off + kind.length, code });
    return kind + code.replace(/[^\n]/g, ' ') + ' '.repeat(m.length - kind.length - code.length);
  });
  return { masked, blocks };
}

/** Index of the brace that closes the one at `open` (text must already be masked). */
function closeBrace(text, open) {
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    const c = text[i];
    if (c === '"') {
      let j = i + 1;
      while (j < text.length && text[j] !== '"' && text[j] !== '\n') j++;
      i = j;
      continue;
    }
    if (c === '{') depth++;
    else if (c === '}') {
      depth--;
      if (depth === 0) return i;
    }
  }
  return -1;
}

/**
 * Top-level `Keyword { … }` blocks and `Keyword "…"` statements directly inside [from, to).
 * @returns {{word: string, start: number, open: number, close: number, arg?: string}[]}
 */
function children(text, from, to) {
  const out = [];
  const re = /\b([A-Za-z]+)\b/g;
  re.lastIndex = from;
  let m;
  while ((m = re.exec(text)) && m.index < to) {
    const word = m[1];
    let k = m.index + word.length;
    while (k < to && /\s/.test(text[k])) k++;
    if (text[k] === '"') {
      const end = text.indexOf('"', k + 1);
      const arg = text.slice(k + 1, end < 0 ? k + 1 : end);
      // `Name "x"`, `UsePass "x"`, `Shader "x" {`, `GrabPass { "x" }`
      let q = end + 1;
      while (q < to && /\s/.test(text[q])) q++;
      if (text[q] === '{' && (word === 'Shader')) {
        const close = closeBrace(text, q);
        out.push({ word, start: m.index, open: q, close, arg });
        re.lastIndex = close < 0 ? to : close + 1;
      } else {
        out.push({ word, start: m.index, open: -1, close: -1, arg });
        re.lastIndex = end < 0 ? to : end + 1;
      }
      continue;
    }
    if (text[k] === '{') {
      const close = closeBrace(text, k);
      out.push({ word, start: m.index, open: k, close });
      re.lastIndex = close < 0 ? to : close + 1;
      continue;
    }
  }
  return out;
}

/** `"Key" = "Value"` pairs of a Tags block. @param {string} inner */
export function parseTags(inner) {
  const tags = {};
  for (const m of inner.matchAll(/"([^"]+)"\s*=\s*"([^"]*)"/g)) tags[m[1]] = m[2];
  return tags;
}

/**
 * Parse a .shader file.
 * @param {string} src raw file text
 * @returns {{text: string, name: string|null, includes: {code: string, start: number}[], subShaders: {
 *   start: number, line: number, tags: Record<string,string>, includes: {code: string, start: number}[],
 *   passes: {kind: 'Pass'|'UsePass'|'GrabPass', start: number, line: number, name: string|null, tags: Record<string,string>,
 *            programs: {kind: string, code: string, start: number}[], usePass?: string}[]}[]} | null}
 *   text: the comment-stripped source (offsets refer to it); null when no `Shader "…" { … }` is found
 */
export function parseShader(src) {
  const text = stripComments(src);
  const { masked, blocks } = maskPrograms(text);
  const top = children(masked, 0, masked.length).find((c) => c.word === 'Shader' && c.open >= 0);
  if (!top) return null;
  const inRange = (b, a, z) => b.start > a && b.start < z;
  const directBlocks = (a, z, nested) => blocks.filter((b) => inRange(b, a, z) && !nested.some(([x, y]) => inRange(b, x, y)));
  const shaderKids = children(masked, top.open + 1, top.close);
  const subRanges = shaderKids.filter((c) => c.word === 'SubShader' && c.open >= 0).map((c) => [c.open, c.close]);
  const shaderIncludes = directBlocks(top.open, top.close, subRanges)
    .filter((b) => /INCLUDE$/.test(b.kind))
    .map((b) => ({ code: b.code, start: b.codeStart }));
  const subShaders = [];
  for (const sc of shaderKids.filter((c) => c.word === 'SubShader' && c.open >= 0)) {
    const kids = children(masked, sc.open + 1, sc.close);
    const passRanges = kids.filter((c) => (c.word === 'Pass' || c.word === 'GrabPass') && c.open >= 0).map((c) => [c.open, c.close]);
    const tagBlock = kids.find((c) => c.word === 'Tags' && c.open >= 0);
    const sub = {
      start: sc.start,
      line: lineOf(text, sc.start),
      tags: tagBlock ? parseTags(masked.slice(tagBlock.open + 1, tagBlock.close)) : {},
      includes: directBlocks(sc.open, sc.close, passRanges).filter((b) => /INCLUDE$/.test(b.kind)).map((b) => ({ code: b.code, start: b.codeStart })),
      passes: [],
    };
    for (const k of kids) {
      if (k.word === 'Pass' && k.open >= 0) {
        const pk = children(masked, k.open + 1, k.close);
        const pt = pk.find((c) => c.word === 'Tags' && c.open >= 0);
        sub.passes.push({
          kind: 'Pass',
          start: k.start,
          line: lineOf(text, k.start),
          name: pk.find((c) => c.word === 'Name' && c.arg !== undefined)?.arg ?? null,
          tags: pt ? parseTags(masked.slice(pt.open + 1, pt.close)) : {},
          programs: blocks.filter((b) => inRange(b, k.open, k.close)).map((b) => ({ kind: b.kind, code: b.code, start: b.codeStart })),
        });
      } else if (k.word === 'UsePass' && k.arg !== undefined) {
        sub.passes.push({ kind: 'UsePass', start: k.start, line: lineOf(text, k.start), name: null, tags: {}, programs: [], usePass: k.arg });
      } else if (k.word === 'GrabPass' && k.open >= 0) {
        sub.passes.push({ kind: 'GrabPass', start: k.start, line: lineOf(text, k.start), name: null, tags: {}, programs: [] });
      }
    }
    subShaders.push(sub);
  }
  return { text, name: top.arg ?? null, includes: shaderIncludes, subShaders };
}

/** URP-facing pipeline tag values of a SubShader "RenderPipeline" tag. */
export const URP_TAGS = new Set(['UniversalPipeline', 'UniversalRenderPipeline']);

/**
 * The SubShader URP runs: the first one whose RenderPipeline tag is absent or names URP.
 * @param {{subShaders: {tags: Record<string,string>}[]}} shader
 */
export function urpSubShader(shader) {
  return shader.subShaders.find((s) => !s.tags.RenderPipeline || URP_TAGS.has(s.tags.RenderPipeline)) ?? null;
}

/** `#include "x"` targets in code, with offsets. @param {string} code */
export function includeTargets(code) {
  return [...code.matchAll(/^[ \t]*#[ \t]*include[ \t]+["<]([^">]+)[">]/gm)].map((m) => ({ path: m[1], index: m.index }));
}

const KW_PRAGMA = /^[ \t]*#[ \t]*pragma[ \t]+(shader_feature|multi_compile|dynamic_branch)(_local)?(?:_(vertex|fragment|hull|domain|geometry|raytracing))?[ \t]+([^\n]+)$/gm;

/**
 * Keyword declarations in code: `#pragma shader_feature[_local][_stage] A B`, `multi_compile…`, `dynamic_branch…`.
 * The `_` / `__` placeholders are dropped.
 * @param {string} code
 * @returns {{kind: 'shader_feature'|'multi_compile'|'dynamic_branch', local: boolean, keywords: string[], index: number}[]}
 */
export function keywordPragmas(code) {
  const out = [];
  for (const m of String(code).matchAll(KW_PRAGMA)) {
    const keywords = m[4].trim().split(/\s+/).filter((k) => /^[A-Za-z_][\w]*$/.test(k) && !/^_+$/.test(k));
    out.push({ kind: m[1], local: !!m[2], keywords, index: m.index });
  }
  return out;
}

/**
 * The declarations inside `CBUFFER_START(UnityPerMaterial) … CBUFFER_END` blocks of code, normalised to a sorted list
 * of `type name` strings (whitespace collapsed, array sizes kept).
 * @param {string} code
 * @returns {string[]|null} null when the code declares no UnityPerMaterial buffer
 */
export function perMaterialDecls(code) {
  const re = /CBUFFER_START\s*\(\s*UnityPerMaterial\s*\)([\s\S]*?)CBUFFER_END/g;
  let found = false;
  const decls = [];
  for (const m of String(code).matchAll(re)) {
    found = true;
    for (const raw of m[1].split(';')) {
      const d = raw.replace(/#[^\n]*/g, ' ').replace(/\s+/g, ' ').trim();
      if (d) decls.push(d);
    }
  }
  return found ? decls.sort() : null;
}

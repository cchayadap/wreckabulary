// sha256 helpers and the two tree hashes: treeHash (freshness, §6.2 step 3) and contentTreeHash (writes-nothing tests).
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { stableStringify } from './jsonio.mjs';
import { globToRegExp, toPosix } from './paths.mjs';

/** @param {string} s @returns {string} hex sha256 of the UTF-8 bytes */
export function sha256Text(s) {
  return crypto.createHash('sha256').update(String(s), 'utf8').digest('hex');
}

/** @param {Buffer|Uint8Array} b @returns {string} */
export function sha256Buffer(b) {
  return crypto.createHash('sha256').update(b).digest('hex');
}

/** sha256 of a file, read in 1 MiB chunks. Throws when missing. @param {string} file @returns {string} */
export function sha256File(file) {
  const h = crypto.createHash('sha256');
  const fd = fs.openSync(file, 'r');
  try {
    const buf = Buffer.allocUnsafe(1 << 20);
    for (;;) {
      const n = fs.readSync(fd, buf, 0, buf.length, null);
      if (n <= 0) break;
      h.update(buf.subarray(0, n));
    }
  } finally {
    fs.closeSync(fd);
  }
  return h.digest('hex');
}

/** sha256 of a value's sorted-key JSON. @param {any} value */
export function sha256Json(value) {
  return sha256Text(stableStringify(value));
}

const DEFAULT_SKIP = new Set(['.git', 'node_modules', '.venv', '__pycache__']);

/**
 * Sorted POSIX relative paths of the regular files under dir (symlinks are not followed).
 * @param {string} dir
 * @param {{skipDirs?: Iterable<string>, filter?: (rel: string) => boolean}} [opts] skipDirs defaults to .git, node_modules, .venv, __pycache__
 * @returns {string[]}
 */
export function listFiles(dir, opts = {}) {
  const skip = new Set(opts.skipDirs ?? DEFAULT_SKIP);
  const out = [];
  const walk = (abs, rel) => {
    let ents;
    try {
      ents = fs.readdirSync(abs, { withFileTypes: true });
    } catch (e) {
      if (e.code === 'ENOENT' || e.code === 'ENOTDIR') return;
      throw e;
    }
    for (const e of ents) {
      const r = rel ? `${rel}/${e.name}` : e.name;
      if (e.isDirectory()) {
        if (!skip.has(e.name)) walk(path.join(abs, e.name), r);
      } else if (e.isFile()) {
        if (!opts.filter || opts.filter(r)) out.push(r);
      }
    }
  };
  walk(dir, '');
  return out.sort();
}

/** Default freshness roots (gates.json freshness.tree_roots). */
export const TREE_ROOTS = Object.freeze(['Assets', 'Packages', 'ProjectSettings']);

/** Default freshness excludes (gates.json freshness.tree_excludes): agent and gate captures are not source. */
export const TREE_EXCLUDES = Object.freeze(['Assets/_AgentCaptures/**', 'Assets/_AgentCaptures.meta']);

/**
 * Freshness hash (the no-git option of gates.json freshness.mode "tree-hash"):
 * sha256 of the sorted lines `<root>/<rel>|<size>|<floor(mtimeMs)>` over the given roots (missing roots contribute nothing),
 * leaving out paths (U-relative POSIX) that match an exclude glob, so a capture written after a verdict does not stale it.
 * @param {string} base folder the roots are relative to (the Unity project, U)
 * @param {string[]} [roots] default TREE_ROOTS
 * @param {{excludes?: string[]}} [opts] default TREE_EXCLUDES; [] hashes everything
 */
export function treeHash(base, roots = TREE_ROOTS, opts = {}) {
  const excludes = (opts.excludes ?? TREE_EXCLUDES).map((g) => globToRegExp(g));
  const lines = [];
  for (const root of roots) {
    const abs = path.join(base, root);
    for (const rel of listFiles(abs, { skipDirs: [] })) {
      const full = `${toPosix(root)}/${rel}`;
      if (excludes.some((re) => re.test(full))) continue;
      const st = fs.statSync(path.join(abs, rel));
      lines.push(`${full}|${st.size}|${Math.floor(st.mtimeMs)}`);
    }
  }
  lines.sort();
  return sha256Text(lines.join('\n'));
}

/**
 * Content hash of a folder: sha256 of sorted `<rel>|<sha256>` lines. Used to prove a dry run wrote nothing.
 * @param {string} dir
 * @param {{skipDirs?: Iterable<string>, filter?: (rel: string) => boolean}} [opts]
 */
export function contentTreeHash(dir, opts = {}) {
  const lines = listFiles(dir, opts).map((rel) => `${rel}|${sha256File(path.join(dir, rel))}`);
  return sha256Text(lines.join('\n'));
}

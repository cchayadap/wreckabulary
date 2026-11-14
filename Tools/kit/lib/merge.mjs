// Merges the installer performs without overwriting user content: .claude/settings*.json, .mcp.json, managed text blocks,
// and the three-way file decision. Contract: CONTRACTS.md §3.4 (idempotency) and ARCHITECTURE §6.8.

/** Hook commands containing this marker are kit hooks; everything else is the user's and is kept. */
export const KIT_HOOK_MARKER = 'Tools/kit/hooks/';

/** Keys the kit must never write into project or local settings (writing it there turns the feature off). */
export const FORBIDDEN_SETTINGS_KEYS = Object.freeze(['autoContinueAtUsageLimit']);

const clone = (v) => (v === undefined ? undefined : JSON.parse(JSON.stringify(v)));
const isObj = (v) => v !== null && typeof v === 'object' && !Array.isArray(v);
const deepEqual = (a, b) => JSON.stringify(a) === JSON.stringify(b);

function findForbidden(obj, trail = '') {
  if (!isObj(obj) && !Array.isArray(obj)) return null;
  for (const [k, v] of Object.entries(obj)) {
    if (FORBIDDEN_SETTINGS_KEYS.includes(k)) return `${trail}/${k}`;
    const deeper = findForbidden(v, `${trail}/${k}`);
    if (deeper) return deeper;
  }
  return null;
}

const isKitHook = (h, marker) => isObj(h) && typeof h.command === 'string' && h.command.replace(/\\/g, '/').includes(marker);

/**
 * Remove every kit hook entry (command contains marker) from a hooks object, in place.
 * @returns {{removed: number, firstIndex: Record<string, number>}} firstIndex: per event, the group index where a kit hook first sat
 */
function stripKitHooks(hooks, marker) {
  let removed = 0;
  const firstIndex = {};
  for (const event of Object.keys(hooks)) {
    const groups = Array.isArray(hooks[event]) ? hooks[event] : [];
    const kept = [];
    groups.forEach((g) => {
      if (!isObj(g) || !Array.isArray(g.hooks)) { kept.push(g); return; }
      const before = g.hooks.length;
      const rest = g.hooks.filter((h) => !isKitHook(h, marker));
      if (rest.length !== before) {
        removed += before - rest.length;
        if (!(event in firstIndex)) firstIndex[event] = kept.length;
        if (rest.length) kept.push({ ...g, hooks: rest });
      } else {
        kept.push(g);
      }
    });
    if (kept.length) hooks[event] = kept;
    else delete hooks[event];
  }
  return { removed, firstIndex };
}

/**
 * Merge a kit settings fragment into existing settings.
 * - hooks: every kit hook already in the file is removed, then the fragment's groups are inserted where the first kit hook
 *   of that event sat (or appended); user hooks are kept in order.
 * - permissions.*: set union, existing order first; the newly added rules are returned so uninstall can remove only them.
 * - env: keys are added only when absent. Other top-level keys: added only when absent.
 * - `added`/`envAdded` are what THIS call added (a re-merge returns {} and reports 0 changes).
 * - `record` is what the manifest must keep: the union of `opts.previous` (the record stored by the last install or
 *   update, keeping only entries still present with the same value) and this call's additions. Store it in
 *   .kit/manifest.json settings[<file>] and pass it back as `previous` on the next merge, so uninstall after any number of
 *   updates still removes every kit-added rule and env key.
 * Throws when the fragment contains a forbidden key (autoContinueAtUsageLimit).
 * @param {object|null} existing
 * @param {object} fragment
 * @param {{marker?: string, previous?: {added?: Record<string, string[]>, envAdded?: Record<string, string>}}} [opts]
 * @returns {{settings: object, added: Record<string, string[]>, envAdded: Record<string, string>, record: {added: Record<string, string[]>, envAdded: Record<string, string>}, hooks: {removed: number, added: number}, warnings: string[]}}
 */
export function mergeSettings(existing, fragment, opts = {}) {
  const marker = opts.marker ?? KIT_HOOK_MARKER;
  const bad = findForbidden(fragment);
  if (bad) throw new Error(`mergeSettings: kit fragment contains forbidden key at ${bad}`);
  const settings = isObj(existing) ? clone(existing) : {};
  const warnings = [];
  const already = findForbidden(settings);
  if (already) warnings.push(`existing settings contain ${already}; the kit leaves it but doctor reports FAIL`);
  const added = {};
  const envAdded = {};
  let hooksAdded = 0;
  let hooksRemoved = 0;

  for (const [key, val] of Object.entries(fragment)) {
    if (key === 'permissions' && isObj(val)) {
      settings.permissions = isObj(settings.permissions) ? settings.permissions : {};
      for (const [list, rules] of Object.entries(val)) {
        if (Array.isArray(rules)) {
          const cur = Array.isArray(settings.permissions[list]) ? settings.permissions[list] : [];
          const fresh = rules.filter((r) => !cur.includes(r));
          const dedup = [...new Set(fresh)];
          settings.permissions[list] = cur.concat(dedup);
          if (dedup.length) added[list] = dedup;
        } else if (!(list in settings.permissions)) {
          settings.permissions[list] = clone(rules);
        }
      }
    } else if (key === 'env' && isObj(val)) {
      settings.env = isObj(settings.env) ? settings.env : {};
      for (const [k, v] of Object.entries(val)) {
        if (!(k in settings.env)) {
          settings.env[k] = v;
          envAdded[k] = v;
        }
      }
    } else if (key === 'hooks' && isObj(val)) {
      settings.hooks = isObj(settings.hooks) ? settings.hooks : {};
      const { removed, firstIndex } = stripKitHooks(settings.hooks, marker);
      hooksRemoved += removed;
      for (const [event, groups] of Object.entries(val)) {
        const cur = Array.isArray(settings.hooks[event]) ? settings.hooks[event] : [];
        const at = event in firstIndex ? Math.min(firstIndex[event], cur.length) : cur.length;
        const ins = clone(groups);
        for (const g of ins) hooksAdded += Array.isArray(g.hooks) ? g.hooks.length : 0;
        cur.splice(at, 0, ...ins);
        settings.hooks[event] = cur;
      }
    } else if (!(key in settings)) {
      settings[key] = clone(val);
    }
  }
  const record = { added: {}, envAdded: {} };
  const prev = isObj(opts.previous) ? opts.previous : {};
  const lists = new Set([...Object.keys(isObj(prev.added) ? prev.added : {}), ...Object.keys(added)]);
  for (const list of lists) {
    const cur = isObj(settings.permissions) && Array.isArray(settings.permissions[list]) ? settings.permissions[list] : [];
    const kept = (Array.isArray(prev.added?.[list]) ? prev.added[list] : []).filter((r) => cur.includes(r));
    const all = [...new Set([...kept, ...(added[list] || [])])];
    if (all.length) record.added[list] = all;
  }
  for (const [k, v] of Object.entries(isObj(prev.envAdded) ? prev.envAdded : {})) {
    if (isObj(settings.env) && deepEqual(settings.env[k], v)) record.envAdded[k] = v;
  }
  Object.assign(record.envAdded, envAdded);
  return { settings, added, envAdded, record, hooks: { removed: hooksRemoved, added: hooksAdded }, warnings };
}

/**
 * Undo a merge: remove all kit hooks, the permission rules the kit added, and env keys the kit added (only while their value is unchanged).
 * Empty arrays/objects left behind are removed.
 * @param {object} settings
 * @param {{added?: Record<string, string[]>, envAdded?: Record<string, string>}} record mergeSettings().record, as kept in .kit/manifest.json
 * @param {{marker?: string}} [opts]
 */
export function unmergeSettings(settings, record = {}, opts = {}) {
  const marker = opts.marker ?? KIT_HOOK_MARKER;
  const out = isObj(settings) ? clone(settings) : {};
  if (isObj(out.hooks)) {
    stripKitHooks(out.hooks, marker);
    if (!Object.keys(out.hooks).length) delete out.hooks;
  }
  if (isObj(out.permissions) && isObj(record.added)) {
    for (const [list, rules] of Object.entries(record.added)) {
      if (!Array.isArray(out.permissions[list])) continue;
      out.permissions[list] = out.permissions[list].filter((r) => !rules.includes(r));
      if (!out.permissions[list].length) delete out.permissions[list];
    }
    if (!Object.keys(out.permissions).length) delete out.permissions;
  }
  if (isObj(out.env) && isObj(record.envAdded)) {
    for (const [k, v] of Object.entries(record.envAdded)) if (deepEqual(out.env[k], v)) delete out.env[k];
    if (!Object.keys(out.env).length) delete out.env;
  }
  return out;
}

/**
 * Add the named servers to an .mcp.json object only when absent. A name present with different content is a conflict
 * (the installer then writes .mcp.json.kit-new). Never modifies an existing entry.
 * @param {object|null} existing
 * @param {Record<string, object>} servers the kit's full template set
 * @param {string[]} names the servers requested with --mcp
 * @returns {{mcp: object, added: string[], unchanged: string[], conflicts: string[], unknown: string[]}}
 */
export function mergeMcp(existing, servers, names) {
  const mcp = isObj(existing) ? clone(existing) : {};
  mcp.mcpServers = isObj(mcp.mcpServers) ? mcp.mcpServers : {};
  const res = { mcp, added: [], unchanged: [], conflicts: [], unknown: [] };
  for (const n of names) {
    if (!isObj(servers[n])) { res.unknown.push(n); continue; }
    if (!(n in mcp.mcpServers)) {
      mcp.mcpServers[n] = clone(servers[n]);
      res.added.push(n);
    } else if (deepEqual(mcp.mcpServers[n], servers[n])) {
      res.unchanged.push(n);
    } else {
      res.conflicts.push(n);
    }
  }
  return res;
}

/** Marker lines per block style. `hash` for .gitignore/.gitattributes, `html` for markdown (CLAUDE.md). */
export const BLOCK_STYLES = Object.freeze({
  hash: { start: '# >>> game-studio-kit', end: '# <<< game-studio-kit', startRe: /^# >>> game-studio-kit\s*$/, endRe: /^# <<< game-studio-kit\s*$/ },
  html: {
    start: '<!-- >>> game-studio-kit (managed; edit outside this block) -->',
    end: '<!-- <<< game-studio-kit -->',
    startRe: /^<!-- >>> game-studio-kit\b.*-->\s*$/,
    endRe: /^<!-- <<< game-studio-kit\s*-->\s*$/,
  },
});

function blockBounds(lines, style) {
  const s = lines.findIndex((l) => style.startRe.test(l));
  if (s < 0) return null;
  const e = lines.findIndex((l, i) => i > s && style.endRe.test(l));
  if (e < 0) throw new Error('managed block has a start marker but no end marker');
  return { s, e };
}

/**
 * The body of the managed block (without markers), or null when the text has none.
 * @param {string} text
 * @param {{style?: 'hash'|'html'}} [opts]
 */
export function extractManagedBlock(text, opts = {}) {
  const style = BLOCK_STYLES[opts.style ?? 'hash'];
  const lines = String(text).split(/\r?\n/);
  const b = blockBounds(lines, style);
  return b ? lines.slice(b.s + 1, b.e).join('\n') : null;
}

/**
 * Insert or replace the kit's managed block. Idempotent: applying the same body twice gives the same text.
 * Keeps the file's line ending (CRLF if present, else LF). A start marker without an end marker throws.
 * @param {string} text current file content ('' for a new file)
 * @param {string} body block content without markers
 * @param {{style?: 'hash'|'html'}} [opts]
 * @returns {string}
 */
export function applyManagedBlock(text, body, opts = {}) {
  const style = BLOCK_STYLES[opts.style ?? 'hash'];
  const src = String(text ?? '');
  const eol = src.includes('\r\n') ? '\r\n' : '\n';
  const bodyLines = String(body).replace(/\r\n/g, '\n').replace(/\n+$/, '').split('\n');
  const block = [style.start, ...(bodyLines.length === 1 && bodyLines[0] === '' ? [] : bodyLines), style.end];
  const lines = src.split(/\r?\n/);
  const hadFinalEol = src.endsWith('\n');
  if (hadFinalEol) lines.pop();
  const b = src ? blockBounds(lines, style) : null;
  let outLines;
  if (b) {
    outLines = [...lines.slice(0, b.s), ...block, ...lines.slice(b.e + 1)];
  } else if (!src) {
    outLines = block;
  } else {
    const sep = lines.length && lines[lines.length - 1].trim() === '' ? [] : [''];
    outLines = [...lines, ...sep, ...block];
  }
  return outLines.join(eol) + eol;
}

/**
 * Remove the managed block (and the blank separator line applyManagedBlock added before it). No block → text unchanged.
 * @param {string} text
 * @param {{style?: 'hash'|'html'}} [opts]
 */
export function removeManagedBlock(text, opts = {}) {
  const style = BLOCK_STYLES[opts.style ?? 'hash'];
  const src = String(text ?? '');
  const eol = src.includes('\r\n') ? '\r\n' : '\n';
  const lines = src.split(/\r?\n/);
  const hadFinalEol = src.endsWith('\n');
  if (hadFinalEol) lines.pop();
  const b = blockBounds(lines, style);
  if (!b) return src;
  let start = b.s;
  const atEnd = b.e === lines.length - 1;
  if (atEnd && start > 0 && lines[start - 1].trim() === '') start -= 1;
  const out = [...lines.slice(0, start), ...lines.slice(b.e + 1)];
  return out.length ? out.join(eol) + (hadFinalEol ? eol : '') : '';
}

/**
 * The installer's three-way decision for one template file (ARCHITECTURE §6.8).
 * @param {{exists: boolean, currentSha?: string|null, manifestEntry?: {sha_installed: string}|null, newSha: string}} s
 * @returns {'write'|'overwrite'|'unchanged'|'kit-new'|'user-file'}
 *  write: missing → write it; overwrite: untouched since install and the kit changed it; unchanged: nothing to do;
 *  kit-new: the user modified it → write <path>.kit-new; user-file: pre-existing file not in the manifest → never overwrite.
 */
export function planFileAction(s) {
  if (!s.exists) return 'write';
  if (!s.manifestEntry) return s.currentSha === s.newSha ? 'unchanged' : 'user-file';
  if (s.currentSha === s.manifestEntry.sha_installed) return s.currentSha === s.newSha ? 'unchanged' : 'overwrite';
  return s.currentSha === s.newSha ? 'unchanged' : 'kit-new';
}

/**
 * Replace `{{KEY}}` placeholders (KEY = [A-Z0-9_]+) that are in `map`; unknown ones are left and reported.
 * @param {string} text
 * @param {Record<string, string>} map
 * @returns {{text: string, missing: string[]}}
 */
export function fillPlaceholders(text, map) {
  const missing = new Set();
  const out = String(text).replace(/\{\{([A-Z0-9_]+)\}\}/g, (m, k) => {
    if (Object.prototype.hasOwnProperty.call(map, k)) return String(map[k]);
    missing.add(k);
    return m;
  });
  return { text: out, missing: [...missing] };
}

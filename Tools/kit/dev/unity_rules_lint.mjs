#!/usr/bin/env node
// unity_rules_lint.mjs: static checks for the unity-dev-practice house rules (CONTRACTS.md §4.5; ARCHITECTURE §3.5).
// Installed as <W>/Tools/kit/dev/unity_rules_lint.mjs. Reads C# and asmdefs under U/Assets and embedded packages; writes
// nothing. A rule the runtime does not enforce is not a rule, so each house rule here has a check:
//   core-no-unityengine     engine-free Core asmdefs (noEngineReferences, or a *.Core name) must not use UnityEngine/UnityEditor
//   static-without-reset    mutable statics in runtime code need a [RuntimeInitializeOnLoadMethod(SubsystemRegistration)]
//                           reset in the same type (domain reload off); Core types use a ResetStatics() method instead
//   random-usage            UnityEngine.Random / System.Random in gameplay assemblies (use seeded Pcg32 streams)
//   shader-find-keepalive   Shader.Find("X") / Resources.Load("p") literals whose target is not kept in the build
//                           (WoO D056: a stripped shader crashed the shipped front end)
//   debug-log-prefix        Debug.Log* string literals must start with the gate prefix (gate.config.json "prefix")
//   find-in-update          GameObject.Find / FindObjectOfType & co. inside Update/LateUpdate/FixedUpdate/OnGUI
//   string-gethashcode-id   GetHashCode() used for ids, keys, seeds or saves (not stable across runtimes; use Hash64)
// Suppress one finding with a reason, on the same line or the line above:  // gsk-lint-ignore <rule>[,<rule>]: <reason>
// Optional project config (never required): <W>/Docs/dev/unity_lint.json
//   {"schema":1, "exclude":["Assets/Vendor/**"], "core_asmdefs":["Game.Core"], "gameplay_exempt":["Assets/Game/Debug/**"],
//    "keepalive":{"shaders":["Hidden/MyBlit"], "resources":["Prefabs/Boot"]}}
import fs from 'node:fs';
import path from 'node:path';
import { EXIT, CliError, UsageError, emit, isMain, note, parseCli, runCli } from '../lib/cli.mjs';
import { findWorkspace, kitPaths, matchesAny, relPosix, toPosix } from '../lib/paths.mjs';
import { listFiles } from '../lib/hash.mjs';
import { readText } from '../lib/jsonio.mjs';
import { identifiers, lineOf, lineStarts, methodName, scanMembers, splitAttributes, stripCs, stripGenerics, assignIndex } from './lib/cs_source.mjs';

export const RULES = Object.freeze([
  'core-no-unityengine',
  'static-without-reset',
  'random-usage',
  'shader-find-keepalive',
  'debug-log-prefix',
  'find-in-update',
  'string-gethashcode-id',
]);

export const DEFAULT_EXCLUDES = Object.freeze([
  'Assets/Plugins/**',
  'Assets/ThirdParty/**',
  'Assets/Third Party/**',
  'Assets/AssetStoreTools/**',
  'Assets/TextMesh Pro/**',
  'Assets/Samples/**',
  'Assets/_AgentCaptures/**',
]);

const KIT_PACKAGE = 'com.gamestudiokit.core';
const SKIP_DIRS = ['.git', 'node_modules', 'Library', 'Temp', 'Obj', 'obj', 'bin', 'Logs', 'Builds'];
// Mutable collection types, matched against the whole declaration (initialiser included, so a field declared as an
// interface and initialised with a concrete collection counts). IReadOnly* types cannot be mutated through the field.
const GROWABLE = /\b(List|Dictionary|HashSet|Queue|Stack|LinkedList|SortedDictionary|SortedList|SortedSet|ConcurrentDictionary|ConcurrentQueue|ConcurrentBag|ConcurrentStack|ObservableCollection|StringBuilder|NativeList|NativeHashMap|IList|ICollection|IDictionary|ISet)\b/;
const ARRAY_TYPE = /\[\s*,*\s*\]/;
const BUILTIN_GUID = '0000000000000000f000000000000000';
/**
 * Built-in shaders (no .shader file in the project) → their fileID in Unity's built-in resources, as materials and
 * GraphicsSettings.asset reference them ({fileID: N, guid: 0000000000000000f000000000000000}) [C]. A listed name counts as
 * kept when its fileID is in Always Included Shaders; other built-in names can be declared in keepalive.shaders.
 */
export const BUILTIN_SHADER_FILEIDS = Object.freeze({
  'Legacy Shaders/Diffuse': 7,
  'Standard (Specular setup)': 45,
  Standard: 46,
  'Unlit/Transparent': 10750,
  'Unlit/Transparent Cutout': 10751,
  'Unlit/Texture': 10752,
  'Sprites/Default': 10753,
  'Unlit/Color': 10755,
  'UI/Default': 10770,
  'Hidden/CubeBlur': 15104,
  'Hidden/CubeCopy': 15105,
  'Hidden/CubeBlend': 15106,
});
const UNITY_RANDOM_MEMBERS = /\bRandom\s*\.\s*(Range|value|insideUnitSphere|insideUnitCircle|onUnitSphere|rotation|rotationUniform|ColorHSV|InitState|state|seed)\b/g;
const FIND_CALLS = /\b(?:GameObject\s*\.\s*(?:Find|FindWithTag|FindGameObjectWithTag|FindGameObjectsWithTag)|(?:Object\s*\.\s*)?(?:FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType))\s*(?:<[^<>()]*>)?\s*\(/g;
const FRAME_METHODS = new Set(['Update', 'LateUpdate', 'FixedUpdate', 'OnGUI']);
const ID_WORDS = /(?:^|_)(?:id|key|seed|hash|guid|uid|save|persist|slot)|(?:Id|ID|Key|Seed|Hash|Guid|Uid|Save|Slot)(?:s|[A-Z_]|$)/;
const PERSIST_CONTEXT = /\b(Pcg32|Hash64|PlayerPrefs|Save\w*|Persist\w*|Serialize\w*|Seed\w*|WithSeed|InitState)\b/;
const LOG_CALL = /\bDebug\s*\.\s*(Log|LogWarning|LogError|LogFormat|LogWarningFormat|LogErrorFormat|LogAssertion)\s*\(\s*/g;

/** Parse an asmdef/asmref (JSON with an optional BOM); null on bad JSON. @param {string} file */
function readJsonLoose(file) {
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
  } catch {
    return null;
  }
}

/** Read the optional lint config. @param {string|null} file */
function readConfig(file) {
  if (!file || !fs.existsSync(file)) return {};
  const cfg = readJsonLoose(file);
  if (!cfg || typeof cfg !== 'object') throw new UsageError(`lint config is not valid JSON: ${file}`);
  return cfg;
}

/** Gate prefix: --prefix, then U/Tools/kit/gate/gate.config.json prefix, then kit.json gate_prefix. */
function resolvePrefix(opt, U, W) {
  if (opt !== undefined) return opt;
  const gc = readJsonLoose(path.join(U, 'Tools', 'kit', 'gate', 'gate.config.json'));
  if (gc && typeof gc.prefix === 'string' && gc.prefix) return gc.prefix;
  if (W) {
    const kj = readJsonLoose(path.join(W, 'Tools', 'kit', 'kit.json'));
    if (kj && typeof kj.gate_prefix === 'string' && kj.gate_prefix) return kj.gate_prefix;
  }
  return null;
}

/**
 * Collect source roots (U-relative folders): Assets, and every embedded package folder under Packages/ (a folder with a
 * package.json). The kit's own package is skipped unless includeKit.
 */
function sourceRoots(U, includeKit) {
  const roots = [];
  if (fs.existsSync(path.join(U, 'Assets'))) roots.push('Assets');
  let ents = [];
  try { ents = fs.readdirSync(path.join(U, 'Packages'), { withFileTypes: true }); } catch { /* no Packages */ }
  for (const e of ents) {
    if (!e.isDirectory()) continue;
    if (!fs.existsSync(path.join(U, 'Packages', e.name, 'package.json'))) continue;
    if (e.name === KIT_PACKAGE && !includeKit) continue;
    roots.push(`Packages/${e.name}`);
  }
  return roots;
}

/**
 * Asmdef model: folder (U-relative) → assembly info. asmref folders map to the asmdef they reference.
 * @returns {{byDir: Map<string, object>, asmdefs: object[]}}
 */
function loadAssemblies(U, files) {
  const asmdefs = [];
  const byName = new Map();
  const byGuid = new Map();
  for (const rel of files.filter((f) => /\.asmdef$/i.test(f))) {
    const j = readJsonLoose(path.join(U, rel));
    if (!j) continue;
    const meta = fs.existsSync(path.join(U, rel + '.meta')) ? fs.readFileSync(path.join(U, rel + '.meta'), 'utf8') : '';
    const guid = /^guid:\s*([0-9a-f]{32})/m.exec(meta)?.[1] ?? null;
    const refs = Array.isArray(j.references) ? j.references.map(String) : [];
    const defs = Array.isArray(j.defineConstraints) ? j.defineConstraints.map(String) : [];
    const pre = Array.isArray(j.precompiledReferences) ? j.precompiledReferences.map(String) : [];
    const opt = Array.isArray(j.optionalUnityReferences) ? j.optionalUnityReferences.map(String) : [];
    const inc = Array.isArray(j.includePlatforms) ? j.includePlatforms.map(String) : [];
    const info = {
      file: rel,
      dir: path.posix.dirname(rel),
      name: String(j.name ?? path.posix.basename(rel, '.asmdef')),
      guid,
      references: refs,
      autoReferenced: j.autoReferenced !== false,
      noEngineReferences: j.noEngineReferences === true,
      editorOnly: inc.length === 1 && inc[0] === 'Editor',
      test:
        defs.includes('UNITY_INCLUDE_TESTS') ||
        opt.includes('TestAssemblies') ||
        refs.some((r) => /TestRunner|^nunit/i.test(r)) ||
        pre.some((p) => /^nunit\.framework\.dll$/i.test(p)),
    };
    asmdefs.push(info);
    byName.set(info.name, info);
    if (guid) byGuid.set(guid, info);
  }
  const byDir = new Map();
  for (const a of asmdefs) byDir.set(a.dir, a);
  for (const rel of files.filter((f) => /\.asmref$/i.test(f))) {
    const j = readJsonLoose(path.join(U, rel));
    const ref = String(j?.reference ?? '');
    const target = ref.startsWith('GUID:') ? byGuid.get(ref.slice(5)) : byName.get(ref);
    if (target) byDir.set(path.posix.dirname(rel), target);
  }
  return { byDir, asmdefs };
}

/** The assembly a .cs file compiles into (nearest folder with an asmdef/asmref, else the predefined assemblies). */
function assemblyFor(rel, byDir) {
  let dir = path.posix.dirname(rel);
  for (;;) {
    if (byDir.has(dir)) return byDir.get(dir);
    if (dir === '.' || !dir.includes('/')) break;
    dir = path.posix.dirname(dir);
  }
  const segs = rel.split('/');
  const editor = segs.slice(0, -1).some((s) => s === 'Editor');
  const firstpass = /^Assets\/(Plugins|Standard Assets|Pro Standard Assets)\//.test(rel);
  const name = `Assembly-CSharp${firstpass ? '-firstpass' : ''}${editor ? '-Editor' : ''}`;
  return { name, dir: 'Assets', file: null, references: [], autoReferenced: true, noEngineReferences: false, editorOnly: editor, test: false, predefined: true };
}

/**
 * Can code in assembly `from` use a public type of assembly `to` without qualification problems? Same assembly, a
 * direct asmdef reference (by name or GUID), or a predefined Assembly-CSharp* that auto-references it. Editor-only
 * assemblies are visible only to editor code.
 */
export function canReference(from, to) {
  if (from.name === to.name) return true;
  if (to.editorOnly && !from.editorOnly) return false;
  if ((from.references ?? []).some((r) => r === to.name || (to.guid && r === `GUID:${to.guid}`))) return true;
  if (from.predefined) {
    if (!to.predefined) return to.autoReferenced !== false;
    return !/-firstpass/.test(from.name) || /-firstpass/.test(to.name);
  }
  return false;
}

/**
 * Namespace blocks and file-scoped namespaces of a file: [{name, start, end}] (offsets in `bare`). Nested blocks give
 * their own segment; nsAt() joins them.
 */
export function namespaceRanges(bare) {
  const out = [];
  for (const m of bare.matchAll(/\bnamespace\s+([A-Za-z_][\w.]*)\s*([{;])/g)) {
    if (m[2] === ';') { out.push({ name: m[1], start: m.index, end: bare.length }); continue; }
    let depth = 0;
    let end = bare.length;
    for (let i = m.index + m[0].length - 1; i < bare.length; i++) {
      if (bare[i] === '{') depth++;
      else if (bare[i] === '}' && --depth === 0) { end = i; break; }
    }
    out.push({ name: m[1], start: m.index, end });
  }
  return out;
}

/** Full namespace at an offset ('' for the global namespace). */
export function nsAt(ranges, off) {
  return ranges.filter((r) => off > r.start && off <= r.end).map((r) => r.name).join('.');
}

/** Suppressions: line number → Set of rule ids (reason required). @param {string} src */
function suppressions(src) {
  const out = new Map();
  const lines = src.split(/\r?\n/);
  lines.forEach((l, idx) => {
    const m = /\/\/\s*gsk-lint-ignore\s+([a-z0-9,\s-]+?)\s*:\s*(\S.*)$/.exec(l);
    if (!m) return;
    const ids = m[1].split(',').map((s) => s.trim()).filter(Boolean);
    const onlyComment = /^\s*\/\//.test(l);
    const target = onlyComment ? idx + 2 : idx + 1; // a comment-only line covers the next line
    for (const t of [idx + 1, target]) {
      if (!out.has(t)) out.set(t, new Set());
      ids.forEach((id) => out.get(t).add(id));
    }
  });
  return out;
}

/** Shader keep-alive model built lazily from project files. */
class KeepAlive {
  constructor(U, cfg) {
    this.U = U;
    this.cfg = cfg;
    this.declaredShaders = new Set((cfg.keepalive?.shaders ?? []).map(String));
    this.declaredResources = new Set((cfg.keepalive?.resources ?? []).map((s) => String(s).toLowerCase()));
    this.nameToGuids = null;
    this.included = null;
    this.resourceIndex = null;
    this.scannedMain = false;
    this.scannedCache = false;
  }

  metaGuid(abs) {
    try {
      return /^guid:\s*([0-9a-f]{32})/m.exec(fs.readFileSync(abs + '.meta', 'utf8'))?.[1] ?? null;
    } catch {
      return null;
    }
  }

  /** Shader files → names (Assets, embedded packages, and Library/PackageCache when present). */
  indexShaders(includeCache) {
    const U = this.U;
    const roots = [];
    if (!this.scannedMain) roots.push('Assets', 'Packages');
    if (includeCache && !this.scannedCache) roots.push('Library/PackageCache');
    this.scannedMain = true;
    if (includeCache) this.scannedCache = true;
    this.nameToGuids = this.nameToGuids ?? new Map();
    this.resourceShaderGuids = this.resourceShaderGuids ?? new Set();
    for (const root of roots) {
      const abs = path.join(U, root);
      if (!fs.existsSync(abs)) continue;
      const files = listFiles(abs, { skipDirs: ['.git', 'node_modules'], filter: (r) => /\.(shader|shadergraph)$/i.test(r) });
      for (const rel of files) {
        const f = path.join(abs, rel);
        let name = null;
        try {
          if (/\.shader$/i.test(rel)) {
            const head = fs.readFileSync(f, 'utf8').slice(0, 4096);
            name = /^\s*Shader\s+"([^"]+)"/m.exec(stripCs(head).code)?.[1] ?? null;
          } else {
            const txt = fs.readFileSync(f, 'utf8');
            const p = /"m_Path"\s*:\s*"([^"]*)"/.exec(txt)?.[1] ?? 'Shader Graphs';
            name = `${p ? p + '/' : ''}${path.basename(rel).replace(/\.shadergraph$/i, '')}`;
          }
        } catch { /* unreadable shader: skip */ }
        if (!name) continue;
        const guid = this.metaGuid(f);
        if (!this.nameToGuids.has(name)) this.nameToGuids.set(name, new Set());
        if (guid) this.nameToGuids.get(name).add(guid);
        if (guid && /(^|\/)Resources\//.test(`${root}/${rel}`)) this.resourceShaderGuids.add(guid);
      }
    }
  }

  /** GUIDs of shaders that ship: GraphicsSettings always-included, preloaded variant collections, Resources materials. */
  indexIncluded() {
    const U = this.U;
    const inc = new Set();
    const gs = path.join(U, 'ProjectSettings', 'GraphicsSettings.asset');
    const refGuids = (text) => [...text.matchAll(/guid:\s*([0-9a-f]{32})/g)].map((m) => m[1]);
    const section = (text, key) => {
      const m = new RegExp(`^\\s*${key}:\\s*\\n((?:\\s*-\\s*\\{[^}]*\\}\\s*\\n?)*)`, 'm').exec(text);
      return m ? m[1] : '';
    };
    let preloaded = [];
    this.includedBuiltin = new Set();
    if (fs.existsSync(gs)) {
      const t = fs.readFileSync(gs, 'utf8');
      const always = section(t, 'm_AlwaysIncludedShaders');
      refGuids(always).forEach((g) => inc.add(g));
      for (const m of always.matchAll(/fileID:\s*(-?\d+)\s*,\s*guid:\s*([0-9a-f]{32})/g)) if (m[2] === BUILTIN_GUID) this.includedBuiltin.add(Number(m[1]));
      preloaded = refGuids(section(t, 'm_PreloadedShaders'));
    }
    const assets = path.join(U, 'Assets');
    if (fs.existsSync(assets)) {
      const files = listFiles(assets, { filter: (r) => /\.(mat|shadervariants)$/i.test(r) });
      for (const rel of files) {
        const abs = path.join(assets, rel);
        if (/\.shadervariants$/i.test(rel)) {
          if (!preloaded.includes(this.metaGuid(abs))) continue;
          const t = fs.readFileSync(abs, 'utf8');
          for (const m of t.matchAll(/-\s*first:\s*\{[^}]*guid:\s*([0-9a-f]{32})/g)) inc.add(m[1]);
        } else if (/(^|\/)Resources\//.test(rel)) {
          const t = fs.readFileSync(abs, 'utf8');
          const g = /m_Shader:\s*\{[^}]*guid:\s*([0-9a-f]{32})/.exec(t)?.[1];
          if (g) inc.add(g);
        }
      }
    }
    this.included = inc;
  }

  /** @returns {{ok: boolean, why: string}} */
  shader(name) {
    if (this.declaredShaders.has(name)) return { ok: true, why: 'declared' };
    if (!this.nameToGuids) this.indexShaders(false);
    if (!this.included) this.indexIncluded();
    let guids = this.nameToGuids.get(name);
    if (!guids && !this.scannedCache) {
      this.indexShaders(true);
      guids = this.nameToGuids.get(name);
    }
    if (!guids || guids.size === 0) {
      // built-in shaders have no .shader file: they are kept by fileID under the built-in guid
      const fid = BUILTIN_SHADER_FILEIDS[name];
      if (fid !== undefined) {
        if (this.includedBuiltin.has(fid)) return { ok: true, why: 'built-in, always included' };
        return { ok: false, why: `built-in shader (fileID ${fid}) that is not in Always Included Shaders: add it there (Project Settings > Graphics), or list it in keepalive.shaders (Docs/dev/unity_lint.json) if a build keeps it another way` };
      }
      return { ok: false, why: 'no shader of that name in the project or its packages: if it is a built-in shader, make sure it is in Always Included Shaders and list it in keepalive.shaders (Docs/dev/unity_lint.json); otherwise the name is wrong' };
    }
    for (const g of guids) if (this.included.has(g) || this.resourceShaderGuids.has(g)) return { ok: true, why: 'included' };
    return { ok: false, why: 'exists but nothing keeps it in a build (not in Always Included Shaders, a preloaded variant collection, a Resources material or the keep-alive list): add it to Always Included Shaders or keepalive.shaders' };
  }

  /** Resources.Load path → exists under some Resources folder (case-insensitive, any extension). */
  resource(p, all) {
    const key = p.toLowerCase().replace(/^\/+|\/+$/g, '');
    if (this.declaredResources.has(key)) return true;
    if (!this.resourceIndex) {
      this.resourceIndex = new Set();
      const dirs = new Set();
      for (const root of ['Assets', 'Packages']) {
        const abs = path.join(this.U, root);
        if (!fs.existsSync(abs)) continue;
        for (const rel of listFiles(abs, { skipDirs: ['.git', 'node_modules'], filter: (r) => !/\.meta$/i.test(r) && /(^|\/)Resources\//.test(r) })) {
          const after = rel.slice(rel.search(/(^|\/)Resources\//)).replace(/^\/?Resources\//, '');
          this.resourceIndex.add(after.replace(/\.[^./]+$/, '').toLowerCase());
          let d = path.posix.dirname(after);
          while (d && d !== '.') { dirs.add(d.toLowerCase()); d = path.posix.dirname(d); }
        }
      }
      this.resourceDirs = dirs;
    }
    if (all) return key === '' || this.resourceDirs.has(key) || this.resourceIndex.has(key);
    return this.resourceIndex.has(key);
  }
}

/** Split at commas outside (), [] and {} (generics must already be blanked). @param {string} h */
function topLevelParts(h) {
  const parts = [];
  let depth = 0;
  let start = 0;
  for (let i = 0; i < h.length; i++) {
    const c = h[i];
    if (c === '(' || c === '[' || c === '{') depth++;
    else if (c === ')' || c === ']' || c === '}') depth = Math.max(0, depth - 1);
    else if (c === ',' && depth === 0) { parts.push({ text: h.slice(start, i), at: start }); start = i + 1; }
  }
  parts.push({ text: h.slice(start), at: start });
  return parts;
}

/** Mutable static declarations in the `field` and `body` members of a scan. */
function mutableStatics(members) {
  const out = [];
  for (const m of members) {
    const { rest } = splitAttributes(m.header);
    const restAt = m.headerStart + (m.header.length - rest.length);
    const h = stripGenerics(rest);
    const eq = assignIndex(h);
    const declPart = eq >= 0 ? h.slice(0, eq) : h;
    if (!/\bstatic\b/.test(declPart) || /\bconst\b/.test(declPart)) continue;
    if (/\b(class|struct|interface|record|enum|delegate|using|operator|implicit|explicit)\b/.test(declPart)) continue;
    const isEvent = /\bevent\b/.test(declPart);
    if (m.kind === 'field') {
      if (/\(/.test(declPart)) continue; // method, constructor or expression-bodied member
      if (eq >= 0 && h.slice(eq, eq + 2) === '=>') continue; // expression-bodied property: no state
      const readonly = /\breadonly\b/.test(declPart);
      // a readonly field holds state only through a mutable object: a collection (declared or initialised) or an array
      if (readonly && !GROWABLE.test(rest) && !ARRAY_TYPE.test(declPart)) continue;
      // declarators: `static int a = 1, b, c = 3;` (the first carries the type)
      topLevelParts(h).forEach((part, idx) => {
        const e = assignIndex(part.text);
        const left = e >= 0 ? part.text.slice(0, e) : part.text;
        const name = (idx === 0 ? /([A-Za-z_]\w*)\s*$/ : /^\s*([A-Za-z_]\w*)/).exec(left)?.[1];
        if (!name || /^(static|readonly|volatile|public|private|internal|protected|new|unsafe|event)$/.test(name)) return;
        const off = restAt + part.at + left.lastIndexOf(name);
        out.push({ name, offset: off, typePath: m.typePath, kind: isEvent ? 'event' : readonly ? 'readonly collection' : 'field' });
      });
    } else if (m.kind === 'body') {
      if (/\(/.test(declPart)) continue; // method or constructor body
      const name = /([A-Za-z_]\w*)\s*$/.exec(declPart)?.[1];
      if (!name) continue;
      const offset = restAt + declPart.lastIndexOf(name);
      if (isEvent) { out.push({ name, offset, typePath: m.typePath, kind: 'event' }); continue; }
      // property: state lives only in an auto-property (`{ get; set; }`, `{ get; private set; }`, `init;`); accessors
      // with bodies keep their state in a backing field, which is checked on its own
      out.push({ property: true, name, offset, typePath: m.typePath, bodyStart: m.bodyStart, bodyEnd: m.bodyEnd });
    }
  }
  return out;
}

/**
 * Methods of a scan, block-bodied and expression-bodied (`static void Reset() => s = 0;`), as
 * {name, header, bodyStart, bodyEnd, typePath} where bare.slice(bodyStart + 1, bodyEnd) is the body text.
 */
export function methodsOf(scan) {
  const out = [];
  for (const m of scan.members) {
    const name = methodName(m.header);
    if (!name) continue;
    if (m.kind === 'body') {
      out.push({ name, header: m.header, bodyStart: m.bodyStart, bodyEnd: m.bodyEnd, typePath: m.typePath });
      continue;
    }
    const h = stripGenerics(m.header);
    const e = assignIndex(h);
    if (e >= 0 && h[e + 1] === '>') out.push({ name, header: m.header, bodyStart: m.headerStart + e + 1, bodyEnd: m.headerStart + m.header.length, typePath: m.typePath });
  }
  return out;
}

/**
 * True when the code mutates `name` (a collection or an array): Add/Remove/Clear/… calls, an element (or element
 * member) assignment, ++/-- on an element, or Array.Clear/Fill/Resize/Reverse/Sort/Copy/CopyTo writing into it.
 */
const MUTATORS = 'Add|AddRange|Remove|RemoveAt|RemoveAll|RemoveWhere|Clear|Insert|InsertRange|Enqueue|Dequeue|Push|Pop|TryAdd|TryRemove|TryDequeue|TryPop|TryUpdate|AddOrUpdate|GetOrAdd|UnionWith|ExceptWith|IntersectWith|SymmetricExceptWith|Sort|Reverse|Append|AppendLine|Fill';
export function mutated(bare, name) {
  // name is a C# identifier ([A-Za-z_]\w*), so it needs no escaping
  const res = [
    String.raw`\b${name}\s*\.\s*(?:${MUTATORS})\b`,
    String.raw`\b${name}\s*\[[^\]]*\](?:\s*\.\s*\w+)*\s*(?:[-+*/%&|^]|\?\?|<<|>>)?=(?!=)`,
    String.raw`\b${name}\s*\[[^\]]*\]\s*(?:\+\+|--)`,
    String.raw`(?:\+\+|--)\s*${name}\s*\[`,
    String.raw`\bArray\s*\.\s*(?:Clear|Fill|Resize|Reverse|Sort)\s*(?:<[^<>()]*>)?\s*\(\s*(?:ref\s+)?${name}\b`,
    String.raw`\bArray\s*\.\s*(?:Copy|ConstrainedCopy)\s*\([^;]*?,\s*${name}\b`,
    String.raw`\.\s*CopyTo\s*\(\s*${name}\b`,
  ];
  return res.some((r) => new RegExp(r).test(bare));
}

/**
 * Types and methods of one file, keyed by `<namespace>|<type path>` so partial declarations of one type in several
 * files meet under one key. Method bodies are carried as text (blanked code).
 * @returns {{types: string[], methods: {key: string, name: string, header: string, body: string}[]}}
 */
export function typeRecords(bare, scan = scanMembers(bare), ranges = namespaceRanges(bare)) {
  const types = scan.types.map((t) => `${nsAt(ranges, t.start)}|${t.path}`);
  const methods = methodsOf(scan).map((m) => ({ key: `${nsAt(ranges, m.bodyStart)}|${m.typePath}`, name: m.name, header: m.header, body: bare.slice(m.bodyStart + 1, m.bodyEnd) }));
  return { types, methods };
}

/** Add one file's records to a type index (Map key → {methods[], texts[]}); keys are prefixed by the assembly. */
export function indexTypes(index, asmName, bare, records) {
  const entry = (key) => {
    const k = `${asmName}\n${key}`;
    if (!index.has(k)) index.set(k, { methods: [], texts: [] });
    return index.get(k);
  };
  for (const key of new Set(records.types)) entry(key).texts.push(bare);
  for (const m of records.methods) entry(m.key).methods.push(m);
  return index;
}

/**
 * Lint one file. Pure: everything it needs is passed in.
 * @param {{rel: string, src: string, asm: object, isCore: boolean, gameplay: boolean, rules: Set<string>, prefix: string|null,
 *   keep: KeepAlive, randomTypeInScope?: boolean, projectHasRandomType?: boolean, typeIndex?: Map<string, object>}} ctx
 *   typeIndex: every file's types of the project (indexTypes), so a reset in another part of a partial type counts;
 *   randomTypeInScope: a project type named Random is visible in this file unqualified.
 * @returns {{violations: {rule: string, line: number, message: string}[], suppressed: number}}
 */
export function lintSource(ctx) {
  const { src, asm, isCore, gameplay, rules } = ctx;
  const { code, bare } = stripCs(src);
  const starts = lineStarts(bare);
  const at = (off) => lineOf(starts, off);
  const found = [];
  const add = (rule, off, message) => found.push({ rule, line: at(off), message });
  const scan = scanMembers(bare);
  const ranges = namespaceRanges(bare);
  const usings = [...bare.matchAll(/^\s*using\s+(static\s+)?([A-Za-z_][\w.]*)\s*(?:=\s*([A-Za-z_][\w.]*))?\s*;/gm)];

  if (rules.has('core-no-unityengine') && isCore) {
    for (const m of bare.matchAll(/\b(?:global::)?(UnityEngine|UnityEditor)\b/g)) {
      add('core-no-unityengine', m.index, `${m[1]} used in engine-free assembly ${asm.name}; Core stays pure .NET (architecture.md)`);
    }
  }

  if (rules.has('static-without-reset') && !asm.editorOnly && !asm.test) {
    const statics = mutableStatics(scan.members);
    const index = ctx.typeIndex ?? indexTypes(new Map(), asm.name, bare, typeRecords(bare, scan, ranges));
    const typeOf = (key) => index.get(`${asm.name}\n${key}`) ?? { methods: [], texts: [bare] };
    const resetIdsFor = (key) => {
      const ms = typeOf(key).methods;
      const named = new Map();
      for (const mem of ms) named.set(mem.name, (named.get(mem.name) ?? []).concat([mem]));
      const isReset = (mem) => {
        const { attrs } = splitAttributes(mem.header);
        const a = attrs.join(' ');
        if (/RuntimeInitializeOnLoadMethod\s*\(\s*(?:(?:UnityEngine\s*\.\s*)?RuntimeInitializeLoadType\s*\.\s*)?SubsystemRegistration\b/.test(a)) return true;
        if (/\bInitializeOnEnterPlayMode\b/.test(a)) return true;
        return isCore && /^(ResetStatics|ResetStaticState)$/.test(mem.name);
      };
      const queue = ms.filter(isReset);
      const seen = new Set(queue);
      const ids = new Set();
      while (queue.length) {
        const mem = queue.shift();
        identifiers(mem.body).forEach((x) => ids.add(x));
        for (const call of mem.body.matchAll(/([A-Za-z_]\w*)\s*\(/g)) {
          for (const callee of named.get(call[1]) ?? []) if (!seen.has(callee)) { seen.add(callee); queue.push(callee); }
        }
      }
      return ids;
    };
    const cache = new Map();
    for (const st of statics) {
      if (st.property) {
        const acc = bare.slice(st.bodyStart + 1, st.bodyEnd);
        if (!/\b(set|init)\s*;/.test(acc)) continue;
      }
      const key = `${nsAt(ranges, st.offset)}|${st.typePath}`;
      // a readonly lookup table (collection or array) that its type never mutates holds no session state
      if (st.kind === 'readonly collection' && !typeOf(key).texts.concat(bare).some((t) => mutated(t, st.name))) continue;
      if (!cache.has(key)) cache.set(key, resetIdsFor(key));
      if (cache.get(key).has(st.name)) continue;
      const how = isCore
        ? 'a static ResetStatics() in the same type that resets it (called from the Unity side)'
        : 'a [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] method in the same type that resets it';
      add('static-without-reset', st.offset, `mutable static ${st.property ? 'property' : st.kind} ${st.typePath}.${st.name} survives play sessions with domain reload off; add ${how} (domain-reload-off.md)`);
    }
  }

  if (rules.has('random-usage') && gameplay) {
    const aliases = new Map();
    let hasUnityEngine = false;
    let hasSystem = false;
    for (const u of usings) {
      if (u[3]) aliases.set(u[2], u[3]);
      else if (!u[1] && u[2] === 'UnityEngine') hasUnityEngine = true;
      else if (!u[1] && u[2] === 'System') hasSystem = true;
    }
    const alias = aliases.get('Random');
    for (const m of bare.matchAll(/\b(UnityEngine|System)\s*\.\s*Random\b/g)) {
      if (/^\s*using\b/.test(bare.slice(starts[at(m.index) - 1], m.index))) continue; // the using/alias line itself
      add('random-usage', m.index, `${m[1]}.Random in gameplay assembly ${asm.name}; use a seeded Pcg32 stream (determinism.md)`);
    }
    let kind = null;
    if (alias) kind = /^(?:global::)?(UnityEngine|System)\.Random$/.exec(alias)?.[1] ?? null;
    else if (!(ctx.randomTypeInScope ?? ctx.projectHasRandomType ?? false)) kind = hasUnityEngine && !hasSystem ? 'UnityEngine' : hasSystem && !hasUnityEngine ? 'System' : hasUnityEngine ? 'UnityEngine' : null;
    if (kind) {
      const seen = new Set();
      const qualified = /(?:UnityEngine|System)\s*\.\s*$/;
      const report = (off) => {
        const line = at(off);
        if (seen.has(line) || qualified.test(bare.slice(Math.max(0, off - 20), off))) return;
        seen.add(line);
        add('random-usage', off, `${kind}.Random in gameplay assembly ${asm.name}; use a seeded Pcg32 stream (determinism.md)`);
      };
      if (kind === 'UnityEngine') for (const m of bare.matchAll(UNITY_RANDOM_MEMBERS)) report(m.index);
      if (kind === 'System') {
        for (const m of bare.matchAll(/\bnew\s+Random\s*\(/g)) report(m.index);
        for (const m of bare.matchAll(/(?<![.\w])Random\s+[A-Za-z_]\w*\s*[=;,)]/g)) report(m.index);
      }
    }
  }

  if (rules.has('shader-find-keepalive') && !asm.editorOnly && !asm.test) {
    // matched in `bare` (so calls inside strings or comments never count), literal read back from `code`
    for (const m of bare.matchAll(/\bShader\s*\.\s*Find\s*\(\s*"/g)) {
      const name = literalAt(code, m.index + m[0].length - 1);
      if (name === null) continue;
      const res = ctx.keep.shader(name);
      if (!res.ok) add('shader-find-keepalive', m.index, `Shader.Find("${name}"): ${res.why} (performance-habits.md, WoO D056)`);
    }
    for (const m of bare.matchAll(/\bResources\s*\.\s*(Load|LoadAsync|LoadAll)\s*(?:<[^<>()]*>)?\s*\(\s*"/g)) {
      const p = literalAt(code, m.index + m[0].length - 1);
      if (p === null) continue;
      if (!ctx.keep.resource(p, m[1] === 'LoadAll')) {
        add('shader-find-keepalive', m.index, `Resources.${m[1]}("${p}"): nothing at Resources/${p} in Assets or embedded packages, so it returns null in a build`);
      }
    }
  }

  if (rules.has('debug-log-prefix') && ctx.prefix && !asm.test) {
    for (const m of bare.matchAll(LOG_CALL)) {
      const after = code.slice(m.index + m[0].length, m.index + m[0].length + 400);
      const lit = /^(\$@|@\$|\$|@)?"/.exec(after);
      if (!lit) continue; // not a literal first argument: cannot check
      const body = after.slice(lit[0].length);
      if (!body.startsWith(ctx.prefix)) {
        add('debug-log-prefix', m.index, `Debug.${m[1]} message does not start with the gate prefix ${ctx.prefix}`);
      }
    }
  }

  if (rules.has('find-in-update') && !asm.test) {
    for (const mem of methodsOf(scan)) {
      const nm = mem.name;
      if (!FRAME_METHODS.has(nm)) continue;
      const body = bare.slice(mem.bodyStart, mem.bodyEnd);
      for (const f of body.matchAll(FIND_CALLS)) {
        add('find-in-update', mem.bodyStart + f.index, `${f[0].replace(/\s+/g, '').replace(/\($/, '')} inside ${nm}() searches the scene every frame; cache the reference (performance-habits.md)`);
      }
    }
  }

  if (rules.has('string-gethashcode-id') && !asm.test) {
    const hashBodies = methodsOf(scan).filter((mm) => mm.name === 'GetHashCode');
    const inHashBody = (off) => hashBodies.some((mm) => off > mm.bodyStart && off < mm.bodyEnd);
    for (const m of bare.matchAll(/\.\s*GetHashCode\s*\(/g)) {
      if (inHashBody(m.index)) continue;
      // the statement around the call
      let s = m.index;
      while (s > 0 && !';{}'.includes(bare[s - 1])) s--;
      let e = m.index;
      while (e < bare.length && bare[e] !== ';' && bare[e] !== '{' && bare[e] !== '}') e++;
      const stmtCode = code.slice(s, e);
      const before = code.slice(s, m.index);
      const stringy = /("|\bnameof\s*\([^)]*\)|\.\s*(name|Name|tag|ToString\s*\(\s*\))|\bstring\s*\.\s*(Concat|Format|Join)\s*\([^;]*\))\s*$/.test(before) || /"\s*\)?\s*$/.test(before);
      const lhs = /([A-Za-z_]\w*)\s*(?:\[[^\]]*\])?\s*(?:=|:)(?!=)[^;]*$/.exec(bare.slice(s, m.index))?.[1] ?? '';
      const idLike = ID_WORDS.test(lhs) || PERSIST_CONTEXT.test(stmtCode) || /\breturn\b/.test(before) && ID_WORDS.test(nearestMethodName(scan, m.index) ?? '');
      if (stringy || idLike) {
        add('string-gethashcode-id', m.index, `GetHashCode() used for ${idLike ? `an id, key, seed or save value${lhs ? ` (${lhs})` : ''}` : 'a string'}: it is not stable across runtimes, platforms or versions; use Hash64 (determinism.md)`);
      }
    }
  }

  const sup = suppressions(src);
  const violations = [];
  let suppressed = 0;
  for (const v of found) {
    if (sup.get(v.line)?.has(v.rule)) suppressed++;
    else violations.push(v);
  }
  return { violations, suppressed };
}

/** Content of the regular string literal whose opening quote is at q, or null when unterminated on its line. */
function literalAt(code, q) {
  let out = '';
  for (let i = q + 1; i < code.length; i++) {
    const c = code[i];
    if (c === '\\') { out += code.slice(i, i + 2); i++; continue; }
    if (c === '"') return out;
    if (c === '\n') return null;
    out += c;
  }
  return null;
}

function nearestMethodName(scan, off) {
  const mem = methodsOf(scan).find((mm) => off > mm.bodyStart && off < mm.bodyEnd);
  return mem ? mem.name : null;
}

/**
 * Lint a Unity project. @param {{U: string, W: string|null, rules: string[], prefix: string|null, cfg: object,
 * excludes: string[], includeKit: boolean}} o
 */
export function lintProject(o) {
  const { U, cfg } = o;
  const rules = new Set(o.rules);
  const roots = sourceRoots(U, o.includeKit);
  const all = [];
  for (const r of roots) {
    for (const rel of listFiles(path.join(U, r), { skipDirs: SKIP_DIRS, filter: (x) => /\.(cs|asmdef|asmref)$/i.test(x) })) all.push(`${r}/${rel}`);
  }
  const excludes = [...DEFAULT_EXCLUDES, ...(cfg.exclude ?? []), ...o.excludes];
  const files = all.filter((f) => !matchesAny(f, excludes));
  const { byDir, asmdefs } = loadAssemblies(U, files);
  const corePatterns = (cfg.core_asmdefs ?? []).map(String);
  const isCoreAsm = (a) => a.noEngineReferences || corePatterns.includes(a.name) || /(^|\.)Core$/.test(a.name);
  const gameplayExempt = (cfg.gameplay_exempt ?? []).map(String);
  const keep = new KeepAlive(U, cfg);
  const sources = files.filter((f) => /\.cs$/i.test(f));
  const texts = new Map();
  const typeIndex = new Map();
  const randomTypes = []; // project types named Random: {ns, asm} (top level) or {file} (nested: visible in its own file)
  const fileNs = new Map(); // file → {namespaces, usings}
  for (const f of sources) {
    const t = readText(path.join(U, f));
    texts.set(f, t);
    const { bare } = stripCs(t);
    const scan = scanMembers(bare);
    const ranges = namespaceRanges(bare);
    const asm = assemblyFor(f, byDir);
    indexTypes(typeIndex, asm.name, bare, typeRecords(bare, scan, ranges));
    for (const ty of scan.types) {
      if (ty.name !== 'Random') continue;
      randomTypes.push(ty.path.includes('.') ? { file: f } : { ns: nsAt(ranges, ty.start), asm });
    }
    const usingNs = new Set([...bare.matchAll(/^\s*using\s+([A-Za-z_][\w.]*)\s*;/gm)].map((m) => m[1]));
    fileNs.set(f, { namespaces: new Set(ranges.map((r) => nsAt(ranges, r.start + 1))), usings: usingNs });
  }
  /** A project Random type can be named unqualified in file f of assembly asm (its namespace is open there and visible). */
  const randomInScope = (f, asm) => randomTypes.some((r) => {
    if (r.file) return r.file === f;
    if (!canReference(asm, r.asm)) return false;
    if (r.ns === '') return true;
    const { namespaces, usings } = fileNs.get(f);
    return usings.has(r.ns) || [...namespaces].some((n) => n === r.ns || n.startsWith(`${r.ns}.`));
  });
  const out = [];
  let suppressed = 0;
  if (rules.has('core-no-unityengine')) {
    for (const a of asmdefs) {
      if (!a.noEngineReferences && (corePatterns.includes(a.name) || /(^|\.)Core$/.test(a.name))) {
        out.push({ rule: 'core-no-unityengine', file: a.file, line: 1, message: `Core assembly ${a.name} does not set "noEngineReferences": true (architecture.md)` });
      }
    }
  }
  for (const f of sources) {
    const asm = assemblyFor(f, byDir);
    const gameplay = !asm.editorOnly && !asm.test && !matchesAny(f, gameplayExempt);
    const r = lintSource({ rel: f, src: texts.get(f), asm, isCore: !asm.predefined && isCoreAsm(asm), gameplay, rules, prefix: o.prefix, keep, randomTypeInScope: randomInScope(f, asm), typeIndex });
    suppressed += r.suppressed;
    for (const v of r.violations) out.push({ rule: v.rule, file: f, line: v.line, message: v.message });
  }
  out.sort((a, b) => (a.file < b.file ? -1 : a.file > b.file ? 1 : a.line - b.line || a.rule.localeCompare(b.rule)));
  return { files_scanned: sources.length, asmdefs: asmdefs.length, violations: out, suppressed };
}

export async function main(argv) {
  const { values } = parseCli(argv, {
    root: { type: 'string' },
    rule: { type: 'string', multiple: true },
    prefix: { type: 'string' },
    config: { type: 'string' },
    exclude: { type: 'string', multiple: true },
    'include-kit': { type: 'boolean', default: false },
    'list-rules': { type: 'boolean', default: false },
  });
  if (values['list-rules']) {
    emit({ ok: true, rules: [...RULES] }, { json: values.json, text: RULES });
    return EXIT.OK;
  }
  const picked = values.rule?.length ? values.rule.flatMap((r) => r.split(',')).map((r) => r.trim()).filter(Boolean) : [...RULES];
  const unknown = picked.filter((r) => !RULES.includes(r));
  if (unknown.length) throw new UsageError(`unknown rule id(s): ${unknown.join(', ')} (known: ${RULES.join(', ')})`);

  let U;
  let W;
  if (values.root) {
    U = path.resolve(values.root);
    if (!fs.existsSync(path.join(U, 'Assets'))) throw new UsageError(`--root ${values.root} has no Assets folder (not a Unity project)`);
    W = findWorkspace(U);
  } else {
    W = findWorkspace(process.cwd());
    U = W ? kitPaths(W).unity : process.cwd();
    if (!fs.existsSync(path.join(U, 'Assets'))) throw new CliError(`no Unity project found (looked at ${U}); pass --root <U>`, EXIT.INFRA);
  }
  const cfgFile = values.config ? path.resolve(values.config) : W ? path.join(W, 'Docs', 'dev', 'unity_lint.json') : null;
  if (values.config && !fs.existsSync(cfgFile)) throw new UsageError(`--config ${values.config} does not exist`);
  const cfg = readConfig(cfgFile);
  const prefix = resolvePrefix(values.prefix, U, W);
  const skipped = [];
  let rules = picked;
  if (rules.includes('debug-log-prefix') && !prefix) {
    rules = rules.filter((r) => r !== 'debug-log-prefix');
    skipped.push({ rule: 'debug-log-prefix', reason: 'no prefix: pass --prefix or set gate.config.json "prefix"' });
  }
  note(`unity_rules_lint: ${U}`);
  const res = lintProject({ U, W, rules, prefix, cfg, excludes: values.exclude ?? [], includeKit: values['include-kit'] });
  const base = W ?? U;
  const rebase = (f) => {
    const abs = path.join(U, f);
    const r = relPosix(base, abs);
    return r.startsWith('..') ? toPosix(f) : r;
  };
  const violations = res.violations.map((v) => ({ ...v, file: rebase(v.file) }));
  const ok = violations.length === 0;
  const result = {
    ok,
    exit: ok ? EXIT.OK : EXIT.FAIL,
    root: W ? relPosix(W, U) || '.' : '.',
    files_scanned: res.files_scanned,
    asmdefs: res.asmdefs,
    rules,
    skipped_rules: skipped,
    suppressed: res.suppressed,
    violations,
  };
  const text = [
    ...violations.map((v) => `${v.file}:${v.line}: [${v.rule}] ${v.message}`),
    ...skipped.map((s) => `skipped ${s.rule}: ${s.reason}`),
    `${ok ? 'PASS' : 'FAIL'}: ${violations.length} violation(s) in ${res.files_scanned} file(s), ${res.suppressed} suppressed`,
  ];
  emit(result, { json: values.json, text });
  return result.exit;
}

if (isMain(import.meta.url)) await runCli(main);

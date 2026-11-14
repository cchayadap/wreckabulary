// Assembly-definition model of a Unity project, the layer rules the R0 rung enforces, and generation of the Check and
// CoreTests projects from the *.tmpl files next to the gate (CONTRACTS §4.4 gen_asmdef_checks).
// Rules: left-only references along gate.config.json `layers`; an engine-free asmdef (noEngineReferences) references only
// engine-free project asmdefs; runtime code never references Editor-only or test asmdefs; no reference cycles.
import fs from 'node:fs';
import path from 'node:path';
import { sha256Text } from '../../lib/hash.mjs';
import { readJsonOr, readText, writeJsonAtomic, writeTextAtomic } from '../../lib/jsonio.mjs';
import { globToRegExp, isInside, toPosix } from '../../lib/paths.mjs';
import { utcIso } from '../../lib/time.mjs';

/**
 * An asmdef name the gate will use as a file name: Unity's assembly-name characters, no `..`, no trailing dot and no
 * Windows device name. Anything else (e.g. a name with separators from a third-party pack) is skipped, never written.
 * @param {string} name
 */
export function validAsmdefName(name) {
  return typeof name === 'string'
    && name.length <= 200
    && /^[A-Za-z0-9_][A-Za-z0-9_.-]*$/.test(name)
    && !name.includes('..')
    && !name.endsWith('.')
    && !/^(con|prn|aux|nul|com[0-9]|lpt[0-9]|conin\$|conout\$)(\.|$)/i.test(name);
}

const SKIP_DIR = (name) => name.startsWith('.') || name.endsWith('~') || ['Library', 'Temp', 'Logs', 'obj', 'bin', 'node_modules', 'Builds', 'UserSettings'].includes(name);

function walk(dir, onFile, depth = 0) {
  if (depth > 40) return;
  let ents;
  try {
    ents = fs.readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const e of ents) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) {
      if (!SKIP_DIR(e.name)) walk(p, onFile, depth + 1);
    } else if (e.isFile()) onFile(p, e.name);
  }
}

function readMetaGuid(file) {
  try {
    const m = /^guid:\s*([0-9a-fA-F]{32})/m.exec(fs.readFileSync(`${file}.meta`, 'utf8'));
    return m ? m[1].toLowerCase() : null;
  } catch {
    return null;
  }
}

function readJsonLenient(file) {
  try {
    return JSON.parse(readText(file).replace(/^\s*\/\/.*$/gm, ''));
  } catch {
    return null;
  }
}

/** Package names present in Packages/manifest.json, packages-lock.json and embedded Packages/<name>/package.json. */
export function presentPackages(U) {
  const names = new Set();
  const m = readJsonOr(path.join(U, 'Packages', 'manifest.json'), null, { onInvalid: 'fallback' });
  for (const k of Object.keys(m?.dependencies || {})) names.add(k);
  const l = readJsonOr(path.join(U, 'Packages', 'packages-lock.json'), null, { onInvalid: 'fallback' });
  for (const k of Object.keys(l?.dependencies || {})) names.add(k);
  try {
    for (const e of fs.readdirSync(path.join(U, 'Packages'), { withFileTypes: true })) {
      if (!e.isDirectory()) continue;
      const pj = readJsonOr(path.join(U, 'Packages', e.name, 'package.json'), null, { onInvalid: 'fallback' });
      if (pj?.name) names.add(pj.name);
    }
  } catch { /* no Packages folder */ }
  return names;
}

const VERSIONISH = /^\s*v?\d+(\.\d+)*([a-z]+\d+)?(-[0-9A-Za-z.-]+)?(\+\S*)?\s*$/i;

/**
 * Resolved package versions: packages-lock.json (over manifest.json), the package.json of a Library/PackageCache folder
 * for git or file sources, and embedded Packages/<dir>/package.json (which wins). Names without a readable version are
 * left out.
 * @param {string} U
 * @returns {Map<string, string>}
 */
export function packageVersions(U) {
  const v = new Map();
  const put = (name, ver) => { if (typeof ver === 'string' && VERSIONISH.test(ver)) v.set(name, ver.trim()); };
  const m = readJsonOr(path.join(U, 'Packages', 'manifest.json'), null, { onInvalid: 'fallback' });
  for (const [k, x] of Object.entries(m?.dependencies || {})) put(k, x);
  const l = readJsonOr(path.join(U, 'Packages', 'packages-lock.json'), null, { onInvalid: 'fallback' });
  for (const [k, x] of Object.entries(l?.dependencies || {})) put(k, x?.version);
  try {
    for (const e of fs.readdirSync(path.join(U, 'Library', 'PackageCache'), { withFileTypes: true })) {
      if (!e.isDirectory()) continue;
      const pj = readJsonOr(path.join(U, 'Library', 'PackageCache', e.name, 'package.json'), null, { onInvalid: 'fallback' });
      if (pj?.name && !v.has(pj.name)) put(pj.name, pj.version);
    }
  } catch { /* no import yet */ }
  try {
    for (const e of fs.readdirSync(path.join(U, 'Packages'), { withFileTypes: true })) {
      if (!e.isDirectory()) continue;
      const pj = readJsonOr(path.join(U, 'Packages', e.name, 'package.json'), null, { onInvalid: 'fallback' });
      if (pj?.name) put(pj.name, pj.version);
    }
  } catch { /* no Packages folder */ }
  return v;
}

function parseVersion(s) {
  const m = /^\s*v?(\d+(?:\.\d+)*)(?:([a-z]+)(\d+))?(?:-([0-9A-Za-z.-]+))?(?:\+\S*)?\s*$/i.exec(String(s ?? ''));
  if (!m) return null;
  return { nums: m[1].split('.').map(Number), tag: m[2] ? m[2].toLowerCase() : null, tagNum: m[3] ? Number(m[3]) : 0, pre: m[4] ? m[4].split('.') : null };
}

// Unity release letters: alpha < beta < rc < final (f, or no letter) < patch.
const TAG_RANK = { a: -3, b: -2, rc: -1, c: -1, f: 0, p: 1 };

/**
 * Compare two versions: numeric parts (missing = 0), then Unity's release letter (6000.3.25f1, 6000.4.0a2), then a SemVer
 * pre-release (1.0.0-pre.2 < 1.0.0). NaN when either does not parse.
 * @param {string} a
 * @param {string} b
 */
export function compareVersions(a, b) {
  const x = parseVersion(a);
  const y = parseVersion(b);
  if (!x || !y) return NaN;
  for (let i = 0; i < Math.max(x.nums.length, y.nums.length); i++) {
    const d = (x.nums[i] ?? 0) - (y.nums[i] ?? 0);
    if (d) return Math.sign(d);
  }
  const rx = x.tag ? TAG_RANK[x.tag] ?? 0 : 0;
  const ry = y.tag ? TAG_RANK[y.tag] ?? 0 : 0;
  if (rx !== ry) return Math.sign(rx - ry);
  if (x.tag && y.tag && x.tagNum !== y.tagNum) return Math.sign(x.tagNum - y.tagNum);
  if (!x.pre && !y.pre) return 0;
  if (!x.pre) return 1;
  if (!y.pre) return -1;
  for (let i = 0; i < Math.max(x.pre.length, y.pre.length); i++) {
    const p = x.pre[i];
    const q = y.pre[i];
    if (p === undefined) return -1;
    if (q === undefined) return 1;
    const pn = /^\d+$/.test(p);
    const qn = /^\d+$/.test(q);
    if (pn && qn) { if (Number(p) !== Number(q)) return Math.sign(Number(p) - Number(q)); continue; }
    if (pn !== qn) return pn ? -1 : 1;
    if (p !== q) return p < q ? -1 : 1;
  }
  return 0;
}

/**
 * Unity's versionDefines `expression`: empty = any version; `1.2.3` = at least 1.2.3; `[1.2.3]` = exactly; `[a,b]`,
 * `(a,b)`, `[a,b)`, `(a,b]` = the interval, with an empty bound left open. False when the version or expression does not parse.
 * @param {string} version
 * @param {string} expression
 */
export function versionInRange(version, expression) {
  const e = String(expression ?? '').trim();
  if (!e) return true;
  if (!parseVersion(version)) return false;
  const m = /^([[(])\s*([^,\])]*?)\s*(?:,\s*([^\])]*?)\s*)?([\])])$/.exec(e);
  if (!m) return parseVersion(e) ? compareVersions(version, e) >= 0 : false;
  const [, open, lo, hi, close] = m;
  if (hi === undefined) return open === '[' && close === ']' && Boolean(parseVersion(lo)) && compareVersions(version, lo) === 0;
  if (lo) {
    const c = compareVersions(version, lo);
    if (Number.isNaN(c) || (open === '[' ? c < 0 : c <= 0)) return false;
  }
  if (hi) {
    const c = compareVersions(version, hi);
    if (Number.isNaN(c) || (close === ']' ? c > 0 : c >= 0)) return false;
  }
  return Boolean(lo || hi);
}

let packageGuidCache = null;
/** GUID → asmdef name for package asmdefs (Library/PackageCache and embedded Packages), scanned once per process. */
function packageAsmdefGuids(U) {
  let embedded = [];
  try { embedded = fs.readdirSync(path.join(U, 'Packages')).sort(); } catch { /* none */ }
  const key = `${U}|${packageCacheFolders(U).join(',')}|${embedded.join(',')}`;
  if (packageGuidCache && packageGuidCache.key === key) return packageGuidCache.map;
  const map = new Map();
  for (const root of [path.join(U, 'Library', 'PackageCache'), path.join(U, 'Packages')]) {
    walk(root, (p, name) => {
      if (!name.endsWith('.asmdef')) return;
      const guid = readMetaGuid(p);
      const j = readJsonLenient(p);
      if (guid && j?.name) map.set(guid, j.name);
    });
  }
  packageGuidCache = { key, map };
  return map;
}

/**
 * Every asmdef (and asmref) under the code roots, as a model.
 * @param {string} U
 * @param {{codeRoots?: string[], checkExcludes?: string[]}} [opts]
 */
export function loadAsmdefs(U, opts = {}) {
  const roots = (opts.codeRoots?.length ? opts.codeRoots : ['Assets']).map((r) => toPosix(r).replace(/\/+$/, ''));
  const excludes = (opts.checkExcludes || []).map((g) => globToRegExp(g));
  const excluded = (rel) => excludes.some((re) => re.test(rel) || re.test(`${rel}/x`));
  const files = [];
  const asmrefs = [];
  for (const r of roots) {
    walk(path.join(U, r), (p, name) => {
      const rel = toPosix(path.relative(U, p));
      if (excluded(path.posix.dirname(rel))) return;
      if (name.endsWith('.asmdef')) files.push(p);
      else if (name.endsWith('.asmref')) asmrefs.push(p);
    });
  }
  const list = [];
  const warnings = [];
  const invalid = [];
  for (const f of files.sort()) {
    const j = readJsonLenient(f);
    const rel = toPosix(path.relative(U, f));
    if (!j || typeof j.name !== 'string' || !j.name) {
      warnings.push({ asmdef: rel, rule: 'unreadable', message: `${rel} is not a valid asmdef` });
      continue;
    }
    if (!validAsmdefName(j.name)) {
      const msg = `${rel} has the name ${JSON.stringify(j.name.slice(0, 80))}, which is not a valid assembly name (letters, digits, _ . -; no '..'): it is skipped`;
      warnings.push({ asmdef: rel, rule: 'bad_name', message: msg });
      invalid.push({ asmdef: rel, dir: path.posix.dirname(rel), message: msg });
      continue;
    }
    const includePlatforms = Array.isArray(j.includePlatforms) ? j.includePlatforms : [];
    const refsRaw = Array.isArray(j.references) ? j.references.filter((x) => typeof x === 'string') : [];
    const precompiled = Array.isArray(j.precompiledReferences) ? j.precompiledReferences : [];
    const defineConstraints = Array.isArray(j.defineConstraints) ? j.defineConstraints.filter((x) => typeof x === 'string') : [];
    list.push({
      name: j.name,
      file: rel,
      dir: path.posix.dirname(rel),
      guid: readMetaGuid(f),
      refsRaw,
      refs: [],
      editorOnly: includePlatforms.length === 1 && includePlatforms[0] === 'Editor',
      noEngine: j.noEngineReferences === true,
      overrideReferences: j.overrideReferences === true,
      autoReferenced: j.autoReferenced !== false,
      precompiled,
      defineConstraints,
      versionDefines: Array.isArray(j.versionDefines) ? j.versionDefines.filter((v) => v && v.name && v.define) : [],
      allowUnsafe: j.allowUnsafeCode === true,
      isTest: defineConstraints.includes('UNITY_INCLUDE_TESTS') || refsRaw.some((r) => /^(UnityEngine|UnityEditor)\.TestRunner$/.test(r)) || precompiled.includes('nunit.framework.dll'),
      extraDirs: [],
    });
  }
  const byGuid = new Map(list.filter((a) => a.guid).map((a) => [a.guid, a]));
  const byName = new Map(list.map((a) => [a.name, a]));
  const resolve = (raw) => {
    const m = /^GUID:([0-9a-fA-F]{32})$/.exec(raw);
    if (!m) return { raw, name: raw, project: byName.has(raw) };
    const g = m[1].toLowerCase();
    if (byGuid.has(g)) return { raw, name: byGuid.get(g).name, project: true };
    const pkg = packageAsmdefGuids(U).get(g);
    return { raw, name: pkg || null, project: false };
  };
  for (const a of list) {
    a.refs = a.refsRaw.map(resolve);
    for (const r of a.refs) if (!r.name) warnings.push({ asmdef: a.name, reference: r.raw, rule: 'unresolved_reference', message: `${a.name}: cannot resolve ${r.raw}` });
  }
  for (const f of asmrefs) {
    const j = readJsonLenient(f);
    const target = j?.reference ? resolve(j.reference) : null;
    const owner = target?.project ? byName.get(target.name) : null;
    if (owner) owner.extraDirs.push(path.posix.dirname(toPosix(path.relative(U, f))));
    else warnings.push({ asmdef: toPosix(path.relative(U, f)), rule: 'asmref_target', message: `asmref ${toPosix(path.relative(U, f))} points at no project asmdef` });
  }
  return { list, byName, warnings, invalid };
}

/**
 * Evaluate asmdef defineConstraints against a set of symbols (`A`, `!A`, `A || B`).
 * @param {string[]} constraints
 * @param {Set<string>} symbols
 */
export function constraintsMet(constraints, symbols) {
  return constraints.every((c) => String(c).split('||').map((t) => t.trim()).filter(Boolean).some((t) => (t.startsWith('!') ? !symbols.has(t.slice(1).trim()) : symbols.has(t))));
}

/**
 * The layer and boundary rules.
 * @param {{list: object[], byName: Map<string, object>}} model
 * @param {{layers?: string[]}} [opts]
 * @returns {{violations: {asmdef:string, reference:string, rule:string, message:string}[], warnings: object[]}}
 */
export function checkRules(model, opts = {}) {
  const layers = (opts.layers || []).filter((x) => typeof x === 'string');
  const violations = [];
  const warnings = [];
  for (const x of model.invalid || []) violations.push({ asmdef: x.asmdef, reference: x.asmdef, rule: 'bad_name', message: x.message });
  const seen = new Map();
  for (const a of model.list) {
    if (seen.has(a.name)) violations.push({ asmdef: a.name, reference: a.name, rule: 'duplicate_name', message: `two asmdefs are named ${a.name}: ${seen.get(a.name)} and ${a.file}` });
    else seen.set(a.name, a.file);
  }
  for (const a of model.list) {
    const ai = layers.indexOf(a.name);
    if (layers.length && ai < 0 && !a.editorOnly && !a.isTest) warnings.push({ asmdef: a.name, rule: 'not_in_layers', message: `${a.name} is a runtime asmdef that gate.config.json layers does not list` });
    for (const r of a.refs) {
      if (!r.project) continue;
      const b = model.byName.get(r.name);
      if (!b) continue;
      const bi = layers.indexOf(b.name);
      if (ai >= 0 && bi >= 0 && bi >= ai) {
        violations.push({ asmdef: a.name, reference: b.name, rule: 'left_only', message: `${a.name} (layer ${ai}) references ${b.name} (layer ${bi}); references must point left: ${layers.join(' < ')}` });
      }
      if (a.noEngine && !b.noEngine) violations.push({ asmdef: a.name, reference: b.name, rule: 'engine_free', message: `${a.name} has noEngineReferences but references ${b.name}, which uses the engine` });
      if (!a.editorOnly && b.editorOnly) violations.push({ asmdef: a.name, reference: b.name, rule: 'runtime_refs_editor', message: `runtime asmdef ${a.name} references Editor-only ${b.name}` });
      if (!a.isTest && b.isTest) violations.push({ asmdef: a.name, reference: b.name, rule: 'runtime_refs_tests', message: `${a.name} references test asmdef ${b.name}` });
    }
  }
  // Reference cycles among project asmdefs (Unity refuses them).
  const state = new Map();
  const stack = [];
  const visit = (a) => {
    state.set(a.name, 1);
    stack.push(a.name);
    for (const r of a.refs) {
      const b = r.project ? model.byName.get(r.name) : null;
      if (!b) continue;
      if (state.get(b.name) === 1) {
        const cyc = [...stack.slice(stack.indexOf(b.name)), b.name];
        violations.push({ asmdef: a.name, reference: b.name, rule: 'cycle', message: `reference cycle: ${cyc.join(' -> ')}` });
      } else if (!state.has(b.name)) visit(b);
    }
    stack.pop();
    state.set(a.name, 2);
  };
  for (const a of model.list) if (!state.has(a.name)) visit(a);
  return { violations, warnings };
}

/** `6000.3.25f1` → UNITY_x_OR_NEWER defines (plus the 2017–2023 series Unity 6 still defines). */
export function unityVersionDefines(version) {
  const m = /^(\d+)\.(\d+)/.exec(String(version || ''));
  if (!m) return [];
  const major = Number(m[1]);
  const minor = Number(m[2]);
  const out = ['UNITY_5_3_OR_NEWER', 'UNITY_5_6_OR_NEWER'];
  const series = { 2017: 4, 2018: 4, 2019: 4, 2020: 3, 2021: 3, 2022: 3, 2023: 3 };
  for (const [y, maxMinor] of Object.entries(series)) {
    for (let i = 1; i <= maxMinor; i++) if (major > Number(y) || (major === Number(y) && minor >= i)) out.push(`UNITY_${y}_${i}_OR_NEWER`);
  }
  if (major >= 6000) for (let i = 0; i <= minor; i++) out.push(`UNITY_6000_${i}_OR_NEWER`);
  out.push(`UNITY_${major}`, `UNITY_${major}_${minor}`);
  return out;
}

/** MSBuild item-spec escaping for literal path text (wildcards stay live). */
export function msbuildEscape(s) {
  return String(s).replace(/%/g, '%25').replace(/\$/g, '%24').replace(/@/g, '%40').replace(/'/g, '%27').replace(/;/g, '%3B');
}
const xmlEscape = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const win = (rel) => rel.replace(/\//g, '\\');
const itemPath = (rel) => `$(ProjectRoot)${xmlEscape(msbuildEscape(win(rel)))}`;

/** Fill `@@NAME@@` markers (kept distinct from the installer's {{NAME}} placeholders). */
export function fillTemplate(text, map) {
  const missing = new Set();
  const out = String(text).replace(/@@([A-Z0-9_]+)@@/g, (m, k) => {
    if (Object.prototype.hasOwnProperty.call(map, k)) return String(map[k]);
    missing.add(k);
    return m;
  });
  if (missing.size) throw new Error(`template markers without a value: ${[...missing].join(', ')}`);
  return out;
}

const TEMPLATE_FILES = ['Check/Directory.Build.props.tmpl', 'Check/Runtime.csproj.tmpl', 'Check/Editor.csproj.tmpl', 'Check/Asmdef.csproj.tmpl', 'CoreTests/CoreTests.csproj.tmpl'];

function readTemplate(gateDir, rel) {
  return readText(path.join(gateDir, ...rel.split('/')));
}

/** Plugin DLLs under the code roots (auto-referenced by asmdefs without overrideReferences). */
function pluginDlls(U, roots) {
  const out = [];
  for (const r of roots) walk(path.join(U, r), (p, name) => { if (/\.dll$/i.test(name) && out.length < 200) out.push(toPosix(path.relative(U, p))); });
  return out.sort();
}

/** Find a precompiled reference DLL at generation time (Assets, embedded Packages, then Library/PackageCache). */
function findPrecompiled(U, dllName, editor) {
  const hits = [];
  for (const root of ['Assets', 'Packages', path.join('Library', 'PackageCache')]) {
    walk(path.join(U, root), (p, name) => { if (name.toLowerCase() === dllName.toLowerCase() && hits.length < 20) hits.push(toPosix(path.relative(U, p))); });
    if (hits.length) break;
  }
  const pick = hits.find((h) => editor || !/\/Editor\//i.test(h)) || hits[0];
  return pick || null;
}

/**
 * Compute everything the templates need; pure except for reading the project.
 * @param {{U:string, gateDir:string, cfg:object}} ctx
 * @param {{editorDir?: string|null, unityVersion?: string|null}} opts
 */
export function planGeneration(ctx, opts = {}) {
  const U = ctx.U;
  const cfg = ctx.cfg;
  const roots = (cfg.codeRoots?.length ? cfg.codeRoots : ['Assets']).map((r) => toPosix(r).replace(/\/+$/, ''));
  const model = loadAsmdefs(U, { codeRoots: roots, checkExcludes: cfg.checkExcludes });
  const rules = checkRules(model, { layers: cfg.layers });
  const pkgs = presentPackages(U);
  const versions = packageVersions(U);
  const base = new Set([...unityVersionDefines(opts.unityVersion), 'UNITY_STANDALONE_WIN', 'UNITY_STANDALONE', 'CSHARP_7_3_OR_NEWER', ...(cfg.extraDefines || [])]);
  if (pkgs.has('com.unity.inputsystem')) base.add('ENABLE_INPUT_SYSTEM');
  if (pkgs.has('com.unity.render-pipelines.universal')) base.add('UNITY_PIPELINE_URP');
  if (pkgs.has('com.unity.render-pipelines.high-definition')) base.add('UNITY_PIPELINE_HDRP');
  // versionDefines: the resource (Unity or a package) must be present AND its version inside `expression`, as in Unity.
  const versionWarnings = new Map();
  const versionDefined = (a) => a.versionDefines.filter((v) => {
    if (v.name !== 'Unity' && !pkgs.has(v.name)) return false;
    if (!String(v.expression ?? '').trim()) return true;
    const ver = v.name === 'Unity' ? opts.unityVersion : versions.get(v.name);
    if (!ver) {
      versionWarnings.set(`${a.name}|${v.define}`, { asmdef: a.name, rule: 'version_unknown', message: `${a.name}: versionDefine ${v.define} needs ${v.name} ${v.expression}, but its version is unknown here (no packages-lock.json yet?): not defined` });
      return false;
    }
    return versionInRange(ver, v.expression);
  }).map((v) => v.define);
  const allVersionDefines = new Set(model.list.flatMap(versionDefined));
  const editorSymbols = new Set([...base, ...allVersionDefines, 'UNITY_EDITOR', 'UNITY_EDITOR_WIN', 'UNITY_INCLUDE_TESTS']);
  const active = [];
  const skipped = [];
  for (const a of model.list) {
    const sym = new Set([...base, ...versionDefined(a), 'UNITY_INCLUDE_TESTS', ...(a.editorOnly ? ['UNITY_EDITOR', 'UNITY_EDITOR_WIN'] : [])]);
    if (constraintsMet(a.defineConstraints, sym)) active.push(a);
    else skipped.push({ asmdef: a.name, rule: 'define_constraints', message: `${a.name} is skipped: defineConstraints ${a.defineConstraints.join(', ')} are not met` });
  }
  const activeNames = new Set(active.map((a) => a.name));
  const invalidDirs = (model.invalid || []).map((x) => x.dir);
  const skippedDirs = [...model.list.filter((a) => !activeNames.has(a.name)).flatMap((a) => [a.dir, ...a.extraDirs]), ...invalidDirs];
  // Folders owned by each asmdef (its folder minus nested asmdef/asmref folders). A skipped bad-name asmdef still owns its
  // folder, so its scripts never fall into a parent asmdef or Assembly-CSharp.
  const allDirs = [...model.list.flatMap((a) => [a.dir, ...a.extraDirs].map((d) => ({ d, owner: a.name }))), ...invalidDirs.map((d) => ({ d, owner: '' }))];
  const nestedOf = (a) => allDirs.filter((x) => x.owner !== a.name && [a.dir, ...a.extraDirs].some((own) => x.d.startsWith(`${own}/`))).map((x) => x.d);
  const inAnyAsmdef = (relFile) => allDirs.some((x) => relFile.startsWith(`${x.d}/`));
  const excludeGlobs = (cfg.checkExcludes || []).map((g) => win(g));
  const exclList = (extra) => [...excludeGlobs.map((g) => `$(ProjectRoot)${xmlEscape(msbuildEscape(g))}`), ...extra].join(';');

  // Whole-project Runtime / Editor projects (KNOTWELL's fast check, generalised).
  const editorOrTestDirs = active.filter((a) => a.editorOnly || a.isTest).flatMap((a) => [a.dir, ...a.extraDirs]);
  const rootItems = (runtime) => roots.map((r) => {
    const extra = [...skippedDirs, ...(runtime ? editorOrTestDirs : [])].map((d) => `${itemPath(d)}\\**`);
    if (runtime) extra.push(`${itemPath(r)}\\**\\Editor\\**`, `${itemPath(r)}\\**\\Tests\\**`);
    return `    <Compile Include="${itemPath(r)}\\**\\*.cs" Exclude="${exclList(extra)}" />`;
  }).join('\n');
  const ownDlls = model.list.map((a) => `${a.name}.dll`);
  const saExcl = (extra) => [...ownDlls, 'Assembly-CSharp*.dll', '*DocCode*.dll', ...extra].map((n) => `$(ProjectRoot)Library\\ScriptAssemblies\\${xmlEscape(msbuildEscape(n))}`).join(';');

  // Project-reference edges without cycles: an edge that would close a cycle is left out (the cycle is already a rule
  // violation), so MSBuild still builds and reports the compile errors instead of stopping at MSB4006.
  const edges = new Map(active.map((a) => [a.name, new Set()]));
  const reaches = (from, to) => {
    const seen = new Set();
    const stack = [from];
    while (stack.length) {
      const n = stack.pop();
      if (n === to) return true;
      if (seen.has(n)) continue;
      seen.add(n);
      for (const m of edges.get(n) || []) stack.push(m);
    }
    return false;
  };
  for (const a of active) {
    for (const r of a.refs) {
      if (r.project && activeNames.has(r.name) && r.name !== a.name && !reaches(r.name, a.name)) edges.get(a.name).add(r.name);
    }
  }
  // Per-asmdef projects. References that cannot be resolved now (no Library/PackageCache before the first import, a
  // package not installed) are recorded, so generation is redone once they may resolve.
  const unresolved = [];
  const plugins = pluginDlls(U, roots);
  const asmdefProjects = active.map((a) => {
    const nested = nestedOf(a);
    const compile = [a.dir, ...a.extraDirs].map((d) => {
      const ex = [...nested.filter((n) => n.startsWith(`${d}/`)).map((n) => `${itemPath(n)}\\**`)];
      return `    <Compile Include="${itemPath(d)}\\**\\*.cs"${ex.length || excludeGlobs.length ? ` Exclude="${exclList(ex)}"` : ''} />`;
    });
    const refs = [];
    let newtonsoft = !a.overrideReferences;
    for (const r of a.refs) {
      if (!r.name) {
        unresolved.push({ asmdef: a.name, kind: 'guid', ref: r.raw });
        continue;
      }
      if (r.project) {
        if (edges.get(a.name).has(r.name)) refs.push(`    <ProjectReference Include="${xmlEscape(msbuildEscape(r.name))}.csproj" />`);
      } else {
        const dll = `$(ProjectRoot)Library\\ScriptAssemblies\\${xmlEscape(msbuildEscape(r.name))}.dll`;
        refs.push(`    <Reference Include="${dll}" Private="false" Condition="Exists('${dll}')" />`);
      }
    }
    for (const p of a.precompiled) {
      if (/^newtonsoft\.json\.dll$/i.test(p)) { newtonsoft = true; continue; }
      if (/^nunit\.framework\.dll$/i.test(p)) continue; // KitTests adds it
      const found = findPrecompiled(U, p, a.editorOnly);
      if (found) refs.push(`    <Reference Include="${itemPath(found)}" Private="false" />`);
      else unresolved.push({ asmdef: a.name, kind: 'precompiled', ref: String(p) });
    }
    if (!a.overrideReferences) for (const d of plugins) refs.push(`    <Reference Include="${itemPath(d)}" Private="false" />`);
    const defines = versionDefined(a);
    return {
      name: a.name,
      file: `asmdef/${a.name}.csproj`,
      text: {
        ASMDEF_FILE: xmlEscape(a.file),
        NAME: xmlEscape(a.name),
        NO_ENGINE: a.noEngine ? 'true' : 'false',
        EDITOR: a.editorOnly ? 'true' : 'false',
        TESTS: a.isTest ? 'true' : 'false',
        NEWTONSOFT: newtonsoft ? 'true' : 'false',
        UNSAFE: a.allowUnsafe ? 'true' : 'false',
        DEFINES: xmlEscape([...defines, ...(a.isTest ? ['UNITY_INCLUDE_TESTS'] : [])].join(';')),
        COMPILE_ITEMS: compile.join('\n'),
        REFERENCES: refs.join('\n'),
      },
    };
  });
  // Loose scripts outside every asmdef compile into Assembly-CSharp (and -Editor for Editor folders).
  const loose = { runtime: false, editor: false };
  for (const r of roots) {
    walk(path.join(U, r), (p, name) => {
      if (!name.endsWith('.cs')) return;
      const rel = toPosix(path.relative(U, p));
      if (inAnyAsmdef(rel)) return;
      if (/(^|\/)Editor\//.test(rel)) loose.editor = true;
      else loose.runtime = true;
    });
  }

  // CoreTests: engine-free asmdefs plus their pure tests.
  const coreNames = cfg.core?.asmdefs?.length ? cfg.core.asmdefs : active.filter((a) => a.noEngine && !a.isTest && !a.editorOnly).map((a) => a.name);
  const coreAsm = coreNames.map((n) => model.byName.get(n)).filter(Boolean);
  let testDirs = (cfg.core?.testDirs || []).map((d) => toPosix(d).replace(/\/+$/, ''));
  if (!cfg.core?.testDirs?.length) {
    for (const t of active.filter((a) => a.isTest)) {
      for (const d of [t.dir, ...t.extraDirs]) {
        if (fs.existsSync(path.join(U, d, 'Core'))) testDirs.push(`${d}/Core`);
      }
    }
  }
  testDirs = [...new Set(testDirs)];
  const core = coreAsm.length
    ? {
        COMPILE_ITEMS: [
          ...coreAsm.flatMap((a) => [a.dir, ...a.extraDirs].map((d) => `    <Compile Include="${itemPath(d)}\\**\\*.cs" />`)),
          ...testDirs.map((d) => `    <Compile Include="${itemPath(d)}\\**\\*.cs" />`),
        ].join('\n'),
        CORE_TFM: xmlEscape(cfg.core?.tfm || 'net9.0'),
        CORE_ASMDEFS: xmlEscape(coreAsm.map((a) => a.name).join(', ')),
      }
    : null;

  return {
    model,
    rules: { violations: rules.violations, warnings: [...model.warnings, ...rules.warnings, ...skipped, ...versionWarnings.values()] },
    unresolved,
    plugins,
    active,
    loose,
    common: {
      UNITY_EDITOR_DIR: xmlEscape(opts.editorDir || 'C:\\Program Files\\Unity\\Hub\\Editor\\UNKNOWN\\Editor'),
      PROJECT_ROOT_REL: '..\\..\\..\\..\\',
      DEFINES: xmlEscape([...base].join(';')),
      SA_EXCLUDES_RUNTIME: saExcl(['*Editor*.dll', '*CodeGen*.dll', '*Tests*.dll', '*TestRunner*.dll']),
      SA_EXCLUDES_EDITOR: saExcl([]),
      VERSION_DEFINES: xmlEscape([...allVersionDefines].join(';')),
      EDITOR_DEFINES: xmlEscape([...editorSymbols].filter((s) => !base.has(s)).join(';')),
    },
    runtimeItems: rootItems(true),
    editorItems: rootItems(false),
    asmdefProjects,
    core,
    coreTestDirs: testDirs,
  };
}

/**
 * Write Check/Directory.Build.props, Check/Runtime.csproj, Check/Editor.csproj, Check/asmdef/<Name>.csproj,
 * Check/asmdef/_All.csproj and CoreTests/CoreTests.csproj (when an engine-free asmdef exists), plus Check/.gen.json.
 * @returns {{projects:string[], violations:object[], warnings:object[], core:boolean, inputs_sha256:string}}
 */
export function generateChecks(ctx, opts = {}) {
  const gateDir = ctx.gateDir;
  const plan = planGeneration(ctx, opts);
  const t = Object.fromEntries(TEMPLATE_FILES.map((f) => [f, readTemplate(gateDir, f)]));
  const checkDir = path.join(gateDir, 'Check');
  const asmDir = path.join(checkDir, 'asmdef');
  const written = [];
  const write = (abs, text) => {
    // Names come from project content: never write outside the gate folder (a crafted asmdef name is also refused earlier).
    if (!isInside(abs, gateDir)) throw new Error(`refusing to write outside ${gateDir}: ${abs}`);
    const body = text.replace(/\r\n/g, '\n');
    let same = false;
    try { same = fs.readFileSync(abs, 'utf8') === body; } catch { /* new file */ }
    // Unchanged projects keep their timestamps, so a regeneration does not force MSBuild to rebuild everything.
    if (!same) writeTextAtomic(abs, body);
    written.push(toPosix(path.relative(ctx.U, abs)));
  };
  write(path.join(checkDir, 'Directory.Build.props'), fillTemplate(t['Check/Directory.Build.props.tmpl'], plan.common));
  write(path.join(checkDir, 'Runtime.csproj'), fillTemplate(t['Check/Runtime.csproj.tmpl'], { ...plan.common, COMPILE_ITEMS: plan.runtimeItems }));
  write(path.join(checkDir, 'Editor.csproj'), fillTemplate(t['Check/Editor.csproj.tmpl'], { ...plan.common, COMPILE_ITEMS: plan.editorItems }));
  fs.mkdirSync(asmDir, { recursive: true });
  const keep = new Set();
  for (const p of plan.asmdefProjects) {
    write(path.join(checkDir, ...p.file.split('/')), fillTemplate(t['Check/Asmdef.csproj.tmpl'], p.text));
    keep.add(path.basename(p.file).toLowerCase());
  }
  // Loose scripts: Assembly-CSharp(-Editor) reference every auto-referenced runtime asmdef.
  const autoRefs = plan.active.filter((a) => a.autoReferenced && !a.isTest);
  const looseProject = (name, editor) => {
    const refs = autoRefs.filter((a) => editor || !a.editorOnly).map((a) => `    <ProjectReference Include="${xmlEscape(msbuildEscape(a.name))}.csproj" />`);
    if (editor && plan.loose.runtime) refs.push('    <ProjectReference Include="Assembly-CSharp.csproj" />');
    const roots = (ctx.cfg.codeRoots?.length ? ctx.cfg.codeRoots : ['Assets']).map((r) => toPosix(r));
    const asmDirs = plan.model.list.flatMap((a) => [a.dir, ...a.extraDirs]);
    const compile = roots.map((r) => {
      const ex = asmDirs.map((d) => `${itemPath(d)}\\**`);
      if (!editor) ex.push(`${itemPath(r)}\\**\\Editor\\**`);
      const inc = editor ? `${itemPath(r)}\\**\\Editor\\**\\*.cs` : `${itemPath(r)}\\**\\*.cs`;
      return `    <Compile Include="${inc}" Exclude="${[...ex, ...(ctx.cfg.checkExcludes || []).map((g) => `$(ProjectRoot)${xmlEscape(msbuildEscape(win(g)))}`)].join(';')}" />`;
    });
    return fillTemplate(t['Check/Asmdef.csproj.tmpl'], {
      ASMDEF_FILE: '(loose scripts outside every asmdef)',
      NAME: name,
      NO_ENGINE: 'false',
      EDITOR: editor ? 'true' : 'false',
      TESTS: 'false',
      NEWTONSOFT: 'true',
      UNSAFE: 'false',
      DEFINES: '',
      COMPILE_ITEMS: compile.join('\n'),
      REFERENCES: [...refs, ...plan.plugins.map((d) => `    <Reference Include="${itemPath(d)}" Private="false" />`)].join('\n'),
    });
  };
  if (plan.loose.runtime) { write(path.join(asmDir, 'Assembly-CSharp.csproj'), looseProject('Assembly-CSharp', false)); keep.add('assembly-csharp.csproj'); }
  if (plan.loose.editor) { write(path.join(asmDir, 'Assembly-CSharp-Editor.csproj'), looseProject('Assembly-CSharp-Editor', true)); keep.add('assembly-csharp-editor.csproj'); }
  const allRefs = [...keep].sort().map((f) => {
    const real = fs.readdirSync(asmDir).find((x) => x.toLowerCase() === f) || f;
    return `    <ProjectReference Include="${xmlEscape(msbuildEscape(real))}" />`;
  });
  write(path.join(asmDir, '_All.csproj'), [
    '<Project Sdk="Microsoft.NET.Sdk">',
    '  <!-- Generated by gen_asmdef_checks.mjs: builds every per-asmdef check project. Do not edit. -->',
    '  <PropertyGroup>',
    '    <AssemblyName>GskCheck.AllAsmdefs</AssemblyName>',
    '    <KitNoEngine>true</KitNoEngine>',
    '  </PropertyGroup>',
    '  <ItemGroup>',
    ...allRefs,
    '  </ItemGroup>',
    '</Project>',
    '',
  ].join('\n'));
  keep.add('_all.csproj');
  for (const f of fs.readdirSync(asmDir)) if (f.toLowerCase().endsWith('.csproj') && !keep.has(f.toLowerCase())) fs.unlinkSync(path.join(asmDir, f));
  let core = false;
  const coreFile = path.join(gateDir, 'CoreTests', 'CoreTests.csproj');
  if (plan.core) {
    write(coreFile, fillTemplate(t['CoreTests/CoreTests.csproj.tmpl'], { ...plan.core, PROJECT_ROOT_REL: plan.common.PROJECT_ROOT_REL }));
    core = true;
  } else if (fs.existsSync(coreFile)) {
    fs.unlinkSync(coreFile);
  }
  const inputs = inputsHash(ctx, opts);
  writeJsonAtomic(path.join(checkDir, '.gen.json'), {
    schema: 1,
    utc: utcIso(),
    inputs_sha256: inputs,
    editor_dir: opts.editorDir || null,
    unity_version: opts.unityVersion || null,
    projects: written,
    core,
    core_test_dirs: plan.coreTestDirs,
    violations: plan.rules.violations.length,
    unresolved: plan.unresolved,
    package_cache: fs.existsSync(path.join(ctx.U, 'Library', 'PackageCache')),
  });
  return { projects: written, violations: plan.rules.violations, warnings: plan.rules.warnings, core, inputs_sha256: inputs, unresolved: plan.unresolved };
}

/** Sorted folder names of Library/PackageCache (`<package>@<hash>`: a version change renames the folder). */
function packageCacheFolders(U) {
  try {
    return fs.readdirSync(path.join(U, 'Library', 'PackageCache'), { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name).sort();
  } catch {
    return [];
  }
}

/**
 * Hash of everything generation depends on: asmdef/asmref files and their .meta GUIDs, config fields, editor dir, the
 * package set with resolved versions, the Library/PackageCache folder names (a version change renames `<pkg>@<hash>`, and
 * the first import creates them), plugin DLLs under the code roots, and the templates. The gate regenerates when it
 * differs from Check/.gen.json.
 */
export function inputsHash(ctx, opts = {}) {
  const U = ctx.U;
  const cfg = ctx.cfg;
  const roots = (cfg.codeRoots?.length ? cfg.codeRoots : ['Assets']).map((r) => toPosix(r));
  const parts = [];
  const dlls = [];
  for (const r of roots) {
    walk(path.join(U, r), (p, name) => {
      if (/\.dll$/i.test(name) && dlls.length < 200) dlls.push(toPosix(path.relative(U, p)));
      if (name.endsWith('.asmdef') || name.endsWith('.asmref') || name.endsWith('.asmdef.meta')) {
        let body = '';
        try { body = fs.readFileSync(p, 'utf8'); } catch { /* raced */ }
        parts.push(`${toPosix(path.relative(U, p))}\n${body}`);
      }
    });
  }
  parts.sort();
  parts.push(JSON.stringify({ codeRoots: cfg.codeRoots, checkExcludes: cfg.checkExcludes, layers: cfg.layers, core: cfg.core, extraDefines: cfg.extraDefines }));
  parts.push(`editor:${opts.editorDir || ''}|version:${opts.unityVersion || ''}`);
  parts.push([...presentPackages(U)].sort().join(','));
  parts.push([...packageVersions(U).entries()].map(([k, v]) => `${k}@${v}`).sort().join(','));
  parts.push(`cache:${packageCacheFolders(U).join(',')}`);
  parts.push(`plugins:${dlls.sort().join(',')}`);
  for (const f of TEMPLATE_FILES) {
    try { parts.push(readTemplate(ctx.gateDir, f)); } catch { parts.push(`missing:${f}`); }
  }
  return sha256Text(parts.join('\n\u0000\n'));
}

/**
 * True when Check/.gen.json matches the current inputs and the generated projects exist. References left unresolved at
 * generation (recorded in .gen.json) make it stale while Library/PackageCache exists, so a project generated before its
 * first import picks up the package DLLs and GUID references as soon as they can resolve.
 */
export function generationFresh(ctx, opts = {}) {
  const stamp = readJsonOr(path.join(ctx.gateDir, 'Check', '.gen.json'), null, { onInvalid: 'fallback' });
  if (!stamp || stamp.inputs_sha256 !== inputsHash(ctx, opts)) return false;
  if (Array.isArray(stamp.unresolved) && stamp.unresolved.length && fs.existsSync(path.join(ctx.U, 'Library', 'PackageCache'))) return false;
  return ['Directory.Build.props', 'Runtime.csproj', 'Editor.csproj', path.join('asmdef', '_All.csproj')].every((f) => fs.existsSync(path.join(ctx.gateDir, 'Check', f)));
}

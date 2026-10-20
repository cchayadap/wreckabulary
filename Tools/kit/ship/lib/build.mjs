// What a player build actually ships: BuildReport packed assets (packed_assets.json from BuildReportExport) plus a rescan
// of the build folder (DLLs, native plugins, StreamingAssets), joined to Docs/licenses.csv (ARCHITECTURE §3.13, P11;
// research_ship.md §3.4 and §4.2: managed DLLs, plugins and StreamingAssets are files, not packed assets). IL2CPP builds
// have no Managed folder, so ScriptingAssemblies.json names their assemblies. Code (asmdef assemblies, packed scripts)
// counts as the project's own unless a LICENSE/COPYING/NOTICE file in its pack folder or a non-Own-Work row says not.
import fs from 'node:fs';
import path from 'node:path';
import { listFiles, sha256File } from '../../lib/hash.mjs';
import { readJson, readJsonOr, readText } from '../../lib/jsonio.mjs';
import { validate, formatErrors } from '../../lib/schema.mjs';
import { toPosix } from '../../lib/paths.mjs';
import { licencesSchema, normRowPath, pathKey, relOrAbs } from './ledger.mjs';
import { isToolBinaryName } from './rules.mjs';

const BUILD_ID = /^\d{8}T\d{6}Z$/;
// Compiled code is not a raw media file: an embedded_only DLL or native plugin ships in its binary form.
const RAW_OK_EXT = /\.(dll|so|dylib|bundle|exe)$/i;
/** File names that carry a third-party licence (LICENSE, COPYING, NOTICE, OFL). */
export const LICENCE_FILE = /^(licen[cs]e|copying|notice|ofl)(\.(txt|md))?$|^(licen[cs]e|copying|notice)[-_. ][^.]*\.(txt|md)$|ofl\.txt$/i;

/**
 * Resolve --build: an absolute folder, a folder relative to U, W or the cwd, or `latest` (newest release build under U/Builds).
 * @param {{ws: string, unity: string, cwd: string}} ctx
 * @param {string} arg
 * @returns {string|null}
 */
export function resolveBuildDir(ctx, arg) {
  if (!arg) return null;
  if (arg === 'latest') {
    const root = path.join(ctx.unity, 'Builds');
    let names = [];
    try { names = fs.readdirSync(root, { withFileTypes: true }).filter((e) => e.isDirectory() && BUILD_ID.test(e.name)).map((e) => e.name).sort(); } catch { /* none */ }
    return names.length ? path.join(root, names[names.length - 1]) : null;
  }
  const cands = path.isAbsolute(arg) ? [arg] : [path.join(ctx.unity, arg), path.join(ctx.ws, arg), path.resolve(ctx.cwd, arg)];
  return cands.find((c) => fs.existsSync(c) && fs.statSync(c).isDirectory()) || null;
}

/**
 * The newest shippable build under U/Builds: a <build id> folder whose build.json / packed_assets.json say kind
 * release and not development. @param {{unity: string}} ctx @returns {string|null}
 */
export function latestReleaseBuild(ctx) {
  const root = path.join(ctx.unity, 'Builds');
  let names = [];
  try { names = fs.readdirSync(root, { withFileTypes: true }).filter((e) => e.isDirectory() && BUILD_ID.test(e.name)).map((e) => e.name).sort().reverse(); } catch { /* none */ }
  for (const n of names) {
    const idn = buildIdentity(path.join(root, n));
    if (idn.kind === 'release' && !idn.development) return path.join(root, n);
  }
  return null;
}

/**
 * Build identity from the folder name, build.json and packed_assets.json; the most development-like answer wins.
 * @param {string} dir
 * @returns {{id: string, name: string, kind: 'release'|'release-qa'|'dev', development: boolean, buildJson: object|null, reasons: string[]}}
 */
export function buildIdentity(dir) {
  const name = path.basename(dir);
  const buildJson = readJsonOr(path.join(dir, 'build.json'), null, { onInvalid: 'fallback' });
  const packed = readJsonOr(path.join(dir, 'packed_assets.json'), null, { onInvalid: 'fallback' });
  const reasons = [];
  let kind = /^dev-/i.test(name) ? 'dev' : /^qa-/i.test(name) ? 'release-qa' : 'release';
  if (kind !== 'release') reasons.push(`folder name ${name}`);
  for (const [label, doc] of [['build.json', buildJson], ['packed_assets.json', packed]]) {
    if (!doc) continue;
    if (doc.kind === 'dev' && kind !== 'dev') { kind = 'dev'; reasons.push(`${label} kind dev`); }
    else if (doc.kind === 'release-qa' && kind === 'release') { kind = 'release-qa'; reasons.push(`${label} kind release-qa`); }
  }
  const development = kind === 'dev' || buildJson?.development === true || packed?.development === true;
  if (development && kind !== 'dev') reasons.push('development flag set');
  const id = buildJson?.id || packed?.build_id || name.replace(/^(dev|qa)-/i, '');
  return { id, name, kind, development, buildJson, reasons };
}

/** The `<Product>_Data` folder name of a Windows player build, or null. @param {string} dir */
export function dataFolder(dir) {
  let ents = [];
  try { ents = fs.readdirSync(dir, { withFileTypes: true }); } catch { return null; }
  const d = ents.find((e) => e.isDirectory() && /_Data$/i.test(e.name));
  return d ? d.name : null;
}

/**
 * A LICENSE/COPYING/NOTICE file in the folder of a U-relative path or a folder above it, stopping below the Assets and
 * Packages roots (a licence file there belongs to the game or to nobody in particular).
 * @param {string} unity
 * @param {string} uRel U-relative POSIX path of a file
 * @param {Map<string, string|null>} [cache] folder → licence file (U-relative) or null
 * @returns {string|null} U-relative POSIX path of the licence file
 */
export function licenceFileNear(unity, uRel, cache = new Map()) {
  let dir = path.posix.dirname(toPosix(uRel));
  const seen = [];
  let found = null;
  while (dir.includes('/')) {
    if (cache.has(dir)) { found = cache.get(dir); break; }
    seen.push(dir);
    let names = [];
    try { names = fs.readdirSync(path.join(unity, dir), { withFileTypes: true }).filter((e) => e.isFile() && LICENCE_FILE.test(e.name)).map((e) => e.name).sort(); } catch { /* no folder */ }
    if (names.length) { found = `${dir}/${names[0]}`; break; }
    dir = path.posix.dirname(dir);
  }
  for (const d of seen) cache.set(d, found);
  return found;
}

/**
 * Assemblies defined by .asmdef files under U/Assets and U/Packages.
 * @param {string} unity
 * @returns {Map<string, {name: string, asmdef: string, licenceFile: string|null}>} lower-case name → asmdef (U-relative)
 *   and the licence file beside it or above it (licenceFileNear), which marks it as third-party source code
 */
export function projectAssemblies(unity) {
  const out = new Map();
  const cache = new Map();
  for (const root of ['Assets', 'Packages']) {
    const base = path.join(unity, root);
    for (const rel of listFiles(base, { filter: (r) => r.toLowerCase().endsWith('.asmdef') })) {
      const doc = readJsonOr(path.join(base, rel), null, { onInvalid: 'fallback' });
      if (!doc?.name) continue;
      const asmdef = `${root}/${rel}`;
      out.set(String(doc.name).toLowerCase(), { name: String(doc.name), asmdef, licenceFile: licenceFileNear(unity, asmdef, cache) });
    }
  }
  return out;
}

const ENGINE_DLL = /^(unityengine|unityeditor|unity\.|system(\.|$)|mono\.|mscorlib|netstandard|microsoft\.|bee\.)/i;
const ENGINE_ROOT = /^(unityplayer\.dll|unitycrashhandler(32|64)\.exe|baselib\.dll|gameassembly\.dll|winpixeventruntime\.dll)$/i;
const DOC_ROOT = /^(third_party_notices|credits|readme|licen[cs]e|notice)[^/]*$/i;
const CODE_EXT = new Set(['.cs', '.asmdef', '.asmref', '.dll', '.so', '.dylib', '.bundle', '.jslib', '.shader', '.hlsl', '.cginc', '.compute', '.shadergraph', '.shadersubgraph']);
const AUTHORED_EXT = new Set(['.unity', '.prefab', '.mat', '.asset', '.controller', '.overridecontroller', '.anim', '.mask', '.physicmaterial', '.physicsmaterial2d', '.mixer', '.playable', '.signal', '.spriteatlas', '.spriteatlasv2', '.rendertexture', '.lighting', '.terrainlayer', '.brush', '.flare', '.guiskin', '.fontsettings', '.vfx', '.inputactions', '.preset', '.cubemap', '.giparams', '.lightingdata', '.uss', '.uxml', '.tss']);

/**
 * Classify one packed source asset path (U-relative, as BuildReport reports it).
 * @returns {'engine'|'own'|'code'|'authored'|'needs_row'}
 */
export function classifyPacked(src) {
  const s = toPosix(String(src || ''));
  if (!s || /^(resources\/unity_builtin_extra|library\/unity (default|editor) resources|built-in)/i.test(s)) return 'engine';
  if (/^packages\/com\.unity\./i.test(s)) return 'engine';
  if (/^packages\/com\.gamestudiokit\.core\//i.test(s)) return 'own';
  const ext = path.posix.extname(s).toLowerCase();
  if (CODE_EXT.has(ext)) return 'code';
  if (AUTHORED_EXT.has(ext)) {
    if (/(^|\/)textmesh pro\//i.test(s) || /sdf/i.test(path.posix.basename(s))) return 'needs_row';
    return 'authored';
  }
  return 'needs_row';
}

/**
 * Classify one file of the build folder (path relative to the build root, POSIX).
 * @param {string} rel
 * @param {{data: string|null, own: Set<string>}} info
 * @returns {{cls: 'engine'|'own'|'needs_row'|'tool_binary'|'debug'|'steam_appid'|'addressables'|'coplay'|'streaming', why: string}}
 */
export function classifyBuildFile(rel, info) {
  const base = path.posix.basename(rel);
  if (isToolBinaryName(base)) return { cls: 'tool_binary', why: 'dev-only tool binary' };
  if (/_backupthisfolder_butdontshipitwithyourgame(\/|$)|_burstdebuginformation_donotship(\/|$)/i.test(rel)) return { cls: 'debug', why: "Unity's don't-ship folder" };
  if (/\.pdb$/i.test(base)) return { cls: 'debug', why: 'debug symbols' };
  if (/^steam_appid\.txt$/i.test(base)) return { cls: 'steam_appid', why: 'steam_appid.txt must not ship (Valve)' };
  if (/^mcpforunity/i.test(base)) return { cls: 'coplay', why: 'CoplayDev unity-mcp runtime assembly' };
  const top = rel.split('/')[0];
  if (!rel.includes('/')) {
    if (ENGINE_ROOT.test(base)) return { cls: 'engine', why: 'Unity player runtime' };
    if (/\.exe$/i.test(base) && info.data && info.data.toLowerCase() === `${base.slice(0, -4).toLowerCase()}_data`) return { cls: 'engine', why: 'player executable' };
    if (DOC_ROOT.test(base)) return { cls: 'own', why: 'project documents' };
    if (/^(build\.json|packed_assets\.json|qa_token\.txt)$/i.test(base)) return { cls: 'own', why: 'kit build metadata' };
    return { cls: 'needs_row', why: 'file in the build root' };
  }
  if (/^(monobleedingedge|d3d12)$/i.test(top)) return { cls: 'engine', why: 'Unity runtime folder' };
  if (info.data && top.toLowerCase() === info.data.toLowerCase()) {
    const inner = rel.slice(top.length + 1);
    const innerLower = inner.toLowerCase();
    if (innerLower.startsWith('managed/')) {
      const n = path.posix.basename(innerLower).replace(/\.dll$/, '');
      if (/^assembly-csharp/.test(n) || /^gamestudiokit\./.test(n) || info.own.has(n)) return { cls: 'own', why: 'project assembly' };
      if (ENGINE_DLL.test(n)) return { cls: 'engine', why: 'engine or Unity package assembly' };
      return { cls: 'needs_row', why: 'third-party managed assembly' };
    }
    if (innerLower.startsWith('plugins/')) {
      if (/lib_burst_generated/i.test(base)) return { cls: 'own', why: 'Burst-compiled project code' };
      return { cls: 'needs_row', why: 'native plugin' };
    }
    if (innerLower.startsWith('streamingassets/aa/')) return { cls: 'addressables', why: 'Addressables content (not covered by packed_assets.json)' };
    if (innerLower.startsWith('streamingassets/')) return { cls: 'streaming', why: 'StreamingAssets file' };
    if (/^(il2cpp_data|resources|unitysubsystems)\//.test(innerLower)) return { cls: 'engine', why: 'engine data' };
    if (!inner.includes('/')) return { cls: 'engine', why: 'serialized player data (contents are the packed assets)' };
    return { cls: 'needs_row', why: 'file in the data folder' };
  }
  return { cls: 'needs_row', why: 'file in the build folder' };
}

/**
 * Find a ledger row covering a W-relative path: exact path first, then the longest folder row (ending in "/").
 * @param {Map<string, object>} exact pathKey → row
 * @param {{key: string, row: object}[]} folders sorted longest first
 * @param {string} rel
 */
export function coveringRow(exact, folders, rel) {
  const k = pathKey(rel);
  if (exact.has(k)) return exact.get(k);
  for (const f of folders) if (k.startsWith(f.key)) return f.row;
  return null;
}

/** Byte search for type or assembly names inside player binaries (Mono DLLs, IL2CPP metadata, ScriptingAssemblies.json). */
export function findInPlayer(dir, needles) {
  const data = dataFolder(dir);
  const found = new Map(needles.map((n) => [n, []]));
  if (!data) return found;
  const files = [];
  const managed = path.join(dir, data, 'Managed');
  for (const rel of listFiles(managed, { filter: (r) => /\.dll$/i.test(r) })) files.push(path.join(managed, rel));
  for (const extra of [path.join(dir, data, 'il2cpp_data', 'Metadata', 'global-metadata.dat'), path.join(dir, data, 'ScriptingAssemblies.json')]) {
    if (fs.existsSync(extra)) files.push(extra);
  }
  for (const f of files) {
    let buf;
    try { buf = fs.readFileSync(f); } catch { continue; }
    for (const n of needles) {
      if (buf.indexOf(Buffer.from(n, 'utf8')) >= 0 || path.basename(f).toLowerCase().startsWith(n.toLowerCase())) found.get(n).push(toPosix(path.relative(dir, f)));
    }
  }
  return found;
}

/**
 * Join a build with the ledger.
 * @param {{ws: string, unity: string, unitySubdir: string}} ctx
 * @param {Record<string,string>[]} rows every ledger row
 * @param {string} buildDir absolute
 * @returns {{ok: boolean, error?: string, identity: object, shipped: {path: string, source: 'packed'|'file', row: object|null}[],
 *   unlicensed: string[], toolBinaries: string[], warnings: string[], rowsInScope: Set<object>,
 *   rawEmbedded: {path: string, row: object}[], coplayInBuild: boolean,
 *   buildNotices: string|null, counts: object}}
 */
export function joinBuild(ctx, rows, buildDir) {
  const identity = buildIdentity(buildDir);
  const warnings = [];
  const packedFile = path.join(buildDir, 'packed_assets.json');
  if (!fs.existsSync(packedFile)) return { ok: false, error: `packed_assets.json is missing in ${relOrAbs(ctx.ws, buildDir)}: the BuildReportExport post-build step did not run (is com.gamestudiokit.core installed?)`, identity };
  let packed;
  try { packed = readJson(packedFile); } catch (e) { return { ok: false, error: e.message, identity }; }
  const pv = validate(licencesSchema(), packed, { pointer: '#/$defs/packedAssets' });
  if (!pv.valid) return { ok: false, error: `packed_assets.json is invalid: ${formatErrors(pv.errors)}`, identity };
  if (identity.development) warnings.push(`this is a development build (${identity.reasons.join(', ') || 'development flag'}): it carries the Unity Pipeline runtime and must never be uploaded`);

  const exact = new Map();
  const folders = [];
  const byBase = new Map();
  for (const r of rows) {
    const p = normRowPath(r.path);
    if (!p) continue;
    if (p.endsWith('/')) folders.push({ key: pathKey(p), row: r });
    else {
      exact.set(pathKey(p), r);
      const b = path.posix.basename(p).toLowerCase();
      if (!byBase.has(b)) byBase.set(b, []);
      byBase.get(b).push(r);
    }
  }
  folders.sort((a, b) => b.key.length - a.key.length);
  const sub = ctx.unitySubdir && ctx.unitySubdir !== '.' ? `${ctx.unitySubdir}/` : '';
  const shipped = [];
  const unlicensed = [];
  const toolBinaries = [];
  const rowsInScope = new Set();
  const counts = { packed_entries: 0, engine: 0, own: 0, code_unmatched: 0, authored_unmatched: 0, files: 0, il2cpp_assemblies: 0 };
  const seenPacked = new Set();
  const licenceCache = new Map();
  const isOwnRow = (r) => r.licence_id === 'Own-Work' || r.source_kind === 'own_work';

  for (const pa of packed.packed) {
    for (const c of pa.contents || []) {
      counts.packed_entries++;
      const src = toPosix(c.source_asset_path || '');
      if (seenPacked.has(src)) continue;
      seenPacked.add(src);
      const cls = classifyPacked(src);
      if (cls === 'engine') { counts.engine++; continue; }
      if (cls === 'own') { counts.own++; continue; }
      const wrel = `${sub}${src}`;
      const row = coveringRow(exact, folders, wrel) || (sub ? coveringRow(exact, folders, src) : null);
      if (isToolBinaryName(src)) toolBinaries.push(wrel);
      if (row) { rowsInScope.add(row); shipped.push({ path: wrel, source: 'packed', row }); continue; }
      if (cls === 'code') {
        // Scripts and shaders are the project's code unless a LICENSE/COPYING/NOTICE sits in their pack folder.
        const lic = licenceFileNear(ctx.unity, src, licenceCache);
        if (!lic) { counts.code_unmatched++; continue; }
        warnings.push(`${wrel}: third-party code (licence file ${sub}${lic}) needs a ledger row for its folder`);
      } else if (cls === 'authored') { counts.authored_unmatched++; continue; }
      shipped.push({ path: wrel, source: 'packed', row: null });
      unlicensed.push(wrel);
    }
  }

  // Assemblies from asmdefs: the project's own code, unless a licence file or a non-Own-Work row marks it third-party.
  const own = new Set();
  const thirdPartyAsm = new Map();
  for (const [n, a] of projectAssemblies(ctx.unity)) {
    const wrel = `${sub}${a.asmdef}`;
    const row = coveringRow(exact, folders, wrel);
    if (row ? isOwnRow(row) : !a.licenceFile) own.add(n);
    else thirdPartyAsm.set(n, { asmdef: wrel, row, licence: a.licenceFile ? `${sub}${a.licenceFile}` : null });
  }
  const data = dataFolder(buildDir);
  const info = { data, own };
  const buildRel = relOrAbs(ctx.ws, buildDir);
  let coplayInBuild = false;
  const rawEmbedded = [];
  const buildFiles = listFiles(buildDir, { skipDirs: [] });

  /** Ledger row for a build file that has no path mapping: its asmdef's row, else a same-name row. */
  const rowByName = (base, abs, shown) => {
    const asm = /\.dll$/i.test(base) ? thirdPartyAsm.get(base.toLowerCase().replace(/\.dll$/, '')) : null;
    if (asm?.row) return asm.row;
    const cands = byBase.get(base.toLowerCase()) || [];
    let hash = null;
    const shaOf = () => { if (hash === null) { try { hash = abs ? sha256File(abs) : ''; } catch { hash = ''; } } return hash; };
    const sameBytes = (r) => Boolean(r.sha256) && String(r.sha256).toLowerCase() === shaOf();
    // A compiled binary is matched by name (the row may describe the plugin's copy under Assets); any other loose
    // file only when its bytes are the ones the row was written for.
    const row = RAW_OK_EXT.test(base) ? (cands.length > 1 ? cands.find(sameBytes) : null) || cands[0] || null : cands.find(sameBytes) || null;
    if (!row && cands.length) warnings.push(`${shown}: no ledger row; ${cands.slice(0, 2).map((r) => normRowPath(r.path)).join(', ')} has the same name but other bytes (add a row for this file or its folder)`);
    if (!row && asm) warnings.push(`${shown}: third-party assembly ${asm.asmdef}${asm.licence ? ` (licence file ${asm.licence})` : ''} needs a ledger row for its folder`);
    return row;
  };
  const record = (shown, rel, row) => {
    if (row) {
      rowsInScope.add(row);
      shipped.push({ path: shown, source: 'file', row });
      // A loose copy of an embedded_only asset (Sonniss, Mixamo, QAL, Asset Store media) is a redistributed raw file.
      if (row.redistribution === 'embedded_only' && !RAW_OK_EXT.test(rel)) rawEmbedded.push({ path: shown, row });
    } else { shipped.push({ path: shown, source: 'file', row: null }); unlicensed.push(shown); }
  };

  const managedSeen = new Set();
  for (const rel of buildFiles) {
    counts.files++;
    if (data && rel.toLowerCase().startsWith(`${data.toLowerCase()}/managed/`)) managedSeen.add(path.posix.basename(rel).toLowerCase());
    const { cls, why } = classifyBuildFile(rel, info);
    const shown = `${buildRel}/${rel}`;
    if (cls === 'engine') { counts.engine++; continue; }
    if (cls === 'own') { counts.own++; continue; }
    if (cls === 'tool_binary') { toolBinaries.push(shown); continue; }
    if (cls === 'debug') {
      const m = /^(.*?_(?:BackUpThisFolder_ButDontShipItWithYourGame|BurstDebugInformation_DoNotShip))(\/|$)/i.exec(rel);
      warnings.push(`${m ? `${buildRel}/${m[1]}/` : shown}: ${why}; the depot VDF excludes it, but keep it out of the upload folder`);
      continue;
    }
    if (cls === 'steam_appid') { warnings.push(`${shown}: ${why}; the depot VDF excludes it and BuildGuardTests must fail on it`); continue; }
    if (cls === 'addressables') {
      const aa = rel.slice(0, rel.toLowerCase().indexOf('streamingassets/aa/') + 'streamingassets/aa/'.length);
      warnings.push(`${buildRel}/${aa}: ${why}; add ledger rows for its sources by hand`);
      continue;
    }
    let row = null;
    if (cls === 'streaming') {
      const inner = rel.slice(rel.toLowerCase().indexOf('streamingassets/') + 'streamingassets/'.length);
      row = coveringRow(exact, folders, `${sub}Assets/StreamingAssets/${inner}`);
    }
    if (cls === 'coplay') coplayInBuild = true;
    if (!row) row = rowByName(path.posix.basename(rel), path.join(buildDir, rel), shown);
    record(shown, rel, row);
  }

  // IL2CPP compiles every managed assembly into GameAssembly.dll; ScriptingAssemblies.json still names them.
  const sa = data ? readJsonOr(path.join(buildDir, data, 'ScriptingAssemblies.json'), null, { onInvalid: 'fallback' }) : null;
  for (const raw of Array.isArray(sa?.names) ? sa.names : []) {
    const base = path.posix.basename(toPosix(String(raw || '')));
    if (!base || managedSeen.has(base.toLowerCase())) continue;
    managedSeen.add(base.toLowerCase());
    counts.il2cpp_assemblies++;
    const dll = /\.dll$/i.test(base) ? base : `${base}.dll`;
    const { cls } = classifyBuildFile(`${data}/Managed/${dll}`, info);
    const shown = `${buildRel}/${data}/ScriptingAssemblies.json#${dll}`;
    if (cls === 'engine') { counts.engine++; continue; }
    if (cls === 'own') { counts.own++; continue; }
    if (cls === 'tool_binary') { toolBinaries.push(shown); continue; }
    if (cls === 'coplay') coplayInBuild = true;
    record(shown, dll, rowByName(dll, null, shown));
  }

  const hits = findInPlayer(buildDir, ['MCPForUnity']);
  if (hits.get('MCPForUnity').length) coplayInBuild = true;
  let buildNotices = null;
  try { buildNotices = readText(path.join(buildDir, 'THIRD_PARTY_NOTICES.txt')); } catch { buildNotices = null; }
  // Duplicates are noise: keep the first report of each path.
  const uniq = (a) => [...new Set(a)];
  return { ok: true, identity, shipped, unlicensed: uniq(unlicensed), toolBinaries: uniq(toolBinaries), warnings: uniq(warnings), rowsInScope, rawEmbedded, coplayInBuild, buildNotices, counts, packed };
}

/**
 * FAIL rows for loose copies of embedded_only assets found in the build folder (rule embedded_only_raw_file).
 * @param {{rawEmbedded?: {path: string, row: object}[]}} j a joinBuild result
 */
export function rawEmbeddedViolations(j) {
  return (j.rawEmbedded || []).map(({ path: p, row }) => ({
    rule: 'embedded_only_raw_file',
    path: p,
    severity: 'FAIL',
    message: `${normRowPath(row.path)} is embedded_only (${row.licence_id}) but ships as a loose, extractable file: import it as a Unity asset instead of copying it to StreamingAssets or the build folder`,
  }));
}

#!/usr/bin/env node
// render_lint.mjs: static checks for the game-render-world rules in a Unity URP project. Reads ProjectSettings, URP
// assets, renderer data, volume profiles, .shader/.hlsl files, materials, build scenes and runtime C#; writes nothing
// except the optional --out report. Installed as <W>/Tools/kit/render/render_lint.mjs (CONTRACTS §3.2 sub `render`); the
// same file also runs in place from the skill (`${CLAUDE_SKILL_DIR}/scripts/project/render/render_lint.mjs --root <U>`).
// Every rule answers a build-only or look failure that editor play mode hides:
//   urp-quality-builtin        FAIL  an enabled quality level (or the whole project) renders with the Built-in pipeline
//   urp-asset-unresolved       FAIL  a pipeline asset, renderer, default renderer index or volume profile points at nothing
//   urp-asset-unreferenced     WARN  a URP asset that is not the Graphics default nor on an enabled quality level (not in the build)
//   urp-srp-batcher-off        WARN  an in-build URP asset has the SRP Batcher off
//   urp-mixed-rendering-paths  WARN  in-build renderers use different rendering paths (two variant sets per keyword)
//   urp-texture-required       WARN  a shader samples the camera depth/opaque texture but an in-build asset does not make it
//   shader-legacy-pass         FAIL  a Built-in-only LightMode (ForwardBase, Always, Vertex…) or GrabPass in the SubShader URP runs
//   shader-duplicate-lightmode FAIL  two passes share one LightMode (untagged = SRPDefaultUnlit): URP draws only the first
//   shader-srp-batcher-cbuffer WARN  passes of one SubShader disagree on UnityPerMaterial (or UsePass mixes layouts)
//   shader-keyword-stripped    FAIL  C# enables a keyword declared only with shader_feature and no material/variant collection keeps it
//   fog-mode-stripped          WARN  C# turns fog on (or picks a fog mode) that the Graphics fog stripping removes from builds
//   volume-override-stripped   WARN  C# adds a post-processing override that no volume profile asset contains (variants stripped)
//   bake-lighting-missing      WARN  a build scene has Baked/Mixed lights but no baked lighting data
//   navmesh-unbaked            WARN  a NavMeshSurface in a build scene has no NavMesh data
// Suppress one source finding with a reason, on the same line or the line above:  // gsk-render-ignore <rule>[,<rule>]: <reason>
// Optional project config (never required): <W>/Docs/render/render_lint.json
//   {"schema":1, "exclude":["Assets/Vendor/**"], "keep_keywords":["_HIT_FLASH"],
//    "ignore":[{"rule":"urp-asset-unreferenced", "path":"Assets/Settings/Mobile_RPAsset.asset", "why":"mobile only"}]}
// Exit: 0 PASS or WARN only · 1 FAIL (or WARN with --strict) · 2 usage · 3 no Unity project / kit lib missing.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as Y from './lib/unity_yaml.mjs';
import * as SL from './lib/shaderlab.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
// Installed: Tools/kit/render → Tools/kit/lib. In the kit or ~/.claude/skills: skills/game-render-world/scripts/project/render
// → skills/game-studio-run/scripts/lib (install-user always installs all 13 skills, CONTRACTS §2).
const LIB = [path.join(HERE, '..', 'lib'), path.join(HERE, '..', '..', '..', '..', 'game-studio-run', 'scripts', 'lib')]
  .find((d) => fs.existsSync(path.join(d, 'cli.mjs')));
if (!LIB) {
  process.stderr.write('render_lint: the kit lib (Tools/kit/lib or skills/game-studio-run/scripts/lib) was not found\n');
  if (path.resolve(process.argv[1] || '') === fileURLToPath(import.meta.url)) process.exit(3);
  throw new Error('render_lint: kit lib not found');
}
const imp = (m) => import(pathToFileURL(path.join(LIB, m)).href);
const { EXIT, CliError, UsageError, emit, isMain, note, parseCli, runCli } = await imp('cli.mjs');
const { findWorkspace, kitPaths, matchesAny, relPosix, toPosix } = await imp('paths.mjs');
const { listFiles } = await imp('hash.mjs');
const { readText, writeTextAtomic } = await imp('jsonio.mjs');

export const RULES = Object.freeze({
  'urp-quality-builtin': 'FAIL',
  'urp-asset-unresolved': 'FAIL',
  'urp-asset-unreferenced': 'WARN',
  'urp-srp-batcher-off': 'WARN',
  'urp-mixed-rendering-paths': 'WARN',
  'urp-texture-required': 'WARN',
  'shader-legacy-pass': 'FAIL',
  'shader-duplicate-lightmode': 'FAIL',
  'shader-srp-batcher-cbuffer': 'WARN',
  'shader-keyword-stripped': 'FAIL',
  'fog-mode-stripped': 'WARN',
  'volume-override-stripped': 'WARN',
  'bake-lighting-missing': 'WARN',
  'navmesh-unbaked': 'WARN',
});
const RULE_IDS = Object.keys(RULES);

export const DEFAULT_EXCLUDES = Object.freeze([
  'Assets/Plugins/**',
  'Assets/ThirdParty/**',
  'Assets/Third Party/**',
  'Assets/AssetStoreTools/**',
  'Assets/TextMesh Pro/**',
  'Assets/Samples/**',
  'Assets/_AgentCaptures/**',
]);
const SKIP_DIRS = ['.git', 'node_modules', 'Library', 'Temp', 'Obj', 'obj', 'bin', 'Logs', 'Builds', 'UserSettings'];

// Script guids of URP types, stable across URP versions (read from a Unity 6000.3 / URP 17 project) [V].
export const GUIDS = Object.freeze({
  urpAsset: 'bf2edee5c58d82540a51f03df9d42094',
  urpRenderer: 'de640fe3d0db1804a85f9fc8f5cadab6',
  volumeProfile: 'd7fd9488000d3734a9e00ee676215985',
  volume: '172515602e62fb746b5d573b38a5fe58',
  urpGlobalSettings: '2ec995e51a6e251468d2a3fd8a686257',
});

// LightMode values URP does not draw. The first seven are the manual's list; ForwardBase and Deferred are Built-in
// pipeline passes that URP has no path for either.
export const LEGACY_LIGHTMODES = Object.freeze(['Always', 'ForwardAdd', 'PrepassBase', 'PrepassFinal', 'Vertex', 'VertexLMRGBM', 'VertexLM', 'ForwardBase', 'Deferred']);

// Volume overrides whose shader variants URP's "Strip Unused Post Processing Variants" can remove. Colour adjustments,
// curves, white balance and the other grading overrides bake into the colour LUT and have no variants of their own.
export const POST_VARIANT_OVERRIDES = Object.freeze(['Bloom', 'ChromaticAberration', 'LensDistortion', 'FilmGrain', 'Tonemapping', 'DepthOfField', 'MotionBlur', 'PaniniProjection', 'ScreenSpaceLensFlare']);

const RENDERING_MODES = { 0: 'Forward', 1: 'Deferred', 2: 'Forward+', 3: 'Deferred+' };
const FOG_MODES = { 1: 'Linear', 2: 'Exponential', 3: 'ExponentialSquared' };
const DEPTH_USE = /_CameraDepthTexture|\bSampleSceneDepth\b|\bLoadSceneDepth\b|DeclareDepthTexture\.hlsl/;
const OPAQUE_USE = /_CameraOpaqueTexture|\bSampleSceneColor\b|\bLoadSceneColor\b|DeclareOpaqueTexture\.hlsl/;
const HEAD_BYTES = 4096;
const MAX_ASSET_BYTES = 4 * 1024 * 1024;

// ------------------------------------------------------------------------------------------------------------ files

/** Head of a file as text, or null for a binary file. */
function head(abs) {
  let fd;
  try {
    fd = fs.openSync(abs, 'r');
    const buf = Buffer.alloc(HEAD_BYTES);
    const n = fs.readSync(fd, buf, 0, HEAD_BYTES, 0);
    const b = buf.subarray(0, n);
    if (b.includes(0)) return null;
    return b.toString('utf8');
  } catch {
    return null;
  } finally {
    if (fd !== undefined) fs.closeSync(fd);
  }
}

function safeRead(abs) {
  try {
    return readText(abs);
  } catch {
    return null;
  }
}

/** Blank C# comments (strings, verbatim strings and char literals kept), keeping line structure. */
export function stripCsComments(src) {
  const s = String(src).replace(/^﻿/, '').replace(/\r\n?/g, '\n');
  let out = '';
  let i = 0;
  while (i < s.length) {
    const c = s[i];
    const d = s[i + 1];
    if (c === '@' && d === '"') {
      let j = i + 2;
      while (j < s.length) {
        if (s[j] === '"' && s[j + 1] === '"') j += 2;
        else if (s[j] === '"') break;
        else j++;
      }
      out += s.slice(i, j + 1);
      i = j + 1;
      continue;
    }
    if (c === '"') {
      let j = i + 1;
      while (j < s.length && s[j] !== '"' && s[j] !== '\n') j += s[j] === '\\' ? 2 : 1;
      out += s.slice(i, j + 1);
      i = j + 1;
      continue;
    }
    if (c === "'") {
      const end = s.indexOf("'", i + (s[i + 1] === '\\' ? 3 : 2));
      const j = end >= 0 && end - i <= 8 ? end : i;
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

/** First 1-based line of `text` containing `needle`, else 1. */
function lineOfText(text, needle) {
  if (!text) return 1;
  const t = Y.normalise(text);
  const i = t.indexOf(needle);
  return i < 0 ? 1 : SL.lineOf(t, i);
}

/** True when a `gsk-render-ignore` comment on the line or the line above names the rule. */
function suppressedAt(rawLines, line, rule) {
  for (const l of [rawLines[line - 1], rawLines[line - 2]]) {
    const m = l && /gsk-render-ignore\s+([\w,\s-]+?)\s*:/.exec(l);
    if (m && m[1].split(',').map((s) => s.trim()).includes(rule)) return true;
  }
  return false;
}

// --------------------------------------------------------------------------------------------------------- project

/** Files under Assets and embedded packages, U-relative. */
function projectFiles(U) {
  const out = [];
  const roots = ['Assets'];
  try {
    for (const e of fs.readdirSync(path.join(U, 'Packages'), { withFileTypes: true })) if (e.isDirectory()) roots.push(`Packages/${e.name}`);
  } catch {
    /* no Packages folder */
  }
  for (const r of roots) for (const rel of listFiles(path.join(U, r), { skipDirs: SKIP_DIRS })) out.push(`${r}/${rel}`);
  return out;
}

function readConfig(file) {
  if (!file || !fs.existsSync(file)) return { exclude: [], keep_keywords: [], ignore: [] };
  let cfg;
  try {
    cfg = JSON.parse(readText(file));
  } catch (e) {
    throw new UsageError(`${file}: not valid JSON (${e.message})`);
  }
  const arr = (k) => (Array.isArray(cfg[k]) ? cfg[k].map(String) : []);
  const ignore = Array.isArray(cfg.ignore) ? cfg.ignore : [];
  for (const g of ignore) {
    if (!g || !RULE_IDS.includes(g.rule) || typeof g.path !== 'string' || !String(g.why || '').trim()) {
      throw new UsageError(`${file}: every ignore entry needs a known "rule", a "path" glob and a non-empty "why"`);
    }
  }
  return { exclude: arr('exclude'), keep_keywords: arr('keep_keywords'), ignore };
}

/** Index of the project: guid → path for .asset files, URP assets, renderers, profiles, global settings. */
function indexAssets(U, files) {
  const guidToPath = new Map();
  const urpAssets = new Map(); // path → {body, text}
  const renderers = new Map();
  const profiles = new Map(); // path → Set(component names)
  let globalSettings = null;
  for (const f of files) {
    if (!/\.asset$/i.test(f)) continue;
    const meta = safeRead(path.join(U, `${f}.meta`));
    const guid = meta ? Y.metaGuid(meta) : null;
    if (guid) guidToPath.set(guid, f);
    const h = head(path.join(U, f));
    if (!h || !/^--- !u!114 /m.test(h)) continue; // only ScriptableObject assets matter here
    let size = 0;
    try {
      size = fs.statSync(path.join(U, f)).size;
    } catch {
      continue;
    }
    if (size > MAX_ASSET_BYTES) continue;
    const text = safeRead(path.join(U, f));
    if (!text) continue;
    const docs = Y.splitDocs(text);
    const main = docs.find((d) => d.fileId === '11400000') ?? docs[0];
    const g = main ? Y.scriptGuid(main.body) : null;
    if (g === GUIDS.urpAsset || (main && Y.field(main.body, 'm_RendererDataList') !== null && Y.field(main.body, 'm_DefaultRendererIndex') !== null)) {
      urpAssets.set(f, { body: main.body, text });
    } else if (g === GUIDS.urpRenderer || (main && Y.field(main.body, 'm_RendererFeatures') !== null && Y.field(main.body, 'm_RenderingMode') !== null)) {
      renderers.set(f, { body: main.body, docs, text });
    } else if (g === GUIDS.volumeProfile || (main && Y.field(main.body, 'components') !== null && docs.some((d) => d.fileId !== main.fileId && Y.field(d.body, 'active') !== null))) {
      const names = new Set(docs.filter((d) => d !== main).map((d) => Y.field(d.body, 'm_Name')).filter(Boolean));
      profiles.set(f, names);
    } else if (g === GUIDS.urpGlobalSettings || /m_StripUnusedPostProcessingVariants:/.test(text)) {
      globalSettings = { path: f, text };
    }
  }
  return { guidToPath, urpAssets, renderers, profiles, globalSettings };
}

// ------------------------------------------------------------------------------------------------------ the linter

/**
 * Lint a Unity project.
 * @param {{U: string, platform?: string, rules?: string[], cfg?: object, excludes?: string[], scenes?: string[]}} o
 */
export function lintProject(o) {
  const U = o.U;
  const platform = o.platform || 'Standalone';
  const rules = new Set(o.rules ?? RULE_IDS);
  const cfg = o.cfg ?? { exclude: [], keep_keywords: [], ignore: [] };
  const excludes = [...DEFAULT_EXCLUDES, ...(cfg.exclude ?? []), ...(o.excludes ?? [])];
  const violations = [];
  const skipped = [];
  let suppressed = 0;
  let ignored = 0;
  const rawCache = new Map();
  const rawLines = (f) => {
    if (!rawCache.has(f)) rawCache.set(f, (safeRead(path.join(U, f)) ?? '').replace(/\r\n?/g, '\n').split('\n'));
    return rawCache.get(f);
  };
  /** Record a finding (source findings honour gsk-render-ignore comments; every finding honours config ignores). */
  const add = (rule, file, line, message, { source = false } = {}) => {
    if (!rules.has(rule)) return;
    if (source && suppressedAt(rawLines(file), line, rule)) {
      suppressed++;
      return;
    }
    if (cfg.ignore.some((g) => g.rule === rule && matchesAny(file, [g.path]))) {
      ignored++;
      return;
    }
    violations.push({ rule, severity: RULES[rule], file, line, message });
  };

  const files = projectFiles(U);
  const excluded = (f) => matchesAny(f, excludes);
  const idx = indexAssets(U, files);
  const resolve = (ref) => (ref && ref.guid ? idx.guidToPath.get(ref.guid) ?? null : null);

  // -- ProjectSettings
  const gsText = safeRead(path.join(U, 'ProjectSettings', 'GraphicsSettings.asset'));
  const qsText = safeRead(path.join(U, 'ProjectSettings', 'QualitySettings.asset'));
  const bsText = safeRead(path.join(U, 'ProjectSettings', 'EditorBuildSettings.asset'));
  const gs = gsText ? Y.splitDocs(gsText).find((d) => d.type === 'GraphicsSettings')?.body ?? '' : '';
  const qs = qsText ? Y.splitDocs(qsText).find((d) => d.type === 'QualitySettings')?.body ?? '' : '';
  const bs = bsText ? Y.splitDocs(bsText).find((d) => d.type === 'EditorBuildSettings')?.body ?? '' : '';
  const defaultRef = Y.parseRef(Y.field(gs, 'm_CustomRenderPipeline'));
  const levels = Y.mapSeq(qs, 'm_QualitySettings').map((item, index) => ({
    index,
    name: Y.field(item, 'name') ?? `#${index}`,
    ref: Y.parseRef(Y.field(item, 'customRenderPipeline')),
    excluded: (Y.seq(item, 'excludedTargetPlatforms') ?? []).map((s) => s.trim()),
  }));
  const enabled = levels.filter((l) => !l.excluded.includes(platform));
  const assigned = !Y.isNullRef(defaultRef) || enabled.some((l) => !Y.isNullRef(l.ref));
  const pipeline = !gsText ? 'unknown' : assigned ? 'urp' : 'builtin';

  const summary = {
    graphics_default: Y.isNullRef(defaultRef) ? null : resolve(defaultRef) ?? `guid:${defaultRef.guid}`,
    quality_levels: [],
    urp_assets: [],
    scenes: [],
    shaders_scanned: 0,
    shader_parse_errors: [],
    cs_scanned: 0,
    materials_scanned: 0,
  };

  for (const l of levels) {
    const own = Y.isNullRef(l.ref) ? null : resolve(l.ref) ?? `guid:${l.ref.guid}`;
    summary.quality_levels.push({ index: l.index, name: l.name, enabled_for_platform: !l.excluded.includes(platform), asset: own, effective_asset: own ?? summary.graphics_default });
  }
  // Assets referenced by the default or by any quality level on any platform (an asset for another platform is not stray).
  const referenced = new Set([defaultRef, ...levels.map((l) => l.ref)].filter((r) => !Y.isNullRef(r)).map(resolve).filter(Boolean));

  // -- URP assets in the build (Graphics default + quality levels enabled for the platform)
  const inBuild = new Set();
  if (pipeline === 'unknown') {
    for (const r of RULE_IDS.filter((r) => r.startsWith('urp-'))) skipped.push({ rule: r, reason: 'ProjectSettings/GraphicsSettings.asset not found' });
  } else if (pipeline === 'builtin') {
    if (idx.urpAssets.size > 0) {
      add('urp-quality-builtin', 'ProjectSettings/GraphicsSettings.asset', lineOfText(gsText, 'm_CustomRenderPipeline'),
        `the project has ${idx.urpAssets.size} URP asset(s) but neither the Graphics default nor any quality level enabled for ${platform} uses one, so the build renders with the Built-in pipeline and URP materials turn magenta`);
    }
    for (const r of RULE_IDS.filter((r) => r.startsWith('urp-') && r !== 'urp-quality-builtin')) skipped.push({ rule: r, reason: 'Built-in Render Pipeline project' });
  } else {
    if (!Y.isNullRef(defaultRef)) {
      const p = resolve(defaultRef);
      if (p) inBuild.add(p);
      else add('urp-asset-unresolved', 'ProjectSettings/GraphicsSettings.asset', lineOfText(gsText, 'm_CustomRenderPipeline'), `the Graphics default render pipeline (guid ${defaultRef.guid}) is not an asset in Assets or an embedded package`);
    }
    for (const l of levels) {
      if (l.excluded.includes(platform)) continue;
      const own = Y.isNullRef(l.ref) ? null : resolve(l.ref);
      const line = lineOfText(qsText, `name: ${l.name}`);
      if (!Y.isNullRef(l.ref) && !own) add('urp-asset-unresolved', 'ProjectSettings/QualitySettings.asset', line, `quality level "${l.name}" uses a render pipeline asset (guid ${l.ref.guid}) that is not in the project`);
      else if (own) inBuild.add(own);
      else if (Y.isNullRef(l.ref) && Y.isNullRef(defaultRef)) {
        add('urp-quality-builtin', 'ProjectSettings/QualitySettings.asset', line, `quality level "${l.name}" has no render pipeline asset and the Graphics default is empty, so it renders with the Built-in pipeline (URP materials turn magenta at that level)`);
      }
    }
  }

  // -- per URP asset
  const renderingModes = new Map(); // mode → [asset]
  for (const [f, a] of [...idx.urpAssets].sort((x, y) => x[0].localeCompare(y[0]))) {
    const b = a.body;
    const built = inBuild.has(f);
    const rendererRefs = (Y.seq(b, 'm_RendererDataList') ?? []).map((s) => Y.parseRef(s));
    const defIndex = Y.num(b, 'm_DefaultRendererIndex') ?? 0;
    const rs = rendererRefs.map((r) => {
      const p = Y.isNullRef(r) ? null : resolve(r);
      const rd = p ? idx.renderers.get(p) : null;
      const mode = rd ? Y.num(rd.body, 'm_RenderingMode') : null;
      const features = rd
        ? (Y.seq(rd.body, 'm_RendererFeatures') ?? []).map((s) => Y.parseRef(s)).map((ref) => rd.docs.find((d) => ref && d.fileId === ref.fileID)).filter(Boolean)
          .map((d) => ({ name: Y.field(d.body, 'm_Name') ?? '', active: Y.num(d.body, 'm_Active') !== 0 }))
        : [];
      return { ref: r, path: p, rendering_mode: mode === null ? null : RENDERING_MODES[mode] ?? String(mode), features };
    });
    const profileRef = Y.parseRef(Y.field(b, 'm_VolumeProfile'));
    const profile = Y.isNullRef(profileRef) ? null : resolve(profileRef);
    summary.urp_assets.push({
      path: f,
      in_build: built,
      renderers: rs.map((r) => ({ path: r.path, rendering_mode: r.rendering_mode, features: r.features })),
      default_renderer_index: defIndex,
      srp_batcher: Y.num(b, 'm_UseSRPBatcher') !== 0,
      hdr: Y.num(b, 'm_SupportsHDR') === 1,
      msaa: Y.num(b, 'm_MSAA'),
      render_scale: Y.num(b, 'm_RenderScale'),
      depth_texture: Y.num(b, 'm_RequireDepthTexture') === 1,
      opaque_texture: Y.num(b, 'm_RequireOpaqueTexture') === 1,
      shadow_distance: Y.num(b, 'm_ShadowDistance'),
      light_probe_system: Y.num(b, 'm_LightProbeSystem') === 1 ? 'probe-volumes' : 'light-probe-groups',
      gpu_resident_drawer: (Y.num(b, 'm_GPUResidentDrawerMode') ?? 0) !== 0,
      volume_profile: profile,
    });
    if (!built) {
      if (pipeline === 'urp' && !referenced.has(f)) {
        add('urp-asset-unreferenced', f, 1, 'neither the Graphics default nor on any quality level: it is in no build, edits to it change nothing a player sees, and assigning it at runtime can hit variants URP stripped (URP strips per the assets in the build)');
      }
      continue;
    }
    rs.forEach((r, i) => {
      const line = lineOfText(a.text, 'm_RendererDataList');
      if (Y.isNullRef(r.ref)) add('urp-asset-unresolved', f, line, `m_RendererDataList[${i}] is empty`);
      else if (!r.path) add('urp-asset-unresolved', f, line, `m_RendererDataList[${i}] (guid ${r.ref.guid}) is not in the project`);
      else if (r.rendering_mode !== null) {
        if (!renderingModes.has(r.rendering_mode)) renderingModes.set(r.rendering_mode, []);
        renderingModes.get(r.rendering_mode).push(r.path);
      }
    });
    if (defIndex < 0 || defIndex >= rendererRefs.length) add('urp-asset-unresolved', f, lineOfText(a.text, 'm_DefaultRendererIndex'), `m_DefaultRendererIndex ${defIndex} is outside the renderer list (${rendererRefs.length} entries)`);
    if (!Y.isNullRef(profileRef) && !profile) add('urp-asset-unresolved', f, lineOfText(a.text, 'm_VolumeProfile'), `m_VolumeProfile (guid ${profileRef.guid}) is not in the project`);
    if (Y.num(b, 'm_UseSRPBatcher') === 0) add('urp-srp-batcher-off', f, lineOfText(a.text, 'm_UseSRPBatcher'), 'the SRP Batcher is off: every material switch costs a full set-up; turn it on unless a profiler capture says otherwise');
  }
  if (renderingModes.size > 1) {
    const list = [...renderingModes].map(([m, ps]) => `${m} (${[...new Set(ps)].join(', ')})`).join('; ');
    const first = [...inBuild].sort()[0] ?? 'ProjectSettings/QualitySettings.asset';
    add('urp-mixed-rendering-paths', first, 1, `in-build renderers use different rendering paths: ${list}; Unity then compiles two variant sets per keyword (longer builds, bigger player)`);
  }

  // -- shaders
  const shaderFiles = files.filter((f) => /\.shader$/i.test(f));
  const codeFiles = files.filter((f) => /\.(shader|hlsl|cginc|compute)$/i.test(f));
  const textCache = new Map();
  const text = (f) => {
    if (!textCache.has(f)) textCache.set(f, safeRead(path.join(U, f)));
    return textCache.get(f);
  };
  /** Code with project-local includes inlined; unresolved include targets are collected. */
  const expand = (code, fromFile, seen = new Set(), depth = 0, unresolved = new Set()) => {
    let out = code;
    for (const inc of SL.includeTargets(code)) {
      let rel = null;
      const t = inc.path.replace(/\\/g, '/');
      if (/^(Assets|Packages)\//.test(t)) rel = fs.existsSync(path.join(U, t)) ? t : null;
      else {
        const cand = path.posix.normalize(path.posix.join(path.posix.dirname(fromFile), t));
        rel = !cand.startsWith('..') && fs.existsSync(path.join(U, cand)) ? cand : null;
      }
      if (!rel) {
        unresolved.add(t);
        continue;
      }
      if (seen.has(rel) || depth > 8) continue;
      seen.add(rel);
      const inner = text(rel);
      if (inner) out += '\n' + expand(SL.stripComments(inner), rel, seen, depth + 1, unresolved).code;
    }
    return { code: out, unresolved };
  };

  // keyword declarations across every shader source (excluded folders count: they still declare keywords)
  const kwDecl = new Map(); // keyword → {kinds:Set, files:Set}
  for (const f of codeFiles) {
    const t = text(f);
    if (!t) continue;
    for (const p of SL.keywordPragmas(SL.stripComments(t))) {
      for (const k of p.keywords) {
        if (!kwDecl.has(k)) kwDecl.set(k, { kinds: new Set(), files: new Set() });
        kwDecl.get(k).kinds.add(p.kind);
        kwDecl.get(k).files.add(f);
      }
    }
  }

  const urpShaderRules = pipeline !== 'builtin';
  if (!urpShaderRules) {
    for (const r of ['shader-legacy-pass', 'shader-duplicate-lightmode', 'shader-srp-batcher-cbuffer']) skipped.push({ rule: r, reason: 'Built-in Render Pipeline project' });
  }
  const inBuildAssets = summary.urp_assets.filter((a) => a.in_build);
  for (const f of shaderFiles) {
    if (excluded(f)) continue;
    const t = text(f);
    if (!t) continue;
    summary.shaders_scanned++;
    const sh = SL.parseShader(t);
    if (!sh) {
      summary.shader_parse_errors.push(f);
      continue;
    }
    const whole = expand(sh.text, f).code;
    if (pipeline === 'urp') {
      for (const [kind, re, key] of [['depth', DEPTH_USE, 'depth_texture'], ['opaque', OPAQUE_USE, 'opaque_texture']]) {
        const m = re.exec(whole);
        if (!m) continue;
        const off = inBuildAssets.filter((a) => !a[key]).map((a) => a.path);
        if (off.length) {
          const at = sh.text.search(re);
          add('urp-texture-required', f, at >= 0 ? SL.lineOf(sh.text, at) : 1,
            `samples the camera ${kind} texture (${m[0]}) but ${off.join(', ')} ha${off.length > 1 ? 've' : 's'} ${kind === 'depth' ? 'Depth Texture' : 'Opaque Texture'} off: cameras on that asset read black or stale data unless the camera overrides it`, { source: true });
        }
      }
    }
    if (!urpShaderRules) continue;
    const sub = SL.urpSubShader(sh);
    if (!sub) continue;
    const seenModes = new Map();
    for (const p of sub.passes) {
      if (p.kind === 'GrabPass') {
        add('shader-legacy-pass', f, p.line, 'GrabPass is Built-in only: URP ignores it; sample _CameraOpaqueTexture (Opaque Texture on) instead', { source: true });
        continue;
      }
      if (p.kind !== 'Pass') continue;
      const lm = p.tags.LightMode;
      if (lm && LEGACY_LIGHTMODES.includes(lm)) {
        add('shader-legacy-pass', f, p.line, `pass${p.name ? ` "${p.name}"` : ''} uses LightMode "${lm}", which URP does not draw; use UniversalForward for the lit pass and SRPDefaultUnlit (or no tag) for one extra pass`, { source: true });
        continue;
      }
      const eff = lm || 'SRPDefaultUnlit';
      if (seenModes.has(eff)) {
        const first = seenModes.get(eff);
        add('shader-duplicate-lightmode', f, p.line, `pass${p.name ? ` "${p.name}"` : ''} has LightMode ${eff}${lm ? '' : ' (untagged)'} like the pass at line ${first.line}: URP draws only the first pass per LightMode, so this one never renders; give each extra pass its own LightMode (UniversalForward + SRPDefaultUnlit), or draw it with a renderer feature`, { source: true });
      } else seenModes.set(eff, p);
    }
    // SRP Batcher: one UnityPerMaterial layout for every pass of the SubShader
    const shared = [...sh.includes, ...sub.includes].map((b) => b.code).join('\n');
    const sigs = [];
    for (const p of sub.passes) {
      if (p.kind !== 'Pass') continue;
      const { code, unresolved } = expand(shared + '\n' + p.programs.map((b) => b.code).join('\n'), f);
      const decls = SL.perMaterialDecls(code);
      const inputs = [...unresolved].filter((u) => /Input\.hlsl$/i.test(u)).sort();
      sigs.push({ p, sig: `${decls ? decls.join(';') : 'none'}|${inputs.join(',')}`, declares: !!decls || inputs.length > 0 });
    }
    const usePasses = sub.passes.filter((p) => p.kind === 'UsePass');
    if (sigs.some((s) => s.declares)) {
      const distinct = new Set(sigs.map((s) => s.sig));
      if (distinct.size > 1) {
        const odd = sigs.filter((s) => s.sig !== sigs[0].sig).map((s) => `${s.p.name ? `"${s.p.name}"` : 'pass'} at line ${s.p.line}`);
        add('shader-srp-batcher-cbuffer', f, sub.line, `passes declare different UnityPerMaterial buffers (${odd.join(', ')} differ from the first pass): the SRP Batcher rejects the shader; put one CBUFFER_START(UnityPerMaterial) in an HLSLINCLUDE shared by every pass`, { source: true });
      }
      for (const u of usePasses) add('shader-srp-batcher-cbuffer', f, u.line, `UsePass "${u.usePass}" brings another shader's UnityPerMaterial layout into this SubShader, so the SRP Batcher rejects it; write the pass here with the shared HLSLINCLUDE instead`, { source: true });
    }
  }

  // -- materials and variant collections that keep shader_feature keywords alive
  const matKeywords = new Set();
  for (const f of files.filter((x) => /\.(mat|shadervariants)$/i.test(x))) {
    const t = text(f);
    if (!t) continue;
    if (/\.mat$/i.test(f)) {
      summary.materials_scanned++;
      const docs = Y.splitDocs(t);
      for (const d of docs) {
        for (const k of Y.seq(d.body, 'm_ValidKeywords') ?? []) matKeywords.add(k.trim());
        const legacy = Y.field(d.body, 'm_ShaderKeywords');
        if (legacy) for (const k of legacy.split(/\s+/)) if (k) matKeywords.add(k);
      }
    } else {
      for (const m of Y.normalise(t).matchAll(/^[ \t]*(?:-[ \t]+)?keywords:[ \t]*(.*)$/gm)) for (const k of m[1].split(/\s+/)) if (k) matKeywords.add(k);
    }
  }
  const keep = new Set(cfg.keep_keywords ?? []);

  // -- runtime C#
  const csFiles = files.filter((f) => /\.cs$/i.test(f) && !excluded(f) && !/(^|\/)Editor\//.test(f));
  const fogSites = []; // {file, line, mode|null}
  const kwUses = [
    /\.\s*EnableKeyword\s*\(\s*"([A-Za-z_]\w*)"/g,
    /\.\s*EnableShaderKeyword\s*\(\s*"([A-Za-z_]\w*)"/g,
    /\bnew\s+(?:UnityEngine\.Rendering\.)?LocalKeyword\s*\([^,()]+,\s*"([A-Za-z_]\w*)"\s*\)/g,
    /\bnew\s+(?:UnityEngine\.Rendering\.)?GlobalKeyword\s*\(\s*"([A-Za-z_]\w*)"\s*\)/g,
    /\bGlobalKeyword\s*\.\s*Create\s*\(\s*"([A-Za-z_]\w*)"\s*\)/g,
    /\bSetKeyword\s*\(\s*[^,()]+,\s*"([A-Za-z_]\w*)"/g,
  ];
  const profileNames = new Set([...idx.profiles.values()].flatMap((s) => [...s]));
  const stripPost = idx.globalSettings ? /m_StripUnusedPostProcessingVariants:\s*1/.test(idx.globalSettings.text) : true;
  for (const f of csFiles) {
    const raw = text(f);
    if (!raw) continue;
    summary.cs_scanned++;
    const code = stripCsComments(raw);
    const seenKw = new Set();
    for (const re of kwUses) {
      for (const m of code.matchAll(re)) {
        const k = m[1];
        const line = SL.lineOf(code, m.index);
        const key = `${line}:${k}`;
        if (seenKw.has(key)) continue;
        seenKw.add(key);
        const d = kwDecl.get(k);
        if (!d || d.kinds.has('multi_compile') || d.kinds.has('dynamic_branch')) continue;
        if (matKeywords.has(k) || keep.has(k)) continue;
        add('shader-keyword-stripped', f, line,
          `enables "${k}", declared only with shader_feature in ${[...d.files].sort().join(', ')}, and no material or shader variant collection has it on: the build strips that variant, so the effect silently does nothing (or renders wrong) in the player; declare it with multi_compile_local or dynamic_branch_local, or keep a material with it on in the build`, { source: true });
      }
    }
    for (const m of code.matchAll(/\bRenderSettings\s*\.\s*fog\s*=\s*true\b/g)) fogSites.push({ file: f, line: SL.lineOf(code, m.index), mode: null });
    for (const m of code.matchAll(/\bRenderSettings\s*\.\s*fogMode\s*=\s*(?:UnityEngine\.)?FogMode\s*\.\s*(Linear|ExponentialSquared|Exponential)\b/g)) fogSites.push({ file: f, line: SL.lineOf(code, m.index), mode: m[1] });
    if (stripPost) {
      for (const m of code.matchAll(/(?:\.\s*Add|\bCreateInstance)\s*<\s*(?:[\w.]+\.)?(\w+)\s*>\s*\(/g)) {
        if (!POST_VARIANT_OVERRIDES.includes(m[1]) || profileNames.has(m[1])) continue;
        add('volume-override-stripped', f, SL.lineOf(code, m.index),
          `adds a ${m[1]} override at runtime, but no volume profile asset contains ${m[1]} and "Strip Unused Post Processing Variants" is on: its shader variants are stripped and the effect never shows in the player; add ${m[1]} to the shared profile (intensity 0 or inactive is enough) or turn that stripping off`, { source: true });
      }
    }
  }

  // -- build scenes
  const buildScenes = [];
  for (const item of Y.mapSeq(bs, 'm_Scenes')) {
    if (Y.num(item, 'enabled') === 0) continue;
    const p = Y.field(item, 'path');
    if (p) buildScenes.push(p);
  }
  for (const s of o.scenes ?? []) if (!buildScenes.includes(s)) buildScenes.push(s);
  const sceneFog = [];
  for (const s of buildScenes) {
    const t = safeRead(path.join(U, s));
    if (t === null) {
      summary.scenes.push({ path: s, missing: true });
      continue;
    }
    const docs = Y.splitDocs(t);
    const rset = docs.find((d) => d.type === 'RenderSettings');
    const fogOn = rset ? Y.num(rset.body, 'm_Fog') === 1 : false;
    const fogMode = rset ? FOG_MODES[Y.num(rset.body, 'm_FogMode')] ?? null : null;
    sceneFog.push({ scene: s, on: fogOn, mode: fogMode });
    const lm = docs.find((d) => d.type === 'LightmapSettings');
    const ld = lm ? Y.parseRef(Y.field(lm.body, 'm_LightingDataAsset')) : null;
    const hasData = !!ld && !Y.isNullRef(ld) && !Y.isBuiltinRef(ld);
    const lights = docs.filter((d) => d.type === 'Light');
    const bakedLights = lights.filter((d) => [1, 2].includes(Y.num(d.body, 'm_Lightmapping')));
    const volumes = docs.filter((d) => d.type === 'MonoBehaviour' && Y.num(d.body, 'm_IsGlobal') === 1 && Y.field(d.body, 'sharedProfile') !== null)
      .map((d) => Y.parseRef(Y.field(d.body, 'sharedProfile'))).filter((r) => !Y.isNullRef(r)).map((r) => resolve(r) ?? `guid:${r.guid}`);
    const surfaces = docs.filter((d) => d.type === 'MonoBehaviour' && Y.field(d.body, 'm_NavMeshData') !== null && Y.field(d.body, 'm_AgentTypeID') !== null);
    summary.scenes.push({ path: s, fog: fogOn ? fogMode : null, lighting_data: hasData, baked_lights: bakedLights.length, global_volume_profiles: [...new Set(volumes)], navmesh_surfaces: surfaces.length });
    if (bakedLights.length && !hasData) {
      add('bake-lighting-missing', s, lm ? lm.line : 1, `${bakedLights.length} Baked/Mixed light(s) but no baked lighting data: the bake never ran or was cleared, so those lights add nothing (Baked) or fall back (Mixed) in the player; bake on the GPU desktop and commit the output`);
    }
    for (const d of surfaces) {
      if (Y.isNullRef(Y.parseRef(Y.field(d.body, 'm_NavMeshData')))) {
        add('navmesh-unbaked', s, d.line, 'a NavMeshSurface has no NavMesh data: bots and path queries find no path; bake it after the walkable geometry is final, and again after every change to it');
      }
    }
  }

  // -- fog stripping
  if (fogSites.length) {
    const stripping = Y.num(gs, 'm_FogStripping') ?? 0;
    const kept = new Set();
    if (stripping === 1) {
      if (Y.num(gs, 'm_FogKeepLinear') === 1) kept.add('Linear');
      if (Y.num(gs, 'm_FogKeepExp') === 1) kept.add('Exponential');
      if (Y.num(gs, 'm_FogKeepExp2') === 1) kept.add('ExponentialSquared');
    } else for (const s of sceneFog) if (s.on && s.mode) kept.add(s.mode);
    const setModes = fogSites.filter((x) => x.mode).map((x) => x.mode);
    const implied = setModes.length ? setModes : sceneFog.map((s) => s.mode).filter(Boolean);
    const needSites = fogSites.filter((x) => x.mode ? !kept.has(x.mode) : implied.length === 0 || implied.some((m) => !kept.has(m)));
    const how = stripping === 1 ? 'Graphics > Shader Stripping > Fog Modes is Custom and does not keep it' : `Fog Modes is Automatic and no build scene has that fog on (${buildScenes.length ? buildScenes.length + ' scene(s) checked' : 'the build scene list is empty'})`;
    for (const x of needSites) {
      add('fog-mode-stripped', x.file, x.line, `turns fog on${x.mode ? ` in ${x.mode} mode` : ''} at runtime, but ${how}: the fog variants are stripped and the player shows no fog; set Fog Modes to Custom and keep the modes you use, or turn that fog on in the first build scene`, { source: true });
    }
  }

  violations.sort((a, b) => (a.file < b.file ? -1 : a.file > b.file ? 1 : a.line - b.line || a.rule.localeCompare(b.rule)));
  return { pipeline, platform, violations, skipped, suppressed, ignored, summary };
}

// ------------------------------------------------------------------------------------------------------------- CLI

export async function main(argv) {
  const { values } = parseCli(argv, {
    root: { type: 'string' },
    rule: { type: 'string', multiple: true },
    platform: { type: 'string' },
    config: { type: 'string' },
    exclude: { type: 'string', multiple: true },
    scene: { type: 'string', multiple: true },
    strict: { type: 'boolean', default: false },
    out: { type: 'string' },
    'list-rules': { type: 'boolean', default: false },
  });
  if (values['list-rules']) {
    const rows = RULE_IDS.map((r) => ({ rule: r, severity: RULES[r] }));
    emit({ ok: true, rules: rows }, { json: values.json, text: rows.map((r) => `${r.severity.padEnd(4)} ${r.rule}`) });
    return EXIT.OK;
  }
  const picked = values.rule?.length ? values.rule.flatMap((r) => r.split(',')).map((r) => r.trim()).filter(Boolean) : [...RULE_IDS];
  const unknown = picked.filter((r) => !RULE_IDS.includes(r));
  if (unknown.length) throw new UsageError(`unknown rule id(s): ${unknown.join(', ')} (known: ${RULE_IDS.join(', ')})`);

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
  const cfgFile = values.config ? path.resolve(values.config) : W ? path.join(W, 'Docs', 'render', 'render_lint.json') : null;
  if (values.config && !fs.existsSync(cfgFile)) throw new UsageError(`--config ${values.config} does not exist`);
  const cfg = readConfig(cfgFile);
  note(`render_lint: ${U}`);
  const res = lintProject({ U, platform: values.platform, rules: picked, cfg, excludes: values.exclude ?? [], scenes: (values.scene ?? []).map((s) => toPosix(s)) });

  const base = W ?? U;
  const rebase = (f) => {
    if (!f || f.startsWith('guid:')) return f;
    const r = relPosix(base, path.join(U, f));
    return r.startsWith('..') ? toPosix(f) : r;
  };
  const violations = res.violations.map((v) => ({ ...v, file: rebase(v.file) }));
  const fails = violations.filter((v) => v.severity === 'FAIL').length;
  const warns = violations.length - fails;
  const verdict = fails ? 'FAIL' : warns ? 'WARN' : 'PASS';
  const failed = fails > 0 || (values.strict && warns > 0);
  const s = res.summary;
  const summary = {
    ...s,
    graphics_default: rebase(s.graphics_default),
    quality_levels: s.quality_levels.map((q) => ({ ...q, asset: rebase(q.asset), effective_asset: rebase(q.effective_asset) })),
    urp_assets: s.urp_assets.map((a) => ({ ...a, path: rebase(a.path), volume_profile: rebase(a.volume_profile), renderers: a.renderers.map((r) => ({ ...r, path: rebase(r.path) })) })),
    scenes: s.scenes.map((x) => ({ ...x, path: rebase(x.path), global_volume_profiles: (x.global_volume_profiles ?? []).map(rebase) })),
    shader_parse_errors: s.shader_parse_errors.map(rebase),
  };
  const result = {
    ok: !failed,
    exit: failed ? EXIT.FAIL : EXIT.OK,
    verdict,
    root: W ? relPosix(W, U) || '.' : '.',
    platform: res.platform,
    pipeline: res.pipeline,
    rules: picked,
    skipped_rules: res.skipped.filter((x) => picked.includes(x.rule)),
    counts: { fail: fails, warn: warns },
    suppressed: res.suppressed,
    ignored: res.ignored,
    summary,
    violations,
  };
  if (values.out) {
    const out = path.resolve(values.out);
    fs.mkdirSync(path.dirname(out), { recursive: true });
    writeTextAtomic(out, JSON.stringify(result, null, 2) + '\n');
  }
  const live = summary.quality_levels.filter((q) => q.enabled_for_platform).map((q) => `  quality "${q.name}" -> ${q.effective_asset ?? 'Built-in'}`);
  const text = [
    ...violations.map((v) => `${v.file}:${v.line}: [${v.severity} ${v.rule}] ${v.message}`),
    ...result.skipped_rules.map((x) => `skipped ${x.rule}: ${x.reason}`),
    `pipeline: ${res.pipeline} (${res.platform})`,
    ...live,
    `${verdict}: ${fails} fail, ${warns} warn; ${s.shaders_scanned} shader(s), ${s.cs_scanned} C# file(s), ${s.materials_scanned} material(s), ${s.scenes.length} build scene(s); ${res.suppressed} suppressed, ${res.ignored} ignored`,
  ];
  emit(result, { json: values.json, text });
  return result.exit;
}

if (isMain(import.meta.url)) await runCli(main);

// The source → project install map (CONTRACTS.md §3). The installer copies by this map; unit tests stage a project tree
// with stageKitTree() and run scripts from the staged copy, because project scripts import `../lib/*.mjs` (installed layout).
import fs from 'node:fs';
import path from 'node:path';
import { listFiles } from './hash.mjs';
import { normalizeUnitySubdir } from './paths.mjs';
import { utcIso } from './time.mjs';

/** The 13 kit skills (ARCHITECTURE §3 plus game-render-world from OWNER_DEFAULTS C-17). */
export const SKILLS = Object.freeze([
  'game-studio-run',
  'game-design-method',
  'unity-verify-ladder',
  'unity-dev-practice',
  'game-multiplayer',
  'game-asset-generation',
  'game-character-pipeline',
  'blender-rig-animate',
  'game-audio',
  'game-capture-trailer',
  'game-qa-probes',
  'game-ship',
  'game-render-world',
]);

/**
 * Project-side folders: `skills/<skill>/scripts/project/<sub>/**` → `<root>/Tools/kit/<sub>/**`.
 * root 'W' = workspace, 'U' = Unity project. Additional subs from a skill need a CONTRACTS.md change.
 */
export const PROJECT_DIRS = Object.freeze([
  { skill: 'game-studio-run', sub: 'hooks', root: 'W' },
  { skill: 'game-studio-run', sub: 'harness', root: 'W' },
  { skill: 'game-studio-run', sub: 'host', root: 'W' },
  { skill: 'game-studio-run', sub: 'bin', root: 'W' },
  { skill: 'unity-verify-ladder', sub: 'gate', root: 'U' },
  { skill: 'unity-dev-practice', sub: 'dev', root: 'W' },
  { skill: 'game-multiplayer', sub: 'net', root: 'W' },
  { skill: 'game-asset-generation', sub: 'assetgen', root: 'W' },
  { skill: 'game-character-pipeline', sub: 'anim', root: 'W' },
  { skill: 'blender-rig-animate', sub: 'blender', root: 'W' },
  { skill: 'game-audio', sub: 'audio', root: 'W' },
  { skill: 'game-capture-trailer', sub: 'capture', root: 'W' },
  { skill: 'game-qa-probes', sub: 'qa', root: 'W' },
  { skill: 'game-ship', sub: 'ship', root: 'W' },
  { skill: 'game-render-world', sub: 'render', root: 'W' },
]);

/** Embedded UPM package name and the folder → assembly map (ARCHITECTURE §4.4). */
export const PACKAGE_NAME = 'com.gamestudiokit.core';
export const PACKAGE_ASSEMBLIES = Object.freeze({
  Runtime: 'GameStudioKit.Runtime',
  Editor: 'GameStudioKit.Editor',
  'Editor.Pipeline': 'GameStudioKit.Editor.Pipeline',
  'Editor.Recorder': 'GameStudioKit.Editor.Recorder',
});

const SKIP_DIRS = ['.git', 'node_modules', '.venv', '__pycache__', '.pytest_cache', '.mypy_cache', '.ruff_cache'];
const skipFile = (rel) => /\.(pyc|pyo)$/i.test(rel) || /(^|\/)\.DS_Store$/.test(rel);

/**
 * The install plan: every source file and where it lands. Only files that exist are listed.
 * kinds: lib | tools | workflow | schema | package | forwarder (forwarder entries have `content` instead of `src`).
 * dest is relative to the workspace W (POSIX). Package entries are listed but only copied when includePackage is set.
 * When U ≠ W every top-level `*.mjs` of the gate sub (each is a CLI exporting main) gets a W-side forwarder at
 * `Tools/kit/gate/<file>`, so every documented W-relative gate command works.
 * @param {string} kitRoot the game-studio-kit repo folder
 * @param {{unitySubdir?: string}} [opts] validated by normalizeUnitySubdir (throws EUNITYSUBDIR)
 * @returns {{src?: string, content?: string, dest: string, owner_skill: string, kind: string}[]}
 */
export function installPlan(kitRoot, opts = {}) {
  const sub = normalizeUnitySubdir(opts.unitySubdir ?? '.');
  const uPrefix = sub === '.' ? '' : `${sub}/`;
  const skillsDir = path.join(kitRoot, 'skills');
  const plan = [];
  const add = (src, dest, owner, kind) => plan.push({ src, dest, owner_skill: owner, kind });
  const walk = (dir) => listFiles(dir, { skipDirs: SKIP_DIRS, filter: (r) => !skipFile(r) });

  const libDir = path.join(skillsDir, 'game-studio-run', 'scripts', 'lib');
  for (const rel of walk(libDir)) {
    add(path.join(libDir, rel), `Tools/kit/lib/${rel}`, 'game-studio-run', 'lib');
    if (uPrefix) add(path.join(libDir, rel), `${uPrefix}Tools/kit/lib/${rel}`, 'game-studio-run', 'lib');
  }
  for (const skill of SKILLS) {
    let subs = [];
    try {
      subs = fs.readdirSync(path.join(skillsDir, skill, 'scripts', 'project'), { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name);
    } catch { /* skill has no project scripts */ }
    for (const s of subs) {
      if (!PROJECT_DIRS.some((d) => d.skill === skill && d.sub === s)) {
        throw new Error(`installPlan: skills/${skill}/scripts/project/${s} is not in PROJECT_DIRS (needs a CONTRACTS.md change)`);
      }
    }
  }
  for (const d of PROJECT_DIRS) {
    const srcDir = path.join(skillsDir, d.skill, 'scripts', 'project', d.sub);
    const base = d.root === 'U' ? `${uPrefix}Tools/kit/${d.sub}` : `Tools/kit/${d.sub}`;
    for (const rel of walk(srcDir)) add(path.join(srcDir, rel), `${base}/${rel}`, d.skill, 'tools');
  }
  if (uPrefix) {
    const gateTop = `${uPrefix}Tools/kit/gate/`;
    for (const p of [...plan]) {
      if (p.kind !== 'tools' || !p.dest.startsWith(gateTop)) continue;
      const file = p.dest.slice(gateTop.length);
      if (/^[^/]+\.mjs$/.test(file)) plan.push({ content: gateForwarder(sub, file), dest: `Tools/kit/gate/${file}`, owner_skill: 'game-studio-run', kind: 'forwarder' });
    }
  }
  for (const skill of SKILLS) {
    const wf = path.join(skillsDir, skill, 'assets', 'workflows');
    for (const rel of walk(wf)) if (rel.endsWith('.workflow.js') || rel.endsWith('.md')) add(path.join(wf, rel), `Tools/kit/workflows/${rel}`, skill, 'workflow');
    const sc = path.join(skillsDir, skill, 'assets', 'schemas');
    for (const rel of walk(sc)) if (rel.endsWith('.schema.json')) add(path.join(sc, rel), `Tools/kit/schemas/${rel}`, skill, 'schema');
    const un = path.join(skillsDir, skill, 'assets', 'unity');
    for (const rel of walk(un)) {
      const top = rel.split('/')[0];
      if (PACKAGE_ASSEMBLIES[top] && !rel.endsWith('.tmpl')) add(path.join(un, rel), `${uPrefix}Packages/${PACKAGE_NAME}/${rel}`, skill, 'package');
    }
  }
  const pkgSkel = path.join(skillsDir, 'game-studio-run', 'assets', 'unity-package');
  for (const rel of walk(pkgSkel)) add(path.join(pkgSkel, rel), `${uPrefix}Packages/${PACKAGE_NAME}/${rel.replace(/\.tmpl$/, '')}`, 'game-studio-run', 'package');

  const seen = new Map();
  for (const p of plan) {
    const key = path.posix.normalize(p.dest).toLowerCase();
    if (seen.has(key)) throw new Error(`installPlan: two sources map to ${p.dest}: ${seen.get(key)} and ${p.src ?? p.kind}`);
    seen.set(key, p.src ?? p.kind);
  }
  return plan;
}

/**
 * Source of a W-side forwarder written when the Unity project is a subfolder, so `node Tools/kit/gate/<file>` works from
 * W. It imports `<sub>/Tools/kit/gate/<file>` and runs its exported main(argv) through runCli (W/Tools/kit/lib/cli.mjs), so
 * exit codes (2 usage, 3 INFRA, 75 BLOCKED) and the --json error line are exactly those of a direct run. A target that
 * cannot be loaded, or has no main, is INFRA (3).
 * @param {string} unitySubdir relative to W (normalizeUnitySubdir; '.' is refused because no forwarder is needed)
 * @param {string} [file] top-level gate CLI file name
 */
export function gateForwarder(unitySubdir, file = 'game_gate.mjs') {
  const sub = normalizeUnitySubdir(unitySubdir);
  if (sub === '.') throw new Error('gateForwarder: U = W needs no forwarder');
  if (!/^[A-Za-z0-9_.-]+\.mjs$/.test(file)) throw new Error(`gateForwarder: bad file name ${file}`);
  const parts = JSON.stringify(sub.split('/'));
  const f = JSON.stringify(file);
  return [
    `// Generated by game-studio-kit. Forwards to ${sub}/Tools/kit/gate/${file} inside the Unity project; do not edit.`,
    "import path from 'node:path';",
    "import { fileURLToPath, pathToFileURL } from 'node:url';",
    "import { CliError, EXIT, runCli } from '../lib/cli.mjs';",
    'const here = path.dirname(fileURLToPath(import.meta.url));',
    `const target = path.join(here, '..', '..', '..', ...${parts}, 'Tools', 'kit', 'gate', ${f});`,
    'await runCli(async (argv) => {',
    '  let mod;',
    '  try {',
    '    mod = await import(pathToFileURL(target).href);',
    '  } catch (e) {',
    "    throw new CliError(`gate forwarder cannot load ${target}: ${e.message}`, EXIT.INFRA, { verdict: 'INFRA', infra: 'kit_not_installed' });",
    '  }',
    "  if (typeof mod.main !== 'function') throw new CliError(`${target} exports no main(argv)`, EXIT.INFRA, { verdict: 'INFRA', infra: 'kit_not_installed' });",
    '  return mod.main(argv);',
    '});',
    '',
  ].join('\n');
}

/**
 * Copy the kit into a project-shaped folder for tests (or a dry-run preview): lib, tools, workflows, schemas, forwarder,
 * and (optionally) the package. Writes Tools/kit/kit.json with `staged: true`. Does not merge settings or write Docs.
 * @param {string} kitRoot
 * @param {string} wsOut target workspace folder (created)
 * @param {{unitySubdir?: string, includePackage?: boolean, kitVersion?: string}} [opts] unitySubdir: normalizeUnitySubdir rules
 * @returns {{ws: string, unity: string, files: string[]}}
 */
export function stageKitTree(kitRoot, wsOut, opts = {}) {
  const unitySubdir = normalizeUnitySubdir(opts.unitySubdir ?? '.');
  const plan = installPlan(kitRoot, { unitySubdir });
  const files = [];
  for (const p of plan) {
    if (p.kind === 'package' && !opts.includePackage) continue;
    const dest = path.join(wsOut, p.dest);
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    if (p.content !== undefined) fs.writeFileSync(dest, p.content, 'utf8');
    else fs.copyFileSync(p.src, dest);
    files.push(p.dest);
  }
  const kitJson = {
    schema: 1,
    kit_version: opts.kitVersion ?? 'dev',
    installed_utc: utcIso(),
    unity_subdir: unitySubdir,
    gate_prefix: '[GSK]',
    options: { mcp: [], autonomous: false },
    staged: true,
  };
  fs.mkdirSync(path.join(wsOut, 'Tools', 'kit'), { recursive: true });
  fs.writeFileSync(path.join(wsOut, 'Tools', 'kit', 'kit.json'), JSON.stringify(kitJson, null, 2) + '\n', 'utf8');
  files.push('Tools/kit/kit.json');
  return { ws: path.resolve(wsOut), unity: unitySubdir === '.' ? path.resolve(wsOut) : path.resolve(wsOut, unitySubdir), files };
}

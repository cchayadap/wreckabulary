// Which Unity editor runs the project: env override (GSK_UNITY, then gate.config.json editorEnv such as KNOTWELL_UNITY),
// else the version in ProjectSettings/ProjectVersion.txt looked up with `unity editors -i --json`, else the default Hub
// folder. Never a hard-coded path as the only source (KNOTWELL weak spot 2).
import fs from 'node:fs';
import path from 'node:path';
import { readJsonOr, readText, writeJsonAtomic } from '../../lib/jsonio.mjs';
import { run } from '../../lib/proc.mjs';
import { cliEnv, parseCommandValue, unityCliArgv } from './config.mjs';
import { parseJsonLoose } from './unitycli.mjs';

/**
 * @param {string} file ProjectSettings/ProjectVersion.txt
 * @returns {{version: string, revision: string|null}|null}
 */
export function readProjectVersion(file) {
  let text;
  try {
    text = readText(file);
  } catch {
    return null;
  }
  const v = /^\s*m_EditorVersion:\s*(\S+)/m.exec(text);
  if (!v) return null;
  const r = /^\s*m_EditorVersionWithRevision:\s*\S+\s*\(([0-9a-f]+)\)/m.exec(text);
  return { version: v[1], revision: r ? r[1] : null };
}

/** Default Hub install locations for a version (checked in order). */
export function hubCandidates(version, env = process.env) {
  const out = [];
  if (process.platform === 'win32') {
    for (const base of [env.ProgramFiles || 'C:\\Program Files', 'C:\\Program Files']) out.push(path.join(base, 'Unity', 'Hub', 'Editor', version, 'Editor', 'Unity.exe'));
  } else if (process.platform === 'darwin') {
    out.push(`/Applications/Unity/Hub/Editor/${version}/Unity.app/Contents/MacOS/Unity`);
  } else if (env.HOME) {
    out.push(path.join(env.HOME, 'Unity', 'Hub', 'Editor', version, 'Editor', 'Unity'));
  }
  return [...new Set(out)];
}

/**
 * Parse `unity editors -i --json` output into [{version, location}].
 * @param {string} stdout
 */
export function parseEditorsJson(stdout) {
  const j = parseJsonLoose(stdout);
  const list = Array.isArray(j) ? j : Array.isArray(j?.data) ? j.data : Array.isArray(j?.data?.editors) ? j.data.editors : [];
  return list
    .filter((e) => e && typeof e === 'object')
    .map((e) => ({ version: e.version || e.editorVersion || null, location: e.location || e.path || e.editorPath || null }))
    .filter((e) => e.version && e.location);
}

/**
 * Resolve the editor command line.
 * @param {ReturnType<import('./config.mjs').loadContext>} ctx
 * @param {{env?: NodeJS.ProcessEnv, noCli?: boolean}} [opts]
 * @returns {Promise<{argv: string[]|null, exe: string|null, version: string|null, source: string, error?: string}>}
 */
export async function resolveEditor(ctx, opts = {}) {
  const env = opts.env ?? process.env;
  const pv = readProjectVersion(ctx.paths.projectVersion);
  const version = pv?.version ?? null;
  for (const name of ctx.cfg.editorEnv) {
    const argv = parseCommandValue(env[name]);
    if (!argv) continue;
    if (argv.length === 1 && !fs.existsSync(argv[0])) {
      return { argv: null, exe: null, version, source: `env:${name}`, error: `${name} names an editor that does not exist: ${argv[0]}` };
    }
    return { argv, exe: argv.length === 1 ? argv[0] : null, version, source: `env:${name}` };
  }
  if (!version) return { argv: null, exe: null, version: null, source: 'none', error: `cannot read the editor version from ${path.relative(ctx.U, ctx.paths.projectVersion)}` };
  const cacheFile = path.join(ctx.paths.logs, 'gate_editor.json');
  const cached = readJsonOr(cacheFile, null, { onInvalid: 'fallback' });
  if (cached && cached.version === version && cached.location && fs.existsSync(cached.location)) {
    return { argv: [cached.location], exe: cached.location, version, source: 'cache' };
  }
  const cli = opts.noCli ? null : unityCliArgv(env);
  if (cli) {
    const r = await run(cli[0], [...cli.slice(1), 'editors', '-i', '--format', 'json'], { env: cliEnv(env), cwd: ctx.U, timeoutMs: 60000 });
    if (r.exit === 0) {
      const hit = parseEditorsJson(r.stdout).find((e) => e.version === version && fs.existsSync(e.location));
      if (hit) {
        try {
          writeJsonAtomic(cacheFile, { version, location: hit.location, source: 'unity editors' });
        } catch { /* cache is optional */ }
        return { argv: [hit.location], exe: hit.location, version, source: 'unity editors' };
      }
    }
  }
  const hub = hubCandidates(version, env).find((p) => fs.existsSync(p));
  if (hub) return { argv: [hub], exe: hub, version, source: 'hub default' };
  return { argv: null, exe: null, version, source: 'none', error: `no installed Unity editor ${version} (checked ${ctx.cfg.editorEnv.join(', ')}, unity editors, the Hub folder); install it in Unity Hub or set GSK_UNITY` };
}

/**
 * Lower-case executable basenames a Unity editor process of this project may run as: Unity.exe / Unity, the editor named
 * by the editorEnv variables (a wrapper such as a test shim), and the cached `unity editors` location. Synchronous, so the
 * PID checks of lock and descriptor code can use it.
 * @param {ReturnType<import('./config.mjs').loadContext>} ctx
 * @param {NodeJS.ProcessEnv} [env]
 * @returns {Set<string>}
 */
export function editorImageNames(ctx, env = process.env) {
  const base = (p) => path.win32.basename(String(p)).toLowerCase();
  const names = new Set(['unity.exe', 'unity']);
  for (const name of ctx.cfg.editorEnv) {
    try {
      const argv = parseCommandValue(env[name]);
      if (argv?.[0]) names.add(base(argv[0]));
    } catch { /* a bad value is reported where the editor is resolved */ }
  }
  const cached = readJsonOr(path.join(ctx.paths.logs, 'gate_editor.json'), null, { onInvalid: 'fallback' });
  if (cached?.location) names.add(base(cached.location));
  return names;
}

/** The editor folder (for the Check csproj's UnityEditorDir) or null. */
export function editorDirOf(resolved) {
  return resolved?.exe ? path.dirname(resolved.exe) : null;
}

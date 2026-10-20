// Gate context: where U and W are, gate.config.json with defaults, and the command lines of external tools.
// The gate lives at U/Tools/kit/gate/, so U is three folders up from it (CONTRACTS §3.3).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { CliError, EXIT } from '../../lib/cli.mjs';
import { readJson } from '../../lib/jsonio.mjs';
import { findWorkspace, isInside, kitPaths, readKitConfig, relPosix } from '../../lib/paths.mjs';
import { which } from '../../lib/proc.mjs';

export const GATE_DIR = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

/** Defaults for every gate.config.json field (the gateConfig $def plus the gate unit's additive fields). */
export const DEFAULTS = Object.freeze({
  prefix: '[GSK]',
  editorEnv: ['GSK_UNITY'],
  timeoutMs: 30 * 60 * 1000,
  lockWaitMs: 60 * 60 * 1000,
  checkSlots: 4,
  coreSlots: 2,
  testAssemblies: [],
  buildProfile: '',
  buildProfiles: {},
  exeName: '',
  smokeSeconds: 90,
  qaDefine: 'GSK_QA',
  // additive (gate unit)
  codeRoots: ['Assets'],
  checkExcludes: ['Assets/Plugins/**', 'Assets/TextMesh Pro/**', 'Assets/Samples/**', 'Assets/_AgentCaptures/**'],
  layers: [],
  core: { asmdefs: [], testDirs: [] },
  extraDefines: [],
  recompileTimeoutS: 900,
  readyTimeoutMs: 180000,
  serveTimeoutMs: 0, // 0 = timeoutMs
  unserveGraceMs: 30000,
  warmupTries: 5,
  warmupDelayMs: 2000,
  pollMs: 1000,
  lockPollMs: 2000,
  playerArgs: ['-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720'],
  smokeGraceMs: 30000,
  smokeSeed: 1,
});

const PLACEHOLDER = /\{\{[A-Z0-9_]+\}\}/;
const isSet = (v) => v !== undefined && v !== null && !(typeof v === 'string' && (v === '' || PLACEHOLDER.test(v)));

/**
 * Parse an env value naming a tool: a JSON argv array (`["node","fake.mjs"]`, used by tests and wrappers) or a path.
 * @param {string|undefined} value
 * @returns {string[]|null}
 */
export function parseCommandValue(value) {
  if (value === undefined || value === null || String(value).trim() === '') return null;
  const v = String(value).trim();
  if (v.startsWith('[')) {
    let arr;
    try {
      arr = JSON.parse(v);
    } catch {
      throw new CliError(`cannot parse ${v.slice(0, 60)} as a JSON argv array`, EXIT.INFRA, { verdict: 'INFRA', infra: 'bad_tool_env' });
    }
    if (!Array.isArray(arr) || !arr.length || !arr.every((x) => typeof x === 'string')) {
      throw new CliError('a tool command array must be a non-empty array of strings', EXIT.INFRA, { verdict: 'INFRA', infra: 'bad_tool_env' });
    }
    return arr;
  }
  return [v.replace(/^"(.*)"$/, '$1')];
}

function mergeConfig(raw, kit) {
  const cfg = { ...DEFAULTS, core: { ...DEFAULTS.core } };
  for (const [k, v] of Object.entries(raw || {})) {
    if (!isSet(v)) continue;
    if (k === 'core' && v && typeof v === 'object') cfg.core = { ...cfg.core, ...v };
    else if (k === 'buildProfiles' && v && typeof v === 'object') cfg.buildProfiles = Object.fromEntries(Object.entries(v).filter(([, p]) => isSet(p)));
    else cfg[k] = v;
  }
  if (!isSet(raw?.prefix) && isSet(kit?.gate_prefix)) cfg.prefix = kit.gate_prefix;
  const envs = (Array.isArray(cfg.editorEnv) ? cfg.editorEnv : []).filter((e) => /^[A-Z][A-Z0-9_]*$/.test(e));
  cfg.editorEnv = ['GSK_UNITY', ...envs.filter((e) => e !== 'GSK_UNITY')];
  for (const k of ['timeoutMs', 'lockWaitMs', 'checkSlots', 'coreSlots', 'smokeSeconds', 'recompileTimeoutS', 'readyTimeoutMs', 'serveTimeoutMs', 'unserveGraceMs', 'warmupTries', 'warmupDelayMs', 'pollMs', 'lockPollMs', 'smokeGraceMs', 'smokeSeed']) {
    const n = Number(cfg[k]);
    cfg[k] = Number.isFinite(n) && n >= 0 ? n : DEFAULTS[k];
  }
  cfg.checkSlots = Math.max(1, Math.min(16, Math.floor(cfg.checkSlots)));
  cfg.coreSlots = Math.max(1, Math.min(16, Math.floor(cfg.coreSlots)));
  if (!cfg.serveTimeoutMs) cfg.serveTimeoutMs = cfg.timeoutMs;
  for (const k of ['testAssemblies', 'codeRoots', 'checkExcludes', 'layers', 'extraDefines', 'playerArgs']) if (!Array.isArray(cfg[k])) cfg[k] = [...DEFAULTS[k]];
  if (!cfg.buildProfile && cfg.buildProfiles.release) cfg.buildProfile = cfg.buildProfiles.release;
  return cfg;
}

/**
 * Build the gate context.
 * @param {{gateDir?: string}} [opts]
 */
export function loadContext(opts = {}) {
  const gateDir = path.resolve(opts.gateDir ?? GATE_DIR);
  const U = path.resolve(gateDir, '..', '..', '..');
  const cfgFile = path.join(gateDir, 'gate.config.json');
  let raw = {};
  let cfgSource = 'defaults';
  if (fs.existsSync(cfgFile)) {
    try {
      raw = readJson(cfgFile);
      cfgSource = 'gate.config.json';
    } catch (e) {
      throw new CliError(`gate.config.json is not valid JSON: ${e.message}`, EXIT.INFRA, { verdict: 'INFRA', infra: 'gate_config' });
    }
  }
  let W = null;
  if (isSet(raw.workspace)) {
    const cand = path.resolve(U, raw.workspace);
    if (isInside(U, cand) && fs.existsSync(cand)) W = cand;
  }
  if (!W) W = findWorkspace(U) || U;
  if (!isInside(U, W)) W = U;
  const kit = readKitConfig(W);
  const cfg = mergeConfig(raw, kit);
  const unitySubdir = relPosix(W, U) || '.';
  let kp;
  try {
    kp = kitPaths(W, { unitySubdir });
  } catch {
    kp = kitPaths(U, { unitySubdir: '.' });
  }
  const logs = path.join(U, 'Logs');
  return {
    U,
    W,
    unitySubdir,
    gateDir,
    cfg,
    cfgSource,
    kit,
    kp,
    paths: {
      locks: path.join(U, 'Tools', '.locks'),
      logs,
      crashes: path.join(logs, 'crashes'),
      sessions: path.join(logs, 'sessions'),
      history: path.join(logs, 'gate_history.jsonl'),
      testResults: path.join(U, 'TestResults'),
      builds: path.join(U, 'Builds'),
      check: path.join(gateDir, 'Check'),
      core: path.join(gateDir, 'CoreTests'),
      descriptor: path.join(U, 'Library', 'Pipeline', '.unity-pipeline-port'),
      lockfile: path.join(U, 'Temp', 'UnityLockfile'),
      projectVersion: path.join(U, 'ProjectSettings', 'ProjectVersion.txt'),
      projectSettings: path.join(U, 'ProjectSettings', 'ProjectSettings.asset'),
      manifest: path.join(U, 'Packages', 'manifest.json'),
      scriptAssemblies: path.join(U, 'Library', 'ScriptAssemblies'),
      unityPid: kp.unityPid,
      qaRuns: kp.qaRuns,
      gatesJsonl: kp.gatesJsonl,
      gatesJson: kp.gatesJson,
      schemas: path.join(W, 'Tools', 'kit', 'schemas'),
    },
  };
}

/** argv of the Unity CLI: env GSK_UNITY_CLI (JSON argv or path), else `unity` on PATH, else null. */
export function unityCliArgv(env = process.env) {
  const fromEnv = parseCommandValue(env.GSK_UNITY_CLI);
  if (fromEnv) return fromEnv;
  return which('unity', { env }) ? ['unity'] : null;
}

/** argv of dotnet: env GSK_DOTNET (JSON argv or path), else `dotnet` on PATH, else null. */
export function dotnetArgv(env = process.env) {
  const fromEnv = parseCommandValue(env.GSK_DOTNET);
  if (fromEnv) return fromEnv;
  return which('dotnet', { env }) ? ['dotnet'] : null;
}

/** Environment for Unity CLI calls: no banner, pager, colour, prompts or CLI usage ping. */
export function cliEnv(env = process.env) {
  return { ...env, UNITY_NO_BANNER: '1', UNITY_NON_INTERACTIVE: '1', UNITY_NO_PAGER: '1', UNITY_NO_CLI_INVOKED_TELEMETRY: '1', NO_COLOR: '1' };
}

/** Folder holding the GUI editor's Editor.log / Editor-prev.log (GSK_EDITOR_LOG_DIR overrides; tests use it). */
export function editorLogDir(env = process.env) {
  if (env.GSK_EDITOR_LOG_DIR) return env.GSK_EDITOR_LOG_DIR;
  if (process.platform === 'win32' && env.LOCALAPPDATA) return path.join(env.LOCALAPPDATA, 'Unity', 'Editor');
  if (process.platform === 'darwin' && env.HOME) return path.join(env.HOME, 'Library', 'Logs', 'Unity');
  return env.HOME ? path.join(env.HOME, '.config', 'unity3d') : null;
}

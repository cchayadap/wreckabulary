#!/usr/bin/env node
// Run one kit Blender script headless and judge it by its JSON result, never by Blender's exit code alone.
//   node Tools/kit/blender/bl_run.mjs <script.py> [--timeout <s>] [--blender <exe>] [--log <file>] [--result <file>]
//        [--ignore-alerts] [--json] -- <script args>
//   node Tools/kit/blender/bl_run.mjs --version [--json]
// Command line (always): blender --background --factory-startup --python-exit-code 1 --python <script> -- <args>
// --result <file>. stdin is closed (the Blender Lab MCP *_for_cli hang, issue #42 / PR #48), the whole process tree
// is killed on timeout (default 600 s), and key-like variables (*_KEY, *_TOKEN, *_SECRET, *PASSWORD*) are removed from
// Blender's environment. ok = exit 0 and result.ok === true; {'FINISHED'} from an operator never counts.
// Exit: 0 ok; 1 the script failed, timed out or wrote no result; 2 usage; 3 Blender missing or not startable;
// 75 blocked by a watchdog alert (ram_low, commit_high, disk_stop or disk_emergency at level stop/emergency).
// Scripts: a bare name (export_unity.py) is always the kit script next to this file; a script elsewhere is given as a
// path (./my_script.py). --result (here or after --, spaced or --result=) must be a .json file that is new or an
// earlier kit result ({"ok": ...}); --log must be .log/.txt, new or an earlier bl_run log. Neither may name the
// script or one of its arguments. Anything else is refused (exit 2) and left untouched.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { CliError, EXIT, UsageError, emit, isMain, note, parseCli, runCli } from '../lib/cli.mjs';
import { findWorkspace, normalizeForCompare, toPosix, wsRel } from '../lib/paths.mjs';
import { run, which } from '../lib/proc.mjs';
import { utcFileStamp, utcIso } from '../lib/time.mjs';

export const DEFAULT_TIMEOUT_S = 600;
export const RESULT_MARK = '__RESULT__';
export const LOG_HEADER = '# bl_run log';
export const BLOCKING_ALERTS = Object.freeze(['ram_low', 'commit_high', 'disk_stop', 'disk_emergency']);
const HERE = path.dirname(fileURLToPath(import.meta.url));
const SECRET_ENV = /(_KEY|_TOKEN|_SECRET|PASSWORD|_PASS)$/i;

/** Blender version folders under Program Files, newest first. @param {string} root */
export function installedBlenders(root) {
  let names = [];
  try {
    names = fs.readdirSync(root, { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name);
  } catch {
    return [];
  }
  const ver = (n) => (/(\d+)\.(\d+)(?:\.(\d+))?/.exec(n) || []).slice(1).map((x) => Number(x || 0));
  return names
    .filter((n) => /^Blender\s+\d/i.test(n))
    .sort((a, b) => {
      const va = ver(a);
      const vb = ver(b);
      for (let i = 0; i < 3; i++) if ((va[i] || 0) !== (vb[i] || 0)) return (vb[i] || 0) - (va[i] || 0);
      return 0;
    })
    .map((n) => path.join(root, n, process.platform === 'win32' ? 'blender.exe' : 'blender'))
    .filter((p) => fs.existsSync(p));
}

/**
 * The Blender executable: --blender, then GSK_BLENDER_EXE, then the newest Program Files install, then PATH.
 * @param {{explicit?: string, env?: NodeJS.ProcessEnv, programFiles?: string}} [opts]
 * @returns {{exe: string, source: string}|null}
 */
export function findBlender(opts = {}) {
  const env = opts.env ?? process.env;
  if (opts.explicit) return fs.existsSync(opts.explicit) ? { exe: path.resolve(opts.explicit), source: 'flag' } : null;
  if (env.GSK_BLENDER_EXE) return fs.existsSync(env.GSK_BLENDER_EXE) ? { exe: path.resolve(env.GSK_BLENDER_EXE), source: 'GSK_BLENDER_EXE' } : null;
  if (process.platform === 'win32') {
    const pf = opts.programFiles ?? env.ProgramFiles ?? 'C:/Program Files';
    const hit = installedBlenders(path.join(pf, 'Blender Foundation'))[0];
    if (hit) return { exe: hit, source: 'program_files' };
  }
  const onPath = which('blender', { env });
  return onPath ? { exe: onPath, source: 'path' } : null;
}

/** The fixed command line. @param {string} script abs path @param {string[]} scriptArgs @param {string} resultPath */
export function blenderArgs(script, scriptArgs, resultPath) {
  return ['--background', '--factory-startup', '--python-exit-code', '1', '--python', script, '--', ...scriptArgs, '--result', resultPath];
}

/** Environment for Blender without key-like variables. @returns {{env: NodeJS.ProcessEnv, removed: number}} */
export function scrubEnv(env = process.env) {
  const out = {};
  let removed = 0;
  for (const [k, v] of Object.entries(env)) {
    if (SECRET_ENV.test(k) || /^(HF_TOKEN|FAL_KEY|MESHY_API_KEY|TRIPO_API_KEY|OPENAI_API_KEY|ANTHROPIC_API_KEY)$/i.test(k)) removed++;
    else out[k] = v;
  }
  return { env: out, removed };
}

/** Watchdog alerts in W/.harness/alerts that stop background Blender work (ARCHITECTURE 4.5). */
export function blockingAlerts(ws) {
  if (!ws) return [];
  const dir = path.join(ws, '.harness', 'alerts');
  const out = [];
  for (const name of BLOCKING_ALERTS) {
    const f = path.join(dir, name);
    if (!fs.existsSync(f)) continue;
    let level = null;
    try {
      level = JSON.parse(fs.readFileSync(f, 'utf8').replace(/^\uFEFF/, '')).level ?? null;
    } catch {
      /* unreadable: judge by name */
    }
    const stops = level ? ['stop', 'emergency'].includes(level) : name.startsWith('disk_');
    if (stops) out.push({ name, level });
  }
  return out;
}

/** CONTRACTS 1.1: a path for output, W-relative POSIX when inside the workspace, else absolute POSIX. */
export function showPath(p, ws) {
  return (ws && wsRel(ws, p)) || toPosix(path.resolve(p));
}

/**
 * Resolve a script. A bare name (no slash or backslash) is only ever the kit script next to bl_run.mjs, so a same-named file in
 * the working folder (an unpacked asset pack, an old kit copy) can never shadow it; a path resolves against cwd.
 */
export function resolveScript(spec, cwd = process.cwd(), here = HERE) {
  if (!/[\\/]/.test(spec)) {
    const local = path.join(here, spec);
    return fs.existsSync(local) ? local : null;
  }
  const direct = path.resolve(cwd, spec);
  return fs.existsSync(direct) ? direct : null;
}

/**
 * Take every --result from the script args (`--result <p>` and `--result=<p>`). Two different paths are a usage error.
 * @param {string[]} scriptArgs @returns {{args: string[], result: string|undefined}}
 */
export function takeInnerResult(scriptArgs) {
  const args = [];
  let result;
  const take = (v) => {
    if (!v) throw new UsageError('--result after -- needs a path');
    if (result !== undefined && normalizeForCompare(result) !== normalizeForCompare(v)) throw new UsageError('two different --result paths');
    result = v;
  };
  for (let i = 0; i < scriptArgs.length; i++) {
    const a = scriptArgs[i];
    if (a === '--result') {
      take(scriptArgs[i + 1]);
      i++;
    } else if (a.startsWith('--result=')) {
      take(a.slice('--result='.length));
    } else {
      args.push(a);
    }
  }
  return { args, result };
}

function firstBytes(file, n = 256) {
  const fd = fs.openSync(file, 'r');
  try {
    const buf = Buffer.alloc(n);
    const got = fs.readSync(fd, buf, 0, n, 0);
    return buf.subarray(0, got).toString('utf8').replace(/^\uFEFF/, '');
  } finally {
    fs.closeSync(fd);
  }
}

/**
 * Refuse result and log paths that could destroy another file: wrong extension, a folder, an existing file that is not
 * a kit result / bl_run log, the same path twice, or a path that is the script or one of its arguments.
 * @param {{resultPath: string, logPath: string, script: string, scriptArgs: string[], cwd?: string}} o
 */
export function checkOutputPaths({ resultPath, logPath, script, scriptArgs, cwd = process.cwd() }) {
  if (!/\.json$/i.test(resultPath)) throw new UsageError(`--result must be a .json file: ${resultPath}`);
  if (!/\.(log|txt)$/i.test(logPath)) throw new UsageError(`--log must be a .log or .txt file: ${logPath}`);
  const r = normalizeForCompare(resultPath);
  const l = normalizeForCompare(logPath);
  if (r === l) throw new UsageError('--result and --log are the same file');
  const inputs = [script];
  for (const a of scriptArgs) {
    if (!a) continue;
    const eq = a.startsWith('--') ? a.indexOf('=') : -1;
    if (eq > 0) inputs.push(a.slice(eq + 1));
    else if (!a.startsWith('--')) inputs.push(a);
  }
  for (const a of inputs) {
    const n = normalizeForCompare(path.resolve(cwd, a));
    if (n === r || n === l) throw new UsageError(`--result/--log would overwrite ${a}, which the script reads or writes`);
  }
  if (fs.existsSync(resultPath)) {
    if (fs.statSync(resultPath).isDirectory()) throw new UsageError(`--result is a folder: ${resultPath}`);
    let old = null;
    try {
      old = JSON.parse(fs.readFileSync(resultPath, 'utf8').replace(/^\uFEFF/, ''));
    } catch {
      old = null;
    }
    if (!old || typeof old !== 'object' || Array.isArray(old) || !('ok' in old)) {
      throw new UsageError(`--result ${resultPath} exists and is not a kit result file (no "ok" field); refusing to overwrite it`);
    }
  }
  if (fs.existsSync(logPath)) {
    const st = fs.statSync(logPath);
    if (st.isDirectory()) throw new UsageError(`--log is a folder: ${logPath}`);
    if (st.size > 0 && !firstBytes(logPath).startsWith(LOG_HEADER)) {
      throw new UsageError(`--log ${logPath} exists and is not a bl_run log; refusing to overwrite it`);
    }
  }
}

/** Last `__RESULT__{json}` line in Blender's stdout (backup when the result file is missing). */
export function parseResultMark(stdout) {
  const lines = String(stdout || '').split(/\r?\n/).filter((l) => l.startsWith(RESULT_MARK));
  if (!lines.length) return null;
  try {
    return JSON.parse(lines[lines.length - 1].slice(RESULT_MARK.length));
  } catch {
    return null;
  }
}

function errorTail(text) {
  const lines = String(text || '').split(/\r?\n/).filter(Boolean);
  const i = lines.map((l) => l.startsWith('Traceback')).lastIndexOf(true);
  return (i >= 0 ? lines.slice(i) : lines.slice(-8)).slice(-12).join('\n');
}

function defaultPaths(scriptPath) {
  const dir = path.join(os.tmpdir(), 'gsk-blender');
  const stem = `${utcFileStamp()}-${process.pid}-${path.basename(scriptPath, '.py')}`;
  return { result: path.join(dir, `${stem}.result.json`), log: path.join(dir, `${stem}.log`) };
}

async function version(values) {
  const found = findBlender({ explicit: values.blender });
  if (!found) throw new CliError('Blender not found (set GSK_BLENDER_EXE or pass --blender)', EXIT.INFRA, { infra: 'blender_missing' });
  const r = await run(found.exe, ['--version'], { timeoutMs: 60000, env: scrubEnv().env });
  const m = /Blender\s+(\d+\.\d+(?:\.\d+)?)/.exec(r.stdout || '');
  if (r.exit !== 0 || !m) throw new CliError(`blender --version failed (exit ${r.exit})`, EXIT.INFRA, { exe: found.exe, infra: 'blender_broken' });
  const result = { ok: true, version: m[1], line: (r.stdout.split(/\r?\n/)[0] || '').trim(), exe: found.exe, source: found.source, secs: r.secs };
  emit(result, { json: values.json, text: `Blender ${result.version} (${result.exe})` });
  return EXIT.OK;
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals, rest } = parseCli(argv, {
    timeout: { type: 'string' },
    blender: { type: 'string' },
    log: { type: 'string' },
    result: { type: 'string' },
    version: { type: 'boolean', default: false },
    'ignore-alerts': { type: 'boolean', default: false },
  });
  if (values.version) return version(values);
  if (positionals.length !== 1) throw new UsageError('usage: bl_run.mjs <script.py> [options] -- <script args>');
  const script = resolveScript(positionals[0]);
  if (!script) throw new UsageError(`script not found: ${positionals[0]}`);
  const timeoutS = values.timeout === undefined ? DEFAULT_TIMEOUT_S : Number(values.timeout);
  if (!Number.isFinite(timeoutS) || timeoutS <= 0) throw new UsageError('--timeout must be a positive number of seconds');
  const { args: scriptArgs, result: inner } = takeInnerResult(rest);
  if (values.result && inner !== undefined && normalizeForCompare(values.result) !== normalizeForCompare(inner)) {
    throw new UsageError('two different --result paths');
  }
  const given = values.result ?? inner;
  const defaults = defaultPaths(script);
  const resultPath = path.resolve(given || defaults.result);
  const logPath = path.resolve(values.log || (given ? resultPath.replace(/(\.result)?\.json$/i, '') + '.log' : defaults.log));
  checkOutputPaths({ resultPath, logPath, script, scriptArgs });

  const ws = findWorkspace(process.cwd());
  if (!values['ignore-alerts']) {
    const alerts = blockingAlerts(ws);
    if (alerts.length) {
      throw new CliError(`blocked by watchdog alert(s): ${alerts.map((a) => a.name).join(', ')}; free memory or disk first`, EXIT.BLOCKED, { blocked: alerts });
    }
  }
  const found = findBlender({ explicit: values.blender });
  if (!found) throw new CliError('Blender not found (set GSK_BLENDER_EXE or pass --blender)', EXIT.INFRA, { infra: 'blender_missing' });

  fs.mkdirSync(path.dirname(resultPath), { recursive: true });
  fs.mkdirSync(path.dirname(logPath), { recursive: true });
  fs.rmSync(resultPath, { force: true }); // an earlier kit result (checked above): a stale ok:true must never count
  fs.writeFileSync(logPath, `${LOG_HEADER} ${utcIso()} ${path.basename(script)}\n`);
  const { env, removed } = scrubEnv();
  const args = blenderArgs(script, scriptArgs, resultPath);
  note(`blender ${path.basename(script)} (timeout ${timeoutS}s, log ${logPath})`);
  const r = await run(found.exe, args, { timeoutMs: timeoutS * 1000, env, logFile: logPath });

  let result = null;
  let resultSource = null;
  if (fs.existsSync(resultPath)) {
    try {
      result = JSON.parse(fs.readFileSync(resultPath, 'utf8').replace(/^\uFEFF/, ''));
      resultSource = 'file';
    } catch (e) {
      result = null;
      resultSource = `unreadable: ${e.message}`;
    }
  }
  if (!result && !r.timedOut) {
    result = parseResultMark(r.stdout);
    if (result) resultSource = 'stdout';
  }
  const ok = r.exit === 0 && !r.timedOut && !!result && result.ok === true;
  const out = {
    ok,
    exit: r.exit,
    secs: r.secs,
    timedOut: r.timedOut,
    log: showPath(logPath, ws),
    result,
    result_file: showPath(resultPath, ws),
    result_source: resultSource,
    script: path.basename(script),
    script_path: showPath(script, ws),
    blender: toPosix(found.exe),
    env_scrubbed: removed,
  };
  if (r.error) out.error = `could not start Blender: ${r.error}`;
  else if (r.timedOut) out.error = `timed out after ${timeoutS}s; process tree killed`;
  else if (!result) out.error = `no result written (exit ${r.exit})`;
  else if (!ok) out.error = result.error || (r.exit !== 0 ? `blender exit ${r.exit}` : 'result.ok is not true');
  if (!ok && (r.exit !== 0 || !result)) out.stderr_tail = errorTail(`${r.stdout}\n${r.stderr}`);

  let code = EXIT.OK;
  if (r.exit === null && !r.timedOut) code = EXIT.INFRA;
  else if (!ok) code = r.exit === 2 && result && result.usage ? EXIT.USAGE : EXIT.FAIL;
  out.exit_code = code;
  emit(out, {
    json: values.json,
    text: [
      `${ok ? 'OK' : 'FAIL'} ${out.script} in ${r.secs}s (blender exit ${r.exit}${r.timedOut ? ', timed out' : ''})`,
      ...(out.error ? [`error: ${out.error}`] : []),
      `result: ${out.result_file}`,
      `log: ${out.log}`,
    ],
  });
  return code;
}

if (isMain(import.meta.url)) await runCli(main);

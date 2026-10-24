// Child processes: run with a timeout and a PID-tree kill, find executables, start .cmd/.bat safely, and identify a PID
// by its start time before trusting it (Windows reuses PIDs quickly). The kit never kills by process name (guard G3).
import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const CAP = 8 * 1024 * 1024;
const IS_WIN = process.platform === 'win32';

/**
 * True when a process with this PID exists (signal 0 probe; EPERM counts as alive). Says nothing about WHICH process:
 * use pidMatches() before killing or trusting a PID read from a file.
 * @param {number} pid
 */
export function isPidAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (e) {
    return e.code === 'EPERM';
  }
}

/**
 * Kill a process and all its descendants by PID. Windows: `taskkill /PID <pid> /T /F`. Elsewhere: SIGKILL to the group, then the PID.
 * Callers that read the PID from a file check pidMatches() first.
 * @param {number} pid
 * @returns {boolean} true when the kill command reported success
 */
export function treeKill(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  if (IS_WIN) {
    const r = spawnSync('taskkill', ['/PID', String(pid), '/T', '/F'], { windowsHide: true, encoding: 'utf8' });
    return r.status === 0;
  }
  try { process.kill(-pid, 'SIGKILL'); return true; } catch { /* not a group leader */ }
  try { process.kill(pid, 'SIGKILL'); return true; } catch { return false; }
}

/** Environment lookup that ignores key case on Windows (PATH vs Path). */
function envGet(env, name) {
  if (!env) return undefined;
  if (!IS_WIN) return env[name];
  const k = Object.keys(env).find((x) => x.toUpperCase() === name);
  return k === undefined ? undefined : env[k];
}

function isExecFile(p) {
  try {
    const st = fs.statSync(p);
    if (!st.isFile()) return false;
    if (!IS_WIN) fs.accessSync(p, fs.constants.X_OK);
    return true;
  } catch (e) {
    if (e.code === 'ENOENT' || e.code === 'ENOTDIR' || (!IS_WIN && e.code === 'EACCES')) return false;
    // Windows App Execution Aliases (WindowsApps\*.exe) are reparse points: stat fails with EACCES/EPERM, lstat works.
    try { return !fs.lstatSync(p).isDirectory(); } catch { return false; }
  }
}

/**
 * Find an executable. Returns the absolute path or null. Runs nothing.
 * Windows: PATHEXT extensions are tried first; the bare name counts only when it already ends in a PATHEXT extension
 * (so the POSIX shim `nodejs\npx` is skipped for `npx.cmd`). App Execution Aliases (WindowsApps) count as present.
 * A name with a path separator is checked where it points instead of on PATH.
 * @param {string} name
 * @param {{env?: NodeJS.ProcessEnv, cwd?: string}} [opts] PATH and PATHEXT come from env (default process.env); a relative name with a separator resolves against cwd
 */
export function which(name, opts = {}) {
  const env = opts.env ?? process.env;
  const exts = IS_WIN ? (envGet(env, 'PATHEXT') || '.COM;.EXE;.BAT;.CMD').split(';').filter(Boolean).map((e) => e.toLowerCase()) : [];
  const hasExt = IS_WIN && exts.some((e) => String(name).toLowerCase().endsWith(e));
  const suffixes = IS_WIN && !hasExt ? exts : [''];
  const tryBase = (base) => {
    for (const s of suffixes) if (isExecFile(base + s)) return base + s;
    return null;
  };
  if (/[\\/]/.test(name)) return tryBase(path.resolve(opts.cwd ?? process.cwd(), name));
  const dirs = String(envGet(env, 'PATH') || '').split(path.delimiter).map((d) => d.replace(/^"|"$/g, '')).filter(Boolean);
  for (const d of dirs) {
    const hit = tryBase(path.join(d, name));
    if (hit) return hit;
  }
  return null;
}

// cmd.exe metacharacters, escaped with ^ (the escaping rules of the MIT-licensed cross-spawn package).
const CMD_META = /([()\][%!^"`<>&|;, *?])/g;

function cmdEscapeArg(arg) {
  let a = String(arg);
  a = a.replace(/(\\*)$/, '$1$1'); // trailing backslashes are doubled because a closing quote follows (MSVCRT rules)
  return `"${a}"`.replace(CMD_META, '^$1');
}

/**
 * How run() starts a command (exported so code that must use spawnSync can start .cmd files the same way).
 * On Windows a command that resolves to a .cmd or .bat file cannot be spawned directly (EINVAL since Node 18.20.2, and
 * bare names such as `npx` give ENOENT). It is started as `%ComSpec% /d /s /c "<escaped command line>"` with
 * windowsVerbatimArguments; every argument is quoted and its cmd.exe metacharacters are ^-escaped.
 * The batch file re-parses its arguments (`%*`), so an argument holding a double quote, CR or LF cannot be passed safely:
 * such a call throws (code EUNSAFEARG) and run() reports it as a start failure. Pass that data in a file instead.
 * Everything else is spawned as given, without a shell.
 * @param {string} cmd
 * @param {string[]} [args]
 * @param {{env?: NodeJS.ProcessEnv, cwd?: string}} [opts]
 * @returns {{file: string, args: string[], verbatim: boolean, shim: boolean, resolved: string|null}}
 */
export function resolveSpawn(cmd, args = [], opts = {}) {
  if (!IS_WIN || /\.(exe|com)$/i.test(cmd)) return { file: cmd, args, verbatim: false, shim: false, resolved: null };
  const resolved = which(cmd, opts);
  if (resolved && /\.(cmd|bat)$/i.test(resolved)) {
    const bad = args.find((a) => /["\r\n]/.test(String(a)));
    if (bad !== undefined) {
      const e = new Error(`cannot pass an argument containing a double quote or a line break to ${path.basename(resolved)} through cmd.exe: ${JSON.stringify(String(bad).slice(0, 80))}`);
      e.code = 'EUNSAFEARG';
      throw e;
    }
    const line = [path.normalize(resolved).replace(CMD_META, '^$1'), ...args.map(cmdEscapeArg)].join(' ');
    const env = opts.env ?? process.env;
    return { file: envGet(env, 'COMSPEC') || 'cmd.exe', args: ['/d', '/s', '/c', `"${line}"`], verbatim: true, shim: true, resolved };
  }
  return { file: cmd, args, verbatim: false, shim: false, resolved };
}

/**
 * Run a command without a shell (.cmd/.bat on Windows go through resolveSpawn). Resolves, never rejects.
 * - stdin is closed ('ignore') unless `input` is given, so a child that reads stdin cannot hang (the Blender MCP #42 class).
 *   A child that exits before reading all of `input` does not crash the caller: the write error is reported in stdin_error.
 * - Resolves on the child's 'exit' plus a drain grace (drainMs, default 2000) even when a detached descendant still holds
 *   the inherited stdout/stderr open; the pipes are then destroyed.
 * - On timeout (timeoutMs) the PID tree is killed, but only while the child has not exited, so a reused PID is never hit.
 *   timedOut is true only when the timer fired before the child exited.
 * @param {string} cmd
 * @param {string[]} [args]
 * @param {{cwd?: string, env?: NodeJS.ProcessEnv, timeoutMs?: number, input?: string, logFile?: string, onSpawn?: (pid: number) => void, drainMs?: number, windowsHide?: boolean}} [opts]
 * @returns {Promise<{exit: number|null, signal: string|null, timedOut: boolean, stdout: string, stderr: string, secs: number, pid: number|undefined, error?: string, stdin_error?: string}>}
 *   exit is null when the command could not be started (then `error` says why) or when it was killed by a signal.
 */
export function run(cmd, args = [], opts = {}) {
  const t0 = Date.now();
  const drainMs = opts.drainMs ?? 2000;
  return new Promise((resolve) => {
    const env = opts.env ?? process.env;
    let child;
    try {
      const plan = resolveSpawn(cmd, args, { env, cwd: opts.cwd });
      child = spawn(plan.file, plan.args, {
        cwd: opts.cwd,
        env,
        stdio: [opts.input !== undefined ? 'pipe' : 'ignore', 'pipe', 'pipe'],
        windowsHide: opts.windowsHide ?? true,
        windowsVerbatimArguments: plan.verbatim,
        detached: !IS_WIN,
      });
    } catch (e) {
      resolve({ exit: null, signal: null, timedOut: false, stdout: '', stderr: '', secs: 0, pid: undefined, error: e.message });
      return;
    }
    let out = '';
    let err = '';
    let timedOut = false;
    let exited = false;
    let exitCode = null;
    let exitSignal = null;
    let spawnError;
    let stdinError;
    let done = false;
    let timer = null;
    let grace = null;
    let log = null;
    if (opts.logFile) {
      fs.mkdirSync(path.dirname(opts.logFile), { recursive: true });
      log = fs.createWriteStream(opts.logFile, { flags: 'a' });
    }
    const finish = () => {
      if (done) return;
      done = true;
      if (timer) clearTimeout(timer);
      if (grace) clearTimeout(grace);
      child.stdout?.destroy();
      child.stderr?.destroy();
      log?.end();
      resolve({
        exit: spawnError && !child.pid ? null : exitCode,
        signal: exitSignal ?? null,
        timedOut,
        stdout: out,
        stderr: err,
        secs: Math.round((Date.now() - t0) / 100) / 10,
        pid: child.pid,
        ...(spawnError ? { error: spawnError } : {}),
        ...(stdinError ? { stdin_error: stdinError } : {}),
      });
    };
    const keep = (s, add) => { const n = s + add; return n.length > CAP ? n.slice(n.length - CAP) : n; };
    child.stdout.on('data', (d) => { out = keep(out, d.toString('utf8')); log?.write(d); });
    child.stderr.on('data', (d) => { err = keep(err, d.toString('utf8')); log?.write(d); });
    child.stdout.on('error', () => {});
    child.stderr.on('error', () => {});
    if (opts.input !== undefined) {
      child.stdin.on('error', (e) => { stdinError = e.code || e.message; });
      child.stdin.end(opts.input);
    }
    if (child.pid) opts.onSpawn?.(child.pid);
    if (opts.timeoutMs) {
      timer = setTimeout(() => {
        timer = null;
        if (exited) return;
        timedOut = true;
        treeKill(child.pid);
        if (!grace) grace = setTimeout(finish, drainMs);
      }, opts.timeoutMs);
    }
    child.on('error', (e) => {
      spawnError = e.message;
      if (!child.pid) finish();
    });
    child.on('exit', (code, signal) => {
      exited = true;
      exitCode = code;
      exitSignal = signal;
      if (timer) { clearTimeout(timer); timer = null; }
      if (grace) clearTimeout(grace);
      grace = setTimeout(finish, drainMs);
    });
    child.on('close', (code, signal) => {
      if (!exited) { exited = true; exitCode = code; exitSignal = signal; }
      finish();
    });
  });
}

let selfStart = null;
/** Epoch ms when this process started (Date.now() - process.uptime(), computed once; the real clock, never GSK_NOW). */
export function selfStartMs() {
  if (selfStart === null) selfStart = Math.round(Date.now() - process.uptime() * 1000);
  return selfStart;
}

const infoCache = new Map();

function queryProcess(pid) {
  if (IS_WIN) {
    const script = `$ErrorActionPreference='SilentlyContinue'; $p = Get-CimInstance Win32_Process -Filter 'ProcessId=${pid}'; if ($p -and $p.CreationDate) { '{0}|{1}' -f ([DateTimeOffset]$p.CreationDate).ToUnixTimeMilliseconds(), $p.ExecutablePath }`;
    const r = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], { encoding: 'utf8', windowsHide: true, timeout: 20000 });
    const m = /^(\d+)\|(.*)$/m.exec(r.stdout || '');
    return m ? { pid, start_ms: Number(m[1]), image: m[2].trim() || null } : null;
  }
  if (process.platform === 'linux') {
    try {
      const stat = fs.readFileSync(`/proc/${pid}/stat`, 'utf8');
      const ticks = Number(stat.slice(stat.lastIndexOf(')') + 2).split(' ')[19]);
      const btime = Number(/^btime (\d+)/m.exec(fs.readFileSync('/proc/stat', 'utf8'))[1]);
      let image = null;
      try { image = fs.readlinkSync(`/proc/${pid}/exe`); } catch { /* other user's process */ }
      return { pid, start_ms: Math.round((btime + ticks / 100) * 1000), image };
    } catch {
      return null;
    }
  }
  const r = spawnSync('ps', ['-o', 'lstart=', '-o', 'comm=', '-p', String(pid)], { encoding: 'utf8' });
  const m = /^\s*(\w{3} \w{3}\s+\d+ [\d:]+ \d{4})\s+(.*)$/m.exec(r.stdout || '');
  if (!m) return null;
  const t = Date.parse(m[1]);
  return Number.isNaN(t) ? null : { pid, start_ms: t, image: m[2].trim() || null };
}

/**
 * Start time and executable of a live PID, or null when it is not running or cannot be queried. Never kills anything.
 * Windows: Win32_Process CreationDate/ExecutablePath through powershell.exe (about 1-2 s, so results are cached for
 * maxAgeMs, default 30000). Linux: /proc. Elsewhere: ps.
 * @param {number} pid
 * @param {{maxAgeMs?: number}} [opts]
 * @returns {{pid: number, start_ms: number, image: string|null}|null}
 */
export function processInfo(pid, opts = {}) {
  if (!Number.isInteger(pid) || pid <= 0) return null;
  if (pid === process.pid) return { pid, start_ms: selfStartMs(), image: process.execPath };
  const hit = infoCache.get(pid);
  if (hit && Date.now() - hit.at < (opts.maxAgeMs ?? 30000)) return hit.info;
  const info = queryProcess(pid);
  infoCache.set(pid, { at: Date.now(), info });
  return info;
}

/**
 * Is `pid` still the process a PID file or lock recorded? Use it before any kill or liveness decision on a stored PID.
 * - 'dead': no such process.
 * - 'mismatch': a process runs under that PID but it started at another time (|start - start_ms| > toleranceMs), after
 *   `not_after_ms` (for example the lock file's mtime: the recorded holder existed then), or runs another image (basename).
 * - 'match': alive and every given fact agrees. 'unknown': alive, but its facts could not be read (treat as held; never kill).
 * @param {number} pid
 * @param {{start_ms?: number, start_utc?: string, not_after_ms?: number, image?: string, toleranceMs?: number, maxAgeMs?: number}} [expect]
 * @returns {'match'|'mismatch'|'dead'|'unknown'}
 */
export function pidMatches(pid, expect = {}) {
  if (!isPidAlive(pid)) return 'dead';
  const info = processInfo(pid, { maxAgeMs: expect.maxAgeMs });
  if (!info) return isPidAlive(pid) ? 'unknown' : 'dead';
  const tol = expect.toleranceMs ?? 2000;
  const start = typeof expect.start_ms === 'number' ? expect.start_ms : (expect.start_utc ? Date.parse(expect.start_utc) : NaN);
  if (!Number.isNaN(start) && Math.abs(info.start_ms - start) > tol) return 'mismatch';
  if (typeof expect.not_after_ms === 'number' && info.start_ms > expect.not_after_ms + tol) return 'mismatch';
  const base = (p) => path.win32.basename(String(p)).toLowerCase();
  if (expect.image && info.image && base(expect.image) !== base(info.image)) return 'mismatch';
  return 'match';
}

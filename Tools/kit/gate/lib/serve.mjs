// The resident headless editor (R2 without a human's GUI editor): `serve` boots `Unity -batchmode` with no -quit, waits
// for the Pipeline descriptor, polls editor_status and warms up; `unserve` stops it by PID (graceful taskkill, then
// /T /F after the grace period); `status` reports. Never `unity command quit` (it throws in edit mode on 0.8.0-exp.1),
// never a kill by name, and every stored PID is checked with pidMatches first (CONTRACTS §4.4, §1.6).
import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { note } from '../../lib/cli.mjs';
import { writeJsonAtomic } from '../../lib/jsonio.mjs';
import { readLockHolder, tryLock } from '../../lib/locks.mjs';
import { normalizeForCompare } from '../../lib/paths.mjs';
import { isPidAlive, pidMatches, processInfo, resolveSpawn, run, treeKill } from '../../lib/proc.mjs';
import { now, utcFileStamp, utcIso } from '../../lib/time.mjs';
import { presentPackages } from './asmdefs.mjs';
import { unityCliArgv } from './config.mjs';
import { resolveEditor } from './editor.mjs';
import { hasCompileErrors, infraCause, parseCompilerErrors } from './logscan.mjs';
import { addError, relU, setVerdict } from './result.mjs';
import { editorStatus, live, readDescriptor, safeModeCount } from './unitycli.mjs';
import {
  clientLockPath, dropStalePid, editorHoldsProject, readUnityPid, releaseLockByToken, removeDescriptorOf, servedState,
  snapshotEditorLogs, transferLockToEditor, unityLockPath,
} from './unitylock.mjs';
import { readText } from '../../lib/jsonio.mjs';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * Wait for a lock file. `onPoll` runs after each failed try; a truthy return ends the wait early as {ok:false, stopped}.
 * @param {string} file
 * @param {string} purpose
 * @param {number} waitMs
 * @param {number} pollMs
 * @param {() => any} [onPoll]
 */
async function waitLock(file, purpose, waitMs, pollMs, onPoll) {
  const t0 = Date.now();
  for (;;) {
    const r = tryLock(file, { purpose });
    if (r.ok) return r;
    const stopped = onPoll?.();
    if (stopped) return { ok: false, holder: r.holder, stopped };
    if (Date.now() - t0 >= waitMs) return r;
    await sleep(Math.min(pollMs, Math.max(50, waitMs - (Date.now() - t0))));
  }
}

/** True when unity.pid describes an editor that finished booting (ready_utc; older files: a port). */
const bootFinished = (pf) => Boolean(pf && (pf.ready_utc || Number.isInteger(pf.port)));

/**
 * Split a Windows command line the way CommandLineToArgvW does (quotes, doubled quotes, backslashes before a quote).
 * @param {string} line
 * @returns {string[]}
 */
export function splitWindowsCommandLine(line) {
  const s = String(line || '');
  const out = [];
  let i = 0;
  while (i < s.length) {
    while (i < s.length && (s[i] === ' ' || s[i] === '\t')) i++;
    if (i >= s.length) break;
    let arg = '';
    let quoted = false;
    while (i < s.length) {
      const c = s[i];
      if (c === '\\') {
        let n = 0;
        while (s[i] === '\\') { n++; i++; }
        if (s[i] === '"') {
          arg += '\\'.repeat(Math.floor(n / 2));
          if (n % 2) { arg += '"'; i++; }
        } else arg += '\\'.repeat(n);
        continue;
      }
      if (c === '"') {
        if (quoted && s[i + 1] === '"') { arg += '"'; i += 2; continue; }
        quoted = !quoted;
        i++;
        continue;
      }
      if (!quoted && (c === ' ' || c === '\t')) break;
      arg += c;
      i++;
    }
    out.push(arg);
  }
  return out;
}

/**
 * The argv of a running process (Windows: Win32_Process.CommandLine; Linux: /proc; elsewhere: ps), or null when it
 * cannot be read. Never kills anything.
 * @param {number} pid
 * @returns {string[]|null}
 */
export function processCommandLine(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return null;
  if (process.platform === 'win32') {
    const script = `$ErrorActionPreference='SilentlyContinue'; [Console]::OutputEncoding = [Text.Encoding]::UTF8; $p = Get-CimInstance Win32_Process -Filter 'ProcessId=${pid}'; if ($p -and $p.CommandLine) { $p.CommandLine }`;
    const r = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], { encoding: 'utf8', windowsHide: true, timeout: 20000 });
    const t = String(r.stdout || '').replace(/^﻿/, '').trim();
    return t ? splitWindowsCommandLine(t) : null;
  }
  if (process.platform === 'linux') {
    try {
      const a = fs.readFileSync(`/proc/${pid}/cmdline`, 'utf8').split('\0').filter(Boolean);
      return a.length ? a : null;
    } catch {
      return null;
    }
  }
  const r = spawnSync('ps', ['-o', 'args=', '-p', String(pid)], { encoding: 'utf8' });
  const t = String(r.stdout || '').trim();
  return t ? t.split(/\s+/) : null;
}

/**
 * Why a process is not the resident editor `serve` started for this project, or null when it is: its command line must
 * carry -batchmode, -projectPath U and -logFile <the log in unity.pid>. unity.pid is a file an agent can write, so a
 * self-consistent pid and start time alone (e.g. the human's GUI editor) never licenses a kill.
 * @param {object} ctx
 * @param {object} pf unity.pid
 * @param {string[]|null} argv
 */
export function residentProblem(ctx, pf, argv) {
  if (!argv) return 'its command line cannot be read';
  const low = argv.map((a) => a.toLowerCase());
  const value = (name) => { const i = low.indexOf(name); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : null; };
  if (!low.includes('-batchmode')) return 'it was not started with -batchmode (a GUI editor is never stopped by the gate)';
  const pp = value('-projectpath');
  if (!pp || normalizeForCompare(pp) !== normalizeForCompare(ctx.U)) return `its -projectPath is ${pp ? JSON.stringify(pp) : 'missing'}, not this project`;
  if (!pf.log) return 'unity.pid names no log file, so the process cannot be tied to a serve run';
  const lf = value('-logfile');
  if (!lf || normalizeForCompare(lf) !== normalizeForCompare(path.resolve(ctx.U, pf.log))) return `its -logFile is ${lf ? JSON.stringify(lf) : 'missing'}, not ${pf.log}`;
  return null;
}

async function waitDead(pid, ms) {
  const t0 = Date.now();
  while (isPidAlive(pid)) {
    if (Date.now() - t0 >= ms) return false;
    await sleep(250);
  }
  return true;
}

/** `serve`: boot a resident headless editor for this project and hand it the Unity lock. */
export async function cmdServe(ctx, res) {
  res.rung = 'R2';
  const s = servedState(ctx);
  if (s.state === 'unknown') return setVerdict(res, 'BLOCKED', { infra: 'unity_pid_unknown', message: `cannot verify pid ${s.pf.pid} from .harness/unity.pid; nothing was killed` });
  const already = (pf) => {
    res.route = 'warm';
    res.served = { pid: pf.pid, port: pf.port ?? null, started_utc: pf.started_utc, already: true };
    res.messages = [...(res.messages || []), `already serving (pid ${pf.pid})`];
    return setVerdict(res, 'PASS');
  };
  if (s.state === 'match' && bootFinished(s.pf)) return already(s.pf);
  if (s.state === 'mismatch' || s.state === 'dead') res.stale_pid = dropStalePid(ctx, s.pf, s.state).stale_pid;
  if (!presentPackages(ctx.U).has('com.unity.pipeline')) {
    return setVerdict(res, 'INFRA', { infra: 'no_pipeline_package', message: `com.unity.pipeline is not in Packages/manifest.json: the resident editor needs it (an approved human step: unity pipeline install --project-path "${ctx.U}")` });
  }
  if (!unityCliArgv()) return setVerdict(res, 'INFRA', { infra: 'no_cli', message: 'the Unity CLI (unity) is not installed: winget install Unity.CLI' });
  // Another serve may be booting (it holds unity.lock, then hands it to its editor): re-check unity.pid on every poll, so
  // this one reports "already serving" once that boot finishes instead of waiting out lockWaitMs.
  if (s.state === 'match') res.messages = [`a resident editor (pid ${s.pf.pid}) is still booting; waiting for it`];
  const lk = await waitLock(unityLockPath(ctx), 'serve', ctx.cfg.lockWaitMs, ctx.cfg.lockPollMs, () => {
    const cur = servedState(ctx);
    return cur.state === 'match' && bootFinished(cur.pf) ? cur.pf : null;
  });
  if (lk.stopped) return already(lk.stopped);
  if (!lk.ok) return setVerdict(res, 'BLOCKED', { infra: 'lock_timeout', message: `the Unity lock is held by ${lk.holder?.purpose || '?'} (pid ${lk.holder?.pid ?? '?'})` });
  const s2 = servedState(ctx);
  if (s2.state === 'match' && bootFinished(s2.pf)) {
    lk.release();
    return already(s2.pf);
  }
  let token = null;
  let pid = null;
  const fail = (verdict, opts) => {
    if (pid && isPidAlive(pid)) {
      treeKill(pid);
      // A killed editor never removes its own Pipeline descriptor.
      for (let i = 0; i < 20 && isPidAlive(pid); i++) Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 100);
    }
    if (pid) removeDescriptorOf(ctx, pid);
    try { fs.unlinkSync(ctx.paths.unityPid); } catch { /* not written */ }
    if (token) releaseLockByToken(ctx, token);
    else lk.release();
    return setVerdict(res, verdict, opts);
  };
  if (editorHoldsProject(ctx).held) return fail('BLOCKED', { infra: 'editor_holds_project', message: 'an editor already has the project open: use it through the Unity CLI (it is the warm editor) or ask the human to close it' });
  const editor = await resolveEditor(ctx);
  if (!editor.argv) return fail('INFRA', { infra: 'no_editor', message: editor.error });
  snapshotEditorLogs(ctx);
  fs.mkdirSync(ctx.paths.crashes, { recursive: true });
  let logFile = path.join(ctx.paths.logs, `resident_${utcFileStamp(now())}.log`);
  for (let i = 2; fs.existsSync(logFile); i++) logFile = path.join(ctx.paths.logs, `resident_${utcFileStamp(now())}-${i}.log`);
  res.log = relU(ctx, logFile);
  const args = [...editor.argv.slice(1), '-batchmode', '-projectPath', ctx.U, '-logFile', logFile, '-crash-report-folder', ctx.paths.crashes];
  const t0 = Date.now();
  let spawnError = null;
  try {
    const plan = resolveSpawn(editor.argv[0], args);
    const child = spawn(plan.file, plan.args, { cwd: ctx.U, detached: true, stdio: 'ignore', windowsHide: true, windowsVerbatimArguments: plan.verbatim });
    child.on('error', (e) => { spawnError = e.message; });
    child.unref();
    pid = child.pid ?? null;
  } catch (e) {
    spawnError = e.message;
  }
  if (!pid) return fail('INFRA', { infra: 'no_editor', message: `cannot start the editor: ${spawnError || 'no pid'}` });
  const info = processInfo(pid, { maxAgeMs: 0 });
  const process_start_utc = utcIso(new Date(info?.start_ms ?? t0));
  const image = info?.image || editor.exe || editor.argv[0];
  token = transferLockToEditor(ctx, { pid, process_start_utc, image });
  const pidFile = { pid, started_utc: utcIso(), project: ctx.U, port: null, mode: 'serve', process_start_utc, image, lock_token: token, log: relU(ctx, logFile) };
  writeJsonAtomic(ctx.paths.unityPid, pidFile);
  note(`gate: resident editor pid ${pid} booting (log ${res.log}); waiting for the Pipeline descriptor`);
  const deadline = t0 + ctx.cfg.serveTimeoutMs;
  let desc = null;
  for (;;) {
    if (!isPidAlive(pid)) {
      let log = '';
      try { log = readText(logFile); } catch { /* none */ }
      for (const e of parseCompilerErrors(log, { baseDir: ctx.U }).slice(0, 60)) addError(res, e);
      const ic = infraCause(log);
      if (ic && !hasCompileErrors(log)) return fail(ic.kind === 'blocked' ? 'BLOCKED' : 'INFRA', { infra: ic.reason, message: ic.line });
      return fail('FAIL', { message: `the resident editor exited during boot${spawnError ? ` (${spawnError})` : ''}${hasCompileErrors(log) ? ': compile errors at boot (fix them with check, then serve again)' : ''}` });
    }
    desc = readDescriptor(ctx);
    if (desc?.pid && isPidAlive(desc.pid)) break;
    if (Date.now() >= deadline) {
      let log = '';
      try { log = readText(logFile); } catch { /* none */ }
      for (const e of parseCompilerErrors(log, { baseDir: ctx.U }).slice(0, 60)) addError(res, e);
      return fail('FAIL', { message: `no Pipeline descriptor after ${Math.round(ctx.cfg.serveTimeoutMs / 1000)} s${hasCompileErrors(log) ? ' (compile errors at boot keep packages from loading: Safe Mode)' : ''}; the editor was stopped by PID` });
    }
    await sleep(ctx.cfg.pollMs);
  }
  if (desc.pid !== pid) res.messages = [`the descriptor names pid ${desc.pid}; the gate tracks the process it started (pid ${pid}) and its tree`];
  const lv = await live(ctx);
  if (!lv.live) return fail('FAIL', { message: `the resident editor did not become ready: ${lv.reason}${lv.warmup ? ` (warm-up: ${lv.warmup.detail || lv.warmup.exit})` : ''}` });
  writeJsonAtomic(ctx.paths.unityPid, { ...pidFile, port: desc.port ?? null, ready_utc: utcIso() });
  res.route = 'warm';
  res.served = { pid, port: desc.port ?? null, boot_secs: Math.round((Date.now() - t0) / 100) / 10, warmup_tries: lv.warmup?.tries ?? null };
  return setVerdict(res, 'PASS');
}

/** `unserve`: stop the served editor by PID (graceful, then tree-kill after the grace period). */
export async function cmdUnserve(ctx, res) {
  res.rung = 'R2';
  const pf = readUnityPid(ctx);
  if (!pf) {
    const d = readDescriptor(ctx);
    res.messages = ['not serving (no .harness/unity.pid)'];
    if (d?.pid && isPidAlive(d.pid)) res.messages.push(`a Pipeline editor (pid ${d.pid}) runs that the gate did not start: unserve never stops it`);
    return setVerdict(res, 'PASS');
  }
  const m = pidMatches(pf.pid, { start_utc: pf.process_start_utc, image: pf.image, maxAgeMs: 0 });
  if (m === 'unknown') return setVerdict(res, 'BLOCKED', { infra: 'unity_pid_unknown', message: `cannot verify pid ${pf.pid}; nothing was killed` });
  if (m === 'mismatch' || m === 'dead') {
    dropStalePid(ctx, pf, m);
    if (pf.lock_token) releaseLockByToken(ctx, pf.lock_token);
    res.stale_pid = pf.pid;
    res.messages = [`stale .harness/unity.pid (pid ${pf.pid} is ${m === 'dead' ? 'gone' : 'another process now'}); removed, nothing was killed`];
    return setVerdict(res, 'PASS');
  }
  // unity.pid is agent-writable: before any kill, the process must be the batchmode editor serve started for this project.
  const why = residentProblem(ctx, pf, processCommandLine(pf.pid));
  if (why) {
    return setVerdict(res, 'BLOCKED', { infra: 'not_resident_editor', message: `.harness/unity.pid names pid ${pf.pid}, but ${why}; nothing was killed (if that file is wrong, a human removes it)` });
  }
  const client = await waitLock(clientLockPath(ctx), 'unserve', ctx.cfg.lockWaitMs, ctx.cfg.lockPollMs);
  if (!client.ok) return setVerdict(res, 'BLOCKED', { infra: 'lock_timeout', message: `a gate job still uses the served editor (pid ${client.holder?.pid ?? '?'})` });
  const t0 = Date.now();
  let forced = false;
  try {
    if (process.platform === 'win32') await run('taskkill', ['/PID', String(pf.pid)], { timeoutMs: 15000 });
    else { try { process.kill(pf.pid, 'SIGTERM'); } catch { /* gone */ } }
    let gone = await waitDead(pf.pid, ctx.cfg.unserveGraceMs);
    if (!gone) {
      const again = pidMatches(pf.pid, { start_utc: pf.process_start_utc, image: pf.image, maxAgeMs: 0 });
      if (again === 'match') {
        forced = true;
        treeKill(pf.pid);
        gone = await waitDead(pf.pid, 15000);
      } else gone = again === 'dead' || again === 'mismatch';
    }
    if (!gone) return setVerdict(res, 'FAIL', { message: `pid ${pf.pid} is still running after taskkill /T /F` });
    try { fs.unlinkSync(ctx.paths.unityPid); } catch { /* gone */ }
    if (pf.lock_token) releaseLockByToken(ctx, pf.lock_token);
    // Only a graceful editor exit removes the Pipeline descriptor: after a forced stop it would name a dead (later reused) PID.
    const dp = readDescriptor(ctx)?.pid;
    const descriptorRemoved = removeDescriptorOf(ctx, pf.pid) || (dp && dp !== pf.pid && !isPidAlive(dp) ? removeDescriptorOf(ctx, dp) : false);
    res.stopped = { pid: pf.pid, forced, secs: Math.round((Date.now() - t0) / 100) / 10, descriptor_removed: descriptorRemoved };
    return setVerdict(res, 'PASS');
  } finally {
    client.release();
  }
}

/** `status`: what holds Unity, and is the warm editor ready. Always exit 0. */
export async function cmdStatus(ctx, res) {
  res.rung = 'R2';
  const s = servedState(ctx);
  const d = readDescriptor(ctx);
  const st = await editorStatus(ctx);
  const safe = unityCliArgv() && !st.ready ? await safeModeCount(ctx) : null;
  const holder = (f) => {
    const h = readLockHolder(f);
    return h ? { pid: h.pid, purpose: h.purpose, utc: h.utc } : null;
  };
  const locks = {
    unity: holder(unityLockPath(ctx)),
    unity_client: holder(clientLockPath(ctx)),
    dotnet: Array.from({ length: ctx.cfg.checkSlots }, (_, i) => holder(path.join(ctx.paths.locks, `dotnet-${i}.lock`))),
    core: Array.from({ length: ctx.cfg.coreSlots }, (_, i) => holder(path.join(ctx.paths.locks, `core-${i}.lock`))),
  };
  res.status = {
    served: s.state === 'match' ? { pid: s.pf.pid, port: s.pf.port ?? null, started_utc: s.pf.started_utc, log: s.pf.log ?? null } : null,
    unity_pid_state: s.state,
    descriptor: d ? { pid: d.pid, port: d.port, mode: d.mode } : null,
    editor: { reachable: st.reachable, ready: st.ready, status: st.status, reloading: st.reloading, compiling: st.compiling, modal_dialog: Boolean(st.dialog), reason: st.reason },
    safe_mode_instances: safe,
    project_lockfile: fs.existsSync(ctx.paths.lockfile),
    locks,
  };
  res.route = st.ready ? 'warm' : 'none';
  const lines = [
    `served: ${res.status.served ? `pid ${res.status.served.pid} port ${res.status.served.port}` : 'no'} (unity.pid ${s.state})`,
    `pipeline editor: ${st.ready ? 'ready' : st.reachable ? `${st.status}${st.reloading ? ' (reloading)' : ''}` : `not reachable (${st.reason})`}`,
    `unity.lock: ${locks.unity ? `${locks.unity.purpose} pid ${locks.unity.pid}` : 'free'}`,
  ];
  if (safe) lines.push(`Safe Mode instances: ${safe}`);
  res.messages = lines;
  return setVerdict(res, 'PASS');
}

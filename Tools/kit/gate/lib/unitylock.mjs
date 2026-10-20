// One writer per Unity project (P7): every compile/test/exec/serve/build takes U/Tools/.locks/unity.lock. While a
// resident editor is served, unity.lock names the editor process itself (so it goes stale when the editor dies), and gate
// jobs against that editor queue on unity-client.lock instead. Stored PIDs are checked with pidMatches before use.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { note } from '../../lib/cli.mjs';
import { readJsonOr, writeJsonAtomic } from '../../lib/jsonio.mjs';
import { readLockHolder, tryLock } from '../../lib/locks.mjs';
import { isPidAlive, pidMatches, processInfo } from '../../lib/proc.mjs';
import { now, utcFileStamp, utcIso } from '../../lib/time.mjs';
import { editorLogDir } from './config.mjs';
import { editorImageNames } from './editor.mjs';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

export const unityLockPath = (ctx) => path.join(ctx.paths.locks, 'unity.lock');
export const clientLockPath = (ctx) => path.join(ctx.paths.locks, 'unity-client.lock');

/** .harness/unity.pid as written by `serve`, or null. */
export function readUnityPid(ctx) {
  const pf = readJsonOr(ctx.paths.unityPid, null, { onInvalid: 'fallback' });
  return pf && Number.isInteger(pf.pid) && pf.pid > 0 ? pf : null;
}

/**
 * Is the served editor recorded in unity.pid still that process?
 * @returns {{state:'none'|'match'|'mismatch'|'dead'|'unknown', pf:object|null}}
 */
export function servedState(ctx) {
  const pf = readUnityPid(ctx);
  if (!pf) return { state: 'none', pf: null };
  return { state: pidMatches(pf.pid, { start_utc: pf.process_start_utc, image: pf.image }), pf };
}

/** Delete a stale unity.pid (mismatch or dead): nothing is killed. */
export function dropStalePid(ctx, pf, why) {
  try {
    fs.unlinkSync(ctx.paths.unityPid);
  } catch { /* already gone */ }
  note(`gate: removed stale .harness/unity.pid (pid ${pf?.pid}, ${why}); nothing was killed`);
  return { stale_pid: pf?.pid ?? null, reason: why };
}

/**
 * Acquire the right to drive Unity for one job.
 * @param {object} ctx
 * @param {string} purpose
 * @param {{waitMs?: number}} [opts]
 * @returns {Promise<{ok:true, served:boolean, pf:object|null, release:()=>void, stale:object|null} | {ok:false, blocked:string, holder:any, stale:object|null, detail?:string}>}
 */
export async function acquireUnity(ctx, purpose, opts = {}) {
  const waitMs = opts.waitMs ?? ctx.cfg.lockWaitMs;
  const t0 = Date.now();
  let announced = false;
  let stale = null;
  let holder = null;
  for (;;) {
    const s = servedState(ctx);
    if (s.state === 'unknown') return { ok: false, blocked: 'unity_pid_unknown', holder: s.pf, stale, detail: `cannot verify the served editor pid ${s.pf.pid}; nothing was killed` };
    if (s.state === 'mismatch' || s.state === 'dead') stale = dropStalePid(ctx, s.pf, s.state);
    if (s.state === 'match') {
      const r = tryLock(clientLockPath(ctx), { purpose });
      if (r.ok) return { ok: true, served: true, pf: s.pf, release: r.release, stale };
      holder = r.holder;
    } else {
      const r = tryLock(unityLockPath(ctx), { purpose });
      if (r.ok) return { ok: true, served: false, pf: null, release: r.release, stale };
      holder = r.holder;
    }
    const waited = Date.now() - t0;
    if (waited >= waitMs) return { ok: false, blocked: 'lock_timeout', holder, stale, detail: `Unity lock still held after ${Math.round(waited / 1000)} s by ${holder ? `${holder.purpose || '?'} (pid ${holder.pid})` : 'another gate'}` };
    if (!announced) {
      note(`gate: waiting for the Unity lock held by ${holder ? `${holder.purpose || '?'} (pid ${holder.pid})` : '?'}`);
      announced = true;
    }
    await sleep(Math.min(ctx.cfg.lockPollMs + Math.floor(Math.random() * 500), Math.max(50, waitMs - waited)));
  }
}

/**
 * Hand unity.lock to the served editor: the holder becomes the editor's pid, start time and image, so the lock goes stale
 * when that editor dies. Returns the new token (kept in unity.pid as lock_token).
 */
export function transferLockToEditor(ctx, info) {
  const token = `serve-${info.pid}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  writeJsonAtomic(unityLockPath(ctx), {
    pid: info.pid,
    host: os.hostname(),
    utc: utcIso(),
    purpose: 'serve',
    token,
    process_start_utc: info.process_start_utc,
    image: info.image,
  });
  return token;
}

/** Remove unity.lock only when it still carries this token. */
export function releaseLockByToken(ctx, token) {
  const h = readLockHolder(unityLockPath(ctx));
  if (!h || h.token !== token) return false;
  try {
    fs.unlinkSync(unityLockPath(ctx));
    return true;
  } catch {
    return false;
  }
}

/**
 * Does an editor (the human's GUI editor, or any other) hold the project? The Pipeline descriptor names a live Unity
 * editor (descriptorState 'live', or 'unknown' when its facts cannot be read), or Temp/UnityLockfile exists and cannot be
 * deleted (Windows). A leftover lockfile of a dead editor is deleted as a side effect, which is harmless (the KNOTWELL
 * gate's verified test). A descriptor whose PID is dead or now another process is stale and holds nothing.
 * @returns {{held:boolean, removedStale:boolean, descriptorPid?:number, staleDescriptor?:string}}
 */
export function editorHoldsProject(ctx) {
  const f = ctx.paths.lockfile;
  const ds = descriptorState(ctx);
  if (ds.state === 'live' || ds.state === 'unknown') return { held: true, removedStale: false, descriptorPid: ds.pid };
  const stale = ds.state === 'stale' ? ds.why : undefined;
  if (!fs.existsSync(f)) return { held: false, removedStale: false, staleDescriptor: stale };
  if (process.platform !== 'win32') return { held: true, removedStale: false };
  try {
    fs.unlinkSync(f);
    return { held: false, removedStale: true, staleDescriptor: stale };
  } catch {
    return { held: true, removedStale: false };
  }
}

/** The pid in the Pipeline descriptor, null when absent, or undefined when the file exists but does not parse. */
function descriptorPid(ctx) {
  let text;
  try {
    text = fs.readFileSync(ctx.paths.descriptor, 'utf8');
  } catch {
    return null;
  }
  try {
    const d = JSON.parse(text.replace(/^﻿/, ''));
    const pid = Number(d?.pid);
    return Number.isInteger(pid) && pid > 0 ? pid : undefined;
  } catch {
    return undefined;
  }
}

/**
 * Can the PID in Library/Pipeline/.unity-pipeline-port be trusted (CONTRACTS §1.6: a PID read from a file is trusted only
 * after a process check)? Only a graceful editor exit removes the descriptor, so a crashed or force-killed editor leaves
 * one behind whose PID Windows may later reuse.
 * - 'none': no descriptor. 'unreadable': it exists but does not parse (mid-rewrite): let the Unity CLI decide.
 * - 'dead': its PID is not running. 'stale': the PID runs another image, or started after the descriptor was written.
 * - 'live': a Unity editor that existed when the descriptor was written. 'unknown': alive, facts unreadable (treat as live).
 * The eval token is never read out.
 * @returns {{state:'none'|'unreadable'|'dead'|'stale'|'live'|'unknown', pid:number|null, why?:string}}
 */
export function descriptorState(ctx) {
  let st;
  try {
    st = fs.statSync(ctx.paths.descriptor);
  } catch {
    return { state: 'none', pid: null };
  }
  const pid = descriptorPid(ctx);
  if (pid === null) return { state: 'none', pid: null };
  if (pid === undefined) return { state: 'unreadable', pid: null };
  if (!isPidAlive(pid)) return { state: 'dead', pid };
  const info = processInfo(pid);
  if (!info) return isPidAlive(pid) ? { state: 'unknown', pid } : { state: 'dead', pid };
  const image = info.image ? path.win32.basename(String(info.image)).toLowerCase() : null;
  if (image && !editorImageNames(ctx).has(image)) return { state: 'stale', pid, why: `descriptor pid ${pid} is ${image} now, not a Unity editor (PID reuse)` };
  if (info.start_ms > st.mtimeMs + 2000) return { state: 'stale', pid, why: `descriptor pid ${pid} started after the descriptor was written (PID reuse)` };
  return { state: 'live', pid };
}

/** Delete the Pipeline descriptor when it still names `pid` and that process is gone (after unserve stopped it). */
export function removeDescriptorOf(ctx, pid) {
  if (!pid || isPidAlive(pid) || descriptorPid(ctx) !== pid) return false;
  try {
    fs.unlinkSync(ctx.paths.descriptor);
    return true;
  } catch {
    return false;
  }
}

const SNAPSHOT_KEEP = 10;
const SNAPSHOT_DIR = /^(\d{8}T\d{6}Z)(?:-(\d+))?$/;
/** Oldest first: by stamp, then by the numeric same-second suffix (so `-10` sorts after `-9`). */
const bySnapshotAge = (a, b) => {
  const x = SNAPSHOT_DIR.exec(a);
  const y = SNAPSHOT_DIR.exec(b);
  if (x[1] !== y[1]) return x[1] < y[1] ? -1 : 1;
  return Number(x[2] || 1) - Number(y[2] || 1);
};

/**
 * Before an editor (re)launch, copy the GUI Editor.log and Editor-prev.log to U/Logs/sessions/<stamp>/ (with a
 * snapshot.json of their sizes and mtimes). Skipped when both files are unchanged since the newest snapshot, and only the
 * newest SNAPSHOT_KEEP snapshots are kept, so long unattended runs do not fill the disk.
 */
export function snapshotEditorLogs(ctx) {
  const dir = editorLogDir();
  if (!dir) return null;
  const files = ['Editor.log', 'Editor-prev.log'].filter((f) => fs.existsSync(path.join(dir, f)));
  if (!files.length) return null;
  const facts = {};
  for (const f of files) {
    try {
      const s = fs.statSync(path.join(dir, f));
      facts[f] = { size: s.size, mtimeMs: Math.round(s.mtimeMs) };
    } catch { /* raced */ }
  }
  let existing = [];
  try {
    existing = fs.readdirSync(ctx.paths.sessions, { withFileTypes: true }).filter((e) => e.isDirectory() && SNAPSHOT_DIR.test(e.name)).map((e) => e.name).sort(bySnapshotAge);
  } catch { /* no snapshots yet */ }
  const newest = existing.at(-1);
  if (newest) {
    const prev = readJsonOr(path.join(ctx.paths.sessions, newest, 'snapshot.json'), null, { onInvalid: 'fallback' });
    if (prev && JSON.stringify(prev.files) === JSON.stringify(facts)) return path.join(ctx.paths.sessions, newest);
  }
  const stamp = utcFileStamp(now());
  // Never reuse a pruned same-second name: the suffix only grows, so age order stays true.
  const used = existing.map((n) => SNAPSHOT_DIR.exec(n)).filter((m) => m[1] === stamp).map((m) => Number(m[2] || 1));
  let next = used.length ? Math.max(...used) + 1 : 1;
  let name = next === 1 ? stamp : `${stamp}-${next}`;
  while (fs.existsSync(path.join(ctx.paths.sessions, name))) name = `${stamp}-${++next}`;
  const dest = path.join(ctx.paths.sessions, name);
  try {
    fs.mkdirSync(dest, { recursive: true });
    for (const f of files) fs.copyFileSync(path.join(dir, f), path.join(dest, f));
    writeJsonAtomic(path.join(dest, 'snapshot.json'), { utc: utcIso(), files: facts });
  } catch (e) {
    note(`gate: could not copy editor logs: ${e.message}`);
    return null;
  }
  for (const old of [...existing, name].sort(bySnapshotAge).slice(0, -SNAPSHOT_KEEP)) {
    try { fs.rmSync(path.join(ctx.paths.sessions, old), { recursive: true, force: true }); } catch { /* in use */ }
  }
  return dest;
}

/** The editor log to read for `error CS` lines when an editor holds the project: the served log, else the GUI Editor.log. */
export function currentEditorLog(ctx) {
  const pf = readUnityPid(ctx);
  if (pf?.log) {
    const abs = path.resolve(ctx.U, pf.log);
    if (fs.existsSync(abs)) return abs;
  }
  const dir = editorLogDir();
  const gui = dir ? path.join(dir, 'Editor.log') : null;
  return gui && fs.existsSync(gui) ? gui : null;
}

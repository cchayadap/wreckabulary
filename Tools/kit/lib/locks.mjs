// Cross-process file locks (O_EXCL create) and N-slot pools, with safe stale-lock breaking.
// Contract: CONTRACTS.md §1.6 (locks) and §2 (lib API). Never kill anything here: a lock only records who holds it.
//
// A lock file holds `lockHolder` JSON (harness.schema.json): {pid, host, utc, purpose, token, process_start_utc, image}.
// It is stale when (same host only):
//   - its PID is dead, or
//   - its PID is alive but is another process (Windows reuses PIDs): checked with pidMatches() on the holder's
//     process_start_utc and image, and "that process started after the lock file was written" (mtime), once the lock is
//     older than pidCheckAfterMs (default 15 s; the check starts powershell on Windows, so young locks skip it), or
//   - it is unreadable and older than staleMs (default 10 min).
// Breaking is serialised by a `<lock>.break` O_EXCL mutex, and the breaker re-reads the holder under it, so two
// contenders can never both break and both take the lock. Windows EPERM/EACCES/EBUSY on create (a lock being deleted)
// means "held, retry", never a crash.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { isPidAlive, pidMatches, selfStartMs } from './proc.mjs';
import { utcIso } from './time.mjs';

export { isPidAlive };

const sleepSync = (ms) => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const BUSY = new Set(['EPERM', 'EACCES', 'EBUSY']);
const BREAK_STALE_MS = 10000;
const newToken = () => `${process.pid}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;

/** @param {string} lockPath @returns {{pid:number, host:string, utc:string, purpose:string, token:string, process_start_utc?:string, image?:string}|null} */
export function readLockHolder(lockPath) {
  try {
    return JSON.parse(fs.readFileSync(lockPath, 'utf8'));
  } catch {
    return null;
  }
}

function holderInfo(purpose, token) {
  return {
    pid: process.pid,
    host: os.hostname(),
    utc: utcIso(),
    purpose: purpose || '',
    token,
    process_start_utc: utcIso(new Date(selfStartMs())),
    image: process.execPath,
  };
}

/** @returns {'ok'|'exists'|'busy'} */
function createLockFile(lockPath, info) {
  let fd;
  try {
    fd = fs.openSync(lockPath, 'wx');
  } catch (e) {
    if (e.code === 'EEXIST') return 'exists';
    if (BUSY.has(e.code)) return 'busy';
    throw e;
  }
  try {
    fs.writeSync(fd, JSON.stringify(info));
  } catch (e) {
    try { fs.closeSync(fd); } catch { /* already closed */ }
    try { fs.unlinkSync(lockPath); } catch { /* best effort */ }
    throw e;
  }
  fs.closeSync(fd);
  return 'ok';
}

function unlinkRetry(p) {
  for (let i = 0; i < 10; i++) {
    try {
      fs.unlinkSync(p);
      return true;
    } catch (e) {
      if (e.code === 'ENOENT') return false;
      if (!BUSY.has(e.code)) return false;
      sleepSync(20);
    }
  }
  return false;
}

/** @returns {'held'|'stale'|'gone'} */
function lockState(lockPath, holder, opts) {
  let st;
  try {
    st = fs.statSync(lockPath);
  } catch (e) {
    return e.code === 'ENOENT' ? 'gone' : 'held';
  }
  const staleMs = opts.staleMs ?? 10 * 60 * 1000;
  if (!holder || !Number.isInteger(holder.pid)) return Date.now() - st.mtimeMs > staleMs ? 'stale' : 'held';
  if (holder.host !== os.hostname()) return 'held';
  if (!isPidAlive(holder.pid)) return 'stale';
  if (holder.pid !== process.pid && Date.now() - st.mtimeMs < (opts.pidCheckAfterMs ?? 15000)) return 'held';
  const m = pidMatches(holder.pid, { start_utc: holder.process_start_utc, not_after_ms: st.mtimeMs, image: holder.image });
  return m === 'mismatch' || m === 'dead' ? 'stale' : 'held';
}

const sameHolder = (a, b) => (!a && !b) || (Boolean(a) && Boolean(b) && a.token === b.token && a.pid === b.pid);

/** Remove a stale lock under the `<lock>.break` mutex. @returns {boolean} true when the caller should retry the create. */
function breakStale(lockPath, seen, opts) {
  const breakPath = `${lockPath}.break`;
  const token = newToken();
  const r = createLockFile(breakPath, holderInfo('break', token));
  if (r !== 'ok') {
    if (r === 'exists') {
      // A breaker that died between two sync calls leaves .break behind; clear it once it is clearly old.
      try {
        if (Date.now() - fs.statSync(breakPath).mtimeMs > BREAK_STALE_MS) unlinkRetry(breakPath);
      } catch { /* gone already */ }
    }
    return false;
  }
  try {
    const now = readLockHolder(lockPath);
    if (!sameHolder(seen, now)) return true; // someone else broke or released it meanwhile: just retry the create
    const state = lockState(lockPath, now, opts);
    if (state === 'gone') return true;
    if (state !== 'stale') return false;
    unlinkRetry(lockPath);
    return true;
  } finally {
    if (readLockHolder(breakPath)?.token === token) unlinkRetry(breakPath);
  }
}

/**
 * Try once to take the lock (breaking a stale one, see the file header). Never throws for contention.
 * @param {string} lockPath
 * @param {{purpose?: string, staleMs?: number, pidCheckAfterMs?: number}} [opts]
 * @returns {{ok: true, release: () => boolean, path: string} | {ok: false, holder: object|null, path: string, busy?: true}}
 */
export function tryLock(lockPath, opts = {}) {
  fs.mkdirSync(path.dirname(lockPath), { recursive: true });
  let holder = null;
  for (let attempt = 0; attempt < 3; attempt++) {
    const token = newToken();
    const r = createLockFile(lockPath, holderInfo(opts.purpose, token));
    if (r === 'ok') {
      const release = () => {
        let h = null;
        for (let i = 0; i < 5 && !h; i++) {
          h = readLockHolder(lockPath);
          if (!h && !fs.existsSync(lockPath)) return false;
          if (!h) sleepSync(10);
        }
        if (!h || h.token !== token) return false;
        return unlinkRetry(lockPath);
      };
      return { ok: true, release, path: lockPath };
    }
    holder = readLockHolder(lockPath);
    if (r === 'busy') return { ok: false, holder, path: lockPath, busy: true };
    const state = lockState(lockPath, holder, opts);
    if (state === 'gone') continue;
    if (state === 'held' || !breakStale(lockPath, holder, opts)) return { ok: false, holder, path: lockPath };
  }
  return { ok: false, holder, path: lockPath };
}

function timeoutError(lockPath, waitMs) {
  const err = new Error(`lock timeout after ${waitMs} ms: ${lockPath} held by ${JSON.stringify(readLockHolder(lockPath))}`);
  err.code = 'ELOCKTIMEOUT';
  err.holder = readLockHolder(lockPath);
  return err;
}

/**
 * Wait up to waitMs for the lock (polling every pollMs). Rejects with code ELOCKTIMEOUT.
 * @param {string} lockPath
 * @param {{waitMs?: number, pollMs?: number, purpose?: string, staleMs?: number, pidCheckAfterMs?: number}} [opts]
 * @returns {Promise<{release: () => boolean, path: string}>}
 */
export async function lock(lockPath, opts = {}) {
  const waitMs = opts.waitMs ?? 0;
  const pollMs = opts.pollMs ?? 250;
  const t0 = Date.now();
  for (;;) {
    const r = tryLock(lockPath, opts);
    if (r.ok) return r;
    if (Date.now() - t0 >= waitMs) throw timeoutError(lockPath, waitMs);
    await sleep(pollMs);
  }
}

/**
 * Run fn while holding the lock; always releases.
 * @template T
 * @param {string} lockPath
 * @param {() => Promise<T>|T} fn
 * @param {{waitMs?: number, pollMs?: number, purpose?: string, staleMs?: number, pidCheckAfterMs?: number}} [opts]
 * @returns {Promise<T>}
 */
export async function withLock(lockPath, fn, opts = {}) {
  const l = await lock(lockPath, opts);
  try {
    return await fn();
  } finally {
    l.release();
  }
}

/**
 * Synchronous variant for short critical sections (ledger appends). Busy-waits with a sleep, up to waitMs.
 * @template T
 * @param {string} lockPath
 * @param {() => T} fn
 * @param {{waitMs?: number, pollMs?: number, purpose?: string, staleMs?: number, pidCheckAfterMs?: number}} [opts]
 * @returns {T}
 */
export function withLockSync(lockPath, fn, opts = {}) {
  const waitMs = opts.waitMs ?? 10000;
  const pollMs = opts.pollMs ?? 50;
  const t0 = Date.now();
  let r;
  for (;;) {
    r = tryLock(lockPath, opts);
    if (r.ok) break;
    if (Date.now() - t0 >= waitMs) throw timeoutError(lockPath, waitMs);
    sleepSync(pollMs);
  }
  try {
    return fn();
  } finally {
    r.release();
  }
}

/**
 * Take the first free slot of an N-slot pool: `<dir>/<prefix>-0.lock` … `<prefix>-(n-1).lock`.
 * @param {string} dir
 * @param {string} prefix
 * @param {number} n
 * @param {{waitMs?: number, pollMs?: number, purpose?: string, staleMs?: number, pidCheckAfterMs?: number}} [opts]
 * @returns {Promise<{slot: number, release: () => boolean, path: string}>}
 */
export async function lockSlot(dir, prefix, n, opts = {}) {
  const waitMs = opts.waitMs ?? 0;
  const pollMs = opts.pollMs ?? 250;
  const t0 = Date.now();
  for (;;) {
    for (let i = 0; i < n; i++) {
      const r = tryLock(path.join(dir, `${prefix}-${i}.lock`), opts);
      if (r.ok) return { slot: i, release: r.release, path: r.path };
    }
    if (Date.now() - t0 >= waitMs) throw timeoutError(path.join(dir, `${prefix}-*.lock`), waitMs);
    await sleep(pollMs);
  }
}

/**
 * Does an editor hold the Unity project at U? Missing `Temp/UnityLockfile` → false. On Windows a running editor keeps
 * the file open without sharing, so opening it read-write fails (EBUSY/EPERM/EACCES → held), while a leftover file of a
 * crashed editor opens fine (→ not held). Elsewhere the file's existence counts as held. Read-only: nothing is deleted.
 * @param {string} U Unity project root
 * @returns {boolean}
 */
export function unityLockHeld(U) {
  const f = path.join(U, 'Temp', 'UnityLockfile');
  if (!fs.existsSync(f)) return false;
  if (process.platform !== 'win32') return true;
  let fd;
  try {
    fd = fs.openSync(f, 'r+');
  } catch (e) {
    return e.code !== 'ENOENT';
  }
  try { fs.closeSync(fd); } catch { /* closed */ }
  return false;
}

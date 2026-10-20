// Stop hook, OPT-IN ONLY (registered by `kit autonomous on` in .claude/settings.local.json). Inert unless
// .harness/run.json holds an active window. Allows the stop (exit 0, no output) in every case of ARCHITECTURE §6.4;
// otherwise continues the session with labelled additionalContext naming the next action. An idle detector writes
// .harness/STALLED after three stops with no file progress and then lets the stop through (no quota burn loop).
// Only the owner can override it (ALLOW_STOP_OWNER comes from the owner_inbox hook alone).
import fs from 'node:fs';
import path from 'node:path';
import { now, utcIso, parseUtc } from '../lib/time.mjs';
import { readJsonOr, writeJsonAtomic } from '../lib/jsonio.mjs';
import { sha256Json, treeHash, TREE_ROOTS } from '../lib/hash.mjs';
import { isMain } from '../lib/cli.mjs';
import {
  runHook, readRun, runStatus, exists, mtimeMs, sizeOf, safeText, readGates, listAlerts, unackedInbox, nextAction,
  gitHead, unityRoot, isObj, clip,
} from './lib/harness.mjs';

export const IDLE_STALL = 3;

/** The progress signature (Stop fixture description): [git HEAD or treeHash(U), mtime DEVLOG, mtime gates.jsonl, mtime TODO, size touched]. */
export function progressSignature(ws, sessionId) {
  const head = gitHead(ws);
  let tree = null;
  if (!head) {
    try { tree = treeHash(unityRoot(ws), TREE_ROOTS); } catch { tree = null; }
  }
  const sid = typeof sessionId === 'string' && /^[A-Za-z0-9._-]+$/.test(sessionId) ? sessionId : 'unknown';
  return [
    head ?? tree,
    mtimeMs(path.join(ws, 'Docs', 'DEVLOG.md')),
    mtimeMs(path.join(ws, 'Docs', 'qa', 'gates.jsonl')),
    mtimeMs(path.join(ws, 'Docs', 'TODO.md')),
    sizeOf(path.join(ws, '.harness', 'touched', `${sid}.txt`)),
  ];
}

/** Wrap-up PASS: Docs/qa/gates.json milestone "wrapup" has status closed. */
export function wrapupClosed(ws) {
  const g = readGates(ws);
  return Boolean(g?.milestones?.some((m) => isObj(m) && m.id === 'wrapup' && m.status === 'closed'));
}

/**
 * Decide one Stop event.
 * @returns {{allow: boolean, reason: string, context?: string}}
 */
export function decide(input, ws, t = now()) {
  const H = (...p) => path.join(ws, '.harness', ...p);
  const run = readRun(ws);
  if (!run) return { allow: true, reason: 'no run.json' };
  const rs = runStatus(run, t);
  if (!rs.active) return { allow: true, reason: rs.expired ? 'window over' : 'run inactive' };
  if (exists(H('ALLOW_STOP_OWNER'))) return { allow: true, reason: 'owner stop' };
  if (exists(H('PAUSE'))) return { allow: true, reason: 'paused' };
  const wait = parseUtc((safeText(H('WAIT_UNTIL')) || '').trim());
  if (wait && wait.getTime() > t.getTime()) return { allow: true, reason: `waiting until ${utcIso(wait)}` };
  if ((Array.isArray(input.background_tasks) && input.background_tasks.length) || (Array.isArray(input.session_crons) && input.session_crons.length)) {
    return { allow: true, reason: 'background work will wake the session' };
  }
  const agentStop = exists(H('ALLOW_STOP'));
  if (agentStop && wrapupClosed(ws)) return { allow: true, reason: 'ALLOW_STOP after wrap-up PASS' };
  if (listAlerts(ws).some((a) => a.name === 'disk_emergency')) return { allow: true, reason: 'disk emergency' };

  const sid = typeof input.session_id === 'string' ? input.session_id : null;
  const sig = sha256Json(progressSignature(ws, sid));
  const prev = readJsonOr(H('stop_guard.json'), null, { onInvalid: 'fallback' });
  const same = isObj(prev) && prev.sig === sig && (!prev.session_id || !sid || prev.session_id === sid);
  const idle = same ? (Number.isInteger(prev.idle) ? prev.idle : 0) + 1 : 0;
  const blocks = isObj(prev) && Number.isInteger(prev.blocks) ? prev.blocks : 0;
  const state = { ...(sid ? { session_id: sid } : {}), sig, idle, last_utc: utcIso(t), blocks };
  if (!same && exists(H('STALLED'))) { try { fs.rmSync(H('STALLED'), { force: true }); } catch { /* ignore */ } }
  if (idle >= IDLE_STALL) {
    writeJsonAtomic(H('stop_guard.json'), state);
    const msg = typeof input.last_assistant_message === 'string' ? input.last_assistant_message : '';
    writeJsonAtomic(H('STALLED'), { utc: utcIso(t), ...(sid ? { session_id: sid } : {}), last_message: msg.slice(0, 500) });
    return { allow: true, reason: `stalled: ${idle} stops without file progress` };
  }
  if (input.stop_hook_active === true && idle >= 1) {
    writeJsonAtomic(H('stop_guard.json'), state);
    return { allow: true, reason: 'chained stop without progress' };
  }
  state.blocks = blocks + 1;
  writeJsonAtomic(H('stop_guard.json'), state);
  const na = nextAction(ws, t);
  const inbox = unackedInbox(ws).length;
  const extra = agentStop ? ' .harness/ALLOW_STOP is honoured only after gate_close wrapup PASS.' : '';
  const context = `Not done (window ends ${rs.window_end_utc}). Next: ${clip(na.detail, 800)}. Owner inbox: ${inbox} new. Rules: HARNESS_RULES §1.${extra}`;
  return { allow: false, reason: 'continue', context };
}

export async function main() {
  await runHook('keep_working', (input, { ws, invalid }) => {
    if (invalid) return null;
    const d = decide(input, ws);
    if (d.allow) return null;
    return { stdout: { hookSpecificOutput: { hookEventName: 'Stop', additionalContext: d.context } } };
  });
}

if (isMain(import.meta.url)) await main();

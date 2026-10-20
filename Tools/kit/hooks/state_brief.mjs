// SessionStart hook (startup|resume|clear|compact): builds .harness/brief.md (≤ 9,500 chars, under the 10k
// additionalContext cap) and prints it as additionalContext; records .harness/session.json for the supervisor.
// Never blocks. ARCHITECTURE §6.3; CONTRACTS.md §5.2.
import path from 'node:path';
import { now, utcIso } from '../lib/time.mjs';
import { readJsonOr, writeJsonAtomic, writeTextAtomic } from '../lib/jsonio.mjs';
import { isMain } from '../lib/cli.mjs';
import {
  runHook, readRun, runStatus, listAlerts, unackedInbox, parseTodo, safeText, lastDevlogEntry, readGates, currentMilestone,
  latestVerdicts, newestWaveManifest, nextAction, clip, exists, isObj,
} from './lib/harness.mjs';

export const BRIEF_MAX = 9500;
const SKILL_CAP = 4;

/** Skills to re-invoke after compaction: game-studio-run plus the first ≤ 4 of run.json.skills, else of the running wave. */
export function reinvokeSkills(ws) {
  const run = readRun(ws);
  let list = Array.isArray(run?.skills) ? run.skills : [];
  if (!list.length) {
    const w = newestWaveManifest(ws);
    const running = w && [...(w.manifest.units || []), ...(w.manifest.contracts || [])].some((u) => isObj(u) && ['todo', 'running', 'failed'].includes(u.status));
    if (running && Array.isArray(w.manifest.skills)) list = w.manifest.skills;
  }
  const seen = new Set(['game-studio-run']);
  const out = [];
  for (const s of list) {
    if (typeof s !== 'string' || !/^[a-z0-9][a-z0-9-]*$/.test(s) || seen.has(s)) continue;
    seen.add(s);
    out.push(s);
    if (out.length >= SKILL_CAP) break;
  }
  return ['game-studio-run', ...out];
}

/** Build the brief text (markdown). Sections are individually capped; the whole is cut to BRIEF_MAX. */
export function buildBrief(ws, source, t = now()) {
  const L = [];
  L.push(`# Harness brief (${utcIso(t)}, source: ${source})`);
  if (source === 'compact') L.push(`Re-invoke skills: ${reinvokeSkills(ws).join(', ')}`);
  L.push('Rules: Docs/kit/HARNESS_RULES.md (start of session: §1). Milestones close only through node Tools/kit/harness/gate_close.mjs <M>.');

  const na = nextAction(ws, t);
  L.push('', `## Next action`, `${na.kind}: ${clip(na.detail, 600)}`);

  const run = readRun(ws);
  const rs = runStatus(run, t);
  L.push('', '## Run window');
  if (!run) L.push('No .harness/run.json: not an autonomous run (keep_working is inert).');
  else if (rs.active) L.push(`Active run ${rs.name ?? ''}: window ends ${rs.window_end_utc} (${rs.elapsed_pct ?? '?'}% elapsed, ${rs.remaining_min ?? '?'} min left; freeze at ${rs.freeze_at_pct}%${rs.freeze ? ', FREEZE NOW' : ''}).`);
  else L.push(`Run ${rs.name ?? ''} is not armed (${rs.expired ? `window ended ${rs.window_end_utc}` : 'active=false'}).`);

  const flags = [];
  if (exists(path.join(ws, '.harness', 'PAUSE'))) flags.push('PAUSE: the owner paused the run. Read-only work only until the owner types OWNER: resume.');
  if (exists(path.join(ws, '.harness', 'ALLOW_STOP_OWNER'))) flags.push('ALLOW_STOP_OWNER: the owner said stop. Wrap up and stop.');
  const stalled = readJsonOr(path.join(ws, '.harness', 'STALLED'), null, { onInvalid: 'fallback' });
  if (exists(path.join(ws, '.harness', 'STALLED'))) flags.push(`STALLED${isObj(stalled) && stalled.utc ? ` since ${stalled.utc}` : ''}: three stops without file progress${isObj(stalled) && stalled.last_message ? ` (last: "${clip(stalled.last_message, 200)}")` : ''}. Change approach or ask the owner for a human step.`);
  const wait = (safeText(path.join(ws, '.harness', 'WAIT_UNTIL')) || '').trim();
  if (wait) flags.push(`WAIT_UNTIL ${clip(wait, 40)}: a usage-limit reset is pending.`);
  const alerts = listAlerts(ws);
  if (flags.length || alerts.length) {
    L.push('', '## Alerts and owner controls');
    for (const f of flags) L.push(`- ${f}`);
    for (const a of alerts.slice(0, 12)) L.push(`- alert ${a.name} (${a.level})${a.message ? `: ${clip(a.message, 200)}` : ''}${a.name === 'secret_in_prompt' ? ' The owner should rotate that key.' : ''}`);
  }

  const inbox = unackedInbox(ws);
  if (inbox.length) {
    L.push('', `## Owner inbox: ${inbox.length} unacknowledged`);
    for (const e of inbox.slice(0, 10)) L.push(`- #${e.id} ${e.utc}: ${clip(e.text.replace(/\s+/g, ' '), 300)}`);
    if (inbox.length > 10) L.push(`- … ${inbox.length - 10} more in Docs/OWNER_INBOX.md`);
    L.push('Ack each one in Docs/owner_acks.md (- #<n> <UTC> <what you did>); never edit the inbox.');
  }

  const gates = readGates(ws);
  const cur = currentMilestone(gates);
  if (cur) {
    const latest = latestVerdicts(ws);
    const rows = (cur.requires || []).filter(isObj);
    const bad = rows.filter((r) => latest.get(r.id)?.verdict && latest.get(r.id).verdict !== 'PASS');
    const none = rows.filter((r) => !latest.get(r.id) && r.evidence !== 'human' && r.evidence !== 'file');
    L.push('', `## Milestone ${cur.id}: ${clip(cur.title, 80)}${cur.time_box_utc ? ` (time box ${cur.time_box_utc})` : ''}`);
    L.push(`${rows.length - bad.length - none.length}/${rows.length} rows without a failing or missing verdict.`);
    for (const r of bad.slice(0, 10)) L.push(`- ${r.id} ${latest.get(r.id).verdict}: ${clip(r.what, 120)}`);
    if (none.length) L.push(`- no verdict yet: ${none.slice(0, 12).map((r) => r.id).join(', ')}`);
  }

  const todo = parseTodo(safeText(path.join(ws, 'Docs', 'TODO.md')) || '').filter((x) => !x.done);
  if (todo.length) {
    L.push('', `## TODO (top 5 of ${todo.length})`);
    for (const x of todo.slice(0, 5)) L.push(`- ${clip(x.id, 40)} ${clip(x.text, 200)}`);
  }

  const dev = lastDevlogEntry(ws);
  if (dev) L.push('', '## Last DEVLOG entry', `${dev.utc} ${clip(dev.title, 200)}`);

  const wave = newestWaveManifest(ws);
  if (wave) {
    const m = wave.manifest;
    const counts = {};
    for (const u of [...(m.contracts || []), ...(m.units || [])].filter(isObj)) counts[u.status] = (counts[u.status] || 0) + 1;
    L.push('', `## Wave ${clip(m.wave, 60)} (${clip(m.milestone, 20)})`, Object.entries(counts).map(([k, v]) => `${k} ${v}`).join(', ') || 'no units');
  }

  const cp = readJsonOr(path.join(ws, '.harness', 'checkpoint.json'), null, { onInvalid: 'fallback' });
  if (isObj(cp) && (source === 'compact' || source === 'resume')) L.push('', '## Checkpoint', `${cp.utc ?? ''} wave ${cp.wave ?? 'none'}; next: ${clip(cp.next_action?.detail ?? '', 300)}`);

  let text = L.join('\n') + '\n';
  if (text.length > BRIEF_MAX) text = text.slice(0, BRIEF_MAX - 60) + '\n… (brief cut at 9,500 characters; see Docs/)\n';
  return text;
}

export async function main() {
  await runHook('state_brief', (input, { ws }) => {
    const t = now();
    const source = ['startup', 'resume', 'clear', 'compact', 'fork'].includes(input.source) ? input.source : 'startup';
    const brief = buildBrief(ws, source, t);
    try { writeTextAtomic(path.join(ws, '.harness', 'brief.md'), brief); } catch { /* the brief still goes to stdout */ }
    const sid = typeof input.session_id === 'string' && input.session_id ? input.session_id : null;
    if (sid) {
      const file = path.join(ws, '.harness', 'session.json');
      const prev = readJsonOr(file, null, { onInvalid: 'fallback' });
      const run = readRun(ws);
      const doc = {
        session_id: sid,
        ...(typeof run?.name === 'string' && run.name ? { name: run.name } : {}),
        started_utc: isObj(prev) && prev.session_id === sid && typeof prev.started_utc === 'string' && source !== 'startup' ? prev.started_utc : utcIso(t),
        source,
        ...(typeof input.transcript_path === 'string' ? { transcript_path: input.transcript_path } : {}),
        cwd: typeof input.cwd === 'string' ? input.cwd : ws,
        entrypoint: process.env.CLAUDE_CODE_ENTRYPOINT ?? null,
        updated_utc: utcIso(t),
      };
      try { writeJsonAtomic(file, doc); } catch { /* best effort */ }
    }
    return { stdout: { hookSpecificOutput: { hookEventName: 'SessionStart', additionalContext: brief } } };
  });
}

if (isMain(import.meta.url)) await main();

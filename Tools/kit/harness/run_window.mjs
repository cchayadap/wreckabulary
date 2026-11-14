// The autonomous run window: .harness/run.json arms keep_working (ARCHITECTURE §6.4 layer 2, §6.5).
// CLI: node Tools/kit/harness/run_window.mjs status | start --hours N [--name s] [--skills a,b] [--freeze-at P] | stop [--json]
// Result: {active, started_utc, window_end_utc, elapsed_pct, remaining_min, freeze, …}. Exit 0; 2 usage, and 2 with
// {ok:false, blocked:"owner_stop"} when `start` finds .harness/ALLOW_STOP_OWNER (only `OWNER: resume` clears it).
import fs from 'node:fs';
import path from 'node:path';
import { now, utcIso, addMinutes } from '../lib/time.mjs';
import { writeJsonAtomic } from '../lib/jsonio.mjs';
import { findWorkspace } from '../lib/paths.mjs';
import { EXIT, CliError, UsageError, parseCli, emit, isMain, runCli } from '../lib/cli.mjs';
import { readRun, runStatus, exists } from '../hooks/lib/harness.mjs';

const SKILL_RE = /^[a-z0-9][a-z0-9-]*$/;

function statusOf(ws, t) {
  const run = readRun(ws);
  const s = runStatus(run, t);
  return {
    active: s.active,
    started_utc: s.started_utc,
    window_end_utc: s.window_end_utc,
    elapsed_pct: s.elapsed_pct,
    remaining_min: s.remaining_min,
    freeze: s.freeze,
    freeze_at_pct: s.freeze_at_pct,
    expired: s.expired,
    name: s.name,
    skills: s.skills,
    stopped_utc: typeof run?.stopped_utc === 'string' ? run.stopped_utc : null,
  };
}

export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    hours: { type: 'string' }, name: { type: 'string' }, skills: { type: 'string' }, 'freeze-at': { type: 'string' },
  });
  const cmd = positionals[0] ?? 'status';
  if (positionals.length > 1 || !['status', 'start', 'stop'].includes(cmd)) throw new UsageError('usage: run_window.mjs status | start --hours N [--name s] [--skills a,b] | stop [--json]');
  const ws = findWorkspace() ?? process.cwd();
  const t = now();
  const file = path.join(ws, '.harness', 'run.json');
  if (cmd === 'start') {
    const hours = Number(values.hours);
    if (!Number.isFinite(hours) || hours <= 0 || hours > 168) throw new UsageError('--hours must be a number in (0, 168]');
    if (exists(path.join(ws, '.harness', 'ALLOW_STOP_OWNER'))) {
      // CONTRACTS §4.2 pins run_window exits to 0/2, so the refusal is exit 2 with a machine-readable reason.
      throw new CliError('the owner stopped the run (.harness/ALLOW_STOP_OWNER); the owner types "OWNER: resume" to clear it before a new window starts', EXIT.USAGE, { cmd: 'run_window start', blocked: 'owner_stop' });
    }
    const skills = values.skills ? values.skills.split(',').map((s) => s.trim()).filter(Boolean) : [];
    const bad = skills.find((s) => !SKILL_RE.test(s));
    if (bad) throw new UsageError(`bad skill name: ${bad}`);
    const freezeAt = values['freeze-at'] !== undefined ? Number(values['freeze-at']) : 75;
    if (!Number.isFinite(freezeAt) || freezeAt < 1 || freezeAt > 100) throw new UsageError('--freeze-at must be 1..100');
    writeJsonAtomic(file, {
      schema: 1,
      active: true,
      started_utc: utcIso(t),
      window_end_utc: utcIso(addMinutes(t, Math.round(hours * 60))),
      name: values.name || `run-${utcIso(t).slice(0, 10)}`,
      skills: [...new Set(skills)],
      freeze_at_pct: freezeAt,
    });
    for (const f of ['STALLED', 'stop_guard.json']) { try { fs.rmSync(path.join(ws, '.harness', f), { force: true }); } catch { /* ignore */ } }
  } else if (cmd === 'stop') {
    const run = readRun(ws) || {};
    const { schema, name, skills, started_utc: started, window_end_utc: end, freeze_at_pct: fz } = run;
    writeJsonAtomic(file, {
      ...(schema ? { schema } : { schema: 1 }), active: false,
      ...(started ? { started_utc: started } : {}), ...(end ? { window_end_utc: end } : {}),
      stopped_utc: utcIso(t), ...(name ? { name } : {}), ...(Array.isArray(skills) ? { skills } : {}), ...(fz ? { freeze_at_pct: fz } : {}),
    });
  }
  const r = { cmd: `run_window ${cmd}`, ok: true, ...statusOf(ws, t) };
  const text = r.active
    ? [`run ${r.name ?? ''} active until ${r.window_end_utc} (${r.elapsed_pct}% elapsed, ${r.remaining_min} min left${r.freeze ? ', FREEZE' : ''})`]
    : [`no active run${r.expired ? ` (window ended ${r.window_end_utc})` : ''}${r.stopped_utc ? ` (stopped ${r.stopped_utc})` : ''}`];
  emit(r, { json: values.json, text });
  return 0;
}

if (isMain(import.meta.url)) await runCli(main);

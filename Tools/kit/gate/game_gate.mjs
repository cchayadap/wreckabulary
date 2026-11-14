#!/usr/bin/env node
// Game Studio Kit verification gate: every agent verifies Unity work through this script, never by launching Unity
// directly. Evolved from KNOTWELL Tools/unity_gate.mjs (ARCHITECTURE §4.2, CONTRACTS §4.4).
//
//   node Tools/kit/gate/game_gate.mjs check [runtime|editor|all|asmdef]      R0  dotnet type-check, no Unity (4 slots)
//   node Tools/kit/gate/game_gate.mjs core [filter]                          R1  engine-free tests under plain .NET (2 slots)
//   node Tools/kit/gate/game_gate.mjs compile                                R2 warm editor, else R3 batchmode
//   node Tools/kit/gate/game_gate.mjs test editmode|playmode [--filter F] [--category C] [--assemblies A;B]
//   node Tools/kit/gate/game_gate.mjs exec <Ns.Class.Method> [-- <json> | --args-file <file|->]   KitCli contract
//        (warm eval or -executeMethod). Windows PowerShell 5.1 strips the double quotes of JSON after --: use --args-file there.
//   node Tools/kit/gate/game_gate.mjs serve | unserve | status               resident headless editor, by PID
//   node Tools/kit/gate/game_gate.mjs build [--profile release|release-qa|dev|<asset> [--kind K]] [--dev] [--allow-dirty]
//   node Tools/kit/gate/game_gate.mjs smoke [--build <id>|latest] [--scenario S] [--seconds N] [--seed N] [--stripped]
//   node Tools/kit/gate/game_gate.mjs history [--since <utc>]
// Common flags: --json (one result line), --timeout <s>, --cold, --warm, --row <M>-<nn> (append a gates.jsonl row).
// Exit: 0 PASS, 1 FAIL, 2 usage or missing prerequisite, 3 INFRA (licence, missing tool; never a code failure),
// 75 BLOCKED (an editor or lock holds the project: stop and report). Every run appends Logs/gate_history.jsonl.
import { CliError, EXIT, UsageError, isMain, note, parseCli, runCli } from '../lib/cli.mjs';
import { readJsonl } from '../lib/jsonio.mjs';
import { treeKill } from '../lib/proc.mjs';
import { loadContext } from './lib/config.mjs';
import { liveChildren } from './lib/cold.mjs';
import { cmdCheck, cmdCore, CHECK_TARGETS } from './lib/dotnet.mjs';
import { cmdCompile, cmdExec, cmdTest } from './lib/jobs.mjs';
import { cmdBuild, cmdSmoke } from './lib/player.mjs';
import { ROW_RE, appendHistory, appendLedgerRow, freshnessTree, gitHead, newResult, printResult, setVerdict } from './lib/result.mjs';
import { cmdServe, cmdStatus, cmdUnserve } from './lib/serve.mjs';
import { historyStats } from './lib/stats.mjs';

export const USAGE = [
  'usage: game_gate.mjs <check [runtime|editor|all|asmdef] | core [filter] | compile | test editmode|playmode [--filter F]',
  '       [--category C] [--assemblies A;B] | exec <Ns.Class.Method> [-- <json> | --args-file <file|->] | serve | unserve |',
  '       status | build [--profile P [--kind release|release-qa|dev]] [--dev] | smoke [--build id|latest] [--scenario S] [--seconds N] [--seed N] [--stripped] | history>',
  '       [--json] [--timeout <s>] [--cold] [--warm] [--row <M>-<nn>]',
  'On Windows PowerShell 5.1 pass exec JSON with --args-file <file> (or --args-file - on stdin): it strips quotes after --.',
].join('\n');

export const OPTIONS = {
  timeout: { type: 'string' },
  cold: { type: 'boolean' },
  warm: { type: 'boolean' },
  row: { type: 'string' },
  filter: { type: 'string' },
  category: { type: 'string' },
  assemblies: { type: 'string' },
  profile: { type: 'string' },
  kind: { type: 'string' },
  dev: { type: 'boolean' },
  'allow-dirty': { type: 'boolean' },
  build: { type: 'string' },
  scenario: { type: 'string' },
  seconds: { type: 'string' },
  seed: { type: 'string' },
  stripped: { type: 'boolean' },
  raw: { type: 'boolean' },
  'args-file': { type: 'string' },
  since: { type: 'string' },
};

const SUBCOMMANDS = ['check', 'core', 'compile', 'test', 'exec', 'serve', 'unserve', 'status', 'build', 'smoke', 'history'];

/** The `cmd` field: subcommand, its main operand and the flags that change what ran. */
export function describe(sub, positionals, values) {
  const parts = [sub];
  if (sub === 'check') parts.push(positionals[1] || 'all');
  else if (sub === 'test' || sub === 'exec') { if (positionals[1]) parts.push(positionals[1]); }
  else if (sub === 'core' && positionals[1]) parts.push(positionals[1]);
  for (const f of ['stripped', 'dev', 'cold', 'warm', 'raw']) if (values[f]) parts.push(`--${f}`);
  for (const f of ['profile', 'kind', 'filter', 'category', 'build', 'scenario']) if (values[f]) parts.push(`--${f} ${values[f]}`);
  return parts.join(' ');
}

function flagsOf(values) {
  const t = values.timeout !== undefined ? Number(values.timeout) : undefined;
  if (t !== undefined && (!Number.isFinite(t) || t <= 0)) throw new UsageError('--timeout needs a positive number of seconds');
  if (values.cold && values.warm) throw new UsageError('--cold and --warm exclude each other');
  return { cold: Boolean(values.cold), warm: Boolean(values.warm), raw: Boolean(values.raw), argsFile: values['args-file'], timeoutS: t, timeoutMs: t !== undefined ? t * 1000 : undefined };
}

async function dispatch(ctx, res, sub, positionals, values, rest) {
  const flags = flagsOf(values);
  switch (sub) {
    case 'check': {
      const which = positionals[1] || 'all';
      if (!CHECK_TARGETS[which]) throw new UsageError(`check takes runtime, editor, all or asmdef (got ${which})`);
      return cmdCheck(ctx, res, which);
    }
    case 'core':
      return cmdCore(ctx, res, values.filter || positionals[1] || '');
    case 'compile':
      return cmdCompile(ctx, res, flags);
    case 'test': {
      const mode = String(positionals[1] || '').toLowerCase();
      return cmdTest(ctx, res, mode, { filter: values.filter || positionals[2] || '', category: values.category || '', assemblies: values.assemblies || '' }, flags);
    }
    case 'exec':
      return cmdExec(ctx, res, positionals[1], rest, flags);
    case 'serve':
      return cmdServe(ctx, res);
    case 'unserve':
      return cmdUnserve(ctx, res);
    case 'status':
      return cmdStatus(ctx, res);
    case 'build':
      return cmdBuild(ctx, res, { profile: values.profile, kind: values.kind, dev: Boolean(values.dev), allowDirty: Boolean(values['allow-dirty']) }, flags);
    case 'smoke':
      return cmdSmoke(ctx, res, { build: values.build, scenario: values.scenario, seconds: values.seconds, seed: values.seed, stripped: Boolean(values.stripped) }, flags);
    case 'history': {
      res.rung = null;
      res.stats = historyStats(readJsonl(ctx.paths.history), { since: values.since });
      res.log = 'Logs/gate_history.jsonl';
      const lines = [`${res.stats.runs} gate runs`];
      for (const [k, c] of Object.entries(res.stats.commands)) lines.push(`  ${k.padEnd(18)} runs ${String(c.runs).padStart(4)}  p50 ${c.p50_secs}s  p95 ${c.p95_secs}s  ${Object.entries(c.verdicts).map(([v, n]) => `${v} ${n}`).join(', ')}`);
      const q = res.stats.crash_after_quit;
      lines.push(`  crash after quit: ${q.crashes}/${q.cold_quit_runs} cold -quit runs${q.rate !== null ? ` (${Math.round(q.rate * 100)}%)` : ''}`);
      res.messages = lines;
      return setVerdict(res, 'PASS');
    }
    default:
      throw new UsageError(`unknown subcommand ${sub}\n${USAGE}`);
  }
}

let signalsHooked = false;
function hookSignals() {
  if (signalsHooked) return;
  signalsHooked = true;
  for (const sig of ['SIGINT', 'SIGTERM']) {
    process.on(sig, () => {
      for (const pid of liveChildren) treeKill(pid);
      process.exit(130);
    });
  }
}

/** @param {string[]} argv */
export async function main(argv) {
  const t0 = Date.now();
  const json = argv.includes('--json');
  const firstWord = argv.find((a) => !a.startsWith('-')) || '?';
  let res = newResult(firstWord, null);
  let ctx = null;
  let rowId = null;
  let usage = false;
  try {
    const { values, positionals, rest } = parseCli(argv, OPTIONS);
    const sub = positionals[0];
    res = newResult(sub ? describe(sub, positionals, values) : '?', null);
    if (!sub || !SUBCOMMANDS.includes(sub)) throw new UsageError(sub ? `unknown subcommand ${sub}\n${USAGE}` : USAGE);
    if (values.row !== undefined) {
      if (!ROW_RE.test(values.row)) throw new UsageError(`--row needs a gate row id like M0-01 (got ${values.row})`);
      rowId = values.row;
    }
    ctx = loadContext();
    hookSignals();
    // `exec M -- {"a":1} --json`: a trailing --json after the separator is still the output flag.
    await dispatch(ctx, res, sub, positionals, values, rest.filter((a) => a !== '--json'));
  } catch (e) {
    if (e instanceof UsageError || e?.name === 'UsageError') {
      usage = true;
      setVerdict(res, 'NOT-RUN', { exit: EXIT.USAGE, message: e.message });
    } else if (e instanceof CliError || e?.name === 'CliError') {
      const v = e.result?.verdict;
      if (e.exitCode === EXIT.BLOCKED) setVerdict(res, 'BLOCKED', { infra: e.result?.infra ?? null, message: e.message });
      else if (e.exitCode === EXIT.FAIL || v === 'FAIL') setVerdict(res, 'FAIL', { message: e.message });
      else setVerdict(res, 'INFRA', { infra: e.result?.infra ?? 'gate', message: e.message });
    } else {
      setVerdict(res, 'INFRA', { infra: 'gate_error', message: `the gate itself failed: ${e?.stack?.split('\n').slice(0, 3).join(' | ') || e}` });
    }
  }
  res.secs = Math.round((Date.now() - t0) / 100) / 10;
  if (!ctx) {
    try { ctx = loadContext(); } catch { /* no context: no history */ }
  }
  if (ctx) {
    let head = null;
    let tree = res.tree ?? null;
    try {
      head = res.head ?? (await gitHead(ctx));
      if (rowId && !usage && !tree) tree = freshnessTree(ctx);
    } catch (e) {
      note(`gate: freshness facts unavailable: ${e.message}`);
    }
    // bootSecs only for a fresh, successful serve boot (U-08): an 'already serving' run or a failed boot is not a boot time.
    const bootSecs = res.cmd === 'serve' && res.verdict === 'PASS' && res.served && !res.served.already ? res.served.boot_secs : undefined;
    appendHistory(ctx, res, { crashAfterQuit: res.crashAfterQuit, refreshSecs: res.refreshSecs, head, tree, bootSecs });
    if (rowId && !usage) {
      try {
        appendLedgerRow(ctx, res, rowId, { head, tree });
        res.row = rowId;
      } catch (e) {
        note(`gate: could not append the gates.jsonl row: ${e.message}`);
      }
    }
  }
  delete res.head;
  delete res.tree;
  printResult(ctx, res, json);
  return res.exit;
}

if (isMain(import.meta.url)) await runCli(main);

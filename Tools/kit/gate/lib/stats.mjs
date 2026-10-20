// `history`: turn Logs/gate_history.jsonl into measured numbers (U-08 serve boot time, U-19 crash-after-quit rate, U-25
// compile overhead): p50/p95 seconds and verdict counts per subcommand, plus the crash-after-quit rate of cold runs.

/** Nearest-rank percentile of a numeric array (null when empty). */
export function percentile(values, p) {
  const v = values.filter((x) => typeof x === 'number' && Number.isFinite(x)).sort((a, b) => a - b);
  if (!v.length) return null;
  const rank = Math.max(1, Math.ceil((p / 100) * v.length));
  return v[Math.min(v.length, rank) - 1];
}

/** Grouping key: the subcommand, plus the mode for `test` and `check`. */
export function historyKey(cmd) {
  const parts = String(cmd || '').trim().split(/\s+/);
  if ((parts[0] === 'test' || parts[0] === 'check') && parts[1] && !parts[1].startsWith('-')) return `${parts[0]} ${parts[1]}`;
  if (parts[0] === 'smoke' && parts.includes('--stripped')) return 'smoke --stripped';
  return parts[0] || '?';
}

/**
 * @param {object[]} lines historyLine objects
 * @param {{since?: string}} [opts] ISO lower bound on utc
 */
export function historyStats(lines, opts = {}) {
  const rows = lines.filter((l) => l && typeof l.cmd === 'string' && historyKey(l.cmd) !== 'history' && historyKey(l.cmd) !== 'status' && (!opts.since || String(l.utc) >= opts.since));
  const groups = new Map();
  for (const l of rows) {
    const k = historyKey(l.cmd);
    if (!groups.has(k)) groups.set(k, []);
    groups.get(k).push(l);
  }
  const commands = {};
  for (const [k, g] of [...groups.entries()].sort()) {
    const verdicts = {};
    for (const l of g) verdicts[l.verdict || 'UNKNOWN'] = (verdicts[l.verdict || 'UNKNOWN'] || 0) + 1;
    const routes = {};
    for (const l of g) routes[l.route || 'none'] = (routes[l.route || 'none'] || 0) + 1;
    const secs = g.map((l) => l.secs);
    const refresh = g.map((l) => l.refreshSecs).filter((x) => typeof x === 'number');
    commands[k] = {
      runs: g.length,
      p50_secs: percentile(secs, 50),
      p95_secs: percentile(secs, 95),
      max_secs: percentile(secs, 100),
      verdicts,
      routes,
      refresh_p50_secs: percentile(refresh, 50),
      last_utc: g.map((l) => l.utc).sort().pop() ?? null,
    };
  }
  const quitRuns = rows.filter((l) => l.route === 'cold' && /^(compile|exec)\b/.test(l.cmd) && l.verdict !== 'INFRA' && l.verdict !== 'BLOCKED');
  const crashes = quitRuns.filter((l) => l.crashAfterQuit === true).length;
  return {
    runs: rows.length,
    commands,
    crash_after_quit: { cold_quit_runs: quitRuns.length, crashes, rate: quitRuns.length ? Math.round((crashes / quitRuns.length) * 1000) / 1000 : null },
    // U-08: fresh successful boots only (bootSecs); an 'already serving' run or a failed boot is not a boot time.
    serve_boot_p50_secs: percentile(rows.filter((l) => historyKey(l.cmd) === 'serve' && l.verdict === 'PASS').map((l) => l.bootSecs), 50),
    serve_boots: rows.filter((l) => historyKey(l.cmd) === 'serve' && l.verdict === 'PASS' && typeof l.bootSecs === 'number').length,
  };
}

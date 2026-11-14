#!/usr/bin/env node
// build_join.mjs: which licence rows cover what a build actually ships (CONTRACTS.md §4.9; ARCHITECTURE §3.13, P11).
//   node Tools/kit/ship/build_join.mjs --build <Builds/dir|latest> [--csv Docs/licenses.csv] [--release …] [--providers …]
//        [--no-sha] [--list] [--json]
// Ship scope = BuildReport packed assets (Builds/<dir>/packed_assets.json from BuildReportExport) plus a rescan of the
// build folder (managed and native DLLs, StreamingAssets, loose files). Engine files, Unity packages and the project's
// own code are skipped; Unity-authored files (scenes, prefabs, materials) need a row only when one is present.
// Result: {ok, shipped, unlicensed:[path], blocked:[{path, rule}], tool_binaries:[path], warnings:[], build, counts}.
// Exit 1 when unlicensed, blocked or tool_binaries is non-empty; 3 when packed_assets.json is missing or invalid.
import { EXIT, parseCli, emit, isMain, runCli, UsageError, CliError } from '../lib/cli.mjs';
import fs from 'node:fs';
import { shipContext, readLedger, loadRelease, loadProviders, relOrAbs, normRowPath } from './lib/ledger.mjs';
import { checkRows, projectChecks, assetIndex } from './lib/rules.mjs';
import { resolveBuildDir, joinBuild, rawEmbeddedViolations } from './lib/build.mjs';

/**
 * @param {{build: string, csv?: string, release?: string, providers?: string, checkSha?: boolean, cwd?: string,
 *   list?: boolean, withRows?: boolean}} o withRows: the result also carries rows_in_scope (the ledger rows, for callers)
 */
export function runBuildJoin(o) {
  const ctx = shipContext({ cwd: o.cwd, csv: o.csv, release: o.release, providers: o.providers });
  const dir = resolveBuildDir(ctx, o.build);
  if (!dir) throw new UsageError(`build folder not found: ${o.build}`);
  const ledger = readLedger(ctx.csv);
  const j = joinBuild(ctx, ledger.rows, dir);
  if (!j.ok) throw new CliError(j.error, EXIT.INFRA);
  const { release, errors: releaseErrors } = loadRelease(ctx.releaseFile);
  const rows = ledger.rows.filter((r) => j.rowsInScope.has(r));
  const violations = [
    // An invalid release.json must not silently skip the DRM, platform and public-repo rules.
    ...releaseErrors.map((e) => ({ rule: 'schema', path: relOrAbs(ctx.ws, ctx.releaseFile), severity: 'FAIL', message: e })),
    ...checkRows(rows, { ws: ctx.ws, unity: ctx.unity, release, releaseMissing: !fs.existsSync(ctx.releaseFile), providers: loadProviders(ctx.providersFile), shipSet: j.rowsInScope, checkSha: o.checkSha !== false, assets: assetIndex(ctx.ws) }),
    ...projectChecks(ctx, ledger.rows, { coplayInBuild: j.coplayInBuild, buildNotices: j.buildNotices }),
    ...rawEmbeddedViolations(j),
  ];
  const blocked = violations.filter((v) => v.severity === 'FAIL').map((v) => ({ path: v.path, rule: v.rule, message: v.message }));
  const warnings = [...j.warnings, ...violations.filter((v) => v.severity === 'WARN').map((v) => `${v.rule} ${v.path}: ${v.message}`)];
  const ok = !j.unlicensed.length && !blocked.length && !j.toolBinaries.length;
  return {
    ok,
    exit: ok ? EXIT.OK : EXIT.FAIL,
    build: { dir: relOrAbs(ctx.ws, dir), id: j.identity.id, kind: j.identity.kind, development: j.identity.development },
    shipped: j.shipped.length,
    rows_matched: j.rowsInScope.size,
    unlicensed: j.unlicensed,
    blocked,
    tool_binaries: j.toolBinaries,
    warnings,
    counts: j.counts,
    shipped_paths: o.list ? j.shipped.map((s) => ({ path: s.path, source: s.source, row: s.row ? normRowPath(s.row.path) : null })) : undefined,
    rows_in_scope: o.withRows ? rows : undefined,
  };
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    build: { type: 'string' },
    csv: { type: 'string' },
    release: { type: 'string' },
    providers: { type: 'string' },
    'no-sha': { type: 'boolean', default: false },
    list: { type: 'boolean', default: false },
  });
  if (positionals.length || !values.build) throw new UsageError('usage: build_join.mjs --build <Builds/dir|latest> [--csv Docs/licenses.csv] [--json]');
  const r = runBuildJoin({ build: values.build, csv: values.csv, release: values.release, providers: values.providers, checkSha: !values['no-sha'], list: values.list });
  const text = [
    `${r.ok ? 'OK' : 'BLOCKED'}: ${r.build.dir} (${r.build.kind}${r.build.development ? ', development' : ''}) ships ${r.shipped} licensable item(s); ${r.rows_matched} ledger row(s) matched`,
    ...r.unlicensed.map((p) => `  UNLICENSED ${p}`),
    ...r.blocked.map((b) => `  BLOCKED ${b.rule} ${b.path}: ${b.message}`),
    ...r.tool_binaries.map((p) => `  TOOL BINARY ${p}`),
    ...r.warnings.map((w) => `  warning: ${w}`),
  ];
  emit(r, { json: values.json, text });
  return r.exit;
}

if (isMain(import.meta.url)) await runCli(main);

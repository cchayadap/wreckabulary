#!/usr/bin/env node
// licenses_check.mjs: can these files ship? (CONTRACTS.md §4.9; ARCHITECTURE §3.13 and Critique C-4.)
//   node Tools/kit/ship/licenses_check.mjs [--csv Docs/licenses.csv] [--release Docs/ship/release.json]
//        [--providers Tools/kit/assetgen/providers.json] [--scope all|ships|build:<Builds/dir>] [--no-sha] [--json]
// Scope: all = every row (default); ships = rows with ships=true; build:<dir> = rows joined to what that build packed
// (BuildReport packed_assets.json plus a rescan of the folder), which is the real ship scope. A rule on a shipping row
// is FAIL; on a row that does not ship it is WARN. Rule ids: see lib/rules.mjs RULE_IDS.
// Result: {ok, violations:[{rule, path, severity, message}], scope, rows_checked, shipping_rows, counts}. Exit 1 on any FAIL.
import fs from 'node:fs';
import { EXIT, parseCli, emit, isMain, runCli, UsageError, CliError } from '../lib/cli.mjs';
import { shipContext, readLedger, loadRelease, loadProviders, relOrAbs } from './lib/ledger.mjs';
import { checkRows, projectChecks, assetIndex } from './lib/rules.mjs';
import { resolveBuildDir, joinBuild, rawEmbeddedViolations } from './lib/build.mjs';

/**
 * Run the licence checks.
 * @param {{csv?: string, release?: string, providers?: string, scope?: string, checkSha?: boolean, cwd?: string}} o
 */
export function runLicencesCheck(o = {}) {
  const ctx = shipContext({ cwd: o.cwd, csv: o.csv, release: o.release, providers: o.providers });
  const scope = o.scope || 'all';
  const ledger = readLedger(ctx.csv);
  const { release, errors: releaseErrors } = loadRelease(ctx.releaseFile);
  const providers = loadProviders(ctx.providersFile);
  const violations = [];
  for (const h of ledger.headerErrors) violations.push({ rule: 'schema', path: relOrAbs(ctx.ws, ctx.csv), severity: 'FAIL', message: h });
  for (const e of releaseErrors) violations.push({ rule: 'schema', path: relOrAbs(ctx.ws, ctx.releaseFile), severity: 'FAIL', message: e });
  if (!ledger.exists) violations.push({ rule: 'schema', path: relOrAbs(ctx.ws, ctx.csv), severity: scope === 'all' ? 'WARN' : 'FAIL', message: 'Docs/licenses.csv does not exist yet: every shipped third-party file needs a row (license_row.mjs add)' });

  let rows = ledger.rows;
  let shipSet = null;
  let build = null;
  let buildDir = null;
  if (scope === 'ships') rows = ledger.rows.filter((r) => r.ships === 'true');
  else if (scope.startsWith('build:')) {
    buildDir = resolveBuildDir(ctx, scope.slice(6));
    if (!buildDir) throw new UsageError(`build folder not found: ${scope.slice(6)}`);
    build = joinBuild(ctx, ledger.rows, buildDir);
    if (!build.ok) throw new CliError(build.error, EXIT.INFRA);
    rows = ledger.rows.filter((r) => build.rowsInScope.has(r));
    shipSet = build.rowsInScope;
    for (const t of build.toolBinaries) violations.push({ rule: 'tool_binary', path: t, severity: 'FAIL', message: 'dev-only tool binary in the build (ffmpeg GPL, gifski AGPL, Unity Pipeline runtime): remove it' });
    for (const u of build.unlicensed) violations.push({ rule: 'unlicensed_file', path: u, severity: 'FAIL', message: 'the build ships this file and no ledger row covers it: add a row (license_row.mjs add) or remove it from the build' });
    violations.push(...rawEmbeddedViolations(build));
  } else if (scope !== 'all') throw new UsageError('--scope must be all, ships or build:<Builds/dir>');

  violations.push(...checkRows(rows, {
    ws: ctx.ws,
    unity: ctx.unity,
    release,
    releaseMissing: !fs.existsSync(ctx.releaseFile),
    providers,
    shipSet,
    checkSha: o.checkSha !== false,
    assets: assetIndex(ctx.ws),
  }));
  violations.push(...projectChecks(ctx, ledger.rows, build ? { coplayInBuild: build.coplayInBuild, buildNotices: build.buildNotices } : {}));

  const fails = violations.filter((v) => v.severity === 'FAIL');
  return {
    ok: fails.length === 0,
    exit: fails.length ? EXIT.FAIL : EXIT.OK,
    scope,
    csv: relOrAbs(ctx.ws, ctx.csv),
    rows_checked: rows.length,
    shipping_rows: shipSet ? shipSet.size : rows.filter((r) => r.ships === 'true').length,
    counts: { fail: fails.length, warn: violations.length - fails.length },
    violations,
    ...(build ? { build: { dir: relOrAbs(ctx.ws, buildDir), kind: build.identity.kind, development: build.identity.development, unlicensed: build.unlicensed.length } } : {}),
  };
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, {
    csv: { type: 'string' },
    release: { type: 'string' },
    providers: { type: 'string' },
    scope: { type: 'string', default: 'all' },
    'no-sha': { type: 'boolean', default: false },
  });
  if (positionals.length) throw new UsageError('usage: licenses_check.mjs [--csv] [--release] [--providers] [--scope all|ships|build:<dir>] [--json]');
  const r = runLicencesCheck({ csv: values.csv, release: values.release, providers: values.providers, scope: values.scope, checkSha: !values['no-sha'] });
  const text = [
    `${r.ok ? 'OK' : 'BLOCKED'}: ${r.rows_checked} row(s) checked (scope ${r.scope}); ${r.counts.fail} FAIL, ${r.counts.warn} WARN`,
    ...r.violations.map((v) => `  ${v.severity} ${v.rule} ${v.path}: ${v.message}`),
  ];
  emit(r, { json: values.json, text });
  return r.exit;
}

if (isMain(import.meta.url)) await runCli(main);

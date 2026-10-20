// Rungs R0 (check) and R1 (core): Unity-free dotnet builds and tests, each in its own lock slot and bin/obj folder, so
// four checks and two core runs can go at once. Measured on KNOTWELL: check 13.3 s warm (86.5 s after edits), core 21.1 s.
import fs from 'node:fs';
import path from 'node:path';
import { note } from '../../lib/cli.mjs';
import { readText } from '../../lib/jsonio.mjs';
import { lockSlot } from '../../lib/locks.mjs';
import { run } from '../../lib/proc.mjs';
import { generationFresh, generateChecks, loadAsmdefs, checkRules } from './asmdefs.mjs';
import { dotnetArgv } from './config.mjs';
import { editorDirOf, readProjectVersion, resolveEditor } from './editor.mjs';
import { onlyInfraErrors, parseCompilerErrors } from './logscan.mjs';
import { addError, relU, setVerdict, uniqueLog, uniqueStamped } from './result.mjs';
import { liveChildren } from './cold.mjs';
import { firstAssetsLocation, decodeXml } from '../parse_nunit.mjs';

export const CHECK_TARGETS = Object.freeze({ runtime: ['Runtime.csproj'], editor: ['Editor.csproj'], all: ['Runtime.csproj', 'Editor.csproj'], asmdef: ['asmdef/_All.csproj'] });

/** Regenerate the Check/CoreTests projects when asmdefs, config, editor or templates changed. */
export async function ensureGenerated(ctx) {
  const editor = await resolveEditor(ctx);
  const editorDir = editorDirOf(editor);
  const unityVersion = readProjectVersion(ctx.paths.projectVersion)?.version ?? editor.version ?? null;
  if (generationFresh(ctx, { editorDir, unityVersion })) return { regenerated: false, editor, editorDir };
  note('gate: regenerating Check/ and CoreTests/ from the asmdefs');
  const g = generateChecks(ctx, { editorDir, unityVersion });
  return { regenerated: true, editor, editorDir, gen: g };
}

async function takeSlot(ctx, res, prefix, n, purpose) {
  try {
    return await lockSlot(ctx.paths.locks, prefix, n, { waitMs: ctx.cfg.lockWaitMs, pollMs: Math.min(1000, ctx.cfg.lockPollMs), purpose });
  } catch (e) {
    if (e.code !== 'ELOCKTIMEOUT') throw e;
    setVerdict(res, 'BLOCKED', { infra: 'lock_timeout', message: `all ${n} ${prefix} slots stayed busy for ${Math.round(ctx.cfg.lockWaitMs / 1000)} s` });
    return null;
  }
}

function dotnetOrInfra(res) {
  const argv = dotnetArgv();
  if (!argv) setVerdict(res, 'INFRA', { infra: 'no_dotnet', message: 'dotnet is not installed (winget install Microsoft.DotNet.SDK.9)' });
  return argv;
}

/**
 * `check [runtime|editor|all|asmdef]` (R0).
 * @param {object} ctx
 * @param {object} res
 * @param {string} which
 */
export async function cmdCheck(ctx, res, which) {
  res.rung = 'R0';
  const dotnet = dotnetOrInfra(res);
  if (!dotnet) return {};
  const gen = await ensureGenerated(ctx);
  let violations = [];
  if (which === 'asmdef') {
    const model = loadAsmdefs(ctx.U, { codeRoots: ctx.cfg.codeRoots, checkExcludes: ctx.cfg.checkExcludes });
    violations = checkRules(model, { layers: ctx.cfg.layers }).violations;
    res.violations = violations.map((v) => ({ asmdef: v.asmdef, reference: v.reference, rule: v.rule }));
  }
  const editorDir = gen.editorDir;
  if (editorDir && !fs.existsSync(path.join(editorDir, 'Data', 'Managed'))) {
    setVerdict(res, 'INFRA', { infra: 'unity_editor_dir', message: `the editor folder has no Data/Managed: ${editorDir}` });
    return {};
  }
  if (!editorDir) {
    const props = path.join(ctx.paths.check, 'Directory.Build.props');
    const m = fs.existsSync(props) ? /<UnityEditorDir[^>]*>([^<]*)<\/UnityEditorDir>/.exec(readText(props)) : null;
    if (!m || !fs.existsSync(path.join(m[1], 'Data', 'Managed'))) {
      setVerdict(res, 'INFRA', { infra: 'unity_editor_dir', message: gen.editor?.error || 'cannot find the Unity editor folder for the reference assemblies' });
      return {};
    }
  }
  const slot = await takeSlot(ctx, res, 'dotnet', ctx.cfg.checkSlots, `check ${which}`);
  if (!slot) return {};
  res.slot = slot.slot;
  const log = uniqueLog(ctx, `check_${which}`);
  res.log = relU(ctx, log);
  const messages = [];
  let failed = false;
  let infraOnly = true;
  try {
    for (const proj of CHECK_TARGETS[which]) {
      const abs = path.join(ctx.paths.check, ...proj.split('/'));
      const label = path.basename(proj, '.csproj').replace(/^_All$/, 'asmdefs');
      const args = ['build', abs, '-nologo', '-v', 'q', '-clp:NoSummary;ErrorsOnly', `-p:KitSlot=${slot.slot}`];
      if (editorDir) args.push(`-p:UnityEditorDir=${editorDir}`);
      let pid = null;
      const r = await run(dotnet[0], [...dotnet.slice(1), ...args], { cwd: ctx.U, timeoutMs: ctx.cfg.timeoutMs, logFile: log, onSpawn: (p) => { pid = p; liveChildren.add(p); } });
      if (pid) liveChildren.delete(pid);
      if (r.exit === null && !r.timedOut) {
        // A tool that cannot start is INFRA (CONTRACTS §1.4), never a code failure; `core` says the same.
        res.messages = messages;
        setVerdict(res, 'INFRA', { infra: 'no_dotnet', message: `cannot start dotnet: ${r.error || 'no process'}` });
        return {};
      }
      const out = `${r.stdout}\n${r.stderr}`;
      const errors = parseCompilerErrors(out, { baseDir: ctx.U });
      for (const e of errors) addError(res, e);
      if (r.exit === 0 && errors.length === 0 && !r.timedOut) {
        messages.push(`[check:${label}] OK`);
        continue;
      }
      failed = true;
      if (!onlyInfraErrors(errors)) infraOnly = false;
      if (r.timedOut) { infraOnly = false; addError(res, `dotnet build ${proj} timed out after ${Math.round(ctx.cfg.timeoutMs / 1000)} s`); }
      else if (!errors.length) {
        infraOnly = false;
        addError(res, `dotnet build ${proj} exited ${r.exit}: ${out.trim().split(/\r?\n/).slice(-8).join(' | ').slice(0, 600)}`);
      }
      messages.push(`[check:${label}] FAILED (${errors.length} errors)`);
    }
  } finally {
    slot.release();
  }
  res.messages = messages;
  for (const v of violations) addError(res, { message: v.message, code: `layer:${v.rule}`, severity: 'error' });
  if (!failed && !violations.length) setVerdict(res, 'PASS');
  else if (failed && infraOnly && !violations.length) setVerdict(res, 'INFRA', { infra: 'dotnet_restore_or_sdk' });
  else if (failed && !fs.existsSync(ctx.paths.scriptAssemblies) && res.errors.some((e) => /^CS0(246|234)$/.test(e.code || ''))) {
    setVerdict(res, 'INFRA', { infra: 'no_library', message: 'Library/ScriptAssemblies is missing: run one Unity import first (game_gate compile), then check again' });
  } else setVerdict(res, 'FAIL');
  return {};
}

/** Parse a TRX (VSTest) file: counters and failed tests. */
export function parseTrx(xml) {
  const c = /<Counters\b([^>]*)\/?>/.exec(xml);
  const attr = (s, a) => { const m = new RegExp(`\\b${a}="([^"]*)"`).exec(s || ''); return m ? Number(m[1]) : null; };
  const failures = [];
  const re = /<UnitTestResult\b([^>]*?)(\/>|>([\s\S]*?)<\/UnitTestResult>)/g;
  let m;
  while ((m = re.exec(xml))) {
    const a = m[1];
    if (!/\boutcome="Failed"/.test(a)) continue;
    const name = decodeXml((/\btestName="([^"]*)"/.exec(a) || [])[1] || '?');
    const body = m[3] || '';
    const msg = decodeXml((/<Message>([\s\S]*?)<\/Message>/.exec(body) || [])[1] || '').trim();
    const st = decodeXml((/<StackTrace>([\s\S]*?)<\/StackTrace>/.exec(body) || [])[1] || '');
    failures.push({ name, message: msg, location: firstAssetsLocation(st) });
  }
  if (!c) return failures.length ? { total: failures.length, passed: 0, failed: failures.length, skipped: 0, failures } : null;
  const total = attr(c[1], 'total') ?? 0;
  const passed = attr(c[1], 'passed') ?? 0;
  const failed = Math.max(attr(c[1], 'failed') ?? 0, failures.length);
  const executed = attr(c[1], 'executed') ?? total;
  return { total: executed, passed, failed, skipped: Math.max(0, total - executed) + (attr(c[1], 'notExecuted') ?? 0), failures };
}

/** The `Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4` summary line of `dotnet test`. */
export function parseDotnetTestSummary(text) {
  const m = /(Passed!|Failed!)\s*-\s*Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)/.exec(String(text || ''));
  if (!m) return null;
  return { total: Number(m[5]), passed: Number(m[3]), failed: Number(m[2]), skipped: Number(m[4]) };
}

/**
 * `core [filter]` (R1): `dotnet test CoreTests` in a core slot, results from the TRX file and the summary line.
 */
export async function cmdCore(ctx, res, filter) {
  res.rung = 'R1';
  const dotnet = dotnetOrInfra(res);
  if (!dotnet) return {};
  await ensureGenerated(ctx);
  const proj = path.join(ctx.paths.core, 'CoreTests.csproj');
  if (!fs.existsSync(proj)) {
    setVerdict(res, 'NOT-RUN', { exit: 2, infra: 'no_core_tests', message: 'no CoreTests project: the project has no engine-free asmdef (noEngineReferences) and gate.config.json core.asmdefs is empty' });
    return {};
  }
  const slot = await takeSlot(ctx, res, 'core', ctx.cfg.coreSlots, `core ${filter || ''}`.trim());
  if (!slot) return {};
  res.slot = slot.slot;
  const log = uniqueLog(ctx, 'core');
  res.log = relU(ctx, log);
  const trx = uniqueStamped(ctx.paths.testResults, 'core', '.trx');
  const args = ['test', proj, '-nologo', '-v', 'q', `-p:KitSlot=${slot.slot}`, '--results-directory', path.dirname(trx), '--logger', `trx;LogFileName=${path.basename(trx)}`];
  if (filter) args.push('--filter', filter);
  let r;
  try {
    let pid = null;
    r = await run(dotnet[0], [...dotnet.slice(1), ...args], { cwd: ctx.U, timeoutMs: ctx.cfg.timeoutMs, logFile: log, onSpawn: (p) => { pid = p; liveChildren.add(p); } });
    if (pid) liveChildren.delete(pid);
  } finally {
    slot.release();
  }
  const out = `${r.stdout}\n${r.stderr}`;
  const errors = parseCompilerErrors(out, { baseDir: ctx.U });
  for (const e of errors) addError(res, e);
  let t = fs.existsSync(trx) ? parseTrx(readText(trx)) : null;
  if (t) res.evidence.push(relU(ctx, trx));
  const summary = parseDotnetTestSummary(out);
  if (!t && summary) t = { ...summary, failures: [] };
  if (t) {
    res.tests = { total: t.total, passed: t.passed, failed: t.failed, skipped: t.skipped };
    res.failures = t.failures;
  }
  if (r.timedOut) addError(res, `dotnet test timed out after ${Math.round(ctx.cfg.timeoutMs / 1000)} s`);
  if (r.exit === null && !r.timedOut) {
    setVerdict(res, 'INFRA', { infra: 'no_dotnet', message: `cannot start dotnet: ${r.error}` });
    return {};
  }
  if (errors.length && onlyInfraErrors(errors)) {
    setVerdict(res, 'INFRA', { infra: 'dotnet_restore_or_sdk' });
    return {};
  }
  if (!res.tests) res.tests = { total: 0, passed: 0, failed: 0, skipped: 0 };
  const pass = r.exit === 0 && !r.timedOut && errors.length === 0 && res.tests.total > 0 && res.tests.failed === 0;
  if (!pass && res.tests.total === 0 && !errors.length) addError(res, /No test matches the given testcase filter/i.test(out) ? `no test matches the filter ${filter}` : 'zero tests ran (zero tests is a FAIL)');
  if (!pass && r.exit !== 0 && !errors.length && !res.failures.length && res.tests.total > 0) addError(res, `dotnet test exited ${r.exit}: ${out.trim().split(/\r?\n/).slice(-6).join(' | ').slice(0, 500)}`);
  setVerdict(res, pass ? 'PASS' : 'FAIL');
  return {};
}

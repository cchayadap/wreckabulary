#!/usr/bin/env node
// Generate the R0/R1 dotnet projects from the Unity project's asmdefs (CONTRACTS §4.4):
//   node Tools/kit/gate/gen_asmdef_checks.mjs [--json]
// Writes, under the gate folder: Check/Directory.Build.props, Check/Runtime.csproj, Check/Editor.csproj,
// Check/asmdef/<Asmdef>.csproj (+ _All.csproj), CoreTests/CoreTests.csproj (when an engine-free asmdef exists) and
// Check/.gen.json. Exit 1 when an asmdef breaks a layer rule (left-only references, noEngineReferences, runtime → Editor
// or test, cycles, a name that is not a valid assembly name); the projects are still written so `check` can run.
// `game_gate check` regenerates automatically. The --json `projects` paths are W-relative (CONTRACTS §1.1).
import path from 'node:path';
import { EXIT, emit, isMain, parseCli, runCli } from '../lib/cli.mjs';
import { relPosix } from '../lib/paths.mjs';
import { loadContext } from './lib/config.mjs';
import { editorDirOf, readProjectVersion, resolveEditor } from './lib/editor.mjs';
import { generateChecks } from './lib/asmdefs.mjs';

/**
 * Generate for a context (shared with game_gate).
 * @param {ReturnType<typeof loadContext>} ctx
 * @param {{noCli?: boolean}} [opts]
 */
export async function generateFor(ctx, opts = {}) {
  const editor = await resolveEditor(ctx, { noCli: opts.noCli });
  const version = readProjectVersion(ctx.paths.projectVersion)?.version ?? editor.version ?? null;
  const r = generateChecks(ctx, { editorDir: editorDirOf(editor), unityVersion: version });
  return { ...r, editor };
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values } = parseCli(argv, {});
  const ctx = loadContext();
  const r = await generateFor(ctx);
  const ok = r.violations.length === 0;
  const res = {
    ok,
    exit: ok ? EXIT.OK : EXIT.FAIL,
    // W-relative like every kit JSON path (CONTRACTS §1.1); generateChecks keeps them U-relative in Check/.gen.json.
    projects: r.projects.map((p) => relPosix(ctx.W, path.resolve(ctx.U, p))),
    unresolved: r.unresolved,
    violations: r.violations.map((v) => ({ asmdef: v.asmdef, reference: v.reference, rule: v.rule, message: v.message })),
    warnings: r.warnings.map((w) => w.message),
    core: r.core,
    editor_dir: editorDirOf(r.editor),
  };
  const lines = [`gen_asmdef_checks: ${r.projects.length} files written${r.core ? ' (with CoreTests)' : ''}`];
  for (const v of r.violations) lines.push(`  VIOLATION ${v.rule}: ${v.message}`);
  for (const w of r.warnings.slice(0, 20)) lines.push(`  warning: ${w.message}`);
  if (!r.editor.exe) lines.push(`  warning: ${r.editor.error || 'editor folder unknown'}; the props carry a placeholder UnityEditorDir (the gate passes -p:UnityEditorDir when it can)`);
  emit(res, { json: values.json, text: lines });
  return res.exit;
}

if (isMain(import.meta.url)) await runCli(main);

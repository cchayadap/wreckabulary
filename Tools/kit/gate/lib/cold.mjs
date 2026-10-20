// The cold rung (R3): one batchmode editor per job. Timeouts kill the whole PID tree (Unity.exe, bee_backend,
// UnityShaderCompiler, …) through lib/proc run(); editor logs are snapshotted first; the log is read afterwards.
import fs from 'node:fs';
import path from 'node:path';
import { note } from '../../lib/cli.mjs';
import { readText } from '../../lib/jsonio.mjs';
import { run } from '../../lib/proc.mjs';
import { resolveEditor } from './editor.mjs';
import { cleanQuit, refreshSecs } from './logscan.mjs';
import { uniqueLog } from './result.mjs';
import { snapshotEditorLogs } from './unitylock.mjs';

/** PIDs of children the gate started and still waits on (tree-killed on Ctrl+C). */
export const liveChildren = new Set();

/**
 * Run the editor in batchmode: `-batchmode -projectPath U -logFile <log> -crash-report-folder Logs/crashes <args>`.
 * @param {object} ctx
 * @param {{args: string[], logName: string, timeoutMs: number, env?: NodeJS.ProcessEnv}} job
 * @returns {Promise<{started:boolean, infra?:string, exit:number|null, timedOut:boolean, secs:number, log:string, logFile:string|null,
 *   clean:boolean, crashAfterQuit:boolean, refreshSecs:number|null, editor?:object}>}
 */
export async function runEditorBatch(ctx, job) {
  const editor = await resolveEditor(ctx);
  if (!editor.argv) return { started: false, infra: editor.error, exit: null, timedOut: false, secs: 0, log: '', logFile: null, clean: false, crashAfterQuit: false, refreshSecs: null, editor };
  snapshotEditorLogs(ctx);
  const logFile = uniqueLog(ctx, job.logName);
  fs.mkdirSync(ctx.paths.crashes, { recursive: true });
  const args = ['-batchmode', '-projectPath', ctx.U, '-logFile', logFile, '-crash-report-folder', ctx.paths.crashes, ...job.args];
  note(`gate: ${path.basename(editor.argv[editor.argv.length - 1])} ${job.args.join(' ')} (log ${path.relative(ctx.U, logFile)})`);
  let pid = null;
  const r = await run(editor.argv[0], [...editor.argv.slice(1), ...args], {
    cwd: ctx.U,
    env: job.env ? { ...process.env, ...job.env } : process.env,
    timeoutMs: job.timeoutMs,
    onSpawn: (p) => { pid = p; liveChildren.add(p); },
  });
  if (pid) liveChildren.delete(pid);
  let log = '';
  try {
    log = readText(logFile);
  } catch { /* the editor never wrote it */ }
  if (r.exit === null && !r.timedOut && r.error) return { started: false, infra: `cannot start the editor: ${r.error}`, exit: null, timedOut: false, secs: r.secs, log, logFile, clean: false, crashAfterQuit: false, refreshSecs: null, editor };
  const q = r.timedOut ? { clean: false, crashAfterQuit: false } : cleanQuit({ exit: r.exit, log });
  return { started: true, exit: r.exit, timedOut: r.timedOut, secs: r.secs, log, logFile, clean: q.clean, crashAfterQuit: q.crashAfterQuit, refreshSecs: refreshSecs(log), editor };
}

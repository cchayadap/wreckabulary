// Reading Unity editor logs, dotnet build output and player logs: compiler errors, the clean-quit rule, refresh time,
// infrastructure causes and exception lines. Pure functions (no I/O), so every rule is unit-testable.
import path from 'node:path';

const toPosix = (p) => String(p).replace(/\\/g, '/');

/**
 * Compiler and build errors from Unity or dotnet output, deduplicated, with paths made relative to baseDir when inside it.
 * Recognises `file(line,col): error CS1234: msg [proj]`, `file(line,col): warning CS…` (only with includeWarnings), and
 * `… error MSB1234: …` / `… error NU1101: …` build-tool errors.
 * @param {string} text
 * @param {{baseDir?: string, includeWarnings?: boolean, max?: number}} [opts]
 * @returns {{message:string, code?:string, file?:string, line?:number, col?:number, severity:'error'|'warning'}[]}
 */
export function parseCompilerErrors(text, opts = {}) {
  const out = [];
  const seen = new Set();
  const base = opts.baseDir ? toPosix(path.resolve(opts.baseDir)).toLowerCase() + '/' : null;
  const rel = (f) => {
    const p = toPosix(f.trim());
    if (base && p.toLowerCase().startsWith(base)) return p.slice(base.length);
    return p;
  };
  const push = (e) => {
    const key = `${e.severity}|${e.code || ''}|${e.file || ''}|${e.line || ''}|${e.col || ''}|${e.message}`;
    if (seen.has(key)) return;
    seen.add(key);
    out.push(e);
  };
  for (const raw of String(text || '').split(/\r?\n/)) {
    const line = raw.replace(/\s+\[[^\]]*\.(?:cs|fs|vb)proj(?:::[^\]]*)?\]\s*$/i, '').trimEnd();
    let m = /^\s*(?:\d+>)?(.+?)\((\d+),(\d+)(?:,\d+,\d+)?\):\s*(error|warning)\s+(CS\d+)\s*:\s*(.*)$/.exec(line);
    if (m) {
      if (m[4] === 'warning' && !opts.includeWarnings) continue;
      push({ message: m[6].trim(), code: m[5], file: rel(m[1]), line: Number(m[2]), col: Number(m[3]), severity: m[4] });
      continue;
    }
    m = /(?:^|[\s:])(error|warning)\s+((?:MSB|NU|NETSDK|CS)\d+)\s*:\s*(.*)$/.exec(line);
    if (m) {
      if (m[1] === 'warning' && !opts.includeWarnings) continue;
      const fileM = /^\s*(.+?)\s*:\s*(?:error|warning)\s/.exec(line);
      const e = { message: m[3].trim(), code: m[2], severity: m[1] };
      if (fileM && !/\s(?:error|warning)\s/.test(fileM[1])) e.file = rel(fileM[1]);
      push(e);
    }
  }
  return typeof opts.max === 'number' ? out.slice(0, opts.max) : out;
}

// One compile cycle of an editor log starts with a script-compilation request or the first bee_backend pass and ends with
// the Tundra result (Unity 6.3 Editor.log; the lab resident.log shows both).
const CYCLE_START = /\[ScriptCompilation\] Requested script compilation|bee_backend(?:\.exe)?\b.*backend1\.traceevents|Starting script compilation/;
const CYCLE_END = /\*\*\* Tundra build (?:success|failed)|Scripts have compiler errors/;

/**
 * The editor log from the start of its last finished compile cycle to the end, or null when no finished cycle is found.
 * A long GUI session holds many cycles; only the last one says what is broken now.
 * @param {string} text
 * @returns {string|null}
 */
export function lastCompileCycle(text) {
  const lines = String(text || '').split(/\r?\n/);
  let end = -1;
  for (let i = lines.length - 1; i >= 0; i--) {
    if (CYCLE_END.test(lines[i])) { end = i; break; }
  }
  if (end < 0) return null;
  for (let i = end; i >= 0; i--) if (CYCLE_START.test(lines[i])) return lines.slice(i).join('\n');
  return null;
}

/**
 * Compile errors that describe the editor's current state: those of its last finished compile cycle (and anything
 * after it), else the newest `max` errors of the whole text. Never the oldest ones of an earlier, fixed cycle.
 * @param {string} text
 * @param {{baseDir?: string, max?: number}} [opts]
 */
export function currentCompileErrors(text, opts = {}) {
  const max = opts.max ?? 60;
  const cycle = lastCompileCycle(text);
  const errors = parseCompilerErrors(cycle ?? text, { baseDir: opts.baseDir });
  return cycle !== null ? errors.slice(0, max) : errors.slice(-max);
}

/** A player build that failed for a reason in the project (shaders, build processors, scenes, IL2CPP), not the machine. */
export const BUILD_FAILED = /Build Finished, Result: Failure|Build completed with a result of 'Failed'|BuildFailedException|Shader error/;

/** True when every error is a NuGet/SDK/MSBuild-infrastructure error (network, missing SDK), not a code error. */
export function onlyInfraErrors(errors) {
  return errors.length > 0 && errors.every((e) => /^(NU\d+|NETSDK\d+|MSB4236|MSB4019|MSB1009|MSB1003)$/.test(e.code || ''));
}

const NOTABLE = /(Scripts have compiler errors|Aborting batchmode|executeMethod method .* threw exception|Couldn't set project path|another Unity instance|Multiple Unity instances|already open in a running Editor|Exception:|NullReferenceException|An error occurred while resolving packages|\[Package Manager\].*error|Build Finished, Result: Failure|Build completed with a result of 'Failed'|BuildFailedException|Shader error|Safe Mode)/i;

/** Log lines worth showing an agent (the KNOTWELL gate's list plus build failures), deduplicated. */
export function notableLines(text, max = 25) {
  const seen = new Set();
  const out = [];
  for (const l of String(text || '').split(/\r?\n/)) {
    const t = l.trim();
    if (!t || !NOTABLE.test(t) || seen.has(t)) continue;
    seen.add(t);
    out.push(t);
    if (out.length >= max) break;
  }
  return out;
}

/** Lines that editor commands print for the agent: those starting with the project prefix (e.g. `[KNOTWELL]`) or `[GSK]`. */
export function prefixLines(text, prefixes, max = 80) {
  const ps = (Array.isArray(prefixes) ? prefixes : [prefixes]).filter(Boolean);
  const out = [];
  for (const l of String(text || '').split(/\r?\n/)) {
    const t = l.trim();
    if (ps.some((p) => t.startsWith(p))) out.push(t);
    if (out.length >= max) break;
  }
  return out;
}

/** Sum of `Asset Pipeline Refresh … Total: N seconds` lines (seconds), or null when there are none. */
export function refreshSecs(text) {
  const re = /Asset Pipeline Refresh[^\n]*?Total:\s*([\d.]+)\s*seconds/g;
  let sum = 0;
  let any = false;
  let m;
  while ((m = re.exec(String(text || '')))) {
    sum += Number(m[1]);
    any = true;
  }
  return any ? Math.round(sum * 10) / 10 : null;
}

/** The batchmode clean-quit marker is present and nothing aborted batchmode. */
export function quitMarker(text) {
  const t = String(text || '');
  return /Batchmode quit successfully invoked/.test(t) && !/Aborting batchmode/.test(t);
}

/**
 * Clean-quit rule (Unity 6.3 crashes during shutdown, 0xC0000005, after the work is done in about 2 of 3 `-quit` runs,
 * root cause unknown, U-19): exit 0 is clean; a non-zero exit is still clean when the log has
 * `Batchmode quit successfully invoked` and no `Aborting batchmode`. A crash before that line is a real failure.
 * @param {{exit:number|null, log:string}} r
 * @returns {{clean:boolean, crashAfterQuit:boolean}}
 */
export function cleanQuit(r) {
  if (r.exit === 0) return { clean: true, crashAfterQuit: false };
  if (quitMarker(r.log)) return { clean: true, crashAfterQuit: true };
  return { clean: false, crashAfterQuit: false };
}

/** True when the log reports compile errors (an `error CS` line or Unity's summary line). */
export function hasCompileErrors(text) {
  return /error CS\d+/.test(String(text || '')) || /Scripts have compiler errors/.test(String(text || ''));
}

/** `-executeMethod` or KitCli reported an exception. */
export function threwException(text) {
  return /threw exception/i.test(String(text || ''));
}

/**
 * Why a Unity run could not do its work for reasons that are not the project's code, or null.
 * kind 'blocked' = the project is held by another editor (exit 75); kind 'infra' = licence, packages, bad path (exit 3).
 * @param {string} text
 * @returns {{kind:'blocked'|'infra', reason:string, line:string}|null}
 */
export function infraCause(text) {
  const rules = [
    ['blocked', 'project_open', /It looks like another Unity instance is running with this project open|Multiple Unity instances cannot open the same project|another Unity instance is running|already open in a running Editor/i],
    ['infra', 'licence', /No valid Unity (?:Editor )?licen[cs]e|licen[cs]e (?:is not active|has expired|is invalid)|Unity has not been activated|LICENSE SYSTEM .*(?:error|fail)|Failed to (?:activate|validate) (?:the )?licen[cs]e|com\.unity\.editor\.headless/i],
    ['infra', 'project_path', /Couldn't set project path/i],
    ['infra', 'packages', /An error occurred while resolving packages|Package resolution failed/i],
    ['infra', 'disk', /There is not enough space on the disk|No space left on device/i],
  ];
  const lines = String(text || '').split(/\r?\n/);
  for (const [kind, reason, re] of rules) {
    const line = lines.find((l) => re.test(l));
    if (line !== undefined) return { kind, reason, line: line.trim() };
  }
  return null;
}

/** Lines of a player log that mention an exception (the smoke rule: any `Exception` fails), up to max. */
export function exceptionLines(text, max = 20) {
  return String(text || '').split(/\r?\n/).map((l) => l.trim()).filter((l) => /Exception/.test(l)).slice(0, max);
}

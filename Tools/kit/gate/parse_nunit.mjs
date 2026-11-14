#!/usr/bin/env node
// Test-result parsing for the gate (CONTRACTS §4.4): NUnit 3 XML (Unity -runTests / `unity test --output`) and the
// `##utp:` Unity Test Protocol lines a batchmode log carries. Library + CLI:
//   node Tools/kit/gate/parse_nunit.mjs <results.xml | editor.log> [--assemblies A;B] [--json]
// Exit 0 iff total > 0 and failed == 0; 1 otherwise (zero tests is a FAIL, never a pass); 2 when the file is missing.
import fs from 'node:fs';
import { EXIT, UsageError, emit, isMain, parseCli, runCli } from '../lib/cli.mjs';
import { readText } from '../lib/jsonio.mjs';

const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" };

/** Decode XML character references and the five named entities. @param {string} s */
export function decodeXml(s) {
  return String(s).replace(/&(#x[0-9a-fA-F]+|#\d+|amp|lt|gt|quot|apos);/g, (m, e) => {
    if (e[0] === '#') {
      const code = e[1] === 'x' ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10);
      return Number.isFinite(code) ? String.fromCodePoint(code) : m;
    }
    return ENTITIES[e];
  });
}

function parseAttrs(tag) {
  const attrs = {};
  const re = /([A-Za-z_][\w:.-]*)\s*=\s*("([^"]*)"|'([^']*)')/g;
  let m;
  while ((m = re.exec(tag))) attrs[m[1]] = decodeXml(m[3] ?? m[4] ?? '');
  return attrs;
}

/**
 * Minimal streaming XML walk (enough for NUnit result files): calls onOpen(name, attrs, selfClosing, depth),
 * onClose(name, depth) and onText(text) with CDATA already unwrapped.
 */
function walkXml(xml, { onOpen, onClose, onText }) {
  const re = /<!\[CDATA\[([\s\S]*?)\]\]>|<!--[\s\S]*?-->|<\?[\s\S]*?\?>|<!DOCTYPE[^>]*>|<\/([A-Za-z_][\w:.-]*)\s*>|<([A-Za-z_][\w:.-]*)((?:\s+[^\s=>/]+\s*=\s*(?:"[^"]*"|'[^']*'))*)\s*(\/?)>|([^<]+)/g;
  let depth = 0;
  let m;
  while ((m = re.exec(xml))) {
    if (m[1] !== undefined) onText?.(m[1]);
    else if (m[2] !== undefined) { depth--; onClose?.(m[2], depth); }
    else if (m[3] !== undefined) {
      const self = m[5] === '/';
      onOpen?.(m[3], parseAttrs(m[4] || ''), self, depth);
      if (!self) depth++;
    } else if (m[6] !== undefined) onText?.(decodeXml(m[6]));
  }
}

/**
 * First `Assets/…cs:<line>` (else `Packages/…`) location in a stack trace, POSIX-separated. Takes the Mono form
 * (`…\Foo.cs:42`) and the .NET form (`…\Foo.cs:line 42`, from `dotnet test` TRX files).
 * @param {string} stack
 * @returns {string}
 */
export function firstAssetsLocation(stack) {
  const text = String(stack || '');
  for (const root of ['Assets', 'Packages']) {
    const re = new RegExp(`(?:^|[\\s\\\\/(])(${root}[\\\\/][^:\\r\\n]*?\\.cs):(?:line )?(\\d+)`, 'm');
    const m = re.exec(text);
    if (m) return `${m[1].replace(/\\/g, '/')}:${m[2]}`;
  }
  return '';
}

const num = (v) => (v === undefined || v === '' || Number.isNaN(Number(v)) ? null : Number(v));
const stripDll = (s) => String(s || '').replace(/\.dll$/i, '');

/**
 * Parse an NUnit 3 result file.
 * @param {string} xml
 * @param {{assemblies?: string[]}} [opts] keep only these test assemblies (names with or without .dll, case-insensitive);
 *   an empty or absent list keeps everything. Used to leave package suites (e.g. Netcode's) out of the verdict.
 * @returns {{source:'nunit', total:number, passed:number, failed:number, skipped:number, inconclusive:number,
 *   failures:{name:string, message:string, location:string}[], assemblies:{name:string,total:number,passed:number,failed:number,skipped:number}[]}}
 */
export function parseNUnitXml(xml, opts = {}) {
  const keep = (opts.assemblies || []).map((a) => stripDll(a).toLowerCase()).filter(Boolean);
  const wanted = (name) => !keep.length || keep.includes(stripDll(name).toLowerCase());
  let run = null;
  const asmStack = [];
  const assemblies = [];
  const cases = [];
  let current = null; // test-case being read
  let field = null; // 'message' | 'stack-trace'
  walkXml(xml, {
    onOpen(name, attrs, self, depth) {
      if (name === 'test-run') run = attrs;
      else if (name === 'test-suite' && attrs.type === 'Assembly') {
        const a = { name: stripDll(attrs.name), total: num(attrs.total) ?? 0, passed: num(attrs.passed) ?? 0, failed: num(attrs.failed) ?? 0, skipped: num(attrs.skipped) ?? 0, depth };
        assemblies.push(a);
        if (!self) asmStack.push(a);
      } else if (name === 'test-case') {
        const c = { name: attrs.fullname || attrs.name || '?', result: attrs.result || '', label: attrs.label || '', assembly: asmStack.length ? asmStack[asmStack.length - 1].name : null, message: '', stack: '' };
        cases.push(c);
        if (!self) current = c;
      } else if (current && (name === 'message' || name === 'stack-trace')) {
        field = self ? null : name;
      }
    },
    onClose(name, depth) {
      if (name === 'test-case') current = null;
      else if (name === 'message' || name === 'stack-trace') field = null;
      else if (name === 'test-suite' && asmStack.length && asmStack[asmStack.length - 1].depth === depth) asmStack.pop();
    },
    onText(text) {
      if (!current || !field) return;
      if (field === 'message') current.message += text;
      else current.stack += text;
    },
  });
  const inScope = cases.filter((c) => c.assembly === null || wanted(c.assembly));
  const failures = inScope
    .filter((c) => c.result === 'Failed')
    .map((c) => ({ name: c.name, message: c.message.trim(), location: firstAssetsLocation(c.stack) }));
  const count = (r) => inScope.filter((c) => c.result === r).length;
  let total;
  let passed;
  let failed;
  let skipped;
  let inconclusive;
  const scopedAssemblies = assemblies.filter((a) => wanted(a.name));
  if (keep.length && assemblies.length) {
    total = scopedAssemblies.reduce((s, a) => s + a.total, 0);
    passed = scopedAssemblies.reduce((s, a) => s + a.passed, 0);
    failed = scopedAssemblies.reduce((s, a) => s + a.failed, 0);
    skipped = scopedAssemblies.reduce((s, a) => s + a.skipped, 0);
    inconclusive = Math.max(0, total - passed - failed - skipped);
  } else if (run && num(run.total) !== null) {
    total = num(run.total);
    passed = num(run.passed) ?? count('Passed');
    failed = num(run.failed) ?? count('Failed');
    skipped = num(run.skipped) ?? count('Skipped');
    inconclusive = num(run.inconclusive) ?? count('Inconclusive');
  } else {
    total = inScope.length;
    passed = count('Passed');
    failed = count('Failed');
    skipped = count('Skipped');
    inconclusive = count('Inconclusive');
  }
  // Never let a count hide a listed failure (a truncated or odd file must not read as green).
  failed = Math.max(failed, failures.length);
  total = Math.max(total, passed + failed + skipped);
  return {
    source: 'nunit',
    total,
    passed,
    failed,
    skipped,
    inconclusive,
    failures,
    assemblies: scopedAssemblies.map(({ depth, ...a }) => a),
  };
}

// Unity Test Protocol TestState values [C: Unity.TestProtocol TestStateEnum]: 0 Inconclusive, 1 NotRunnable, 2 Skipped,
// 3 Ignored, 4 Success, 5 Failure, 6 Error, 7 Cancelled. Strings are accepted too.
const UTP_STATE = { 0: 'inconclusive', 1: 'skipped', 2: 'skipped', 3: 'skipped', 4: 'passed', 5: 'failed', 6: 'failed', 7: 'failed' };

/**
 * Parse `##utp:{json}` lines out of a Unity log (the fallback when no NUnit file was written, e.g. a crash mid-run).
 * Only TestStatus messages in their End phase count; a test reported twice keeps its last state.
 * @param {string} text
 * @returns {{source:'utp', total:number, passed:number, failed:number, skipped:number, inconclusive:number, failures:{name:string,message:string,location:string}[], messages:number}}
 */
export function parseUtpLog(text) {
  const tests = new Map();
  let messages = 0;
  for (const line of String(text || '').split(/\r?\n/)) {
    const i = line.indexOf('##utp:');
    if (i < 0) continue;
    let msg;
    try {
      msg = JSON.parse(line.slice(i + 6).trim());
    } catch {
      continue;
    }
    messages++;
    if (!msg || msg.type !== 'TestStatus') continue;
    if (msg.phase && msg.phase !== 'End') continue;
    const raw = msg.state;
    const state = typeof raw === 'number' ? UTP_STATE[raw] : String(raw || '').toLowerCase().replace(/^success$/, 'passed').replace(/^(failure|error|cancelled)$/, 'failed');
    tests.set(msg.name || `#${tests.size}`, { state: state || 'inconclusive', message: msg.message || '', stack: msg.stackTrace || msg.stacktrace || '' });
  }
  const all = [...tests.entries()];
  const by = (s) => all.filter(([, t]) => t.state === s).length;
  const failures = all.filter(([, t]) => t.state === 'failed').map(([name, t]) => ({ name, message: String(t.message).trim(), location: firstAssetsLocation(t.stack) }));
  return { source: 'utp', total: all.length, passed: by('passed'), failed: by('failed'), skipped: by('skipped'), inconclusive: by('inconclusive'), failures, messages };
}

/**
 * Parse a results file: NUnit XML when it looks like one, else a log with ##utp lines.
 * @param {string} text
 * @param {{assemblies?: string[]}} [opts]
 */
export function parseResults(text, opts = {}) {
  return /<test-run\b|<test-suite\b/.test(text) ? parseNUnitXml(text, opts) : parseUtpLog(text);
}

/** Gate verdict rule for a parsed run: PASS iff at least one test ran and none failed. */
export function resultsPass(r) {
  return Boolean(r) && r.total > 0 && r.failed === 0;
}

/** @param {string[]} argv */
export async function main(argv) {
  const { values, positionals } = parseCli(argv, { assemblies: { type: 'string' } });
  const file = positionals[0];
  if (!file) throw new UsageError('usage: parse_nunit.mjs <results.xml|log> [--assemblies A;B] [--json]');
  if (!fs.existsSync(file)) {
    const res = { ok: false, exit: EXIT.USAGE, error: `no such file: ${file}`, total: 0, passed: 0, failed: 0, skipped: 0, failures: [] };
    emit(res, { json: values.json, text: `parse_nunit: no such file: ${file}` });
    return EXIT.USAGE;
  }
  const assemblies = values.assemblies ? values.assemblies.split(/[;,]/).map((s) => s.trim()).filter(Boolean) : [];
  const r = parseResults(readText(file), { assemblies });
  const ok = resultsPass(r);
  const exit = ok ? EXIT.OK : EXIT.FAIL;
  const res = { ok, exit, source: r.source, total: r.total, passed: r.passed, failed: r.failed, skipped: r.skipped, failures: r.failures };
  const lines = [`${r.source}: ${r.passed}/${r.total} passed, ${r.failed} failed, ${r.skipped} skipped${r.total === 0 ? ' (zero tests ran: FAIL)' : ''}`];
  for (const f of r.failures.slice(0, 40)) lines.push(`  FAIL ${f.name}${f.location ? `  at ${f.location}` : ''}\n       ${f.message.split('\n').slice(0, 4).join('\n       ')}`);
  emit(res, { json: values.json, text: lines });
  return exit;
}

if (isMain(import.meta.url)) await runCli(main);

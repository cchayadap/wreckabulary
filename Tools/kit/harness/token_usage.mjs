// Token roll-up from Claude Code session transcripts, deduplicated by message.id (a streamed assistant message is
// written once per content block, each copy carrying the same usage).
// CLI: node Tools/kit/harness/token_usage.mjs [--session <id>] [--all] [--since <utc>] [--json]   exit 0, or 3 when no
// transcript is found. Reads *.jsonl and */subagents/**/*.jsonl in every folder of findClaudeProjectDirs(W), plus
// .harness/session.json transcript_path when it lies elsewhere; --session uses findTranscript.
import fs from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';
import { parseUtc } from '../lib/time.mjs';
import { readJsonOr } from '../lib/jsonio.mjs';
import { findWorkspace, findClaudeProjectDirs, findTranscript, fromShellPath } from '../lib/paths.mjs';
import { listFiles } from '../lib/hash.mjs';
import { EXIT, CliError, UsageError, parseCli, emit, isMain, runCli } from '../lib/cli.mjs';
import { isObj, exists } from '../hooks/lib/harness.mjs';

const FIELDS = ['input_tokens', 'output_tokens', 'cache_read_input_tokens', 'cache_creation_input_tokens'];

/** Transcript files for a workspace (all sessions) or for one session. */
export function transcriptFiles(ws, opts = {}) {
  const files = new Set();
  if (opts.session) {
    const t = findTranscript(opts.session, { cwd: ws });
    if (t) {
      files.add(t);
      const sub = path.join(path.dirname(t), opts.session, 'subagents');
      for (const rel of listFiles(sub)) if (rel.endsWith('.jsonl')) files.add(path.join(sub, rel));
    }
    return [...files];
  }
  for (const d of findClaudeProjectDirs(ws)) {
    for (const rel of listFiles(d)) {
      if (!rel.endsWith('.jsonl')) continue;
      const parts = rel.split('/');
      if (parts.length === 1 || parts.includes('subagents')) files.add(path.join(d, rel));
    }
  }
  const s = readJsonOr(path.join(ws, '.harness', 'session.json'), null, { onInvalid: 'fallback' });
  if (isObj(s) && typeof s.transcript_path === 'string') {
    const p = path.resolve(fromShellPath(s.transcript_path));
    if (exists(p)) files.add(p);
  }
  return [...files];
}

/**
 * Sum usage over transcripts, one count per message.id (fallback: the line uuid).
 * @param {string[]} files
 * @param {{since?: Date|null}} [opts]
 */
export async function sumUsage(files, opts = {}) {
  const seen = new Set();
  const sessions = new Set();
  const total = { sessions: 0, assistant_messages: 0, input_tokens: 0, output_tokens: 0, cache_read_input_tokens: 0, cache_creation_input_tokens: 0, by_model: {} };
  for (const f of files) {
    const rl = readline.createInterface({ input: fs.createReadStream(f, { encoding: 'utf8' }), crlfDelay: Infinity });
    for await (const line of rl) {
      if (!line.includes('"usage"')) continue;
      let o;
      try { o = JSON.parse(line); } catch { continue; }
      const m = o?.message;
      if (!isObj(o) || o.type !== 'assistant' || !isObj(m) || !isObj(m.usage)) continue;
      if (opts.since) {
        const ts = parseUtc(o.timestamp);
        if (ts && ts < opts.since) continue;
      }
      const key = typeof m.id === 'string' && m.id ? `id:${m.id}` : `uuid:${o.uuid ?? `${f}:${seen.size}`}`;
      if (seen.has(key)) continue;
      seen.add(key);
      if (typeof o.sessionId === 'string') sessions.add(o.sessionId);
      const model = typeof m.model === 'string' ? m.model : 'unknown';
      const bm = total.by_model[model] || (total.by_model[model] = { assistant_messages: 0, input_tokens: 0, output_tokens: 0, cache_read_input_tokens: 0, cache_creation_input_tokens: 0 });
      total.assistant_messages++;
      bm.assistant_messages++;
      for (const k of FIELDS) {
        const v = Number(m.usage[k]);
        if (Number.isFinite(v) && v > 0) { total[k] += v; bm[k] += v; }
      }
    }
  }
  total.sessions = sessions.size || (files.length ? new Set(files.map((f) => path.basename(f).replace(/\.jsonl$/, ''))).size : 0);
  return total;
}

export async function main(argv) {
  const { values } = parseCli(argv, { session: { type: 'string' }, all: { type: 'boolean', default: false }, since: { type: 'string' } });
  const since = values.since ? parseUtc(values.since) : null;
  if (values.since && !since) throw new UsageError('--since needs a UTC ISO time, e.g. 2026-10-01T00:00:00Z');
  const ws = findWorkspace() ?? process.cwd();
  const files = transcriptFiles(ws, { session: values.session });
  if (!files.length) throw new CliError(`no transcripts found for ${values.session ? `session ${values.session}` : ws}`, EXIT.INFRA, { files: 0 });
  const r = { cmd: 'token_usage', ok: true, files: files.length, ...(await sumUsage(files, { since })) };
  emit(r, { json: values.json, text: [`${r.assistant_messages} assistant messages in ${r.sessions} session(s): input ${r.input_tokens}, output ${r.output_tokens}, cache read ${r.cache_read_input_tokens}, cache write ${r.cache_creation_input_tokens}`] });
  return 0;
}

if (isMain(import.meta.url)) await runCli(main);

// PostCompact hook: archives compact_summary to Docs/compactions/<UTC stamp>.md and appends one DEVLOG line naming
// that file. The line is not a `## <UTC>` entry, so a compaction never counts as bookkeeping for gate_close.
import fs from 'node:fs';
import path from 'node:path';
import { now, utcIso, utcFileStamp } from '../lib/time.mjs';
import { isMain } from '../lib/cli.mjs';
import { runHook, redactSecrets, loadPolicy } from './lib/harness.mjs';

export async function main() {
  await runHook('post_compact_archive', (input, { ws, invalid }) => {
    if (invalid) return null;
    const t = now();
    const dir = path.join(ws, 'Docs', 'compactions');
    fs.mkdirSync(dir, { recursive: true });
    let name = `${utcFileStamp(t)}.md`;
    for (let k = 2; fs.existsSync(path.join(dir, name)) && k < 100; k++) name = `${utcFileStamp(t)}-${k}.md`;
    const trigger = input.trigger === 'manual' ? 'manual' : 'auto';
    const raw = typeof input.compact_summary === 'string' && input.compact_summary.trim() ? input.compact_summary : '(empty summary)';
    // Docs/ is shared: a summary quoting a pasted key must not carry it (the inbox redaction, CONTRACTS.md §5.2).
    const summary = redactSecrets(raw, process.env, loadPolicy(ws).policy).text;
    const sid = typeof input.session_id === 'string' ? input.session_id : 'unknown';
    fs.writeFileSync(path.join(dir, name), `# Compaction ${utcIso(t)} (${trigger})\n\nsession: ${sid}\n\n${summary.trimEnd()}\n`, 'utf8');
    const devlog = path.join(ws, 'Docs', 'DEVLOG.md');
    const rel = `Docs/compactions/${name}`;
    const head = fs.existsSync(devlog) ? '' : '# DEVLOG\n';
    let sep = '';
    try {
      const buf = fs.readFileSync(devlog);
      if (buf.length && buf[buf.length - 1] !== 0x0a) sep = '\n';
    } catch { /* new file */ }
    fs.appendFileSync(devlog, `${head}${sep}- ${utcIso(t)} compaction (${trigger}): summary archived to ${rel}\n`, 'utf8');
    return null;
  });
}

if (isMain(import.meta.url)) await main();

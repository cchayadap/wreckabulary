// StopFailure hook (its output is ignored by Claude Code): appends {utc, session_id, error, details, resets_text,
// resets_utc} to .harness/stopfailure.jsonl for the supervisor. resets_utc is the next occurrence of
// "resets <time> (<zone>)" from the last assistant message. U-17: every error value is logged.
import path from 'node:path';
import { now, utcIso } from '../lib/time.mjs';
import { appendJsonl } from '../lib/jsonio.mjs';
import { isMain } from '../lib/cli.mjs';
import { runHook, parseResets } from './lib/harness.mjs';

const str = (v) => (typeof v === 'string' ? v : null);

export async function main() {
  await runHook('stop_failure_log', (input, { ws, invalid }) => {
    if (invalid) return null;
    const t = now();
    const error = str(input.error) ?? str(input.error_type) ?? 'unknown';
    const details = str(input.error_details) ?? str(input.error_message) ?? null;
    const msg = str(input.last_assistant_message) ?? '';
    const { resets_text: resetsText, resets_utc: resetsUtc } = parseResets(msg, t);
    appendJsonl(path.join(ws, '.harness', 'stopfailure.jsonl'), {
      utc: utcIso(t),
      session_id: str(input.session_id) ?? 'unknown',
      error,
      details: details === null ? null : details.slice(0, 2000),
      resets_text: resetsText,
      resets_utc: resetsUtc,
    });
    return null;
  });
}

if (isMain(import.meta.url)) await main();

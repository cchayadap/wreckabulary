// Notification hook (quota_auto_resume_*, idle_prompt, permission_prompt): appends a quotaLine to
// .harness/quota.jsonl so the supervisor can see limit resumes and an auto-mode fallback to prompting (U-32).
import path from 'node:path';
import { now, utcIso } from '../lib/time.mjs';
import { appendJsonl } from '../lib/jsonio.mjs';
import { isMain } from '../lib/cli.mjs';
import { runHook } from './lib/harness.mjs';

export async function main() {
  await runHook('notification_log', (input, { ws, invalid }) => {
    if (invalid) return null;
    const line = {
      utc: utcIso(now()),
      ...(typeof input.session_id === 'string' ? { session_id: input.session_id } : {}),
      notification_type: typeof input.notification_type === 'string' && input.notification_type ? input.notification_type : 'unknown',
      ...(typeof input.message === 'string' ? { message: input.message.slice(0, 2000) } : {}),
      ...(typeof input.title === 'string' ? { title: input.title.slice(0, 500) } : {}),
    };
    appendJsonl(path.join(ws, '.harness', 'quota.jsonl'), line);
    return null;
  });
}

if (isMain(import.meta.url)) await main();

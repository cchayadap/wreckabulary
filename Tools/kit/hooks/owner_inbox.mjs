// UserPromptSubmit hook: records every owner prompt in Docs/OWNER_INBOX.md (secrets redacted first) and applies OWNER:
// commands. It NEVER blocks: a block would cancel an auto-continue wait. ARCHITECTURE §6.3; CONTRACTS.md §5.2.
import fs from 'node:fs';
import path from 'node:path';
import { now, utcIso, addMinutes } from '../lib/time.mjs';
import { readJsonl, appendJsonl, writeJsonAtomic } from '../lib/jsonio.mjs';
import { withLockSync } from '../lib/locks.mjs';
import { sha256Text } from '../lib/hash.mjs';
import { isMain } from '../lib/cli.mjs';
import { runHook, loadPolicy, redactSecrets, safeText, isObj, parseCommands } from './lib/harness.mjs';

export { parseCommands };

/** Inbox entry text: redacted; lines that would read as an entry heading are indented so the file stays parseable. */
function entryText(text) {
  return text.split(/\r?\n/).map((l) => (/^## /.test(l) || /^status:/.test(l) ? ` ${l}` : l)).join('\n').trimEnd();
}

function nextInboxId(file) {
  let max = 0;
  const t = safeText(file) || '';
  for (const m of t.matchAll(/^## \S+ #(\d+)[ \t]*$/gm)) max = Math.max(max, Number(m[1]));
  return max + 1;
}

export async function main() {
  await runHook('owner_inbox', (input, { ws, invalid }) => {
    if (invalid || typeof input.prompt !== 'string') return null;
    const prompt = input.prompt;
    const { policy } = loadPolicy(ws);
    const skipExact = policy.inbox?.skip_prompts || [];
    const skipPrefix = policy.inbox?.skip_prefixes || [];
    if (skipExact.includes(prompt) || skipExact.includes(prompt.trim())) return null;
    if (skipPrefix.some((p) => typeof p === 'string' && p && prompt.trimStart().startsWith(p))) return null;

    const t = now();
    const utc = utcIso(t);
    const sid = typeof input.session_id === 'string' ? input.session_id : null;
    const promptSha = sha256Text(prompt);
    const entrypoint = process.env.CLAUDE_CODE_ENTRYPOINT ?? null;
    const nonInteractive = entrypoint !== null && (policy.owner?.noninteractive_entrypoints || ['sdk-cli', 'sdk-ts', 'sdk-py']).includes(entrypoint);
    const red = redactSecrets(prompt, process.env, policy);

    const inboxFile = path.join(ws, 'Docs', 'OWNER_INBOX.md');
    let id = 0;
    const append = () => {
      id = nextInboxId(inboxFile);
      fs.mkdirSync(path.dirname(inboxFile), { recursive: true });
      const head = fs.existsSync(inboxFile) ? '' : '# Owner inbox\n\nWritten only by the UserPromptSubmit hook (Tools/kit/hooks/owner_inbox.mjs). Acknowledge items in Docs/owner_acks.md.\n';
      fs.appendFileSync(inboxFile, `${head}\n## ${utc} #${id}\n${entryText(red.text)}\nstatus: new\n`, 'utf8');
    };
    try {
      withLockSync(path.join(ws, '.harness', 'owner_inbox.lock'), append, { waitMs: 4000, purpose: 'owner_inbox' });
    } catch {
      append();
    }

    const notes = [];
    if (red.kinds.length) {
      try {
        writeJsonAtomic(path.join(ws, '.harness', 'alerts', 'secret_in_prompt'), {
          utc, level: 'warn', message: `A secret (${red.kinds.join(', ')}) was pasted into owner prompt #${id} and redacted from Docs/OWNER_INBOX.md. Rotate that key.`, value: null,
        });
      } catch { /* the redaction itself already happened */ }
      notes.push('a secret was redacted from the inbox; rotate that key');
    }

    const applied = [];
    const cmds = parseCommands(prompt);
    if (cmds.length && nonInteractive) {
      notes.push(`OWNER commands not applied: this session's entrypoint (${entrypoint}) is non-interactive`);
    } else {
      const H = (...p) => path.join(ws, '.harness', ...p);
      const base = { utc, by: 'owner_inbox', inbox_id: id, ...(sid ? { session_id: sid } : {}), prompt_sha256: promptSha };
      for (const c of cmds) {
        if (c.cmd === 'pause') { writeJsonAtomic(H('PAUSE'), base); applied.push('pause'); }
        else if (c.cmd === 'resume') {
          for (const f of ['PAUSE', 'ALLOW_STOP_OWNER']) { try { fs.rmSync(H(f), { force: true }); } catch { /* gone */ } }
          applied.push('resume');
        } else if (c.cmd === 'stop') { writeJsonAtomic(H('ALLOW_STOP_OWNER'), base); applied.push('stop'); }
        else if (c.cmd === 'unlock') {
          const ttl = Number(policy.ttl_min?.unlock) > 0 ? Number(policy.ttl_min.unlock) : 60;
          writeJsonAtomic(H('HARNESS_UNLOCK'), { utc, expires_utc: utcIso(addMinutes(t, ttl)), inbox_id: id, ...(sid ? { session_id: sid } : {}), prompt_sha256: promptSha });
          applied.push(`unlock harness (${ttl} min)`);
        } else if (c.cmd === 'lock') { try { fs.rmSync(H('HARNESS_UNLOCK'), { force: true }); } catch { /* gone */ } applied.push('lock harness'); }
        else if (c.cmd === 'confirm') {
          if (!sid) { notes.push(`confirm ${c.row} ignored: no session id`); continue; }
          appendJsonl(H('owner_confirmations.jsonl'), {
            utc, row: c.row, session_id: sid, prompt_sha256: promptSha, inbox_id: id,
            ...(typeof input.transcript_path === 'string' ? { transcript_path: input.transcript_path } : {}),
            entrypoint,
          });
          applied.push(`confirm ${c.row}`);
        } else if (c.cmd === 'override') {
          if (redactSecrets(c.command, process.env, policy).kinds.length) { notes.push('override refused: the command contains a secret'); continue; }
          const ttl = Number(policy.ttl_min?.override) > 0 ? Number(policy.ttl_min.override) : 30;
          const lines = readJsonl(H('overrides.jsonl'));
          let n = 0;
          for (const l of lines) { const m = isObj(l) && /^ov-(\d+)$/.exec(String(l.id)); if (m) n = Math.max(n, Number(m[1])); }
          appendJsonl(H('overrides.jsonl'), {
            event: 'grant', id: `ov-${n + 1}`, utc, expires_utc: utcIso(addMinutes(t, ttl)), command: c.command, inbox_id: id,
            ...(sid ? { session_id: sid } : {}), prompt_sha256: promptSha,
          });
          applied.push(`override ov-${n + 1} (${ttl} min, single use)`);
        } else notes.push(`unknown owner command "${c.line.slice(0, 60)}" (known: pause, resume, stop, confirm <row>, unlock harness, lock harness, OWNER-OVERRIDE: <command>)`);
      }
    }
    const ctx = `Owner inbox #${id} recorded in Docs/OWNER_INBOX.md${applied.length ? `; applied: ${applied.join(', ')}` : ''}${notes.length ? `; ${notes.join('; ')}` : ''}. Acknowledge it in Docs/owner_acks.md.`;
    return { stdout: { hookSpecificOutput: { hookEventName: 'UserPromptSubmit', additionalContext: ctx } } };
  });
}

if (isMain(import.meta.url)) await main();

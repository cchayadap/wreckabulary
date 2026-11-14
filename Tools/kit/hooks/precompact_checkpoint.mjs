// PreCompact hook: flushes .harness/checkpoint.json (wave, unit statuses, next action) before a compaction.
// It NEVER blocks: blocking an auto-compaction makes the request fail.
import path from 'node:path';
import { now, utcIso } from '../lib/time.mjs';
import { writeJsonAtomic } from '../lib/jsonio.mjs';
import { isMain } from '../lib/cli.mjs';
import { runHook, newestWaveManifest, unitStatusMap, nextAction } from './lib/harness.mjs';

export async function main() {
  await runHook('precompact_checkpoint', (input, { ws, invalid }) => {
    if (invalid) return null;
    const t = now();
    const wave = newestWaveManifest(ws);
    const na = nextAction(ws, t);
    writeJsonAtomic(path.join(ws, '.harness', 'checkpoint.json'), {
      utc: utcIso(t),
      ...(typeof input.session_id === 'string' ? { session_id: input.session_id } : {}),
      trigger: input.trigger === 'manual' ? 'manual' : 'auto',
      wave: typeof wave?.manifest?.wave === 'string' ? wave.manifest.wave : null,
      units: wave ? unitStatusMap(wave.manifest) : {},
      next_action: na,
    });
    return null;
  });
}

if (isMain(import.meta.url)) await main();

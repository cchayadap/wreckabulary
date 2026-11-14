// SubagentStop hook: a subagent whose first prompt line is GSK-LANE:<wave>/<unit> must leave
// BuildNotes/<wave>/<unit>.done.json or .blocked.json. Without one it is blocked ONCE per agent ("write your done
// marker or a BLOCKED note"), then allowed and logged. Workflow templates never depend on this (U-04).
import path from 'node:path';
import { now, utcIso } from '../lib/time.mjs';
import { readJsonOr, writeJsonAtomic } from '../lib/jsonio.mjs';
import { isMain } from '../lib/cli.mjs';
import { unitFiles } from '../lib/paths.mjs';
import { runHook, laneOfAgent, findAgentTranscript, exists, isObj } from './lib/harness.mjs';

export async function main() {
  await runHook('lane_deliverable_check', (input, { ws, invalid }) => {
    if (invalid || input.stop_hook_active === true) return null;
    const lane = laneOfAgent(input, ws);
    if (!lane) return null;
    const f = unitFiles(ws, lane.wave, lane.unit);
    if (exists(f.done) || exists(f.blocked)) return null;
    const key = typeof input.agent_id === 'string' && input.agent_id ? input.agent_id : path.basename(findAgentTranscript(input, ws) || 'unknown', '.jsonl');
    const file = path.join(ws, '.harness', 'lane_blocks.json');
    const doc = readJsonOr(file, {}, { onInvalid: 'fallback' });
    const blocks = isObj(doc) ? doc : {};
    const prev = isObj(blocks[key]) ? blocks[key] : null;
    const laneId = `${lane.wave}/${lane.unit}`;
    if (prev && prev.count >= 1) {
      blocks[key] = { ...prev, count: prev.count, utc: prev.utc, lane: laneId, allowed_utc: utcIso(now()) };
      writeJsonAtomic(file, blocks);
      return null;
    }
    blocks[key] = { count: 1, utc: utcIso(now()), lane: laneId };
    writeJsonAtomic(file, blocks);
    const rel = (p) => path.relative(ws, p).split(path.sep).join('/');
    return { stdout: { decision: 'block', reason: `GSK-LANE ${laneId}: write ${rel(f.done)} (or .blocked.json) before stopping` } };
  });
}

if (isMain(import.meta.url)) await main();

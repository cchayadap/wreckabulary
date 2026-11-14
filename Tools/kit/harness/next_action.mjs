// The single next thing the main loop should do (ARCHITECTURE §7.4 step 1).
// CLI: node Tools/kit/harness/next_action.mjs [--json]   → harness.schema.json#/$defs/nextAction; exit 0.
// Priority: stop/emergency alerts > PAUSE > unacknowledged owner inbox > freeze (≥ freeze_at_pct of the run window)
// > failing gate rows > a wave with todo/failed units > the open milestone's gate > the first open TODO > idle.
import { findWorkspace } from '../lib/paths.mjs';
import { parseCli, emit, isMain, runCli } from '../lib/cli.mjs';
import { nextAction } from '../hooks/lib/harness.mjs';

export { nextAction };

export async function main(argv) {
  const { values } = parseCli(argv);
  const ws = findWorkspace() ?? process.cwd();
  const r = nextAction(ws);
  emit(r, { json: values.json, text: [`${r.kind}: ${r.detail}`, ...(r.refs.length ? [`refs: ${r.refs.join(', ')}`] : [])] });
  return 0;
}

if (isMain(import.meta.url)) await runCli(main);

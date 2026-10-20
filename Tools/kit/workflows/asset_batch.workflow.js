export const meta = {
  name: 'gsk-asset-batch',
  description: 'Game Studio Kit asset batch: generate each asset with assetgen, then checks, a fresh reviewer, an owner-simulator refuter, and accept or a focused retry (at most 3 attempts per stage)',
  whenToUse: 'Generate and review a batch of game assets through Tools/kit/assetgen. Args: {workspace, assets:[{id, stage, preset, provider, endpoint, prompt_file, refs:[path]}], max_parallel}',
  phases: [
    { title: 'Canary', detail: 'one cheap call; nothing starts if it dies' },
    { title: 'Assets', detail: 'per asset: generate, review, refute, accept or retry' },
    { title: 'Reconcile', detail: 'reconcile orphans and write the cost report from the ledger' },
  ],
}

// Installed as Tools/kit/workflows/asset_batch.workflow.js (source: skills/game-asset-generation/assets/workflows/).
// Launch from the main session with the Workflow tool: {scriptPath: 'Tools/kit/workflows/asset_batch.workflow.js',
// args}; if a scriptPath outside the session is refused, read this file and pass it inline as `script`.
//
// args (CONTRACTS §9.2; the fields marked optional are additive):
//   workspace      workspace root W (where Tools/kit/kit.json lives)
//   assets[]       {id, stage, preset, provider, endpoint, prompt_file, refs:[path]}
//                  optional per asset: params_file, n, check_class, asset_class, anchors:[path], brief
//   max_parallel   generation agents in flight at once (default 2). Provider concurrency is still enforced by
//                  assetgen's pools, so extra agents would only wait for a slot.
//   optional: lane (default 'asset-batch'), attempts (default 3, never more than 3)
//
// Rules this template encodes (game-asset-generation SKILL.md hard rules):
// - only assetgen spends money; agents never call providers another way and never edit budget, accounts or the lock;
// - a timeout or unknown submit is resumed by request id, never resubmitted;
// - numbers first: a failed check is a reject with that check as the failed criterion, before any reviewer runs;
// - the generator never reviews; a fresh reviewer and an owner-simulator refuter decide; the owner confirms later;
// - a retry changes only the failed criterion; at most 3 attempts per asset stage.
// Agent prompts do NOT start with GSK-LANE: the SubagentStop hook would then demand a build done marker.

const A = args || {}
const WS = A.workspace
const LANE = A.lane || 'asset-batch'
const MAX_ATTEMPTS = Math.min(3, Math.max(1, A.attempts || 3))
const MAX_PARALLEL = Math.max(1, A.max_parallel || 2)
const AG = 'uv run --project Tools/kit/assetgen assetgen'
const REQUIRED = ['id', 'stage', 'preset', 'provider', 'endpoint', 'prompt_file']

// --- run / need / breaker (same shape as build_wave, ARCHITECTURE §7.2) --------------------------------------------
const TRIES = 2, BREAKER_AT = 2, NULL_TRIP = 3
const breaker = { failed: [], nulls: 0, tripped: false }
const RESUME = (i) => `\n\n(Retry ${i}: an earlier attempt died. Read the attempt folders under ArtSource/_gen/ first; resume by request id with ${AG} resume or poll, never resubmit.)`
function trip(why) {
  if (breaker.tripped) return
  breaker.tripped = true
  log(`BREAKER OPEN (${why}): likely a usage limit or auth problem. No new agents start.`)
}
async function run(prompt, opts) {
  for (let i = 1; i <= TRIES; i++) {
    if (breaker.tripped) { log(`skip ${opts.label}: breaker open`); return null }
    const r = await agent(i === 1 ? prompt : prompt + RESUME(i), i === 1 ? opts : { ...opts, label: `${opts.label}#${i}` })
    if (r) { breaker.nulls = 0; return r }
    breaker.nulls++
    if (breaker.nulls >= NULL_TRIP) trip(`${breaker.nulls} consecutive dead agents`)
  }
  breaker.failed.push(opts.label)
  if (breaker.failed.length >= BREAKER_AT) trip(`chains ${breaker.failed.join(', ')} exhausted`)
  return null
}

// A small limiter so at most MAX_PARALLEL generation agents run at once (promises only; no timers needed).
function limiter(n) {
  let active = 0
  const queue = []
  const next = () => {
    if (active >= n || !queue.length) return
    active++
    const { fn, resolve } = queue.shift()
    fn().then(resolve, () => resolve(null)).then(() => { active--; next() })
  }
  return (fn) => new Promise((resolve) => { queue.push({ fn, resolve }); next() })
}
const genSlot = limiter(MAX_PARALLEL)

// --- schemas --------------------------------------------------------------------------------------------------------
const GEN = { type: 'object', properties: {
  attempt_id: { type: 'string' }, exit: { type: 'number' }, state: { type: 'string' }, reason: { type: 'string' },
  outputs: { type: 'array', items: { type: 'string' } }, checks_ok: { type: 'boolean' },
  check_failures: { type: 'array', items: { type: 'string' } }, est: { type: 'number' }, actual: { type: 'number' },
  unit: { type: 'string' }, tier: { type: 'string' }, prompt_file: { type: 'string' }, note: { type: 'string' } },
  required: ['exit'] }
const REVIEW = { type: 'object', properties: {
  verdict: { type: 'string', enum: ['accept', 'reject'] }, score: { type: 'number' }, best_output: { type: 'number' },
  worst_defects: { type: 'array', items: { type: 'string' } }, failed_criterion: { type: 'string' },
  files_opened: { type: 'array', items: { type: 'string' } } },
  required: ['verdict', 'score', 'worst_defects', 'files_opened'] }
const REFUTE = { type: 'object', properties: {
  owner_would_reject: { type: 'boolean' }, defect: { type: 'string' }, failed_criterion: { type: 'string' } },
  required: ['owner_would_reject'] }
const RECORD = { type: 'object', properties: {
  ok: { type: 'boolean' }, exit: { type: 'number' }, tier: { type: 'string' }, ships: { type: 'boolean' }, note: { type: 'string' } },
  required: ['ok'] }

// --- prompts --------------------------------------------------------------------------------------------------------
// Refs keep their optional ":role" suffix; assetgen treats an item without a valid role as role "ref".
const q = (p) => `"${String(p).replace(/"/g, '')}"`
const flags = (a, promptFile) => [
  `--asset ${a.id}`, `--stage ${a.stage}`, `--preset ${a.preset}`, `--provider ${a.provider}`, `--endpoint ${a.endpoint}`,
  `--prompt-file ${q(promptFile)}`,
  (a.refs && a.refs.length) ? `--refs ${q(a.refs.join(','))}` : '',
  a.params_file ? `--params-file ${q(a.params_file)}` : '', a.n ? `--n ${a.n}` : '',
  a.check_class ? `--check-class ${a.check_class}` : '', a.asset_class ? `--asset-class ${a.asset_class}` : '',
  `--lane ${LANE}`, '--json',
].filter(Boolean).join(' ')
const retryFile = (a, round) => a.prompt_file.replace(/(\.[^./\\]+)?$/, `.r${round}$1`)

const genPrompt = (a, round, prev) => [
  `ASSET-BATCH generator: asset "${a.id}", stage "${a.stage}", round ${round} of ${MAX_ATTEMPTS}. Workspace: ${WS}.`,
  a.brief ? `What the asset is for: ${a.brief}` : '',
  round === 1
    ? `Use the prompt file ${a.prompt_file} as it is.`
    : `First write a new prompt file ${retryFile(a, round)} (Write tool, UTF-8) that copies ${prev.prompt_file} and changes ONLY this failed criterion: "${prev.failed}". Keep every feature the reviewer approved.`,
  `From the workspace root run exactly one generation command, with no output redirection:`,
  `${AG} run ${flags(a, round === 1 ? a.prompt_file : retryFile(a, round))}`,
  `Rules: this command is the only way you may spend money; never call a provider another way; never pass --new-attempt; never edit ArtSource/_gen/budget.json, ArtSource/_gen/accounts.json, Docs/style_lock.json or Tools/kit/**.`,
  `If it exits 3 with reason "timeout", run ${AG} resume <attempt_id> --json once. If it exits 3 with "submission_unknown", or 75, stop and report: never resubmit and never release.`,
  `Do not judge the images. Report: attempt_id, exit, state, reason, outputs (W-relative paths), checks_ok and check_failures (from the result or asset_checks.json), est, actual, unit, tier, and prompt_file (the file you used).`,
].filter(Boolean).join('\n')

const reviewPrompt = (a, g) => [
  `ASSET-BATCH reviewer (fresh context: you did not write the prompt or run the job). Workspace: ${WS}. Asset "${a.id}", stage "${a.stage}", attempt ${g.attempt_id}.`,
  `Open at full size every output: ${JSON.stringify(g.outputs || [])}; the anchors ${JSON.stringify(a.anchors || [])} and those listed in Docs/style_lock.json; Docs/STYLE_BIBLE.md; and the attempt's asset_checks.json.`,
  `Optionally build a sheet: uv run --project Tools/kit/capture Tools/kit/capture/contact_sheet.py --in <outputs> --anchor <anchor> --labels --out ArtSource/_review/${a.id}_${a.stage}.png --json`,
  `A GLB cannot be viewed directly: render it with node Tools/kit/blender/bl_run.mjs turntable.py -- --in <glb> --out ArtSource/_review/${a.id}_${a.stage}_turntable --sheet ArtSource/_review/${a.id}_${a.stage}_turntable.png --shading texture, open that sheet, and read the mesh metrics in asset_checks.json.`,
  a.brief ? `What the asset is for: ${a.brief}` : '',
  `Judge: brief (subject, count, framing), anchor fit (shape language, palette, rendering, detail), identity for variants and sheets, readability at 64 px and 256 px, contamination (text, logo, watermark, brand, likeness, baked shadow), technical agreement with the check metrics, and whether the next step can use it.`,
  `Return verdict accept or reject, score 1-10 against the anchors, best_output (index), the 3 worst defects with file and region, the single failed_criterion a retry should change, and files_opened (every file you actually opened). Do not run assetgen and do not edit files.`,
].filter(Boolean).join('\n')

const refutePrompt = (a, g, r) => [
  `ASSET-BATCH owner simulator. Workspace: ${WS}. A reviewer accepted ${g.attempt_id} (asset "${a.id}", output ${r.best_output || 0}, score ${r.score}).`,
  `Read Docs/OWNER_INBOX.md (what the owner complained about before), Docs/STYLE_BIBLE.md, and open the output at full size next to the anchors.`,
  `Try to refute the accept: would the owner reject this, and for which concrete, visible defect? Set owner_would_reject=true only with a concrete defect you can point at; name the failed_criterion. Do not edit files.`,
].join('\n')

const recordPrompt = (a, g, accept, reason, output) => [
  `ASSET-BATCH recorder. Workspace: ${WS}. From the workspace root run exactly:`,
  accept
    ? `${AG} accept ${g.attempt_id} --reason "${reason.replace(/"/g, "'")}" --by reviewer --output ${output} --json`
    : `${AG} reject ${g.attempt_id} --reason "${reason.replace(/"/g, "'")}" --by reviewer --json`,
  `Report ok, exit, and for accept the tier, ships and note fields. If the tier is reference_only, do not copy anything into Assets/. Do not run any other command.`,
].join('\n')

// --- per-asset loop -------------------------------------------------------------------------------------------------
const capped = new Set()
const HARD_STOP = new Set(['hard_cap', 'research_only', 'blocked_plan', 'uploads_not_ok', 'policy', 'cost_param', 'unsafe_path', 'unsafe_ref'])

async function processAsset(a) {
  const history = []
  let prev = null
  for (let round = 1; round <= MAX_ATTEMPTS; round++) {
    if (breaker.tripped) return { id: a.id, stage: a.stage, status: 'stopped_breaker', history }
    if (capped.has(a.provider)) return { id: a.id, stage: a.stage, status: 'skipped_hard_cap', history }
    // Re-check the cap after the slot is granted: an asset may have queued before another one hit the cap.
    const g = await genSlot(() => (capped.has(a.provider)
      ? Promise.resolve({ skipped: true })
      : run(genPrompt(a, round, prev), { label: `gen:${a.id}:r${round}`, phase: 'Assets', schema: GEN })))
    if (g && g.skipped) return { id: a.id, stage: a.stage, status: 'skipped_hard_cap', history }
    if (!g) return { id: a.id, stage: a.stage, status: 'dead', history }
    history.push({ round, attempt_id: g.attempt_id, exit: g.exit, state: g.state, reason: g.reason, actual: g.actual, unit: g.unit })
    if (g.exit === 75) return { id: a.id, stage: a.stage, status: 'blocked', reason: g.reason, history }
    if (g.exit === 3 || g.exit === 2) return { id: a.id, stage: a.stage, status: g.exit === 3 ? 'infra' : 'usage', reason: g.reason, history }
    if (g.exit === 1 && HARD_STOP.has(g.reason)) {
      if (g.reason === 'hard_cap') { capped.add(a.provider); log(`${a.provider}: hard cap reached; later ${a.provider} assets are skipped`) }
      return { id: a.id, stage: a.stage, status: 'refused', reason: g.reason, history }
    }
    const promptFile = g.prompt_file || (prev ? prev.prompt_file : a.prompt_file)
    if (g.exit !== 0 || !g.checks_ok) {
      const failed = (g.check_failures && g.check_failures.length) ? `checks: ${g.check_failures.slice(0, 3).join('; ')}` : `run failed: ${g.reason || 'exit ' + g.exit}`
      if (g.attempt_id && g.state === 'settled') await run(recordPrompt(a, g, false, failed, 0), { label: `reject:${a.id}:r${round}`, phase: 'Assets', schema: RECORD, effort: 'low' })
      prev = { prompt_file: promptFile, failed }
      continue
    }
    const r = await run(reviewPrompt(a, g), { label: `review:${a.id}:r${round}`, phase: 'Assets', schema: REVIEW })
    if (!r) return { id: a.id, stage: a.stage, status: 'dead', history }
    let verdict = r.verdict
    let failed = r.failed_criterion || (r.worst_defects || [])[0] || 'reviewer rejected'
    if (verdict === 'accept') {
      const x = await run(refutePrompt(a, g, r), { label: `refute:${a.id}:r${round}`, phase: 'Assets', schema: REFUTE })
      if (!x) return { id: a.id, stage: a.stage, status: 'dead', history }
      if (x.owner_would_reject) { verdict = 'reject'; failed = x.failed_criterion || x.defect || failed }
    }
    const reason = verdict === 'accept'
      ? `score ${r.score}/10 vs anchors; worst: ${(r.worst_defects || []).slice(0, 2).join('; ') || 'none'}`
      : `failed: ${failed}`
    const rec = await run(recordPrompt(a, g, verdict === 'accept', reason, r.best_output || 0), { label: `${verdict}:${a.id}:r${round}`, phase: 'Assets', schema: RECORD, effort: 'low' })
    history[history.length - 1].verdict = verdict
    history[history.length - 1].score = r.score
    if (verdict === 'accept') {
      return { id: a.id, stage: a.stage, status: rec && rec.ok ? 'agent-verified' : 'accept_not_recorded', attempt_id: g.attempt_id,
        tier: rec && rec.tier, ships: rec && rec.ships, note: rec && rec.note, history }
    }
    prev = { prompt_file: promptFile, failed }
  }
  log(`${a.id}/${a.stage}: ${MAX_ATTEMPTS} attempts without an accept; change the route or simplify the asset, and ask the owner`)
  return { id: a.id, stage: a.stage, status: 'exhausted', last_failed: prev && prev.failed, history }
}

// --- main -----------------------------------------------------------------------------------------------------------
if (!WS || !Array.isArray(A.assets) || !A.assets.length) return { status: 'invalid_args', error: 'need {workspace, assets:[…], max_parallel}' }
const valid = A.assets.filter((a) => a && REQUIRED.every((k) => typeof a[k] === 'string' && a[k].length))
const invalid = A.assets.filter((a) => !valid.includes(a)).map((a) => (a && a.id) || '(no id)')
if (invalid.length) log(`dropped ${invalid.length} asset(s) with missing fields: ${invalid.join(', ')}`)

phase('Canary')
const canary = await agent('Reply with ok=true and nothing else. Do not use tools.', { label: 'canary', schema: { type: 'object', properties: { ok: { type: 'boolean' } }, required: ['ok'] }, effort: 'low' })
if (!canary) return { status: 'incomplete', stage: 'canary', reason: 'canary died: usage limit or auth; nothing was generated', breaker }

phase('Assets')
log(`${valid.length} asset(s), at most ${MAX_PARALLEL} generating at once, ${MAX_ATTEMPTS} attempt(s) each`)
const results = (await parallel(valid.map((a) => () => processAsset(a)))).map((r, i) => r || { id: valid[i].id, stage: valid[i].stage, status: 'dead' })

phase('Reconcile')
const RECON = { type: 'object', properties: { new_orphans: { type: 'number' }, stuck: { type: 'number' },
  providers: { type: 'object' }, warnings: { type: 'array', items: { type: 'string' } } }, required: ['new_orphans', 'stuck'] }
const recon = await run([
  `ASSET-BATCH bookkeeper. Workspace: ${WS}. From the workspace root run, one after the other:`,
  `${AG} reconcile --since 2d --json`,
  `${AG} report --json`,
  `Report new_orphans, the number of stuck reservations, providers (spent, reserved, hard per provider from the report) and warnings. Do not release, receipt or settle anything.`,
].join('\n'), { label: 'reconcile', schema: RECON, effort: 'low' })

const by = (s) => results.filter((r) => r.status === s).map((r) => r.id)
const notDone = results.filter((r) => r.status !== 'agent-verified')
if (notDone.length) log(`not accepted: ${notDone.map((r) => `${r.id} (${r.status}${r.reason ? ': ' + r.reason : ''})`).join(', ')}`)
return {
  status: breaker.tripped ? 'incomplete' : 'complete',
  accepted: by('agent-verified'),
  exhausted: by('exhausted'),
  refused: results.filter((r) => r.status === 'refused').map((r) => ({ id: r.id, reason: r.reason })),
  infra_or_blocked: results.filter((r) => ['infra', 'blocked', 'usage'].includes(r.status)).map((r) => ({ id: r.id, status: r.status, reason: r.reason })),
  skipped: by('skipped_hard_cap').concat(by('stopped_breaker')),
  dropped_invalid: invalid,
  capped: [...capped],
  results,
  reconcile: recon,
  breaker,
  next: 'Present agent-verified assets to the owner (pending owner review). Resume infra/blocked ones with assetgen poll --all or reconcile; never resubmit.',
}

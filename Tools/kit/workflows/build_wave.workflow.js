export const meta = {
  name: 'gsk-build-wave',
  description: 'Game Studio Kit wave: canary, contracts, builders with review/refute/fix, one Unity integrator, audit',
  whenToUse: 'Launch one milestone wave of a game build with the args printed by node Tools/kit/harness/wave_manifest.mjs args <wave>',
  phases: [
    { title: 'Canary', detail: 'one cheap call; if it dies, launch nothing' },
    { title: 'Contracts', detail: 'shared types, loader, ownership map' },
    { title: 'Contracts review', detail: 'reviewer, refuter, fixer' },
    { title: 'Build', detail: 'per unit: build, review, 2 refuters, fix' },
    { title: 'Integrate', detail: 'the single Unity integrator (R2-R4, gate rows)' },
    { title: 'Audit', detail: 'done markers, house rules, completeness critic' },
  ],
}

// Game Studio Kit build wave (owner skill: game-studio-run; ARCHITECTURE §7.1-§7.2 with critic C-2).
// The script cannot touch the disk and has no clock. Everything that must survive a dead agent lives in files the
// agents write (progress files, done markers, gates.jsonl rows) and in args, which the main loop rebuilds from
// Docs/waves/<wave>.json. Relaunching with fresh args re-runs only todo and failed units; finished units return at
// Step 0. Attempt-1 prompt text is built from args only, so it never changes between launches.

const A = args || {}
const WAVE = A.wave
const WS = A.workspace
const UNITS = Array.isArray(A.units) ? A.units : null
const CONTRACTS = Array.isArray(A.contracts) ? A.contracts : []
const TRIES = 2          // a limit death returns in about 1 s; a third try never helped (KNOTWELL wf_7cc87fca-996)
const BREAKER_AT = 2     // exhausted retry chains before no new agent starts
const NULL_TRIP = 3      // consecutive dead agents across all chains before no new agent starts
const breaker = { failed: [], nulls: 0, tripped: false, why: null }

const stop = (stage, extra) => ({ status: 'incomplete', stage, wave: WAVE, breaker, ...(extra || {}) })

function badUnit(u) {
  if (!u || typeof u !== 'object') return 'not an object'
  if (typeof u.id !== 'string' || !u.id) return 'no id'
  if (!Array.isArray(u.owned) || !u.owned.length) return `${u.id}: no owned paths`
  if (typeof u.brief !== 'string' || !u.brief) return `${u.id}: no brief`
  return null
}

const argProblems = []
if (typeof WAVE !== 'string' || !WAVE) argProblems.push('args.wave missing')
if (typeof WS !== 'string' || !WS) argProblems.push('args.workspace missing')
if (!UNITS) argProblems.push('args.units must be an array (it may be empty for an integrate-only relaunch)')
for (const u of [...(UNITS || []), ...CONTRACTS]) { const b = badUnit(u); if (b) argProblems.push(b) }
if (argProblems.length) {
  log(`Invalid args, nothing launched: ${argProblems.join('; ')}`)
  return { status: 'invalid-args', wave: WAVE || null, problems: argProblems, breaker }
}

function trip(why) {
  if (breaker.tripped) return
  breaker.tripped = true
  breaker.why = why
  log(`BREAKER OPEN (${why}): likely a usage limit or an auth blip. No new agents start.`)
}

const RESUME = (i) => `\n\n(Retry ${i}: an earlier attempt died. Read BuildNotes/${WAVE}/ progress files and your owned paths first; continue, do not restart.)`

async function run(prompt, opts) {
  for (let i = 1; i <= TRIES; i++) {
    if (breaker.tripped) { log(`skip ${opts.label}: breaker open`); return null }
    const r = await agent(i === 1 ? prompt : prompt + RESUME(i), i === 1 ? opts : { ...opts, label: `${opts.label}#${i}` })
    if (r) { breaker.nulls = 0; return r }
    breaker.nulls++
    if (breaker.nulls >= NULL_TRIP) trip(`${breaker.nulls} consecutive dead agents`)
  }
  breaker.failed.push(opts.label)
  if (breaker.failed.length >= BREAKER_AT) trip(`retry chains exhausted: ${breaker.failed.join(', ')}`)
  return null
}

// Never feed a later stage fewer inputs than it expects: "8 dead checkers" must not read as "0 issues".
function need(results, expected, stage) {
  const ok = results.filter(Boolean)
  if (breaker.tripped || ok.length < expected) {
    log(`${stage}: ${ok.length}/${expected} inputs; not starting it`)
    return null
  }
  return ok
}

const ISSUE = {
  type: 'object',
  properties: {
    id: { type: 'string' }, file: { type: 'string' }, claim: { type: 'string' },
    severity: { type: 'string', enum: ['blocker', 'major', 'minor'] },
  },
  required: ['id', 'claim', 'severity'],
}
const UNIT = {
  type: 'object',
  properties: {
    unit: { type: 'string' }, status: { type: 'string', enum: ['done', 'blocked'] },
    files: { type: 'array', items: { type: 'string' } }, verify: { type: 'string' }, summary: { type: 'string' },
    contract_changes: { type: 'array', items: { type: 'string' } }, requests: { type: 'array', items: { type: 'string' } },
  },
  required: ['unit', 'status', 'files', 'verify', 'summary'],
}
const REVIEW = { type: 'object', properties: { issues: { type: 'array', items: ISSUE } }, required: ['issues'] }
const REFUTE = {
  type: 'object',
  properties: {
    verdicts: {
      type: 'array',
      items: { type: 'object', properties: { id: { type: 'string' }, refuted: { type: 'boolean' }, why: { type: 'string' } }, required: ['id', 'refuted'] },
    },
    new_issues: { type: 'array', items: ISSUE },
  },
  required: ['verdicts'],
}
const INTEG = {
  type: 'object',
  properties: {
    status: { type: 'string', enum: ['green', 'red', 'blocked'] },
    rows: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          row: { type: 'string' },
          verdict: { type: 'string', enum: ['PASS', 'FAIL', 'BLOCKED', 'NOT-RUN'] },
          evidence: { type: 'array', items: { type: 'string' } },
        },
        required: ['row', 'verdict'],
      },
    },
    unity_launches: { type: 'number' }, notes: { type: 'string' },
  },
  required: ['status', 'rows'],
}
const OK = { type: 'object', properties: { ok: { type: 'boolean' } }, required: ['ok'] }

const gate = (c) => `node Tools/kit/gate/game_gate.mjs ${c} --json`
const notes = (id) => `BuildNotes/${WAVE}/${id}`

const unitPrompt = (u, mode) => [
  `GSK-LANE:${WAVE}/${u.id}`,
  `You are builder "${u.id}" in wave ${WAVE} of a game build. Workspace: ${WS}. Design snapshot hash: ${A.design_hash || 'none (design not frozen)'}.`,
  `Owned paths (write ONLY here): ${u.owned.join(', ')}. Context pack: ${u.context || `${notes(u.id)}.context.md`}. Contracts: BuildNotes/${WAVE}/CONTRACTS.md (additive changes only; list them in contract_changes; any other cross-unit need goes in requests).`,
  `Scratch files go only in BuildNotes/${WAVE}/scratch/${u.id}/, never in a folder another lane uses. Write files with the Write and Edit tools, not shell heredocs.`,
  mode === 'fix'
    ? `Step 0 (FIX PASS): do NOT return early. If ${notes(u.id)}.done.json exists, rename it to ${u.id}.done.prev.json first. If only ${u.id}.done.prev.json exists, you are resuming a fix: continue.`
    : `Step 0: if ${notes(u.id)}.done.json exists and the command in its "verify" field (default: ${gate('check all')}) is still green, return the marker's content unchanged and do nothing else.`,
  `Step 1: create or append ${notes(u.id)}.progress.md; its first line is "STATUS: IN PROGRESS", then one line per finished step. If the file already exists you are RESUMING: read it and your owned files, then continue.`,
  `Step 2: implement: ${u.brief}`,
  `Step 3: verify with rungs R0-R1 only: ${(Array.isArray(u.verify) && u.verify.length ? u.verify : ['check all', 'core']).map(gate).join(' ; ')}. Do NOT launch Unity: the integrator owns R2 and above. The type-check sees the whole tree, so ignore errors in files you do not own, and leave none in yours.`,
  `Step 4: every feature ships with a test that can fail.`,
  `Step 5: write ${notes(u.id)}.done.json LAST: {unit, status:"done", files[], verify:"<one command that re-checks this unit>", summary, contract_changes[], requests[]}. If you cannot finish, write ${notes(u.id)}.blocked.json {unit, reason, needs} instead. Return the same object.`,
].join('\n')

const reviewPrompt = (u, r) => `Fresh-context reviewer for unit ${u.id} of wave ${WAVE} (${WS}). Read ${notes(u.id)}.done.json, every file it lists, the context pack ${u.context || `${notes(u.id)}.context.md`} and BuildNotes/${WAVE}/CONTRACTS.md. Report only concrete defects: contract breaks, missing or unfalsifiable tests, rule violations (Docs/kit/HARNESS_RULES.md, CLAUDE.md), numbers that differ from the design, files written outside the owned paths (${u.owned.join(', ')}). Cite file and line. Do not edit files.\nUnit summary: ${r.summary}`

const refutePrompt = (u, issues, lens) => `Refuter (${lens}) for unit ${u.id}, wave ${WAVE} (${WS}). For each issue, try to REFUTE it from the code and the design text. Set refuted=true only when you can cite evidence that the issue is false; otherwise refuted=false. Then, through your lens, add up to 3 new_issues the reviewer missed. Do not edit files.\nIssues: ${JSON.stringify(issues)}`

const fixPrompt = (u, issues) => `${unitPrompt(u, 'fix')}\n\nFIX PASS: resolve these confirmed issues inside your owned paths, re-verify with R0-R1, then write a new done marker whose summary lists the issue ids you fixed and any you could not fix (with the reason):\n${JSON.stringify(issues)}`

phase('Canary')
const canary = await agent('Reply with ok=true and nothing else. Do not use tools.', { label: 'canary', schema: OK, effort: 'low' })
if (!canary) return stop('canary', { reason: 'canary died: usage limit or auth; nothing else was launched' })

if (CONTRACTS.length) {
  phase('Contracts')
  const c = need(await parallel(CONTRACTS.map((k) => () => run(unitPrompt(k), { label: `contracts:${k.id}`, phase: 'Contracts', schema: UNIT }))), CONTRACTS.length, 'contracts review')
  if (!c) return stop('contracts')
  const cBlocked = c.filter((x) => x.status !== 'done')
  if (cBlocked.length) return stop('contracts', { blocked: cBlocked.map((x) => x.unit) })
  phase('Contracts review')
  const rev = await run(`Review BuildNotes/${WAVE}/CONTRACTS.md and the contract files ${c.flatMap((x) => x.files).join(', ')} of wave ${WAVE} (${WS}) for ambiguity, missing ownership, overlapping owned paths and loader strictness. Cite lines. Do not edit files.`, { label: 'contracts-review', phase: 'Contracts review', schema: REVIEW })
  if (!rev) return stop('contracts review')
  const ref = await run(refutePrompt({ id: 'contracts' }, rev.issues, 'buildability: can every builder implement against this without guessing?'), { label: 'contracts-refute', phase: 'Contracts review', schema: REFUTE })
  if (!ref) return stop('contracts refute')
  const kept = rev.issues.filter((i) => !(ref.verdicts.find((v) => v.id === i.id) || {}).refuted).concat(ref.new_issues || [])
  if (kept.some((i) => i.severity !== 'minor')) {
    const fixed = await run([
      `GSK-LANE:${WAVE}/contracts-fix`,
      `Fix these contract issues of wave ${WAVE} (${WS}) in BuildNotes/${WAVE}/CONTRACTS.md and the contract files, additively where possible, then re-verify with ${gate('check all')}. Write ${notes('contracts-fix')}.done.json last.`,
      JSON.stringify(kept),
    ].join('\n'), { label: 'contracts-fix', phase: 'Contracts review', schema: UNIT })
    if (!fixed) return stop('contracts fix')
  }
}

phase('Build')
if (!UNITS.length) log('No todo or failed units in args: integrate-only relaunch (units come from their done markers).')
const LENSES = [
  'correctness and contracts',
  'owner simulator: what would the owner complain about next (feel, visuals, embodiment, missing states, placeholders)',
]
const built = await pipeline(UNITS,
  (_, u) => run(unitPrompt(u), { label: `build:${u.id}`, phase: 'Build', schema: UNIT }),
  async (r, u) => {
    if (!r || r.status !== 'done') return r && { r, issues: [] }
    const v = await run(reviewPrompt(u, r), { label: `review:${u.id}`, phase: 'Build', schema: REVIEW })
    return v && { r, issues: v.issues }
  },
  async (x, u) => {
    if (!x || !x.issues.length) return x && { r: x.r, confirmed: [] }
    const votes = (await parallel(LENSES.map((l, i) => () => run(refutePrompt(u, x.issues, l), { label: `refute${i + 1}:${u.id}`, phase: 'Build', schema: REFUTE })))).filter(Boolean)
    if (votes.length < LENSES.length) { log(`${u.id}: ${votes.length}/${LENSES.length} refuters returned; the unit stays unfinished`); return null }
    const confirmed = x.issues.filter((i) => !votes.every((v) => (v.verdicts.find((d) => d.id === i.id) || {}).refuted))
      .concat(votes.flatMap((v) => v.new_issues || []))
    return { r: x.r, confirmed }
  },
  async (y, u) => {
    if (!y) return null
    const serious = y.confirmed.filter((i) => i.severity !== 'minor')
    const minor = y.confirmed.filter((i) => i.severity === 'minor')
    if (!serious.length) return { ...y.r, minor }
    const f = await run(fixPrompt(u, serious), { label: `fix:${u.id}`, phase: 'Build', schema: UNIT })
    return f && { ...f, minor, fixed_issues: serious.map((i) => i.id) }
  })
const units = need(built, UNITS.length, 'integrate')
if (!units) return stop('build', { done: built.filter(Boolean).map((u) => u.unit) })
const blocked = units.filter((u) => u.status !== 'done')
if (blocked.length > (A.max_blocked || 0)) return stop('build', { blocked: blocked.map((b) => b.unit) })

phase('Integrate')
const integ = await run([
  `GSK-LANE:${WAVE}/integrator`,
  `You are the ONLY agent that runs Unity for wave ${WAVE} of a game build (${WS}). Budget: at most ${A.unity_launch_budget || 14} Unity launches; batch commands and get R0 green before any Unity rung.`,
  `Units finished in this run: ${JSON.stringify(units.map((u) => ({ unit: u.unit, status: u.status, files: u.files, summary: u.summary, requests: u.requests || [], contract_changes: u.contract_changes || [] })))}. Also read every BuildNotes/${WAVE}/*.done.json: units finished in earlier runs are not in this list.`,
  `Step 1: create or append ${notes('integrator')}.progress.md ("STATUS: IN PROGRESS", then one line per finished step); if it exists you are RESUMING.`,
  `Step 2: resolve the units' requests, then run ${gate('check all')} ; ${gate('compile')} ; ${gate('test editmode')} ; ${gate('test playmode')}.`,
  `Step 3: run the gate rows ${JSON.stringify(A.gate_rows || [])} of milestone ${A.milestone || '(see args)'}: look up each row's cmd or probe in Docs/qa/gates.json and run it through its tool with --row <id> (game_gate rows: node Tools/kit/gate/game_gate.mjs <cmd> --json --row <id>; probe rows: node Tools/kit/qa/probe_report.mjs <run dir> --row <id> --json). The tools append Docs/qa/gates.jsonl; never write it by hand.`,
  `Never weaken, skip or delete a test. If a test encodes a design number the code cannot meet, leave it failing and report it. On BLOCKED (exit 75) stop and report; on INFRA (exit 3) report it as infrastructure, not as a code failure.`,
  `Step 4: write ${notes('integrator')}.done.json last {unit:"integrator", files[], verify, summary}, and return {status: green|red|blocked, rows[{row, verdict, evidence[]}], unity_launches, notes}.`,
].join('\n'), { label: 'integrate', phase: 'Integrate', schema: INTEG })
if (!integ) return stop('integrate', { units: units.map((u) => u.unit) })

phase('Audit')
const owned = [...UNITS, ...CONTRACTS].map((u) => ({ id: u.id, owned: u.owned }))
const audits = await parallel([
  () => run(`Audit wave ${WAVE} (${WS}): for every unit in ${JSON.stringify(owned)} check that BuildNotes/${WAVE}/<unit>.done.json (or .blocked.json) exists, validates as {unit, files[], verify, summary}, and that every listed file lies inside that unit's owned paths. Report violations only, as issues. Do not edit files.`, { label: 'audit-done', phase: 'Audit', schema: REVIEW }),
  () => run(`In ${WS}, run node Tools/kit/dev/unity_rules_lint.mjs --json and ${gate('check asmdef')}. Report each violation as an issue with file and line. Do not edit files.`, { label: 'audit-rules', phase: 'Audit', schema: REVIEW }),
  () => run(`Completeness critic for wave ${WAVE} (${WS}). Compare the rows of Docs/qa/gates/${A.milestone || '<milestone>'}.md and Docs/qa/gates.json with Docs/qa/gates.jsonl and the integrator result ${JSON.stringify(integ.rows)}. What is missing: a row not run, evidence not produced, a modality skipped (clip, frame sheet, built player), a claim with no file behind it? Report each gap as an issue. Do not edit files.`, { label: 'audit-critic', phase: 'Audit', schema: REVIEW }),
])
const deadAudits = audits.filter((a) => !a).length
if (deadAudits) log(`${deadAudits}/3 audits did not return; treat the wave as unaudited until they rerun.`)

const STATUS = { green: 'complete', red: 'integrated-red', blocked: 'integrated-blocked' }
return {
  status: integ.status === 'green' && deadAudits ? 'unaudited' : STATUS[integ.status],
  integration: integ.status,
  wave: WAVE,
  milestone: A.milestone || null,
  units: units.map((u) => ({ unit: u.unit, status: u.status, minor: u.minor || [], fixed_issues: u.fixed_issues || [] })),
  integrate: integ,
  audits: audits.map((a) => (a ? a.issues : null)),
  breaker,
}

export const meta = {
  name: 'gsk-gate-review',
  description: 'Game Studio Kit milestone review: fresh reviewer on clips, sheets and numbers, two owner-simulator refuters, a completeness critic, verified defects, and a ledger row',
  whenToUse: 'After the integrator is green and before gate_close for a milestone of a game project (Tools/kit/workflows/gate_review.workflow.js)',
  phases: [
    { title: 'Canary', detail: 'one cheap call; stop if it dies' },
    { title: 'Prepare', detail: 'inventory evidence, validate checks.json, make missing frame sheets' },
    { title: 'Review', detail: 'fresh-context reviewer: scores, worst defects, claims with evidence' },
    { title: 'Refute', detail: 'owner-inbox simulator and embodiment/world simulator attack every PASS-looking claim' },
    { title: 'Critic', detail: 'card rows without evidence, modalities not run' },
    { title: 'Confirm', detail: 'one verifier per new blocker or major defect' },
    { title: 'Record', detail: 'Docs/reviews/<M>.md, review.json, KNOWN_ISSUES rows, probe_report --row' },
  ],
}

// Game Studio Kit (MIT), owned by the game-qa-probes skill. Installed to <W>/Tools/kit/workflows/.
// Advisory: gate_close.mjs decides whether a milestone closes. This workflow produces one reviewer verdict row.
// args (CONTRACTS §9.2): {workspace, milestone, card, evidence:{checks:[], clips:[], sheets:[], captures:[]},
//   owner_inbox_since}, plus optional {review_row, stamp, anchors:[], areas:[]}.
//   review_row: the card row id the verdict is recorded under (e.g. "M2-09"); without it nothing is appended to the ledger.
//   stamp: UTC file stamp YYYYMMDDTHHMMSSZ for the review run folder (scripts cannot read the clock).
// Verdict rule (deterministic, in this script): NOT-RUN without checks.json evidence or when a stage died; FAIL on any
// confirmed blocker, any blocking gap from the critic, any checks.json that is invalid or not PASS, or frame sheets
// nobody opened; PASS otherwise.

const A = args || {}
const WS = A.workspace || '.'
const M = A.milestone || 'M?'
const CARD = A.card || `Docs/qa/gates/${M}.md`
const EV = A.evidence || {}
const LIST = (k) => (Array.isArray(EV[k]) ? EV[k] : [])
const CHECKS = LIST('checks'), CLIPS = LIST('clips'), SHEETS = LIST('sheets'), CAPTURES = LIST('captures')
const SINCE = A.owner_inbox_since || null
const STAMP = typeof A.stamp === 'string' && /^\d{8}T\d{6}Z$/.test(A.stamp) ? A.stamp : null
const RUN_DIR = STAMP ? `Docs/qa/runs/${STAMP}-gate_review_${M}` : `Docs/qa/runs/<UTC file stamp now>-gate_review_${M}`
const MAX_CONFIRM = 12

const TRIES = 2, BREAKER_AT = 2, NULL_TRIP = 3
const breaker = { failed: [], nulls: 0, tripped: false }
const RESUME = (i) => `\n\n(Retry ${i}: an earlier attempt died. Re-read what exists under ${RUN_DIR} and continue; do not redo finished work.)`
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
const stop = (stage, extra) => ({ status: 'incomplete', verdict: 'NOT-RUN', milestone: M, stage, breaker, ...(extra || {}) })

// Owner-complaint taxonomy (WoO run report, rewritten; see the skill's references/owner-complaint-taxonomy.md).
const TAXONOMY = [
  'pose or retarget wrong: leaning back, arms hanging off a weapon, A-pose while walking (idle_spine_lean_deg, bind_pose_while_moving)',
  'floating or sinking: body above the visible floor, dragged root, death pose through the ground (stance_height_error_cm, pose_float_cm, death_pose_ground_penetration_cm)',
  'sliding: feet skate or drift sideways in run or idle (stance_foot_slide_cm_per_step, idle_foot_drift_*, foot_heading_vs_facing_deg)',
  'legs crossing or a terrible run on non-prototype bodies (foot_crossing_frames_per_cycle on EVERY body x clip)',
  'held prop spinning or clipping the face (prop_angular_velocity_deg_s, prop_up_axis_error_deg, prop_over_face_pct, grip_offset_cm)',
  'stuck on or unable to cross an obstacle; a route that needs a jump (goldpath_no_jump_completes, jumpable/blocking margins)',
  'placeholders: primitives, default or error materials, missing models near the route (placeholder_objects)',
  'walking through props, invisible walls, visual surface far from the collision surface (collider_render_divergence, orphan_colliders)',
  'NPCs with the wrong or a frozen animation controller (animator_missing_controller, animator_unexpected_controller)',
  'hitches, stutter, a slower build than yesterday (worst_frame_after_warmup_ms, first_visit_hitch_ms, perf_regression_p95_pct)',
  'bad backgrounds, sky seams, blurry downsampled textures, black bake blotches (texture_import_downsample_factor, bake_*)',
  'a crash or error on a normal start of the built game (smoke --stripped, console_exceptions)',
  'mess and waste: trash files, low disk (watchdog, storage policy)',
]

const ISSUE = { type: 'object', properties: {
  id: { type: 'string' }, area: { type: 'string' }, claim: { type: 'string' },
  severity: { type: 'string', enum: ['blocker', 'major', 'minor'] },
  taxonomy: { type: 'string' }, evidence: { type: 'array', items: { type: 'string' } },
  owner_words: { type: 'string', description: 'How the owner would phrase the complaint, in one line' } },
  required: ['id', 'claim', 'severity', 'evidence'] }
const CLAIM = { type: 'object', properties: {
  id: { type: 'string' }, row: { type: 'string' }, claim: { type: 'string' },
  evidence: { type: 'array', items: { type: 'string' } } }, required: ['id', 'claim', 'evidence'] }
const PREP = { type: 'object', properties: {
  present: { type: 'array', items: { type: 'string' } }, missing: { type: 'array', items: { type: 'string' } },
  checks: { type: 'array', items: { type: 'object', properties: { path: { type: 'string' }, result: { type: 'string' },
    valid: { type: 'boolean' }, errors: { type: 'array', items: { type: 'string' } } }, required: ['path', 'valid'] } },
  sheets_made: { type: 'array', items: { type: 'string' } },
  clip_sheets: { type: 'array', items: { type: 'object', properties: { clip: { type: 'string' }, sheet: { type: 'string' } }, required: ['clip'] } },
  run_dir: { type: 'string' }, notes: { type: 'string' } }, required: ['present', 'missing', 'checks', 'run_dir'] }
const REVIEW = { type: 'object', properties: {
  opened: { type: 'array', items: { type: 'string' }, description: 'Every file actually opened (Read) during the review' },
  scores: { type: 'array', items: { type: 'object', properties: { area: { type: 'string' }, score: { type: 'number' }, why: { type: 'string' } }, required: ['area', 'score'] } },
  defects: { type: 'array', items: ISSUE }, claims: { type: 'array', items: CLAIM }, summary: { type: 'string' } },
  required: ['opened', 'scores', 'defects', 'claims'] }
const REFUTE = { type: 'object', properties: {
  opened: { type: 'array', items: { type: 'string' } },
  verdicts: { type: 'array', items: { type: 'object', properties: { id: { type: 'string' }, refuted: { type: 'boolean' },
    why: { type: 'string' }, evidence: { type: 'array', items: { type: 'string' } },
    severity: { type: 'string', enum: ['blocker', 'major', 'minor'] } }, required: ['id', 'refuted'] } },
  new_issues: { type: 'array', items: ISSUE } }, required: ['verdicts', 'new_issues'] }
const CRITIC = { type: 'object', properties: {
  missing: { type: 'array', items: { type: 'object', properties: { row: { type: 'string' }, what: { type: 'string' },
    blocking: { type: 'boolean' } }, required: ['what', 'blocking'] } } }, required: ['missing'] }
const CONFIRM = { type: 'object', properties: { holds: { type: 'boolean' }, why: { type: 'string' },
  severity: { type: 'string', enum: ['blocker', 'major', 'minor'] }, opened: { type: 'array', items: { type: 'string' } } },
  required: ['holds', 'why'] }
const RECORD = { type: 'object', properties: {
  review_md: { type: 'string' }, review_json: { type: 'string' },
  known_issues_added: { type: 'array', items: { type: 'string' } },
  ledger: { type: 'object', properties: { row: { type: 'string' }, verdict: { type: 'string' }, exit: { type: 'number' } } },
  notes: { type: 'string' } }, required: ['review_md', 'review_json', 'known_issues_added'] }

const EVIDENCE_TEXT = JSON.stringify({ checks: CHECKS, clips: CLIPS, sheets: SHEETS, captures: CAPTURES })
const RULES = [
  'Open (Read) every file you cite; never describe an image, sheet or number you have not opened.',
  'Motion defects are invisible in stills: judge motion from frame sheets and probe numbers, never from a single screenshot.',
  'Numbers and images each catch what the other misses: pair every visual judgement with the probe number that should agree, and every number with a picture.',
  'Cite paths relative to the workspace. Do not edit game files; only the Record stage writes files.',
].join('\n- ')

phase('Canary')
const canary = await agent('Reply with ok=true and nothing else. Do not use tools.', { label: 'canary', schema: { type: 'object', properties: { ok: { type: 'boolean' } }, required: ['ok'] }, effort: 'low' })
if (!canary) return stop('canary', { reason: 'canary died: usage limit or auth; nothing else was launched' })
if (!CHECKS.length) {
  log('No checks.json in args.evidence.checks: a review without probe evidence cannot pass. Returning NOT-RUN.')
  return { status: 'complete', verdict: 'NOT-RUN', milestone: M, reason: 'no probe evidence (evidence.checks is empty)', breaker }
}

phase('Prepare')
const prep = await run([
  `Evidence clerk for the ${M} review of the game project at ${WS}. Card: ${CARD}. Evidence: ${EVIDENCE_TEXT}.`,
  `1. For every path, record whether it exists (present / missing).`,
  `2. For every checks.json run: node Tools/kit/qa/checks_validate.mjs <path> --json, and report valid, result and the errors. Then run node Tools/kit/qa/probe_report.mjs <run folder> --json (no --row) so each run folder has a fresh summary.md.`,
  `3. Clips (mp4, gif) cannot be opened as images. For each clip, find its frame sheet (same stem under Docs/qa/sheets/ or next to it). If there is none but a PNG frame folder exists, make one: uv run --project Tools/kit/capture Tools/kit/capture/contact_sheet.py --in <frame folder> --out <folder>/<stem>_sheet.png --select every:15 --labels --json. Report clip_sheets and sheets_made; a clip with no frames stays without a sheet (say so in notes).`,
  `4. Create the review run folder ${RUN_DIR} (mkdir only) and return its exact path in run_dir.`,
  `Do not judge quality. Do not edit anything else.`,
].join('\n'), { label: 'prepare', schema: PREP })
if (!prep) return stop('prepare')
const allSheets = [...new Set([...SHEETS, ...(prep.sheets_made || []), ...(prep.clip_sheets || []).map((c) => c.sheet).filter(Boolean)])]
const invalid = (prep.checks || []).filter((c) => !c.valid || c.result !== 'PASS')
if (invalid.length) log(`${invalid.length} checks.json file(s) invalid or not PASS; the reviewer sees them as defects`)

phase('Review')
const review = await run([
  `Fresh-context reviewer for milestone ${M} of the game project at ${WS}. You have not seen this project before; judge only what the files show.`,
  `Read the gate card ${CARD}, Docs/STYLE_BIBLE.md and the approved anchors (Docs/style_lock.json anchors${Array.isArray(A.anchors) && A.anchors.length ? `, plus ${A.anchors.join(', ')}` : ''}), then the evidence:`,
  `- checks.json runs and their summary.md: ${JSON.stringify(CHECKS)}; validation: ${JSON.stringify(prep.checks)}`,
  `- frame sheets (open every one): ${JSON.stringify(allSheets)}`,
  `- clips (judge through their sheets): ${JSON.stringify(prep.clip_sheets || CLIPS)}`,
  `- captures and story-camera stills: ${JSON.stringify(CAPTURES)}`,
  `- also world_audit*.json, perf.json and visual/*/visual.json inside those run folders (routed visual diffs are yours to judge).`,
  `Areas to score 1-10 against the anchors: ${JSON.stringify(A.areas && A.areas.length ? A.areas : ['characters and motion', 'world and traversal', 'UI and front end', 'performance', 'look vs anchors'])}. For each area list the 3 worst defects with severity (blocker: the owner would reject the milestone; major: must be fixed before the next milestone; minor), a taxonomy class and the evidence path.`,
  `Also list claims: every card row or area you believe PASSES, each with the evidence that proves it. A claim without evidence is not a claim.`,
  `Rules:\n- ${RULES}`,
].join('\n'), { label: 'review', schema: REVIEW })
if (!review) return stop('review')
const opened = new Set(review.opened || [])
const unopened = (x) => (x.evidence || []).filter((p) => !opened.has(p))
const reviewerDefects = (review.defects || []).map((d, i) => ({ ...d, id: d.id || `R${i + 1}`, source: 'reviewer', unverified: unopened(d) }))
const claims = (review.claims || []).map((c, i) => ({ ...c, id: c.id || `C${i + 1}` }))
if (allSheets.length && !allSheets.some((s) => opened.has(s))) log('The reviewer opened none of the frame sheets: motion was not reviewed')

phase('Refute')
const LENSES = [
  { key: 'inbox', text: `Owner-inbox simulator. Read Docs/OWNER_INBOX.md${SINCE ? ` (entries since ${SINCE}, and older ones for the owner's habits)` : ''}, Docs/owner_acks.md, Docs/CHANGE_REQUESTS.md and Docs/KNOWN_ISSUES.md. From what the owner has complained about before, predict what they will point out next in this milestone and look for it in the evidence.` },
  { key: 'embodiment', text: `Embodiment and world simulator. Walk the owner-complaint taxonomy against the frame sheets, clips (through their sheets), world audits and probe numbers. Check every body x clip listed in Docs/qa/probes.config.json bodies, not only the prototype body.` },
]
const refutes = await parallel(LENSES.map((l, i) => () => run([
  `${l.text}`,
  `Game project: ${WS}. Milestone ${M}. Card ${CARD}. Evidence: ${EVIDENCE_TEXT}; sheets: ${JSON.stringify(allSheets)}.`,
  `The reviewer claims these PASS: ${JSON.stringify(claims)}`,
  `The reviewer found these defects: ${JSON.stringify(reviewerDefects.map((d) => ({ id: d.id, claim: d.claim, severity: d.severity })))}`,
  `For EVERY claim, try to refute it: set refuted=true only when you can cite a file you opened that shows it is false, with a severity. Otherwise refuted=false.`,
  `Then add up to 5 new_issues the reviewer missed, each phrased in owner_words ("the knight hovers above the bridge deck") with evidence paths and a taxonomy class.`,
  `Taxonomy:\n- ${TAXONOMY.join('\n- ')}`,
  `Rules:\n- ${RULES}`,
].join('\n'), { label: `refute:${l.key}`, phase: 'Refute', schema: REFUTE })))
const votes = refutes.filter(Boolean)
if (votes.length < LENSES.length) return stop('refute', { reason: `${votes.length}/${LENSES.length} refuters returned`, review })

phase('Critic')
const critic = await run([
  `Completeness critic for milestone ${M} of the game project at ${WS}. Compare the card ${CARD} with Docs/qa/gates.jsonl (latest line per row) and the evidence ${EVIDENCE_TEXT}.`,
  `List what is missing: a card row with no evidence; a modality not run (gold path in a built player, smoke of the stripped release build, every body x clip in Docs/qa/probes.config.json with probe results, world audit of every changed scene, perf.json from a release or release-qa build, clips WITH frame sheets, routed visual diffs judged); a claim with no file; checks.json that is invalid or not PASS: ${JSON.stringify(invalid)}.`,
  `Mark blocking=true when the missing item means the milestone cannot be judged. Do not edit files.`,
].join('\n'), { label: 'critic', schema: CRITIC })
if (!critic) return stop('critic', { review })

phase('Confirm')
const refutedClaims = []
for (const c of claims) {
  const hits = votes.flatMap((v) => (v.verdicts || []).filter((d) => d.id === c.id && d.refuted))
  if (hits.length) refutedClaims.push({ id: `X-${c.id}`, claim: `claim refuted: ${c.claim} (${hits.map((h) => h.why).join(' | ')})`,
    severity: hits.some((h) => h.severity === 'blocker') ? 'blocker' : hits.some((h) => h.severity === 'major') ? 'major' : 'minor',
    evidence: [...new Set(hits.flatMap((h) => h.evidence || []))], source: 'refuter' })
}
const fresh = votes.flatMap((v, i) => (v.new_issues || []).map((n, j) => ({ ...n, id: n.id ? `${LENSES[i].key}-${n.id}` : `${LENSES[i].key}-${j + 1}`, source: `refute:${LENSES[i].key}` })))
const toConfirm = [...refutedClaims, ...fresh, ...reviewerDefects.filter((d) => d.unverified.length)].filter((d) => d.severity !== 'minor')
const dropped = toConfirm.slice(MAX_CONFIRM)
if (dropped.length) log(`Confirm stage capped at ${MAX_CONFIRM}; ${dropped.length} defect(s) remain unconfirmed at their original severity: ${dropped.map((d) => d.id).join(', ')}`)
const confirmed = await pipeline(toConfirm.slice(0, MAX_CONFIRM), (d) => run([
  `Verifier for one defect raised in the ${M} review of the game project at ${WS}. Open the cited evidence yourself and decide whether the defect is real.`,
  `Defect: ${JSON.stringify(d)}`,
  `holds=true only when a file you opened shows it. Give the severity you would assign. Default to holds=false when the evidence does not show it.`,
].join('\n'), { label: `confirm:${d.id}`, phase: 'Confirm', schema: CONFIRM }).then((r) => (r ? { ...d, holds: r.holds, why: r.why, severity: r.severity || d.severity } : { ...d, holds: null, why: 'verifier died' })))
const kept = [
  ...reviewerDefects.filter((d) => !d.unverified.length),
  ...confirmed.filter((d) => d && d.holds !== false),
  ...dropped.map((d) => ({ ...d, holds: null, why: 'not verified (confirm cap)' })),
  ...[...refutedClaims, ...fresh, ...reviewerDefects.filter((d) => d.unverified.length)].filter((d) => d.severity === 'minor'),
]
const blockers = kept.filter((d) => d.severity === 'blocker')
const gaps = (critic.missing || []).filter((m) => m.blocking)
const motionUnreviewed = allSheets.length > 0 && !allSheets.some((s) => opened.has(s))
const confirmationPending = dropped.length > 0 || confirmed.some((d) => !d || d.holds === null)
const verdict = blockers.length || gaps.length || motionUnreviewed || invalid.length || confirmationPending ? 'FAIL' : 'PASS'
log(`${M}: ${verdict} (${blockers.length} blocker(s), ${gaps.length} blocking gap(s), ${invalid.length} checks.json not valid or not PASS${motionUnreviewed ? ', motion not reviewed' : ''})`)

phase('Record')
const record = await run([
  `Scribe for the ${M} review of the game project at ${WS}. Write exactly these files, then run one command.`,
  `1. ${prep.run_dir || RUN_DIR}/review.json: {"schema":1,"milestone":"${M}","verdict":"${verdict}","blockers":[ids],"gaps":[what],"scores":[...],"defects":[...],"claims":[...],"refuted_claims":[ids],"motion_reviewed":${!motionUnreviewed}} using the data below. Do not add a "results" key (probe_report would read it as probe results).`,
  `2. Docs/reviews/${M}.md following Tools/kit/workflows/gate_review.review.md (headings: Verdict, Scores, Blockers, Majors, Minors, Refuted claims, Missing evidence, Evidence opened). If the file exists, move its old content under "Previous reviews" at the end instead of deleting it.`,
  `3. Append one row per blocker and major to Docs/KNOWN_ISSUES.md in its table format (| KI-<next n> | severity | ${M} | <recheck gate> | open | <evidence> |), recheck_at_gate = ${M} for blockers and majors raised against work in this milestone. Skip a defect already listed (same evidence and claim).`,
  A.review_row
    ? `4. Run: node Tools/kit/qa/probe_report.mjs ${prep.run_dir || RUN_DIR} --row ${A.review_row} --json and report its verdict and exit code in ledger. Never write Docs/qa/gates.jsonl by hand.`
    : `4. No review_row was given: do NOT touch Docs/qa/gates.jsonl; run node Tools/kit/qa/probe_report.mjs ${prep.run_dir || RUN_DIR} --json (summary only) and say in notes that the verdict still needs a ledger row.`,
  `Data: ${JSON.stringify({ verdict, scores: review.scores, blockers, majors: kept.filter((d) => d.severity === 'major'), minors: kept.filter((d) => d.severity === 'minor'), refuted_claims: refutedClaims.map((r) => r.id), gaps: critic.missing, opened: [...opened], motion_reviewed: !motionUnreviewed, summary: review.summary })}`,
].join('\n'), { label: 'record', schema: RECORD })
if (!record) return stop('record', { verdict_unrecorded: verdict, blockers, gaps })

return {
  status: 'complete',
  verdict,
  milestone: M,
  run_dir: prep.run_dir,
  scores: review.scores,
  blockers: blockers.map((b) => ({ id: b.id, claim: b.claim, evidence: b.evidence })),
  majors: kept.filter((d) => d.severity === 'major').map((b) => ({ id: b.id, claim: b.claim })),
  gaps,
  refuted_claims: refutedClaims.map((r) => r.id),
  motion_reviewed: !motionUnreviewed,
  confirmation_pending: confirmationPending,
  unconfirmed_defects: kept.filter((d) => d.holds === null).map((d) => d.id),
  record,
  breaker,
}

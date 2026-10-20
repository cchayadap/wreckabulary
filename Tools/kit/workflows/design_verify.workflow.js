export const meta = {
  name: 'gsk-design-verify',
  description: 'Game Studio Kit design verify: canary, optional proposals and judge panel, lens checkers each with a refuter, per-file revisers, cross-file gate',
  whenToUse: 'Verify a game design in Docs/design before any build wave. Launched by the game-design-method skill with {workspace, docs, owner_words_file, lenses, new_design, tools}.',
  phases: [
    { title: 'Canary', detail: 'one cheap call; launch nothing if it dies' },
    { title: 'Proposals', detail: 'new designs only: one proposal per angle' },
    { title: 'Judges', detail: 'new designs only: judge panel, then a chair writes the concept and design' },
    { title: 'Lenses', detail: 'one checker per lens, each followed by an adversarial refuter' },
    { title: 'Revise', detail: 'one reviser per file for confirmed blocker and major issues' },
    { title: 'Gate', detail: 'cross-file gate: coverage of the owner words, acceptance lint, README' },
  ],
}

// Template owned by the game-design-method skill (installed to Tools/kit/workflows/). No filesystem access, no clock and
// no randomness in here: agents read and write the files, and the main loop hashes the design after a PASS.
// Args: {workspace, docs:[path], owner_words_file, lenses?:[id | "id: lens text"], new_design?, angles?:[string],
//        research?:[path], tunings?:[path], design_dir?, tools?:{lint, sim}}.

const A = args || {}
const WS = A.workspace
const DIR = A.design_dir || 'Docs/design'
const DOCS = A.docs || []
const OWNER = A.owner_words_file
const TOOLS = A.tools || {}
const RESEARCH = A.research || []
const TUNINGS = A.tunings || []
if (!WS || !OWNER || (!DOCS.length && !A.new_design)) {
  return { status: 'incomplete', stage: 'args', reason: 'need workspace, owner_words_file, and docs (or new_design: true)' }
}

const TRIES = 2, BREAKER_AT = 2, NULL_TRIP = 3
const breaker = { failed: [], nulls: 0, tripped: false }
const RESUME = (i) => `\n\n(Retry ${i}: an earlier attempt died. Read the files you were writing first; continue, do not restart.)`
function trip(why) {
  if (breaker.tripped) return
  breaker.tripped = true
  log(`BREAKER OPEN (${why}): likely usage limit or auth. No new agents start.`)
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
// A dead checker is never "0 issues": a stage runs only when every input arrived.
function need(results, expected, stage) {
  const ok = results.filter(Boolean)
  if (breaker.tripped || ok.length < expected) { log(`${stage}: ${ok.length}/${expected} inputs; not starting it`); return null }
  return ok
}
const stop = (stage, extra) => ({ status: 'incomplete', stage, breaker, ...(extra || {}) })

const ISSUE = { type: 'object', properties: {
  id: { type: 'string' }, file: { type: 'string' }, where: { type: 'string' }, claim: { type: 'string' },
  fix: { type: 'string' }, severity: { type: 'string', enum: ['blocker', 'major', 'minor'] } },
  required: ['id', 'claim', 'severity', 'fix'] }
const ISSUES = { type: 'object', properties: { issues: { type: 'array', items: ISSUE }, notes: { type: 'string' } }, required: ['issues'] }
const REFUTE = { type: 'object', properties: {
  verdicts: { type: 'array', items: { type: 'object', properties: { id: { type: 'string' }, refuted: { type: 'boolean' },
    why: { type: 'string' }, better_fix: { type: 'string' } }, required: ['id', 'refuted', 'why'] } },
  new_issues: { type: 'array', items: ISSUE } }, required: ['verdicts'] }
const PROPOSAL = { type: 'object', properties: { angle: { type: 'string' }, file: { type: 'string' }, fantasy: { type: 'string' },
  pillars: { type: 'array', items: { type: 'string' } }, loop: { type: 'string' }, risks: { type: 'array', items: { type: 'string' } } },
  required: ['angle', 'file', 'fantasy', 'pillars', 'loop'] }
const JUDGEMENT = { type: 'object', properties: { base: { type: 'string' }, scores: { type: 'object' },
  grafts: { type: 'array', items: { type: 'string' } }, cuts: { type: 'array', items: { type: 'string' } }, why: { type: 'string' } },
  required: ['base', 'grafts', 'why'] }
const CHAIR = { type: 'object', properties: { files: { type: 'array', items: { type: 'string' } }, summary: { type: 'string' } }, required: ['files', 'summary'] }
const REVISED = { type: 'object', properties: { file: { type: 'string' }, applied: { type: 'array', items: { type: 'string' } },
  not_applied: { type: 'array', items: { type: 'object', properties: { id: { type: 'string' }, why: { type: 'string' } }, required: ['id', 'why'] } },
  cross_file: { type: 'array', items: { type: 'object', properties: { file: { type: 'string' }, change: { type: 'string' } }, required: ['file', 'change'] } } },
  required: ['file', 'applied'] }
const GATE = { type: 'object', properties: {
  verdict: { type: 'string', enum: ['PASS', 'FAIL', 'BLOCKED'] },
  blockers: { type: 'array', items: { type: 'string' } },
  wishes: { type: 'object', properties: { total: { type: 'number' }, covered: { type: 'number' }, partial: { type: 'number' }, missing: { type: 'number' }, cut: { type: 'number' } },
    required: ['total', 'covered', 'partial', 'missing', 'cut'] },
  lint: { type: 'object', properties: { ran: { type: 'boolean' }, ok: { type: 'boolean' }, failures: { type: 'number' } }, required: ['ran', 'ok'] },
  readme: { type: 'string' }, files_changed: { type: 'array', items: { type: 'string' } }, summary: { type: 'string' } },
  required: ['verdict', 'blockers', 'wishes', 'lint', 'readme', 'summary'] }

const LENS_LIBRARY = {
  consistency: 'CROSS-FILE CONSISTENCY: contradictions between files in numbers, ids, names, terms, rules and scope; ids, systems or terms referenced but never defined; a glossary term used two ways.',
  fidelity: `FIDELITY TO THE OWNER: read ${OWNER} and list every distinct wish in it (keep the owner's wording). For each, find where the design honours it (file and section) and mark it covered, partial, missing, or cut (cut only with a written reason in the design). Report every partial or missing wish as an issue; a cut with no reason is a major issue.`,
  fun: 'FUN DIAGNOSIS: does any system measure loss where this genre measures gain (health as the main meter, shrinking power, clamps)? Is growth rare, hollow or invisible? Does failure pay nothing? Is time outside play over the budget in the design? Are rules or bureaucracy standing in for toys? Is a choice an expected-value spreadsheet instead of a temptation? Is the best moment locked behind a long session? Cite the design text for each finding.',
  'first-session': 'FIRST-SESSION SIMULATION: simulate minute by minute (a) a first-time solo player\'s first 20 minutes and (b) if the game is multiplayer, a group of friends\' first session. Where is it confusing, slow, unfair, boring or not funny? Where is progress not felt? Count the new systems introduced before the first big payoff and compare with the design\'s own budget. Every beat in the design\'s timeline must be reachable by the actions described.',
  progression: `PROGRESSION AND TUNING MATH: every progression, pacing or economy claim must have a tuning file and a simulation result (${TUNINGS.length ? TUNINGS.join(', ') : 'look under the design folder for *.tuning.json and sim outputs'}). ${TOOLS.sim ? `Re-run each tuning with: ${TOOLS.sim} --tuning <file> --out <its sim folder> --json, and compare the percentiles with the acceptance rows and the design text.` : 'No simulation command was provided: read the saved percentiles.json files and compare them with the design text.'} Flag closed-form averages used where the real content mix differs, windows with no check, and numbers in the text that disagree with the sim output.`,
  'rules-clarity': 'RULE CLARITY: every danger has a cue before it and a plain rule the first time; punishments are readable; every choice screen shows at most 4 facts and never a formula; the control bindings and the glossary stay within the design\'s own budgets; every number shown to a player is bound to real data (the game never lies).',
  fairness: 'SOCIAL FAIRNESS (party, co-op, competitive): every effect one player can have on another has a fixed ceiling stated as a constant; betrayal is announced, scoped to a round or room, moves only unbanked value and has a non-betrayal answer; strangers cannot grief strangers; the host has no latency edge; hidden information goes only to its owner; bots follow the same caps and never act on hidden information; catch-up keeps the last player in the game without erasing skill.',
  buildability: 'BUILDABILITY: act as the engineer implementing each system from the text alone. List ambiguities, missing numbers, impossible physics, soft-locks, network or hidden-information traps, performance traps, and content counts that do not fit the schedule.',
  'economy-ethics': 'ECONOMY AND ETHICS: exploits, dead currencies, grind walls, runaway snowballs, pay-to-win leaks, paid randomness, premium currency obscuring prices, countdowns and streaks that punish breaks, invented odds or rarity numbers, missing stopping points, and anything that both is earned and can be bought.',
  acceptance: `ACCEPTANCE ROWS: every design claim a gate will judge has an acceptance row with a number, an evidence type, who measures it, where, a tier and a due milestone; the feel-budget beats and the stop conditions have rows too.${TOOLS.lint ? ` Run: ${TOOLS.lint} <each design file that holds acceptance rows> --json, and report each failure as an issue.` : ''}`,
  critique: 'CRITIQUE FOLLOW-THROUGH: every item of every earlier critique, diagnosis or review log in the design folder or research notes is either fixed in the text or explicitly rejected with a one-line reason.',
}
const DEFAULT_LENSES = ['consistency', 'fidelity', 'fun', 'first-session', 'progression', 'rules-clarity', 'buildability', 'economy-ethics', 'acceptance']
const LENSES = (A.lenses && A.lenses.length ? A.lenses : DEFAULT_LENSES).map((l) => {
  const m = /^([a-z0-9-]+)\s*:\s*(.+)$/i.exec(l)
  if (m) return { key: m[1], lens: m[2] }
  return { key: l, lens: LENS_LIBRARY[l] }
})
const unknown = LENSES.filter((l) => !l.lens).map((l) => l.key)
if (unknown.length) return { status: 'incomplete', stage: 'args', reason: `unknown lenses ${unknown.join(', ')}; use ${Object.keys(LENS_LIBRARY).join(', ')} or "id: text"` }

const CONTEXT = [
  `Workspace: ${WS}. Every path is relative to it.`,
  `The owner's words, verbatim (the highest authority after the owner): ${OWNER}.`,
  RESEARCH.length ? `Research notes (claims carry Strong/Supported/Weak labels; never re-cite an item from a "do not re-cite" list): ${RESEARCH.join(', ')}.` : '',
  'Every timing and retention number in a design is a greybox hypothesis until a playtest measures it; say so rather than treating it as fact.',
].filter(Boolean).join('\n')

phase('Canary')
const canary = await agent('Reply with ok=true and nothing else. Do not use tools.', { label: 'canary', schema: { type: 'object', properties: { ok: { type: 'boolean' } }, required: ['ok'] }, effort: 'low' })
if (!canary) return stop('canary', { reason: 'canary died: usage limit or auth; launched nothing else' })

let docs = DOCS.slice()
let panel = null
if (A.new_design) {
  phase('Proposals')
  const ANGLES = A.angles && A.angles.length ? A.angles : [
    'player power and visible progression',
    'social play and moment-to-moment comedy or tension',
    'immersion, world and first-session pacing',
  ]
  const slug = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 40)
  const proposals = need(await parallel(ANGLES.map((angle) => () => run([
    `You write ONE design proposal for a game, from the angle: ${angle}.`,
    CONTEXT,
    `Read the owner's words and any research notes and existing design files (${DOCS.join(', ') || 'none yet'}).`,
    `Write ${DIR}/proposals/${slug(angle)}.md (create the folder): a one-sentence fantasy, at most 3 pillars, the core loop at every time scale (seconds, minutes, session), session length and player counts, the first 10 minutes shot by shot, the progression spine with numbers, what you would cut, and the 3 biggest risks. Numbers, not adjectives.`,
    'Return the summary fields; the file is the proposal.',
  ].join('\n'), { label: `proposal:${slug(angle)}`, phase: 'Proposals', schema: PROPOSAL }))), ANGLES.length, 'judges')
  if (!proposals) return stop('proposals')

  phase('Judges')
  const JUDGES = [
    'a player and streamer: fun per minute, the first 5 minutes, clip-worthy moments, whether you would come back tomorrow',
    'a producer and tech lead: scope for the team and schedule, buildability, risk, cost, what can be proven cheaply first',
    'a creative director: one clear fantasy, coherence of the pillars, fidelity to the owner\'s words, a hook that reads in one line',
  ]
  const judgements = need(await parallel(JUDGES.map((j, i) => () => run([
    `You are a judge: ${j}.`,
    CONTEXT,
    `Proposals: ${proposals.map((p) => `${p.file} (${p.angle})`).join('; ')}. Read every one in full.`,
    'Score each proposal 1-10 from your seat, pick ONE base, list the grafts worth taking from the others (with the proposal they come from) and what to cut. The owner\'s words outrank any proposal. Be decisive.',
  ].join('\n'), { label: `judge:${i + 1}`, phase: 'Judges', schema: JUDGEMENT }))), JUDGES.length, 'chair')
  if (!judgements) return stop('judges', { proposals })

  const chair = await run([
    'You are the CHAIR of the design panel. Merge the judges into ONE design: majority base; on a split, the stronger argument; the owner\'s words outrank everyone.',
    CONTEXT,
    `Judgements: ${JSON.stringify(judgements)}`,
    `Proposals: ${proposals.map((p) => p.file).join(', ')}.`,
    `Write or update ${DIR}/CONCEPT.md and ${DIR}/DESIGN.md (keep the section structure of any template already in those files). DESIGN.md must end with a review log: grafts taken (from which proposal), objections accepted (and where applied), objections rejected (one-line reason each).`,
    'Return the files you wrote and a 200-word summary.',
  ].join('\n'), { label: 'chair', phase: 'Judges', schema: CHAIR })
  if (!chair) return stop('chair', { proposals, judgements })
  panel = { proposals, judgements, chair }
  for (const f of chair.files) if (!docs.includes(f)) docs.push(f)
}

phase('Lenses')
const checkPrompt = (c) => [
  `You are a design verifier. Lens: ${c.lens}`,
  CONTEXT,
  `Design files: ${docs.join(', ')}. Read them in full before you judge; consult the research and tunings when a claim depends on them.`,
  `Report concrete issues only, with ids ${c.key}-1, ${c.key}-2, ...: the file, the exact location (section or table row), the problem, a concrete fix, and a severity (blocker: the game cannot be built or would not be fun as written; major: a gate or the owner would reject it; minor: polish). Default to skepticism, but report only what you can point to. Do not edit files.`,
].join('\n')
const refutePrompt = (c, issues) => [
  `You are an adversarial REFUTER. A verifier with this lens reported the issues below about the design files ${docs.join(', ')}. Lens: ${c.lens}`,
  CONTEXT,
  'For each issue, open the cited file and location and decide: real (the text really has the problem and it matters) or not (misread, handled elsewhere: cite where; or harmless). Set refuted=true only when you can cite the evidence. If it is real but the fix is poor, give a better_fix. Then add up to 3 new_issues the verifier missed through the same lens. Do not edit files.',
  `ISSUES:\n${JSON.stringify(issues)}`,
].join('\n')

const checked = await pipeline(LENSES,
  (_, c) => run(checkPrompt(c), { label: `check:${c.key}`, phase: 'Lenses', schema: ISSUES }),
  async (res, c) => {
    if (!res) return null
    if (!res.issues.length) return { lens: c.key, reported: 0, confirmed: [] }
    const v = await run(refutePrompt(c, res.issues), { label: `refute:${c.key}`, phase: 'Lenses', schema: REFUTE })
    if (!v) return null
    const confirmed = res.issues
      .filter((i) => !((v.verdicts.find((d) => d.id === i.id) || {}).refuted))
      .map((i) => { const d = v.verdicts.find((x) => x.id === i.id); return d && d.better_fix ? { ...i, fix: d.better_fix } : i })
      .concat(v.new_issues || [])
    return { lens: c.key, reported: res.issues.length, confirmed }
  })
const lensResults = need(checked, LENSES.length, 'revise')
if (!lensResults) return stop('lenses', { panel, lenses_done: checked.filter(Boolean).map((r) => r.lens), lenses_dead: LENSES.filter((_, i) => !checked[i]).map((l) => l.key) })

const confirmed = lensResults.flatMap((r) => r.confirmed)
const serious = confirmed.filter((i) => i.severity !== 'minor')
const minor = confirmed.filter((i) => i.severity === 'minor')
log(`${confirmed.length} confirmed issues (${serious.length} blocker or major, ${minor.length} minor) from ${LENSES.length} lenses`)

let revised = []
if (serious.length) {
  phase('Revise')
  const byFile = {}
  const loose = []
  for (const i of serious) {
    if (i.file && docs.includes(i.file)) (byFile[i.file] = byFile[i.file] || []).push(i)
    else loose.push(i)
  }
  const files = Object.keys(byFile)
  const out = await parallel(files.map((f) => () => run([
    `You are revising ${f} in place (Read, then Edit). Apply every issue below; an independent refuter confirmed each one.`,
    CONTEXT,
    `Stay consistent with the other design files (${docs.filter((d) => d !== f).join(', ') || 'none'}): read the sections you touch elsewhere. If a fix needs a change in ANOTHER file, do not edit it: list it in cross_file. Numbers, not adjectives. Keep the owner's words unchanged.`,
    `ISSUES:\n${JSON.stringify(byFile[f])}`,
  ].join('\n'), { label: `revise:${f.split('/').pop()}`, phase: 'Revise', schema: REVISED })))
  revised = need(out, files.length, 'gate')
  if (!revised) return stop('revise', { panel, confirmed, revised_done: out.filter(Boolean).map((r) => r.file) })
  if (loose.length) revised.push({ file: '(cross-file)', applied: [], cross_file: loose.map((i) => ({ file: i.file || '(unspecified)', change: `${i.id}: ${i.claim} FIX: ${i.fix}` })) })
}

phase('Gate')
const crossFile = revised.flatMap((r) => r.cross_file || [])
const notApplied = revised.flatMap((r) => (r.not_applied || []).map((n) => ({ file: r.file, ...n })))
const gate = await run([
  `You are the FINAL GATE for the game design in ${DIR}/ (files: ${docs.join(', ')}).`,
  CONTEXT,
  `1. Apply the cross-file changes the revisers could not make (you may edit any design file): ${JSON.stringify(crossFile)}`,
  `2. Issues a reviser did not apply, with its reason: ${JSON.stringify(notApplied)}. Resolve each, or keep it as a blocker.`,
  '3. Re-read the whole set end to end: contradictions between files, every owner wish covered or cut with a reason, every system specified with numbers, the first-session timeline, the feel budget, cut lines and stop conditions present.',
  TOOLS.lint ? `4. Run: ${TOOLS.lint} ${docs.join(' ')} --json. Fix every failure you can (make the pass condition a number with an evidence type), then run it again. Report the final result.` : '4. No acceptance-lint command was provided: set lint.ran=false and the verdict to BLOCKED.',
  `5. Write ${DIR}/README.md: a reading guide (which file answers what), the fantasy and pillars in at most 5 lines, a wish-coverage table (| wish, in the owner's words | status: covered, partial, missing or cut | where: file and section |), the acceptance-lint result, the open minor issues ${JSON.stringify(minor.map((i) => `${i.id} ${i.file || ''}: ${i.claim}`))}, and the open unknowns with a check and a fallback each. Do not write a design hash: the main loop adds it after a PASS.`,
  'Verdict: PASS only when no blocker or major issue remains, no wish is partial or missing (a cut needs a written reason), and the acceptance lint passes. Otherwise FAIL and list the blockers. BLOCKED only when a required input or command was missing.',
].join('\n'), { label: 'gate', phase: 'Gate', schema: GATE })
if (!gate) return stop('gate', { panel, confirmed })

return {
  status: 'complete',
  verdict: gate.verdict,
  gate,
  docs,
  panel: panel ? { proposals: panel.proposals.map((p) => p.file), chair: panel.chair.files } : null,
  lenses: lensResults.map((r) => ({ lens: r.lens, reported: r.reported, confirmed: r.confirmed.length })),
  confirmed: { serious: serious.length, minor: minor.length },
  revised: revised.map((r) => ({ file: r.file, applied: r.applied || [], not_applied: (r.not_applied || []).length })),
  breaker,
}

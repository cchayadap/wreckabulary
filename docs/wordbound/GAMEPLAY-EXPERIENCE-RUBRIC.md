# Gameplay experience rubric and first narrative slice

STATUS: SOURCE REVIEW COMPLETE — implementation delivered for root integration; native, visual and human acceptance pending, 2026-10-26. Owner: James / SethyPagna.

This applies the requested minimal, immersive direction to the existing Lost Address course. It extends [the story plan](STORY-AND-PLAYER-EXPERIENCE.md), not the authoritative match contract. The review inspected integration `9fce9f25`; implementation is in the `experience-refresh/story26` lane. No participant sessions, player survey, visual approval or new full-course completion are claimed here.

## What to judge

Judge whether the player can understand and act in the room, whether the house tells a coherent mystery, and whether another run offers worthwhile decisions. More text, more systems and more puzzle sites do not automatically raise the score. A score of 4 means the particular quality is demonstrated consistently; it is not a review score or a prediction of commercial reception.

| Dimension | 0 — broken | 1 — requires explanation | 2 — usable with friction | 3 — clear through play | 4 — expressive and dependable |
| --- | --- | --- | --- | --- | --- |
| Introduction | Cannot start or complete the first loop | Needs a developer to identify action, object or goal | Can follow explicit prompts; opening teaches too many things or stalls | First room teaches collect → spell → use → acknowledgement with one immediate goal | New and returning players start smoothly; assistance is available without interrupting fluency |
| Moment-to-moment UX | Action state is hidden or misleading | Repeated guesswork about target, recipe, reach or success | Actions are labelled but feedback competes or recovery is unclear | Target, missing requirement and result are readable at the point of action | Players combine tools confidently; mistakes teach and recover without menu hunting |
| Plot comprehension and complexity | Events contradict progress or reveal another team's story | Mystery depends on unexplained prose or optional clues | Beginning, discovery and repair exist but causal links require reading | Players can connect wrong labels → faulty sorter → repaired address through actions | Optional detail adds interpretation while a silent route remains causally complete |
| Immersion and information restraint | UI blocks play or breaks spatial truth | Floating explanation dominates the place | Place names and props support setting, with duplicate prompts or unearned reveals | Landmarks and mechanisms carry primary meaning; short cues appear only when relevant | Material, motion, sound and consequence work together; settings preserve understanding without excess text |
| Mechanical depth and application | Required interactions cannot be completed | Repeat the same response without a meaningful choice | Six families exist but transfer/choice is unclear | Familiar objects acquire understandable second uses; players choose preparation and execution | Multiple viable methods and team plans create mastery while difficulty remains readable and solo valid |
| Cooperation and public play | Another player can permanently block progress | One player acts while others wait or require voice | Shared objective works; roles/contribution often overlap | Silent players coordinate through visible progress and useful parallel preparation | Mixed skills contribute without carrying, griefing or unnecessary waiting across complete sessions |
| Pacing and replay | Route cannot complete under its rules | Long unexplained stalls or repetitive labour dominate | Sessions complete but repeated tasks and optional content crowd the timer | First and repeat runs fit the intended budget with room for mistakes | Route knowledge, seeded variation and expressive play improve replay without imposing grind |
| Accessibility and recovery | Required action/meaning is unavailable to a supported player | Tiny/timed text, colour-only meaning or input barriers | Some alternate cues and recoveries exist with gaps | No compulsory timed reading; controls, cue scale and recovery are usable across target settings | Disabled and mixed-ability participants independently confirm equivalent agency and comprehension |

Use **N/O** where the dimension was not observed. Record a per-person/per-run score after observing a concrete example; do not average missing values or collapse a blocking fairness/accessibility defect into a high total. A result is only as broad as the modes, seeds, devices and people tested.

## Current observations and confidence

The source estimates below are hypotheses about likely friction, not participant ratings. Confidence describes the inspected code fact, not whether players enjoy it. None can establish a 3 or 4 without observed play.

| Dimension | Source estimate | Confirmed evidence and implication | Confidence / missing evidence |
| --- | --- | --- | --- |
| Introduction | 2 | `WordboundFlagshipLayout.Objectives` starts with HAMMER/letters/TABLE; `WordboundPuzzleGuidePlan` points to the current checkpoint, source and socket. This supports a first loop, but immediate comprehension and control latency are unmeasured. | High source; low player inference. Observe a first uncoached session. |
| UX | 2 | Guide markers expose recipe and physical purpose. `WordboundStoryView` also repeats full clue prose in signs and an eight-second banner; equipment menu holds the same journal. Competing surfaces are a concrete subtraction opportunity. | High source; overlap/legibility in the actual camera is unmeasured. |
| Plot | 2 | Three narrative IDs, four destinations, twelve main clues and two branch clues exist. The second lap reverses the route; the final Packing repair branch is mandatory. Optional richer endings/restoration are only design. | High source. No unprompted player retelling; do not claim visible repair staging. |
| Immersion | 1–2 | Before this slice, all current-lap notices are instantiated regardless of the team's reached checkpoints. Their 20 m camera radius can expose future explanations. Landmarks are authored concepts; their readability in the current build was not observed here. | High code fact; low visual inference. Requires capture with original art. |
| Mechanical depth | 2 | Six families, changed recipes and three laps exist. Family order is repeated across four destinations, giving 72 sites; count does not prove depth. | High source. Compare choices and transfer on first/repeat human runs. |
| Cooperation | N/O | Journal progress is actor-team gated; the ordinary solo puzzle contract avoids compulsory simultaneous people. These are necessary foundations, not evidence everyone has meaningful work. | High contract; no social-play inference score. |
| Pacing/replay | N/O | 25 minutes / 72 sites is about 20.8 seconds per site before travel, mistakes and reading. The published 18–25-minute goal is a target. | High arithmetic; no measured human completion distribution. |
| Accessibility/recovery | N/O | Journal preserves reached clues and does not require timed reading. Story signs scale with camera distance. Core recovery and input policy exist elsewhere; these do not prove accessible experience. | High source; low/no evidence of low vision, language, motor or cognitive access. |

Evidence paths: [story plan](../../Assets/_Project/Scripts/Online/Presentation/WordboundStoryPlan.cs), [story view](../../Assets/_Project/Scripts/Online/Presentation/WordboundStoryView.cs), [guide plan](../../Assets/_Project/Scripts/Online/Presentation/WordboundPuzzleGuidePlan.cs), [flagship layout](../../Assets/_Project/Scripts/Rules/Wordbound/WordboundFlagshipLayout.cs), [journal menu](../../Assets/_Project/Scripts/Online/Client/WordboundMatchHudMenu.cs), [approved mode and recovery rules](PLAN.md). Source references point to integrated files; this lane's older checkout has a separately identified baseline synchronization.

## Perspectives that can disagree

These are test lenses, not fictional testimonials. Recruit real participants for the human rows and preserve disagreements instead of reporting a single invented consensus.

| Perspective | Concrete concern / useful behaviour | What to observe | Proposed passing target |
| --- | --- | --- | --- |
| First-time solo player | Learns a physical verb before managing several screens | Time from readiness to identifying target, first spell, placement, success acknowledgement; each request for help | At least 4/5 identify the next objective within 60 s and finish one real loop within 5 min without developer instruction |
| Expert / returning player | Wants immediate control and transferable mastery | Second use of known equipment, pauses caused by repeated exposition, choices across two seeds | 5/5 repeat participants can skip journal and finish the first loop; zero forced story pauses; record at least one distinct valid plan per supported family before claiming depth |
| Mixed-skill co-op | Newcomer should have a useful task while expert prepares | Useful actions by each player, idle time, duplicated crafting, carrier/scout handover | Each player identifies one specific contribution; no player spends more than 60 consecutive seconds idle solely because another has the only usable task in the first destination |
| Silent public pair | Has no voice, shared plan or assumed vocabulary | Whether source/receiver/progress show the same goal; failed handovers and unexplained waits | At least 4/5 unfamiliar pairs complete the first shared objective without voice/text or developer coaching; record each stuck interval over 30 s |
| Accessibility / low-text preference | Needs meaning without rapid English reading, sound or colour alone | Keyboard/controller, 1280×720 and 1920×1080, scaled UI, muted audio, long/CJK text, reduced-motion preference | Zero compulsory timed story text; full collected prose remains available; no essential cue relies solely on colour/sound; zero clipped required menu controls at supported settings |
| Speedrunner | Wants stable rules, no mandatory delays and no false shortcuts | Route/checkpoint identity across profile swaps, delayed/replayed snapshots, lap boundary cue | Identical authoritative state/hash/result with or without profile; zero new input captures or pauses; no previous-lap cue after advancing |
| Disruptive teammate / rival | Can waste time or withhold objects; must not reveal/erase progress | Opponent ahead, unreached future flags, optional route omitted, reconnect, lost/withheld keepsake | Zero rival/future clue disclosures; profile cannot grant progress/rewards. Run existing authority recovery/griefing tests separately; active withholding remains an open gameplay risk, not fixed by story text |

## How the story becomes gameplay

The causal chain should be visible at three distinct levels. The **required action** comes from the real puzzle/checkpoint state and its physical guide. The **short narrative cue** gives meaning to a place already reached. The **optional journal** supplies the full explanation without competing with aiming or movement. Narrative copy must never claim a repair succeeded before authoritative progress confirms it.

| Beat | Existing mechanics to retain | This slice | Later evidence-dependent work |
| --- | --- | --- | --- |
| First Packing arrival | Marked source, HAMMER → letters → TABLE, keepsake checkpoint | A brief reached cue such as “An address nobody wrote.” Full clue remains in journal. | An actual readable label/bell prop response; observe whether it introduces mystery without developer speech |
| Kitchen / Attic ascent | Socket, wind, timed mechanism, spring and weight families | Short mismatched-label/house-diagram clues tied to existing reached notices | Physical matching labels and repeatable object responses; prove a player links them to the task |
| Reverse route | Existing second-lap ordering and recipe variation | Broken-pointer explanation appears only after its own checkpoint, never because a rival reaches it | A changed mechanism/label comparison; require players to apply a familiar object differently |
| Final repair and ascent | Mandatory repair branch and rooftop delivery | Existing repair cue/full clue preserved under the same IDs | Visible restoration and result-aware ending need separate state/asset contract and pacing review |

Do not add optional chores to solve the 72-site pacing risk. Measure and simplify repeated labour first. A completely wordless version is a later art, sign-language and interaction test: deleting the existing puzzle instructions before equivalent physical cues are proven would conceal required actions.

## First implementable slice: presentation-only Story Profile

1. Add a Unity `WordboundStoryProfile` asset with title/chapter text, stable clue-ID rows, concise cue and full journal fallback, and optional localization keys. Canonical story IDs, chapter triggers, selected-route checkpoints and anchor positions remain defined by the course. No profile field may add a trigger, restoration task, ending condition or gameplay reward.
2. Preserve existing `WordboundStoryPlan(resolved, items)`, `StoryView.Bind(...)`, `Journal`, and notice constructor callers. Optional profile/localizer arguments override copy only. Validate against **all canonical clues**, so a profile can include an optional branch absent from one seed while unknown or duplicate IDs are rejected.
3. Use a short default cue for each canonical clue. Retain all canonical full clue text unless explicitly overridden. Localized IDs remain invariant; blank/missing translations fall back to authored text. No new localization package or claim of translated content.
4. Select at most the latest collected notice in the local team's current chapter. The full journal still accumulates all earned clues. Keep physical anchor/camera facing/distance bounds; remove the duplicate screen-space prose banner. No timed reading, input capture, audio dependency, cutscene, pause or new result panel.
5. Add an Inspector preview and validation using canonical content. Preview is a representation of copy, not a simulated earned checkpoint. Populate only an empty profile, support Undo, and create/update any real `.asset` through Unity. Root wires the profile to the HUD and owns native asset generation/testing.

Acceptance is source + native behaviour + human observation. Removing the banner alone cannot establish improved immersion; that remains a testable hypothesis.

## Measurable acceptance and evidence requests

All rows are proposed or pending unless the lane report cites an executed result. The 4/5 thresholds are small formative checks, not statistical population estimates.

| id | checkpoint | pass (measurable) | evidence | measured by | where | tier | due |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A-1 | Story identity | 14 canonical IDs remain unchanged; profile changes 0 lap/checkpoint/anchor/manifest values | test | root Unity tests | profile/plan tests | source/native | slice integration |
| A-2 | Information boundary | 0 unreached/rival clues; at most 1 current-chapter world cue; absent optional branch produces 0 signs | test | root Unity tests | story plan tests | native | slice integration |
| A-3 | Copy and localization | All 14 fallback cues are concise; duplicate/unknown IDs rejected; 0 empty translations suppress fallback; journal retains canonical prose without override | test | root Unity tests | profile tests | native | slice integration |
| A-4 | Authoring | 1 empty profile populates all chapters/clues; edited copy survives save/reload; preview awards 0 progress; population cannot overwrite authored rows | test | root editor fixture | Inspector/asset | native | slice integration |
| A-5 | On-screen restraint | 0 automatic story banners or mandatory pauses; latest cue visible near its earned anchor, hidden beyond 20 m; no old chapter cue after transition | capture | root capture and owner review | graphics player, 720p/1080p | visual | before presentation acceptance |
| A-6 | First loop | At least 4 of 5 novices identify target within 60 s and complete real loop within 5 min uncoached | human | consented observer | fresh ready session | human | playable slice |
| A-7 | Comprehension | At least 4 of 5 completed-run participants explain wrong labels, sorter error and final delivery purpose without leading questions | human | consented observer | end of full course | human | story acceptance |
| A-8 | Silent cooperation | At least 4 of 5 unfamiliar pairs complete first objective without voice/text or developer guidance; log every stall over 30 s | human | consented observer | public-style private test | human | co-op acceptance |
| A-9 | Pacing | Record complete first/repeat runs for at least 5 participants across 2 seeds; report completion, timeout, travel and task time separately against 18–25 min target | human | consented observer | full course | human | before adding restoration |
| A-10 | Access and recovery | 0 mandatory timed story text or colour-only requirements; inspect 2 resolutions, keyboard/controller and long/CJK fallback; verify 0 duplicate grants on reconnect | test, capture, human | root and recruited participants | native/player | mixed | before acceptance |

Record build/content identity, seed, mode/format/roster, participant prior experience, input, display/assists, readiness time, objective timestamps, hints, stalls, result and direct observations. Ask “What were you trying to do?” before offering help; ask for a retelling before explaining the mystery. Keep consented minimal identifiers. Report aborted/timeout runs and conflicting perspectives. Do not promote source confidence to a human score after automated tests pass.

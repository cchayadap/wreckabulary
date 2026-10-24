# Wordbound: The Lost Address — story and player experience

Owner: James / SethyPagna. Design expansion requested 2026-10-24. Source baseline inspected: `7f8ac615`. This document expands the [approved implementation plan](PLAN.md); [progress.md](../../progress.md) records delivery evidence.

**Goal:** make every run an understandable physical adventure: wreck furniture, recover letters, spell something useful, solve a problem, and bring a misplaced home back together. Players should remember a place, a funny recovery and something they accomplished together, as well as the result.

**Status:** the three-chapter course, mode rules, story notices and journal exist in source. The fuller staging, alternative epilogues, optional restoration objectives, feedback systems and narrative authoring controls below are **design work, not implemented features**. “Target” means something to test; player predictions are hypotheses, not research findings. No runtime or online rule changes are made by this document.

## 1. The story, written out

### Premise and opening

The roommates wake to the brass parcel bell ringing in the Packing Yard. The Movers are packing their home away, but the parcels make no sense: kitchen things are labelled for the attic, drawers arrive in the wrong rooms, and the rooftop windmill keeps calling an address nobody recognises.

The roommates find a keepsake that belongs at home. Its delivery label cannot tell them where home is. They set out with it, using the one skill this house still understands: break an object into letters and build a useful word from the pieces. A TABLE can become a bridge or a weight. A FAN can move something hands cannot reach. A CLOCK can make a frantic mechanism manageable.

The mystery is not who to defeat. It is why the house keeps sending its own belongings away. The Movers appear threatening because of what they do, but the investigation reveals that they are following bad instructions. Repairing those instructions is the answer.

This keeps the original game's plush roommates, wooden letters and playful household destruction. Detailed art should strengthen that identity through material finish, construction and readable movement. It should not turn the game into a grim, realistic combat setting. No named hero, villain, family tragedy or Mover biography is established canon here.

### Chapter one — Find the address

The first ascent is a journey from confusion to recognition. At the **Packing Yard**, players learn the full loop with a clearly marked object and a useful recipe. The giant parcel bell remains their landmark. The first clue establishes that the label is wrong; it does not demand that they read a paragraph before moving.

At **Kitchen Works**, the teapot chimney makes the new floor recognisable. Players use spelling and physical placement to get through sorting equipment. Recipe labels describe rooms upstairs. What initially looked like random packing becomes a pattern.

At **Attic Stacks**, the illuminated toy-house archive holds old labels and a diagram showing that these rooms once belonged together. Familiar equipment now has a second use. A player who learned to place furniture as a weight can begin to recognise where it might also provide access.

At the **Rooftop Garden**, the address windmill repeats the same missing destination. The team completes the first valid delivery, but the address system still sends the keepsake's route back through the house. Reaching the top answers “where do we go?” and creates the next question: “why is it sending everything back?”

Proposed short chapter transition: **“The address is wrong. Follow it back.”** Continue play immediately; the journal can hold the explanation.

### Chapter two — Follow the wrong delivery

Players travel back down, **Rooftop → Attic → Kitchen → Packing**. This reversed route is in the authored course. It should feel like an investigation through a now-familiar place, with changed approaches and equipment requirements, rather than a reset of the same tutorial.

The windmill sends the broken pointer's instructions downstairs. In the attic, the sorting diagram makes the mechanical fault understandable. Kitchen parcels bear attic labels. By the time players return to the yard, the Movers' behaviour makes sense: they obey the label even when it is obviously wrong.

The selected Packing route may include the Mislabel Room and Repair Bay. If that optional route is absent, the essential explanation must still be available through the main chapter clues. A random seed must never remove the clue required to understand the final repair.

The emotional turn is small and warm: the house is confused, not hostile. Smashing is useful because the roommates can rebuild from the pieces. The Movers should become understandable through labels, arrows and machinery; a new animated Mover sequence is proposed staging, not an existing encounter.

Proposed transition: **“We found the mistake. Now send the right address home.”**

### Chapter three — Put home back together

The final ascent begins with the **mandatory Packing repair branch**. The repaired pointer gives the journey a purpose beyond another lap. Kitchen labels return to their drawers. The archive diagram is put in order. At the rooftop, the team sets the address and delivers its keepsake to the sorter after completing the required route.

Proposed visual progression makes success visible without reading: corrected label pairs, steadier pointer movement and a warm response from the destination landmark. These are presentation tasks still to implement. They must not move occupied terrain, change collision unexpectedly or reveal a rival team's unreached chapter.

At the final delivery, the bell can finally ring for the right reason. The keepsake is home. How much else the roommates restored determines the proposed epilogue below; finishing the ordinary route is already a complete, positive ending.

### What tells the story

| Channel | Intended job | Current boundary |
| --- | --- | --- |
| Landmarks and physical route | Tell players where they are and make each return recognisable | Destination names and landmark concepts are authored; distinctive landmark artwork/staging still needs verification or implementation, followed by readability testing |
| Short notices | Deliver one useful clue at a reached place | Fourteen clue definitions exist; the selected route filters which appear |
| Collected-clue journal | Let curious players reread at their pace | Implemented and gated by the reader's own team progress |
| Changed labels and mechanism responses | Show the consequences of repair | Fuller restoration staging is proposed |
| Optional ending panel | Close the story after a confirmed result | Proposed; no branching ending resolver exists yet |

No mandatory competitive cutscene, timed reading test or spoken-English requirement. Skipping text must still leave the next gameplay objective clear. Existing notices have short banners; banner duration is not a deadline for understanding the plot.

## 2. How a session should run

### Before play

The player selects a mode, a race format where applicable, and Fill where applicable. Explain the actual rules in one compact card: objective, human player count, timer, damage and rival interaction. Show any undersized team before joining. A party can contain up to four people; a Freestyle session can contain up to eight people across parties.

Public cooperative play requires at least two humans. Private/local Time Run and Freestyle can support one. Race **Solo means 1v1**, not an AI opponent. Keep whole parties together, show cancellation/retry clearly, and never quietly substitute bots or an offline session. Match formation and loading are not part of the 25-minute gameplay timer.

Start only after the roster, content and initial state are ready. Once the roster is locked, no random replacement joins mid-run. Reconnection belongs to the original reserved seat, with the existing 120-second grace; choosing Return gives that seat up permanently. A returning host is not permitted to restart the story or award the result again.

### Proposed pacing budget for the flagship

These are design targets, not measured full-course completion times. The gameplay clock covers orientation and all optional tasks inside the course; do not silently pause it for reading.

| Phase | Target | What the player should understand or do |
| --- | --- | --- |
| Arrival and first complete crafting loop | 1–2 minutes | Identify the keepsake, collect required letters, craft and use one object |
| Rest of first ascent | 5–6 minutes | Recognise the four places and reach the first delivery |
| Reverse investigation | 5–6 minutes | Apply familiar tools, understand the wrong labels and return to Packing |
| Repair and final ascent | 6–8 minutes | Repair, coordinate and make the final delivery |
| Optional exploration/recovery margin | 1–3 minutes within the cap | Recover from mistakes or pursue restoration tasks |

The rows total **18–25 gameplay minutes**, matching the existing hard race cap at 25. The optional/recovery margin is included in that total, never added on top of it. Loading, queueing, recovery pauses and result viewing must be reported separately as well as in total wall-clock session time.

**Pacing risk:** the current flagship authors six puzzle sites per destination per lap: 72 sites across three laps. A 25-minute cap leaves about 20.8 seconds per site before any travel, reading or mistakes. The first full-course test must determine whether these are quick repeated interactions or an exhausting succession of puzzles. Do not call the course an 18–25-minute experience until humans complete it. If it is too dense, simplify repeated interactions or adjust authored requirements with explicit evidence and revised content identity; do not silently raise the timer or mark untested routes complete.

### The moment-to-moment loop

1. See a physical problem and the next required objective.
2. Locate a marked supply or decide which permitted object to break.
3. Understand the missing letters and spell the useful object.
4. Equip, deploy, position or activate it with clear feedback.
5. Observe the mechanism and checkpoint acknowledge success.
6. Carry or hand over the keepsake and continue.

Failure messages should explain the next action: “Move closer,” “This socket needs TABLE,” or “Finish the marked switch first.” These are proposed copy examples, not proof that every current error uses them. Do not make a player diagnose authority, snapshot or network terminology.

Roles are temporary activities, not classes: someone scouts, someone gathers, someone crafts or positions equipment, and someone carries. Any player can do each job. Required checkpoints and dependencies remain ordered; parallel preparation is legitimate, arbitrary sequence skipping is not. Only the carrier must cross the finish after valid team progress, so a slower teammate need not be dragged to the final line.

### Mode-specific expectations

| Mode | Current contract | Likely play and presentation |
| --- | --- | --- |
| Time Run | 1–4 cooperative players; three laps, 25 minutes, hazards, no enemy damage | Shared learning and efficient preparation; show team progress rather than naming the carrier as the sole hero |
| Team Race | Two teams; Solo/Duo/Squad capacities 1/2/4 each; three laps, 25 minutes | Clean competition through route knowledge and coordination; opponents cannot damage, body-block, steal or alter the other team's progression |
| Brawl Race | Same formats and delivery objective; enemy combat, protected progress | Decide when disruption is worth lost travel time; ordinary combat and object interaction must not erase solved progress or deny essential supplies |
| Free-for-All | 2–8 individuals; ten eligible knockouts or eight minutes; no team Fill | A playful equipment contest on its arena; it does not resolve the Lost Address plot |
| Freestyle | 1–8 in a shared session; no timer, player/hazard damage or winner; physical mechanisms and falls remain | Explore, practise a selected shared station, experiment and leave without a loss screen; no campaign completion promise |

Public matchmaking is **unranked**. Keep matchmaking availability separate from acceptance: a deployed mode entry is not evidence of a reliable completed match. AI companions, AI opponents, mixed couch/online play and a global skill ladder are outside this delivery.

## 3. Multiple endings and exact triggers

The story ending and the match outcome are different records. An epilogue must never change the winner, timer, reward eligibility or another team's clues. The following are the proposed implementation contract, not current runtime behaviour.

### Three principal Time Run endings

| Ending | Exact proposed condition | Scene and message |
| --- | --- | --- |
| **Home, at last** | Confirmed successful final delivery; fewer than all four optional restoration tasks completed | The address is restored, the keepsake settles into its place, and the bell rings correctly. “Home was here all along. We just had to put the words back.” |
| **A place for everything** | The same confirmed success plus one unique optional restoration task completed in each of the four destinations | Corrected parcels reach their shelves; the restored house becomes useful to everyone in it. “One address brought us home. Every small repair made room for the rest.” |
| **Delivery postponed** | Confirmed Time Run `TimeExpired` | The keepsake remains the story's focus while the journal recaps only places actually reached. “We found part of the way. The address can wait for another try.” Offer Retry, Practice or Return; do not imply the unfinished run is saved as a campaign checkpoint. |

The ordinary success is not a morally inferior ending. Deaths, falls, hint use, reading speed, accessibility settings and disconnects cannot select a worse ending. A team that ignores optional text can still understand and complete the plot.

### Optional restoration tasks — new content to implement

Use four small, clearly optional physical tasks on or immediately beside the valid route: return a spare parcel tag to its side shelf in Packing; reunite an additional recipe card with a secondary drawer in Kitchen; restore a separate archive display in the Attic; align a decorative garden address plaque at the Rooftop. These are bonus objects, separate from the main recipe labels, house diagram and address repair required by chapter three. Ordinary completion still restores the main system. These tasks extend existing motifs; visual production should reuse approved assets where appropriate.

Each task needs a stable unique ID, a visible destination, an independent supply/recovery rule and an authoritative completion condition. Total added time should fit the remaining 1–3-minute margin for an experienced cooperative group; that is a test target. Do not take mandatory puzzle letters, reserve the keepsake, require multiple humans, gate the main route or reward repeated activation. Journal entries are automatically discovered by progression and **must not** be mistaken for these new optional accomplishments.

Expose this restoration challenge in Time Run only initially. Keep it out of public race objectives and rating comparisons. Until its objects, snapshot fields, validation and tests are implemented, always use ordinary success rather than fabricating restored flags. Freestyle may later preview the tasks without earning a campaign ending or persistent rewards.

### Competitive and sandbox conclusions

| Actual outcome | Proposed presentation |
| --- | --- |
| Team/Brawl valid third-lap finish | Show the confirmed win/draw first. A team that completed its own delivery can receive the ordinary homecoming vignette |
| Team/Brawl timeout | Show the actual ordered-progress result, even when it is a win. Use “Furthest along when time ran out”; do not depict a repaired house if no final delivery occurred |
| Racing team loses before reaching the ending | Show its own last confirmed chapter and result without revealing the winner's unreached story clues |
| Same-tick race finishes | Show a draw and each qualifying team's completion; no client-arrival-order tie-break |
| FFA score target or timeout | Equipment-contest result with confirmed score and tie state; no address restoration, combat morality or campaign ending |
| Freestyle Return | Neutral session recap and return; no winner, timeout, abandonment grade or invented story completion |
| Disconnect, failed recovery or unconfirmed finish | Recovery/confirmation state, then a clear technical interruption if necessary; never a story failure, automatic loss or fabricated winner |

### Authority, priority and replay rules

Use the exact committed result and final team progress. A valid finish or knockout target on the limit tick takes priority over timeout. Same-tick target achievers can draw. Race timeout compares ordered objectives and then the earliest tick at that progress; FFA timeout compares knockouts. Time Run timeout has no winners. Disconnect or permanent Return does not currently create an automatic forfeit; do not imply otherwise in the UI.

For Time Run, resolve in this order: no confirmed result → no ending; confirmed timeout → Delivery postponed; confirmed final delivery plus all four validated optional IDs → A place for everything; otherwise confirmed final delivery → Home, at last. Invalid or missing ending data falls back to a plain confirmed result, never a richer ending.

New optional progress must live in the same durable, versioned authority state as the run, scoped to course identity and team. Pure presentation must not advance simulation after the result. Reconnect and host recovery must reproduce the same ending from the same committed state. Deduplicate any one-time unlock; no account unlock or persistent reward is part of this proposal. Dismissing the ending is local and cannot block another player's Return or require unanimous voting.

## 4. How players may actually behave

These predictions define what to observe. They are not claims that every member of a group acts this way.

| Player perspective | Anticipated behaviour or difficulty | Design response and what to check |
| --- | --- | --- |
| First-time player | Smashes everything, hoards letters, confuses held and placed equipment, misses the next dependency | Teach one complete loop; show missing letters and a placement preview. Can the player explain what to do without a developer? |
| Returning Wreckabulary player | Reuses known recipes and assumes familiar mode rules | Keep familiar object behaviour; disclose changed damage, ownership and delivery rules before launch |
| Speedrunner | Learns seeds, prepares supplies, tries jumps or sequence breaks | Reward route mastery; validate mandatory progress. A future personal best needs identical mode, content version, seed, format, actual roster and assist settings |
| Mixed-skill friends | One expert solves everything; others become passengers or choose to teach each other | Give useful preparation and carrying jobs without forced equal-action quotas. Ask every participant what they contributed |
| Silent public strangers | Duplicate crafts, contest pickups, misunderstand the selected station | Make shared objectives visible without voice. Proposed contextual pings: “Need A,” “Crafting TABLE,” “Ready to carry.” Pings are not yet implemented |
| Explorer | Reads signs, investigates branches and prefers discovery over speed | Journal, optional clues and Freestyle provide room to explore. No hidden reading requirement or negative score for pausing |
| Controller or accessibility user | Has difficulty with small text, precise holds, camera motion or typed names | Test remapping, focus, readable text, shape-plus-colour signals and camera controls. Hold-toggle, screen-reader support and full localization are not established features |
| Language learner | Recognises the object before its English spelling | Pair the word with its image and letter counts; keep prose concise. Translation must not silently change canonical recipes or online identity; make no learning-outcome claim without research |
| Solo player | Performs each job sequentially and cannot hold two switches | Every required puzzle has a one-human solution; test it explicitly. Race Solo still needs an opponent |
| Disruptive participant | Wastes resources, withholds the keepsake, camps a route or repeatedly switches practice stations | Test mechanical protection and recovery. An actively held keepsake is not an abandoned item; its anti-stall policy remains unresolved and must not be claimed fixed |

### Predictable failure and abuse cases

- **Resource exhaustion:** required supplies must recover without duplicating inventory or undoing completed objectives. Test crafted objects lost out of bounds and intentional waste, not just an empty initial inventory.
- **Keepsake placement:** startup, deliberate drop, forced drop, return and below-course recovery need separate clearance checks. The startup fix is source-tested; the other placement paths are still open work.
- **Active withholding:** define and test an objective reclaim mechanism before public acceptance. It must help a team recover from deliberate refusal without enabling teammates to repeatedly steal from a legitimate carrier. No kick vote or reclaim timer is silently introduced by this document.
- **Freestyle switching:** all players currently share the selected practice station. Repeated switching can spoil another player's attempt. A visible owner/request/cooldown policy needs a concrete decision and usability test; no such protection is claimed implemented.
- **Brawl camping:** test repeated hits, obstruction and recovery protection together. Preserve the approved one-second control-disable/two-second recovery-protection tuning targets; they are not proof that all combinations feel fair.
- **Menu confusion:** entering text or navigating the workshop must not craft, attack or move the player underneath. Return, pause/menu and release/drop need distinct labels matching the active bindings.
- **Technical stalls:** report measured frame/network interruption separately from puzzle difficulty. Never interpret a frozen client as a player choosing not to cooperate.

## 5. Randomness, replay value and fairness

Keep the four landmarks stable. Resolve route choices, prop placements, required supplies and mechanism variation before play; distribute the exact manifest, not a seed alone. Separate random streams so changing decoration does not silently alter puzzle requirements. Never regenerate terrain beneath an occupied player during a chapter transition.

Randomness should change decisions: which valid branch is used, where a permitted supply is found, which allowed furniture solution is required, and how an already-understood mechanism is approached. It should not produce unwinnable letter combinations, blind deaths or one team's easier mandatory route. Limit repeated solutions only after testing that the alternative remains readable and physically valid.

Competitive teams require equivalent mandatory opportunities and independent progress. Brawl permits ordinary disruption; Team Race does not. A rare decorative arrangement has no statistical advantage. Record the content version and resolved seed for every reproduced defect and any future time comparison.

Do not promise endless novelty from shuffled furniture. Replay interest should be tested across repeated runs: improved coordination, new legitimate solutions and recognisable but meaningfully changed routes. No daily streak penalties, random paid rewards or global ranking system is proposed.

## 6. Ratings: content, run feedback and map quality

### Audience and content classification

Design for accessible family play, with a tentative audience around age ten and above; **the game has no assigned age classification**. This is a design intention, not an ESRB/PEGI decision. Keep combat playful and nongraphic, and inspect actual weapon impact, sounds, fear, language and user-generated content before choosing a store submission target.

ESRB's E10+ category can include more cartoon/fantasy or mild violence than Everyone; its separate interactive elements describe online exposure such as user interaction. This does not establish that Wordbound qualifies. See the [official ESRB ratings guide](https://www.esrb.org/ratings-guide/).

PEGI distinguishes very mild/nonrealistic violence from stronger fantasy violence and nonrealistic violence against human-like characters. Stylized PvP therefore does not automatically establish a PEGI 7 outcome. The current guide also considers online communication safeguards. Review the final build and feature set rather than promising a label from the art style. See the [official PEGI label guide](https://pegi.info/what-do-the-labels-mean), consulted 2026-10-24.

If free-form player-to-player chat, voice or public uploaded-map discovery is introduced, scope the relevant reporting, mute/block and content-review work before launch. Existing editable names and private map titles/text already belong in the final online-exposure review; bounded JSON does not establish text moderation. Do not claim those safeguards exist today. Public Quick Play currently uses a curated built-in catalogue; private JSON map sharing is not a public user-content browser.

### Feedback about a run

Keep the confirmed outcome understandable: mode, result, progress or knockouts, and gameplay duration where relevant. Pending confirmation must stay visibly pending. Do not invent an expected store score or aggregate player rating before people have tested the game.

Proposed optional personal milestones include a first successful craft, a new destination and a completed puzzle family. A short contribution recap may acknowledge a useful placement or handoff if the event is reliably recorded. It must not rate the person's worth or make all credit belong to the carrier.

Do not score raw furniture destruction, attack counts, collected letters, distance, deaths, hints or disconnects. Those measures encourage farming or punish different roles. Do not subtract for accessibility settings or leaving an unhealthy session. Public teammate star ratings and a hidden skill ladder are outside the design. Difficulty labels should describe the course, not shame its players.

### Feedback about a map

Use a local playtest form first. A future map-feedback interface should separate **clarity, fairness, enjoyment and technical reliability**, tied to the exact version and seed played. A challenging map is not automatically unfair; a failed download is not evidence of poor puzzle design.

For human testing, use anchored questions rather than a single “good/bad” score: “I knew the next objective” from never to consistently; “I had something useful to do” from rarely to throughout; “mistakes had an understandable recovery” from unclear to clear. Collect one concrete example and optional improvement. Report small-sample counts and disagreements, not a fabricated prediction of review scores.

## 7. Editing the story and presentation in Unity

Ordinary design work should use assets, Inspector fields and scene handles. The existing camera/course/equipment controls are part of that direction; a complete no-code story editor is **new work**. Current flagship prose is defined in C# and is not yet a designer-editable narrative asset.

Proposed Story Profile fields: story title; chapter IDs and titles; destination links; checkpoint-triggered notices; text and optional icon; restoration-objective IDs; permitted ending conditions; ending panel text; approved presentation prefab; camera profile; and preview controls. Prefer stable references and validated dropdowns over free-form script names.

The designer workflow should be: select a course asset → open Story → select a chapter/place → edit text or drag a notice handle → choose a validated trigger → preview the selected ending → validate → playtest → return to the exact draft. Provide a compact table of all endings and a preview-state selector; do not require playing 25 minutes to check a line of text.

Presentation previews cannot award gameplay progress. Built-in competitive results are locked rules; a custom ending cannot change the public win condition. Narrative/optional-objective edits affecting gameplay create a new content identity and must survive exact online transfer and recovery. Private custom maps use approved asset IDs and bounded data, not arbitrary scripts or executable downloads.

Validation should identify missing/duplicate IDs, references to absent checkpoints, impossible optional objectives, unsupported mode/ending combinations, unreachable notices, missing fallback endings and stale content references. Include large-text, controller-focus and camera-safe-area previews. Playtest-return preserves the live draft and in-memory Undo/Redo. Save/reload preserves the document/settings; restarting or recovering begins new Undo history unless separate history persistence is implemented. The pending durable-draft recovery change still needs its native verification.

## 8. Acceptance: what would prove this works

These tests are proposed unless explicitly linked to existing evidence in progress.md. Scripted peers establish integration, not human understanding or enjoyment.

| Test | Passing target / evidence needed |
| --- | --- |
| First-time orientation, five participants | At least 4/5 identify the next objective within 60 seconds after readiness and complete one real crafting/use loop within five minutes without developer instruction |
| Story comprehension | After a completed run, at least 4/5 can explain the misplaced labels, broken sorter and purpose of final delivery without being led to the answer; reading every clue is not required |
| Mixed-skill and silent cooperation | Each person can identify a meaningful contribution; an unfamiliar pair completes the first shared objective without voice/text. Record duplicate work and stuck time |
| Pacing and repeated seeds | Measure full first and repeat runs, optional-task time and each destination. Confirm the 18–25-minute target or revise content from evidence |
| Solo validity | Complete every required puzzle with one player; no hidden simultaneous-player requirement |
| Ending truth table | Ordinary success, all four restoration IDs, partial restoration, timeout, exact-limit finish, same-tick draw, progress-only competitive win and no-result Freestyle all select the specified presentation |
| Ending authority/recovery | Replayed commands, duplicate result delivery and host loss before/after final commit yield one identical confirmed outcome and correct team-scoped ending; no rival spoilers |
| Failure recovery | Waste supplies, lose/drop/withhold the keepsake, fall, disconnect and switch practice stations. No permanent softlock, duplicate reward or erased completed checkpoint |
| Input and visual clarity | Keyboard/mouse and controller; resized UI, long text, camera obstruction, colour-independent signals and menu focus; original held/placed/used assets inspected in motion |
| Workshop onboarding | Existing target: at least 4/5 new testers author and play a simple randomized course in ten minutes, including validation, save/reload and exact return |
| Full multiplayer and performance | Real 2/4/8-client mode coverage, separate Windows clients, declared RTT/loss, full outcomes and abrupt recovery. Retain ten-second recovery/two-second rewind and 60 fps at 1080p Low targets; none are established by this document |

Record build/content identity, mode, format, actual roster and Fill, seed, device, assists, network conditions, confirmed result, failures and participant comments. Use consented test notes and minimal identifiers. Observe a first attempt without coaching, then ask what was confusing. Fix one major cause and retest; do not relabel a timeout as success because the participant enjoyed it.

## 9. Implementation order and outstanding work

1. **Stabilize the playable baseline.** Complete the repaired editor install, pending native tests, actual equipment interaction, official Windows smoke, public movement/recovery and full-mode outcomes. Story design does not bypass these blockers.
2. **Make existing content understandable and measure the route.** Inspect actual first-run guidance, signs, journal, next-objective feedback, landmarks and camera behaviour with original assets. Run an early human full-course pacing pilot before implementing optional restoration tasks; the current 72-site route must establish that it has room for them.
3. **Move story presentation into editable assets.** Preserve current chapter/notice identity and add validated Unity preview controls before increasing prose or adding effects.
4. **Add result-aware epilogues.** Implement ordinary success, timeout and correct competitive/sandbox conclusions from confirmed state; test the truth table and recovery.
5. **Add the optional restoration branch.** Version its authoring/state contract, implement four recoverable tasks, then validate the richer ending, pacing and solo accessibility. Keep it separate from race scoring.
6. **Close disruptive-play gaps.** Resolve active withholding and shared-station switching, then verify team isolation, fair recovery and no duplicated rewards.
7. **Run human sessions and finalize presentation.** Measure comprehension, contribution, pacing, accessibility, replay interest and creator usability. Finish visual/feel review and exact release-build verification.

Completion requires both implemented features and player evidence. This document is a written design deliverable, not a production-ready declaration or formal milestone closure.

## 10. Source and review notes

The canon and contracts were checked independently from narrative/art, player-experience and online-rule perspectives against integration `7f8ac615`. These were source/document reviews, not additional playtests.

- [Flagship destinations, clues and routes](../../Assets/_Project/Scripts/Rules/Wordbound/WordboundFlagshipLayout.cs)
- [Mode definitions](../../Assets/_Project/Scripts/Rules/Wordbound/ModeDefinition.cs) and [progress/result rules](../../Assets/_Project/Scripts/Rules/Wordbound/MatchProgress.cs)
- [Story plan and journal filtering](../../Assets/_Project/Scripts/Online/Presentation/WordboundStoryPlan.cs) and [current notice presentation](../../Assets/_Project/Scripts/Online/Presentation/WordboundStoryView.cs)
- [Authority and durable state](../../Assets/_Project/Scripts/Rules/Wordbound/AuthoritativeMatch.cs), [original-seat admission](../../Assets/_Project/Scripts/Rules/Wordbound/OnlineSessionGate.cs) and [shared practice policy](../../Assets/_Project/Scripts/Rules/Wordbound/MatchPracticeState.cs)
- [Existing delivery evidence and open tasks](../../progress.md)

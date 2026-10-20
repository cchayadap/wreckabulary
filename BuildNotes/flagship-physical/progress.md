# Authored physical puzzle runtime

STATUS: SOURCE VERIFIED — NATIVE PENDING

Base: puzzle-race `7511d65`, BALL integrated by root as `7d9d122`. This lane owns the existing Online/Puzzles plan/runtime/policy and additive helpers/tests. Root additionally authorized a narrow canonical mechanism evidence/validation/codec follow-up where physical traversal or transport needs a persistent latch. Session, GameplayController, Client, ModeDefinition and shared catalogue writes remain outside this lane. Native Unity execution remains root-owned.

Goal: execute all six approved families against exact authored sites and actual runner-local physical bodies: smash furniture and collect/spell letters; crafted socket placement; spring/traversal; wind-operated movement; object-operated floor switches; timed keepsake transport. Each family must remain Solo-solvable, team/lap isolated and recoverable. Checkpoint flags alone do not implement a puzzle.

Content owner: `/root/sethy_assets` supplies `WordboundFlagshipContent` and frozen site specifications. Its current source defines 34 rooms over four layers and 72 unique puzzle bindings, with author-facing lap numbers 1–3. Runtime uses canonical lap indices 0–2. These sources have not yet passed their independent content gate.

Confirmed constraints:

- Existing runtime hardcodes BedBridge/FanDoor sockets and UpA/UpB paths. A generic builder must validate every authored site against the exact resolved course and bind exact rule-book/effective-rule/item sources into admission.
- Resources placements currently materialize loose tiles. Real smash objectives therefore reference authored breakable Props placements; independent protected renewable supplies preserve solvability. Future-lap props exist from the start, so do not claim a fresh current-lap smash unless activation is actually enforced.
- Existing UsePuzzle requires a deployed recipe item in the same module and within three metres of the player. Long crossings need a host-verified latch or bounded trusted completion route.
- Existing mechanism state stores one deployed recipe reference and continuous timing. Persistent launch/arrival evidence needs bounded explicit state and validation.
- CLOCK currently affects movement; a physical sorting arm is not implemented by a timer or presentation alone.
- CourseDocument does not currently carry authored physical site profiles. A pinned separate plan can support a built-in course; workshop round-trip requires an explicit versioned persistence integration.

Contract proposal sent to root and content owner: site puzzle/lap/checkpoint/recovery IDs; explicit socket transform/tolerances/hold; arrival volume; smash Prop source zone; wind receiver source/start/arrival volumes; two floor-pad volumes; timed cycle/window. Proposed canonical evidence: bounded stage, actor, auxiliary body, stage tick and site hash. Observations must be generated from host physics, preserve the exact recipe reference, and call the host evaluator before consuming/solving.

Root subsequently authorized CourseDocument/codec/resolved/validation persistence, mechanism state/codec/authority, and only the platform branch of GameplayPlan.Presentation. No GameplayController or Session edits were needed.

Implemented source:

- `CoursePhysicalSite` and `CourseMovingFixture` persist sockets, sensors, source/recovery references, timing and actual machinery paths. Source and manifest schema/canonical version 2 apply to site-bearing courses. Empty schema1 serialization is preserved. Draft structural validation permits temporary missing links; playable resolution checks the full graph and geometry.
- Immutable resolved profiles carry canonical profile hashes. Generic `WordboundAuthoredPuzzleContent(mode,geometry,exactResolvedCourse,itemJson,ruleJson)` derives its plan from the persisted selected sites, retains the exact seed/manifest, and pins actual item/rule bytes plus effective Dibs-derived mode rules.
- Match mechanism state carries host-observed Waiting/Armed/Complete, actor, auxiliary body, stage tick, site hash and integer motion units. Causal/replay/identity checks and snapshot validation are independent of the runtime evaluator. Authored puzzle commands cannot complete from a recipe and permissive query alone.
- The six runtime predicates inspect actual source destruction, stable deployed socket geometry, spring impulses and arrival volumes, wind force applied to a free receiver, collider coverage of both floor pads, and keepsake transport through a real CLOCK-slowed arm. Arrival stages persist; current-lap destruction is deliberately not claimed for prebuilt future props.
- CLOCK arms use canonical protected team-owned Platform bodies. Existing reconciliation registers their physics normally. Their exact pose is derived from authored paths and persisted motion phase; pose and phase advance atomically. Only GameplayPlan's platform dimension lookup changed.

Verification completed after the compiler hold:

- First focused real-NUnit run: 104 passed, 16 new fixture setup failures. The failures exposed test fixtures lacking a physical traversal arrival and a CLOCK geometry-catalogue entry. Fixtures now use an explicit PlaceObject profile for state-machine tests and actual CLOCK dimensions for machinery.
- Corrected `WordboundPhysicalEvidenceTests`: **16/16 PASS**, `TestResults/runtime-puzzles/physical-evidence.trx`.
- Final `WordboundAuthoredPuzzleContentTests`: **14/14 PASS**, `TestResults/runtime-puzzles/physical-content-final.trx`. The initial 104 passing cases also cover existing mechanism timing, snapshot codec/validation, deployment expiry and physical proof policy. Combined final coverage is 120 distinct passing focused cases.
- Fresh lightweight Rules build: **PASS, 0 warnings/errors**. Fresh Fusion bridge reference build: **PASS, 0 warnings/errors**.
- Installed Unity 6000.6.3f1/Fusion SDK reference compilation of Physics, Gameplay and Puzzles: **PASS, 0 warnings/errors**. This gate found and corrected two new guards calling an internal Rules helper across assembly boundaries; they now use public finite-float checks.
- No Unity process was launched by this lane. Native six-family contacts, presentation, full-course traversal and multiplayer recovery are **NOT RUN** here. These source gates do not establish playable acceptance.

Compiler slot released to the content owner. Its flagship focused/sweep/body-budget audit should link the frozen Rules in this checkpoint so actual CLOCK fixture bodies and mechanism payloads are counted. The previous 512-body gameplay reconciliation budget remains unchanged; root owns capacity/performance decisions.

Content owner independently reported **24/24 focused PASS** against these frozen Rules, including canonical site/fixture persistence and actual AuthoritativeMatch startup. Measured startup bodies: TimeRun108, TeamRace Squad222, BrawlRace Squad165, Freestyle102. Its three-lap refill scenario estimates 668/1342/1285 for the race modes respectively, exceeding512. These are the content owner's source measurements/estimates, not native performance or full-playthrough evidence. Root has assigned a separate lifecycle cleanup follow-up; this checkpoint does not change the limit.

Next recoverable action: root integrates the source checkpoint, generates metadata through Unity, runs the two focused test classes natively and exercises all six physical families with actual runner-local bodies. Content owner completes source/manifest roundtrip, seed sweeps and initial/later body-budget measurements. Preserve existing online diagnostics and offline-session lifecycle changes during cherry-pick; the only Gameplay change here is the platform presentation dimension branch.

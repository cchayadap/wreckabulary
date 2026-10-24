# Freestyle spawn correction review

STATUS: READ-ONLY REVIEW COMPLETE — Online audit `dc770218` corroborated, with additional drop/below-course paths and bounded implementation choices. No production edits or execution.

Root requested a minimal robust correction proposal after the equipment run exposed an airborne original avatar. The exact initial player/keepsake overlap is confirmed in source; the cause of the photographed displacement remains pending the same-sample Windows diagnostics in `6951e5bf`. Original artwork, collection range, QA letter coordinates and player collision shapes should remain unchanged.

Current production source cross-checked in integration at `a57d5a53a57cfdb9331ff2c65384c6b33f746aeb`. Relevant locations: `Rules/Wordbound/AuthoritativePractice.cs:15,43`, `AuthoritativeMatch.cs:459,836,855,1038`, `MatchProgress.cs:206,469`, `MatchStateValidation.cs:410`, `CourseGeometry.cs:100`, `CourseResolver.cs:154`, `WordboundRuntime/WordboundCourseWorld.cs:121`, and `Online/Gameplay/WordboundGameplayController.cs:115`.

## Confirmed scope and additional paths

Online's audit is accurate about constructor ordering, anchor limitations, floor openings and exact snapshot restoration. `InitializePractice` runs before map props/resources/fixtures are added, so a constructor check of only the current `bodies` dictionary cannot establish static clearance. `CourseGeometry.ValidateLocation` requires the authored module floor Y and an X/Z margin; runtime `BuildFloor` can still subtract a stair/ramp opening there.

Two additional source paths matter:

- `AuthoritativeMatch.Apply(DropKeepsake)` sets the active loose body to the carrier's exact `origin.Position`. This can recreate the same overlap in both Freestyle and race modes. It is not solved by changing the constructor. A full keepsake-placement correction must either cover this explicitly or leave it recorded as a separate defect; do not quietly broaden a Freestyle patch into all race handling.
- `Synchronize` sends any active nonplayer/nonplatform body below the course to `course.RecoveryAnchors[0]` when it lacks a source. That includes a free keepsake and bypasses `PracticeRecoveryPosition`. It also assumes a nonempty anchor collection. A Freestyle recovery fix must route this case through the same safe selection rather than retain a hidden first-anchor fallback.

The ordinary generation recovery path is not a free-form retry point. `MatchProgress.AdvanceCore` clears the due timer and increments the team's generation before `UpdateKeepsakes`. Snapshot validation requires body/team generations to match and rejects a recovery timestamp at or before the current tick. Returning the old generation, throwing every time a candidate is occupied, or leaving an expired timer would violate existing behavior or validity.

## Smallest implementation boundary to select

The recommended first geometric implementation is an internal, deterministic **authored-anchor selector**, rather than a general procedural placement/search framework. Prefer eligible anchors in the starting module for initial/no-site placement, and the selected site's module for selected recovery; then use the same validated starting-module fallback. Use ordinal IDs for ties, no RNG, no input/physics mutation. Choose only at creation, drop/recovery events, not during ordinary ticks or restore.

For each candidate, require the whole upright keepsake volume (with a documented contact clearance) on supported floor, within the module, outside declared start/respawn capsule volumes, resolved static prop bounds, supplies and moving-fixture envelopes. Account for actual floor cutouts; rejecting an intersected opening conservatively is sufficient for a bounded first implementation and must be labelled as such. Preserve vertical separation so objects on another floor do not reject a candidate. Do not treat authored Path/Recovery reservations as physical solids: valid flagship paths and anchors overlap intentionally. Derive or parity-test shape dimensions against the actual `.6 × 1.8` capsule and `.45` keepsake box, rather than introducing unrelated magic dimensions.

At runtime, also reject overlap with relevant active canonical actors/objects, excluding the keepsake itself. Static candidate construction can be cached for this immutable course, while current occupancy must be evaluated at the actual event. Declared starts remain reserved even for seats not currently occupied. This keeps a later arriving player from spawning inside the initial keepsake.

For a new match with no supported safe authored candidate, return an actionable preflight failure before spawning native bodies. Preserve the old course/draft for editing. No anchor search, silent offset or document rewrite is needed for this bounded option. It deliberately does not promise that every previously accepted anchorless/overlapping map can launch unchanged. If root requires that compatibility, a separate bounded search with identical geometry predicates is needed; do not add it speculatively.

## Blocked recovery needs one explicit policy decision

An occupied preferred anchor should try another already validated candidate first. If **all** candidates are occupied, a robust correction cannot simultaneously promise immediate recovery, no overlap and no behavior change. The recommended extension is to postpone the recovery transaction until a candidate is clear, keep the same keepsake/generation, and retry with a future timer; do not consume the generation before placement succeeds. This requires a narrow progress/authority coordination point before `RecoverKeepsake`, and a testable pending-recovery policy. A timestamp of `current tick + 1` can use the existing serialized timer and satisfies current validation, but would be a deliberate blocked-recovery behavior change, not a purely geometric two-line fix. The normal unblocked 600-tick deadline stays exact.

The below-course path and unavailable drop also need explicit treatment: an unsupported falling keepsake cannot be left to fall indefinitely while waiting, and a requested drop can safely return `BlockedPlacement` while retaining its carrier, as ordinary item releases already do. How an already out-of-world keepsake is held pending recovery must be selected and validated before implementation; do not synthesize an inactive state merely because current loose-body validation happens to permit it. This report does not authorize that state-machine extension. If root chooses an initial-only correction first, mark recovery/drop acceptance incomplete rather than claiming the whole defect fixed.

## Restore and compatibility

`RestoreState` replaces the exact saved body poses and practice state. `RestoreCommitted` verifies the durable identity/hash and next epoch, then restores; it does not call the placement selector. Preserve this behavior, including an old snapshot's historical overlap. Do not add a universal snapshot nonoverlap invariant: native contacts, carried relationships and historical poses are legitimate imported evidence.

`WordboundGameplayController.Initialize` constructs a new `AuthoritativeMatch` before restoring the committed state. A constructor-only new-course preflight would therefore also reject some old course restores before their valid bodies are read. A robust implementation must distinguish new initialization from restoration without bypassing existing binding/content/snapshot validation; a narrowly scoped restore factory or deferred new-match preflight is preferable to catching and ignoring the constructor failure.

No snapshot field, codec/canonical version or protocol version change is inherently required for selecting a different valid free-body position. Do not bump those versions as part of the geometric repair. My earlier equipment report recommended a practice-policy hash change conservatively; this review narrows that recommendation: a positional bug fix does not by itself prove a hash/version migration is necessary. An explicit new pending-recovery semantic or stronger content-eligibility policy needs a deliberate release/mixed-build decision. `WordboundPracticePolicy.Hash` feeds Freestyle's mode identity (and authored Explore plan identity), so changing it has real admission/content consequences and should not be automatic. Exact saved snapshot hash/pose restoration remains required either way.

## Focused acceptance cases

| Case | Required evidence |
| --- | --- |
| Existing equipment course, default template and flagship | Stable selected point; no player/start overlap; source and resolved manifest bytes untouched; same body identity/ownership/count. |
| Anchor coincides with any of eight starts, boundary, rotated prop, supply or swept fixture | Candidate rejected; another valid authored candidate selected deterministically; no arbitrary fallback. |
| Upper-floor stair opening and another-floor object | Unsupported candidate rejected; unrelated vertical object does not falsely block valid floor. |
| Missing/all invalid anchors | Clear new-match preflight failure; draft remains editable; no native match started. |
| No selected site, selected site, selected module without usable anchor | Same safe selector and documented home fallback; no path reaches `Spawn(0)` or first-anchor-only recovery. |
| Due recovery with preferred/all candidates occupied | Exact decision chosen above; no generation consumed before placement, no expired invalid timer, no repeated rollback/fault; unblocked recovery still at the existing deadline. |
| Drop and below-course escape paths | No relocation into the carrier/another actor; blocked drop retains ownership; explicit valid pending/out-of-world behavior if included. |
| Rollback, restore and migration | Exact saved poses/hash retained, including displaced/overlapping legacy state; fresh-constructor preflight cannot silently break restore. |
| Actual prefab native and Windows | Same-sample canonical/Rigidbody/visual facts, owned collider geometry, startup/recovery contact and support; real unchanged-range letter collection; direct original world/held image review. |

These are proposed focused cases, not executed coverage. Existing `WordboundPracticeTests.SharedKeepsakeTakeDropAndRecoveryRemainCanonicalWithoutRace` only checks carrier/timer bookkeeping; it does not establish geometric separation. No new broad test framework, runtime patch or version change is present in this report.

Online independently confirmed no overlooked safe-placement/pending helper: native `TryRelease` cannot be reused directly by constructor/loose-keepsake recovery. It agreed that all-occupied recovery is an explicit atomic-policy choice. Next recoverable action: read the actual unchanged-course startup diagnostic, then root selects initial-only versus complete placement scope and the blocked-recovery behavior before a scoped patch. Preserve the failed PNG/receipts and exact old snapshots as regression evidence. `git diff --check` passes; no Unity, compiler, player, Blender or other heavy job ran in this lane.

# Keepsake release and recovery clearance

STATUS: COMPLETE — read-only proposal against integration `0ff2996d`; no runtime edits or jobs.

The initial-only fix does not correct subsequent release/recovery. This review
traces the existing transitions and proposes one bounded transaction contract;
root decides the runtime scope and owns verification.

## Confirmed paths and the current coverage limit

| Path | Current behavior | Concrete consequence |
|---|---|---|
| Explicit DropKeepsake | Progress releases carrier, then body.At(origin.Position, active:true, kinematic:false). | Same-root capsule/cube overlap is directly recreated; no clearance query. |
| Disconnect / Return / Fell / knockout | MatchProgress.DropKeepsake clears carrier; UpdateKeepsakes reactivates the last carried body pose. Disconnect does this outside Advance. | An implicit release has the same unchecked placement. Return may also deposit inventory at its recovery point. |
| Due recovery | MatchProgress.AdvanceCore recovers after player respawns but before observations; it clears the timer and increments generation. Synchronize then chooses the pose. | Placement cannot be refused after the generation change without rolling back the entire tick. |
| Freestyle recovery | PracticeRecoveryPosition uses first selected-module anchor, or Spawn(0) when no selected site/anchor. | The no-site path recreates player overlap; an authored anchor may be occupied or over a native cutout. |
| Below-course loose keepsake | Generic non-player loop uses RecoveryAnchors[0]. | Wrong module/team destination, no clearance, and an empty-anchor legacy course can throw after valid durable restore. |
| Race recovery | Landmark returns the same safe checkpoint point used by recovering players. | A keepsake can share a future player respawn pose even when no player occupies it at selection time. |

These are source-proven conditions, not new executed native failures. The prior
Windows lift's magnitude/cause remains separately unproven. Existing
WordboundPracticeTests.SharedKeepsakeTakeDropAndRecoveryRemainCanonicalWithoutRace,
WordboundReturnTests, ProgressTests.LostKeepsakeReturnsOnceToLatestSafeCheckpoint,
PickingUpDroppedKeepsakeCancelsItsPendingRecovery and lifecycle/migration cases
check ownership, deadlines, conservation and exact-once behavior, not actual space.

## Smallest coherent runtime contract

1. **Voluntary drop is local and atomic.** Resolve a supported clear position before
   clearing the carrier. Reuse the existing small release candidate pattern derived
   from actual capsule/box dimensions, with a swept path and full-footprint support.
   If none passes, return BlockedPlacement and retain carrier/body/timer. No teleport
   to a distant checkpoint or arbitrary offset. Existing TryRelease cannot simply
   be called: it requires Item plus a held economy entry, assumes item orientation,
   and does not itself require floor support.

2. **Forced loss cannot prevent leaving or kill the match.** On Disconnect, Return,
   Fell or knockout, park a just-released keepsake inactive+kinematic at its existing
   bounded pose with zero motion. Retain the existing 600-tick recovery deadline,
   identity and generation. This deliberately gives up immediate pickup after a
   forced loss; normal local Drop remains immediately pickable. It avoids introducing
   a fallible geometry callback into nontransactional Disconnect. UpdateKeepsakes
   must preserve this parked state rather than reactivate it each tick. Do not use
   the private initial-placement flag: that would freeze publication/Advance.

3. **Recovery commits progress and body together.** Add an internal authority-only
   deferred-recovery mode to MatchProgress's existing advance path; keep its public
   standalone behavior unchanged. In the authority tick, leave due recovery pending
   transiently, process player respawns, observations, inventory drops, resource
   retirement/minting and other body updates, then resolve keepsake recovery at the
   end of Synchronize before publication/full validation. A successful selection
   clears the timer and increments progress/body generation once. No candidate
   parks the body and sets the existing timer to Tick+1 with unchanged generation.
   This is legal under the current Restore bounds (future and <=Tick+600). No
   intermediate due-timer state may escape the existing inTick/rollback boundary.
   Every team gets resolved or deferred before the completed tick can be captured.

4. **Below-course keepsakes use that same recovery path.** Exclude them from the
   generic RecoveryAnchors[0] fallback. Preserve an already pending deadline; an
   otherwise loose lost body schedules a next-tick recovery attempt. Do not reset
   a pending timer every tick. Other item/tile fallback behavior is outside this
   change and remains a separately visible limitation.

5. **Candidate locality remains mode-aware.** Race: latest safe checkpoint/module;
   Freestyle: currently selected site module, otherwise starting module. Try the
   relevant authored anchor first, then ordinal eligible anchors in that module.
   For occupied centres, a fixed small set of neighbouring positions derived from
   keepsake/capsule extents is needed: an anchor-only retry can remain blocked by a
   respawn at that exact point. For anchorless legacy restores, use the matching
   existing Start/Landmark neighbourhood as the final bounded candidate source.
   No whole-world grid or cross-floor teleport. Reserve actual future player respawn
   positions as well as current players; reconnectable disconnected seats retain
   their old-pose reservation, while permanently returned seats are not blockers.

## Fresh native evidence, without another floor model

Use one narrow optional keepsake-placement query beside IWordboundReleaseQuery.
Its production implementation belongs in WordboundPhysicsWorld and uses the actual
registered box/capsules, CourseWorld surfaces, bounded overlap/sweep buffers and
floor rays. The query is read-only and fails closed on saturated buffers or absent
supported geometry. Do not copy native floor cutout construction into recovery code.
Existing Supported currently permits non-player item support; keepsake recovery
should require an authored static floor/connector, not a movable pile.

The query must see the **current in-tick canonical obstacle set**, not merely the
previous physics frame. New inventory drops/respawns can appear during Synchronize.
A bounded frozen body list is sufficient to check ownership/activity and actual
registered collider shapes at the current canonical poses; no snapshot export or
full validation is needed for a read-only query. If a newly active body is not yet
registered, or its required shape is not current, refuse/defer this placement until
normal reconciliation supplies it. This avoids guessing the missing shape or
temporarily moving physics objects. Explicit drops also require a clear swept local
path; recovery teleports only to the bounded mode-appropriate candidates.

All candidates occupied cannot be solved honestly by a collision-free algorithm.
The contract guarantees that returns/disconnects and the rest of the simulation
continue, and that clearing a candidate permits recovery on a later tick; it cannot
promise a safe pose in a completely obstructed room. Keep the pending state visible
(existing HUD currently has no KeepsakeRecoveryAt display) with an actionable
"Keepsake recovering — clear space near the checkpoint" status. This is a small
read-only HUD addition, not a modal or a relaxed gameplay gate.

## Restore, compatibility and bounded acceptance

RestoreCommitted/Import restore exact saved body poses, carrier/timer and generations;
never relocate during restore. Ordinary next-tick handling may recover a saved lost
body under the same rules as a live one. Pending state uses existing serialized
fields (inactive+kinematic body, null carrier, future timer); no new codec field is
necessary. An old authoritative implementation would reactivate that combination,
so mixed-version recovery behavior is **not** equivalent. Root must choose the
build/authority compatibility fence before shipping this gameplay change; do not
silently claim unchanged behavior merely because the wire schema remains readable.

Proposed edit boundary: AuthoritativeMatch/AuthoritativePractice placement and
transition helpers, MatchProgress internal deferred recovery/finalization, the
existing PhysicsWorld query seam, existing practice/return/progress/native physics
fixtures, and one existing HUD pending-state line if approved. No SDK, cadence,
checkpoint guard, player dimensions, artwork or service changes.

Meaningful acceptance: blocked voluntary drop changes no ownership/timer; forced
Return/Disconnect succeeds with every candidate blocked and preserves conservation;
600-tick recovery and pickup cancellation remain exact; all-busy retries preserve
generation and importability, clearing one spot increments once; simultaneous teams
cannot choose the same colliding pose; player respawn/inventory drops in the same
tick are obstacles; query exceptions roll back the existing authority tick; stored
pending/legacy snapshots restore exactly before later recovery; no-anchor restored
course below-floor handling never indexes an empty list. Native two-peer cases must
show a teammate can recover after the carrier leaves, actual supported placement,
no penetration and normal progress/ACK. Do not count current state-only return tests
as native clearance proof.

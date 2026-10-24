# Freestyle keepsake placement implementation plan

STATUS: IN PROGRESS — INITIAL-only implementation authorized; no jobs.

## Approved first patch

Baseline `f60ef8dc` is aligned in excluded baseline-only commit `1ae138bf`.
Owned source is `AuthoritativePractice.cs` and the initialization/Advance/Publish/
successful RestoreCommitted boundaries in `AuthoritativeMatch.cs`; focused
existing practice/authority and OfflineGameplay native tests only.

After the constructor has composed and synchronized all bodies, choose the first
ordinal-ID recovery anchor with no prerequisite in the same module as the first
Start used by Spawn(0). Require its entire 450 mm cube, plus 20 mm contact clearance,
on the module floor, inside the wall interior, clear of declared Start/Respawn
capsules, actual resolved prop bounds (quarter-turn yaw), all resource/supply tile
positions, gated connection platforms and complete moving-fixture swept bounds.
Conservatively reject stair/ramp projected envelopes on floors affected by native
floor cutouts and connector solids. Other-layer obstacles only exclude when their
vertical volumes intersect. Selection uses bounded authored data, no world-query
callbacks, grid search, new content, or engine dependency.

No eligible anchor retains the initial bounded keepsake pose as inactive/kinematic
and sets a private unresolved flag. Capture and full constructor validation remain;
Publish and direct Advance reject before mutation. Only the successful end of exact
fully validated RestoreCommitted clears that flag. General RestoreState, failed
restore and rollback cannot clear it. Saved valid legacy poses are restored exactly.

Tests will cover determinism, blockers and layers, no-anchor refusal, retained
snapshots and successful/failed durable restore, plus one actual native floor/
collider support and clearance check. No compiler or native execution in this lane.

## Deferred whole-scope correction

Recovery, voluntary/implicit drop and below-course fallback remain unchanged and
open. The later transaction needs a separate decision for all-occupied recovery,
generation/timer atomicity and local drop refusal. This first patch adds no pending
recovery state and changes no protocol, codec, mode/practice hash, artwork or native
collider dimensions. A new initial pose is deterministic but differs from old new-
match construction under the existing contract; no mixed-version equivalence is
claimed. Restore compatibility is tested explicitly.

Root requested one bounded correction covering initial placement, selected/no-site
recovery, voluntary drop and below-course recovery, while preserving exact legacy
snapshot restoration. This extends the earlier `FREESTYLE-KEEPSAKE-SPAWN-AUDIT.md`
scope. Initial source overlap is established; actual avatar lift magnitude and
its causal share remain unproven pending unchanged native diagnostics.

Current constraints: no arbitrary offset, player teleport, ignored collision pair,
new general placement framework or unbounded search; authored anchor candidates
come first. Existing wire fields and construction/Restore route are preferred.
Root owns policy choice, implementation authorization and all verification.

# Resource retirement scan proposal

STATUS: IMPLEMENTATION IN PROGRESS. Root approved the bounded private per-call index after the separate count, collision and membership checkpoints. Root owns every compiler, core, Unity/native, player and measurement run. Existing dirty ModeDefinition and historical evidence are untouched. The original design/evidence below is retained; current implementation and regression findings are appended as established.

## Source and measured boundary

`AuthoritativeMatch.Synchronize` invokes `RetireInactiveResources` once after updating team/puzzle completion and before recovery/minting of sources. `AuthoritativeLifecycle.cs` currently captures current teams, iterates a frozen resource array, calls the live lifecycle Demand predicate, and for each inactive resource scans every current body for matching Tile/SourceId/OwnerTeam/RecoveryGeneration. It then retires each matching economy tile and removes its world body, finally clearing a scheduled recovery deadline when present.

This repeats the full body scan even when no tile matches. For R inactive resources and B bodies, candidate discovery is proportional to R×B plus actual removals. The same-call dynamic keys are exact source string, team and generation. Lap is represented by the resource generation invariant, but the proposed index must not assume different resources cannot alias a key when preserving sequential removal behavior.

The retained181114Z Team baseline has Advance360calls/7187.259ms inclusive. It also contains command processing, rollback Capture, synchronization, mechanisms, capacity/budget and other work. There is no isolated timing for retirement. Thus no millisecond, fps or deadline benefit is established for this candidate. Do not combine it with the already integrated tick/presence change before measuring that change.

## Smallest candidate after approval

Keep current team capture, frozen resource iteration, Demand call, deadline clearing and all retirement checks in their current order. At the first inactive resource only, build a private per-call dictionary from exact `(SourceId, OwnerTeam, RecoveryGeneration)` to an ordered list of Tile bodies. Include inactive tiles, unprotected tiles and all matching ownership/generation states; the old predicate does not filter Active or Protected. Default string equality for tuple keys must remain equivalent to the old ordinal string equality.

Look up the resource key instead of scanning all bodies. Consume/remove its bucket once, retaining the original body's encounter order and `RetireWordboundTile` followed by `bodies.Remove`. This prevents a later alias resource from attempting to retire an already removed body, matching the current scan of the now-smaller dictionary. If no resource is inactive, do not build the index. The index never survives this invocation or crosses a callback, tick, rollback, recovery or migration boundary.

The current retirement body has no callback or addition path: `Economy.RetireWordboundTile` checks membership, computes checked Spent+1, removes the tile, then assigns Spent; it does not raise ItemChanged. Resource deadline replacement touches only resources. Therefore an ordered snapshot of matching candidates stays accurate except for removals already handled by consuming buckets. Do not generalize this index across later Synchronize mint/recovery steps, which can add or move bodies and change generations.

This reduces candidate discovery to one body pass plus resource lookups and actual retirements. It does not skip Demand, remove a resource, change deferred-recovery policy, weaken economy membership/overflow checks or avoid transaction rollback. No retained dynamic cache, static trust assumption, new public API, lifetime/deadline change or snapshot format change is needed.

## Focused verification before measurement

Keep the existing `WordboundLifecycleTests` and `WordboundAuthorityTests` retirement, three-lap progression, partial supply recovery and migration coverage. Add only meaningful public-API regressions missing from that set: retire an actual nonempty obsolete supply batch and prove the exact economy Spent increase/body removal; retain a still-demanded batch from another team/generation; validate later mint/recovery and then a subsequent retirement to expose a stale cross-call index; compare canonical export/import and letter conservation after each transition. Preserve multiplicity when several matching tiles carry the same letter. Do not fabricate invalid internal state or weaken full import validation to manufacture an alias case.

Before editing, inspect existing tests for overlap and choose the smallest additions. Required affected core filter should include `FullyQualifiedName~WordboundLifecycleTests|FullyQualifiedName~WordboundAuthorityTests|FullyQualifiedName~WordboundEntityBudgetTests`; exact expected count depends on the eventual test delta. Root must run R0, these core cases and the same actual public TeamSolo/native profile workload after the first candidate's baseline is retained. Compare Advance inclusive time, full forward-tick batch wall time and actual public outcome, while disclosing whole-machine scheduling variability. No performance acceptance is claimed from asymptotic source analysis alone.

Scope, if approved later: `AuthoritativeLifecycle.cs`, focused existing lifecycle/authority test file and this report. Do not bundle EnsureCapacity counters, resource sorting, source availability, mutable validation or additional capture changes.

## Authorized implementation checkpoint

Root subsequently approved this exact scope. Alignment-only commit `a71aa2c5` copies the current integration AuthoritativeLifecycle.cs, including existing Freestyle practice support, and MUST NOT be integrated. The lifecycle fixture already matched root. The later deliverable contains only the new retirement index, strengthened lifecycle regression and this report; the unrelated ModeDefinition change and untracked evidence remain excluded.

The implementation builds the transient dictionary only after the first unchanged Demanded check returns false. It groups all current Tile bodies by exact `(SourceId, OwnerTeam, RecoveryGeneration)`, retaining dictionary encounter order within each list. No Active/Protected filter was added. Default tuple string equality has the same ordinal value semantics as the former string `==` predicate. Each resource still calls Demanded in the original frozen resource order, then performs economy retirement before body removal, then resets its recovery deadline exactly where it did before.

The matched bucket is removed before processing. If separate resource scopes alias the same key, the later scope finds no already-retired bucket, matching the previous rescan of the now-smaller body ledger. Multiple actual tile bodies sharing a key remain a list, not a uniqueness error, so repeated letters are retained. No per-body skip hides an invalid economy reference: the original membership and checked spending operations still throw and the surrounding authoritative transaction still rolls back. Invalid snapshot/resource identities remain rejected by unchanged full validation. No new tolerance for malformed snapshots or private-state corruption is claimed.

The index is local to one invocation and is discarded on success or exception. Neither retirement nor resource-deadline reset calls user code or creates/replaces bodies, so no between-item callback can invalidate the ordered bucket. Later recovery/minting occurs after this method and obtains a fresh index on the next invocation. Team capture, practice selection, progress queries, retry behavior, existing callbacks and snapshots are unchanged. Candidate discovery changes from repeated body scans to one body pass plus lookups, but there is no measured timing or FPS claim.

## Regression and execution handoff

The existing TeamRace refill/solve regression now runs both `BED` and `BBEEDD`, increasing WordboundLifecycleTests from nine to ten cases without adding a separate redundant fixture. Each case uses actual public collection, crafting, recovery, deployment and puzzle completion. It proves:

- A nonempty generation3 recovered batch has the exact authored letter multiplicity.
- Only that batch retires, while rival and all other loose-tile IDs stay unchanged.
- Spending increases by the consumed three-letter recipe plus every retired tile; the economy tile count drops exactly and leftover inventory letters remain.
- The recovery deadline resets, solved evidence and the destroyed crafted-item tombstone remain, and the retained pre-retirement snapshot exports unchanged.
- Replayed commands and601 later ticks neither spend the batch again nor mint new retired-source identities; publication/import succeeds.

The existing three-lap test already validates fresh generations and retirement repeatedly, and the committed-host restore test already validates that retired letters/deadlines stay retired. Those cases and all existing assertions are retained. Practice selection/cleanup and strict resource-generation rejection remain in their existing suites. No internal setter/reflection mutation was added to manufacture malformed live authority state.

Root must run R0 then the bounded existing core suites, followed by its same actual public/native profile workload:

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs core --filter 'FullyQualifiedName~WordboundLifecycleTests|FullyQualifiedName~WordboundAuthorityTests|FullyQualifiedName~WordboundEntityBudgetTests|FullyQualifiedName~WordboundSnapshotValidationTests|FullyQualifiedName~WordboundPracticeTests' --json
```

Only the lifecycle class has a new case; assembled total is for root to record. The existing standalone ValidationProbe does not execute this retirement method and cannot measure its performance. Compare inclusive Advance, actual forward-tick batch wall time, readiness and public outcome using unchanged content/deadlines; retain scheduling and workload limits. Compiler/core/native/performance execution: NOT RUN in this lane. Independent source review requested from recover_input and pending at this checkpoint.

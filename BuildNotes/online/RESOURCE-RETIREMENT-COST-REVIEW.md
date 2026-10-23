# Resource retirement scan proposal

STATUS: READ-ONLY DESIGN. Root requested this separate proposal after integrating the reviewed tick/presence optimization as `041ce4a8`. No implementation or heavy job is authorized here. The first optimized native comparison must remain separate for attribution. Existing dirty ModeDefinition and historical evidence are untouched.

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

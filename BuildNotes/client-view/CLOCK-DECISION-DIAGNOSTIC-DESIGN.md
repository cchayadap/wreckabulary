# CLOCK field decision diagnostic proposal

STATUS: DESIGN ONLY — no production or fixture edits, compile or execution. Based on current integrated `WordboundAuthoredRuntime.cs`, `WordboundPuzzleRuntime.cs`, and the failed native 181114Z result. This is separate from the fixture's confirmed render-frame steering error.

The minimum useful observation is the actual pre-physics decision for the watched authored arm and CLOCK, before `Rigidbody.MovePosition`. A later raycast or recomputed decision in `FrameChanged` can disagree because physics has already moved bodies; it cannot explain this failure reliably.

## Proposed bounded interface

Add an instance-scoped observer and a readonly scalar record under `#if UNITY_EDITOR`, in a small partial beside `WordboundPuzzleRuntime`. The native fixture opts in only while crossing CLOCK and clears the observer in its route `finally` and teardown. No static observer, environment variable, runtime settings asset, release-player field, logging, telemetry or assembly dependency is needed.

Suggested contract: a watched fixture body ID used only as an in-memory filter, and `Action<WordboundClockDecision>` receiving the completed decision. The record carries frame epoch/tick; an enum outcome (`NoEligibleSlowField`, `CollisionRejected`, `ContainmentRejected`, `LineBlocked`, `LineBufferFull`, `Applied`); eligible-field count; evaluated flags for collision/containment/line; the final motion increment; and scalar geometric facts only when their stage ran. Use evaluated flags rather than converting short-circuited tests into false results. No snapshot, controller, Rigidbody or Collider references should escape through the record.

For containment, useful facts are actual source/fixture position, horizontal squared distance, height difference, radius and arc. For the actual line query, record origin/end, hit count, buffer capacity and the first rejecting hit's distance, layer, collider instance number, static-course-surface flag, registered-body flag and body kind. These identify a structural/owned/unregistered blocker without exporting private identity, names, payloads or hashes. A full buffer stays explicitly ambiguous; retain the existing fail-closed return. An unregistered non-structural collider does not block in today's `Blocks` policy and must not be reported as the cause.

## Exact capture points and behavior constraints

- In `MoveAuthoredFixtures`, count the eligible slow fields yielded by the existing `Fields(frame)` loop for the watched arm. Zero yields a `NoEligibleSlowField` observation at that real call site. It proves no field qualified there, not why an individual item was excluded; existing fixture facts can separately report deployed state/lifetime. Do not add a second enumeration or broaden `Fields` filtering for diagnostics.
- In `ClockAffects`, preserve collision → containment → line short-circuit order. Capture the values already used and the resulting decision; do not run later predicates after an earlier failure. Preserve the existing nonkinematic query body for authored machinery.
- In `ClearLine`, capture facts from the **same** raycast and the exact first `Blocks` result that decides the return. Do not raycast again, sort/expand the hit buffer, iterate beyond the current rejecting hit, or alter ignored source/target bodies. Prefer an editor-only optional trace path through the existing implementation, with no duplicated collision algorithm.
- Deliver the observer after the decision/hit facts have been copied and after any temporary trace context has been restored. If a scoped context is used to avoid altering other callers, restore it with `finally`; never leave tracing enabled after a failed query. Observer exceptions must not change the field result: disable the failing observer and expose one diagnostic-observer error to the fixture, which then fails evidence collection explicitly.
- All diagnostic fields, callbacks and extra calculations are editor-conditional and observer-conditional. Release behavior and data layout remain unchanged. The normal editor path with no observer takes only the null/filter guard; no per-tick strings, lists or logs.

The fixture should retain bounded outcome counters and the first/last decision plus at most 24 decision transitions, reporting omitted count. Correlate by epoch and completed-frame tick plus the proposed next tick, because this observation precedes simulation and the existing `FrameChanged` stage record follows it. Reset/stop on epoch change. Report only on failure, and distinguish multiple eligible fields from the one that set the minimum speed. The existing readonly canonical route observation should remain independent of the new steering controller.

## Acceptance and limitation

Source review must confirm identical predicate evaluation, unchanged ray count/mask/trigger handling and unchanged motion result with the observer off/on, including observer failure cleanup. The next actual six-family run must show the pre-physics decision corresponding to observed 600/1000 increments. Only then choose a product correction. This instrumentation cannot itself prove correct human controls, network performance, a completed CLOCK puzzle, or Windows build readiness.

The coordinator retains implementation ownership and must authorize the concrete source slice. Assets owns the separate `IBeforeTick` test-input repair; no source overlap is needed except the native fixture's bounded observer setup/reporting.

## Authorized implementation checkpoint

The coordinator approved and independently source-reviewed the implementation on 2026-10-23. It changes only `WordboundAuthoredRuntime.cs` and `WordboundPuzzleRuntime.cs`; alignment-only commit `50dce010` is excluded from delivery. No new assembly or source dependency is introduced. Compile/native execution of this delta remains pending.

The implemented observer uses `NativeClockFixtureId`, `NativeClockDecision` and `NativeClockDiagnosticError`. Public readonly records contain epoch/completed tick, numeric source/fixture component instances, stage evaluation flags/outcome, containment geometry and actual line-query/first-blocker scalars. It emits each eligible slow-field decision for the watched fixture, or `NoEligibleSlowField` when the original loop yields none. Per-field decisions are deliberately not described as final aggregate motion; multiple fields can contribute to the minimum speed. The fixture will correlate with actual canonical motion separately.

The editor-only `ClearLine` overload shares the original implementation and returns an `out` record from that same query. It avoids a scoped mutable trace context entirely. Existing callers use observation off. Actual collision, containment, raycast and `Blocks` calls remain outside diagnostic exception handlers. Only observer delivery and added blocker metadata are caught; failure disables the observer and retains its exception type so the fixture can reject missing evidence. The release preprocessing removes all observer/types/extra calculations; only explicit braces around existing single-statement scopes remain.

Independent coordinator verdict: bounded source PASS. Assets owns fixture subscription, bounded aggregation, evidence-error assertions and teardown. No actual CLOCK cause or completed physical route is claimed yet.

Lightweight release-branch comparison passed against exact aligned baseline `50dce010`: 2,562 tokens for AuthoredRuntime and 3,623 for PuzzleRuntime are identical after ignoring whitespace and the added scope braces. String literals were retained as tokens. All diagnostic identifiers are absent from those release branches. This is a textual preprocessing check, not compilation or executable equivalence proof. `git diff --check` also passed.

## Compiler compatibility correction

Root's combined R0 at 184145Z rejected `GetInstanceID` as obsolete with error severity. That result supersedes any suggestion that source review established compilation; native observer execution had not begun. The installed 6000.6.3f1 `UnityEngine.CoreModule.xml` documents `Object.GetEntityId`, `EntityId.None`, equality and `ToString`. The diagnostic fields now retain the actual opaque `EntityId` values as `SourceEntity`, `FixtureEntity` and `Line.BlockerEntity`, with `None` for absent source/non-blocker. No integer cast, hash code or raw bit extraction substitutes for object identity. Assets updates the fixture field comparisons and text labels in its separate lane. All changed code remains editor-only; the previously checked release token sequence is unaffected.

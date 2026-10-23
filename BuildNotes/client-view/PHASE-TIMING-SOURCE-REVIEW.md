# Bounded phase timing source review

STATUS: SOURCE PASS — bounded collector, production scopes, native consumer and 19 test cases; compile/native/player execution pending.

## Reviewed checkpoint and scope

Online checkpoint `c5db1efd`, excluding alignment-only `f58c60f4`, implements the nine phases authorized by `HOST-COST-MEASUREMENT-PLAN.md` (`44ebeadd`). Review is read-only production inspection; no compiler, Unity, player or benchmark was run by this lane. Parent retains all execution ownership. Unrelated dirty `ModeDefinition.cs` and historical evidence are outside this review and must not be integrated with the delta.

**Bounded production/collector SOURCE PASS.** Six Rules scopes wrap the existing rollback Capture, Synchronize, RetireInactiveResources, full validation, mutable validation and economy reconstruction. Three controller scopes wrap the existing ObserveHost evaluation, Prepare callback body and FrameChanged publication. Each original operation is still evaluated once. No new snapshot, validation, rule lookup, delegate or gameplay call is introduced. The existing inTick/rollback catches, validation error conversion, reconcile guard release and FrameChanged order remain in place; value-type using disposal also runs during exceptions and aborts.

`Wreckabulary.Rules.asmdef` retains `noEngineReferences=true` and its existing determinism-only reference. The collector depends only on System types. Its phase count and stack storage are fixed (nine phases, 256 nested scope tokens); all arrays are allocated at explicit capture startup. The disabled Editor/QA path returns a default scope before Stopwatch access. Active disposal updates counters only, detects non-LIFO/duplicate/late/cross-thread scopes and marks invalid captures rather than throwing into gameplay. Capture object identity plus unique in-capture token prevents an old scope from recording into a replacement capture. A wrong-thread stop does not clear the actual owner's thread-local capture; owner cleanup is still required, as the consumers must guarantee.

Every new production call and the helper are guarded by `UNITY_EDITOR || GSK_QA`. With neither symbol, the added diagnostics disappear; remaining explicit local declarations and the FrameChanged braces preserve original evaluation and control flow. This is a source preprocessing/control-flow check, not a normal-release compilation result. Non-development GSK_QA uses Stopwatch rather than depending on ProfilerMarker availability.

Reports label inclusive elapsed time, all instances on the owner thread and combined full-validation contexts. Nested phases must not be summed or subtracted as exclusive CPU. Zero-count phases and invalid totals are unavailable; stopped-window frequency, generation, raw totals/maxima and unfinished state are retained. No clock result is interpreted as network latency, frame rate or native allocation bytes.

## Pending consumer review

Initial native/QA wiring inspected while still uncommitted. The QA observer's Update/OnDisable/OnDestroy path is needed because the installed QaRunner can abandon a timed-out iterator without disposal. The native teardown should sample its optional end boundary best-effort, so a missing/stopped runner cannot prevent normal cleanup. Online has been notified; final consumer and focused test checkpoint remains required. Compile/native execution and same-workload enabled/disabled perturbation comparison are pending parent gates.


## Final native/test closure

Reviewed exact follow-up `963e35b9` after collector `c5db1efd`. Native end-boundary observation now runs inside its own best-effort try, and finally stops capture before existing runner/service cleanup. An unavailable boundary emits a fixed diagnostic type and unknown boundary values; it does not skip cleanup. The `teardown` outcome identifies where measurement stopped and is explicitly not a test pass. Startup capture switches to a separately allocated ready capture immediately after the unchanged all-peer-ready predicate; no extra wait, capture, simulation step or timeout change is inserted. The original eight native ProfilerRecorders remain independent.

All **19** source-counted `Wreckabulary.Tests.WordboundPhaseCaptureTests` cases were inspected. They test disabled and conflicting ownership, independent threads, cross-thread cleanup, exceptional and out-of-order disposal, stale scopes, fixed nesting capacity, invalid/unavailable reporting, five real mode Advance/atomic-validation snapshot parity cases, the real host-query rollback/retry/reentry path, and actual atomic abort/prepare-error/apply-error boundaries and exception identity. Their fixture internals are in the same Rules test assembly; runtime operations use existing public APIs. No new production visibility, reflection, copied gameplay implementation or assertion weakening was introduced.

The test and helper compile guards matter: existing ordinary `Tools/RulesHarness` projects define neither UNITY_EDITOR nor GSK_QA, so the default core run excludes these 19 cases. Parent must require the exact native Editor filter with 19 discovered cases; zero/skipped is not acceptance. Root's QA assembly already directly references Rules, Client, Gameplay and GameStudioKit.Runtime; no assembly-reference expansion is required for the new observation. Assets owns the separate final QA lifetime/abandonment review; my source read found no additional issue in the observer or unchanged 90-frame scenario flow.

Final bounded verdict: **SOURCE PASS** for `c5db1efd` + `963e35b9`; exclude baseline-only `f58c60f4`. No compiler/native/player run was performed by this lane. Root still needs native19, enabled/disabled workload comparison and real non-development QA output before interpreting timings. Inclusive elapsed totals are not exclusive CPU, and capture results do not certify multiplayer or frame-rate acceptance.

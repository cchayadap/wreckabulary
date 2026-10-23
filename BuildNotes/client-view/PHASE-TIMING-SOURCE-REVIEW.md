# Bounded phase timing source review

STATUS: IN PROGRESS — production/collector source review complete; consumer/tests closure pending.

## Reviewed checkpoint and scope

Online checkpoint `c5db1efd`, excluding alignment-only `f58c60f4`, implements the nine phases authorized by `HOST-COST-MEASUREMENT-PLAN.md` (`44ebeadd`). Review is read-only production inspection; no compiler, Unity, player or benchmark was run by this lane. Parent retains all execution ownership. Unrelated dirty `ModeDefinition.cs` and historical evidence are outside this review and must not be integrated with the delta.

**Bounded production/collector SOURCE PASS.** Six Rules scopes wrap the existing rollback Capture, Synchronize, RetireInactiveResources, full validation, mutable validation and economy reconstruction. Three controller scopes wrap the existing ObserveHost evaluation, Prepare callback body and FrameChanged publication. Each original operation is still evaluated once. No new snapshot, validation, rule lookup, delegate or gameplay call is introduced. The existing inTick/rollback catches, validation error conversion, reconcile guard release and FrameChanged order remain in place; value-type using disposal also runs during exceptions and aborts.

`Wreckabulary.Rules.asmdef` retains `noEngineReferences=true` and its existing determinism-only reference. The collector depends only on System types. Its phase count and stack storage are fixed (nine phases, 256 nested scope tokens); all arrays are allocated at explicit capture startup. The disabled Editor/QA path returns a default scope before Stopwatch access. Active disposal updates counters only, detects non-LIFO/duplicate/late/cross-thread scopes and marks invalid captures rather than throwing into gameplay. Capture object identity plus unique in-capture token prevents an old scope from recording into a replacement capture. A wrong-thread stop does not clear the actual owner's thread-local capture; owner cleanup is still required, as the consumers must guarantee.

Every new production call and the helper are guarded by `UNITY_EDITOR || GSK_QA`. With neither symbol, the added diagnostics disappear; remaining explicit local declarations and the FrameChanged braces preserve original evaluation and control flow. This is a source preprocessing/control-flow check, not a normal-release compilation result. Non-development GSK_QA uses Stopwatch rather than depending on ProfilerMarker availability.

Reports label inclusive elapsed time, all instances on the owner thread and combined full-validation contexts. Nested phases must not be summed or subtracted as exclusive CPU. Zero-count phases and invalid totals are unavailable; stopped-window frequency, generation, raw totals/maxima and unfinished state are retained. No clock result is interpreted as network latency, frame rate or native allocation bytes.

## Pending consumer review

Initial native/QA wiring inspected while still uncommitted. The QA observer's Update/OnDisable/OnDestroy path is needed because the installed QaRunner can abandon a timed-out iterator without disposal. The native teardown should sample its optional end boundary best-effort, so a missing/stopped runner cannot prevent normal cleanup. Online has been notified; final consumer and focused test checkpoint remains required. Compile/native execution and same-workload enabled/disabled perturbation comparison are pending parent gates.

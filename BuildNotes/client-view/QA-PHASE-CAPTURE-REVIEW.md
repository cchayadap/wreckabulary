# Workshop QA phase-capture consumer review

Status: bounded SOURCE PASS through follow-up `ef20294588584bc11b225c236031e36e9ca8737d`. Parent assigned a read-only independent review of the online lane's timing consumer: explicit opt-in, non-development GSK_QA behavior and lifetime across startup, readiness, timeout/cancellation, abandoned coroutines and destruction. Input owns collector/native/test review. This lane owns only this report; no source, kit or heavy-job changes. Existing work and unrelated log are preserved.

Reviewed checkpoint: online `963e35b934d7bb9a8115b0264fa3c4b67bf7e263`, consumer files `WordboundWorkshopQa.cs` and matching `WordboundPhaseObservation.cs`, atop collector `c5db1efd`. Exclude alignment-only `f58c60f4`. Native/build/player execution remains root-owned.

## Confirmed boundary finding

`QaRunner.Drive` exits on deadline or nested iterator exception without disposing its iterator stack (`Packages/com.gamestudiokit.core/Runtime/Qa/QaRunner.cs`). The outer run can proceed into the next Return step in the same continuation. The initial consumer relies on its iterator finally or observer Update; on this abandoned path, the first is not guaranteed to execute and the second may occur after `client.ReturnToWorkshop()` starts synchronous teardown. Therefore the nominal ready window can include return work and its final boundary may no longer contain the live gameplay frame.

Requested a bounded consumer correction: explicitly finish the owned observation at the very start of Return, before RecordPlaytest or ReturnToWorkshop, while retaining Update/OnDisable/OnDestroy as fallback. No kit, deadline, gameplay, captures or assertion change is needed. This finding affects measurement attribution; it is not a new gameplay failure.

Closure checked directly in `ef202945`: the scenario now owns the observer in a field; `FinishPhases` clears that field before calling the observer's idempotent Finish. Return invokes it before both RecordPlaytest and client teardown. The normal iterator finally uses the same helper; independent observer callbacks remain. Thus the actual timeout path closes at the observable return boundary while the gameplay snapshot is still available, rather than including return teardown. SOURCE PASS; the original finding is preserved here as review history.

## Other reviewed lifetime and compile properties

- Exact environment value `WORDBOUND_PROFILE_PHASES=1` enables the observer. Unset/other values return before creating a component or capture. Both scenario and observer compile under `UNITY_EDITOR || GSK_QA`; root QA assembly has that same constraint, and the collector shares the guard. There is no development-build requirement, so the intended non-development release-qa player can include this path; a normal non-QA player excludes it.
- Startup begins immediately before the unchanged RequestPlaytest call. Readiness stops/reports startup and begins a separate ready generation using the actual snapshot boundary. The readiness wait, 90 post-ready frames, outer deadline, image captures and existing QA expectations are unchanged.
- Stop clears the consumer's capture reference before disposal/reporting, preventing repeated callbacks from double-reporting the active window. Collector disposal on the Unity main thread releases its thread-local owner. Reporting failures are caught; the boundary is read only once per stop and has no per-frame scene scan.
- Observer Update notices stopped QA, a changed step or a removed launcher independently of iterator cleanup. OnDisable/OnDestroy provide owned-component cleanup paths. It does not destroy the launcher/client. On timeout/watchdog, summaries may be available only in the player log because the current step is no longer exact_playtest; no complete checks.json summary is implied.

These are source findings, not a native callback or captured-timing pass. Abnormal process termination cannot be made into a completed capture by these managed cleanup paths. Collector arithmetic/native coverage is separately reviewed by Input.

Native acceptance should verify one startup generation and one ready generation with matching stop reasons, no active-capture ownership left after success/timeout, the unchanged 90-frame requirement and one final live snapshot boundary before Return. On abandoned-step paths, inspect the retained player log rather than assuming all final fields were attached to the already-finished playtest checks. A capture that cannot acquire ownership must remain explicitly unavailable. Timings are inclusive elapsed scope observations, not CPU utilization, frame rate or public multiplayer performance. No remaining blocking consumer source issue was found in the reviewed scope.

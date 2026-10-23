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

## Actual QA player phase run: 204132

Execution review: COMPLETE. The non-development QA player recorded both timing windows and closed the ready window before Return teardown on the actual timeout path. The workshop scenario still FAILS its unchanged 90-second playtest deadline; this is a successful observation of a failed flow, not delivery acceptance.

Root launched normal visible run `20261023T204132Z-visible-wordbound_workshop_flow` with process environment `WORDBOUND_PROFILE_PHASES=1`. Directly read build receipt `Builds/qa-20261023T203920Z/build.json`: release-qa, non-development, Unity 6000.6.3f1, source `d8eb1804da0b79b4253bfc44614aa5383b3941ae`, tree `4f7c7fc736e7aaf4875ae5818b85ce09fbff4e9d9078a9238a48d2f737fffc49`, ok=true, recorded build duration 101.8 seconds (root wrapper interval 102.6 seconds). Final status records PID 38424, normal 1280×720 scenario/seed1, start 20:41:32.3555416Z, finish 20:43:47.2590308Z, exit 1. This wrapper is diagnostic-only, not an official gate replacement. The process-environment setting is root-reported; the emitted summaries independently establish that the enabled collector path ran. `checks.json` has null commit/tree, so source attribution uses the separate build receipt.

### Observed window boundaries and cleanup

Directly inspected `Logs/player/20261023T204132Z-visible-wordbound_workshop_flow.log` and final `docs/qa/runs/20261023T204132Z-visible-wordbound_workshop_flow/checks.json` in integration. Exactly two window summaries and eighteen phase rows were recorded; no unavailable summary appeared. Both windows use thread 1, Stopwatch frequency 10,000,000, `invalid=None`, `inFlight=0`.

| Window | Generation / outcome | Elapsed ticks / seconds | Actual snapshot boundaries |
| --- | --- | --- | --- |
| Startup | 1 / local-ready | 58,463,597 / 5.8463597 | No frame initially; epoch 0, tick 50, 93 bodies, 294 resources, 216 mechanisms at ready |
| Ready | 2 / return-boundary | 843,499,108 / 84.3499108 | Tick 50 to 4970; epoch and entity counts unchanged |

The ready summary at log line 60 and its nine phase rows precede `before_return` at line 70. That latter observation still has the live client, controller, world and camera, tick 4970, no pause, and 81 completed post-ready update waits. This verifies the intended abandoned-iterator timeout boundary before gameplay teardown. The final ready window is correctly retained in the log even though its fields are absent from the already-finished exact_playtest check. No duplicate summary appears during subsequent cleanup.

This run does not independently test normal 90-frame completion, every cancellation/destruction branch, abnormal process termination, or direct thread-local ownership readback. The observed disposal order, zero in-flight scopes and lack of duplicate reporting support this specific timeout closure; the broader lifetime analysis remains the source review above.

### Recorded phase measurements

Every row has `status=observed`. Values below are the log's inclusive elapsed milliseconds, with actual invocation counts. These scopes can overlap/nest, include all instances on the owning thread, and combine FullValidation contexts. They must not be summed as exclusive time or described as CPU time, per-frame cost or FPS.

| Phase | Startup count / ms | Ready count / ms |
| --- | ---: | ---: |
| RollbackCapture | 50 / 64.891 | 4920 / 5526.181 |
| Synchronize | 51 / 121.506 | 4920 / 7717.210 |
| RetireResources | 51 / 17.655 | 4920 / 1091.268 |
| FullValidation | 59 / 367.449 | 5061 / 19601.250 |
| MutableValidation | 51 / 31.741 | 4920 / 3005.204 |
| EconomyValidation | 110 / 34.642 | 9981 / 2272.127 |
| ObserveHost | 50 / 76.936 | 4920 / 6019.018 |
| Prepare | 51 / 205.278 | 4920 / 4953.966 |
| FrameChanged | 51 / 5.800 | 4920 / 611.114 |

FullValidation has the largest recorded phase total, 19.601250 seconds during the 84.349911-second ready window. That observation does not alone explain the whole window or establish a complete bottleneck diagnosis. Periodic samples advance 900 canonical ticks per 15 Unity update waits; these are not presented-frame measurements. `perf` is empty in the final checks.

### Flow and actual image review

Open, workshop edit/autosave, Return and Undo/Redo PASS. Autosave is the exact 183,314-byte draft, without backup use or temporary files; Return preserves the same history and unchanged draft and removes client/camera while restoring the editor. Console counts are zero errors, exceptions and warnings. No crash is recorded; the player exits with the failed QA result.

Exact playtest FAILS `timed out after 90 s`. Ready occurred at 5.854643 seconds, frame 14, tick 50. The last periodic playtest probe records 75 waits; the Return boundary records the more recent 81/90 waits at 90.204967 seconds, frame 96, tick 4970. No gameplay, guide or presentation fault is reported there. Later world/equipment captures were not reached. The difference from earlier runs is not a controlled performance-benefit claim.

Directly viewed the actual `wordbound_local_ready.png`: the original rounded red avatar, dark satchel/boots and pale cuffs remain. Compact top HUD, three-line Packing Yard clue and two-line checkpoint caption are readable in this view. Footer controls read `[E] Pick up · [Q] Craft · [LMB] Smash · [ESC] Menu`. No visible black frame, missing-material presentation, text overlap or clipping regression appears in this captured view. Workshop and returned-workshop PNGs are byte-identical to the previously inspected corrected workshop view. This is static image evidence, not owner approval, final art quality, camera-motion/animation acceptance, controller/rebinding coverage or full-flow success.

### Artifact identity

Paths are relative to integration. SHA-256 values were read after completion; no runtime/source/artifact changes were made by this review.

| Artifact | SHA-256 |
| --- | --- |
| `Builds/qa-20261023T203920Z/build.json` | `65ED85772CECD5BE336170B4C18746F2C0E39F22DEC07BD686BEBE62DFB9A080` |
| Player log above | `2AA994A4E5B23D26FC1EC3A049ECCCD50B821D527B2190F93C3E7452FC4C5C86` |
| Final checks above | `51320493E449E9AB751FFB5622C85E54F8D22E303E2DAF373EA725E3BDC7A61C` |
| Ready PNG | `70E5BF1E75EBD9DD5B2052FE1379EFC7365B67B75B0F7EF4737A02A4FDB81057` |
| Workshop and returned-workshop PNGs | `AD72EBA2592686855CB8E79D0156CA3C76E17E10B78BE01D0CAEF85D3D7BE8D0` |

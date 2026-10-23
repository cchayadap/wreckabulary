# Host cost and public input measurement plan

STATUS: SOURCE INVESTIGATION COMPLETE; MEASUREMENT DESIGN PROPOSED, NOT IMPLEMENTED. Requested after root reported retirement R0 PASS (201147) and 167 core PASS (201209). Completed native 201239 comparison is included below. No implementation, compiler, tests, profiling jobs, service requests or root writes in this lane.

Scope: identify the smallest useful native phase measurements for remaining Rules Advance/reconciliation cost, preserving engine-free Rules and no default/release diagnostics; inspect the completed 200052 public-input evidence without treating readiness or absent input callbacks as proof of a service fault.

## Confirmed completed evidence

Exact integration artifacts: `Logs/gate_test_editmode_20261023T200052Z.log` and `TestResults/editmode_20261023T200052Z.xml`. The public case is `WordboundNativeGameplayTests.PublicTeamRacePreservesWholeSoloParties`. Its XML failure is the admitted client movement deadline, not authorization, writer expiry, snapshot corruption or a reported session failure.

The 36.558-second native profiler window recorded the following. These are inclusive main-thread marker durations across all in-process instances, not exclusive CPU, frame time or an FPS estimate. Every listed buffer was valid and unwrapped.

| Marker | Invocations | Captured total ms | Maximum sample ms |
| --- | ---: | ---: | ---: |
| Reconcile | 508 | 6,094.033 | 97.512 |
| Advance | 507 | 5,679.164 | 41.840 |
| Capture | 508 | 1,009.875 | 18.791 |
| Presentation | 508 | 199.486 | 9.177 |
| PhysicsApply | 508 | 476.196 | 11.153 |
| BeforePhysics | 507 | 1,226.844 | 25.255 |
| BuildWorld | 2 | 660.139 | 469.378 |

`WB.Match.Validate` had no samples because the earlier atomic validation change removed that marker boundary. Its cost is **unavailable**, not zero. Reconcile already includes Presentation, PhysicsApply, validation, body preparation and synchronous FrameChanged listeners. The rollback snapshot taken inside Rules Advance is not the controller's separate Capture marker. Do not add these totals or attribute Reconcile's remaining duration entirely to validation.

## Minimum next CPU measurements

Keep the same public course/seed/roster and existing outer/tick-batch observers. Add only these nine coarse synchronous boundaries if root authorizes a follow-up implementation. Labels below are fixed phase labels; the required measurement mechanism is Stopwatch aggregation, not ProfilerMarker alone, so it also works in a non-development GSK_QA player:

| Proposed boundary | Exact scope | Question answered |
| --- | --- | --- |
| `WB.Rules.Advance.RollbackCapture` | The existing `Capture(Math.Max(1, commits.PublishedSequence))` in `AuthoritativeMatch.Advance`, without another capture | How much of Advance is its mandatory rollback copy? |
| `WB.Rules.Advance.Synchronize` | Existing `Synchronize(before)` call, including resource retirement/refill and budget work | Does synchronization dominate the remainder after capture? |
| `WB.Rules.Advance.RetireResources` | Existing `RetireInactiveResources()` call inside Synchronize | Is retirement itself still substantial, versus other synchronization work? |
| `WB.Rules.Validation.Full` | Existing `MatchStateValidation.Validate` body, including its existing error conversion | Full dynamic and structural validation cost across reconciliation, publication and import |
| `WB.Rules.Validation.Mutable` | Existing `ValidateMutableDependencies` body | Cost of the required fresh checks after preparation against the distinct physics rules |
| `WB.Rules.Validation.Economy` | Existing `ValidateEconomy` body: construction, AddPlayer and Restore | Whether economy reconstruction dominates either validation pass |
| `WB.Match.ObserveHost` | Existing `options.ObserveHost?.Invoke(frame, poses)` | Authored physical observation currently outside Advance and BeforePhysics |
| `WB.Match.Prepare` | One invocation of Reconcile's existing local `Prepare` callback | Body plan, input-authority lookup, spawn/despawn, configuration and callback costs; includes existing Presentation |
| `WB.Match.FrameChanged` | Existing synchronous `FrameChanged?.Invoke(published)` | Cost of presentation/HUD/other subscribers already inside Reconcile |

No per-body, per-source or per-mechanism marker is needed initially. Capture and Synchronize are disjoint children of Advance; the remaining Advance work still includes bounded input checks, pose/mechanism application, crafting, command execution, combat, progress and capacity checks. Measure those next only if the first split leaves a dominant unexplained remainder. Retain failure/rollback cases in accounting; do not move the rollback catch or its restoration to create a cleaner timer.

Full validation and its nested Economy marker also run outside Reconcile. Therefore their global totals cannot simply be subtracted from the Reconcile total. The adapter should retain only bounded numeric context counters for full-validation calls inside an atomic reconciliation versus other calls, or report that distinction as unavailable. All nested totals stay labelled inclusive. Do not recreate a second validation pass to make a benchmark easier.

### Installed support and smallest bridge

`Assets/_Project/Scripts/Rules/Wreckabulary.Rules.asmdef` has `noEngineReferences: true` and references only the determinism assembly. Do not introduce Unity.Profiling into Rules or change that assembly contract. The installed and already working test consumer is `Tests/OnlineGameplay/WordboundNativeProfiler.cs`: Scripts-category ProfilerMarker, ProfilerRecorder, main-thread-only per-invocation samples, capacity 32,768, wrap reporting, TimeNanoseconds unit checks, and end-only formatting. The physical fixture has an equivalent consumer.

For the six Rules boundaries and three Unity call-site boundaries, propose one **System-only, editor/QA-only** fixed phase enum, thread-local collector and value-type scope. Preallocate count/total/max arrays at explicit fixture/scenario startup. Begin returns a struct retaining collector, phase, capture generation and Stopwatch start; Dispose updates those numeric cells only. No delegate, Unity callback, per-call object or per-tick formatting is needed. All call sites and the helper compile out when neither `UNITY_EDITOR` nor `GSK_QA` is defined. With capture disabled in Editor/QA, a null collector returns a default scope with no allocation, Stopwatch call, logging, payload capture or state inspection. There is still a null check in those diagnostic-capable builds; do not claim literally zero executed instructions there. Normal release contains no new diagnostic call sites.

This addresses root's explicit non-development GSK_QA requirement: ProfilerMarker.Auto may be inactive there, while System.Diagnostics.Stopwatch is independent of the development profiler. Stopwatch totals are **inclusive elapsed time**, including descheduling, not exclusive CPU. Existing native ProfilerRecorder output remains a separate corroborating measurement. The new output must give Stopwatch.Frequency, completed count/total/max ticks, unfinished scopes, generation, window and invalid/unavailable state, with conversion/formatting only when capture stops.

Requirements before accepting such a bridge:

- Collector registration is thread-local and owned by one synchronous thread/run, with no persistent reference to a match, catalogue, runner or snapshot. Capture its generation at scope entry so disposal cannot end a replacement capture. Clear it on fixture/scenario teardown, including failed setup and cancellation; restore a previous owner only if still legitimate. Reject simultaneous overlapping capture ownership rather than silently combine runs.
- All scopes use `finally`/value-type disposal without spanning `await`. No diagnostic delegate may execute from gameplay. Scope entry/exit mismatches, late scope disposal, negative elapsed or counter overflow make the capture explicitly invalid; they must not replace production exceptions or affect rollback.
- The observer performs timing/counter updates only; no gameplay callback, rule lookup, network action, capture or validation. Reentrancy guards and production catch boundaries remain exactly where they are. No allocation, snapshots or formatting from scope disposal.
- No successful-path string formatting, lists, closures per scope, allocation stack collection, deep profiling or SDK instrumentation. Bound all arrays and contextual counters ahead of the workload.
- Enable capture only through an explicit project-owned native test or QA scenario flag, not automatically because GSK_QA is compiled. Existing uninstrumented/default paths remain available for comparison. No SDK/kit modifications or global profiler settings.

The three Unity boundaries use the same compile-gated System collector, so their measurements remain available in that non-development QA player. Do not add a production callback parameter to the reconciliation API just to expose timing.

To separate steady work, retain startup composition as its own capture, then start a new preallocated capture at the existing all-peer-ready boundary (native) or local-ready boundary (QA). Stop/report it at the existing movement/scenario completion or failure. Record starting/ending canonical tick, completed Advance count, body/resource/mechanism counts obtained once at the boundary, and operation outcome. Do not add waits, advance ticks, or reset the gameplay clock. Empty/partial windows are unavailable/partial, never successful zero cost. A tiny untimed atomic-context depth scope inside Rules ReconcileAndApply may categorize full/mutable validation counters; if omitted, retain the existing explicit limitation on cross-context totals.

Before using results to choose another optimization: compile diagnostics-capable and normal-release forms; verify disabled/active/invalidated captures preserve exact snapshot hashes, result/error types, rollback and reentry outcomes; verify scopes close on prepare abort/exception and nested validation; verify normal release excludes the helper/call sites. Compare the same bounded workload once with detail disabled and once enabled. Report outer/batch timings and whether the outcome changed. This control estimates perturbation; it does not remove OS scheduling variance or make timing a network latency measurement. The earlier Unity Mono allocation counter returned zero for both paths, so use no claim of native byte allocations from that counter.

## What the public client's zero input count means

Readiness ended after 9.442 seconds at host canonical tick 186 and client canonical frame tick 0. Both were running, session-ready, had the gameplay scene and isolated physics scene, and reported no failure. Replicated-body membership passed. The movement wait then consumed 11.750 seconds, including a 2.625-second largest observed continuation gap; host advanced to canonical tick 507 while client canonical frame remained 0. Client ReadMovement and moving-read counts both remained zero.

`inputReads` increments inside the fixture's `ReadMovement` delegate. The production caller is `WordboundGameplayController.OnInput`, **after** rejecting a foreign runner, missing frame, stopping controller, controller fault, or non-ready session. It is not an unconditional count of Fusion OnInput callbacks. Controller.ChangeRunner sets ProvideInput true and registers callbacks; project source has no other assignment to ProvideInput. The recorded readiness booleans are sampled at operation boundaries, not at every earlier OnInput call.

Likewise `canMove=false` does not establish a replica motor defect: the diagnostic computes `Controller.PhysicsWorld != null && PhysicsWorld.CanMove(...)`; that physics authority exists on the host. The compact TimeRun client also reported false while actual movement, commands and exact snapshot ACK succeeded. Its snapshot tick is a received canonical checkpoint, not Fusion Runner.Tick or InputTick.

The existing tick observer proves the public client was not wholly absent from SDK simulation:

| Observation | Host | Client |
| --- | ---: | ---: |
| Completed tick batches | 278 | 103 |
| Forward batches / SDK ticks | 278 / 1,166 | 55 / 145 |
| Resimulation batches / SDK ticks | 0 / 0 | 48 / 544 |
| Largest forward batch | 60 ticks / 2,430.707 ms | 24 ticks maximum; maximum wall sample 7.704 ms for 6 ticks |
| Age of last completed batch at observer stop | 2,203.814 ms | 13,124.127 ms |
| Age of last Unity observer Update at stop | 24.075 ms | 23.446 ms |

The client remained Unity-update-active but no longer completed Fusion batches near the end. Its last retained forward batch ended at observer 23,444.944 ms; the window ended at 36,564.720 ms. The host executed long 60-tick catch-up batches. SDK tick totals include startup and resimulation and are not canonical match ticks. Operation and observer timings do not yet establish the precise first-ready-to-first-input ordering. This supports investigating scheduler/input-production state under the full workload; it does **not** prove why Fusion stopped forward work or implicate the public coordinator. No reported service/lease/auth fault accompanies this failure.

### Smallest discriminating input observation

Use the existing fixture-owned runtime observer/adapter, enabled only for the diagnostic run, with no input.Set, simulation writes or tick-rate changes:

1. Register a read-only INetworkRunnerCallbacks observer to count raw OnInput and OnInputMissing calls, first/last Stopwatch offsets, and bounded forward/resimulation classifications. Snapshot only installed public `Runner.ProvideInput`, `Runner.InputTick`, `Runner.Tick`, `TicksExecuted`, running/client/server flags and whether LocalPlayer is real at readiness, movement begin/end and the existing bounded batch tail. Never print player identities or tokens.
2. Count actual Controller.OnInput outcomes before its existing guard under the same editor/QA diagnostic gate: foreign runner, no frame, stopping, fault, not GameplayReady, accepted ReadMovement, input set, or caught failure. Preserve the original short-circuit guard and callback behavior. A separate observer seeing input cannot prove that this controller was registered or accepted it; these outcome counters close that gap.
3. Timestamp first non-null canonical frame and first GameplayReady observation on the same diagnostic Stopwatch. Retain no more than a fixed transition tail plus aggregate counts. Sample public state without fetching credentials, calling injected clocks extra times, or invoking Poll.

Interpretation: zero raw callbacks with stalled InputTick points to SDK scheduling/input collection; raw callbacks with rejected guards points to controller/session readiness; accepted ReadMovement but no set is a callback/fault path; valid set plus continued runner ticks but missing host movement requires a later host-input-admission/motor check. None of these alone establishes a remote service fault. Preserve the current deadlines and outcome assertions throughout.

The installed Fusion 2 SDK documents these public members in `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml`: ProvideInput (15046), InputTick (15200), Tick (15204), TicksExecuted (15219), INetworkRunnerCallbacks.OnInput (17701), and OnInputMissing (17710). This proposal needs no reflection or private SDK modification.

## Handoff

### Completed retirement comparison: 201239

Exact artifacts: `Logs/gate_test_editmode_20261023T201239Z.log`, `TestResults/editmode_20261023T201239Z.xml`, and `.harness/delivery/native-retirement-full-online-2012-status.json`. Wrapper 30820 finished at 20:15:59Z, exit 1; 0/2 cases passed. Root supplied source checkpoint 924fa277.

Public TeamSolo again failed its 10-second admitted movement predicate (observed continuation elapsed 10.002 seconds). This time client input was present: readiness host tick 132/client canonical tick 12, client ReadMovement 116; movement end host tick 390/client canonical tick 150, ReadMovement 474, **358 nonzero reads**. Its received canonical position changed Z from -18.000 to -17.968; that checkpoint position is not the host's complete final motion trace. Host canonical movement never satisfied the actual >0.25 m predicate. Both sessions still reported no failure.

The public window recorded Reconcile 391 / 4,659.606 ms (11.917 ms per captured call), Advance 390 / 3,090.472 ms (7.924 ms per captured call), Capture 391 / 680.859 ms, Presentation 391 / 189.059 ms, PhysicsApply 391 / 355.205 ms, and BeforePhysics 390 / 798.980 ms. Buffers were valid/unwrapped. Compared with 200052, Advance's observed per-call average is lower while Reconcile remains about 12 ms; this is an uncontrolled single-run comparison with differing completed ticks/window/startup scheduling, not isolated causal proof or an FPS result. Do not add nested durations.

Host still reached 60-tick batches; largest forward batch wall time was 1,857.784 ms. Client now completed 295 forward batches / 562 ticks and 220 resimulation batches / 573 ticks; last completed batch age at stop was 1,183.024 ms, rather than the prior 13,124.127 ms. The log also contains 52 Fusion input-buffer-full warning lines. Those warnings establish rejected buffer insertions, not their cause or the complete per-tick delivery/acceptance history. The old zero-input explanation must not be reused as the current cause.

Compact authored TimeRun passed initial movement, authenticated command and exact command-bearing snapshot ACK (1.734-second ACK wait), progressed through native TABLE deployment/socket and UsePuzzle, then failed with `Sequence contains no matching element` at fixture `CompleteCompactTimeRun` line 189 / `Pose` line 149. Input owns this separate fixture diagnosis. It is not a second public movement or service failure, and full-match acceptance remains incomplete.

### Minimal implementation slices for root decision

1. **Timing only:** one compile-gated System collector + fixed nine phases; six Rules call sites in AuthoritativeMatch/AuthoritativeLifecycle/MatchStateValidation and three controller call sites; explicit fixture/scenario capture with startup/ready windows; focused collector exception/ownership/disabled/parity tests. Existing native marker helper remains unchanged. No new gameplay callbacks or clock/validation calls.
2. **Input flow only:** retain the small raw-input/controller-guard observation above, and add bounded host-player motor outcome counts because 201239 already proves ReadMovement is reached. In `WordboundNetworkBody.FixedUpdateNetwork`, record the outcome of the existing gates and existing single GetInput/TryMovement evaluation: frozen/inactive/non-player, authenticated actor mismatch, CanMove rejection, movement-environment rejection, missing GetInput, invalid epoch/vector, accepted zero/nonzero direction, and velocity assignment. Reuse evaluated locals without repeating GetInput, CanMove, ground/environment queries or changing short-circuit order. Record only fixed seat ordinal/role, tick ranges, counts and bounded velocity/position scalars, no identities or payloads. Observe authoritative post-step pose at existing completion, not by extra simulation. This separates missing/stale host input from a motor/collision obstruction and is now more diagnostic than client callback counts alone.

These slices can be reviewed and gated independently. No implementation is authorized by this report itself. No budget, timeout, input-buffer, tick-rate, lease, auth, snapshot or service change is proposed. This report makes no 60 FPS, networking acceptance, public-mode completion or release-readiness claim.

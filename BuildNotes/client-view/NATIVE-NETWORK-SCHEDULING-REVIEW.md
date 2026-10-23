# Native network scheduling investigation

STATUS: COMPLETE — source and recorded-result review only. No runtime, SDK, configuration, deadline or test changes; no heavy jobs launched. Runtime cause remains partly unmeasured.

## Scope and preserved evidence

The completed integration run `TestResults/editmode_20261023T160217Z.xml` has 0/2 passing cases. Its log is `Logs/gate_test_editmode_20261023T160217Z.log`. This investigation separates a measured stale transfer from the hypothesis that an over-budget synchronous Fusion catch-up batch starves receive processing. A default batch cap of 60 is not yet established by the inspected public SDK documentation.

- Public TeamRace Solo: 91.246958 s case duration. Readiness lasted 44.643 s, with a 6.278 s maximum observed coroutine gap and host canonical tick 360.
- The rejected transfer reports elapsed 17,972 ms against its unchanged 10,000 ms limit; same mark/count/length/wire hash are all true and duplicate chunk is false. Thus the stale branch is identified for this failure. This is time observed by the receive continuation, not measured network round-trip time.
- Physical fixture: 218.115201 s case duration; FAN wait 13.189 s against 12 s, ticks 4320→4620, lifetime cancellation false. The parent owns that separate physics investigation.

## Confirmed scheduling path

Inspected the integration checkout at `153ff57`, installed Fusion 2.1.3 XML and Physics Addon 2.1.2 source. The recorded run remains identified by its log/XML, rather than being relabelled as a test of this later HEAD.

1. `Assets/_Project/Tests/OnlineGameplay/WordboundNativeGameplayTests.cs:333` and production `Scripts/Online/Client/WordboundMatchClient.cs:109` construct a Multiple-peer configuration with client tick rate 60 and server tick/send intervals 1. `Scripts/Online/Fusion/WordboundFusionSession.cs:214` supplies no `StartGameArgs.Updater`. There is no project override of the updater or simulation time mode in the inspected scripts/tests.
2. Installed `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml` documents the default updater as calling `NetworkRunner.UpdateInternal` before normal script `Update`, and its `InvokeUpdate` as updating all runners. The public API allows a supplied updater, but documents no preemption of work inside one `UpdateInternal` call. [Photon updater contract](https://doc-api.photonengine.com/en/fusion/current/interface_fusion_1_1_i_network_runner_updater.html).
3. `Scripts/Online/Gameplay/WordboundGameplayDriver.cs` has order 900. Its `FixedUpdateNetwork` calls `PrepareHostPhysics`, which polls the session and performs any needed reconciliation and prephysics work. The installed `RunnerSimulatePhysics.cs` has order 1000; lines 107–155 synchronously execute prephysics callbacks, the isolated physics scene simulation, then postphysics callbacks. `WordboundPhysicsWorld.AfterSimulate` invokes `HostStepCompleted`, and `WordboundGameplayController.AdvanceHost` observes poses, advances the canonical match and reconciles the next frame. This work does not yield between its internal validation/presentation/physics steps.
4. The actual failure stack at log lines 2354–2372 locates reliable-data delivery inside `Simulation.NetworkRecv → Simulation.Update → NetworkRunner.UpdateInternal → NetworkRunnerUpdaterDefault.InvokeUpdate`. Official documentation likewise places callback processing before the resimulation and forward loops, with normal Unity `Update` later. [Photon update loop](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/fusion-update-loop).

**Inference:** expensive synchronous tick work can postpone later receive-processing turns, regular updates, async continuations and other runners sharing this Unity main thread. The 6.278 s coroutine gap is compatible with that mechanism; it does not identify which runner/batch consumed it, packet arrival times, or the complete cause of the 17.972 s transfer span. Session polling also occurs inside the host tick path, so it would be wrong to claim that all lease checks wait for ordinary `Update`.

## What the profile does establish

The TeamSolo per-invocation profile at log lines 2399–2710 covers 79.823 s, all main-thread instances, without buffer wrap. It records 361 reconciliations totalling 17,192.870 ms (mean approximately 47.63 ms), including 361 validation scopes totalling 7,384.124 ms and 361 physics-application scopes totalling 7,884.974 ms. Advance has 360 samples totalling 6,591.941 ms. These are inclusive marker timings, **not additive wall time**, and include startup/pausing effects.

The observed mean reconciliation alone exceeds a 60 Hz tick's 16.67 ms interval. This supports prioritizing the measured hot path before a scheduling workaround. It does not prove that every tick takes that long or that every batch has 60 ticks. These measurements predate the new static-index benchmark/result being evaluated by the parent.

## Supported knobs and their limits

| Inspected API/config | Verified meaning | Decision for this failure |
| --- | --- | --- |
| `TickRateSelection.Client = 60` | The project requires actual 60 Hz; `PrepareScene` rejects a different `Runner.DeltaTime`. | It is a tick rate, not evidence of a 60-tick-per-update cap. Keep unchanged. |
| `SimulationUpdateTimeMode.UnscaledDeltaTime` | Installed XML documents the default elapsed-time source. `DeltaTime` is explicitly documented **only for `GameMode.Single`**. | Do not substitute scaled/clamped Unity time for multiplayer. |
| `ClientMaxResimsPerFrame` | Soft client resimulation/prediction bound; installed default -1 means unlimited. Optional input delay can reduce client prediction depth. | It does not document a host forward-catch-up cap. Do not treat it as the fix. [Photon configuration](https://doc.photonengine.com/fusion/v2/manual/network-project-config). |
| `TickAccumulator.AddTime(dt, step, maxTicks)` | Public primitive with an optional maximum; XML says reaching the maximum clears the remaining time. | No inspected public contract establishes the runner's caller/default value, a supported project-level cap, or that changing it preserves elapsed-time semantics. No SDK edit/reflection recommendation. |
| `INetworkRunnerUpdater` / default PlayerLoop registration | Supported customization of when runner update/render calls occur. | Moving a long call does not make it yield. Splitting/clamping elapsed time or updating repeatedly would be a separate scheduling experiment requiring a defined time/debt policy and network/physics acceptance; it is not justified as a production fix by current evidence. [Default updater API](https://doc-api.photonengine.com/en/fusion/current/class_fusion_1_1_network_runner_updater_default.html). |

The installed Fusion assembly XML search for catch-up, maximum ticks and ticks-per-frame found the accumulator and client resimulation contract, not a documented host forward-batch setting. This is a bounded documentation result, not a claim that no internal limit exists.

## Smallest discriminating next check

Keep the 10 s transfer limit, 15 s lease, 5 s renewal and gameplay tick rate unchanged. After the already-owned optimization is measured, run only `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.PublicTeamRacePreservesWholeSoloParties` with existing profiling and a small **test-only** scheduling observer:

- Register a project-owned `SimulationBehaviour` on each fixture runner using public `IBeforeAllTicks` and `IAfterAllTicks`. Both distinguish forward from resimulation loops and supply the batch tick count. Record monotonic start/end timestamps, frame number, batch count, total/max duration and maximum gap between runner callbacks. Keep host/client observations separate and do not print player/session identifiers. The public callback contract is documented in installed XML lines 1703–1717 and 1797–1810. [Before-all-ticks API](https://doc-api.photonengine.com/en/fusion/current/interface_fusion_1_1_i_before_all_ticks.html).
- Keep only scalar aggregates and a bounded tail of recent batches; emit them at failure/cleanup with capacity/overflow disclosure. Mark absent/incomplete callbacks unavailable. No per-tick logging, new waits, extra `Poll`, manual runner update, or input changes. Public `NetworkRunner.TicksExecuted` can corroborate counts after an update, but the before/after callbacks distinguish resimulation from forward work better than a once-per-frame sample.
- Relate the observed largest batch and peer-service gaps to the already-recorded transfer elapsed time and failure stack. A measured large forward batch followed by the client's receive callback supports scheduling starvation. Small batches with large non-simulation gaps instead direct the next check to scene/setup work, other Editor work or network delivery; the observer does not timestamp socket arrivals.
- If a large shared-process gap persists after CPU optimization, compare the same exact-content public flow with host/client in separate Windows processes once the delivery build is available. That separates single-Editor multi-peer contention from a real one-peer client workload. It cannot excuse an over-budget host or replace the existing native tests.

No new test observer has been implemented or executed in this report. The next recoverable action is parent authorization/integration of that bounded observer, followed by one serial native case. There is no evidence-based deadline increase recommended.

## Authorized probe follow-up

After report commit `260a02b`, the parent authorized the bounded test observer. It is implemented only in new `Assets/_Project/Tests/OnlineGameplay/WordboundNativeTickBatchObserver.cs` and four integration lines in `WordboundNativeGameplayTests.cs`. The latter's pre-edit SHA-256 exactly matched root: `083C652CC741C55B8BEA05E4054563E94A28009A90A5F0C608515FEC7617D002`; no baseline adoption is needed.

The existing `WORDBOUND_PROFILE_NATIVE=1` gate enables it after entering real Play Mode. A component is attached to each fixture-created Runner before `StartGame`; the installed `SimulationBehaviour` XML explicitly documents automatic registration when present during runner initialization. Existing test assembly references already include Fusion.Runtime, NUnit and Unity. No assembly, production or SDK edits are needed.

The observer records separate completed forward/resimulation totals and maxima, the count/frame/time of the worst complete batch, a fixed 16-completion tail, pairing/invalid-count errors, any unfinished batch, normal Unity-update gaps and `TicksExecuted` corroboration. Durations use `Stopwatch` wall time and include interruptions; they are not CPU time. Callback gaps are not packet arrival/service timestamps. Completed aggregate totals retain overwritten tail samples; overwritten counts are explicit. Absent observations remain unavailable. Managed records survive destruction of a failed Runner and are frozen/reported at teardown before cancellation. Callbacks only update scalar/struct fields; strings and logs are created during reporting.

`git diff --check` PASS. Independent source review requested from the assets lane. Compilation and native execution NOT RUN; the parent owns both. No waits, deadlines, physics/input operations or production settings change. The next action is source review, integration and the single TeamSolo native case with profiling enabled; successful registration requires actual nonzero, well-paired callback evidence in that result.

Source checkpoint `6132c2e` received independent bounded source **PASS** from `/root/recover_assets`: installed callback/registration contract, paired wall-time/count aggregation, tail/unfinished disclosure, destroyed-Runner retention and unchanged gameplay/configuration/deadlines were checked. No source changed after that checkpoint. Actual callback delivery, compilation and native clock values remain NOT RUN. Next: parent integrates `260a02b` and `6132c2e`, performs its serial compile and one TeamSolo native check, then assesses measured batch size/cost before any scheduling intervention.

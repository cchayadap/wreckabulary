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

## Native adapter failure and correction

Root run `Logs/gate_test_editmode_20261023T163048Z.log:587` failed before the first Runner started: Unity rejected attaching the observer because its test assembly is Editor-only, and the resulting null component caused setup to fail. **No tick scheduling evidence was collected.** The earlier source review did not catch this Unity assembly attachment restriction.

The bounded repair moves only the `SimulationBehaviour` callback adapter to `Scripts/Online/Gameplay/WordboundNativeTickBatchAdapter.cs`, in the existing runtime gameplay assembly, with the whole file guarded by `UNITY_EDITOR`. Public `Action` callbacks forward the same before/after/normal-update/destruction notifications to the retained Editor-test aggregate. It has no NUnit reference, no configuration logic and no player-build type. The test uses its existing direct gameplay assembly reference and asserts successful attachment before binding the callbacks. All timing/aggregation/control-flow behavior is retained. The test helper's filename and root-generated `.meta` are preserved; no assembly platform or SDK edits.

Source/diff review only; corrected compilation, attachment and callback delivery await the parent's native rerun. A missing/failed probe cannot be interpreted as a passing scheduling result.

Root integrated the adapter as `90ac700`; R0 then caught a new NUnit/Fusion `Assert` ambiguity in the Editor aggregate. Root `c40fe19` qualifies `NUnit.Framework.Assert.IsNotNull`, and that fix must be preserved by later helper edits. The same root commit repairs a separate private authored-fixture preparation failure by awaiting the status-bearing party refresh before reading its assignment, and adds at most four safe method/type stack origins to failed task diagnostics. Independent bounded source review is PASS; detailed evidence is in [NATIVE-WORKSHOP-SETUP-REVIEW.md](NATIVE-WORKSHOP-SETUP-REVIEW.md), report-only commit `c98e8e3`. The corrected native batch was running at review time; no attachment or network success is inferred before its completed result.

## Completed corrected native run — 20261023T163900Z

Independently read root `TestResults/editmode_20261023T163900Z.xml`, `Logs/gate_test_editmode_20261023T163900Z.log` and `.harness/delivery/native-team-private-repaired-probes-stdout.log`. The tested source was root `c40fe19`. XML: **2 total, 1 PASS, 1 FAIL, 0 skipped**, 149.0402821 s, 16:41:22–16:43:51 UTC. The cold gate remains **FAIL**, 302.1 s including 118.4 s refresh; its only test failure is public TeamRace. This is not a successful delivery gate.

| Case | Completed result | Narrow evidence |
| --- | --- | --- |
| `PublicTeamRacePreservesWholeSoloParties` | FAIL, 82.230695 s | Exact approved public allocation/whole-party certificate observed; scene/durable-start readiness failed on a stale snapshot before client gameplay readiness. |
| `TwoAuthenticatedPeersDownloadAndPlayExactAuthoredWorkshopMap` | PASS, 66.573732 s | Two authenticated scripted clients independently downloaded matching authored content and composed actual bodies; client movement, command/deduplication, durable snapshot acknowledgement and fixture cleanup passed. |

### Public failure and measured tick batches

Log line 1080 reports transfer elapsed **21,052 ms** against **10,000 ms**, with same mark/count/length/wire hash all 1 and duplicate chunk 0. This identifies the stale predicate again, without attributing all elapsed time to the network. Readiness ended after **46.139 s**, max observed coroutine gap **7.068 s**; host canonical tick/advance count were 360, host was paused, client had no canonical frame and reported `FormatException`.

The adapter now actually attached and delivered native callbacks. Both peer records have no pending batch, unmatched begin/end, pairing mismatch or invalid tick count. The two-runner observer window is 74,064.160 ms. Aggregates cover every completed callback; tails retain only 16 completions and disclose 550 host / 406 client overwritten samples.

- Host: **566 forward batches, 1,908 Fusion forward ticks**, maximum **60 ticks**; worst complete batch was **60 ticks / 6,482.646 ms**, frame 26190, observer offsets 34,719.048→41,201.694 ms. No resimulation batch was observed. `TicksExecuted` also reached 60 at a normal Unity update.
- Client: 222 forward batches / 339 ticks (maximum 41) and 200 resimulation batches / 5,385 ticks (maximum 33). Maximum forward duration was 21.082 ms for an 11-tick batch; maximum resimulation duration 8.170 ms for a 23-tick batch. The largest **batch-start callback gap** was **21,264.360 ms**, from the retained forward callback at 32,917.856 ms to the next at 54,182.216 ms.
- The client gap overlaps host forward batches lasting 6,482.646, 5,711.140 and 5,378.924 ms on consecutive frames, followed by two shorter batches. Normal Unity-update gaps also reached approximately 8,838.8 ms for both peers.

This establishes that large, expensive synchronous forward batches really occur in this setup, rather than only being a theory from tick-rate configuration. It strengthens the shared-main-thread scheduling explanation. It still does **not** prove a universal SDK cap of 60, timestamp socket arrivals, or establish that `NetworkRecv` was absent throughout the client callback gap; callbacks occur only when that runner performs ticks. These are Stopwatch **wall** durations, not CPU samples. The 1,908 Fusion ticks span pre-gameplay and paused periods and must not be equated to the 360 canonical advances. Existing protocol deadlines remain unchanged.

### Private authored-map pass and limits

Private operation durations in log lines 1975–4119 were: readiness **7.503 s** (max gap 1.304 s), exact body replication **0.004 s**, admitted client movement **2.614 s** (max gap 0.370 s), host command application **1.104 s** (max gap 0.587 s), and exact committed snapshot acknowledgement **11.163 s** (max gap 1.870 s). The authored-transfer marker at line 4140 follows assertions for independently constructed content, exact document/manifest/catalogue/rule binding and canonical world composition. No private session failure was recorded.

At the **last durable-ack observation**, the host was nevertheless `paused=True`, `canMove=False`, tick 438, with running/ready/committed state true and no failure. The client was `paused=False`, tick 300, with running/ready/committed state true and no failure. The case does not require both peers to remain unpaused after this final acknowledgement, so its PASS must not be described as a sustained smooth-playing acceptance or a proven recovered final host state. Pause cause was not captured by this diagnostic.

Private observer maximum host batch was also 60 ticks, but **1,860.718 ms**; maximum client batch-start gap was **4,873.477 ms**. Host and client pairing counters were clean. This private Freestyle authored workload is a useful successful comparison, not an equivalent public TeamRace workload or a 60 Hz performance pass.

The private XML PASS includes `UnityTearDown`: controller stops, pending task completion, service match-return/queue-cancel/party-leave cleanup, and the existing zero-failure/no-proof-owned-object-leak assertions. The intermediate transfer/gameplay markers correctly say cleanup pending; final XML supplies the completed verdict. This is fixture-owned cleanup, not a claim that all remote historical records or every Unity allocation disappeared.

### Preserved infrastructure caveat and next action

Startup log lines 544–558 retain Burst's **60,000 ms compiler command-queue timeout**. The gate also retains those messages and `crashAfterQuit=false`; the tests did execute to final XML. Later shutdown helper/Mono warnings remain in the raw log. None is proven to cause the snapshot failure, and none is erased or relabelled as a clean startup.

Next: parent measures the approved resource-lookup optimization and reruns the exact public case with the same batch/protocol diagnostics. Compare content identity, native marker costs, maximum host batch duration, callback gaps, readiness and final paused state before any scheduling or deadline intervention. The private authored acceptance is now one real narrow PASS; public TeamRace and production-ready Windows acceptance remain incomplete.

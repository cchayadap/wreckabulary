# Windows workshop slowdown — simulation audit

STATUS: SOURCE REVIEW COMPLETE; root's plain confirmation run remains separate. Read-only source/evidence review. No native run, source mutation, service call or timeout relaxation by this lane.

Owner: `/root/asset_intake`. Target: source `3cec5407`, Windows build `20261026T122831Z`, failed workshop smoke `20261026T123303Z-wordbound_workshop_flow`.

Investigate the accumulator/tick, physics and snapshot path against actual frame/tick timestamps and recent commits. Root reports 55 updates over about 91 seconds, while the frontend runs at about 129 FPS. Preserve the historical comparison boundary: earlier 90 updates / 7.7 seconds used source `9e013dc9` and may differ in workload.

## Confirmed workload and outcome comparison

Read actual `checks.json`, player logs and saved workshop payloads for these three runs. All three have exactly the same manifest, seed, saved payload bytes and SHA256:

- Manifest: `146a289906d1cf3840a9985ce0a38e4ae968889b9e1cb1b60808333ed519e77f`.
- Seed: `0314159265358979`.
- Autosave: 183314 bytes, SHA256 `a97b13dedfa47126279d290b963cbf618e3e90e45502e0720fb6c77e90659678`.
- All three report 93 visuals, one marker, an active camera and `Time.timeScale = 1`. No gameplay, guide or presentation fault is reported.

| Run | Build | Outcome | Ready frame / tick | Final frame / tick | Post-ready updates | Measured observation window | Focus at reported playtest state |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `20261024T035028Z-wordbound_workshop_flow` | `20261024T034544Z` | PASS | 14 / 44 | 107 / 572 | 90 | 7.694559 s | true |
| `20261026T123303Z-wordbound_workshop_flow` | `20261026T122831Z` | FAIL: exact-playtest 90 s | 14 / 59 | 70 / 3419 | 55 | No completed observation; return observation at 91.736082 s from playtest start | false |
| `20261026T123805Z-wordbound_workshop_flow` | `20261026T122831Z` | PASS | 14 / 34 | 107 / 666 | 90 | 9.537622 s | true; zero unfocused updates in measured window |

The historical source was supplied as `9e013dc9`, current source as `3cec5407`; run receipts themselves have null commit/tree fields, so those source identities come from build provenance/root, not these receipts. The current pass and failure use the same recorded build ID. Root states no source edits between them and enabled existing diagnostics for the successful rerun. That rerun therefore disproves a consistently failing source/workload combination. Diagnostics and focus differ, so it is not a controlled experiment isolating focus alone.

Failure timestamps from the player log:

| Frame | Canonical tick | Elapsed from playtest start |
| --- | --- | --- |
| 14 | 59 | 5.646 s |
| 29 | 959 | 28.587 s |
| 44 | 1859 | 43.390 s |
| 59 | 2759 | 73.009 s |
| 70 | 3419 | 91.736 s |

Each adjacent sampled interval averages exactly 60 canonical ticks per rendered-frame increment: 900/15, 900/15, 900/15 and 660/11. This is an observed aggregate ratio, not per-update instrumentation proving every individual update had exactly 60 ticks. It is consistent with catch-up batching under long frame gaps. It does not prove a documented Fusion cap or identify why those gaps began. The failed run did not record the successful run's detailed frame/phase diagnostics.

## Simulation, physics and snapshot path

The current source path is coherent and unchanged in its scheduling behavior since `9e013dc9`:

1. `Online/Fusion/WordboundFusionConfiguration.cs:15` creates the local runner configuration. Lines 22–24 explicitly select a 60 Hz client/server tick rate. `WordboundLocalSession` starts `GameMode.Single`; `Poll` validates the local seat and readiness, not a bespoke accumulator loop. Local checkpoints publish/validate/acknowledge in process, not through the remote coordinator.
2. `Online/Gameplay/WordboundGameplayController.cs` checks the runner's 1/60 delta and isolated physics scene, attaches `RunnerSimulatePhysics`, and subscribes `WordboundPhysicsWorld.HostStepCompleted` to `AdvanceHost`.
3. `Online/Physics/WordboundPhysicsWorld.cs:480` handles `AfterSimulate`: capture current bodies and invoke `HostStepCompleted`. Capture covers the whole body ledger (`CaptureHost`, line 215). No independent catch-up accumulator exists here.
4. `Online/Gameplay/WordboundGameplayController.cs:257` advances canonical state once for an admitted host physics step. It invokes observation/evaluation, then `Reconcile(CaptureFrame())` at line 281. `CaptureFrame` obtains a full match snapshot at line 288.
5. Reconciliation constructs/validates the complete snapshot and configures each body's presentation (`line 377`), then invokes `FrameChanged` synchronously at line 403. Catch-up ticks repeat this work before the next rendered update. This is a concrete amplification mechanism; it was already present in the historical source.

`git diff 9e013dc9..3cec5407 --stat` for the Fusion, Gameplay, Physics and Rules script roots plus `NetworkProjectConfig.fusion` reports only three added lines in `WordboundGameplayController.cs`: the `IsPresentationReady` property. There is no accumulator, tick rate, physics callback, rules, checkpoint or snapshot change in those paths. The editor-only native tick batch adapter is behind `#if UNITY_EDITOR` and cannot itself be the Windows player trigger.

The inspected presentation additions do not establish a repeated expensive spawn/rebuild:

- `WordboundGameplayPresentation.cs:143` returns from visual refresh when parent, key and size match. `ApplyAppearance` at line 185 returns when appearance is unchanged. `ApplyMelee` returns immediately for non-avatar views; it observes state, while animation graph evaluation remains in render-time presentation.
- `WordboundGameplayBody.Configure` updates networked shape/key/carrier each canonical step, but `Apply` caches presentation validation and only changes shape/visuals when relevant values change. This behavior is unchanged.
- New `AvatarSurfaceProfile` correction is called from model spawn/appearance initialization. It is not an unconditional per-tick mesh rebuild or disk fingerprint calculation. The string fingerprint is provenance data; runtime validation checks references/counts and the bound head.
- Story view retains its signature guard; unchanged story state does not recreate the journal/signs on every canonical tick.

These are source exclusions, not an exhaustive performance certificate for new artwork.

The inspected focus callback (`Online/Client/WordboundPlayerView.cs:246`) opens the menu and requests its presentation when focus is lost; it does not directly change the tick rate or `Time.timeScale`. All three receipts show the menu open at the final playtest observation, so menu-open alone does not distinguish the failure. No claim about Windows/GPU background throttling is established from these files.

## What the successful profiled rerun actually measures

`Logs/player/20261026T123805Z-wordbound_workshop_flow.log:69` records 90 Unity updates / 9.538 s, canonical delta 606, maximum 35 ticks/update, no missing snapshots, no epoch changes, no tick regressions and zero unfocused updates. Mean update gap is 105.940 ms; maximum 528.926 ms. This is still low update throughput and should not be represented as smooth gameplay or presented FPS.

The longer ready-phase window includes captures after the 90-update observation. It spans tick 34 to 666: 632 host steps over 10.4697652 s, with 93 bodies, 294 resources and 216 mechanisms throughout. Its inclusive wall-clock samples include:

| Phase | Count | Inclusive elapsed |
| --- | --- | --- |
| HostStep | 632 | 6247.953 ms |
| FullValidation | 659 | 1925.212 ms |
| Synchronize | 632 | 920.297 ms |
| ObserveHost | 632 | 655.535 ms |
| Prepare | 632 | 631.421 ms |
| FrameChanged | 632 | 80.576 ms |

HostStep averages about 9.89 ms/step in this run. The snapshot/host path is a material cost even in the passing case; the much smaller FrameChanged total does not support blaming the new story/appearance subscribers for this run's main cost. These scopes overlap and are inclusive elapsed time, not CPU-exclusive samples. Do not add them together, treat them as GPU time, or transfer their exact costs to the failed run.

The successful camera callback window was 275.291 ms total across 89 paired callbacks. The log explicitly labels this callback wall time, not GPU/present time. Frontend FPS and free-memory observations from root are different workloads and cannot certify physics or map-render throughput.

## Conclusion and recoverable next step

No concrete source regression was identified. The strongest evidence is identical workload and build changing from timeout to pass, with a visible focus difference and diagnostic-mode difference. Focus is a credible condition to control in root's plain confirmation, not yet a proven cause. The measured per-tick host/snapshot work can amplify long-frame catch-up, but changing that logic without controlled evidence would be speculative.

Preserve the original FAIL and the profiled PASS. Root is running a plain workshop confirmation with unchanged timeout/build and observing focus. If failure recurs, capture the existing frame/phase/native/checkpoint diagnostics in that failing condition before proposing scheduling changes. Do not weaken the 90 s guard, bypass validation, reduce canonical tick rate, or claim production performance from these smoke results.

Files changed by this lane for this audit: this report only. No native processes started or stopped by this lane.

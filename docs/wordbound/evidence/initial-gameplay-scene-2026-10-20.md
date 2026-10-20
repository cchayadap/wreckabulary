# Initial gameplay scene repair

STATUS: PASS — initial scene and authenticated startup; broader gameplay remains in progress

The real two-peer case in `TestResults/editmode_20261020T144759Z.xml` failed at Fusion startup. Installed Fusion 2.1.3 source (`Assets/Photon/Fusion/Runtime/NetworkSceneManagerDefault.cs`, Initialize and LoadSceneCoroutine) requires an initial scene in Multiple mode and creates the Runner's isolated physics scene itself. Explicit `LocalPhysicsMode.Physics3D` on a loaded scene is rejected in this mode.

`WordboundFusionSession.ConfigureScene` now records an additive initial scene before startup and passes it to every Runner, including replacement Runners. Multiple mode refuses to start without it before making network requests. `WordboundGameplayController.AttachSession` supplies the selected gameplay scene; scene preparation waits for that scene and its isolated physics world instead of requesting a second load. Installed SDK files are unchanged.

Source lifecycle regression: **31/31 PASS**, 7.05 seconds, using the existing `BuildNotes/online/FusionCompile/SessionLifetimeTests.csproj` and its explicitly identified SDK doubles. Three new cases cover missing/invalid scenes and the same configured scene reaching initial and replacement Runner startup. This does not prove real scene loading.

The initial `dotnet run` invocation incorrectly forwarded `-m:1` as the test-name filter and matched no tests. The built assembly was then run explicitly with `dotnet BuildNotes/online/FusionCompile/bin/Debug/net9.0/SessionLifetimeTests.dll SessionLifetimeTests`, producing the 31/31 result above.

Aggregate runtime/editor R0 passes in 82.8 seconds (`Logs/gate_check_all_20261020T150340Z.log`). This includes the newly integrated workshop source and interaction fixture. No engine-free production rules changed after the last 228/228 core pass. Native loading and gameplay retry remain pending.

Native retry completed in 277.1 seconds (`Logs/gate_test_editmode_20261020T150614Z.log`, `TestResults/editmode_20261020T150614Z.xml`): 24/25 passed. All selected workshop tests passed, including exact-draft dispatch and invalid-draft refusal. The host loaded `WordboundPhotonProof.unity` without either previous scene error. The two-peer case then failed during client startup with OperationCanceled and cleanup disconnect logs. That is a new unresolved failure, not gameplay acceptance.

The fixture now records controller/session failure callbacks and checks health while waiting for startup. Previously its task wait allowed a composition fault to be hidden by later SDK cancellation logs. These diagnostics retain unexpected-error assertions and require clean teardown; no SDK logging is suppressed. First-cause rerun is pending.

Independent installed-SDK inspection found that `NetworkRunner.IsRunning` reports a live simulation before awaited client startup finishes, while `DeltaTime` uses runtime tick configuration. The controller was applying its exact-60-Hz requirement during that interval. Session now exposes a per-Runner `RunnerStarted` barrier only after successful SDK startup and authenticated identity checks. Controller scene preparation waits for it. Replacement and stopped Runners clear the barrier through existing run invalidation.

Updated source lifecycle suite: **33/33 PASS**, 7.80 seconds, build 20.13 seconds with zero warnings/errors. The new tests hold SDK startup pending while its `IsRunning` is true, verify the barrier remains closed, and verify successful completion, replacement, stop and failed startup. This repairs a concrete readiness-contract defect; the next native run still must establish whether it resolves the observed cancellation.

Native verification at source commit `7d9d122` passes **6/6** in 234.4 seconds (`Logs/gate_test_editmode_20261020T152312Z.log`, `TestResults/editmode_20261020T152312Z.xml`). The actual two-peer case authenticates both players, constructs separate physics scenes and canonical network bodies, applies client movement and a repeated command exactly once, receives and acknowledges the exact committed snapshot, and tears down cleanly. The other five cases check thrown-body collision settings and bounded contact buffering. This resolves the observed startup cancellation. The editor reported a Burst compiler queue timeout during refresh; it did not fail or skip the tests, whose XML records six passes and zero failures.

The preceding aggregate R0 passes in 20.7 seconds (`Logs/gate_check_all_20261020T152047Z.log`); 259 affected core tests pass in 33.5 seconds (`TestResults/core_20261020T152147Z.trx`). Four/eight-peer gameplay, detailed interactions, complete host recovery and player-build acceptance remain separate outstanding checks.

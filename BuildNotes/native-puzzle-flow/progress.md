# Native six-family puzzle flow

STATUS: SOURCE VERIFIED AGAINST INSTALLED SDK; NATIVE EXECUTION PENDING

Root approved a new isolated test assembly under `Assets/_Project/Tests/OfflineGameplay/PhysicalPuzzles`. This lane owns only new fixture files and this evidence directory. Existing session, gameplay, physical policy, scenes, prefabs, item data and production settings remain outside its write scope.

The fixture uses the actual flagship TimeRun source, exact resolved manifest, real item/rule bytes, existing native body prefabs, a Single runner and `WordboundPuzzleRuntime` callbacks. It reaches the six first-destination objectives in route order: CollectLetters, PlaceObject, RedirectWind, TimedMechanism, BuildRoute and BalanceWeight.

Fixture setup may reposition the host player Rigidbody between rooms and beside supplies. Those moves are recorded explicitly and do not prove human navigation. Craft, attack, deploy, use, recovery, physical observations and checkpoint publication use normal authoritative paths. No progress event, solved state, physical evidence, damage amount or synthetic mechanism completion is supplied by the test.

Positive evidence includes the marked prop's actual destruction and released letters, wrong-socket rejection, native wind displacement, CLOCK's effect on a real canonical sorting arm, a BED-induced upward launch with movement into the arrival volume, and coverage of both weight switches. Each completion must survive canonical checkpoint import. Scene-registration changes are SessionState-backed and restored in teardown even after assertions fail.

Next action: finish source and inspect it without launching a compiler. Root owns the current build/native window and will grant the next reference-compilation slot. Native execution remains root-owned; source authoring is not an acceptance result.

Source checkpoint: the two-part fixture and explicit test asmdef are drafted. The online owner completed a bounded read-only review with no confirmed lifecycle or setup false-pass. Native-only risks to retain in diagnostics are actual collider support after the small setup height offset, reaching UsePuzzle while physical completion remains valid, and exact checkpoint freshness. The offline session currently publishes synchronously; the fixture still requires the committed tick to include each solve.

The test is `WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints`, category `LocalWordboundPhysicalPuzzles`, opt-in environment `WORDBOUND_NATIVE_PUZZLES=1`. It requires the existing root-generated Player/Item/Platform body prefabs and `WordboundPhotonProof.unity`. It uses a six-minute cancellation budget after entering Play Mode and restores the previous build-scene registration through SessionState.

An ignored local `ReferenceCheck.csproj` is prepared to compile only these new test sources against installed Unity/Fusion and integration production DLLs, with warnings as errors. It has not been run. No production source or native setting has been edited.

## Reference-verified checkpoint

The new test sources compiled as their own assembly against installed Unity 6000.6.3f1, Fusion and the current integration production DLLs: PASS, zero warnings/errors, 8.29 seconds. The initial compile found one missing `Wreckabulary.Online` namespace import in the action partial; that source-only correction is included. The test assembly SHA-256 is `65076A6E5013DF6230E23614F5F30F446D5417F07059D65B0627AC8DDAC7084C`. No production source was linked into the test compilation, so the check preserves the native assembly access boundary.

Native test filter: `Wreckabulary.OfflineGameplayTests.WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints`. Category: `LocalWordboundPhysicalPuzzles`. Required environment: `WORDBOUND_NATIVE_PUZZLES=1`.

The fixture checks six families through actual authoritative commands and the real physical evaluator, including negative wrong-socket/waiting checks, HAMMER destruction of authored furniture, released letters and craft completion, native wind displacement, canonical CLOCK fixture phase slowing, BED launch, and both weight pads. It then imports a fresh canonical checkpoint after every solve. Host-only setup moves are explicit and counted; this is not evidence of human traversal, race duration, a full three-lap finish, online migration or performance.

Compiler slot released to the online owner's one native-fixture reference build. Next recoverable action: root integrates this additive checkpoint, lets Unity create metadata, and runs the opt-in test with the existing native scene/prefabs. No Unity process was launched in this lane; runtime result is NOT RUN.

## First native attempt and setup-observation repair

Root ran the opt-in fixture with the 16 affected GameplayPlan cases. Native result: 16/17 PASS; all GameplayPlan cases passed, while the six-family flow stopped at its first TakeKeepsake receipt with `OutOfReach`. Root evidence: `TestResults/editmode_20261020T175216Z.xml`, `Logs/gate_test_editmode_20261020T175216Z.log`; process exited cleanly. No family completion was reached or proved.

The failure output did not capture actor/keepsake coordinates or LOS, so range versus occlusion cannot yet be determined. Source inspection confirmed that the fixture's setup wait checked only a boolean and any tick later than the request, without asserting that the requested pose had reached canonical state. Setup now records the actual application tick and requires a later captured canonical pose near the requested position and agreement with the live Rigidbody. The same condition covers the explicit wind-receiver setup. The bounded tolerance permits one physics step of gravity/launch, without accepting a remote stale pose.

Before taking the keepsake, the fixture allows three real startup physics ticks, places the player's feet slightly above the target floor height, then requires actual canonical range, ownership/activity and runner-local LOS. Receipt failures include before/after canonical and engine positions, distance, velocity and native raycast collider hits. Production interaction checks and expected `Ok` receipts remain unchanged.

This follow-up is SOURCE ONLY: reference compilation and native rerun NOT RUN while root owns the Windows QA build/compiler slot. Next action: root compiles this narrow fixture update and reruns the opt-in flow; the additional diagnostics must distinguish a setup race from a real geometry/query problem rather than assume the repair proves playability.

## Completed-frame observation correction

Read-only follow-up identified a transient polling problem in the previous setup repair: catalogue BED launches at 11 m/s, so after three 60 Hz steps its position can exceed the 0.4 m vertical setup tolerance before a render-frame coroutine resumes. The fixture now observes setup in its already-subscribed `FrameChanged` callback, after controller reconciliation has applied the exact canonical snapshot to native bodies. Each request records its application epoch/tick and latches only the immediately following completed tick. Missing that tick, changing epoch, or capturing a mismatched pose records a test failure instead of accepting a later descent near the old position.

Upward launch evidence is likewise latched from a completed frame that simultaneously has real upward actor velocity and matching puzzle/site/item/actor mechanism evidence. Arrival-volume evidence is recorded from actual completed-frame actor positions and keyed by the exact site hash, preventing render-frame catch-up from hiding a genuine physical crossing. Current command range/LOS and all expected receipts remain unchanged.

Lifecycle inspection: only one setup observation can be pending; queued setup closures ignore invalidated requests; iterator `finally` blocks clear setup/launch requests; teardown clears pending callbacks/data and unsubscribes `FrameChanged` before stopping/destroying the controller. Frame callbacks record evidence or failure data without throwing NUnit assertions through production simulation. Assertions remain in the fixture coroutine. Repeated same-tick notifications cannot fulfill a setup request, and an old epoch cannot fulfill a new one.

Verification for this follow-up: source/lifecycle inspection and `git diff --check` only. No compiler or Unity run; the online lane owns the compiler slot. Root must compile/import and rerun the physical fixture before any runtime acceptance claim.
# Root native follow-up — 2026-10-20T18:49:06Z

The combined native run is FAIL: 17 catalogue tests passed; the physical fixture exceeded NUnit's default180000ms timeout before recording a completed mechanism. Evidence: `TestResults/editmode_20261020T184007Z.xml`, total538.3s including197s refresh. Its native case ran18:43:51–18:48:24 with only Fusion player-add output. This is not physical interaction acceptance and does not identify a gameplay root cause.

Root added an explicit600000ms test-runner ceiling around the existing six-minute cancellation and bounded per-operation waits, plus coarse stage diagnostics to locate startup/interaction delays. Neither puzzle conditions nor production timing changed. Rerun pending.

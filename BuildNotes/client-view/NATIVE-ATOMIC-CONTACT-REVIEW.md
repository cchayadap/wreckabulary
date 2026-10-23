# Native atomic, contact and physical-flow review

STATUS: COMPLETED RESULT REVIEW — overall FAIL, 18/19 passed, 0 skipped. Read root `TestResults/editmode_20261023T174133Z.xml`, matching `Logs/gate_test_editmode_20261023T174133Z.log`, `.harness/delivery/native-local-atomic-contact-stdout.log`, and the corresponding assertions. No heavy jobs or production changes by this reviewer.

The gate lasted 301 s; the XML test run lasted 243.7705042 s. The single failure is a real failed assertion, not a skipped or empty filter. The gate reports no infrastructure failure or crash after quit.

| Group | Result | Established scope |
| --- | --- | --- |
| `WordboundNativeLocalGameplayTests` | 8/8 pass | Two actual offline Fusion/local workshop cases plus six atomic/callback lifetime cases. No Photon Cloud or remote peers. |
| `WordboundNativeWindPhysicsTests` | 2/2 pass | Actual assigned prefab material and temporary candidate, with eight total isolated native solver workloads. |
| Body motion and physics policy | 8/8 pass | One thrown CCD/freeze-resume native Rigidbody check, six economy-state collision/simulation flag cases, and one TeamRace/Brawl interference policy case. Policy assertions are not live multiplayer sessions. |
| Six authored physical families | 0/1 pass | First three families progressed; the CLOCK crossing failed. Full route acceptance remains open. |

## Atomic and local acceptance

Both `TimeRunUsesOfflineRunnerAndExactWorkshopManifest` and `FreestyleUsesOfflineRunnerAndExactWorkshopManifest` passed their actual local movement/jump, exact manifest and validated in-memory checkpoint checks. The six additional passing methods are `AtomicPhysicsApplyRetainsValidationAndAbortGuards`, `PresentationStopCannotPublishAnAbandonedReconciliation`, `PresentationFailureKeepsItsExceptionAndCannotPublishAFrame`, `PresentationPollCannotReenterInitialHostComposition`, `FirstAppliedFrameCannotReenterInitialHostComposition`, and `RunningFrameStopPreventsCheckpointAndLocalViewContinuation`.

These exercise actual native pose preservation on invalid/aborted/throwing/reentrant apply, subsequent valid application, disposal, initial composition reentry, and stop during publication. The running-frame case checks no later checkpoint clock read, frame/advance or active local view. Their passing XML includes teardown; no teardown failure is recorded. This establishes the bounded native behavior previously marked source-only, not every possible callback or production network load.

## Item contact acceptance

The actual-prefab method passed asset-path/shared-material identity and settings checks, player/platform null-material preservation, native receiver serialization checks, unchanged course surface contacts, arrival during wind, and natural stop without non-floor proximity. The fixture uses the real prefab's serialized Rigidbody/BoxCollider data on a bare receiver, not a spawned network entity.

Actual prefab wind arrived at tick 43 while pushed, at Y 15.9999886 m: the recorded lower-floor gap was 0.011444091796875 mm, within the shared 1 mm rule. The field exited at tick 75; final velocity, final-second maximum speed/drift and non-floor proximity were zero. The 3 m/s no-wind control moved about 0.9022 m and stopped. The historical null-material control received 300 pushes but never arrived (maximum travel about 0.01390 m). The separate zero-friction control arrived at tick 27; its later motion is not used as the natural-stop acceptance. Candidate results also passed. This closes actual-prefab isolated contact acceptance, not general network or Windows equipment acceptance.

## Exact remaining physical failure

`SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints` failed after 223.479464 s at `WordboundNativePuzzleActions.cs:329`: **Cross slowed native sorting arm with the keepsake — expected Complete, observed Waiting**.

- `CollectLetters` completed at tick 2160, `PlaceObject` at 3480 and `RedirectWind` at 4380. Each was followed by `SaveAndCheck` and the next site's approach, so their local checkpoint publish/import assertions returned successfully. The FAN now progressed through the real authored runtime in this route.
- CLOCK was crafted at tick 5160, fixture elapsed 207.939 s, with lifetime not cancelled, running/ready session and simulation not paused at that observation. Before the failed crossing, the fixture passed the actual slowed-motion count, enabled/kinematic sorting-arm and canonical arm-position assertions, then constructed its collision-avoidance waypoints.
- The failure is before CLOCK's successful UsePuzzle/checkpoint and before `BuildRoute` or `BalanceWeight`. No `WORDBOUND_SIX_FAMILIES_OBSERVED` marker exists. The fixture is already a setup-assisted first-six-families check, not human traversal or a complete three-lap game.
- `MoveIntoArrival` uses the unchanged 8 s wall-time budget and only reports final mechanism stage. It does not report final player/waypoint, arm phase, field/arrival/support status or active-window reset reason. Therefore this artifact cannot distinguish a navigation problem from a physical-condition reset. It is not evidence to increase a deadline, bypass the arm or mark the family complete. The total case duration is below the six-minute lifetime; no lifetime-cancellation failure is recorded.

Shared the exact trace and missing end-state facts with Assets, which owns diagnosis and any bounded repair. The smallest next check is failure-only crossing facts through the actual path, retaining the existing arm, keepsake, sensor, movement and timing assertions, followed by the same six-family test. Public online mode coverage, Windows smoke/rendering and sustained player performance remain independent gates.

Profiling recorded 5,700 Advance and 5,701 Reconcile samples without buffer wrap over 102 Unity frames. These are invocation observations, not a frame-rate acceptance. Inclusive scopes must not be added as wall time; atomic integration changed the PhysicsApply scope and retired the separate Validate scope, so their old/new totals are not equivalent work measurements.

Artifact SHA-256: XML `36706EAE220548640789B548F6457A9E8B54592D6AB88D1DE9AE46DFFA30A1BE`; log `A168DBF9B8B449AEC9D78AD983EED565039B2E593C7827C5D3F963252FD39C77`.
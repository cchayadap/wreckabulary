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

## CLOCK diagnostic follow-up — completed 181114Z run

Read `TestResults/editmode_20261023T181114Z.xml`, its matching gate log, and current fixture/runtime source. This later batch is **0/2 passed, 2 failed, 0 skipped**, XML duration 316.033043 s. The physical case failed in 246.567229 s at `WordboundNativePuzzleActions.cs:471` with the same Complete/Waiting assertion. The other case, public TeamRace Solo, failed separately with a session FormatException in 69.095474 s; no online acceptance is implied here. No execution or source change by this reviewer.

The new facts establish a fixture feedback problem. The route started at tick 5400 and ended at 5640 after 8.988 s, still on waypoint 0. Its 240 advancing canonical callbacks had maximum tick gap 1 and maximum 60 callbacks per Unity frame; the coroutine sampled only four times. The actual canonical player position entered the unchanged 3 cm waypoint tolerance at tick 5426 and came within 0.002285 m at tick 5614. The coroutine never observed that entry. It kept reversing its stored desired movement around the first waypoint; the final actor position was `(38.348210, 0, 1.5)`, with negative X velocity. Later waypoints were never active, so their Infinity/-1 diagnostics are absence of observations, not impossible geometry.

`MoveIntoArrival` updates desired input once per `yield null`, whereas the real motor consumes `GetInput` in `FixedUpdateNetwork`. The first waypoint is demonstrably reachable through the existing motor and collision path. This failure does not justify a larger tolerance, longer deadline, repositioning during the crossing, or bypassing the arm. There were no raw or shared arrival samples, so the lower-floor arrival tolerance did not determine this outcome.

The CLOCK field has a separate unresolved signal. Only nine of the 240 adjacent observed steps had slowed motion. Armed/Waiting repeatedly alternated with motion increments 600/1000. For example, the mechanism armed at 5404 and reset at 5405, then armed at 5446 and reset at 5447. Those one-tick resets cannot be the 180-tick active-window expiry. `FamilyReady` explicitly resets when the current machinery step is not slowed; the observed normal-speed increments are sufficient to trigger that condition. This identifies the failing condition, not why the field failed to affect the arm.

At the end, the session/Runner were running and ready, simulation was unpaused, fixture lifetime was not cancelled, CLOCK was active/kinematic/deployed at `(39, 0, 3)`, and the keepsake was present in every route sample. Deployment was at 5281; end tick 5640 was 359 ticks later, below the item's 600-tick lifetime. The final support ray found the floor, but no arrival support sample existed. The ray is observational and is not proof of the runtime's complete `Supported` predicate.

Source tracing narrows the next observation: `MoveAuthoredFixtures` applies the 0.6 strength only when a current slow field passes `ClockAffects`. That checks collision policy, field containment against the actual fixture pose, and `ClearLine` from CLOCK to the arm's center of mass. `Fields` additionally checks crafted/deployed state, lifetime and body activity. The existing report has no per-call predicate outcomes or blocking hit classification, so it cannot choose range, occlusion, field availability or another cause. The query intentionally constructs a nonkinematic target for field containment; the arm's actual kinematic state is not itself a demonstrated rejection.

Recommended bounded next work, owned by the coordinator:

1. Repair the fixture's route feedback using observed canonical waypoint entry and current canonical pose, preserving the actual input/motor path and existing tolerances, time budget, arm and completion assertions. Verify the input sampling/consumption cadence: updating the stored input in `FrameChanged` alone does not establish that inputs already collected for a catch-up batch will change. Installed Fusion XML documents `GetInput` as the input for the current tick and `IBeforeTick`/`IAfterTick` as per-tick callbacks, but does not establish the relevant `OnInput` delivery ordering in this run.
2. Observe the actual pre-physics CLOCK field decision with bounded predicate flags and blocking-hit type/count at the failing site, then isolate the condition if needed. Preserve the active-window and field rules until that evidence identifies a product defect. Do not infer a solved mechanism from a repaired waypoint driver.
3. Repeat the same six-family case. CLOCK UsePuzzle/checkpoint, BuildRoute and BalanceWeight remain unaccepted; no six-family success marker exists. This result does not alter the earlier 18 passing atomic/contact/policy cases or establish Windows delivery.

Artifact SHA-256: XML `27D60FA7FF994899681BEEB2476E3C671D9EA5A84A5305F0E52617B51AB53A36`; log `CD7C8F02516E6D95630D11E5D57E6649D76E61A29272D56AFF9E7716617DA791`.

## Tick steering source checkpoint

Independently reviewed Assets `62385dd8`: bounded source PASS after correcting the added `using Fusion` / NUnit `Assert` ambiguity with an explicit alias. The existing editor-only runtime adapter adds `IBeforeTick`; the fixture limits writes to the current local Single/forward runner's existing typed input. It validates epoch and movement before `GetRawInputForPlayer(...).TrySet`, leaves the actual motor/collision/physics path intact, and checks the same input after the native physics step. Route feedback uses the current canonical pose while preserving the prior movement formula, waypoint/final tolerances, route deadline and completion/arrival assertions. Lifetime/epoch/error checks neutralize the input; route `finally` clears steering and teardown detaches the adapter.

This is explicitly scripted tick input, not normal hardware polling or a human-control acceptance test. Positive injection counts and equality counts are asserted; the actual callback delivery/order and successful traversal still require the coordinator's native run. The independent diagnostic observer implementation and its source/release checks are recorded in `CLOCK-DECISION-DIAGNOSTIC-DESIGN.md`; neither source review establishes the underlying CLOCK field cause.

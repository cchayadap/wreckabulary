# CLOCK native route investigation

Status: source investigation complete; bounded fixture diagnostics SOURCE PASS after independent input-lane review. Reviewing root `20dcf826` and native run `174133Z`; no new native/compiler/Unity job. Specific failure cause remains unproven; no runtime tuning proposed from Waiting alone.

The 19-case batch passed 18 cases: both actual-prefab/candidate wind cases, eight local/atomic cases and eight motion/policy cases. The six-family route failed after 223.479464 seconds at `WordboundNativePuzzleActions.MoveIntoArrival` line 329: “Cross slowed native sorting arm with the keepsake”, expected physical stage Complete but observed Waiting. The trace establishes CLOCK craft completion at tick 5160 and the preceding RedirectWind completion at tick 4380. It does not record the actor/clock/arm poses or each route waypoint at the failure, so a specific movement obstruction is not yet established.

## Measured scope

The flow completed CollectLetters at tick 2160, PlaceObject at 3480 and RedirectWind at 4380, then reached ConveyorTurn/CLOCK. Control flow returned through each prior canonical checkpoint import. CLOCK completion/UsePuzzle/save and the later BuildRoute/BED and BalanceWeight/SOFA families were **not reached**. This is not a six-family pass or a BED/throw pass.

CLOCK crafting began at tick 5040 / 204.653 fixture seconds, was accepted at 5100 / 206.150 seconds and completed at 5160 / 207.939 seconds. The six-minute lifetime was still uncancelled at these observations; the case ended around 223.48 seconds. The final profiler reports 5700 Advance invocations and a 222.991-second main-thread window. This invocation count is not a recorded final actor pose or a full path trace.

Evidence SHA-256:

| Artifact | SHA-256 |
| --- | --- |
| `TestResults/editmode_20261023T174133Z.xml` | `36706EAE220548640789B548F6457A9E8B54592D6AB88D1DE9AE46DFFA30A1BE` |
| `Logs/gate_test_editmode_20261023T174133Z.log` | `A168DBF9B8B449AEC9D78AD983EED565039B2E593C7827C5D3F963252FD39C77` |

## Source findings and alternatives

`ClockApproach` calculates its detour from actual CLOCK box and player capsule sizes, checks room containment, and requires rejoining before the capsule-expanded sorting arm. `MoveIntoArrival` changes input once per rendered coroutine iteration and accepts a waypoint only within 0.03 m. Nonzero analog magnitude is clamped to at least 0.12. The native motor runs at simulation cadence with desired speed `6.5 * moveScale * environment.SpeedMultiplier`. The trace's repeated 60-tick advances between coroutine observations make an overshot or repeatedly crossed waypoint plausible. The old log does not prove this happened.

The actual CLOCK has a 2.75 m radius, 0.6 slow factor and 10-second deployment lifetime. The authored crossing window is 3 seconds. `WordboundAuthoredRuntime.FamilyReady` requires the keepsake carrier, real slowed machinery and the bounded active window, then actual arrival plus support. `ObserveAuthored` can reset to Waiting when the recipe no longer qualifies, no eligible nearby actor exists, the carrier/field requirement fails or the armed window expires. A final Waiting state alone cannot distinguish these. It also cannot establish collision with the CLOCK or sorting arm. No change to physical bounds, content/signatures, lifetimes, wind behavior or actor movement is justified yet.

`ReadMovement` is called through Fusion's `OnInput`. A future fixture control repair must verify its actual polling cadence before claiming a change there provides per-simulation-tick feedback. The conservative next step is measurement, not an enlarged waypoint tolerance or longer deadline.

## Authorized diagnostic implementation

Root authorized fixture-only observations in the existing partial test class. Starting file hashes matched root exactly:

- `Assets/_Project/Tests/OfflineGameplay/PhysicalPuzzles/WordboundNativePuzzleActions.cs`: `9BB3EC4853053797A18D088761969CBE92767B4FBA1A24916E4B0E0F80885B5E`.
- `Assets/_Project/Tests/OfflineGameplay/PhysicalPuzzles/WordboundNativePuzzleFlow.cs`: `E25A65E2872B99D4BCED0D270E2DDBD6CC26A0EE4015FFE1456057038BEC406C`.

The existing `RecordFrame` hook now feeds a bounded observer only while `MoveIntoArrival` is active. It records advancing canonical tick samples and maximum tick gap/samples per Unity update frame, counting nonadvancing callbacks separately. `Time.frameCount` is not proof of a presented image. For each currently commanded waypoint it keeps closest observed distance/pose/tick, first observed within-tolerance tick, first coroutine within-tolerance tick and a segment-cross inference only for adjacent ticks whose endpoints both lie outside tolerance. An observed inside tick can establish that the coroutine missed a sampled arrival. The segment calculation only says the straight line between two adjacent sampled positions intersects the tolerance circle; it does not prove an unsampled curved physics path. It does not infer any intermediate positions when the callback skips ticks. These are canonical `FrameChanged` samples delivered after the controller applies the reconciled frame to the native bodies, not independent native solver callbacks; failure formatting separately reads the actual Rigidbody pose and velocity.

It also retains raw exact and shared production arrival counts, slowed-motion sample counts, missing-carrier sample counts and up to 24 stage transitions (with an omitted count). At the first shared arrival it records a raw 0.2 m downward support ray from foot+0.1 m; the same ray is recorded at failure. Hit collider names, distances, normals, course-surface membership and body identities are evidence, **not** a replacement for the runtime's full `Supported`/collision policy. The 32-hit buffer explicitly reports when full, meaning further hits may be omitted.

Immediately before the unchanged assertions fail, one `WORDBOUND_PHYSICAL_ROUTE` record includes the above evidence, start/end timing, native/canonical actor pose and velocity, movement eligibility, final stage/window, recipe state/deployment tick/lifetime/activity, arm pose and all target coordinates. Observation/formatting exceptions are caught and identified by type so diagnostics do not replace the gameplay assertion. Missing recipe bodies are explicitly represented. The observer reference is cleared in `finally`.

Movement vectors, waypoint tolerance, eight-second route deadline, room/arm checks, completion/actual-arrival assertions and keepsake/slow requirements are unchanged. No source outside these test partials is edited. The observations add some diagnostic work, so a rerun is not a clean performance comparison with the prior profiler window.

`git diff --check` passed. Required root gate: compile, then `Wreckabulary.OfflineGameplayTests.WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints` with the existing native opt-in. Inspect the first failure facts before any control or production change; no new native result is claimed here.

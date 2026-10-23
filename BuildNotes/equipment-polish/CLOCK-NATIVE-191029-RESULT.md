# CLOCK and six-family native result — 191029Z

Status: COMPLETE — independent read-only review of actual root XML, log and finished wrapper receipt. No test run or source change in this lane. Batch result is **2 passed, 1 failed, 0 skipped**, 385.7 seconds; wrapper exit 1. Do not report the combined gate as passing.

| Exact native case | Actual result | Duration |
| --- | --- | --- |
| `Wreckabulary.Tests.WordboundNativeClockLineTests.ActualPlatformColliderTargetClearsFloorAndRetainsWallOcclusion` | PASS | 1.314039 s |
| `Wreckabulary.OfflineGameplayTests.WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints` | PASS | 258.311444 s |
| `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.PublicTeamRacePreservesWholeSoloParties` | FAIL: peer 1 `FormatException` during snapshot transfer | 54.972788 s |

## Geometry result

The actual test output records `colliderCenterChecks=12`, `oldFloorEndpointHits=12`, `actualPrefabPhysicsCopied=true`, `nativeKinematicMovement=true`, `wallBlocked=true`, `networkSession=false`. All moved/rotated/unit-and-nonuniform-scale collider-centre checks pass. The actual prefab's copied native components and authored arm dimensions are used, with an explicit zero-COM control reproducing the historically measured floor-level endpoint. Each old-endpoint case hits the floor; the new target clears it while an intervening real wall remains blocking. This is isolated native geometry evidence, not a spawned Fusion prefab or an art/feel test.

## Actual CLOCK route

The full fixture's CLOCK traversal starts at canonical tick 5400. It records 300 actual scripted local tick injections, 300 identical post-physics input readbacks, zero mismatch/failure, a maximum canonical gap of one, and approach-waypoint advancement at 5460, 5551 and 5611. The player keeps the keepsake throughout. This preserves the successful steering behavior from the previous run.

The new ray endpoint is now the actual arm collider centre at Y=.600000 rather than the floor-level mass centre. Of 300 real field decisions, 254 apply and 46 are line-blocked; collision and containment reject none, no ray buffer fills, and both observer error strings remain empty. The retained blocked interval identifies a registered Player collider, not a course surface, and zero transitions were omitted. The previous run's terminal course-surface obstruction is not present in the recorded transitions; legitimate player obstruction remains active.

The mechanism reaches **Complete at tick 5647**, while the actor is inside the shared authored arrival boundary and native support ray identifies the ConveyorTurn floor. Recorded arrival samples total six exact/raw and 54 shared-boundary samples. The reported final route stage remains Complete, `steeringStopped=complete`, 254 slowed-motion samples and no missing carrier. No floor blocker was globally ignored, and the observed 46 blocked decisions show the effect is not made unconditionally active. The logged coroutine route duration is 8.293 s; this is its resumed observation time after tick batching, not a claim that the configured eight-second deadline was enlarged.

## Six-family result and limits

The actual run approaches and completes the authored LabelBench, CratePress, RibbonSwitch, ConveyorTurn, SpringDock and AddressDesk sites, covering CollectLetters, PlaceObject, RedirectWind, TimedMechanism, BuildRoute and BalanceWeight. The BED route reaches Complete at tick 6573, with 60 matching tick-input readbacks and six raw/shared arrival samples. The final `WORDBOUND_SIX_FAMILIES_OBSERVED` marker is present; the XML case passes, so its per-site canonical checkpoint export/import, solved/stage/site-hash checks, final source-content equality and teardown assertions completed.

Scope stays explicit: one offline TimeRun actor, one exact flagship seed, first six lap-one sites, **49 setup player repositionings**, normal commands/native mechanism sensors between setup operations. The source still asserts `Lap == 0` and `!Finished`. This is not continuous human traversal, a full lap, three-lap/winner acceptance, public multiplayer, eight-player performance, Windows-player usability or owner visual approval. The earlier failures remain preserved; this later scoped pass closes the six-family native fixture and the tested CLOCK endpoint defect.

## Separate public failure

The public case reaches actual isolated public allocation and later fails during “Commit and acknowledge the exact command-bearing snapshot.” Its fault code is `snapshot-transfer-mismatch-duplicate-or-stale`; actual diagnostic values are same mark/count/length/wire hash = 1, duplicate chunk = 0, transfer elapsed **15,117 ms** against **10,000 ms**. The rejection is therefore the recorded transfer age, not evidence of a content mismatch or a repeated admission-token failure. It does not identify the underlying scheduling/network cause. Online lane owns the next bounded diagnosis; no deadline or validation relaxation is proposed by this report.

Evidence: `TestResults/editmode_20261023T191029Z.xml` SHA-256 `F868A7B68C966289EAC7FAAD0E0B223950C715809884FFB5AB3F401EA2F117D8`; `Logs/gate_test_editmode_20261023T191029Z.log` SHA-256 `78B7A4C56A6928F371D571E57F708C53EF666197FC2643079D959084E25C2A9C`; wrapper `.harness/delivery/native-clock-center-admission-1910-status.json` reports finished at `2026-10-23T19:16:55.1522880Z`, exit 1, and the exact three-case filter. Gate receipt reports no crash after quit.

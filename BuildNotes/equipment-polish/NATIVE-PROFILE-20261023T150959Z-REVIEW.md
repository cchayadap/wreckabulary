# Native profile and presentation probe evidence review

STATUS: NATIVE 0/3 PASS; physical profile is usable within stated limits; public-case profiles incomplete/unavailable; separate pure presentation probe records 3/3 PASS

Read [native XML](../../TestResults/editmode_20261023T150959Z.xml), its matching `Logs/gate_test_editmode_20261023T150959Z.log`, profiler/marker source, and [presentation probe JSON](../online/presentation-probe-20261023.json) plus its actual source/project. All 27 window/marker lines in the XML match the log exactly. This lane did not rerun any test, compiler, profiler or heavy job and made no source changes.

Evidence SHA-256: XML `7ae7edb50aef3439eabfbc4805a90f910742ac523a8fed1102c9e4570121d8ae`; matching log `7dfa8f38fae1f269a3d2bfe35f1092034c879c409792d42bf5358eebfb3b5b8e`; probe JSON `b27b48448dd08d0d8331adee494c464fc349bc680b4a2b7f4a59b6fc84ea4666`.

## Actual native outcomes

The XML reports three tests, **zero passed, three failed, zero skipped**, from 15:11:14 to 15:19:39 UTC on 23 October 2026 (505.073 seconds).

| Case | Actual result |
| --- | --- |
| `SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints` | Failed after 363.604 seconds. CollectLetters completed at tick 2,160 and PlaceObject at tick 3,480. Before RibbonSwitch fixture setup could enter its first completed host physics frame, the existing six-minute lifetime was already cancelled. The failure reports wait elapsed 0.000 seconds, local budget 5 seconds, fixture age 362.505 seconds, tick 3,480, gameplay ready and simulation not paused. This is fixture lifetime expiry before the third-family setup, not a passed six-family run or a new five-second movement timeout. |
| `PublicTeamRacePreservesWholeSoloParties` | Failed after 54.915 seconds because `Healthy` observed sanitized session failure `TimeoutException`. The scene/start operation ended at 22.679 seconds with maximum yielded-iteration gap 11.958 seconds. Peer 0 had reached tick 88 and 120 input reads, then was paused/no longer host; peer 1 still had no frame. No public-gameplay pass or timeout root cause is established. |
| `PublicTimeRunRequiresTwoRealHumans` | Failed after 86.181 seconds at the unchanged 65-second scene/durable-start deadline. Both peer diagnostics still had no frame, zero advances/input reads and paused simulation, with no session failure string. This is an incomplete start, not a measured slow movement phase. |

Craft evidence in the physical case confirms HAMMER and both TABLE crafts completed; the failure moved beyond those crafts. Reaching two canonical family completions does not waive the remaining family/checkpoint/keepsake assertions.

## Physical case — all eight recorded markers

Window: frames 1–64, **362.519 seconds**, main thread, all in-process instances. All eight recorders report `TimeNanoseconds`, status `observed`, 64 frame samples, capacity 32,768, `wrapped=False`, zero invalid samples and no recorder error. These are summed CPU scope durations per rendered frame; the maximum is a frame's summed marker time, not a single call. The final partial frame may be omitted; no domain-reload retention is claimed.

| Marker | Invocations | Captured total CPU ms | Maximum frame-total CPU ms |
| --- | ---: | ---: | ---: |
| `WB.Match.Reconcile` | 3,481 | 264,390.287 | 5,670.163 |
| `WB.Match.Capture` | 7,020 | 12,677.063 | 279.552 |
| `WB.Match.Advance` | 3,480 | 30,770.228 | 687.918 |
| `WB.Match.Validate` | 3,481 | 127,330.950 | 2,733.105 |
| `WB.Match.Presentation` | 3,481 | 1,613.030 | 42.349 |
| `WB.Match.PhysicsApply` | 3,481 | 130,675.734 | 2,797.178 |
| `WB.Match.BuildWorld` | 1 | 1,038.115 | 1,038.115 |
| `WB.Match.BeforePhysics` | 3,480 | 5,832.354 | 182.782 |

Scopes are inclusive and must not all be added. Source places Validate and PhysicsApply in separate sibling regions inside Reconcile; together their recorded 258,006.684 ms account for about 71.2% of the window duration and 97.6% of Reconcile's inclusive CPU time. `PhysicsApply` includes `WordboundPhysicsWorld.ApplySnapshot`'s own validation, so its total must not be described as PhysX simulation time. The data prioritizes validation/application investigation over further presentation-only optimization; it does not identify a particular validator line as the sole cause. No deadline increase or validation bypass is justified by these readings.

## Public cases — preserve recorder limitations

TeamSolo's window is 48.770 seconds, frames 1–33,562. Every recorder wrapped its 32,768 capacity, retaining 794 reported frame samples; dropped history is unknown and at least one sample. Seven markers are explicitly **retained-tail**, not full-window totals:

| Marker | Retained invocations | Retained CPU ms | Retained maximum frame-total ms |
| --- | ---: | ---: | ---: |
| Reconcile | 89 | 14,577.259 | 8,916.374 |
| Capture | 178 | 595.046 | 343.026 |
| Advance | 88 | 1,618.454 | 1,121.827 |
| Validate | 89 | 6,428.113 | 4,150.498 |
| Presentation | 89 | 94.560 | 62.961 |
| PhysicsApply | 89 | 6,871.682 | 4,574.148 |
| BeforePhysics | 88 | 331.948 | 227.631 |

TeamSolo BuildWorld is **unavailable**, with zero observed invocations in the retained data, not zero total build-world cost. Do not derive whole-window averages or speed comparisons from this wrapped sample set.

TimeRun's window is 81.865 seconds, frames 1–92,881. All eight recorders wrapped and retained 27,345 frame samples; all report **unavailable**, zero observed invocations, and CPU totals/maxima explicitly unavailable. Zero observed calls in wrapped history is not proof of zero cost across the full run. The separate operation diagnostics establish that no authoritative frame/advance was observed at either start-wait endpoint; they do not recover missing profiler history or explain why durable start stalled.

## Separate .NET presentation probe

The JSON records exit code 0 and **3 passed / 0 failed**, .NET 9.0.15 X64, command `dotnet run --project Tools/RulesHarness/PresentationProbe/PresentationProbe.csproj --configuration Release`, source commit `f7877dc6a08ff6dd418e687d476ee8e611769445`. This is a recorded run reviewed here, not an independently repeated run.

Both recorded source hashes match current actual bytes:

- `Assets/_Project/Scripts/Online/Gameplay/WordboundGameplayPlan.cs`: `5094a8830a7914a47bae8982ca4d4e26c5a866fa84d17eba0f1442d13dca3a83`.
- `Tools/RulesHarness/PresentationProbe/Program.cs`: `3e466da165f09c86910fc4746e2bef506c21b63d3267bd431306faf6b31617a2`.

The project directly compiles that gameplay-plan source and references the Rules project. It uses synthetic item-only frames, compares each output against the unchanged standalone presentation API, consumes equal complete outputs, and includes construction of each indexed frame context. A 65,536-byte control payload measured 65,560 bytes before and after. Each measurement below totals four iterations:

| Synthetic bodies | Standalone allocated bytes | Indexed allocated bytes | Standalone ms | Indexed ms |
| --- | ---: | ---: | ---: | ---: |
| 32 | 467,680 | 42,400 | 2.197 | 0.321 |
| 128 | 6,590,176 | 149,472 | 33.457 | 0.879 |
| 256 | 25,763,552 | 300,448 | 78.640 | 1.350 |

The probe's pass requires calibrated positive allocations, presentation parity/equal consumed output, and indexed allocations below half the standalone path; timing has no pass threshold. This establishes reduced managed allocation for that pure presentation workload under .NET. It does not measure Unity Mono allocation behavior, rendered frame rate, real multiplayer throughput, physics validation or a Windows player. It must not turn the native 0/3 result into an overall performance or online-readiness pass.

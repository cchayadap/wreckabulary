# Cached-validation native result review

STATUS: EVIDENCE REVIEW COMPLETE — batch failed, 1/3 passed. Read-only review; no test, compiler, Unity or source modification was performed by this lane.

## Evidence identity

- Root result: `TestResults/editmode_20261023T154541Z.xml`, SHA-256 `FAF3E01EE59FE32D3111CE3BE25803A164D727E748CEAF0B736D067485853B96`.
- Matching log: `Logs/gate_test_editmode_20261023T154541Z.log`, SHA-256 `8F6972271C66B7A68105FA675F947BC54521C8C4D45F2CA349CA4D3E7F182580`.
- Summary: `BuildNotes/online/native-cached-validation-20261023.json`, declaring source checkpoint `b1cae92`. Both recorded hashes match the actual files; all recorded profile/operation lines occur verbatim in the XML output and matching log.
- `.harness/delivery/native-cached-validation-public-physical-status.json` records exit 1. Gate stdout reports FAIL, 3 total / 1 passed / 2 failed / 0 skipped, 572.3 seconds including 126.4-second refresh. The job enabled actual external public gameplay, physical puzzles and native profiling; no placeholder/skipped cases count as passes.

## Exact case scope

| Case | Native outcome | Supported conclusion and limits |
| --- | --- | --- |
| `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.PublicTimeRunRequiresTwoRealHumans` | PASS, 79.022470 s | Two authenticated clients entered the approved TimeRun/Solo public allocation through a disposable isolated public pool (`fill=False`). Exact roster/body checks, admitted client movement, authenticated command, duplicate-command single receipt, and committed snapshot acknowledgement passed. This is an entry/gameplay/commit check, not a completed race, populated release-pool test, Windows player or soak. |
| `Wreckabulary.OfflineGameplayTests.WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints` | FAIL, 235.238261 s | CollectLetters completed at tick 2160 and PlaceObject at tick 3480. FAN/RedirectWind did not reach its authored arrival completion. Wait began at 4320 and ended at 4560: 15.301 s observed against the existing 12-second budget. Fixture lifetime was not cancelled; runner/gameplay ready, simulation unpaused, time scale 1. Six-family completion is still unverified. |
| `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.PublicTeamRacePreservesWholeSoloParties` | FAIL, 90.004864 s | Peer 1 failed with `FormatException`, reported origin `WordboundFusionSession.ReceiveSnapshot > OnReliableDataReceived`. At the end of scene/readiness waiting, the host had advanced 354 ticks and was paused; peer 1 had no course world or gameplay start. This locates the observed failure on snapshot receipt/transfer, but does not identify a corrupt field or prove a format-repair cause. TeamRace Solo remains failing; Duo/Squad were not selected. |

The TimeRun success has a material caveat: its final acknowledgement diagnostic shows host tick 765, `paused=True`, `canMove=False`; the replica had tick 645 and remained unpaused. The passing assertions prove receipt/acknowledgement, but do not require resumed host simulation afterward. The XML also contains 197 Fusion input-buffer-full warnings in that case. Those observations prevent claiming stable continuous play or acceptable frame pacing from this PASS. No session failure is recorded for TimeRun.

## Physical profiler window

The physical window was 233.763 seconds, frames 1–82. All eight markers were valid `TimeNanoseconds`, observed per marker invocation with no buffer wrap and zero invalid samples. These are main-thread, all-in-process inclusive CPU scopes; they are not additive, frame-time samples or Windows performance acceptance. In-flight scopes can be absent. `PhysicsApply` includes validation and is not a measurement of PhysX simulation alone.

| Marker (`WB.Match.` prefix) | Invocations | Captured total CPU ms | Max invocation ms |
| --- | ---: | ---: | ---: |
| Reconcile | 4561 | 101461.973 | 270.388 |
| Capture | 9198 | 17330.008 | 46.024 |
| Advance | 4560 | 47430.162 | 73.194 |
| Validate | 4561 | 44897.139 | 114.450 |
| Presentation | 4561 | 2163.450 | 21.991 |
| PhysicsApply | 4561 | 48234.315 | 73.073 |
| BuildWorld | 1 | 1384.637 | 1384.637 |
| BeforePhysics | 4560 | 8123.575 | 28.530 |

The two public cases also retain all eight observed markers without wrap: TeamRace window 81.882 s / 355 Reconcile invocations; TimeRun window 71.081 s / 766 Reconcile invocations. Unlike the older `150959Z` frame-sampled/tail-limited recording, this run labels sampling as per-marker-invocation and maximum as per sample. Do not compare their maximum-sample numbers as if they were identical frame measurements. The physical fixture now reaches FAN before its six-minute lifetime, but the changed endpoint and native work distribution are not a controlled end-to-end speed benchmark.

## FAN failure-diagnostic source closure

Independently reviewed root's post-run `WordboundNativePuzzleActions.cs` delta. It adds `WindDetails` only as the existing `Until` failure-detail callback. The 12-second deadline, completion predicate, body placement, command sequence and completion/arrival assertions are unchanged. `ReadWindDetails` reads canonical/native body state and performs a non-simulating raycast; it does not move bodies or advance physics.

The initial direct diagnostic could throw from a missing body/collider or field validation and mask the original failure. The revised wrapper catches that and returns only `windDetailsUnavailable=<exception type>`, preserving the original wait/lifetime evidence. Bounded source closure PASS. Reviewed file SHA-256: `9BB3EC4853053797A18D088761969CBE92767B4FBA1A24916E4B0E0F80885B5E`. This diagnostic was added after the reviewed run and has not yet produced native FAN evidence; no arrival cause or fix is claimed.

# Native validation-cache verification

STATUS: FAIL overall; one case PASS, two cases FAIL, zero skipped. The complete 154541Z run ended 2026-10-23T15:55:14Z. No timeout or gameplay check was relaxed.

Source at launch: `b1cae92`. Gate duration 572.3 seconds, including 126.4 seconds of refresh. Evidence: `TestResults/editmode_20261023T154541Z.xml`, `Logs/gate_test_editmode_20261023T154541Z.log`, and the hashed extract `BuildNotes/online/native-cached-validation-20261023.json`.

| Native case | Result | Case seconds | Observed result |
| --- | --- | ---: | --- |
| PublicTimeRunRequiresTwoRealHumans | PASS | 79.022470 | Exact public admission, two authenticated scripted clients, native readiness/bodies, movement, host command, exact durable snapshot acknowledgement and cleanup. |
| PublicTeamRacePreservesWholeSoloParties | FAIL | 90.004864 | Client FormatException originated in ReceiveSnapshot. Host reached tick354; readiness wait ended after54.043 seconds with max frame gap9.013 seconds. The exact receive rejection is not yet classified in this run. |
| SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints | FAIL | 235.238261 | CollectLetters and PlaceObject complete, including both TABLE crafts. FAN crafts and deploys, but its receiver arrival wait ends at tick4560 after15.301 seconds against the unchanged12-second budget. Lifetime was not cancelled. |

TimeRun readiness took21.022 seconds, movement3.293, command7.678 and acknowledgement14.366. Both peers had running sessions and isolated scene physics. The host is paused at the final acknowledgement record with empty fault text; the client is unpaused. This passing bounded test is not continuous-session, complete-race, cross-device or human acceptance, and does not establish the earlier blank-readiness stall's cause.

All24 marker records are valid nanosecond measurements with nonzero invocations, no buffer wrap, no invalid samples and no recorder error. Per-invocation retention fixes the earlier empty-frame displacement. Inclusive parent/child scopes must not be added; this is batchmode Editor evidence, not Windows frame rate.

| Scope | Physical total ms / calls | Team Solo total ms / calls | TimeRun total ms / calls |
| --- | ---: | ---: | ---: |
| Reconcile | 101461.973 / 4561 | 24160.137 / 355 | 15663.123 / 766 |
| Capture | 17330.008 / 9198 | 4876.177 / 1020 | 6737.409 / 3405 |
| Advance | 47430.162 / 4560 | 9857.884 / 354 | 6918.514 / 765 |
| Validate | 44897.139 / 4561 | 10556.289 / 355 | 6844.856 / 766 |
| Presentation | 2163.450 / 4561 | 368.584 / 355 | 362.580 / 766 |
| PhysicsApply | 48234.315 / 4561 | 11369.285 / 355 | 7411.551 / 766 |
| BuildWorld | 1384.637 / 1 | 1122.650 / 2 | 1226.937 / 2 |
| BeforePhysics | 8123.575 / 4560 | 1947.561 / 354 | 1344.735 / 765 |

The first physical TABLE is held at97.336 seconds versus207.733 in the pre-cache run, at the same tick1920. The second is held at145.184 versus308.329, at tick2940. This supports improved progression through this repeated segment; different completed workloads and shared-PC timing prohibit a whole-case speedup/FPS percentage. Validation remains substantial, especially the two-team world.

Next: classify the existing snapshot rejection using fixed safe codes and numeric facts; record FAN position/field/sensor facts only on failure; optimize remaining immutable static lookups with unchanged dynamic validation and compare the same saved .NET workload. Then rerun affected native cases before full multiplayer/private/recovery and Windows delivery. No service deployment or account change is needed for these steps.

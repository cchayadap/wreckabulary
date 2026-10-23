# Native CPU capture and retention correction

STATUS: First capture reviewed; diagnostic retention correction independently source-reviewed PASS by recover_online, native rerun pending. No heavy job run in this lane. Root owns performance fixes and acceptance.

Evidence: root `Logs/gate_test_editmode_20261023T150959Z.log` and `TestResults/editmode_20261023T150959Z.xml`. The mixed batch finished **0/3 passed, three failed, zero skipped**. Raw logs/XML are preserved. Physical reached canonical tick 3480 and completed CollectLetters plus CratePress, including both actual TABLE crafts; it then entered a setup wait with zero elapsed wait and the six-minute lifetime already cancelled at 362.505 seconds. TeamRace Solo failed its writer lease; TimeRun failed readiness. These are authenticated scripted client tests, not human play acceptance.

## Physical capture: no buffer wrap

The recorder window was 362.519 seconds, engine frame 1 through 64. Each marker retained 64 frame entries, with valid nanosecond units, no wrap, no invalid samples or recorder errors. Original values below are **summed per frame**; their maxima are aggregated frame values, not individual calls.

| Marker | Invocations | Captured total CPU ms | Maximum aggregated frame CPU ms |
| --- | ---: | ---: | ---: |
| Reconcile | 3,481 | 264,390.287 | 5,670.163 |
| Capture | 7,020 | 12,677.063 | 279.552 |
| Advance | 3,480 | 30,770.228 | 687.918 |
| Validate | 3,481 | 127,330.950 | 2,733.105 |
| Presentation | 3,481 | 1,613.030 | 42.349 |
| PhysicsApply | 3,481 | 130,675.734 | 2,797.178 |
| BuildWorld | 1 | 1,038.115 | 1,038.115 |
| BeforePhysics | 3,480 | 5,832.354 | 182.782 |

Validation is the dominant measured component: Reconcile's explicit Validate is expensive, and PhysicsApply includes another validation call. Presentation is much smaller in this capture. Reconcile includes these child scopes and fixture FrameChanged observers, so adding these rows is invalid; they are not separate portions of wall time. BeforePhysics includes fixture setup and the real runtime callback. ObserveHost and checkpoint serialization/publication are not fully covered by these eight markers. Final partial frames, uninstrumented work, Editor overhead and other threads remain outside a complete attribution. This is not a presented Windows frame-rate measurement or a controlled before/after improvement percentage.

## Online capture: incomplete retention

TeamRace's 48.770-second window advanced to engine frame 33,562, beyond the 32,768-entry capacity. Every recorder reports wrap; only 794 frame entries remain. Retained Validate is 6,428.113 ms and PhysicsApply 6,871.682 ms versus Presentation 94.560 ms. These values identify costly retained work but cannot be called whole-case totals. BuildWorld's retained invocation count is zero and correctly reports unavailable.

TimeRun's 81.865-second window advanced to engine frame 92,881. All recorders wrapped and retained 27,345 empty frame entries with zero invocations. All timing fields correctly report unavailable; this does not mean the case did no work. Fast empty Editor frames displaced the meaningful samples.

The bounded diagnostic correction removes only `SumAllSamplesInFrame`, preserving main-thread collection, wrap behavior, capacity 32,768, start/stop points, validity handling and disposal. It records actual completed marker-invocation samples instead of empty frame entries. Logs now say `sampling=per-marker-invocation`, `samples`, `capturedMaxSampleCpuMs`, and `max=sample-not-frame`; pending open scopes are explicitly not yet recorded. No buffer growth, per-frame managed polling, extra yield, timeout/order/input change or production edit is included. Original aggregated maxima above must not be compared directly with new per-sample maxima.

The next root run must establish nonzero relevant samples, valid time units and whether wrap occurred before using the corrected capture. Capacity loss is still possible for more than 32,768 marker invocations and remains explicit. A successful recorder is separate from gameplay acceptance. Source-only review of the proposed validation metadata cache is tracked separately.

Independent source closure: recover_online confirmed the explicit flags omit frame summing while preserving current-thread collection, capacity and wrap; sample/max labels match the new granularity; start/stop/disposal and fixture behavior are unchanged. No native success is inferred from this review.

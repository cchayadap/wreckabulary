# Phase capture control and enabled evidence

STATUS: NATIVE COMPARISON COMPLETE — report-only evidence. Root exclusively executes all jobs. Existing unrelated ModeDefinition changes and prior logs remain untouched. The later Windows QA workload is separate and pending final evidence.

Control requested by root: native-phase-control-2033, source 714acc05, WORDBOUND_PROFILE_PHASES=0 and WORDBOUND_PROFILE_NATIVE=1, collector fixture (19 expected cases) plus public TeamSolo. Exact saved status, XML, logs and subsequent enabled job will determine the result. No absent measurement is treated as zero cost, no failed scenario as acceptance, and no source-only prediction as measured performance.

## Disabled control: confirmed PASS

Exact integration artifacts:

- `.harness/delivery/native-phase-control-2033-job.json` and `-status.json`: PHASES=0, PROFILE_NATIVE=1, external gameplay/public flags=1, exact shipped public catalogue path; wrapper 34660 exited 0 at 20:35:26.711Z.
- `TestResults/editmode_20261023T203305Z.xml`: **20/20 passed, 0 failed, 0 skipped**, XML duration 63.841 seconds. Nineteen are the new PhaseCaptureTests; public TeamSolo took 62.360 seconds including its test lifecycle.
- `Logs/gate_test_editmode_20261023T203305Z.log`: real public allocation, whole-party roster, gameplay/movement/command/exact snapshot ACK observed; final test pass includes existing teardown assertions. No WORDBOUND_PHASE_WINDOW or WORDBOUND_PHASE lines were emitted with the capture flag disabled.

All 19 diagnostic cases executed in actual Unity: disabled ownership, nested exception timing, overlapping capture rejection, cross-thread isolation and cleanup, stale generation, duplicate/out-of-order disposal, fixed nesting bound, invalid/unavailable output, five-mode actual Advance/atomic parity, real rollback/retry/reentry, and three atomic callback endings. This is not the default engine-free projection, which excludes those guarded tests.

Public case operation outcomes: readiness host canonical tick 160/client checkpoint tick 40; movement passed in 7.088 seconds (maximum continuation gap 1.839 seconds); authenticated command passed in 3.544 seconds; exact command-bearing snapshot ACK passed in 8.744 seconds. At ACK end host was at canonical tick 588 and client checkpoint tick 450. Host paused=true at that observation is retained evidence, not silently converted into smooth real-time progression. Final test passed its actual command/roster/replica/cleanup assertions; it does not complete a race or prove every public mode.

Outer profiler window: 54.778 seconds, main thread, all in-process instances, inclusive scopes; every recorded buffer was valid/unwrapped. These timings cover startup and the entire observed test window, not a steady-only slice.

| Outer marker | Calls | Total ms | Maximum ms |
| --- | ---: | ---: | ---: |
| Reconcile | 589 | 7,308.022 | 165.009 |
| Advance | 588 | 4,922.768 | 49.277 |
| Capture | 589 | 1,087.425 | 17.852 |
| Presentation | 589 | 247.511 | 10.481 |
| PhysicsApply | 589 | 541.477 | 9.769 |
| BeforePhysics | 588 | 1,333.608 | 31.479 |
| BuildWorld | 2 | 887.367 | 711.471 |

Validate remained unavailable because its old marker boundary was removed. Reconcile contains several other listed scopes, so totals are not additive. The host completed 690 forward batches / 2,343 SDK ticks, largest batch 60 ticks / 2,156.704 ms. Client completed 311 forward batches / 893 ticks plus 278 resimulation batches / 2,767 ticks. SDK tick counts include startup/resimulation and differ from canonical match ticks. Last observed host/client batch ages were 125.601/140.675 ms at stop. Passing the operation did not eliminate large host catch-up batches.

## Comparison contract

The public method fixes TeamRace/Solo, two authenticated humans, seed `0123456789abcdef`, and uses the exact catalogue assigned to the shipped LaunchAssets; the constructor rejects unapproved content. Retain the same catalogue, item/rule bytes, code and flags apart from explicit phase enablement. Each run still has fresh users/allocation and may swap which signed seat becomes host, with uncontrolled network/OS scheduling and differing tick/window counts. One disabled/enabled pair can establish that useful phase observations are obtainable and reveal a dominant measured section; it cannot isolate small instrumentation overhead, establish stable FPS or attribute every service/scheduling delay to CPU.

Enabled job announced and independently inspected: `.harness/delivery/native-phase-enabled-2035-job.json`, wrapper 30324, started 20:36:05.162Z, exact same 20-case filter and environment except PHASES=1. `git diff --name-only 714acc05 2eec7d91` contains only the Unity-generated `.meta` files for the collector, its tests and QA observer; no executable source differs. Root deferred the QA Return-boundary fix until this comparison finishes.

Read-only SHA-256 of frozen inputs at comparison time:

- Public catalogue: `4633207d08f8acce18ca3463bb86a71b1f3eaba41a2adfd475b46e9e5826ea23`.
- Items JSON: `79363c8f1f14e7aca553718b552468d34b3140fe4ba88688fd4ded8330cf2416`.
- Rules JSON: `f183d9b79d1b438a78fb682455c6ce5e6b15cbd09d47b0831b3af47b2f697d4c`.

## Enabled run: confirmed PASS

Exact integration artifacts: `.harness/delivery/native-phase-enabled-2035-status.json` (exit 0, finished 20:37:44.640Z), `TestResults/editmode_20261023T203605Z.xml` and `Logs/gate_test_editmode_20261023T203605Z.log`. The XML reports **20/20 passed, 0 failed, 0 skipped**, duration 63.784 seconds; the public case took 62.387 seconds. All 19 collector cases passed again.

The public case reached readiness at host canonical tick 157/client checkpoint tick 37. Movement passed in 6.653 seconds; the command passed in 2.429 seconds; its exact command-bearing snapshot ACK passed in 10.497 seconds. At ACK end host tick was 596, client checkpoint tick 450, client input reads 643 with 273 nonzero. Host paused=true remains an observed endpoint, with no reported session failure. The final pass includes existing real public allocation, roster, body, command, replica and cleanup checks. It is one TeamRace/Solo two-human case, not all-mode or full-race acceptance.

Both phase windows were valid, with `invalid=None`, `inFlight=0`, thread 1, frequency 10,000,000 ticks/second and distinct generations. Values are Stopwatch **inclusive elapsed**, not CPU time; they aggregate all instances on the owner thread and all full-validation contexts.

| Phase | Startup calls | Startup ms | Ready calls | Ready ms | Ready ms/call |
| --- | ---: | ---: | ---: | ---: | ---: |
| RollbackCapture | 157 | 308.495 | 439 | 911.831 | 2.077 |
| Synchronize | 159 | 536.582 | 439 | 1,453.066 | 3.310 |
| RetireResources | 159 | 72.831 | 439 | 164.957 | 0.376 |
| FullValidation | 170 | 1,457.530 | 459 | 3,858.095 | 8.405 |
| MutableValidation | 158 | 188.802 | 439 | 502.476 | 1.145 |
| EconomyValidation | 328 | 153.850 | 898 | 349.471 | 0.389 |
| ObserveHost | 157 | 451.029 | 439 | 1,280.563 | 2.917 |
| Prepare | 158 | 404.582 | 439 | 738.956 | 1.683 |
| FrameChanged | 158 | 0.083 | 439 | 0.224 | 0.000511 |

Startup lasted 34.584466 seconds, from no host frame to epoch 0/tick 157 with 186 bodies, 588 resources and 432 mechanisms. It includes gameplay ticks before all peers become ready, not just loading. Ready lasted 19.799523 seconds, from epoch 0/tick 157 to epoch 0/tick 596 with those same section counts. The 439 canonical-tick difference matches its rollback, synchronization, host observation, prepare, mutable-validation and FrameChanged counts. Full validation ran 20 additional times in combined contexts. Economy's 898 calls equal 459 full plus 439 mutable calls; its total cannot be attributed wholly to either parent.

FullValidation is the largest measured ready phase: 3,858.095 ms, about 19.49% of the ready elapsed window. This is an attribution to a measured region, not exclusive CPU or a claim that it explains every pause. RetireResources is nested in Synchronize and accounts for 11.35% of that parent's elapsed total; optimizing it again would target only 164.957 ms in this window. Economy is also nested and already relatively small. FrameChanged's tiny native total does not establish Windows HUD/rendering cost, because this fixture does not attach the same presentation listeners.

The outer native profiler covers a different, 54.409-second window. All buffers were valid/unwrapped; Reconcile recorded 597 calls / 7,340.048 ms, Advance 596 / 4,927.515 ms, Capture 597 / 1,177.880 ms, Presentation 597 / 287.529 ms, PhysicsApply 597 / 563.206 ms, BeforePhysics 596 / 1,375.850 ms and BuildWorld 2 / 760.325 ms. The removed Validate marker remains unavailable. These totals overlap and must not be added to phase totals.

The enabled host completed 762 forward batches / 2,316 SDK ticks; the largest 60-tick batch lasted 1,845.364 ms. Client completed 335 forward batches / 802 ticks and 284 resimulation batches / 1,514 ticks. No pending batch, unmatched begin/end, pairing mismatch or invalid tick count was recorded. Last observed host/client batch ages were 136.354/148.730 ms. This still demonstrates large host batches despite a passing operation. Compared with the control's 2,156.704-ms largest batch, it is one variable run, not a measured speedup or proof of insignificant diagnostic overhead. No FPS conclusion follows.

## Smallest next attribution slice

Root approved five child scopes after reviewing this pair. They expand the fixed collector from 9 to 14 phases and retain the existing opt-in, compile guards, windows and failure semantics. No performance fix is inferred yet.

| Proposed full-validation child | Exact existing work to surround |
| --- | --- |
| ProgressRestore | `new MatchProgress(mode, expectedBinding.Seats, content.Route)` and the immediately following `progress.Restore(snapshot.Progress)` together |
| Resources | The existing `ValidateResources` call |
| Bodies | The existing `ValidateBodies` call |
| Mechanisms | The existing `ValidateMechanisms` call |
| BodyBudget | Only the existing `ValidateBodyBudget` call inside full `Validate`, not the same helper's fresh mutable-dependency call |

All five boundaries belong at their existing full-validation call sites, inside the same exception filter and original order. Do not call a dependency again for timing. Valid full calls should each enter every child once; early rejected snapshots may stop before later children. Retain existing invalid/order, atomic mutation, rollback and exception-identity tests; extend the current collector fixture with child-count/parity checks rather than introducing a duplicate fixture. Root must execute the actual guarded Unity fixture, because the ordinary engine-free projection excludes it.

The remainder of full validation includes binding/roster/play checks, catalogue/economy, command-journal restore, actors, practice, deployments, thrown bodies, random/story checks and instrumentation overhead. Do not subtract the existing combined Economy total from FullValidation, or call a residual exclusive CPU time. Use completed, valid windows and exact child boundaries before ranking the next optimization. Public admission, transport and writer deadlines remain unchanged; dynamic checks and mutable verdicts are not cached or skipped.

Source-only candidates to revisit **after** this split: Bodies still scans the current mechanism list for each physical platform; BodyBudget rebuilds current item/team/body/auxiliary views; ProgressRestore includes route and roster validation. None has a measured individual attribution yet. Static physical site/puzzle lookups are already cached, and `ResolvedCoursePhysicalSite.Hash` is a stored get-only value, so no repeated site-hash computation is asserted. A future per-call mechanism index would need to preserve duplicate/missing rejection and error order; an eager dictionary is not automatically equivalent.

## Separate Windows workload

Root launched the existing visible workshop QA scenario at 20:41:32Z using build `203920` (source `d8eb1804`, build PASS 102.6 seconds), `WORDBOUND_PROFILE_PHASES=1`. Its log is `Logs/player/20261023T204132Z-visible-wordbound_workshop_flow.log`. This includes local workshop playtest presentation in a Windows player and the reviewed explicit Return-boundary finish; it is not the native two-human public workload. Await final scenario checks and phase windows before appending its outcome or comparing its individual scopes.

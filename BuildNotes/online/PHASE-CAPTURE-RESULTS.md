# Phase capture control and enabled evidence

STATUS: NATIVE COMPARISON AND WINDOWS OBSERVATION COMPLETE — report-only evidence. Root exclusively executes all jobs. Existing unrelated ModeDefinition changes and prior logs remain untouched. Native 20/20 passed in both runs; the separate Windows scenario failed its existing 90-second playtest deadline.

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

Root launched the existing visible workshop QA scenario at 20:41:32Z using build `203920` (source `d8eb1804`, build PASS 102.6 seconds), `WORDBOUND_PROFILE_PHASES=1`. Its log is `Logs/player/20261023T204132Z-visible-wordbound_workshop_flow.log`. This includes local workshop playtest presentation in a Windows player and the reviewed explicit Return-boundary finish; it is not the native two-human public workload.

Final evidence is now inspected: `.harness/delivery/20261023T204132Z-visible-wordbound_workshop_flow-status.json`, that player log, and `docs/qa/runs/20261023T204132Z-visible-wordbound_workshop_flow/checks.json`. The normal-window supplemental launch exited 1 at 20:43:47.259Z. It explicitly records `diagnosticOnly=true` and `officialGateReplacement=false`; this is not a substitute for official R4 acceptance. The player is non-development, Unity 6000.6.3f1, 1280x720, build 20261023T203920Z. The scenario's overall result is **FAIL**.

Open, edit/autosave, return and Undo/Redo steps passed. Actual autosave matched edited bytes, recovered without backup, left zero temporary files and preserved draft/history. Four captures were written; pixel checks report nonblack, nontransparent, varied images. This report does not substitute pixel checks for visual-quality review. Console checks recorded zero errors, exceptions and warnings. Local gameplay became ready at 5.855 seconds/tick 50/frame 14 with 93 visuals; its ready capture passed the same pixel checks.

The existing 90-frame wait did not finish before the unchanged 90-second deadline. The failed step retained the last periodic 75-frame observation at 83.850 seconds/tick 4550/frame 89. Return entry supplied the later, explicit final observation: 81 waited frames at 90.205 seconds/tick 4970/frame 96, still ready, unpaused, timeScale 1, focused, with no gameplay/guide/presentation fault. Those are distinct observation times, not contradictory final counts. Each logged 15-frame interval advanced 900 canonical ticks, consistent with 60 simulation ticks per observed frame over those intervals. This is evidence of poor responsiveness, not a measured graphics-only FPS or a claim that every frame contains exactly 60 SDK ticks.

Both phase windows were valid (`None`, 0 inFlight), with 10,000,000 Hz Stopwatch and separate generations. Startup lasted 5.846360 seconds and ended tick 50 with 93 bodies/294 resources/216 mechanisms; full validation ran 59 times/367.449 ms. The ready window ended explicitly at `return-boundary` **before** workshop teardown, lasted 84.349911 seconds, and covered tick 50→4970 with unchanged section counts. Its 4,920 canonical advances match rollback/synchronization/observe/prepare/mutable/FrameChanged counts. Full validation's 5,061 calls include 141 other-context validations, and Economy's 9,981 calls equal 5,061 full + 4,920 mutable calls.

| Ready phase | Calls | Inclusive elapsed ms | ms/call |
| --- | ---: | ---: | ---: |
| RollbackCapture | 4,920 | 5,526.181 | 1.123 |
| Synchronize | 4,920 | 7,717.210 | 1.569 |
| RetireResources | 4,920 | 1,091.268 | 0.222 |
| FullValidation | 5,061 | 19,601.250 | 3.873 |
| MutableValidation | 4,920 | 3,005.204 | 0.611 |
| EconomyValidation | 9,981 | 2,272.127 | 0.228 |
| ObserveHost | 4,920 | 6,019.018 | 1.223 |
| Prepare | 4,920 | 4,953.966 | 1.007 |
| FrameChanged | 4,920 | 611.114 | 0.124 |

FullValidation remains the largest measured ready phase (23.24% of ready elapsed). Its 3.873 ms/call must not be presented as a speedup over native 8.405 ms/call: local content has half the recorded bodies/resources/mechanisms, a different seed/mode/roster, real player presentation and a different runtime/window. The nested-phase and elapsed-not-CPU limits still apply. Even FrameChanged's 611.114 ms cannot stand for all rendering, GUI, driver or scheduling cost. The result supports measuring the five approved validation children next; it does not justify skipping validation, extending deadlines or claiming a tested smooth Windows build.

## Hypotheses awaiting the 14-phase run

This section is a read-only source inventory requested after the timing extension was approved. The five child costs have **not** been measured yet. None of these candidates is authorized or implemented as a behavior change. Relevant files are `MatchStateValidation.cs`, `MatchProgress.cs`, `MatchPhysicalValidation.cs`, `WordboundEntityBudget.cs` and `AuthoritativeLifecycle.cs` under `Assets/_Project/Scripts/Rules/Wordbound`.

| Child | Precisely repeated source work | Conditional smallest candidate and required invariant |
| --- | --- | --- |
| ProgressRestore | Every full call constructs MatchProgress: copies roster/route arrays, validates roster/route, constructs default player/team dictionaries and computes DefinitionHash by serializing mode, sorted seats and every lap/checkpoint/puzzle ID. Restore then validates the current snapshot and replaces those initial dictionaries with defensive copies. Restore also rebuilds required/allowed puzzle prefixes and keepsake-location enumerations for each team. | If measured dominant, first isolate immutable match-definition preparation from current-state restore. Any descriptor must bind the exact mode, roster and ordered route; a course/mode-only cache is insufficient because rosters differ. Public constructor validation and hash bytes must remain unchanged, and every dynamic restore check/result/confirmed state/defensive copy must still run. Do not cache a restored verdict or reuse a mutable MatchProgress across callers. |
| ResourcesValidation | The initial uniqueness check constructs a slash-delimited ResourceKey string for each resource; a later per-call tuple set independently indexes the same source/team/lap identity. Existing current-team and source-to-loose-tile lookups already remove larger scans. Nonempty tile groups still construct and consume a LetterBag exactly. | If this section matters, a single per-call tuple membership set could potentially serve both uniqueness and later presence, provided exact identity semantics and initial count/duplicate error precedence are proved. Retain every generation/lap/deadline/Demanded check and exact loose-letter multiplicity, including inactive or foreign-owner tiles. Freestyle practice demand has a different selected-station path and must remain live. |
| BodiesValidation | Each physical fixture body performs SingleOrDefault over the current mechanism collection for exact puzzle/lap/team, even though static fixture/site lookup is cached. Protected source validation separately scans current resources and consults live demand. | If this section dominates, a private per-call mechanism grouping is a small candidate. It must preserve a missing result, and duplicate rejection only when that exact key is queried at the original location. Eager ToDictionary would change error order, especially for duplicates unrelated to a body. Preserve body enumeration order, pose calculation/tolerance, all body/ownership bounds and the later missing-fixture check. Current snapshots must be indexed anew each call. |
| MechanismsValidation | Initial distinct checking constructs ResourceKey strings; all mechanisms run live physical evidence and timing checks. Completed past laps scan the relevant checkpoint puzzle IDs. Activated/solved evidence may repeatedly search item/body lists; public Economy.Items access in physical validation creates defensive views for active CollectLetters/RedirectWind evidence. | If measured large, identify whether the cost occurs in waiting or active evidence before choosing an index. Waiting physical evidence returns before auxiliary body/economy lookups. Site and puzzle lookup are already cached and Site.Hash is computed only when the resolved site is created. A blanket claim of rebuilding physical context/hash per mechanism is false. Preserve causal ticks, actor/team membership, consumed-item uniqueness, recipe cost and active-station/prerequisite checks. |
| BodyBudgetValidation | The full pass creates item/team dictionaries and map/dynamic/core/pinned arrays. AuxiliaryBodies copies body/mechanism enumerables, scans current checkpoint wind sites and selects the lowest eligible receiver item. Three Usage walks run per pool. The same helper deliberately runs again against fresh mutable rules after preparation, outside this new child scope. | Start with exact static placement membership lookup already available in StaticContent if measurement supports it; broader per-call counts or receiver indexes need separate proof. Keep deployed limits live, ownership reassignment for held/deployed items, shared-body ceiling division, reserved recovery/auxiliary capacity, pinned references and pending crafts. Lowest item ID and one-receiver-per-site selection must not change. Never carry a budget verdict or mutable rule result across the prepare callback. |

Meaningful follow-up regressions depend on the measured winner. Progress needs same-content/different-roster and ordered-route identity, reconnect/result boundaries and repeated independent restoration. Bodies needs missing/duplicate mechanisms both referenced and unreferenced, earlier competing body errors, moving-fixture parity and immutable prior/new snapshots. Resources needs duplicate/missing scope, wrong generation/team/lap, exact and excessive loose-letter batches and valid→invalid→valid calls. Mechanisms needs waiting/armed/complete evidence and retained physical rejection order. Budget needs all ownership modes, shared rounding, held/deployed reassignment, auxiliary receiver selection, mutation during atomic prepare and exact capacity edges. Existing guarded collector tests alone cannot establish those future behavior changes.

The observed two-human ready shape is 186 bodies/588 resources/432 mechanisms; the separate Windows local shape is 93/294/216. These counts bound the source hypothesis but are not operation timing. Choose one candidate only after valid child data arrives, preserve the unchanged actual input workload for before/after evidence, and retain the other hypotheses as unimplemented.

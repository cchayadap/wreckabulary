# Public TeamSolo comparison — 2026-10-23 19:10:29 UTC

Status: final evidence inspected; public acceptance FAIL. Read-only review of integration's completed `191029Z` run against `181114Z`. No source edits, execution, profiling, service calls or deployment in this lane. Original dirty ModeDefinition and historical logs remain untouched.

## Actual outcome

The gate finished in385.7s (44.7s refresh), exit1,3 tests:2 PASS,1 FAIL,0 skipped. Unity log reports6000.6.3f1, Windows x64, batch EditMode with real Play Mode transitions. The six-family physical flow passed in258.311444s and the actual CLOCK collider/occlusion case passed in1.314039s. Public TeamRace Solo failed in54.972788s. These two physical passes do not turn the public case into a pass or establish a full match.

Exact evidence, relative to integration:

- `Logs/gate_test_editmode_20261023T191029Z.log`
- `TestResults/editmode_20261023T191029Z.xml`
- `.harness/delivery/native-clock-center-admission-1910-{job,status}.json` and `-stdout.log`
- Baseline `Logs/gate_test_editmode_20261023T181114Z.log`, its XML, and `BuildNotes/online/native-clock-atomic-team-20261023.json`.

The job used both external gameplay flags, the actual public catalogue path, `WORDBOUND_PROFILE_NATIVE=1`, `WORDBOUND_NATIVE_PUZZLES=1`, the three exact methods recorded in the job, and gate timeout2400. Root alone executed it. Current integration HEAD at inspection was `1ba367ca75e408e9a87780b227d8c4faf2f70606`; this is not substituted for a separately recorded source-at-launch digest.

Public entry verified the approved `wordbound-20261023-v1` whole-party certificate in its isolated public pool. The same two admitted peers then completed scene/durable readiness in12.779s, canonical body replication in0.005s, actual host-observed client movement in4.472s and authenticated command execution in5.726s. The exact command-bearing snapshot/replica-ACK wait failed after14.721s. Host canonical tick reached763; client frame remained175. The earlier baseline failed during initial readiness at host tick360, before these gameplay phases.

## Measured public markers

Both windows are main-thread, all in-process instances, per-marker invocation,32768 capacity, no wrap and no invalid samples. Baseline window61.169s; current52.214s. They cover different amounts of successful gameplay and different final failure phases, so raw totals are not fixed-workload timings.

| Marker | Baseline calls / inclusive CPU ms | Current calls / inclusive CPU ms |
| --- | ---: | ---: |
| Reconcile |361 /9084.061|764 /11217.552|
| Capture |1337 /4507.558|764 /1721.394|
| Advance |360 /7187.259|763 /10298.099|
| Presentation |361 /297.577|764 /429.865|
| PhysicsApply |361 /591.357|764 /806.808|
| BuildWorld |2 /996.795|2 /759.116|
| BeforePhysics |360 /1614.610|763 /1961.761|

Controller Capture calls per Advance fell from3.714 to1.001, consistent with removal of the two unchanged-frame polling captures; the current764 consists of initialization plus763 advances. The wrapper still excludes internal rollback captures. Mean observed Reconcile sample changed25.164→14.683ms and Advance19.965→13.497ms; these averages include different initialization/state/machine scheduling and are descriptive, not a controlled speedup or60Hz result. Both Validate markers had no samples and remain **unavailable**, not zero cost. These scopes overlap and must not be summed. PhysicsApply is the post-atomic physical application scope in both compared runs.

| Tick observer | Baseline | Current |
| --- | ---: | ---: |
| Host forward batches / Fusion ticks |675 /1778|283 /2085|
| Maximum host batch ticks |60|60|
| Maximum host batch wall ms |4746.743|2637.959|
| Maximum host Unity Update gap ms |5368.842|2877.727|
| Maximum client batch-start gap ms |14886.722|6185.537|
| Maximum client forward batch wall ms |17.049|47.222|

Fusion ticks include periods without canonical Advance; they are not interchangeable with the763 canonical advances. Tick clocks are Stopwatch wall time, not CPU or packet arrival. Both runners have no pairing mismatch, unmatched begin/end or invalid tick count. The16-entry tail overwrites old detail, but its totals include those samples. Smaller maximum host batches coexist with a failed transfer; no FPS, throughput guarantee, independent-machine performance or network-loss claim follows.

## Exact failure and source attribution

The final sanitized client fault is `snapshot-transfer-mismatch-duplicate-or-stale` at `WordboundFusionSession.ReceiveSnapshot`. Same mark/count/length/wire-hash flags are all1, duplicate flag0, elapsed15117ms against the unchanged10000ms limit. The baseline had the same flags with14766ms. Thus the absolute age predicate at Session.cs727–729 is sufficient to explain both failures. This run has no admission-time failure.

`incomingStarted` is set once when the first chunk of a mark arrives. Each later chunk checks the same absolute window. The error occurs before assembling/decompressing the completed packet, full snapshot validation, ReplicaReceived or ACK. Matching wire-hash metadata is not evidence that the unfinished payload passed hash validation. The observed elapsed value is the receiving continuation's wall-clock interval, not measured RTT or proof of malformed content.

`SendCommitted` captures one immutable packet/mark, sets `sentMarks[player]` and queues every chunk synchronously. That per-peer marker blocks a second transfer until the exact ACK removes it; only then is a newer committed mark sent. The traced code and matching diagnostics do not establish a mixed-publication defect. Current tail samples show several host batches lasting2–2.64s, and client callbacks separated by multiple seconds; this is consistent with aggregate main-thread scheduling delay across a transfer, but precise send/arrival/queue timing and packet size/index progression were not recorded. No transport cause is proven.

## Bounded next action

Do not increase/reset the absolute transfer deadline, skip hash/import/ACK checks, suppress the exception, change authentication or presume a network fix. Root approved the previously isolated EnsureCapacity count-only snapshot elimination as the next small main-thread cost change. Preserve full rollback/publication/import captures and every validation boundary. Its own source review, core lifecycle/capacity regression and the same public native comparison are still required; this report does not claim it will resolve the transfer failure.

If the next comparison still fails, the smallest useful additional observation is bounded safe transfer metadata (chunk count/index, received-count, packet bytes, monotonic send/receive span and existing run generation), without payloads/identities/secrets or protocol changes. That would distinguish queued/chunk scheduling from guessing a new transfer policy. It is a proposal only, not implemented here.

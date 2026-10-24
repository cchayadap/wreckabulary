# Checkpoint pacing: exact public case 000038

STATUS: ANALYSIS COMPLETE. Read-only evidence and bounded proposal; no runtime edits, jobs or deployment. All eight diagnostic cases and twenty phase cases passed. The exact public TeamRace Solo case failed its unchanged movement deadline. Overall gate45/47 also includes an unrelated party GUI fixture failure owned elsewhere.

## Evidence identity and scope

- Root XML: `TestResults/editmode_20261024T000038Z.xml`, SHA256 `539850AD840232F4904E40166A93D108405345A2065264F93040F2170969DA6B`.
- Extracted only `//test-case[@name='PublicTeamRacePreservesWholeSoloParties']`, whose duration is52.044084s. Synthetic diagnostic fixture output is excluded.
- Root job `native-checkpoint-viewport-0000` enabled CHECKPOINTS, PROFILE_NATIVE, PROFILE_PHASES and the two external gameplay flags, with the exact existing public catalogue. Root source e8dddc6b includes the reviewed diagnostic slice; optional case-label commit21ffe261 was not integrated.
- This case has one host Runner and one client Runner, no replacement. Counters cover startup and subsequent gameplay, not just the failed movement operation. All times below are Stopwatch wall/continuation observations, not CPU, GPU, service execution or network RTT.

## Measured checkpoint facts

Host accepted12 exact publication ACKs: no unmatched ACK, superseded/pending publication, stage failure or in-flight operation at stop. Maximum observed canonical-versus-committed gap is120 ticks. Publication entry to accepted durable ACK totals12,903.532ms, mean1,075.294ms, maximum1,749.236ms. This includes canonical work, upload/recovery and Unity continuation scheduling, ending before session publication/replica work and controller scheduling complete.

Ten matched post-bootstrap accepted-ACK to next successful publication-entry gaps total14,173.105ms, mean1,417.311ms, maximum2,179.346ms. One bootstrap gap is explicitly excluded; no ownership/epoch gap or abandoned publication is reported. These are inclusive inter-publication gaps, not1.417s of pure controller idle. The known configured500ms completion-based delay is only one component.

| Host stage | Calls | Total ms | Mean ms | Maximum ms |
| --- | ---: | ---: | ---: | ---: |
| Publish, including existing canonical export/validation/hash | 12 | 1,213.125 | 101.094 | 238.708 |
| Existing ValidateSnapshot call | 12 | 1,326.359 | 110.530 | 149.204 |
| Accepted ACK call | 12 | 6.556 | .546 | 6.289 |
| Checkpoint begin transport await | 12 | 1,400.822 | 116.735 | 159.842 |
| Individual chunk transport awaits | 49 | 7,347.825 | 149.956 | 272.409 |
| Finalize transport await | 12 | 1,482.280 | 123.523 | 173.112 |

Chunk maximum concurrency is4. Its sum cannot be treated as upload wall duration or subtracted from the inclusive publication total. Remaining preparation/compression, coordinator parsing, main-thread queueing and session work are not separately timed. Host head1/145.212ms, renew6/736.017ms, claim1/105.028ms and open1/80.480ms all succeeded. Client ValidateSnapshot ran twice/213.617ms; client head ran twice/178.488ms. No client publication/authority ACK is expected. These counts do not replace exact replica-ACK evidence.

## Pause samples versus elapsed intervals

| Host-forward classification | Samples | Previous-label observed interval ms |
| --- | ---: | ---: |
| Not ready | 461 | 6,070.659 |
| Eligible | 865 | 26,148.572 |
| Paused with observed same-epoch durable gap | 519 | 821.849 |
| Migration / terminal result / other pause | 0 / 0 / 0 | 0 / 0 / 0 |

There are no stale or unavailable samples. The519 durable-gap samples are37.5% of1,384 post-ready samples, but their attributed intervals are only3.047% of the26,970.421ms post-ready observed intervals. Those are different measurements: fast paused catch-up ticks can consume many SDK ticks with little wall time. Intervals use the prior observation's label, omit final/cross-runner gaps and are not exact pause onset or duty cycle. Therefore neither37.5% nor3.047% is a measured production wall-time loss. The865 eligible intervals average30.230ms per observation; this includes main-thread/batch work and gaps and is not a CPU cost per canonical tick.

This supports a real durable-window limiter and measurable checkpoint pacing overhead. It does not establish that changing cadence alone fixes the movement failure or the remaining frame-time target.

## Separate movement/scheduling evidence in the same exact case

The movement operation lasts10.876s. Host canonical tick advances543→865 and is not paused at the final operation observation. The client stays at53 total input reads and0 moving reads throughout that operation; its own committed replica advances0→25. These differ from the prior234950 case's303 moving reads. The printed client position belongs to its own committed replica, not the host's native remote-player pose.

The existing exact-case tick recorder reports client143 completed batches, no pending/unmatched/mismatched batch, last completed batch11,092.939ms before stop, while its latest Unity Update is only80.745ms old. Host269 batches continue; both runners have recent Unity updates. Thus no new client OnInput during the movement interval is corroborated by absent client SDK batches, not proved to be a rejected movement command or a stale canonical-input tick mapping. The reason the client stopped batching remains unproved and is independently owned by Input/root. Checkpoint scheduling must not be presented as its established cure.

## Smallest bounded cadence proposal, not implemented

A successful publication currently adds the configured interval after the entire session task completes. The measured1.075s mean publication→ACK plus1.417s mean subsequent gap makes that completion-based delay worth a controlled comparison, while the input scheduling issue remains separate.

1. In the existing controller Poll branch, retain its single existing `options.Clock()` sample and pass that observed attempt-start time into `PublishCheckpoint`; add no clock call or deadline change.
2. Preserve the session's actual `Task<bool>` outcome. Only `true` qualifies for start-anchored pacing; a returned `false` remains a retry/backoff outcome. Do not equate no exception with a successful durable commit.
3. Retain the original `publishing` exclusion and same-Runner finally fence. On true, set the next eligible start to at least attemptStart+the existing interval and no earlier than the already-existing completion clock sample. This avoids an extra full interval after a slow successful commit without overlapping publications or issuing catch-up bursts within one Poll.
4. On false or exception, keep original completion+interval behavior. Preserve exact Fail handling, startup nextCheckpoint initialization, authority/epoch/term checks,120-tick provisional guard, deadlines, SDK configuration and service protocol. Runner replacement/Stop must not reschedule an obsolete attempt.
5. Meaningful regression must exercise the actual controller with a test session that defers publication completion: no overlap during the wait; fast success waits the remaining start interval; slow success is eligible at the next Poll; false retains full completion backoff; exceptions retain fault behavior; replacement/Stop before completion does not alter the new run or add a clock read. A formula-only mirror test is insufficient.
6. Compare the same exact public workload with the present diagnostic flags and separately inspect client SDK batching. Require unchanged authority/ACK correctness and record request count, accepted gaps, pause samples, observed intervals and real movement outcome. A shorter gap is only a pacing improvement; it is not acceptance of a fix for missing client batches, full FFA completion,8-peer memory, or60fps.

No implementation is authorized by this report itself. Root must choose the next scoped change after the independent scheduling investigation; no timeout increase, guard relaxation or network-authentication bypass is proposed.

## Decision after root review

Cadence implementation is on hold. More frequent publication would repeat the measured synchronous Publish and ValidateSnapshot work: approximately101.094+110.530ms per observed publication on average in this run, before unisolated preparation and transport work. Those are blocking-call wall observations, not isolated CPU measurements. Removing completion-based idle could therefore increase main-thread contention and request volume while the client already stops producing SDK batches. The current evidence does not show that this tradeoff is beneficial. Next priority is the existing operation-boundary SDK frontier/input state and actual HostStep/reconciliation costs; keep the present cadence and all guards unchanged until that evidence supports a specific fix.

# Native FFA and authoring review — 233437

STATUS: COMPLETE — evidence review; FFA acceptance remains FAILED. Read-only independent review of integration source `b36ce231c8420db67e64860e90c0707ca8bb3a1e`, `TestResults/editmode_20261023T233437Z.xml`, its matching gate log and `native-ffa-authoring-20261023.json`. No runtime, fixture, JSON or artifact changes; no jobs run in this lane.

Confirmed XML: 14 total, 13 passed, 1 failed, 0 skipped, 502.6748468 s. All 13 `WordboundAuthoringPersistenceTests` passed. The public FFA full-match case failed after 487.762312 s; its last movement operation ended after 5.999 s with `lifetimeCancelled=True`, so the generic “exceeded its deadline” assertion alone must not be read as expiration of that movement's own deadline.

## FFA reach and precise failure

The tested source sets an eight-minute fixture lifetime; `FullWalk` calls `Until` with 35 seconds. `Until` stops on either time limit or lifetime cancellation and uses the same final assertion message. The final operation began at canonical tick 14319 and ended at 14536, maximum sampled frame gap .269 s, with lifetime cancellation true (log line 16797). Thus the observed final movement ended because the enclosing lifetime expired, not because 35 seconds elapsed. This does not establish that it would otherwise finish successfully.

Automatic respawn generations 1–6 and their protection waits returned successfully. Their operation-end host ticks are 5183, 6890, 8703, 10444, 12251 and 14132; these are observation ticks, not necessarily the exact respawn ticks. The tested loop's score/health/generation assertions and subsequent control flow establish six scored knockouts, six respawns and the first hit of the seventh scoring cycle before the failing return. The ten-point result, replica confirmation and downloaded terminal checkpoint were not reached; `WORDBOUND_PUBLIC_FFA_FINISH_OBSERVED` is absent. These are scripted two-peer native observations, not human or independent-machine acceptance.

## Sampled allocation and shutdown

There are 81 `WORDBOUND_FFA_MEMORY` samples per peer. In the final 62 samples (canonical match tick 3990–14391), both peers retain 181 network objects; host object-used stays 956,672 bytes / 2,015 free blocks, client object-used stays 14,405,888 bytes / 1,595 free blocks, out of 2,048 reported blocks. Client general-used rises 5,280→6,048 bytes in this interval, so not all fields are flat. Earlier client object-used grew from 3,305,728 bytes while history filled. The raw client `objectFree=-13019592` is preserved as an SDK diagnostic field; do not reinterpret it as validated available capacity or use it to claim a safe memory margin.

No `OutOfMemory`, out-of-memory, allocator-exhaustion or allocation-failed text occurs in the matching log. This supports no observed repeat of that failure during this run, not leak freedom, a maximum-capacity proof or successful full-match endurance. Sampling is roughly five seconds and cannot establish every transient allocation.

Service cleanup reports `failures=0; return=0; cancel=0; leave=0; clients=2; parties=2` (line 18296). The wrapper finished normally with exit 1 for test failure; the gate has no infrastructure error and `crashAfterQuit=false`. Unity writes its normal test-failure exit and shutdown, with no leaked weak pointers reported. Preserve the tail caveat: Mono abort-thread/debugger-listen messages and the standard final memory-label record exist. “Clean shutdown” here means no observed native crash and completed fixture/service teardown, not a warning-free or allocation-free process.

## Actual authoring and Windows IO evidence

All 13 authoring cases passed, including saved course/profile/prefab/material reload, theme value/font persistence, existing custom-theme preservation and empty-slot default assignment, explicit changed-course recovery, damaged/invalid pending forms, scene restoration, locked recovery and repeated draft recovery. Exact individual names/durations are already in the parent JSON.

The IO diagnostics below are in the XML test-case outputs (not all are Unity log lines). They report only state flags and HRESULTs:

| Native operation | Actual result |
| --- | --- |
| Actual Store, changed content while target is read-shared | Expected `IOException`, `0x80070020`; old disk bytes and saved asset preserved; newest draft recoverable in memory. |
| Actual Store after release, then BOM normalization | The strict case passed both saves, cleared memory-only state/temporary file and verified exact expected bytes; failure-only diagnostic lines are absent. |
| Store's actual comparison + retained temporary rewrite + direct replacement after release | `type=none`, `0x00000000`, new target true, temporary absent, no backup. This direct IO control deliberately does not execute Store success bookkeeping. |
| Null backup, fresh / released | Both succeed, new target true, source absent, no backup. |
| Named backup, fresh / released | Both succeed, new target true, source absent, backup contains old bytes. |
| Null and named backup, locked | Both expected `IOException`, `0x80070020`; old target and new temporary retained; no backup created. |

This closes the previously failing release/BOM sequence for this recorded Windows native run; it does not erase previous failures or establish arbitrary concurrent-writer recovery.

Artifact hashes independently recomputed and matching the parent JSON: XML 455,886 bytes, SHA256 `1d2a363b6f43014beb5e6671c4458c32e44414e2c8fc680fa6a86d030a241ac1`; log 2,885,885 bytes, SHA256 `267a1481b9bb37f899d8fd575921d85744c2ed94351f4eca1a7eb937159496df`. Parent JSON/root artifacts were left unchanged.

# Fusion FFA allocator review

STATUS: COMPLETE — read-only attribution and bounded capacity proposal. No SDK, heap, runtime, test, configuration, service or root-worktree edits; no compiler/native job was run by this lane.

## Actual failure and memory trajectory

Root executed `native-ffa-memory-2221` on source `7ff66792`. The job finished at 2026-10-23 22:26:27 UTC with exit 1. `TestResults/editmode_20261023T222105Z.xml` contains one failed test, zero skipped, duration 280.595890 seconds: `TwoAuthenticatedPublicFfaIndividualsReachScoredWinAndConfirmDurableResult`. Its recorded failure is the unexpected Fusion allocator exception requesting 1,260 bytes. This is not a scored-finish pass, even though host gameplay continued after the first logged exception.

Evidence: root `.harness/delivery/native-ffa-memory-2221-{job,status}.json`, `Logs/gate_test_editmode_20261023T222105Z.log`, and the XML above. The diagnostic samples every five observed seconds during the FFA behavior phase. It is not a continuous allocation trace or an isolated network-process comparison.

| Match tick | Host/client Fusion tick | Objects host/client | Host allocated segment bytes / free blocks | Client allocated segment bytes / free blocks |
| --- | --- | --- | --- | --- |
| 286 | 1051 / 1053 | 179 / 179 | 946,432 / 223 | 1,888,768 / 194 |
| 463 | 1358 / 1360 | 177 / 177 | 936,192 / 223 | 4,626,688 / 108 |
| 660 | 1658 / 1662 | 176 / 176 | 931,072 / 223 | 6,015,488 / 64 |
| 891 | 1961 / 1965 | 174 / 174 | 920,832 / 223 | 8,101,888 / 0 |
| 7790 | 13789 / 13794 | 181 / 168 | 956,672 / 223 | 8,101,888 / 0 |

Each allocator reported 256 total blocks. The first OOM occurs before the fourth row. The client subsequently has fewer objects than the host; its full allocator prevents treating that later state as successful replication. Both Fusion tick counters advance, so an entirely frozen client simulation clock is not demonstrated.

The exception path is `StateReplicator.ReadObjectUpdate_ApplyData -> NetworkObjectMeta.NextSnapshot -> NetworkObjectHeaderSnapshot.Clone -> NativeAllocObjectSnapshot -> Allocator.Alloc`. This directly attributes the failed allocation to received network-object snapshots. It is not the managed canonical JSON codec or reliable-chunk array allocation path. The decreasing early object counts oppose an ever-growing live-object explanation.

## Installed SDK proof, rather than a render-setting guess

Read-only Mono.Cecil metadata/IL inspection used the existing `Library/PackageCache/com.unity.nuget.mono-cecil@ecb9724e46ff/Mono.Cecil.dll`; no SDK code was executed or rewritten. Inspected `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll` SHA-256: `d213b95e4694c167f5f6faffcbd75ca1e47f19a9249c91a4964f8342fac9b560`.

- `NetworkProjectConfig` constructor sets `InvokeRenderMode = 1` (`Always`, IL0032–0034). `RenderInternal` admits that mode in batch mode, and `InvokeRenderInBatchMode` returns true. Both project configuration factories construct a fresh config with this default; the saved global config is also 1. Setting Always again would be a no-op, not an established fix.
- `NetworkObjectMeta.NextSnapshot` checks the list count against the simulation tick rate (IL002e–0056). Below that count it clones the latest snapshot; at or above it, it removes the oldest, copies the latest into that allocation and reinserts it (IL005d–00a9). At the configured 60 Hz this is a bounded list of up to 60 snapshots per object, not an unbounded list. It can still exhaust a shared heap before every object's list reaches its bound.
- The other mutation sites of that snapshot list in this installed DLL are `PoolReset` and the negative-tick cleanup inside `NextSnapshot`. `PoolReset` releases the current/shadow/previous/migration allocations and every history entry. `FindSnapshot` only searches. Ordinary interpolation does not release old entries from this native snapshot list.
- `TryGetSnapshotsBuffers` invokes `RenderTimeline.GetRenderBuffers`, which invokes the client's on-demand `UpdateObjectInterpolationParams`. That operation updates timeline/interpolation metadata. It is not a passive diagnostic getter and does not drain the native object-history list. `RemoteRenderTime` and `LatestServerTick` are forwarding scalar getters suitable for a later passive timing observation, but an additional render probe is not required to establish the list-capacity calculation.

Photon's release notes describe the on-demand interpolation change and the default enabled interpolation flag. Disabling interpolation changes retained-state behavior; it should not be applied indiscriminately to moving physics bodies merely to make this test pass. [Photon Fusion 2.1 release notes](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/release-notes-2-1).

## Exact object and allocator sizes

The three actual body prefabs have three networked behaviors and flags 262402. The current woven assemblies report `WordboundNetworkBody = 202` words, `WordboundGameplayBody = 69`, and `NetworkRigidbody = 21`. `NetworkObject.GetWordCount` adds the 20-word header and one change-tick word per behavior: **202 + 69 + 21 + 20 + 3 = 315 words = 1,260 bytes**, matching the exception. The four fixed-capacity network strings account for substantial state, but reducing their layout would be a separate compatibility and identifier-bound change.

`Allocator.Config` defaults are block shift 15 and count 256: 32 KiB pages and 8 MiB usable capacity. `Simulation` constructs **two** allocators from the same heap configuration: general and object allocators. The installed constructor allocates each immediately; each has eight additional alignment bytes. Other Unity, managed, transport, physics and graphics memory is additional. This installed proof is more precise for this SDK than interpreting the generic documentation as a total-process budget. [Photon allocator configuration API](https://doc-api.photonengine.com/en/fusion/current/struct_fusion_1_1_allocator_1_1_config.html).

The installed allocator has eight-byte words. Its size table rounds a 1,260-byte request to the 160-word bucket, **1,280 bytes**. A 32 KiB page holds 25 such segments, leaving 768 bytes of page slack. The table below is body-state capacity arithmetic, not observed peak memory or an FPS claim:

| Body count | 60 history snapshots: raw payload | History pages / usable page capacity | Conservative 66 segments per body: pages / capacity |
| --- | --- | --- | --- |
| 174 | 13,154,400 bytes | 418 / 13.0625 MiB | 460 / 14.375 MiB |
| 178 | 13,456,800 bytes | 428 / 13.375 MiB | 470 / 14.6875 MiB |
| 512 | 38,707,200 bytes | 1,229 / 38.40625 MiB | 1,352 / 42.25 MiB |

The 66-segment allowance includes 60 history entries, four auxiliary snapshot slots, current data and change ticks, conservatively charging each at the body's 1,280-byte bucket. It is a planning envelope, not proof that all those slots coexist for every object or that all SDK allocations have been enumerated. SDK internal objects, the election carrier, other bucket/page slack and headroom remain additional. `WordboundEntityBudget.MaximumBodies` is 512 and full snapshot validation enforces it.

The election carrier is one object per election, not a carrier per canonical checkpoint. Its woven behavior itself has 1,294 words, so it is materially larger than a single body state; include it and SDK internal state in the headroom. `SeedElection` despawns previous carriers before replacing the election marker. Regular committed snapshots use reliable chunks.

**Statistics caveat:** `GetMemorySnapshot.TotalBytesUsed` sums allocated segment capacity (`SegmentsAllocated × SegmentStride`), not strictly currently live bytes. Its `TotalBytesFree` calculation subtracts the aggregate allocated segments from one bucket's per-page segment capacity; this explains the observed negative numbers and the constant 1,386,296 used-plus-free sum. Do not use that negative field to compute heap utilization. Free-block counts and the actual allocator exception remain direct evidence; bucket used/free segment counts would support a more precise follow-up.

## Project lifetime and capacity conclusion

`WordboundGameplayController.Reconcile` compares canonical IDs: retained IDs reuse the same NetworkObjects, missing IDs are unregistered/despawned, and only new IDs spawn. `RetireInactiveResources`, collection and destroyed-item compaction remove bodies through the existing canonical lifecycle. Snapshot publication/reconciliation frequency does not respawn the whole world. These source facts and the observed falling object count reveal no project live-object leak in the traced path. They do not certify every lifecycle or recovery case.

The measured client growth and the installed bounded history algorithm together support **insufficient configured object-heap capacity for the supported body population** as the smallest justified correction to evaluate. Eight MiB cannot contain even the ordinary 174-body full history; 16 MiB may fit that particular scene but cannot cover the admitted 512-body maximum, and 32 MiB is below the maximum's history-only requirement. No interpolation disable, SDK modification, slower tick rate, deadline extension or ignored OOM is justified by this evidence. Photon explicitly documents heap sizing and an OOM when its budget is exhausted. [Photon memory optimization guidance](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/optimizations#memory-optimization).

## Bounded next change and verification proposal — not implemented

1. Use one shared production/test configuration policy, including reconnect-created runners, with 32 KiB pages and a **64 MiB per-heap candidate** (2,048 pages). This exceeds the 42.25 MiB conservative body envelope with room for the carrier, SDK objects and fragmentation. Preserve the 60 Hz simulation, default interpolation, all protocol/authentication/validation rules and existing deadlines. Read back the actual runner configuration. Do not raise only the FFA test heap or infer success from configuration alone.
2. A 64 MiB setting means approximately **128 MiB of these preallocated heaps per Runner**, versus 16 MiB currently: +112 MiB per Runner. Two same-process peers use approximately 256 MiB for these heaps; eight use approximately 1 GiB, before the rest of Unity. A normal separate-process player generally has one active Runner, but overlap during recovery must also be measured. The SDK uses a common configuration for both heaps, so the mostly empty general heap also grows. This is a real memory tradeoff, not debloating.
3. First rerun the exact full public FFA behavior with the existing memory observation and unchanged budgets. Require no unexpected OOM, all ten real KOs/nine respawns, confirmed exact durable result/ACK and clean service/runner teardown. Observe memory through the complete case; reaching a flat line only because the allocator is full is not a plateau pass.
4. Add a bounded capacity regression using the actual three baked prefabs: `NetworkObject.GetWordCount` must stay within the budgeted body layout. Exercise the admitted upper body count with real client replication long enough to populate the history, then despawn/recreate and verify available blocks/segments recover rather than growing across cycles. A static arithmetic assertion alone cannot prove SDK reuse or a full eight-peer run. Keep the existing separate 4/8-peer and migration/rejoin acceptance requirements.
5. If the increased budget still exhausts or the observed client exceeds the planned envelope, stop treating it as merely default capacity. Next passive observations should include actual config, per-bucket used/free segments, object count/word-count distribution, latest server tick and remote render time. Do not call `TryGetSnapshotsBuffers` solely for telemetry, since it changes interpolation metadata. Do not repeatedly raise the budget without this discrimination.

No new native, Windows-player, capacity, multi-peer or reconnect acceptance was performed by this lane. Root owns the implementation decision and all execution. The completed diagnostic is retained as a failure, not suppressed or converted into expected-log success.

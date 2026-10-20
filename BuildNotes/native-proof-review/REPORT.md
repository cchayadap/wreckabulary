# Native migration proof review

STATUS: SOURCE FOLLOW-UP COMPLETE — N1 CLOSED; NATIVE TEMPORARY-CARRIER PROOF PENDING

Read-only review of integration `Assets/_Project/Tests/Photon/WordboundPhotonMigrationTests.cs` and `Assets/_Project/Editor/Online/WordboundFusionSetup.cs`, their shared cleanup, and installed Fusion SDK semantics. These are active root-owned files; capture hashes for reviewed versions. Integration HEAD at entry: `bf8a31b`.

Focus: false passes, hangs, cleanup and state identity in the two-peer cloud recovery experiment. This reviewer does not launch Unity, mutate native assets or implementation source, or claim live Photon results. Findings will be recorded as confirmed; independent verification of the online host-proof correction takes priority when its final source is ready.

## N1 — P2: pre-checkpoint tick wait can hang indefinitely

Initial migration-test source SHA-256: `EEF827E234ACC1C9555C143F1CE8DD91921AD663FFB4EF45D8750ABB5CE3C4D0`. Immediately before `PushHostMigrationSnapshot`, the test used:

```csharp
int writtenTick = host.Tick.Raw;
while (host.Tick.Raw <= writtenTick) yield return null;
```

If the host stops or its simulation stops advancing after replication, this loop does not consult the 180-second cancellation source or any deadline. It can leave the coroutine waiting instead of failing and reaching teardown. This is directly established by the loop condition; no Unity hang was induced.

Sent the finding to root. Root added a 10-second monotonic deadline, host-running and cancellation checks, followed by an explicit tick-advanced assertion. Corrected source SHA-256: `13676A4D1E78C447B5F8195C4A3D782122E759382D827875D123B8AE4AFAE056`. Independently reread that loop and confirmed the stalled-host path now exits to a failing assertion. Native execution remains root-owned.

## Other coverage

Setup source SHA-256: `7B9640325DDB743C6F1FF2B0288B6FDF287B9A9BEEDBACDBE0A45FDE6E3C69B4`. Shared connection-test/cleanup source SHA-256: `CC8F4DBA46EFA6E2196D1836EEA15EC998AB6C75E846A23028646E8122F824F5`.

| Boundary | Source checks and conclusion |
| --- | --- |
| Cloud checkpoint evidence | Test checks complete client chunk replication and exact bytes/stamp, then requires successful explicit push before original-host shutdown. Resume reads `GetResumeSnapshotNetworkObjects`, compares recovered JSON byte-for-byte and every stamp field, and semantically imports the canonical fixture. It does not simply reuse the original in-memory JSON as recovered output. No additional false-pass path confirmed here. |
| SDK semantics | Installed official `Fusion.Runtime.xml` documents `PushHostMigrationSnapshot` as computing/sending a snapshot to Photon Cloud with success result, `GetResumeSnapshotNetworkObjects` as old objects from the resume snapshot, `HostMigrationResume` as new-host restoration, and `NetworkObject.CopyStateFrom` as copying the entire state. The test creates a fresh runner after shutting down the old client, consistent with the SDK's one-use-runner rule. No decompilation or external service call by reviewer. |
| State identity | Restore requires the complete chunk set, consistent match/binding/epoch/sequence/tick/state/wire hashes and unique indexes. Canonical case uses the actual course/mode/catalogue snapshot codec and newer epoch restore. The stress case is explicitly synthetic and reports `completeMatchValidated=false`; it is transport capacity evidence only. |
| Time bounds | Startup/shutdown/replacement loops use cancellation; replication, migration-token and push loops have monotonic deadlines. N1 was the confirmed unbounded wait and is source-fixed. No test process or native scheduler was exercised by this lane. |
| Cleanup | Migration delegates `[UnityTearDown]` to shared cleanup. It selects only named proof runners, shuts clients before servers, bounds each shutdown wait, destroys remaining proof objects, exits play mode and restores saved build scenes. This is best-effort cleanup: returned shutdown tasks are not fault-asserted. The body JSON PASS file is written before teardown; full NUnit result and post-run cleanup are required to accept a run. No concrete surviving-peer leak was observed or reproduced here. |
| Native setup | Read official editor `NetworkObjectBakerEditTime`, prefab-table rebuilding and prefab-ID lookup implementation. Setup adds the assembly names/config, loads or creates the dedicated prefab, checks required components and absence of renderer/collider content, bakes/saves, rebuilds the SDK table and checks a valid ID. Membership in `AssembliesToWeave` alone does not prove actual weaving; root's native compile and carrier test remain necessary. No additional confirmed setup defect. |

The experiment proves only its actual packet size and tested lifecycle. It does not establish the full 256 KiB ceiling, deployed custom authentication, the production `WordboundFusionSession` callback wiring, reconnection of a second peer after replacement, ongoing player/item simulation, or physical gameplay recovery. The random room and transport IDs are separate from the canonical fixture's fixed domain roster; authenticated end-to-end roster mapping is explicitly outside this proof and remains with the separate admission gate.

Only this report was written by this reviewer. Root owns the N1 source correction, native SDK/weaving changes and live test results. No Unity launch, tests, package changes or implementation edits occurred during this review.

## Temporary migration carrier follow-up — 2026-10-20

Root requested independent review after native evidence showed exact client replicas but a mixed/incomplete large cloud recovery. The suspected cause is temporary component/state reuse while iterating `GetResumeSnapshotNetworkObjects`. Root now copies carrier fields and payload during each yield, then rebuilds fresh carriers from validated recovered packet data. This follow-up reviews the active changes; native proof is still pending.

Entry pins: integration base `80292513e9571c3eda816988843d2062be093b9b`, active `WordboundFusionSnapshotChunk.cs` SHA-256 `91527191DBAEAEE1DEEDCEE1CE2D3692C560396A1715805044906ED006D8CF9E`, active `WordboundPhotonMigrationTests.cs` SHA-256 `2A1338F855DA67D7D6F5AB2883BB06282304B3F56F7016006AADDF605AA448B2`. Online production caller is committed as `8cc5cb0`, session source SHA-256 `F07B4BD311087C1EB5B4F99C01B52E40B5D1D8066C5997AADF61D41649A8355C`.

No new confirmed source defect found in this change. The following coverage is specific to these versions:

| Boundary | Independent source conclusion |
| --- | --- |
| Temporary lifetime | `Take(...).Select(c => new SavedChunk(c)).ToArray()` executes the value-copy constructor before the iterator's next `MoveNext`. It copies Index, Count, TotalBytes, both hashes, immutable snapshot mark and a newly allocated payload byte array. Sorting/validation afterward touches only those copies. No component, NetworkArray view or temporary NetworkObject is retained for subsequent reconstruction. |
| Production enumeration | `8cc5cb0` removes caller-side `.ToArray()` and passes the lazy selected carrier sequence directly to Restore. Both this caller change and the new SavedChunk implementation must be integrated together. Fixing the callee alone cannot recover values already lost by a materializing caller. |
| Complete set and bounds | At most 65 matching chunks are copied before enforcing the 64-chunk limit. Per-payload reads are bounded to 1–4096 bytes. Equal count/total/wire hash/exact mark, unique in-range indexes and exact array count imply every index is present once. Packet restore then checks total length, exact chunk lengths, magic, wire SHA-256, at most 8 MiB inflation, exact raw length, raw SHA-256 and strict UTF-8. Malformed/null/missing/mixed/duplicate data throws; partial restored output is not accepted. |
| Canonical trust | The carrier reader validates transport consistency; it does not independently bind the mark's state hash to the domain JSON. The production caller performs expected match/identity comparison and `authority.ValidateSnapshot(json, mark)` before `RestoreCommitted`. Its failure path invalidates the active run and freezes transport/authority. The canonical fixture separately imports/validates and restores at the next epoch. |
| Re-emission | The test compresses only `recoveredJson` and writes the recovered mark into newly spawned carriers; it no longer copies raw temporary NetworkObject state. It compares exact original/recovered JSON and mark, then checks rebuilt carrier count and restores/recompares those actual replacement-runner carriers. It does not feed original `json` into reconstruction as a fallback. |
| Test materialization | The test's post-spawn `.ToArray()` reads persistent replacement objects found in the scene, not the resume iterator. The resume path itself remains lazy. The reuse diagnostic retains the previous component only for `ReferenceEquals`; it does not read its fields later. |
| Native evidence limits | Installed SDK documentation describes the resume-object iterator and a temporary host snapshot data buffer, but does not specify per-prefab component lifetime. Source reasoning establishes that immediate value capture is safe under reuse; it does not prove this SDK actually reuses the same component. Root's native `temporaryReuses` and restored-chunk observations must establish that. The count is diagnostic, not an acceptance condition tied to a particular SDK implementation. |

No Unity launch, rebuild, SDK mutation, cloud request or fabricated native result was performed by this reviewer. The source-level repair is coherent and preserves fail-closed validation. Native proof must still show both exact canonical recovery and the large chunked case, including rebuilt carriers and full NUnit/cleanup results. Existing limits remain: body PASS JSON precedes shared teardown; the experiment does not test authenticated migration, replacement-client rejoin or dynamic gameplay recovery.

Next recoverable action: root integrates the lazy production caller and value-copy reader together, runs the native canonical/large tests, records exact resulting source commit and reuse diagnostics, then independently binds those results to this review. A useful later pure regression would yield one mutable component repeatedly with different fields and require exact reconstruction; such a double would test enumeration discipline, not prove native SDK allocation behavior.

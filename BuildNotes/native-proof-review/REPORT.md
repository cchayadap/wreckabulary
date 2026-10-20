# Native migration proof review

STATUS: COMPLETE — ONE HANG FINDING, SOURCE CORRECTION VERIFIED

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

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

## Controller shutdown follow-up — 2026-10-21

Scope: independent read-only review of the small `WordboundGameplayController.Stop` / `Fail` change and its original six actual-controller test cases in the online lane. No Unity, compiler, cloud, or production source writes by this reviewer.

Confirmed defect in the initial pending patch: `CleanupRunner()` could throw from the application `LocalPlayerChanged(null)` callback before `session.Stop()` ran. Because the new Stop task was cached, every later caller received the same fault without another shutdown attempt. A concrete probe sets `localBody`, supplies a throwing callback, calls Stop twice and observes zero session shutdown calls. The online owner confirmed the defect and added two regressions.

Independent source reinspection at **17f5481** confirms `StopCore` now catches cleanup errors, always awaits the session shutdown, and completes the shared task only afterward. Cleanup and shutdown failures are both retained through aggregation; sole cancellation remains cancelled. This resolves the identified ordering defect at source level. The owner reports reference compilation green and eight tests authored; this reviewer did not execute those native tests. No conclusion is offered on unrelated destruction or migration paths.

## Windows profile and workshop QA source review — 2026-10-21

STATUS: BOUNDED SOURCE REVIEW COMPLETE — PLAYER BUILD/EXECUTION NOT RUN BY REVIEWER

Reviewed the active integration source at base `724513276c991ff82b05c50ac01c90ccac939629`. Exact SHA-256 pins: `Editor/Online/WordboundBuildProfiles.cs` = `1C7708A9D01D885D6975D2807028A17747DBF91A270A236B68279C6A24E01005`; `Scripts/WordboundQa/WordboundWorkshopQa.cs` = `B238B698D9D69138E4D40D09BE65AD05512F8CDF06D146690F66EB6FB448F8D0`; its asmdef = `08521D02946ADBC465DB0649D123D50B92782842E6B9BDCD98CD3D9738BACDD7`.

No concrete false-pass or draft-loss defect was found within this bounded review. The profile helper refuses an unexpected editor version, absent Windows module, active play/build, global QA define, absent required scenes, conflicting existing asset, or incompatible existing profile. New profiles use global scene order, explicit release/QA defines and development settings. It checks active-profile identity after creation and does not intentionally change global scene settings. This reviewer did not independently execute the private Unity factory or prove profile asset serialization; root's installed-editor metadata inspection and eventual native build remain separate evidence.

The QA scenario calls real lobby/workshop handlers, then uses the actual history transaction and exact Preview/RequestPlaytest flow. A new workshop initially uses default storage, but it is unchanged and is replaced with the isolated QA folder before the coroutine next yields; no default autosave write is triggered on this fresh path. The test waits for the real local controller, course world, frame, unpaused state and nonempty guide, checks exact manifest and recovery JSON, awaits Return, checks client/camera destruction and the same visible history, then checks exact Undo/Redo content. All action waits have deadlines; the QA runner records failed steps and has a final run watchdog. No synthetic successful gameplay state is supplied.

The scenario directly invokes action handlers and draft transactions; it does not establish mouse/controller usability, physical puzzle completion, image readability, shipped release stripping, or a successful standalone player build. Screenshot capture and actual Windows execution remain pending. This review made no production edits, launches, builds, service requests or credential changes.

## Entity-budget integration hooks — 2026-10-21

STATUS: BOUNDED SOURCE REVIEW COMPLETE — COMPILE/TEST PENDING

Reviewed the six active hook/test files against integration base `63f7e8689e4ec6bd3798ea22e3913b301e5150d6`, reading the domain owner's pending `WordboundEntityBudget` public contract for compatibility. The helper was not yet integrated or compiled in the root checkout. This review covers call placement and identity binding, not independent acceptance of the whole resource-retirement algorithm.

No concrete mismatch was found. `WordboundAuthoredPuzzleContent` preflights the exact resolved course, mode, geometry, item catalogue and derived rules before constructing its signed content hash; it is the common path for local authored maps and race/Freestyle party content. The separate FreeForAll content branch now performs the same check before creating either binding. Fingerprint version 2 length-prefixes the budget policy hash, which both content branches already include through the effective-rules hash in CatalogueHash. Consequently a policy change produces a different admission identity even when item/rule JSON is otherwise unchanged. Reconciliation's bounded enumeration and snapshot count check, plus Physics.Register's pre-add count check, use the same unchanged 512 maximum.

The two local excess-reserve cases and one arena case insert `maxLetters` into the actual Dibs override used to derive these modes, first verify ordinary rule-book validity, and assert the specific budget error. They do not create a runner or party. These are meaningful preflight cases; their result remains unproven until executed. Existing physics, full resource pressure, cloud admission and native startup are outside this source-only hook review.

Exact local SHA-256 pins:

| File under Scripts/Online | SHA-256 |
| --- | --- |
| `Puzzles/WordboundRulesFingerprint.cs` | `DA83EB512D32C191CDD4AA1F27B7C1DDA55C92AF467C287D53B3F7FE96DE1ACD` |
| `Puzzles/WordboundPuzzleContent.cs` | `D5A2A46DF93FA951F27EF207726678D4B5CE25FC3F96CA8CAEDC34783AD1A217` |
| `Client/WordboundClientContent.cs` | `80A564AD6D7FDA97AC9FEF04F3374E69175A73B4E8DFF069EAA9234CCCF732DD` |
| `Gameplay/WordboundGameplayPlan.cs` | `3BD8538D44BA0739B321FB6CB83902DFE1E3CA8C0DD25F07F8AD105918E2CC16` |
| `Physics/WordboundPhysicsWorld.cs` | `E54371A18D38C823CE9D66DDD101000C7465438D882D3E803BEE5EB1099DB490` |
| `Client/Tests/WordboundWorkshopLaunchTests.cs` | `3782617A54E200137FE04EB6CE6549A5A319657F03C668C368294A87C2AED313` |

No root/domain source writes or compiler/native process were performed by this reviewer.

## Production-content multiplayer fixture follow-up — 2026-10-21

STATUS: BOUNDED SOURCE REVIEW COMPLETE — TWO ASSERTION GAPS REPAIRED; NATIVE NOT RUN

Read-only review requested for the online owner's frozen new eight-peer flagship TeamRace/Squad composition and four-peer flagship BrawlRace/Duo graceful-departure cases. Scope is exact private party/roster formation, actual elected survivor selection, physical phase recovery and false-pass boundaries. No compiler, Unity or external requests are authorized in this review lane. Exact initial/final source pins and bounded conclusions follow.

Initial source pins at online base `96fa195e80905a3bc8c5f7d20887ddb55cba6f40`: `WordboundLiveMatch.cs` SHA-256 `53B272635B9EA0A59AA6A18DA88A6C16DE9D0446D48C0C017127927ED266F511`; `WordboundNativeGameplayTests.cs` SHA-256 `DAC4A9943026E69584A61DD3B544B2DD85B2E459E1DD4D561DFAFB79D4BFA54B`.

Confirmed **F1 (P2) test-proof gap: eventual motion comparison can miss a reset**. The initial fixture records a committed replica's positive MotionUnits before host departure, but compares them only after every survivor is Ready and has the next epoch. The elected replacement may advance while other survivors reconnect. A faulty restore that starts motion at zero can therefore catch up during that readiness interval and satisfy the eventual `>=` assertion. This is a concrete assertion-order gap, not a reproduced production restoration defect. The domain allows up to 1,000 motion units per tick, and the wait permits 75 seconds. Recommended repair sent to the owner: capture the replacement host's first new-epoch FrameChanged/BeginHost frame before authoritative advancement and check that frontier there; keep eventual body/ACK checks separately.

Confirmed **F2 (P2) test-proof gap: whole-party assertion does not compare original membership**. The initial `Prepare` assertion checks two teams, one party label per team, equal counts, two distinct party labels and the complete guest set. It does not compare signed PartyId/member sets to the concrete `parties` groups created earlier. A wrongly reassigned set of guests with two equal labels can pass. Recommended repair: for each original PartyGroup, require its exact ID and exact authenticated member set in the signed roster, as well as one team and a distinct team for the other party. Both findings were delivered directly to the online owner and root. Reviewer made no source changes.

The owner implemented both repairs. Independent source reinspection confirms the fixture subscribes before Stop, captures the first frame from each survivor's new server Runner at exactly the next epoch, saves its authoritative-advance count, then compares the chosen replacement's saved physical frontier with that count equal to zero. Current production `BeginHost` emits this `FrameChanged` through Reconcile before `hostReady = true`; subsequent catch-up cannot change the immutable saved snapshot. Signed-party assertions now match every original PartyGroup.Id and its exact authenticated player set, retaining team uniqueness and complete-roster checks.

Final reviewed local SHA-256 pins: `WordboundLiveMatch.cs` = `E3B002346BC1E118AE0AB9CA9C3EFEB67E9EAB8EA4348C24DBBB47FC46D0A595`; `WordboundNativeGameplayTests.cs` = `41EF96544E41E71C95AF37AD109E644F468E82EA4FDE006902CBB9310E76EE5E`; native test asmdef = `326597619B4C28D322717F6D551055447158863FB91B0AC8168ED0B7D8331B74`.

Owner froze the same reviewed hashes in **a5ea59fb098505916ed5c9117453592e7511202b** and reports separate-assembly installed-reference compilation passed with zero warnings/errors. This reviewer did not run that compile. Native execution remains NOT RUN.

No additional concrete blocker was found in the remaining bounded source review. Replacement selection uses an actual new running server Runner among original survivors and requires one eventual host; every survivor must reach the next domain epoch. Checks retain exact authenticated survivor identities, disconnected prior host, complete canonical/native body IDs, one prior command receipt, and a fresh surviving-client command with exact new-epoch ACK. Physical counters wrap the actual production runtime callbacks. Timeouts/cancellation remain bounded, and cleanup failures fail the test rather than becoming successful body-only evidence.

Compilation and native execution of these new cases remain pending with their owners. This source review does not establish abrupt-kill recovery, cross-device behavior, adverse networking, all-family puzzle completion or full three-lap playability.

## Standalone QA startup review — 2026-10-21

STATUS: BOUNDED SOURCE REVIEW COMPLETE — STARTUP ORDER REPAIRED; PLAYER RUN PENDING

Root requested a second bounded review specifically for player-build inclusion/stripping and scene boot after native profile creation/readback passed. The prior profile/action-flow review did not execute a standalone player.

Confirmed **P1 QA startup order defect** in the current source: `WordboundWorkshopQa.Register` and package `QaRunner.Bootstrap` both use `RuntimeInitializeLoadType.BeforeSceneLoad`. Bootstrap calls `StartCoroutine(Run(...))`; Run resolves `QaScenarios.Create` before its first yield. If Bootstrap executes first, this scenario is absent from the registry and has no `QaScenario` discovery attribute, so the player reports an unknown scenario and exits. Editor checks do not exercise this path because Bootstrap returns when Application.isEditor. Unity explicitly does not guarantee ordering within one callback phase ([official Unity runtime initialization API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html)). Sent root a minimal owned-source repair: register at AfterAssembliesLoaded, after the package registry's SubsystemRegistration reset and before its BeforeSceneLoad bootstrap.

Root applied that repair. Independent source reinspection confirms `Register` now uses AfterAssembliesLoaded. Corrected QA source SHA-256: `4B6C05284CC6FDAFEA958C8361CEB2C03DE3F6425C884C7B797B29F7655F38A8`. The pre-fix root source checkpoint is `737728c`; the unchanged profile helper remains SHA-256 `1C7708A9D01D885D6975D2807028A17747DBF91A270A236B68279C6A24E01005`, and QA asmdef remains `08521D02946ADBC465DB0649D123D50B92782842E6B9BDCD98CD3D9738BACDD7`.

No additional confirmed player-build blocker was found in the bounded remaining coverage:

- The source and asmdef both require UNITY_EDITOR or GSK_QA, without Editor-only API use in the scenario. Registration directly constructs the scenario and uses the runtime-initialize entry point. This is coherent source inclusion/retention, but does not replace inspection of the actual built assemblies.
- Native-created Win-QA has GSK_QA; Win has no profile QA define, and global Standalone defines contain no GSK_QA. Both retain the global scene list. Profile-specific defines require building with the selected QA profile; creating its asset alone does not activate it ([official profile defines API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Profile.BuildProfile-scriptingDefines.html)). No claim is made that a player has already been built from it.
- Scene index 0 is Hub. LobbyMenu installs a runtime scene hook plus AfterSceneLoad fallback to create the real lobby component. OpenWordbound uses the registered WordboundLobby scene, whose serialized launcher script GUID matches WordboundLauncher. The registered gameplay scene and Resources/GameData plus Resources/Wordbound/LaunchAssets are present; the latter carries the actual body-prefab/trust references.

Root reports native profile creation/readback and source/native compilation passed. This reviewer did not run those gates or launch/build a player. The corrected registration's standalone startup, actual QA assembly inclusion, release exclusion, rendering and captured UI remain pending player-build/runtime evidence. Read-only inspection used local source/assets, bounded installed editor metadata reads and official Unity documentation; no SDK/source/asset mutation occurred.

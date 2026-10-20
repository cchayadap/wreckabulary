# Complete application checkpoint durability review

STATUS: COMPLETE — independent source review through `93b28e4`; native end-to-end durability acceptance remains open.

Read-only architecture review requested after native recovery test `editmode_20261020T133054Z` recovered only carrier 0 of 33. Scope is complete application checkpoint acknowledgement, upload/finalize/migration races, and bounded speculative progress. No Unity launch, SDK modification, private-reflection runtime access, service deployment or credential action is authorized in this lane.

Baseline integration: `95df5dcfde0e9600d7523bb092d61872e3c566a0`. Installed `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll` SHA-256: `D213B95E4694C167F5F6FAFFCBD75CA1E47F19A9249C91A4964F8342FAC9B560`. Proprietary IL is not copied into this report.

Independently read native log evidence: `Logs/gate_test_editmode_20261020T133054Z.log:699` reports one recovered carrier, index 0/count 33, payload 4096 bytes of total 132470, exact expected mark and wire hash. This establishes incomplete native recovery for that run. It does not itself establish a documented SDK acknowledgement contract.

Current production defect confirmed by source: `PublishCheckpointCore` replaces the previous carrier set, calls `PushHostMigrationSnapshot` once, and treats its returned true as `authority.AcknowledgeDurable(mark)`. With the above incomplete recovery, this is not adequate evidence for complete application durability. Acknowledgement and client distribution can advance while native recovery has only a prefix. `RestoreMigration` correctly rejects the incomplete set, but cannot reconstruct the claimed committed state.

## D1 — confirmed P1: local send success advances the durable frontier

Source locations at the pinned baseline:

- `Assets/_Project/Scripts/Online/Fusion/WordboundFusionSession.cs:401`: removes the previous native carriers before the replacement is proven durable.
- The same file, lines 419–422: one SDK push result immediately authorizes `AcknowledgeDurable`, then advances the committed mark and packet.
- The same file, line 473: recovery requires the complete, exactly bound carrier set. Rejecting a prefix is correct; it cannot recover a complete acknowledged checkpoint from a prefix.
- `Assets/_Project/Scripts/Rules/Wordbound/SnapshotCommitTracker.cs:60`: validates that an acknowledgement matches a pending publication, but necessarily trusts its caller's durability assertion.

Independent read-only inspection used Unity's installed Cecil library against the exact DLL above. The public push async method awaits the SDK send method. That method's worker obtains a server snapshot and invokes a void cloud-send method; the latter submits a message rather than awaiting a complete application-packet receipt. The server serializer uses a bounded buffer and rotates its migration-object list. Later sends are subject to last-snapshot/last-confirmed-tick gating. These are paraphrases of the inspected control flow, not copied IL and not a promise about undocumented runtime behavior.

The [public Photon API reference](https://doc-api.photonengine.com/en/fusion/current/class_fusion_1_1_network_runner.html) describes computing and sending a host migration snapshot. It does not establish the missing complete-application acknowledgement. The [official recovery sample](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect) describes host migration callbacks and restoration of the SDK's available snapshot objects. It does not add a complete packet commit protocol. The installed DLL evidence, rather than any documentation version label, pins this finding.

Reproduction evidence is the existing native run, not a new test performed by this reviewer. In `TestResults/editmode_20261020T133054Z.xml`, `LargeChunkedPacketSurvivesHostReplacement` fails after recovering 4096 of 132470 packet bytes. Root's log identifies one carrier of the expected 33. The mark and wire hash match the expected publication, so a marker alone cannot prove that its payload is complete.

A successful local send, arbitrary waits, repeated pushes without an explicit complete-set receipt, a marker written last, or a client's replica acknowledgement must not substitute for a durable application receipt. Packing everything into one oversized network object also lacks evidence of a supported complete-transfer guarantee.

## Proposed durable source and acknowledgement boundary

The online owner is implementing coordinator storage. This is an architecture review of that proposal; implementation and deployment are not yet verified here.

1. Keep immutable checkpoint bytes, their manifest, the current committed head, and exclusive writer ownership in separate SQL tables inside the same `MatchPool` Durable Object that owns match membership and Return. Do not embed blobs into the existing single pool JSON row.
2. Separate service writer terms from domain epochs. A successful claim yields an exclusive, expiring writer fence and the exact committed head under one transaction. A failed or expired claim must not consume a domain epoch. An established replacement restores the head and advances the domain epoch exactly once.
3. Stage an immutable upload bound to the match, identity, writer fence, upload ID, parent head, full mark, packet length, chunk count and wire hash. Retry of the same chunk is idempotent only for identical bytes. Different bytes at the same index or changing the manifest must reject.
4. Finalization reconstructs the exact set, validates bounded decompression and content hashes, then rechecks live writer ownership, its deadline, membership, terminal state and the unchanged parent head in the final transaction. Rechecking after asynchronous validation prevents a stale owner from committing after another claim or Return.
5. Atomically commit the complete immutable packet and replace the head. Retain the former complete head until the replacement transaction succeeds. Partial uploads never become the recovery source.
6. Return the exact durable receipt only after persistence is confirmed. The host must match that receipt to its pending publication and current Runner lifetime before calling `AcknowledgeDurable`, distributing a committed replica or confirming a result.
7. A lost response is an unknown outcome. Retry or read the stable upload/commit identity; never invent a second publication merely to infer whether the first committed. Returning the same receipt for the same immutable committed upload is safe; an expired writer may read its old receipt but may not create or extend a new commit.
8. On migration, freeze gameplay; claim ownership and retrieve the corresponding committed head; fetch every immutable chunk; run the full existing C# snapshot validator; restore the exact mark; reconcile terminal membership; then publish and durably acknowledge the first checkpoint of the replacement epoch before reporting gameplay ready.

Cloudflare documents transactional, strongly consistent Durable Object storage, output gating that withholds outgoing messages until previous writes are persisted, and `sync()` for explicitly awaiting pending persistence. Preserve those semantics; do not opt out with unconfirmed writes. Async hashing or decompression still requires the final transactional fence check. [Cloudflare SQLite storage documentation](https://developers.cloudflare.com/durable-objects/api/sqlite-storage-api/)

The initial writer should be the designated original match host. Define the empty-head case explicitly: a replacement must not pretend to restore a checkpoint that never committed, and a failed initial claim must not force an unsupported epoch transition. Original-roster membership and Photon connection authorization establish eligibility, not unique checkpoint ownership. Each seat currently has its own connection lease, so two roster peers can have different host IDs in their individual leases.

Keeping all relevant state in the same Durable Object is significant. Checking Return in one object, then committing a head in another, creates a time-of-check race. A separate storage object requires its own cross-object fencing design. A repeated membership read alone would not make the two operations atomic.

## Bounds, latency and recovery semantics

The current guard is **120 ticks / two seconds at 60 Hz**, from `ModeDefinition.ProvisionalWindowSeconds`, `ModeDefinition.TickRate`, and `SnapshotCommitTracker.CanAdvance`. It is not a two-tick guard. Slow or unavailable persistence should pause further speculative simulation at that boundary. It should not silently enlarge the rollback window or claim a result committed. Native verification must also establish that physics/commands stop consistently while the domain authority is paused.

The existing packet format limits raw JSON to 8 MiB, compressed data to 256 KiB, and each chunk to 4096 bytes. The generic service request limit is 16 KiB and the C# JSON response limit is 32 KiB. Four-KiB chunk routes fit these bounds, but up to 64 strictly sequential requests per publication are a latency risk. Use bounded concurrency or a dedicated bounded bulk route; prove the normal cadence against the two-second progress window. Do not globally remove request or response limits.

Keep the three hashes distinct: compressed wire hash; decompressed byte hash in the packet header; and canonical state hash computed over the snapshot envelope without its final hash field. The service may attest durable, exactly bound bytes while the existing C# validator remains responsible for full domain validity. It must not claim complete game-rule verification from transport checks alone.

A newer committed checkpoint can legitimately include a player who returned immediately afterward. Recovery therefore needs a current terminal-membership overlay before command admission; durable historical data must not revive a returned actor. If the writer itself returns, takeover can be immediate under the same transaction even when its old 15-second writer lease has not elapsed. Otherwise lease expiry bounds failover time; that delay is separate from the two-second speculative gameplay allowance.

Coordinator durability fixes the source of application state. It does not independently guarantee Photon host election, migration-token availability, successful Runner recreation, or physics/body reconciliation. Those remain native acceptance gates. Native carriers may support routing/transport recovery, but must no longer be the authoritative proof that the entire canonical state is durable.

## Required failure cases

| Interleaving or malformed input | Required outcome |
| --- | --- |
| Host dies after only some chunks | Previous complete head remains recoverable; no new receipt. |
| Host dies after validation, before the final transaction | Previous head remains current. |
| Host dies after commit, before receiving response | Replacement reads the newly committed exact head; retry returns its identical receipt. |
| Two claims race after expiry | One writer fence wins; the loser cannot renew, upload or finalize under the winner's term. |
| Old finalize finishes after takeover | Final transactional fence check rejects it; new head remains unchanged. |
| Writer returns during validation | Finalize rejects; eligible replacement can claim without reviving the returned writer. |
| Non-writer returns after the last checkpoint | Recovery reapplies current terminal membership before enabling commands. |
| Writer renews after its lease expired | Reject even if no successor has claimed yet; a new claim is required. |
| Same upload ID with changed bytes, parent or manifest | Conflict; no mutation of the original immutable upload. |
| Duplicate finalize after a successful commit | Return the same exact receipt; do not create a new sequence or epoch. |
| Wrong match/roster/identity, missing or duplicate chunks, extra chunk, wrong length/hash, invalid UTF-8, inflate overflow | Reject without replacing the head or allocating beyond declared bounds. |
| Long outage or service error | Pause at the existing speculative limit; no fabricated acknowledgement or finalized result. |
| Finalize response completes after Runner migration/stop/failure | Ignore old-lifetime mutation; replacement's pending publication and ownership remain intact. |
| Two replacements crash after claims but before first new commit | Domain epoch still derives from the last committed head, independently of service writer-term count. |
| Initial host fails before any complete checkpoint | Explicit bootstrap recovery or explicit safe failure; no false restored mark. |

## Verification and remaining work

Performed: pinned source review, targeted read-only installed-SDK inspection, exact SDK SHA-256, inspection of the existing native failure artifacts, current checkpoint-window/bounds review, and protocol discussion with the online owner. No new Unity test, service deployment, credentials action, SDK mutation, or proprietary source dump occurred.

Pending: review the implemented coordinator/client protocol and tests at an exact commit; verify fault-injected persistence and takeover interleavings; then root must run native complete-state recovery with a multi-chunk checkpoint, exact identity/hash/epoch/body state, terminal membership and result boundaries. D1 remains open until the runtime acknowledgement and recovery source have been replaced and those tests supply evidence.

## Implementation review checkpoint

Reviewed the online owner's in-progress `BuildNotes/online/COORDINATOR-CHECKPOINTS.md`, `OnlineServices/src/checkpoints.ts`, its `MatchPool`/HTTP routing integration, and initial `OnlineServices/test/checkpoints.test.ts`. These are not yet pinned to a green commit. The proposal uses the same MatchPool transaction, a deterministic signed-roster first host, 15-second exclusive terms, immutable claim receipts, epoch assignment from the committed head, two pending uploads, exact parent-head CAS, and current/prior committed retention. The first native-DO test run had 12/13 passing, with the 33-chunk case still failing; no completed service gate is claimed here.

The schema2/canonicalVersion2 expectation is an explicit coordinated dependency on James's `3633046` snapshot upgrade, independently inspected with `git show`. The older integration baseline still exported schema1 at the time of the first review. This is a rollout dependency, not an unresolved service-format defect. A real C#-exported schema2 packet remains necessary evidence; synthetic transport fixtures cannot establish end-to-end domain compatibility.

Two bounded validation suggestions were incorporated into the draft: reject sequence zero at the request schema, and check writer/upload/reconnect expiry again at finalization. A further source compatibility issue was sent to the owner:

**D2 — P2, snapshot-specific canonical escaping.** `CourseJson.Quote` emits every character below U+0020 as lowercase `\uXXXX`; the service's existing `canonical()` uses `JSON.stringify`, which emits shorter escapes for newline, tab and several other controls. `MatchStateValidation` permits such characters in a valid actor appearance string. A C# snapshot containing them therefore fails the draft service's raw canonical comparison and canonical state-hash check despite being valid for the game codec. The service needs a snapshot-specific canonical serializer matching `CourseJson`; changing the existing grant/signing serializer would unnecessarily alter another protocol. Required regression: actual C# export containing newline, tab, a general control character and supplementary Unicode, followed by exact service acceptance and C# recovery. This is a source-confirmed mismatch; no native reproduction was run.

Additional limits communicated to the owner:

- An expired writer cannot use finalize retry to renew itself. Resolve a lost successful response through authenticated head/read, then claim a new term before further gameplay.
- A returned non-writer after the checkpoint must be applied to the restored state before readiness.
- Fifteen seconds is not an unconditional maximum failover time. An old host that can still renew HTTP ownership during an asymmetric Photon partition can keep the replacement fenced out. Preserve safety and report this liveness limit unless a separate authority mechanism resolves it.
- Up to 64 chunk writes per publication share the pool's serialized transaction path. Bounded upload parallelism and measured normal cadence are necessary before claiming the two-second speculative allowance is practical at load.

## Independent service-source verification

Applied the local [Durable Objects review skill](C:/Users/mrkl6/.codex/skills/durable-objects/SKILL.md), including its concurrency/storage and testing references. The existing pool coordinates several matches, so its throughput remains an explicit acceptance limit; this review did not silently move state into a different coordination object.

`checkpoint-probe.mjs` loads and transpiles the owner's actual TypeScript source without changing it, invokes the real `CheckpointStore` against in-memory Node SQLite, and consumes the owner's real C# schema2 Export/Import fixture. The SQL wrapper supplies transactions and cursor methods only; it does not reimplement checkpoint policy. It is deliberately not evidence of Cloudflare output gating, HTTP authentication, live persistence, Photon or Unity behavior.

Independent result: **12/12 PASS**; captured in `checkpoint-probe-result.json`. Exact source SHA-256 values:

- `src/checkpoints.ts`: `993c5ab36eaae025eb68904e80d654d4d758d40d75824fa18bfc90fa3dcbc120`
- `src/model.ts`: `fa3c4e8b8f228d5bbccac0db40b5fc24a97668ab707cba803e4c318a55a668bb`
- `test/fixtures/schema2.ts`: `6269eb8e82acd6e5363a5d011d5364573304af2378e64222fab0d2212a584bfa`

Coverage: real C# deflate bytes and exact canonical text; all 32 control-character spellings; invalid surrogate rejection; token/string/depth preflight before JSON parsing; correctly rehashed invalid UTF-8 and inflation above 8 MiB; duplicate-key rejection; initial roster-host eligibility; complete begin/chunk/finalize retry; competing parent-head CAS; current/prior byte retention; retired-handle reuse; exact writer expiry and replacement; returned writer rejection; and separate term/domain epoch progression.

**D2 is source-verified fixed** at that hash. A dedicated `snapshotCanonical` matches the actual C# fixture, including controls and supplementary Unicode; the original service signing serializer remains separate. Preflight now mirrors the game's token/string/depth limits before `JSON.parse`.

**D3 — retired upload handle reuse, fixed during review.** Initial draft cleanup deleted old manifests, allowing a retired UUID to identify different future bytes. The owner changed upload IDs and parent-head references to SHA-256 content identifiers over the immutable manifest. Begin verifies the identifier, and the C# reader also recomputes it. Independent retry-after-retention now rejects changed content under the retired handle without requiring unbounded tombstones. The first source probe already read the corrected version; the pre-fix case was established by source inspection and confirmed by the owner, not by a saved failing execution.

A client retry gap was also found and repaired while the draft was evolving: redoing the full Commit operation after a lost finalize response originally failed while resending already committed chunks. The service now accepts identical retained committed chunk bytes under the still-live original term, while rejecting changed data. The independent full begin/chunk/finalize retry passes. After expiry, recovery must use head/read and must not revive the old writer.

The owner's latest actual Cloudflare test-runtime log, `BuildNotes/online/checkpoint-suite.log`, reports **64/64 PASS** over four files (39.83 seconds). This reviewer inspected its source and result but did not independently rerun that suite. Runtime native acceptance and the final C# session cutover remain pending; D1 is not closed.

## Admission-open extension and current schema fixture

The store probe now passes **13/13**, adding the new `checkpoint-open` transition. It verifies that a claim starts closed, another member cannot open it, the wrong base-head hash leaves it closed, an exact open can be retried, takeover starts closed again, and neither an expired term nor the wrong replacement base head can open admission. This flag attests the current writer's declared recovery readiness; it is not independent validation that a remote host actually ran the full game codec.

Current actual-source hashes from this run:

- `src/checkpoints.ts`: `442e333c8d04bc47cedd3a20d63c374ab58b701663a87857d737acb8abd1d439`
- `test/fixtures/schema2.ts`: `7cc16249af185f434681c080149aea97c0bf3350b9f896ec792f808975b27d09`

The regenerated real C# fixture now contains `attack`, `equippedSlot`, and `staggerImmuneUntilTick` actor fields alongside the controls/Unicode case. It is 6,299 raw bytes. This addresses the requested combat-schema compatibility coverage; the small fixture alone does not prove large native recovery.

The session draft waits for `HostAvailable` before issuing its one-use connection ticket. Its constructor rejects client-to-client reliable forwarding, and a prompt triggers a bounded coordinator head lookup requiring the live exact writer/term and `admissionOpen` before latching. Duplicate prompts are coalesced and there is a maximum of four head checks per Runner. A prompt grants no command or replica privilege: the signed host authorization remains required. These are draft source observations only; the final session, deferred completion tests, and actual transport behavior are still under review.

## Independent session scheduling probe

Service pinned at `9f23973`; C# checkpoint client subsequently committed as `b0c5625` with the owner's seven focused client tests passing. The latest service gate reports 65/65 tests, including the admission-open extension and the regenerated combat fixture.

`SessionProbe/SessionProbe.csproj` compiles the actual session, checkpoint client, wire and rules sources. It uses the owner's SDK scheduling doubles and this reviewer's independent HTTP/scheduling fixture. The fake authority records calls and deliberately does not validate game snapshots; those tests establish asynchronous control flow, not full game-state validity. No native SDK runtime or live coordinator is invoked.

Independent result: **10/10 PASS**, recorded in `session-probe-result.txt`.

1. A closed writer never consumes a connection ticket; repeated prompts cannot exceed four head checks.
2. Duplicate prompts coalesce while Head is pending and after the one ticket is requested.
3. A superseded Head response cannot request a ticket.
4. A superseded ticket response cannot send into either old or replacement Runner.
5. An exact coordinator receipt acknowledges the publication with **zero Photon snapshot pushes**.
6. An old Runner's successful finalize cannot acknowledge or clear the replacement publication guard.
7. An old Runner's failed finalize cannot fail the replacement or clear its guard.
8. A finalize arriving at the exact local writer expiry cannot acknowledge; Poll then closes gameplay.
9. A lost finalize response resolves the identical committed head and acknowledges exactly once.
10. Stop during delayed head resolution prevents acknowledgement and readiness.

Source hashes at this checkpoint:

- `WordboundFusionSession.cs`: `9210908D89B5A98CEA4532EAD3B3CE81A5CD3C5B0C2DA769DC7204F99535A85A`
- `OnlineCheckpointService.cs`: `39BC8A38080C9955CE93A132A37F7C768C7E007CF9C7DFFC07F3B824B1A26D60`
- `OnlineFusionWire.cs`: `5C2EE23284F6133E823869DE5792363368793997A3722AF53A93172DE51AB530`
- Owner's scheduling doubles: `6270AC59EC4B54F3202EB286239FA746F4E3854DB8483BD27786802F9BC28B31`
- Locally built `SessionProbe.dll`: `D68313FB20CA2C306F74A9CB98F5149E759E8A9973A9836A3E964FB0B1AD2920` (generated binary remains local).

The initial probe attempt exposed an error in this reviewer's HTTP fixture: it handed a `double` index to the strict integer-only encoder. Casting that fixture index to `int` produced the passing run. This was not a production defect.

A source compatibility question was also resolved during review: a fresh Session joining after a prior host replacement must not assume the original roster-first host still owns the room. The owner changed fresh Start to inspect current coordinator writer/head before choosing Host/Client, and permits the exact live open replacement writer in the roster. Admitted now additionally requires a previously confirmed HostAvailable identity. Native room discovery/rejoin remains unproven.

Next required checkpoint: pin the final session commit and owner lifecycle tests, rerun the independent probe if its source changed, then consume root's authenticated native recovery evidence. This report remains IN PROGRESS.

## D4 — P2 election setup deadline, confirmed and fixed

`SeedElection` originally checked its ten-second deadline only between SDK calls; the SDK push await itself could remain pending while writer renewals continued. The owner added bounded polling. Independent reproduction then found the remaining late-success edge: if the SDK task completes after the deadline but before the next poll, the loop exits and `if (queued) return` bypasses the deadline check.

The saved `seed-deadline-reproduction.txt` confirms that edge against session SHA-256 `D2B9591E6003B2615FDB4670EFDE62E25ABBB389FB22349103E1A80FFB023D6F`. This exact compiled source checksum was read from the portable PDB, avoiding ambiguity from concurrent source edits. The probe publishes a coordinator checkpoint, defers only the SDK election push, advances the injected clock to deadline plus 1,000 milliseconds while the writer lease is still live, and completes the push successfully. The session accepted that result without failure.

This is a bounded readiness/liveness defect, not a new false durable acknowledgement: the checkpoint had already committed independently. The owner added deadline checks before each push and after awaiting it. Independent verification now covers both a never-completing SDK task and late success, requiring failure to remain closed and the already committed frontier to remain unchanged.

## Root-owned native acceptance conditions

For an actual authenticated two-player host loss, require exact server-minted Photon identities and signed content/roster binding; a complete multi-chunk coordinator receipt and matching client replica; a partial newer upload that leaves the previous complete head recoverable; and a separately committed upload whose finalize response is lost. Recovery must claim exclusive ownership, retrieve and validate the complete canonical packet, overlay terminal membership, admit with a fresh ticket only after recovery readiness, and commit exactly the next domain epoch before enabling play. Confirm body, inventory and combat state plus resumed input. Verify that commands/physics remain frozen during fencing/restoration and that old Runner completions cannot unfreeze or acknowledge.

The recovered byte count/hash must come from the coordinator packet, not the small Photon election marker. The latter remains a transport-election aid; queueing it is not a complete-state durability receipt. Source/double tests cannot replace these native assertions.

## Final source review handoff

Pinned final session commit: **`93b28e480f50cd14ca8cf5c170da00a34b79a929`**, following service `9f23973` and client `b0c5625`. The reviewed worktree had no diff from that commit for the session, client or wire files. Its session SHA-256 exactly matches the portable-PDB checksum from the independently rebuilt probe:

`1F29EF39CE5C3E7DCBED92A86DD083C06FC36E042C9D7240DED32A3442D1EBCE`.

The final independent session probe passed **12/12**, including both election-deadline cases. Its output records compiler source checksums for the actual linked files. Final local probe assembly SHA-256 is `E53927976FA553A8E83CF6321554E0B2922D5E94CAF12902161559769CD84C0C`; generated binaries remain ignored. The owner's committed lifecycle log reports **28/28**, and its installed-SDK and native-fixture reference builds report zero warnings/errors. Those compilation results establish API compatibility, not native execution.

Within the reviewed scope, there is **no remaining confirmed source blocker**. D2, D3 and D4 are fixed and independently verified. D1's incorrect SDK-based acknowledgement has been removed: complete coordinator receipts now advance the durable frontier, recovery reads and semantically validates coordinator bytes, and native snapshot calls only seed transport election. Initial/replacement admission follows writer claim and restore, validates current terminal membership, uses bounded one-use prompting and signed host proof, and first publishes the replacement epoch before readiness.

D1's **end-to-end native acceptance remains open**. The first authenticated two-player recovery, multi-chunk recovery/failure cases, actual physics freezing, restored body/combat consistency, room rejoin, persistence latency and load limits must be established by root's native and deployed-service evidence. A stale host that can continue renewing its coordinator lease during an asymmetric Photon partition can still delay takeover; safety is preserved but fifteen seconds is not an unconditional failover limit.

All reviewer compiler/test processes completed before the root's native compiler hold. This lane made no Unity launch, deployment, dashboard change, SDK write or production-source mutation.

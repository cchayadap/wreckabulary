# Checkpoint main-thread audit

STATUS: AUDIT COMPLETE. Source and existing evidence only; no execution or measured improvement claim. Root subsequently authorized only the combined export/hash candidate described below, in a separate source commit.

Root-authorized read-only audit at integration HEAD 6eed388b. Scope: Fusion session checkpoint publication, authority capture/export/commit bridge and client replica receipt; separate synchronous work from asynchronous waits and assess immutable worker candidates. No source edits, Unity/jobs or timing/cadence/configuration changes. Existing full validation and mutable authority/session ownership must remain intact.

## Exact owner-thread call graph

Paths below are relative to `Assets/_Project/Scripts/`; line references describe audited integration `6eed388b`.

1. `Online/Gameplay/WordboundGameplayController.cs:130–152`: ordinary publication starts in Unity `Update → Poll → PublishCheckpoint`. `AdvanceHost:254` and `PrepareHostPhysics:239` call **session** `Poll` for authorization/renewal, not the controller's checkpoint scheduler. Thus this serialization is synchronous owner-thread work between canonical steps, not serialization inside each `Authority.Advance`. Bootstrap also calls the same session publication method from its startup continuation.
2. Controller `PublishCheckpoint:440` calls/awaits the session without suppressing its synchronization context. The async method executes its synchronous prefix immediately. Its existing same-Runner finally schedules `completion clock + CheckpointMilliseconds`; the default is 1000 ms (`WordboundGameplayContracts.cs:53`), while the actual public native fixture supplies 500 ms. Both must remain unchanged.
3. `Online/Fusion/WordboundFusionSession.cs:663–679`: current run/server/readiness/writer/migration/failure and single-publication guards; then `authority.Publish`, `authority.ValidateSnapshot`, `checkpoints.Prepare`; **only then** the first await at `checkpoints.Commit`.
4. `WordboundFusionAuthority.cs:73 → Rules/Wordbound/AuthoritativeMatch.cs:276`: guard against an in-progress canonical tick; capture the next sequence; `EnsureValid`; `MatchSnapshotCodec.Export`; separately `ComputeHash` of that same private snapshot; register the hash/tick in the commit tracker. `EnsureValid:1051` runs complete validation and the extra pinned course-asset letter checks. It is not merely a JSON check.
5. Bridge `ValidateSnapshot:76–85` imports that JSON, parses/reconstructs its complete snapshot, recomputes its hash and runs full validation against bridge dependencies. It then checks exact mark identity/epoch/sequence/tick. This is a second full validation, retained by this proposal.
6. `OnlineCheckpointService.cs:174–183` synchronously checks writer/frontier, compresses UTF-8 JSON, hashes raw/compressed bytes and creates/binds an immutable upload manifest. `OnlineSnapshotPacket.Compress:36` uses local streams, Deflate and SHA objects. This cost is outside the existing Publish/Validate diagnostic stages.
7. Service `Commit:185` awaits begin, up to four concurrent chunks and finalize with `ConfigureAwait(false)`. `OnlineCoordinatorClient.Send:99` and `Online/WordboundHttpTransport.Send:25` likewise suppress context capture for their I/O continuations. Their synchronous prefixes still serialize the bounded request and issue HTTP on the calling thread; `ConfigureAwait(false)` does not guarantee a thread switch when an await is already complete.
8. Session completion resumes the captured Unity context in the normal call path. It rechecks current run/writer term, running Runner, migration and exact receipt/upload identity. `CommitPublication:709` mutates the authority's durable ACK/result state, replaces the head/upload, handles the initial bootstrap barrier and enumerates actual admitted Runner peers. `SendCommitted:771` synchronously copies/base64-encodes each compressed chunk, JSON-encodes its wire envelope and calls Fusion reliable send. These SDK/state/callback operations remain owner-thread work. The receipt-recovery `Head` branch retains the same checks and upload retry object.

## Client packet and replica path

`WordboundFusionSession.OnReliableDataReceived:906` synchronously decodes an SDK callback and dispatches `ReceiveSnapshot:780`. Admission/identity/frontier and mixed/duplicate/stale chunk checks precede packet assembly. The last accepted chunk triggers bounded concatenation, wire/raw hashes, inflate and UTF-8 decode; bridge full Import/Validate follows. Only then does the session install its committed mark, clear the receive accumulator, invoke `ReplicaReceived`, send exact SnapshotAck and consider Begin.

The controller subscriber `ReceiveReplica:413` synchronously calls `MatchSnapshotCodec.Import` again, verifies the mark, assigns its frame and invokes `FrameChanged`. The bridge and controller own distinct rules objects and potentially different current dependency state; there is no current API establishing an interchangeable validated result. The second import, frame publication and downstream callbacks are outside the diagnostic wrapper around bridge `ValidateSnapshot`. No validation may be dropped solely because both presently accept the same bytes.

## Existing measured evidence and its limits

Re-read only the exact `PublicTeamRacePreservesWholeSoloParties` output in `TestResults/editmode_20261024T003140Z.xml`; SHA256 `9D8D3CD3A7B7638D74E528ED64E888FAB4117A4A7039810F5204312C79345770`. Extracted record `BuildNotes/client-view/native-public-delta-20261024.json`, SHA256 `827A9FE0D29166338DE12A9E2730B46F009D7E5D9450677F903DA684B19B71D6`, source `596822484af17e272b631c9a55304daac1ff61da`.

| Synchronous authority wrapper | Completed | Total ms | Maximum ms |
| --- | ---: | ---: | ---: |
| Host Publish (capture + validation + export/hash + registration) | 11 | 994.252 | 166.033 |
| Host ValidateSnapshot (full import/check) | 11 | 1278.118 | 186.859 |
| Host AcknowledgeDurable | 11 | 11.354 | 11.109 |
| Client bridge ValidateSnapshot | 2 | 255.159 | 134.253 |

The paired host Publish+Validate means are 90.387+116.193 = 206.579 ms per completed publication, before unisolated compression/wire preparation. These are blocking-call Stopwatch wall observations, not isolated CPU instructions. Their maxima are from potentially different publications and cannot be added as an observed worst frame. Earlier exact `000038` measured 12 Publish/1213.125 ms and 12 Validate/1326.359 ms; it corroborates repeated blocking work, not a controlled before/after comparison.

The same `003140` output has actual `UnscaledDeltaTime` >1 s on 11 host and 9 client Update observations, maximum 1835.871 ms, with no invalid/unavailable samples. Long host batches and other work share those frames. There is no per-publication frame correlation or separate compression/controller-import/send duration, so neither a single >1 s checkpoint nor a checkpoint-caused SDK stall is established. HTTP Begin/Chunk/Finalize timings include await/queue/continuation time; four concurrent chunks make their sum especially unsuitable as main-thread cost. Inclusive HostStep/full-validation phase totals overlap and do not isolate this between-step publication path.

This case passed movement and the authenticated command, then failed the durable command-bearing ACK wait: peer 1 rejected transfer age **11485 ms ≥10000 ms**, with same mark/count/length/wire hash and no duplicate chunk. That is continuation-observed transfer age, not proof of corrupt content or network RTT. No transfer or scheduling deadline change is justified here.

## Snapshot isolation and worker boundary

An authority capture is detached from subsequent ordinary authority mutation: `CaptureState:272` copies progress/economy/journal; their constructors deep-copy mutable teams/players/result, inventories/items/craft/letters and command frontier maps. `MatchSnapshot:213` freezes section arrays; actor/body/resource/mechanism/flight/practice records are get-only values. `Economy.Restore` validates/copies into a replacement, so later authority restoration does not edit a retained captured ledger. Ordinary later Advance, ACK/result confirmation and Restore therefore do not change the captured bytes.

This is a bounded ownership guarantee, not automatic immutability merely because the outer class is sealed. Progress entries have internal setters and economy internal arrays contain mutable items. A worker must receive a completed, exclusively retained snapshot or immutable JSON/packet, never live authority dictionaries or mutable caller-owned sections. The current publication's local snapshot does not escape between export and hash.

Pure `Export`/`ComputeHash` and packet compress/restore use local builders/streams/hash objects and no Unity/Fusion APIs. They are plausible worker payloads **after** ownership is sealed. Full validation is not: it observes mutable `GameRules`, shared mutable `ItemCatalogue` entries, and publication's extra course catalogue checks. Copying those dependencies would change the time at which their validity is observed; it is not equivalent to validating current dependencies on return. `ConditionalWeakTable` metadata caching does not make those live dependencies immutable.

Moving a whole existing Publish/Prepare/Import call to `Task.Run` is unsafe. Capture and `commits.Publish` access live authority state; Prepare consults credentials/current writer; session dictionaries, renewals, startup/recovery, ACK and SDK sends are owner-thread operations. `PublishSnapshot` currently hashes captured tick N and immediately registers `commits.Publish(Tick, hash)` with no intervening Advance. An asynchronous split must register the captured tick and exact reserved sequence, never the later live Tick; premature sequence reservation would also change failure bookkeeping.

A later worker design would need a single in-flight operation tied to exact run object, epoch, writer term and immutable upload/mark. Completion must recheck cancellation, current run/Runner, live writer expiry/term, publication frontier and receipt identity before any install/ACK/send. Writer renewal under the same term may continue; replacing the writer object alone cannot be treated as ownership loss. Failed/cancelled work must not acknowledge or poison later sequence/retry state. Initial bootstrap, ambiguous Commit→Head recovery and migration/rejoin paths must keep their present semantics.

Client background decoding would additionally require a sealed complete transfer and explicit in-flight ownership so a later packet cannot replace/reuse its accumulator. It must preserve rejection order and the current 10 s chunk-arrival age rule, then recheck admission/frontier/run before installing, invoking callbacks or ACKing. A fresh immutable decoded snapshot could support owner-thread validation, but exposing an unvalidated parse result is a broader API/state-machine change. No such redesign is authorized here.

## Concrete first implementation: return the existing export hash

Root authorized this bounded candidate after the audit:

- Add `MatchSnapshotCodec.Export(MatchSnapshot snapshot, out string hash)`; keep the existing `Export(snapshot)` entry delegating to it. Build one envelope, calculate the same hash before adding the hash field, serialize the same final JSON, and retain the existing final Preflight. Do not change standalone `ComputeHash` or Import.
- In **only** `AuthoritativeMatch.PublishSnapshot`, use that returned hash in `commits.Publish(Tick, hash)`. Keep the inTick guard, capture, full `EnsureValid`, export failure point, commit tracker registration and out JSON ordering in place. Session bridge validation, compression and every subsequent fence remain unchanged.
- The removed work is precisely the second complete envelope construction, canonical encode, canonical Preflight and SHA pass for the same private snapshot. A successfully preflighted final JSON already includes the same canonical values plus the fixed valid hash field; removing that field cannot introduce an extra byte/token/depth/string violation. Do not cache across snapshots or calls.
- Focused regressions: exact JSON/hash equality against the unchanged ComputeHash and independent hash-field recomputation across cultures and populated sections; old/new retained snapshots after real authority mutation; mutable catalogue invalidation still fails publication before advancing its sequence; null/non-finite/text/size/token failures preserve type/message and do not create a publication; actual valid publish/ACK and next sequence remain exact. Existing codec tamper/binding/round-trip and authority ACK suites remain required.
- Root alone runs those checks and the same fixed-snapshot export+hash workload (warmup/calibration/raw samples) if measurement is desired, followed by the existing native checkpoint case. There is no throughput/FPS forecast. Existing checkpoint stages can show whether aggregate Publish changes; their broad scope cannot attribute all variance to the removed hash pass.

No worker threads, validation skips, telemetry expansion, cadence, provisional window, transfer limit, SDK configuration, original artwork or gameplay behavior changes are included.

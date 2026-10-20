# Admission boundary review

STATUS: COMPLETE — TWO CONFIRMED FINDINGS; FIXES REMAIN WITH IMPLEMENTATION OWNER

Lane: `wordbound/admission-review`. Reviewer: independent workshop/assets lane. Inspected target: integration commit `e8eae92c96ffc5b2eac14c77e2e46d17a4377e42` (online source also supplied as `ed0abd9`).

Scope: `OnlineServices` coordinator and `Online*.cs` / `WordboundHttpTransport.cs`. Focus: authenticated session identity; full-party and Fill concurrency; request replay and deadlines; Photon single-use authentication tickets; signed start/admission bindings; bounded wire parsing and redirects.

The integration worktree contains active SDK/import changes outside this review. They are not being modified. No Unity launch, service deployment, shared source edits or Git mutations are part of the investigation. This report is the only working output until its final report commit.

Method: committed-source inspection plus bounded local reproductions. Review completed 2026-10-20. A source/typecheck pass is not a claim about deployed Photon, Fusion, credentials, or real multiplayer behavior.

## Confirmed findings

### F1 — P1: a delayed successful join replay recreates a phantom membership after leave

Evidence: `OnlineServices/src/identity.ts:63–82` creates a fresh PENDING membership whenever the prior membership is absent, then marks it ACTIVE on any successful Party join result. `OnlineServices/src/party.ts:27–33` returns a stored join receipt before checking current membership or applying a join.

Concrete public-route sequence:

1. Create a Squad party A with owner O.
2. Guest J joins A with idempotency key `join-original`; retain that successful request.
3. J leaves A using the current revision and a new key. A now contains only O.
4. Replay the exact original `POST /v1/party-joins` request as J, with `join-original`.
5. The old two-member success result returns, and J's identity claims ACTIVE membership in A even though A's actual member list contains only O.
6. J cannot GET A or leave A (`403 NOT_PARTY_MEMBER`), and cannot create another party (`409 ALREADY_IN_PARTY`). The identity is stuck without administrative repair/new guest creation.

Reproduced by executing the unchanged TypeScript Identity/Party/model/rpc modules through TypeScript's in-memory transpiler, with clone-on-read/write transactional storage and an access-token-signing stub. No source file was modified, no network endpoint was contacted, and native Durable Object behavior was not claimed. The sequence requires no concurrency; a delayed duplicate after a completed leave suffices.

Observed output:

```json
{
  "replayReportedMembers": 2,
  "actualMembers": 1,
  "profileStillInParty": true,
  "getError": "WB:403:NOT_PARTY_MEMBER",
  "leaveError": "WB:403:NOT_PARTY_MEMBER",
  "createError": "WB:409:ALREADY_IN_PARTY"
}
```

Required correction: bind enter-operation replay to the identity's completed lifecycle (or retain a membership epoch/tombstone), so an old join receipt cannot create new membership after leave. A fresh join must use a new operation and actually insert the member. Add a real service regression with the route sequence above; ensure its failure does not erase a later legitimate membership.

### F2 — P2: overlapping refresh calls discard the winning rotated credentials

Evidence: `Assets/_Project/Scripts/Rules/Wordbound/OnlineCoordinatorClient.cs:120–134` increments `authenticationGeneration` independently for every refresh request, and discards a successful result if another request was started after it. `OnlineServices/src/identity.ts:24–32` consumes and rotates each refresh token once, incrementing the access-token version in the same transaction.

Concrete sequence:

1. A client has valid access and refresh credentials.
2. Two callers concurrently call `Refresh` with that same refresh token.
3. The first request reaches the service first, succeeds, and rotates both credentials. The second request fails with `401 INVALID_REFRESH`, as intended for a consumed token.
4. The first response cannot install its valid credentials because the second call has already incremented the client generation.
5. Both callers see failures, and the client retains the old credentials, which the service has now invalidated.

Reproduced by compiling the actual `Json.cs`, `OnlineJson.cs`, `OnlineContracts.cs`, and `OnlineCoordinatorClient.cs` sources in memory with PowerShell `Add-Type`. A controlled `IOnlineHttpTransport` completes the first refresh with `200` and new credentials, then the second with `401 INVALID_REFRESH`. No source file was modified. The controlled responses match the inspected service's single-use rotation behavior; this was not a live HTTP test.

Observed output:

```text
winner=OperationCanceledException: Authentication was superseded.
loser=OnlineServiceException: Coordinator request failed: INVALID_REFRESH
retainedAccess=old.token
```

Required correction: coalesce or serialize refresh for the same identity/token so only one rotation can be in flight, while preserving `ClearCredentials` cancellation fencing. A later failed refresh must not supersede the winning rotation. Add a client regression for overlapping refresh callers and ensure a logout still cannot be undone by a late response.

## Coverage and evidence

- Confirmed integration HEAD and active dirty paths.
- Enumerated the TypeScript coordinator modules, service/model/Photon tests and C# admission/client/transport source.
- Read identity rotation, membership operations, party operation receipts/locking, pool allocation/expiry, Photon ticket issue/consume, strict HTTP schemas, and C# signature/roster/content binding.
- Confirmed F1 in an in-memory execution of the unchanged service modules. Online implementation owner agrees delayed old joins must be harmless and is handling any fix in its own lane.
- Confirmed F2 in an in-memory compilation/execution of the unchanged C# client sources. Sent both reproduction results to the integration and online implementation owners.
- Read the existing model, Photon, service and C# online tests to identify covered assertions and missing regressions. Their pre-existing passing results were not independently rerun by this review.

| Boundary examined | Evidence and result |
| --- | --- |
| Authentication and session identity | `OnlineServices/src/index.ts`, `auth.ts`, `identity.ts`: routes derive identity from a verified bearer token and current identity version; server creates UUIDs; refresh hashes are rotated transactionally. Confirmed F2 in client refresh coordination. Did not contact a deployed identity service. |
| Membership and replay | `identity.ts`, `party.ts`, `rpc.ts`: membership reservation, operation hashes, receipts, async operation fencing, leave retries and lifecycle inspected. Confirmed F1 on an old join after completed leave. |
| Whole-party/Fill concurrency | `model.ts`, `party.ts`, `pool.ts`, `store.ts`: inspected transactional allocation, team capacities, logical Fill-Off reservation, no party splitting, private empty opponent restrictions, channel/map eligibility, queue attempt reuse, and final-ready versus cancellation transition. No additional confirmed defect from these inspected paths. No new native Durable Object concurrency run. |
| Deadlines and reconnect | `pool.ts`: inspected admission expiry, whole-party rollback/requeue, attempt tombstones, fixed disconnect grace and terminal return checks. Coordinator rejects a fresh grant after terminal return; offline previously signed grant revocation remains an adapter integration concern below. |
| Photon single-use tickets | `auth.ts`, `identity.ts`, `index.ts`: inspected app-specific audience, signature/issuer/version checks, dashboard provider secret, exact app binding, 60-second expiry, bounded outstanding ticket count, transactional nonce consumption and provider-derived UserId. Inspected corresponding tests, including exact expiry and refresh revocation. No live Photon callback executed. |
| Signed start and admission | `OnlineAdmissionVerifier.cs`, `OnlineContracts.cs`, `auth.ts`, `pool.ts`: inspected RS256/trusted key/issuer/audience/expiry checks; exact generation/content/roster commitment; contiguous valid seat domains; whole-party roster binding; authenticated Photon player ID and connection-secret commitment. No additional confirmed defect in those checks. |
| Bounded wire | `index.ts`, `OnlineJson.cs`, shared `Json.cs`, `OnlineCoordinatorClient.cs`: inspected 16 KiB server request streaming limit, strict schemas/UTF-8, client 32 KiB response parsing limit, depth and exact integer constraints, C# duplicate-key rejection, field allowlists, canonical hashing and idempotency keys. |
| Redirects and transport | `WordboundHttpTransport.cs`: inspected pinned absolute HTTPS origin, explicit loopback-only HTTP exception, fixed `/v1/` routes, same-authority check, disabled/rejected redirects, 15-second linked deadline across headers and body reads, bounded streamed response and strict UTF-8. No live socket or TLS fault-injection test. |

## Integration limits and next action

This review covers the listed source at `e8eae92`; it does not approve later Fusion adapter code. A signed admission grant is reusable within its validity window, whereas a Photon authentication ticket is single-use. The adapter must enforce the exact authenticated identity and connection secret, one active connection per seat, fixed reconnect deadline and terminal return. A coordinator preflight followed by local acceptance has a possible time-of-check/time-of-use window if terminal return wins between them. The implementation owner is designing live reconnect authorization/epoch enforcement. That design and actual disconnect/return behavior require a separate adapter/native check; this report does not call them a confirmed defect in the reviewed coordinator.

NOT RUN in this review: native Workers/Durable Object suite; Unity editor/runtime suite; real Photon/Fusion SDK admissions; IL2CPP/platform HTTP/TLS behavior; deployed endpoints, signing configuration, provider dashboard or credentials. No deployment occurred. No source files outside this report were changed.

Next recoverable action: implementation owner adds the two failing regressions and fixes F1/F2 in its lane, then integration reruns the actual service and C# suites. Independently recheck the fixed route/client sequences and the finalized Fusion terminal-return/reconnect admission path before claiming live multiplayer acceptance.

## Follow-up review — COMPLETE

Scope extended by integration owner after the initial report commit: independently verify the local F1/F2 fixes in `wordbound-online`, and inspect the new `OnlineSessionGate.cs` / `OnlineSnapshotPacket.cs`. These are uncommitted implementation-owner changes; capture exact source hashes with the results. No implementation source edits or Unity launch by this reviewer.

Base revision: `ed0abd97b6f044da0ecfdae94bb53b59ef51a80c` plus the following local implementation-owner files:

| File | SHA-256 reviewed |
| --- | --- |
| `OnlineServices/src/identity.ts` | `3CA19BE1864145FFEDC44D81B1F3F88EE074CF64265053AD977784E4B414145E` |
| `OnlineCoordinatorClient.cs` — final guard | `201055CCF9BDB945FA8D6DF6A9C614C0EB066C0099606C0B1144DB6648608869` |
| `OnlineSessionGate.cs` | `5A135C739B73C550F3A6B30A6DE2B60C8945DAE22BFDCD66C7A92775CC948522` |
| `OnlineSnapshotPacket.cs` | `E69F0BE35D41C9659508F31E54D6C2385C8484D0CAC9C716E5B69CC989AACEE8` |

F1 correction independently verified using the same actual-source in-memory service method:

```json
{
  "oldReplay": "WB:409:MEMBERSHIP_OPERATION_RETIRED",
  "profileCleared": true,
  "laterReplay": "WB:409:MEMBERSHIP_OPERATION_RETIRED",
  "laterPreserved": true,
  "freshMembers": 2,
  "actualMembers": 2
}
```

The identity now retires successful membership operation keys on leave, rejects their reuse before reserving a new membership, and checks the operation ID as well as request hash before marking a pending entry ACTIVE. Inspected new real-service regressions also cover a retired create key and preservation of a later party membership. Native service execution remains with the implementation owner.

An independent in-memory C# probe passed 17 assertions across same-token refresh coalescing/logout, local session ready/start gating, duplicate seat exclusion, unchanged disconnect grace on duplicate reports and migration, exclusive deadline rejection, terminal return preservation, multi-chunk snapshot round-trip, hash/tamper rejection and compressed size bounds. This run used the first coalescing client version (`1566C8E2B37D02BE4527044FB3827719899AFBD4C52E48A3B6BC65D766ABEF62`); the gate and packet hashes match the table above.

Further source review identified a residual different-token overlap in that first client version: a stale token could still supersede an active refresh. The implementation owner immediately added the final guard listed above, rejecting a different token while a refresh is pending without incrementing the authentication generation. The separate final-guard probe passed against that final client hash:

```text
PASS: same-token coalesced; different-token rejected before send; winning new.token retained; logout fenced late refresh.
```

Two probe preparation errors (noncanonical base64url fixture and an unawaited-call compile warning treated as an error) were corrected before that successful run; neither is counted as product evidence.

No additional confirmed defect was found in the inspected gate or packet primitives. `OnlineSessionGate` is a local state machine operating on already-verified admissions and trusted migration presence; it does not itself implement live coordinator revocation or validate a signed migration snapshot. `OnlineSnapshotPacket` bounds/frames/compresses/checksums bytes; it does not authenticate their sender or validate game state. Correct authenticated callback routing, snapshot codec validation and coordinator return/reconnect races remain integration responsibilities and were not exercised by these primitive probes.

Follow-up conclusion: both original findings are independently verified corrected in the exact local source versions listed above. This does not retroactively change their status at the original reviewed revision. Integration must bring in those fixes and run the native service/C# gates; this review makes no production or live-multiplayer acceptance claim.

## Connection lease and Fusion binding review — COMPLETE; F3/F4 FIX VERIFICATION PENDING

Assigned scope: exact committed change `3f5a655` → `39047cc` in `wordbound-online`. Review return/admit/migration races, signed host and Photon identity binding, fail-closed lease expiry, duplicate/replay/renewal handling, and end-to-end session mapping. Later commits are outside this initial checkpoint unless explicitly recorded. Source is read-only; only this report may be edited. Bounded in-memory reproductions are permitted; no Unity, deployment, external service or source mutation.

The implementation's declared revocation model is a 15-second connection lease renewed/polled every five seconds, not tick-atomic coordinator return. An authenticated roster host consumes each peer's one-use connection ticket and secret. The coordinator cannot independently attest which Photon participant became the selected transport host. These are review context and trust boundaries, not passing results.

### F3 — P2: an old admission failure can stop the replacement host session

At `39047cc`, `WordboundFusionSession.Admit` captures its runner before awaiting coordinator authorization and correctly ignores a successful result after runner replacement. Its exception path instead calls `Fail(error)` whenever the captured runner is no longer current. `Fail` freezes the current gate and authority. An outstanding HTTP request from the pre-migration runner can therefore stop the newly migrated session when that old request times out or fails. Its unconditional `finally { authorizing.Remove(player); }` also removes a replacement runner's pending guard if that runner reused the same `PlayerRef`.

Independent reproduction compiled the exact committed `Admit` method body in memory with controlled runner, verifier and pending HTTP dependencies. Begin a remote admission, replace the runner while authorization awaits, establish the replacement player's authorization marker, and complete the old request with an exception. A success control uses the same interleaving.

```text
lateFailure=True currentSessionFailure=InvalidOperationException newAdmissions=0 newAuthorizationMarker=False oldDisconnects=0
lateFailure=False currentSessionFailure= newAdmissions=0 newAuthorizationMarker=False oldDisconnects=0
```

This executes the production method body but uses dependency doubles; it is not a native Fusion migration run. The online implementation owner independently found the same lifecycle issue while specifying migration tests. One finding is recorded, not two independent bugs counted for the overlapping reports.

Required correction: scope pending operations and their cleanup to the captured runner/session generation; discard stale success, failure and finally effects. Inspect the other startup, ticket, renewal and checkpoint awaits under the same rule. The owner also identified related unfenced startup/ticket/shared-operation flags; those additional paths have not yet been independently executed here.

Independent coordinator method probes at this same commit used the real TypeScript model/pool source, in-memory transactional storage and a signer double. They do not test actual Cloudflare scheduling or RSA signatures:

```json
{
  "sameRetryClaims": true,
  "oneUse": "WB:403:INVALID_CONNECTION_TICKET",
  "renewedLease": 1020000,
  "replacedEpoch": "2",
  "staleRenewPreservedNewLease": true,
  "returnReplay": "WB:409:RECONNECT_EXPIRED",
  "returned": true,
  "returnEpoch": "3",
  "returnLease": 0
}
```

### F4 — P1: the joining client does not authenticate the room host as a roster member

At `39047cc`, the client branch of `WordboundFusionSession.OnReliableDataReceived` sets `localAdmitted=true` and sends Ready when an `Admitted` message contains the expected roster hash. The wire payload carries no signed connection authorization, host identity, or fresh connection challenge. `Begin` similarly checks only readiness and that hash. The server path verifies each client's Photon identity and coordinator grant, but there is no corresponding application check that the joining client's server is an authorized original roster member.

A known room name is `wb-<match UUID>-<generation>`. If an authenticated outsider creates that room before the legitimate host, it can send the expected hash to advance the victim's admission. Disabling client session creation in the legitimate client's configuration does not authenticate the room creator. This exceeds the declared trust in an authorized roster member acting as listen host. The online implementation owner independently agreed this is outside that trust model.

The exact committed callback method was compiled in memory with controlled runner/wire dependencies. Delivering an Admitted payload containing only the correct roster hash, with no host proof, produces:

```text
hostProofSupplied=False
localAdmitted=True
readyMessages=1
failure=
```

This confirms the application acceptance boundary. Actual outsider room precreation, Photon configuration, provider/dashboard behavior, and native transport exploitation were NOT RUN; no claim of an observed deployed exploit is made.

Required correction: verify a coordinator-signed proof binding the original-roster host, this client, exact match and a fresh per-connection client nonce before Ready or accepting gameplay replicas. The implementation owner proposed returning the signed `wordbound-connection` authorization to the client and pinning its nonce. This prevents an outsider without roster authorization from satisfying the admission proof; it does not eliminate malicious or colluding authorized listen hosts.

### Additional coverage and limits for this checkpoint

Exact reviewed online revision: `39047cc2aaf6e9032aa2c85a9dd128dc8c550ffa`. Source was read with `git show`; later owner edits to `WordboundFusionSession.cs` do not change these findings.

| Boundary | Work performed and result |
| --- | --- |
| Connection linearization | Inspected transactional ticket consumption, exact request-hash retry, per-seat epoch replacement, return/disconnect invalidation, caller membership and renewal host/ID/epoch matching. Independent method probes above passed. Return winning before authorization rejects; authorization winning first may remain locally usable only within its old lease, as explicitly declared. |
| Signed lease | Inspected pinned RS256 verification, separate audience, exact match/generation/roster/player/host/connection binding, positive epoch, 15-second lifetime and exclusive deadline. Re-executed owner's `ConnectionAuthorizationPinsHostTransportAndExclusiveLease` test: PASS. F4 is missing client consumption of this proof, not a failure in its signature verifier. |
| Host callback identity | Inspected use of `GetPlayerUserId` and transport token, fixed roster seat admission, duplicate local-seat exclusion, active-runner/player checks after successful authorization, and two independent reconnect gates. Found F3 on stale failure/cleanup. |
| Revocation and expiry | Inspected `Renew`, `RefreshConnectionState`, `Poll`, and `Revoke`. Complete roster and binding are checked before lease mutation; returned/replaced/expired seats are disconnected. Renew failures retain only the old deadline; the owner requires `Poll` every frame. No native scheduling or service-outage experiment was run here. |
| Queued commands after Return/disconnect | Independently queued a real facade command before each transition, then advanced the engine-free authority. Both produced one `NotAlive` receipt and no gameplay action. Disconnect/Return disables domain permission before queued commands execute. Migration also clears the command queue. |
| Migration presence and readiness | Re-executed owner gate tests for all-original-seat readiness, identity replacement, exclusive reconnect deadline and terminal Return across restore: PASS. Inspected exact snapshot-carrier validation and newer domain epoch restore. Actual carrier durability/weaving/callback ordering is outside this read-only review. |
| Wire and route bounds | Inspected strict new route schemas, self-issued tickets, at most eight unique renewals, fixed routes, connection ID/secret formats, bounded reliable messages and signed-token limits. Existing pinned-origin transport remains unchanged. |

The three owner lease/gate tests ran from existing `Tools/RulesHarness/bin/Debug/net9.0/RulesHarness.dll` (SHA-256 `F72A7A37322E51A3AC3B7DBFDA661BC26FDE71654AA6E74018B83CCFDD590E87`) and `Wreckabulary.Rules.dll` (`32470A4CF6108FD5DD18353662E00F46424199E97C8933E283235CE9CE96F9E3`). These are re-executions of owner tests, not independent test authorship.

The queued-command probes used the existing post-`b06deb5` bridge test fixture, whose production facade is unchanged from `39047cc`: `BuildNotes/online/FusionCompile/bin/Debug/net9.0/FusionCompile.dll` SHA-256 `3433695E0BB93A26B726074A7BE59032E6E1010578281B4294313381C5289E23`; companion rules DLL `0022A959B38C5E4F46B94BD62EAEC565AA22ED7537CBA735B3B1086BDCCAC28B`. Results:

```text
Return:     Advanced=True, Receipt.Sequence=1, Receipt.Effect.Code=NotAlive
Disconnect: Advanced=True, Receipt.Sequence=1, Receipt.Effect.Code=NotAlive
```

F3 and F4 remain confirmed at this checkpoint; their fixes are owned by the online lane and are not yet independently verified. No additional confirmed lease/replay/queued-command defect was found in the coverage above. This report does not approve live authentication, deployed service configuration, native migration, or physics. Next recoverable action: verify the owner's committed lifecycle fencing and nonce-bound client host-proof corrections with the same failing probes and corresponding native integration gates.

### F3 repair verification — IN PROGRESS

The implementation owner supplied an uncommitted per-run scope repair. Captured session source SHA-256: `5170630DF80894AF47834E310E593B01DC1317D6770A224D81765BCFC8DC3B30`. The independent probe compiled the exact `RunScope`, current-run helpers and `Admit` method in memory with controlled dependencies.

```text
lateFailure=True  currentSessionFailure= newAdmissions=0 newAuthorizationMarker=True oldMarker=False oldDisconnects=0
lateFailure=False currentSessionFailure= newAdmissions=0 newAuthorizationMarker=True oldMarker=False oldDisconnects=0
```

The old admission success/failure and same-player cleanup defect is corrected in that captured source. Broader source review found one remaining terminal-state path: `Fail` does not invalidate the run, and `BeginAfterCheckpoint` checks Failure only before awaiting publication. The checkpoint continuation can therefore restore readiness after a concurrent terminal failure. The exact `BeginAfterCheckpoint` and `Fail` method bodies with a deferred publication dependency reproduced:

```text
failure=TimeoutException gameplayReady=True authorityTransportReady=True gateFrozen=True
```

This demonstrates contradictory terminal/UI/transport readiness, not that domain gameplay actually advances through the frozen gate. The concrete production trigger is a host lease failure while checkpoint push awaits; publication's post-push guard also omits the Failure check. The owner received this residual for correction before F3 is marked fully verified.

The owner then changed terminal failure to set Failure and invalidate the active run before freezing. Independent repeat against captured source SHA-256 `EBF9359498933FBF3EDB57B62C66B92C82D266658D5C58A12147AE6620E8101D` passes all three interleavings:

```text
lateFailure=True  currentSessionFailure= newAdmissions=0 newAuthorizationMarker=True oldMarker=False
lateFailure=False currentSessionFailure= newAdmissions=0 newAuthorizationMarker=True oldMarker=False
failure=TimeoutException gameplayReady=False authorityTransportReady=False gateFrozen=True
```

This verifies F3 and its terminal-state residual in that exact captured local source. A committed revision pin and broader owner lifecycle harness are pending. F4 host proof remains pending implementation/independent verification.

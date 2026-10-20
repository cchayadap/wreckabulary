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

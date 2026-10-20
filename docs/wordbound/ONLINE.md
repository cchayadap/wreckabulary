# Wordbound coordinator

STATUS: LOCAL SERVICE VERIFIED. This is the separate, transport-independent service contract. It does not establish Photon connectivity or successful host migration.

All routes are under `/v1`. JSON bodies reject unknown fields and exceed neither 16 KiB nor their schema limits. Authentication is `Authorization: Bearer <accessToken>`. The caller identity is obtained from that token, never from a supplied player ID. Party mutations require `Idempotency-Key` (8–96 ASCII letters, digits, underscores or hyphens). Reusing a key with a different operation fails.

`Settings`: `{mode, format, fill, region, protocolVersion, mapPoolVersion}`. Mode uses the five IDs in CONTRACTS; race format is `Solo|Duo|Squad`; other modes require `Solo`; region is `asia|eu|us`; versions are 1–24 ASCII identifier characters. TimeRun capacity is four; FFA capacity is one per individual; Freestyle is private with up to eight occupants across invited parties. A party has at most four members. FFA/Freestyle require Fill false.

`ContentBinding`: `{kind, courseId, courseHash, manifestHash, modeHash, catalogHash}`. Kind is `BuiltIn|Workshop`; ID is an allowlisted content identifier; each hash is 64 lower-case hexadecimal characters. The map producer owns the canonical bytes. `manifestHash` identifies the exact resolved manifest accepted by all clients; `courseHash` binds its source course. The coordinator checks binding equality and public approval, not the geometry or executability of an unseen file. Every client must independently validate the actual bounded manifest before accepting it.

All revisions/generations are decimal strings. UTC deadlines are Unix milliseconds. IDs are server-minted UUIDs. Pool keys are returned with the queued attempt and comprise region, protocol, map-pool version, mode and format, separated by colons. URL-encode that key in routes.

| Method/path | JSON body | Result |
|---|---|---|
| POST `/guests` | `{displayName}` | `{playerId, accessToken, refreshToken, expiresIn:900}` |
| POST `/auth/refresh` | `{refreshToken}` | Rotated credentials; old refresh and access tokens cease to work. |
| GET `/me` | None | Own player ID, display name and current party ID. |
| GET `/keys` | None | Public RS256 JWK set; no private fields. |
| POST `/photon/tickets` | `{}` | Authenticated guest obtains `{ticket, appId, expiresAt}` for one Photon authentication attempt. |
| POST `/parties` | `{settings}` | Own revisioned party and invite code. |
| POST `/party-joins` | `{partyId, invite}` | Join as authenticated self, cancelling an old queue first. |
| GET `/parties/{id}` | None | Member-only party, attempt and current assignment. |
| PATCH `/parties/{id}` | `{expectedRevision, settings}` | Leader changes settings after cancellation. |
| POST `/parties/{id}/ready` | `{expectedRevision, ready}` | Set own readiness. |
| POST `/parties/{id}/queue` | `{expectedRevision, channel, content?, code?}` | Locked whole-party attempt. |
| POST `/parties/{id}/cancel` | `{expectedRevision}` | Leader cancels; bumps revision and clears readiness. |
| POST `/parties/{id}/leave` | `{expectedRevision}` | Leave as self after cancellation. |
| POST `/pools/{key}/matches/{id}/admission` | `{}` | Own signed grant, connection secret and claims. |
| POST `/pools/{key}/matches/{id}/certificate` | `{}` | Signed match-start certificate; available to current members only after all admissions commit. |
| POST `/pools/{key}/matches/{id}/ready` | `{reservationGeneration, manifestHash, rosterHash}` | Accept exact locked content and roster. |
| POST `/pools/{key}/matches/{id}/launch` | `{}` | Private owner locks a Freestyle/FFA roster. |
| POST `/pools/{key}/matches/{id}/presence` | `{connected}` | Own 120-second reconnect lease/deadline. |
| POST `/pools/{key}/matches/{id}/reconnect` | `{}` | Original member receives same seat/secret grant if grace remains. |
| POST `/pools/{key}/matches/{id}/return` | `{}` | Return own seat to lobby permanently; that seat can no longer reconnect. |

Queue channels:

- `QuickPlay` requires approved BuiltIn content; it may fill a compatible private built-in session.
- `CustomFill` requires Fill On and no supplied content. It can only fill teammate vacancies in private Workshop sessions. The actual assigned manifest still requires explicit client acceptance.
- `PrivateCreate` requires content and returns a random 16-hex-digit private code in assignment status. Private UI defaults Fill Off.
- `PrivateJoin` requires that code and no content. Settings must match the selected session. Whole parties reserve one team.

Fill Off consumes the entire team capacity while leaving its unused seats empty. Fill On can combine consenting parties without splitting them. Private random fill never creates random opponents in an empty opposing team. Invite another party by code to occupy that team. FFA public staging starts with eight players or, after 30 seconds, at least two; no bots or altered format. Private Freestyle is explicitly launched by its owner.

The pool owns cancellation/start ordering. Cancellation tombstones prevent delayed enqueue revival. A party's durable pending operation prevents concurrent roster/settings changes during cross-object calls; interrupted operations retry with the same key and body. Do not retry a pending operation using a new key. After start, a party can cancel its old attempt and edit/requeue only once every member has returned or exhausted reconnect grace. Other teams can continue that match; its old seats never reopen.

Staging uses a 90-second deadline. Every member must accept the same generation, manifest and roster. Failure aborts the whole reservation, cancels the incomplete party, and returns unaffected public/custom-fill parties to their original queue age. No new players enter after admission commits.

Admission grants are RS256 JWTs with issuer, audience `wordbound-admission`, key ID and 90-second expiry. Claims bind version, authenticated subject/player ID, party/revision, attempt, match/generation, team/seat, protocol, course/mode/manifest/catalog hashes, roster hash and connection-secret hash. The seat is a zero-based slot within its team, matching the Unity contract. The separate random secret travels in the transport's short connection token. Public roster entries expose only its SHA-256 commitment (canonical JSON encoding of the string), not the secret. The host must also verify Photon-authenticated identity matches that seat. The service's `STARTED` means roster admission committed; the simulation still waits for actual verified transport connections before advancing gameplay.

The start certificate uses the same public key, a separate audience `wordbound-match-start` and a 90-second expiry. Its claims are `{version:1, matchId, reservationGeneration, startedAt, settings, content, rosterHash, roster}` plus standard JWT issuer, audience, issued-at and expiry. It contains every public roster entry and no connection secrets. A staging grant by itself is insufficient: a cancelled staging roster cannot obtain this certificate. Every client verifies the certificate before entering gameplay.

Unity adapter sequence:

1. Authenticate the guest, retain protected credentials, create/join a party and display the revisioned settings.
2. Each member marks the current party revision ready. The leader submits one queue attempt and retains its idempotency key until it receives a definitive response.
3. Poll the member-only party endpoint. When assignment becomes `ADMITTING`, download and independently validate the assigned content, then show its exact content acceptance. This is mandatory for `CustomFill`.
4. Obtain the own admission grant and short connection secret. Verify RS256, configured issuer, audience, key ID, time validity, authenticated identity, roster membership, team-local slot and every content binding. Never trust the unverified response `claims` alongside the JWT.
5. Submit the exact reservation generation, manifest and roster hash to `/ready`. Once all members accept, obtain and verify the start certificate with its distinct audience. Check its settings against the locally derived mode definition and its roster hash against canonical roster bytes.
6. The transport adapter admits only the fixed identities and secrets, matching Photon-authenticated identity to the coordinator identity. It must validate actual transport arrivals and the match certificate before advancing simulation. Membership in a Photon room alone is insufficient.
7. Renew presence while connected; on reconnect, obtain a fresh grant for the same seat and validate it again. The simulation enforces its own authoritative disconnect deadline as well. A valid JWT never overrides a returned seat or an expired host-side reconnect deadline.
8. On voluntary return, call `/return`. Once the party has all returned or timed out, the leader cancels the old attempt, obtains the new revision and marks the next round ready.

The coordinator never decides who is Photon host, advances host epochs, receives command ledgers or attests gameplay snapshots. Those remain the transport and authoritative simulation's responsibilities. The transport must reject stale host epochs and restore a durably acknowledged complete snapshot; no HTTP claim that a caller is host can grant that authority.

Refresh credentials expire after 30 days and are stored only as hashes. Persist the client's credential using OS credential protection, not Unity PlayerPrefs. Refresh rotation invalidates previous access tokens. A lost refresh response requires a new guest in this initial implementation; there is no email recovery.

Connected clients renew their own presence lease regularly (recommended every ten seconds). Explicit disconnect fixes a deadline once; repeats do not extend it. Abrupt disconnect retains the deadline from the last presence renewal. Migration does not call a host-authorized deadline-reset API. Reconnect uses wall time and never creates new entities or inventory; those are simulation responsibilities.

Real Durable Object alarms advance the public FFA minimum-player timer and staging expiry even without polling. Queue attempts expire after 24 hours; tombstones remain for 48 hours, longer than the accepted attempt lifetime. Expired attempts cannot revive after tombstone cleanup. Terminal match records are reclaimed on subsequent pool activity.

Local setup: `npm ci`, `npm run types`, `npm run verify` inside `OnlineServices`. Tests generate ephemeral signing keys in memory. Production configuration intentionally lacks signing secrets, an account ID and approved maps. Authentication/admission fail closed until configured. No deployment has been performed. `npm run dev` uses local Workers storage; provision ignored `.dev.vars` manually for an interactive local client.

`ACCESS_SECRET` is a server-only random secret of at least 43 characters. `ADMISSION_PRIVATE_JWK` is a server-only RSA private JWK; `SIGNING_KEY_ID` identifies its public key; `TOKEN_ISSUER` must be pinned by the client. `BUILTIN_CONTENT_JSON` is the exact array of approved content bindings. It defaults to an empty array. Never embed signing keys or guest refresh tokens in Unity assets or commit them. Before external rollout, configure the approved account, signing-key rotation, request-abuse limits and validate actual transport identity integration. No public-load or deployed-service claim is made by these local tests.

The API returns `{error:{code}}` on failure. HTTP 400 means invalid schema/JSON, 401 invalid credentials, 403 wrong member/leader, 404 absent/private-closed match, 409 revision/lifecycle/content conflict, 413 oversized request, 415 wrong content type, 422 unapproved content, 429 pool capacity, and 503 missing service configuration. Retry an interrupted party operation with the original key and body; refresh the party view after a definitive revision conflict. A party has a bounded operation history; `PARTY_OPERATION_LIMIT` requires leaving and creating a new party, with leave remaining available at that limit.

Implementation follows Cloudflare's [SQLite Durable Object storage](https://developers.cloudflare.com/durable-objects/api/sqlite-storage-api/), [Workers Vitest integration](https://developers.cloudflare.com/workers/testing/vitest-integration/), and [RPC lifecycle rules](https://developers.cloudflare.com/workers/runtime-apis/rpc/lifecycle/). RPC results are cloned to plain values and explicitly disposed at boundaries.

Photon custom authentication is implemented independently of its SDK. Configure `PHOTON_APP_ID` with the approved Fusion app UUID and `PHOTON_PROVIDER_SECRET` with a separate server-only random secret of at least 43 characters. The client obtains an authenticated `/photon/tickets` response and submits only `{ticket}` through `AuthenticationValues.SetAuthPostData(Dictionary<string,object>)`, with `AuthType = CustomAuthenticationType.Custom`, passed as `StartGameArgs.AuthValues`.

The provider route is `POST /v1/photon/auth`. Its query has exactly two dashboard-owned values, `appId` and `providerKey`; its JSON body has exactly `ticket`. It verifies the provider secret, configured app, signature, issuer, app-specific audience `wordbound-photon:<app UUID>`, identity credential version, nonce and exclusive 60-second expiry. Ticket consumption is transactional and single-use. Photon receives HTTP 200 with `ResultCode:1` and the coordinator's `UserId` on success; invalid credentials return code 2 and malformed parameters return code 3, without a user ID. Missing configuration fails closed with HTTP 503. It never accepts a body/query user ID. Obtain a fresh ticket after an interrupted authentication or when starting a new Runner; Photon handles subsequent server switches with its own encrypted authentication token.

Dashboard setup and deployment are **NOT RUN**. When authorized, set the Custom provider URL to the deployed `/v1/photon/auth` route, configure static query values `appId` and `providerKey`, reject anonymous clients, and reject clients when the provider is unavailable. Clients must never contain `providerKey`. The worker disables automatic invocation logs because dashboard secrets arrive in query parameters; retain this redaction boundary in any reverse proxy or request telemetry. Do not log tickets, full provider URLs or raw authentication errors.

The request/response contract and dashboard behavior are documented in the official [Fusion 2 custom authentication guide](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/authentication/custom-authentication) and [Realtime 5 guide](https://doc.photonengine.com/realtime/v5/connection-and-authentication/authentication/custom-authentication). This local endpoint test is not evidence of a configured Photon application.

Runtime evidence, tests and remaining limitations are recorded in `BuildNotes/online/STATUS.md`.

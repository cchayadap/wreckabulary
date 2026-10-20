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
| POST `/pools/{key}/matches/{id}/presence` | `{connected}` | Pre-transport own presence; connected=true is refused after a transport connection has been authorized. |
| POST `/pools/{key}/matches/{id}/connection-tickets` | `{}` | Own opaque, single-use 15-second transport authorization ticket. |
| POST `/pools/{key}/matches/{id}/connections` | `{playerId,ticket,connectionSecret,connectionId}` | Authenticated original roster member consumes a target's ticket and secret, receiving a signed 15-second connection lease. |
| POST `/pools/{key}/matches/{id}/connection-state` | `{renew:[{playerId,connectionId,connectionEpoch}]}` | Full current roster connection/revocation state; renews only leases belonging to the authenticated caller. |
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
7. Obtain a fresh connection ticket after Photon connects and generate a fresh client UUID `connectionId` for that Runner. The host consumes both only after verifying the callback's authenticated Photon identity, seat grant and transport secret. It returns the signed connection authorization to the client. The client verifies its own identity, nonce, exact match/roster and a host identity in that roster before sending Ready or accepting snapshots. The host renews the resulting lease every five seconds. Reconnect obtains fresh credentials and a new nonce for the same seat. The simulation enforces its original authoritative disconnect deadline as well. A valid JWT never overrides a returned seat or an expired host-side reconnect deadline.
8. On voluntary return, call `/return`. Once the party has all returned or timed out, the leader cancels the old attempt, obtains the new revision and marks the next round ready.

The coordinator never decides who is Photon host, advances host epochs, receives command ledgers or attests gameplay snapshots. Those remain the transport and authoritative simulation's responsibilities. The transport must reject stale host epochs and restore a durably acknowledged complete snapshot; no HTTP claim that a caller is host can grant that authority.

Refresh credentials expire after 30 days and are stored only as hashes. Persist the client's credential using OS credential protection, not Unity PlayerPrefs. Refresh rotation invalidates previous access tokens. A lost refresh response requires a new guest in this initial implementation; there is no email recovery.

Transport connections use a 15-second lease renewed by the host every five seconds. The host polls full connection state and immediately disables a returned or superseded seat when observed; a service outage disables a seat when its last lease expires. This bounds revocation latency; it does not make an HTTP return atomic with an already-running gameplay tick. Explicit disconnect fixes a reconnect deadline once; repeats do not extend it. Abrupt disconnect conservatively retains the coordinator deadline from the last renewal, while the domain separately enforces its observed disconnect deadline. Migration cannot renew an expired lease. Reconnect uses wall time and never creates new entities or inventory; those are simulation responsibilities.

Real Durable Object alarms advance the public FFA minimum-player timer and staging expiry even without polling. Queue attempts expire after 24 hours; tombstones remain for 48 hours, longer than the accepted attempt lifetime. Expired attempts cannot revive after tombstone cleanup. Terminal match records are reclaimed on subsequent pool activity.

Local setup: `npm ci`, `npm run types`, `npm run verify` inside `OnlineServices`. Tests generate ephemeral signing keys in memory. The separate `wrangler.production.jsonc` pins the selected website account and Photon app; source contains no signing secrets and the public map allowlist remains empty. The coordinator is deployed at `https://wreckabulary-wordbound-coordinator.pagna.workers.dev`; its live HTTP and eight-seat private API checks pass. `npm run dev` uses local Workers storage; provision ignored `.dev.vars` manually for an interactive local client. `npm run production:preview` only validates and bundles locally. Exact deployment evidence and provider instructions are in `BuildNotes/online/DEPLOYMENT.md`; `OnlineServices/production-client.json` contains the public client trust configuration.

`ACCESS_SECRET` is a server-only random secret of at least 43 characters. `ADMISSION_PRIVATE_JWK` is a server-only RSA private JWK; `SIGNING_KEY_ID` identifies its public key; `TOKEN_ISSUER` must be pinned by the client. `BUILTIN_CONTENT_JSON` is the exact array of approved content bindings. It defaults to an empty array. Never embed signing keys or guest refresh tokens in Unity assets or commit them. Before external rollout, configure the approved account, signing-key rotation, request-abuse limits and validate actual transport identity integration. No public-load or deployed-service claim is made by these local tests.

The API returns `{error:{code}}` on failure. HTTP 400 means invalid schema/JSON, 401 invalid credentials, 403 wrong member/leader, 404 absent/private-closed match, 409 revision/lifecycle/content conflict, 413 oversized request, 415 wrong content type, 422 unapproved content, 429 pool capacity, and 503 missing service configuration. Retry an interrupted party operation with the original key and body; refresh the party view after a definitive revision conflict. A party has a bounded operation history; `PARTY_OPERATION_LIMIT` requires leaving and creating a new party, with leave remaining available at that limit.

Implementation follows Cloudflare's [SQLite Durable Object storage](https://developers.cloudflare.com/durable-objects/api/sqlite-storage-api/), [Workers Vitest integration](https://developers.cloudflare.com/workers/testing/vitest-integration/), and [RPC lifecycle rules](https://developers.cloudflare.com/workers/runtime-apis/rpc/lifecycle/). RPC results are cloned to plain values and explicitly disposed at boundaries.

Photon custom authentication is implemented independently of its SDK. Configure `PHOTON_APP_ID` with the approved Fusion app UUID and `PHOTON_PROVIDER_SECRET` with a separate server-only random secret of at least 43 characters. The client obtains an authenticated `/photon/tickets` response and submits only `{ticket}` through `AuthenticationValues.SetAuthPostData(Dictionary<string,object>)`, with `AuthType = CustomAuthenticationType.Custom`, passed as `StartGameArgs.AuthValues`.

The provider route is `POST /v1/photon/auth`. Its query has exactly two dashboard-owned values, `appId` and `providerKey`; its JSON body has exactly `ticket`. It verifies the provider secret, configured app, signature, issuer, app-specific audience `wordbound-photon:<app UUID>`, identity credential version, nonce and exclusive 60-second expiry. Ticket consumption is transactional and single-use. Photon receives HTTP 200 with `ResultCode:1` and the coordinator's `UserId` on success; invalid credentials return code 2 and malformed parameters return code 3, without a user ID. Missing configuration fails closed with HTTP 503. It never accepts a body/query user ID. Obtain a fresh ticket after an interrupted authentication or when starting a new Runner; Photon handles subsequent server switches with its own encrypted authentication token.

Deployment and direct provider HTTP checks are complete; Photon dashboard setup and the actual custom-auth Runner handshake remain **NOT RUN**. Root will configure the provider after its current anonymous native probes finish. Set the Custom provider URL to the deployed `/v1/photon/auth` route, configure static query values `appId` and `providerKey`, reject anonymous clients, and reject clients when the provider is unavailable. Clients must never contain `providerKey`. The worker disables automatic invocation logs because dashboard secrets arrive in query parameters; retain this redaction boundary in any reverse proxy or request telemetry. Do not log tickets, full provider URLs or raw authentication errors.

The request/response contract and dashboard behavior are documented in the official [Fusion 2 custom authentication guide](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/authentication/custom-authentication) and [Realtime 5 guide](https://doc.photonengine.com/realtime/v5/connection-and-authentication/authentication/custom-authentication). This local endpoint test is not evidence of a configured Photon application.

C# entry points are additive and SDK-independent:

- `OnlineCoordinatorClient` takes `IOnlineHttpTransport`. Its party methods require the caller's stable operation key; it performs no implicit retries. `CreateGuest` and `Refresh` return credentials for protected local storage. `ClearCredentials` fences outstanding authentication replies. `PhotonTicket().PhotonPostData()` gives the exact dictionary for Photon.
- `WordboundHttpTransport` implements real HTTP with a pinned HTTPS origin, no redirects, a 15-second deadline covering headers and body, and a 32 KiB response bound. HTTP is available only through an explicit loopback testing option. No global certificate-validation override is used.
- `OnlineAdmissionVerifier(pinnedIssuer, trustedJwks)` uses standard-library RSA/PKCS1/SHA256. Fetch keys only through the pinned coordinator origin. Untrusted JWT headers cannot redirect key lookup. `VerifyStart(certificate, expectation, unixSeconds)` returns an immutable verified roster. `VerifyAdmission(grant, verifiedMatch, photonVerifiedPlayerId, connectionSecret, unixSeconds)` returns the matching immutable seat. Expired certificates must be refreshed for a new admission; an expiry does not revoke already-established gameplay mappings.
- `OnlineMatchExpectation` contains match ID, reservation generation, roster hash, independently checked settings and exact content hashes. A valid signature cannot substitute different expectations. The verifier rejects unknown/duplicate fields, malformed UTF-8/base64, excessive size/depth, non-integer numeric claims, invalid time windows, duplicate/split players, incorrect mode/team capacities and wrong secret commitments. Counters remain decimal strings.

The host transport must obtain identity from `NetworkRunner.GetPlayerUserId(player)`, and the connection secret from `GetPlayerConnectionToken(player)`, not an RPC identity field. The [Fusion API](https://doc-api.photonengine.com/en/fusion/current/class_fusion_1_1_network_runner.html) exposes remote user IDs to the server in client/server mode. The short connection token contains UTF-8 bytes of the 43-character secret, not the JWT. A joining peer can deliver its longer grant through a bounded reliable admission message after connecting, while remaining unspawned and unable to submit gameplay commands until verification succeeds. Limit that pending state and disconnect failures. Rejoining must also pass the authoritative simulation's original wall-clock deadline and epoch checks before input authority is reassigned.

Local C# verification:

```text
dotnet run --project Tools/RulesHarness/RulesHarness.csproj -- WordboundOnline
dotnet build BuildNotes/online/OnlineTransport.csproj
node OnlineServices/scripts/write-verifier-fixture.mjs BuildNotes/online/generated/jose-fixture.json
dotnet run --project BuildNotes/online/Interop/Interop.csproj -- BuildNotes/online/generated/jose-fixture.json
```

The last two commands test independent JavaScript JOSE tokens and canonical hashes against the C# verifier, then test actual loopback HTTP headers, body, redirect rejection and response bounds. Private RSA keys remain in process memory; generated public test vectors and test secrets stay in the ignored `generated/` folder. No new C# package is required: rules target netstandard2.1; the standalone test runner uses the installed .NET runtime. Unity native/IL2CPP behavior and the actual Fusion callback hookup still require the integrator's tests. Only root launches Unity.

Runtime evidence, tests and remaining limitations are recorded in `BuildNotes/online/STATUS.md`.

Connection authorization is separate from the reusable admission grant. A target obtains `{ticket,expiresAt}` as authenticated self; no supplied identity can mint another player's ticket. `/connections` accepts a UUID `connectionId` and consumes the ticket transactionally with the exact secret commitment. Its response contains a signed token whose audience is `wordbound-connection`, with `{version,matchId,reservationGeneration,rosterHash,playerId,hostId,connectionId,connectionEpoch,leaseExpiresAt,connectionRevision}` and standard issuer/time claims. Epoch and revision are decimal strings; the lease deadline is Unix milliseconds. An exact retry for a live connection ID is idempotent. Reusing it with different inputs, replaying the consumed ticket under another ID, or authorizing after return fails.

`/connection-state` accepts at most eight unique renewals. It returns `{matchId,reservationGeneration,rosterHash,revision,serverTime,seats}`. Each seat has `{playerId,returned,connectionEpoch,connectionId,hostId,leaseExpiresAt,reconnectDeadline}`. IDs without a current connection are null; a revoked or returned lease has deadline zero. Renewal requires matching authenticated host ID, connection ID and epoch and an unexpired lease and reconnect window. Returning deletes any pending ticket, advances the epoch and revokes the lease. The client validates the complete original roster and binding before updating any local lease. This service does not independently attest who Photon selected as host: a roster member can authorize only a peer whose one-use ticket and secret it possesses, and the actual host callback must supply Photon-verified identity. It is a listen-host trust model, not server-authoritative anti-cheat.

Actual SDK adapter entry points:

- `WordboundFusionSession` takes a fresh Runner factory, copied app settings, project config, registered snapshot prefab, pinned verifier/expectation, fresh credential callback, authority bridge and `OnlineConnectionService`. Obtain fresh Photon tickets for every Runner, including migration. Call `Poll()` every frame, `MarkLocalReady()` only after local content/world preparation, and schedule `PublishCheckpoint()` while the host advances. The authority freezes beyond its durable-ack window. `GameplayStarted` is raised after actual verified readiness and the initial durable checkpoint. Client `ReplicaReceived` must apply the verified replica to visuals before input starts.
- `WordboundFusionAuthority` binds `AuthoritativeMatch` to the signed original content and roster. Its `Advance` receives trusted host bodies, observations, damage and mechanism state. Client command payloads cannot supply an actor, host damage or host physics. Commands remain queued if a tick is paused or throws. Queued commands are discarded on migration and must be retried by the client using their original IDs.
- `WordboundFusionSnapshotChunk` is a `NetworkBehaviour` in assembly `Wreckabulary.Online.Fusion`. The native installer must create a prefab with a baked `NetworkObject` and this component, register it in Fusion's prefab table, pass its `NetworkPrefabRef`, and include the assembly in Fusion's weaver list. The prefab needs no renderer, collider or Rigidbody. Keep `HostMigration.EnableAutoUpdate=false`; enable client-to-server reliable data and disable client-to-client server proxying.

Snapshots retain at most 8 MiB of raw canonical JSON and 256 KiB of compressed envelope data, split into at most 64 chunks of 4 KiB. The networked component stores each chunk as 1024 uints plus the exact match/binding/epoch/sequence/tick/state/wire hashes. Every chunk must agree, be present exactly once, and validate the complete payload before restore. Mixed, incomplete, oversized or altered snapshots fail closed. Compression is lossless and never removes content. Initial publication preflights the complete snapshot before gameplay; later growth can still exceed the limit and must pause rather than truncate.

The session acknowledges the authoritative journal only after the exact carrier's `PushHostMigrationSnapshot()` returns true, then sends bounded reliable replica chunks. Peer ACKs are diagnostics, not durable commits. Migration creates a new Runner, validates only its complete resume carrier, restores that committed state under a strictly newer epoch, replays newer coordinator return denials, and re-admits authenticated original seats. The 256 KiB aggregate migration capacity and meaning of successful cloud push still require the root's native migration experiment; successful source compilation and ordinary reliable data transfer do not prove those properties.

Focused compile with installed official SDKs and the parallel domain source:

```text
dotnet build BuildNotes/online/FusionCompile/FusionCompile.csproj
dotnet run --project BuildNotes/online/FusionCompile/BridgeTests.csproj
```

The project reads the sibling integration SDK/compiled Unity assemblies and puzzle-race rules without copying them; paths can be overridden with `IntegrationRoot`, `PuzzleRoot` and `UnityRoot`. The proprietary SDK is not in Git. Native prefab registration/weaving, authenticated deployed provider, full multi-peer admission, migration and dynamic player/item physics are separate integration gates and are not claimed by this compile.

The second command runs the actual authority bridge against the domain with the existing standalone NUnit shim. Its six regressions cover ready-host authority, queued requests surviving a paused/failed tick, exact committed restore, fixed reconnect deadlines and permanent return. The same source is in `Wreckabulary.Online.Fusion.Tests` for native Unity NUnit verification. Root's Unity importer generates all new asset metadata.

`WordboundFusionSession.TryGetAdmittedActor(PlayerRef, out string)` is the host physics/input boundary. It requires the current Runner, active authenticated peer, ready seat and unexpired caller-owned connection lease. It denies input at lease expiry even between polling frames. The method returns false on clients.

Each Runner owns a separate operation scope. Migration, Stop and terminal failure invalidate it before shutdown; late HTTP/cloud successes and failures cannot change the replacement's readiness, publication flags or admission locks. Reproduce deferred-task regressions with `dotnet run --project BuildNotes/online/FusionCompile/SessionLifetimeTests.csproj -- SessionLifetimeTests`. This harness compiles the complete actual session source against scheduling doubles for SDK calls and the real C# token/client contracts; it proves lifecycle ordering, not native cloud durability. The actual SDK compilation and native migration experiment remain separate checks.

Client admission also authenticates the host's roster authority. `Admission` carries `{certificate,grant,connectionTicket,connectionId}`; `Admitted` carries `{roster,authorization}`. `VerifyHostAuthorization` accepts only a coordinator-signed, unexpired connection authorization for the client's fresh nonce, own identity and exact verified match, with the host in the original roster. The host identity is pinned for that Runner. Begin and replica messages before this proof fail closed. The host sends snapshots only after that peer's Ready, which confirms receipt of the proof. A guessed room name or copied roster hash cannot authenticate a room creator. This retains the declared listen-host trust model: an approved host remains trusted to run gameplay.

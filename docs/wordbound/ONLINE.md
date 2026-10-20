# Wordbound coordinator

STATUS: IN PROGRESS. This is the separate, transport-independent service contract. It does not establish Photon connectivity or successful host migration.

All routes are under `/v1`. JSON bodies reject unknown fields and exceed neither 16 KiB nor their schema limits. Authentication is `Authorization: Bearer <accessToken>`. The caller identity is obtained from that token, never from a supplied player ID. Party mutations require `Idempotency-Key` (8–96 ASCII letters, digits, underscores or hyphens). Reusing a key with a different operation fails.

`Settings`: `{mode, format, fill, region, protocolVersion, mapPoolVersion}`. Mode uses the five IDs in CONTRACTS; format is `Solo|Duo|Squad`; region is `asia|eu|us`; versions are 1–24 ASCII identifier characters. TimeRun capacity is four regardless of format; FFA capacity is one per individual; Freestyle is private with up to eight occupants. FFA/Freestyle require Fill false.

`ContentBinding`: `{kind, courseId, courseHash, manifestHash, modeHash, catalogHash}`. Kind is `BuiltIn|Workshop`; ID is an allowlisted content identifier; each hash is 64 lower-case hexadecimal characters. The map producer owns the canonical bytes. `manifestHash` identifies the exact resolved manifest accepted by all clients; `courseHash` binds its source course. The coordinator checks binding equality and public approval, not the geometry or executability of an unseen file. Every client must independently validate the actual bounded manifest before accepting it.

All revisions/generations are decimal strings. UTC deadlines are Unix milliseconds. IDs are server-minted UUIDs. Pool keys are returned with the queued attempt and comprise region, protocol, map-pool version, mode and format, separated by colons. URL-encode that key in routes.

| Method/path | JSON body | Result |
|---|---|---|
| POST `/guests` | `{displayName}` | `{playerId, accessToken, refreshToken, expiresIn:900}` |
| POST `/auth/refresh` | `{refreshToken}` | Rotated credentials; old refresh and access tokens cease to work. |
| GET `/me` | None | Own player ID, display name and current party ID. |
| GET `/keys` | None | Public RS256 JWK set; no private fields. |
| POST `/parties` | `{settings}` | Own revisioned party and invite code. |
| POST `/party-joins` | `{partyId, invite}` | Join as authenticated self, cancelling an old queue first. |
| GET `/parties/{id}` | None | Member-only party, attempt and current assignment. |
| PATCH `/parties/{id}` | `{expectedRevision, settings}` | Leader changes settings after cancellation. |
| POST `/parties/{id}/ready` | `{expectedRevision, ready}` | Set own readiness. |
| POST `/parties/{id}/queue` | `{expectedRevision, channel, content?, code?}` | Locked whole-party attempt. |
| POST `/parties/{id}/cancel` | `{expectedRevision}` | Leader cancels; bumps revision and clears readiness. |
| POST `/parties/{id}/leave` | `{expectedRevision}` | Leave as self after cancellation. |
| POST `/pools/{key}/matches/{id}/admission` | `{}` | Own signed grant, connection secret and claims. |
| POST `/pools/{key}/matches/{id}/ready` | `{reservationGeneration, manifestHash, rosterHash}` | Accept exact locked content and roster. |
| POST `/pools/{key}/matches/{id}/launch` | `{}` | Private owner locks a Freestyle/FFA roster. |
| POST `/pools/{key}/matches/{id}/presence` | `{connected}` | Own 120-second reconnect lease/deadline. |
| POST `/pools/{key}/matches/{id}/reconnect` | `{}` | Original member receives same seat/secret grant if grace remains. |

Queue channels:

- `QuickPlay` requires approved BuiltIn content; it may fill a compatible private built-in session.
- `CustomFill` requires Fill On and no supplied content. It can only fill teammate vacancies in private Workshop sessions. The actual assigned manifest still requires explicit client acceptance.
- `PrivateCreate` requires content and returns a random 16-hex-digit private code in assignment status. Private UI defaults Fill Off.
- `PrivateJoin` requires that code and no content. Settings must match the selected session. Whole parties reserve one team.

Fill Off consumes the entire team capacity while leaving its unused seats empty. Fill On can combine consenting parties without splitting them. Private random fill never creates random opponents in an empty opposing team. Invite another party by code to occupy that team. FFA public staging starts with eight players or, after 30 seconds, at least two; no bots or altered format. Private Freestyle is explicitly launched by its owner.

The pool owns cancellation/start ordering. Cancellation tombstones prevent delayed enqueue revival. A party's durable pending operation prevents concurrent roster/settings changes during cross-object calls; interrupted operations retry with the same key and body. Do not retry a pending operation using a new key. A party cannot mutate its roster after start.

Staging uses a 90-second deadline. Every member must accept the same generation, manifest and roster. Failure aborts the whole reservation, cancels the incomplete party, and returns unaffected public/custom-fill parties to their original queue age. No new players enter after admission commits.

Admission grants are RS256 JWTs with issuer, audience `wordbound-admission`, key ID and 90-second expiry. Claims bind version, authenticated subject/player ID, party/revision, attempt, match/generation, team/seat, protocol, course/mode/manifest/catalog hashes, roster hash and connection-secret hash. The separate random secret travels in the transport's short connection token. Public roster entries expose only its SHA-256 commitment (canonical JSON encoding of the string), not the secret. The host must also verify Photon-authenticated identity matches that seat. The service's `STARTED` means roster admission committed; the simulation still waits for actual verified transport connections before advancing gameplay.

Refresh credentials expire after 30 days and are stored only as hashes. Persist the client's credential using OS credential protection, not Unity PlayerPrefs. Refresh rotation invalidates previous access tokens. A lost refresh response requires a new guest in this initial implementation; there is no email recovery.

Connected clients renew their own presence lease regularly (recommended every ten seconds). Explicit disconnect fixes a deadline once; repeats do not extend it. Abrupt disconnect retains the deadline from the last presence renewal. Migration does not call a host-authorized deadline-reset API. Reconnect uses wall time and never creates new entities or inventory; those are simulation responsibilities.

Local setup: `npm ci`, `npm run types`, `npm run verify` inside `OnlineServices`. Tests generate ephemeral signing keys in memory. Production configuration intentionally lacks signing secrets, an account ID and approved maps. Authentication/admission fail closed until configured. No deployment has been performed. `npm run dev` uses local Workers storage; provision ignored `.dev.vars` manually for an interactive local client.

Runtime evidence, tests and remaining limitations are recorded in `BuildNotes/online/STATUS.md`.

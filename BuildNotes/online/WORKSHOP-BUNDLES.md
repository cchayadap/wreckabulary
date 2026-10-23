# Automatic workshop sharing contract

STATUS: CLIENT AND WORKER SOURCE VERIFIED; NATIVE ACCEPTANCE NOT RUN

On 2026-10-23, all 41 C# client/bundle/catalogue cases passed. The first run exposed a real empty-content parsing defect: the shared `Has` helper treats JSON null as absent. Workshop info now requires exactly the descriptor key and accepts its legitimate null value. Transport fixtures use actual HTTP null bytes. The actual C# authored exporter passed strict roundtrip and produced 367,999 expanded bytes, 26,868 compressed bytes and seven chunks. Its immutable source hash is `8dfea6e6b3e5de34491987743745eb8f7e33d7c38221122476cff2ef584d9e14`; resolved hash is `9600711e064371c67f8150cba27f09688e7d4d23beecb267baee302f07909923`.

Both TypeScript gates passed. The final actual Worker/HTTP/SQLite suite passed 98/98 across six test files, including real C# content through Party and Pool eviction, incomplete/expired uploads, completed-map reselection, lost-response recovery, access fencing and terminal retention. Malformed non-document payloads return 422 without selection. Native Photon/custom-map gameplay remains unverified.

The interoperability fixture retains exact compressed bytes plus independent C# SHA-256 commitments instead of duplicate expanded source copies: 447,758 to 37,016 bytes (91.7% smaller). Test discovery is explicitly limited to the real `test/` directory, preventing ignored source overlays from silently duplicating the suite. Evidence and commands are recorded in `WorkshopInterop/VERIFICATION.json`.

Entry points:

- `WordboundClientContent.FromAuthored(documentJson, manifestJson, installedGeometry, itemJson, ruleBookJson, modeId, format, seed)` imports and validates exactly the supplied draft and resolved preview. It never chooses another seed or resolves a replacement layout. It creates an immutable `OnlineWorkshopBundle` before any party/network side effect.
- `WordboundPartyMenu.OpenAuthoredPrivate(content, region, fill)` requires an existing guest and no party/pending launch, stages the map, and leaves the party menu visible. Existing Ready and Create private room actions remain explicit. `SelectedRegion`, `SelectedFill`, and `CanOpenAuthoredPrivate` support the root workshop handoff.
- `WordboundPartyCoordinator.CreateAuthoredParty(settings, content)` uploads before ready. Invitation, private-code and assigned Custom Fill paths automatically download and validate the assigned bundle before admission. Custom Fill still requires its separate exact-map consent.

Authenticated routes (all JSON request bodies retain the 16 KiB cap):

- POST `/v1/parties/{partyId}/content-begin`: `{expectedRevision,descriptor}`.
- POST `.../content-chunk`: `{expectedRevision,bundleId,index,data}` (base64url).
- POST `.../content-finalize`: `{expectedRevision,bundleId}`; returns updated party, increments revision and clears ready states exactly once for a new selection.
- POST `.../content-info`: `{}`; selected descriptor or null.
- POST `.../content-read`: `{bundleId,index}`; exact immutable chunk.
- POST `/v1/pools/{poolKey}/matches/{matchId}/content-info|content-read`: corresponding read body; only currently assigned, nonreturned seats, with reconnect deadline enforced after start.

Descriptor commits mode/format/seed/protocol, exact course/mode/manifest/catalogue binding, installed geometry/item/rule-source hashes, raw size, compressed packet hash/size/chunk count. `bundleId` is SHA-256 of the canonical descriptor without its own ID. Packet contains canonical source document and resolved manifest as data, with no scripts, asset files or remote resource URLs. C# validates document/manifest semantics against installed geometry, mode, items and effective gameplay rules. Server validates bounded canonical bytes, embedded bindings, source and manifest hashes; it does not claim to execute Unity/course semantic validation.

Limits: 2 MiB source document, 8 MiB combined expanded packet, 256 KiB compressed, at most64 chunks of4096bytes and4 concurrent HTTP requests. Oversize fails before party creation. Pending uploads expire after60s, completed unassigned source copies after24h; max3 records per party with old completed selections evicted before blocking a new draft. Active selected content and incomplete uploads are preserved within that bound. Expired source bytes leave the immutable selected descriptor in party metadata and block readiness/new creation; clients cannot silently reconstruct a built-in map. Leave and share a newly validated draft to recover. MatchPool owns an independent durable complete copy until normal terminal-match cleanup; owner departure does not erase it. Pool alarms include the existing final reconnect deadline, so an abandoned started match closes without relying on another client request, followed by the unchanged24h terminal retention cleanup.

Party mutations are revision/leader fenced. Pending bytes remain unreadable. Finalize atomically selects complete verified bytes and resets readiness. PrivateCreate carries an internal server-derived workshopId; Pool atomically copies bytes with the new reservation. Exact committed attempt replay resolves a lost response even if the source copy subsequently expired; a fresh match cannot be created without its matching complete bundle. Returned or cancelled assignments cannot read match content. Public Quick Play never accepts authored bundles; explicit private invitation/Custom Fill remains the sharing path.

Root owns the Workshop button, final launch composition, native acceptance, and deployment of the reviewed artifact. No service redeploy, secret change, Unity run, generated catalogue or live authored-map proof has occurred for this slice.

Storage implementation follows the current primary [SQLite Durable Object API](https://developers.cloudflare.com/durable-objects/api/sqlite-storage-api/) and [alarm contract](https://developers.cloudflare.com/durable-objects/api/alarms/): synchronous transaction boundaries protect local selection/copy, and each object schedules its earliest stored deadline through its single alarm. The local SQLite/HTTP gate is green; deployment and native acceptance remain root-owned.

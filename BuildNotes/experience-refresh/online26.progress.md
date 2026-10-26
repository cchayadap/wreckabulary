# Public maximum-roster identity visibility repair

STATUS: OPERATOR INSPECTION COMPLETE — abandoned match is CLOSED with expired leases, but all eight original party memberships remain. Match-data expiry is source-predicted; identities/parties have no automatic deletion path. No service mutation or runtime execution by this lane.

## Recovery and scope

- Lane `experience-refresh/online26`; checkout `wordbound-online`, branch `sethypagna/online`, starting HEAD `2f7b84822b888eeebea739fa0e3751a8bdfc219d`.
- Read the shared `wordbound-integration/.harness/recovery/LANES-20261026.md`, portable harness startup/core, investigate mode and continuity workflow. No `AGENTS.md` or `META-HARNESS.md` exists at the inspected workspace/worktree paths.
- Preserve the dirty `Assets/_Project/Scripts/Rules/Wordbound/ModeDefinition.cs`, untracked `BuildNotes/online/ProtocolV3/`, and five checkpoint logs. Write scope is this report and `Assets/_Project/Tests/OnlineGameplay/WordboundNativePublicEntryTests.cs` only.
- Preserve full-duration chain `8605ad59` through `2f7b8482`; exclude baseline-only `29ef9783` from integration.
- Root's failed capacity receipt is `BuildNotes/client-view/native-public-max-rosters-first-20261024.json` with `TestResults/editmode_20261024T041032Z.xml` on integration `9fce9f25`. The native failure must remain preserved.

## Initial observation

The capacity helper currently compares authenticated service player IDs to `peer.Runner.ActivePlayers.Select(peer.Runner.GetPlayerUserId)` on every peer. The installed cached Fusion Runtime XML documents that Client/Server mode exposes remote player user IDs only on the server. Diagnosis will verify the exact installed SDK and production binding before replacing this visibility assumption. Required invariants remain: exact native PlayerRef roster, authenticated identity mapping, canonical seats/progress, unchanged limits and all existing shared gameplay assertions.

## Confirmed diagnosis

- Inspected the preserved XML: all three capacity cases failed at `WordboundNativePublicEntryTests.cs:104`. Eight-player cases saw one non-null local ID and seven nulls; TimeRun saw one local ID and three nulls. Each reached `WORDBOUND_GAMEPLAY_OBSERVED` after the shared movement, command and exact checkpoint ACK checks. Cleanup reported zero failures for 8/8/4 clients and 8/8/1 parties. The original result stays FAIL; no first-failure file was edited.
- The actual installed integration `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml:15424–15432` documents the remote-user-ID visibility limitation in Client/Server mode. Installed Runtime DLL SHA-256: `D213B95E4694C167F5F6FAFFCBD75CA1E47F19A9249C91A4964F8342FAC9B560`; XML: `6F21BA1E72256631D74B0B5CA46BD5F3F223E348001F2EF55792A7FBED83D78C`. The current official [NetworkRunner API reference](https://doc-api.photonengine.com/en/fusion/current/class_fusion_1_1_network_runner.html) corroborates this contract under `GetPlayerUserId`.
- `WordboundFusionSession.StartRunner` uses `GameMode.Host`/`GameMode.Client`, production custom authentication and a post-start local SDK ID check against the admitted guest. Host admission verifies the start certificate, admission grant, connection authorization and current SDK ID before storing a lease.
- `WordboundFusionSession.TryGetAdmittedActor` (515–529 in this lane) is the public production guard used by commands and body input authority. It requires the active ready host/writer, active native player, command admission and unexpired lease, then cross-checks SDK identity against both lease and gate. No identity mapping needs to be invented or exposed to remote clients.
- The unchanged shared fixture already uses the correct visibility boundary in `AssertIdentities`: all user IDs on the host, each client's own ID locally. The failing maximum-roster helper was inconsistent with that existing contract.

## Bounded correction

Only the existing maximum-roster helper changes. It now derives the exact native reference set from every peer's `LocalPlayer`, requires the expected number of distinct references, and compares every peer's `ActivePlayers` against that exact set. The unique host must resolve all references to exactly the authenticated service roster. For every peer, its local SDK ID and the host's current `TryGetAdmittedActor(LocalPlayer)` must both equal its credential-derived `Actor`.

This preserves identity assertions at their supported observation points and strengthens the former implicit native-count check into exact PlayerRef equality. Missing/extra/duplicate references, wrong host identities, invalid/expired admissions, wrong local identity or incorrect signed/canonical seats still fail. The signed team/party/seat assertions, binding roster hash, canonical player IDs/teams/slots and connected/not-returned checks are unchanged. All 12 public declarations, default arguments, timeouts, shared gameplay/body/ACK checks and cleanup remain unchanged. No production authentication, privacy, configuration or runtime code changed.

## Verification and handoff

- `git diff --check`: PASS.
- Source diff: confined to `AssertPublicMaximumRoster`, 11 added lines and one removed line. API availability is established by existing compiled fixture calls to the same public session method and SDK method; no new dependency or import is introduced.
- Self-review checked failure sensitivity for exact native membership, identity bijection and live admission. Root was sent the diagnosis and requested to review the bounded source during integration. No independent review is claimed by this lane.
- Compiler, Unity, core, service, network, live client and runtime mutation tests: NOT RUN under the root-only execution boundary. No capacity PASS claim.
- Next recoverable action: integrate this scoped source/report commit, preserving the full-duration chain and excluding baseline `29ef9783`; root runs the three `ExternalPhotonPublicCapacityGameplay` cases with the same catalogue and unchanged internal limits, then records exact XML and 8/8/4-client cleanup. Keep the original 20261024T041032Z failure as separate evidence.

## Rerun follow-up opened 2026-10-26

Root supplied `BuildNotes/client-view/native-public-max-rosters-contract-20261026.json`, source `c23590b7674dc244f7670ef7d115d7f78bfb8b44`, and `TestResults/editmode_20261026T014532Z.xml`. Receipt summary is one passed, two failed, zero skipped, total 331.4352602 seconds. Root reports FFA eight-player PASS with clean service cleanup, Freestyle eight-player completed roster/gameplay checks but failed cleanup, and TimeRun failed preparation with `HttpRequestException`. This follow-up inspects those exact observations and read-only reachability; no client, service mutation, fixture rerun or production change is authorized to this lane.

Next recoverable action: classify raw XML failure/cleanup evidence and exact-source transport behavior, then perform a bounded public health GET only if a documented read-only endpoint exists. Distinguish current health from any unproven historical network cause.

## Rerun findings

The XML SHA-256 matches the receipt: `EFAD01C9ECAE208CBE5F80C1D448A87F23931A6BC4CD3168D164DB26F911E20C`. The inspected native fixture, HTTP transport, coordinator client and service sources have no diff against the tested `c23590b7` for the examined paths.

| Case | UTC interval / duration | Confirmed result |
| --- | --- | --- |
| FreeForAll, eight peers | 01:51:29–01:53:37 / 128.264201 s | PASS; real bodies/input/command, exact maximum native/authenticated/canonical roster, and zero cleanup failures for eight clients/eight parties |
| Freestyle, eight peers | 01:53:37–01:56:42 / 184.294440 s | Both gameplay and exact maximum roster markers reached; FAIL at teardown; service cleanup reports 24 operation failures: return 8, cancel 8, leave 8 |
| TimeRun, four requested peers | 01:56:42–01:57:00 / 18.673014 s | FAIL during prepare with `HttpRequestException` originating in `System.Net.Http.MonoWebRequestHandler.SendAsync`; zero cleanup failures, one constructed client and zero parties |

Freestyle's first three return attempts log `OtherException`, HTTP status 0, code `none`. All remaining 21 failures log `OperationCanceledException`, status 0, code `none`. These are exception classifications; they are **not 24 HTTP error responses or 24 proved server-side rejections**. `SafeFailureType` does not name `HttpRequestException` separately, so the first three cleanup exception types cannot be recovered from these diagnostics. No raw exception messages were exposed.

`WordboundHttpTransport` has a 15-second HttpClient timeout plus a linked 15-second request cancellation. Native teardown gives the entire sequential service cleanup one separate 60-second cancellation budget. Once that token is canceled, later return/cancel/leave awaits can fail immediately. The observed cancellation cascade is consistent with this budget exhausting after the early failures. The aggregate teardown count of 1 is consistent with the outer task-status branch (incomplete/canceled/faulted) rather than summing a completed task's 24 returned operation failures; the specific outer branch is not logged and is not asserted as proven. No deadline change is proposed from this evidence.

`Prepare` adds a client before awaiting `CreateGuest`. Consequently, TimeRun's one-client/zero-party state localizes the exception to the **first `POST /v1/guests`**, before party creation/allocation/Photon startup. It does not establish that one guest authenticated, nor prove that the service failed to create a guest before the response was lost. The shared 120-second prepare limit was not reached.

The nearby failures make interrupted transport/connectivity a plausible common cause, but the retained diagnostics cannot distinguish DNS, TLS, socket/connection, Unity Mono handling or a remote interruption. They do not establish an authentication rejection, rate limit, invalid capacity setting or service business-rule error. FFA's complete native pass and Freestyle's completed maximum-roster checks validate the identity correction for those observations; Freestyle still fails overall and TimeRun capacity remains unaccepted.

## Read-only reachability observation

Source `OnlineServices/src/index.ts` exposes unauthenticated `GET /v1/keys` before authentication and returns only the configured public key. There is no dedicated health endpoint in the inspected routing source. At **2026-10-26T02:06:43.3928129Z**, one unauthenticated GET to the pinned coordinator origin's `/v1/keys` returned **HTTP 200**, 616 bytes, one RSA public key, in **2161 ms**. A separate DNS lookup at 02:06:43.7936365Z resolved two IPv4 addresses. Only status/shape/timing were emitted; no key contents, tokens or credentials were printed. No redirect or TLS-validation relaxation was used.

This establishes current reachability of the public Worker/key route through PowerShell's current .NET transport. It does not reproduce Unity Mono, exercise authenticated Durable Object operations, prove sustained availability, or identify the historical failure cause. No client creation, service mutation, Unity/core job or fixture rerun was performed by this lane.

## Residual cleanup and exact next action

The affected Freestyle run used release `wordbound-20261023-v1`, isolated pool suffix `pub-da6f14f6b78f`, eight original guests and eight one-member parties. Keep that run's cleanup marked **unresolved**. A timeout can leave an operation's server outcome unknown. Do not relabel it clean because the public key route recovered or because a fresh run later passes.

The native cleanup path clears each client's in-memory credentials and disposes transports; it writes no credential-bearing recovery snapshot. The existing CLI `live-check-recovery.json` belongs to a different script and is not evidence of recoverable credentials for this native run. This report has not searched private credential stores or exported identities. No exact public-API cleanup replay is executable from the retained native receipt alone.

If root has a separately authorized, matching recovery context for these original guests, the bounded cleanup sequence is: read each original `/v1/me` and party state; verify the exact original membership/leader; return original match seats as needed; read the latest party revision; cancel each exact original party; leave each original member using current revisions; reread every profile and require no party membership. Keep operation idempotency keys for ambiguous writes and reconcile their state before repeating. Do not run a generic cleanup or use credentials from an unrelated proof. Otherwise the next cleanup action is a targeted operator-supported state inspection for this isolated pool and its original parties, with cleanup still unresolved until an authorized supported path can establish the result.

Service source provides a 120-second reconnect window and closes a started match when all seats have returned or their deadlines expire. It later deletes closed/aborted match state after the match is 24 hours old. This is source behavior, not observed cleanup; Party/Identity membership is removed by explicit leave, and the inspected party alarm handles content expiry rather than member removal. Match expiry therefore cannot be claimed as successful party cleanup.

For root's next **fresh verification**, preserve both failed receipts, confirm the pinned read-only key route at job start, then select `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.PublicTimeRunAllocatesFourAuthenticatedCooperativePlayers` alone through the existing gate and unchanged limits. This is the smallest unaccepted capacity case and begins at the failing guest operation. If it passes with zero cleanup failures, select `PublicFreestyleFillsEightAuthenticatedCooperativePlayers` alone and require its complete cleanup as well. Do not rerun the entire category, extend deadlines, waive cleanup or silently retry mutations based on this diagnosis. A fresh isolated-pool success would establish that new run's acceptance only; it would not resolve the old Freestyle parties.

Potential later diagnostic improvement, not implemented: whitelist `HttpRequestException` (and a bounded inner exception/status category) in the existing safe service-failure logger so a recurrence can distinguish transport failure without printing raw messages or responses. This follow-up changes only this report. `git diff --check` is the source-format verification; no production or test code changed.

## Operator-path follow-up opened 2026-10-26

Root integrated the preceding diagnosis and requested read-only inspection of the exact abandoned Freestyle pool through a supported operator API if available. Scope remains this report plus credential-free receipts. Discovered the installed Cloudflare connector's `search`, `execute`, `docs` and `whoami` capabilities; read its Cloudflare skill. Next: enumerate documented read endpoints and trace exact source expiry/janitor behavior for match, checkpoint, party and guest identity state. Do not infer deployed cleanup from source timers alone.

## Supported operator observation

The connector's current OpenAPI documents `POST /accounts/{account_id}/workers/durable_objects/namespaces/{id}/query/v2`, accepting SQL against an exact Durable Object name. The existing connection to the configured `Apps` account authorized the queries. Namespace metadata confirms the coordinator's SQLite-backed `MatchPool`, `Party` and `Identity` classes. Latest deployment metadata reports version `05384420-7e14-40c2-b965-0e8a91681df7` at 100%, deployed 2026-10-23T13:20:33.680965Z. No provider setting, deployment or credential was changed.

Used only bounded **SELECT** projections against `asia:wordbound-v3:pub-da6f14f6b78f:Freestyle:Solo` and the eight party/identity names selected from that pool's actual roster. Every query reported `rows_written=0`. Queries returned counts, status, timestamps and size measurements; no tokens, secrets, invites, refresh hashes, connection secrets or raw checkpoint data were selected. This is the supported operator SQL surface, not a forged guest or an added administration route. Official [Data Studio documentation](https://developers.cloudflare.com/durable-objects/observability/data-studio/) describes inspecting deployed SQLite objects by name or ID; its normal audit/usage records do not constitute application-state cleanup.

Credential-free receipt: `BuildNotes/experience-refresh/online-cleanup-operator-20261026.json`. It includes the exact original match/party/player IDs as correlation selectors for a later narrowly scoped inspection, so those objects remain identifiable after pool expiry. IDs are not authentication credentials. The receipt also preserves every query's zero-write metadata.

At **02:17:57.655Z** the exact pool contained:

- One `CLOSED` match, created **01:53:49.817Z**; eight seats, **zero returned seats**.
- Zero queued tickets, eight assignment entries, zero tombstones.
- Latest connection lease expired **01:55:49.706Z**; latest reconnect deadline was **01:57:34.706Z**. Closure and expired connection eligibility are observed; the precise time/trigger that performed closure was not traced.

By **02:19:14.154Z**, all eight individually inspected original parties still had exactly one member, the matching original leader/member, the original pool as their active attempt, two stored operation receipts and no pending party operation. All eight corresponding identities still had `ACTIVE` membership to the exact original party and zero unused Photon tickets. Serialized party state totaled **24,048 characters**; identity state totaled **3,648 characters**. These are string-length measures, not allocated database sizes or a billing estimate.

At **02:21:12.767Z**, the pool also retained one checkpoint state, two committed uploads and six chunks containing **26,314 encoded characters**; pending uploads and workshop bundles/chunks were zero. The checkpoint writer expired at **01:55:49.695Z**. No checkpoint contents were read.

## Exact source expiry boundary

Inspected `OnlineServices/src` has no diff against tested source `c23590b7674dc244f7670ef7d115d7f78bfb8b44`. Deployment metadata was checked, but deployed source bytes were not downloaded or asserted identical to this checkout. The following timings describe inspected source; only the state above is live observation.

| State | Source behavior | Residual consequence |
| --- | --- | --- |
| Connection and checkpoint writer leases | Connection leases and writer leases last 15 seconds; renewals require valid current state. Reconnect eligibility lasts 120 seconds. | The inspected leases/deadlines are expired; a closed match is not eligible for new admission. |
| Started match | `MatchPool.run` schedules the latest outstanding reconnect deadline. `expire` closes the match once every seat returned or expired. | Live query confirms `CLOSED`, without pretending that return calls succeeded. |
| Closed/aborted match, assignment and checkpoint data | An alarm is scheduled for match creation plus 24 hours; expiry deletes assignments, match checkpoint tables, match bundle scope and the match entry once strictly older than 24 hours. | This match becomes deletion-eligible just after **2026-10-27T01:53:49.817Z**. Future execution/deletion has not been observed. |
| Queue tickets and tombstones | Tickets expire after 24 hours. Removed assignments become tombstones; tombstones older than two days are pruned on a later `run`. | Tombstone expiry itself is omitted from the alarm-deadline list. Once an isolated pool has only tombstones, no future alarm is scheduled for them; full automatic pool-row deletion is not implemented. |
| Guest authentication | Access tokens expire after 15 minutes; refresh eligibility is stored for 30 days. `refresh` checks/extends that timestamp. | Authentication expiry does **not** delete identity state or membership. Observed refresh timestamps fall on 2026-11-25, but no refresh credential was read or recovered. |
| Guest identity and membership | `Identity` has no alarm/expiry deletion routine. Membership is removed on successful explicit leave or a failed pending enter rollback. | All eight observed `ACTIVE` memberships persist until a valid lifecycle action or a separately designed administration/migration operation changes them. |
| Party membership and active attempt | Party alarms clean workshop content only; `Party.get` returns pool status without deleting the stored active attempt. Cancel/leave changes active state; leave removes the member. No party-row deletion exists. | Match expiry alone will not remove the eight party rows, members, active-attempt references or stored receipts. Even successful normal leave preserves an empty party row/receipt history. |
| Checkpoint upload residue | Uncommitted uploads have a 60-second expiry and cleanup; current/previous committed uploads remain until later replacement or match deletion. | The two observed committed uploads should leave with match deletion, not on their writer lease expiry. |

Relevant source anchors: `pool.ts` `run` (33–51), `expire` (201–227), `returnToLobby` (388–402); `model.ts` reconnect constant (100); `checkpoints.ts` lease/upload constants (32–33), `deleteMatch`/`cleanupExpired` (159–170); `identity.ts` `create`/`refresh` (15–33) and `leave` (100–139); `party.ts` content alarm (18–21, 51), `changed`/leave (117–152), `get` (204–208); `store.ts` retains its state row. A source search found no identity alarm, state-row deletion, or automatic membership janitor in these classes.

## Actual impact and remaining action

The abandoned run no longer has an active match or queued ticket, and its live connection/writer eligibility is expired. It retains eight stale assignments plus checkpoint storage until the scheduled match cleanup, and **confirmed persistent** guest/party membership records beyond that cleanup. Because the fixture used its own pool suffix and disposable original guests, these records do not occupy slots in a fresh fixture pool. The original identities would still encounter existing-membership guards when attempting a new party; the state remains a cleanup failure and a storage-retention issue.

The receipt makes a later exact read-only check possible immediately after the 24-hour deletion boundary; it should require zero matches/assignments/checkpoint rows and separately acknowledge any tombstones, parties and identities still present. No automation or future job was created by this lane. Match data expiry is likely under the inspected alarm code but is not accepted cleanup until observed.

Normal authenticated return/cancel/leave remains the supported lifecycle repair, but the native proof discarded its guest credentials. Existing operator access allows state inspection; it is not authority to fabricate guest authentication, extract authentication material, or issue direct SQL repairs across dependent records. No such action was attempted. If root elects to repair retained memberships, first prepare a separate bounded administrative/janitor design with exact object selectors and lifecycle invariants; current authorization here is read-only. Keep fresh TimeRun/Freestyle acceptance separate from this original residual-state record.

No historical network cause is inferred from the later closed state. Receipt validation confirms **21 SELECT statements, 114 reported rows read, zero rows written**, one original match/eight original parties/eight original identities, and no credential-valued fields. `git diff --check` passes. No code, compiler, Unity, service mutation or cleanup execution is included in this follow-up.

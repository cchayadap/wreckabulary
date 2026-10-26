# Public maximum-roster identity visibility repair

STATUS: SOURCE READY — bounded source correction and self-review complete; runtime acceptance remains pending. All compiler, Unity, core, network and live-client execution is reserved for root.

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

# Public release evidence assessment

STATUS: PASS for the saved deployment/readback and HTTP-canary evidence; native Photon execution remains pending. Independent read-only review on 2026-10-23 of integration checkout d4268cc and the exact files below. No service rerun, deployment, network request, compiler, Unity job, private-secret read or root edit was performed.

## Deployment evidence

Root evidence directory: `wordbound-integration/BuildNotes/online/public-activation-20261023/`.

| Artifact | Independently checked result |
| --- | --- |
| `worker-results.json` | Actual native-catalogue Worker cases: 41 passed, zero failed/pending; includes all 36 approvals and mismatched mode/format rejection. This is saved local Worker evidence, not 41 live-production requests. |
| `pre-deployment.json`, `post-deployment.json` | Deployment changed the module and approved public bindings. Three secret names, all three Durable Object namespace identities, issuer, signing-key ID, Photon app ID, compatibility date/flags, subdomain configuration and website identity/timestamp are unchanged. |
| `activation.json`, `readback-verification.json` | Exact native catalogue and strict decoded approval rows agree: 36 rows, nine mode/format variants, four seeds. Independently re-evaluated parity in memory against the current installed catalogue; no verification artifact was overwritten. |
| `public-live.json` | Live HTTP canary succeeded, cleanup succeeded, zero cleanup failures and no unresolved party IDs. Duration 29.124 seconds, 13:27:44.316–13:28:13.440 UTC. |

Recorded deployed module: `3329fc54ebe2373c1f50a776b6731f243127fdcb24a502376102af8ef0be55f5`, 918,845 bytes. Deployment `3b87e4ba-1933-44f4-85c7-52d972d80a0f` assigns 100% to version `05384420-7e14-40c2-b965-0e8a91681df7`.

Native catalogue SHA-256: `4633207d08f8acce18ca3463bb86a71b1f3eaba41a2adfd475b46e9e5826ea23`. Canonical binding SHA-256: `4aed331375f99caa0701209e4f783938d4baefe0de999202e2452ace1748af90`. Public release: `wordbound-20261023-v1`. Readback contains the primary shard descriptor and four shards, each below 4,096 UTF-8 bytes.

Limits: this assessment verifies saved authenticated readback, not a fresh account query. Secret-name preservation does not disclose or independently compare secret values. The remote settings endpoint omits observability details; the staged update disables logs/traces and redacts query strings, but readback does not positively prove omitted settings. The 13:23 readback artifact correctly says HTTP and Photon were then NOT RUN; the later HTTP artifact supersedes only the HTTP status.

## Privacy, scope and cleanup

Recursive credential-marker checks and inspection of the six JSON artifacts found no credential values: secret bindings contain names/types only; no private JWK material, provider key, bearer/refresh token, signed admission, connection secret or invite credential is saved. Catalogue hashes, public signing trust, application ID, pool/match IDs and public content rows are expected metadata. No private setup directory was inspected.

`OnlineServices/scripts/verify-public-entry.mjs` uses the deployed origin with pinned signing trust, four real guest identities, two complete TeamRace/Duo parties and an isolated `pub-*` pool. It verifies the exact approved content/settings, preserved party membership, all four admissions and the signed start certificate. It does not use a private-room code. Cleanup has its own bounded deadline, returns admitted seats, cancels reservations, removes party memberships and verifies `/me` no longer has those memberships. Guest identity records remain subject to normal service retention; this is not deletion of every account or database row. Evidence reports `productionPopulationTested: false` and must retain that distinction.

## Nine native cases and bounded execution

Reviewed root sources: `WordboundNativePublicEntryTests.cs`, `WordboundNativeGameplayTests.cs`, `WordboundLiveMatch.cs`, and the test asmdef under `Assets/_Project/Tests/OnlineGameplay/`. All nine public methods are non-Explicit UnityTests with a 600,000 ms NUnit timeout; the shared operation has an eight-minute lifetime plus teardown. **The assembly name is `Wreckabulary.Online.NativeGameplay.Tests`**, despite the asmdef filename lacking “Native”.

| Group | Exact method suffix | Clients per case | Initial party shape |
| --- | --- | ---: | --- |
| Two clients | PublicTeamRacePreservesWholeSoloParties | 2 | Two single-member parties |
| Two clients | PublicBrawlRacePreservesWholeSoloParties | 2 | Two single-member parties |
| Two clients | PublicTimeRunRequiresTwoRealHumans | 2 | One two-member party |
| Two clients | PublicFreeForAllAllocatesTwoIndividuals | 2 | Two single-member parties |
| Two clients | PublicFreestyleFillsTwoRealHumansWithCoopRules | 2 | Two single-member parties; Fill enabled |
| Duo | PublicTeamRacePreservesWholeDuoParties | 4 | Two complete Duo parties |
| Duo | PublicBrawlRacePreservesWholeDuoParties | 4 | Two complete Duo parties |
| Squad | PublicTeamRacePreservesWholeSquadParties | 8 | Two complete Squad parties |
| Squad | PublicBrawlRacePreservesWholeSquadParties | 8 | Two complete Squad parties |

Total: nine cases, 34 authenticated guest/Runner starts, at most eight simultaneously in a case. These are separate authenticated peers inside one Editor process, not separate PCs. Only Freestyle enables Fill. The tested seed is `0123456789abcdef`; the other three seeds have saved catalogue/Worker coverage, not native gameplay coverage from these nine methods.

Run sequentially in the root checkout, with the heavy slot held. Cold routing inherits these environment variables and preserves combined name/category/assembly filters. Installed Unity Test Framework `CommandLineOption` splits `-testFilter` on semicolons; the gate passes these exact filters to Unity. Wrapper timeouts below are ceilings including startup, not estimated durations.

```powershell
$env:WORDBOUND_EXTERNAL_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY = '1'
$env:WORDBOUND_PUBLIC_CATALOGUE = (Resolve-Path 'Assets/_Project/Resources/Wordbound/public-catalogue.json').Path
$publicPrefix = 'Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.'
$publicTwo = @('PublicTeamRacePreservesWholeSoloParties', 'PublicBrawlRacePreservesWholeSoloParties', 'PublicTimeRunRequiresTwoRealHumans', 'PublicFreeForAllAllocatesTwoIndividuals', 'PublicFreestyleFillsTwoRealHumansWithCoopRules')
$publicDuo = @('PublicTeamRacePreservesWholeDuoParties', 'PublicBrawlRacePreservesWholeDuoParties')
$publicSquad = @('PublicTeamRacePreservesWholeSquadParties', 'PublicBrawlRacePreservesWholeSquadParties')
$publicTwoFilter = ($publicTwo | ForEach-Object { $publicPrefix + $_ }) -join ';'
$publicDuoFilter = ($publicDuo | ForEach-Object { $publicPrefix + $_ }) -join ';'
$publicSquadFilter = ($publicSquad | ForEach-Object { $publicPrefix + $_ }) -join ';'

node Tools/kit/gate/game_gate.mjs test editmode --cold --assemblies Wreckabulary.Online.NativeGameplay.Tests --category ExternalPhotonPublicGameplay --filter $publicTwoFilter --timeout 3600 --json
# Inspect the gate result and XML: exactly 5 passed, zero failed/skipped before continuing.
node Tools/kit/gate/game_gate.mjs test editmode --cold --assemblies Wreckabulary.Online.NativeGameplay.Tests --category ExternalPhotonPublicGameplay --filter $publicDuoFilter --timeout 1800 --json
# Require exactly 2 passed, zero failed/skipped before continuing.
node Tools/kit/gate/game_gate.mjs test editmode --cold --assemblies Wreckabulary.Online.NativeGameplay.Tests --category ExternalPhotonPublicGameplay --filter $publicSquadFilter --timeout 1800 --json
# Require exactly 2 passed, zero failed/skipped.
```

Restore the caller's previous environment values afterward. Both enable flags are required: either missing flag produces ignored tests. A skip is not acceptance. These EditMode fixtures enter Play Mode themselves. Do not select unfiltered core or the whole native class: those include unrelated/private/long-running cases. A separate cold compile, only if the current integrated C# checkpoint lacks compilation evidence, is `node Tools/kit/gate/game_gate.mjs compile --cold --timeout 3600 --json`; cold native execution also compiles changed scripts. Do not rerun already-valid R0/core solely for this report.

## What a passing native batch would establish

The public path locks the installed approved catalogue, uses QuickPlay with no private-code fallback, checks exact signed roster/content/settings, then starts real authenticated Fusion Runners. Shared assertions cover isolated physics scenes, replicated canonical bodies, host-only authority, remote movement, one accepted command despite duplicate delivery, and a replicated acknowledged checkpoint. Race content checks include the selected production physical plan; Freestyle checks its cooperative rules. Teardown requires runner shutdown, independent live party cleanup, no owned-object leaks and restoration of Editor build-scene settings. `WORDBOUND_PUBLIC_MODE_OBSERVED` explicitly precedes cleanup; the XML/gate result must also pass.

These nine cases do not solve complete race courses, assert every mode's win/loss outcome, test a populated release queue, migrate a host, inject packet blackout/latency/loss, or validate presented HUD/controller navigation in the Windows executable. Those remain separate acceptance work. No new source blocker was identified in this bounded review; native execution can proceed within these limits.


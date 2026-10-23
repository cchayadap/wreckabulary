# Wordbound final release checklist

STATUS: IN PROGRESS — source review and release procedure; no deployment or native acceptance implied.

Review date: 2026-10-23. Reviewed integration sources through `1f1903d`, including root-owned authoring/delivery/menu changes. Root owns integration, Unity, deployment and Windows delivery. Online owns the metadata approval repair and this checklist. One heavy job at a time. Existing seven-worktree recovery snapshot and uncommitted work remain preserved.

## Current acceptance record

- [x] Recovered public C# gate: 28/28. Actual authored-bundle C# gate: 41/41.
- [x] Pre-metadata-repair Worker suite: 98/98; Node sharding checks: 6/6; both TypeScript checks passed on 2026-10-23. These results do not validate later edits.
- [x] Root reported assembled R0 PASS in 140.6 seconds before the following repair.
- [x] Source review found missing native timeouts: `1aba048` adds `Timeout(600000)` to six baseline network fixtures and one physical interaction fixture. This prevents the default 180-second runner cutoff from contradicting the existing eight-minute operation lifetime. Production timeouts are unchanged. Native execution remains pending.
- [~] Public approval now binds exact catalogue mode and format in source, in addition to all six Content fields. JavaScript syntax checks and diff checks passed; new Worker rejection and projection tests await the heavy slot. The previous decoder dropped mode/format metadata, allowing a nonstandard client to queue an approved binding under different settings. Honest clients reject mismatched content, but server admission also needs to reject it.
- [x] Independent bounded controller source review PASS through input lane `7a7aa50`; confirmed queued-pad blocker ordering finding repaired. Nine new native cases and actual rendered controls remain NOT-RUN. See [CONTROLLER-SOURCE-REVIEW.md](CONTROLLER-SOURCE-REVIEW.md).
- [ ] Produce and validate the actual native four-seed catalogue, then stage the exact service artifact.
- [ ] Deploy to the existing coordinator, verify independent readback, and run live API acceptance.
- [ ] Run five native public cases, native authored transfer, physical/HUD/host migration acceptance, authoring persistence and editor use checks.
- [ ] Produce the release Windows player; record player smoke, visual/motion and keyboard/controller evidence. Mark owner visual review separately.

Older `PUBLIC-MATCHMAKING-ACTIVATION.md` and `DEPLOYMENT.md` describe earlier scope/settings. Their historical eight-entry/Freestyle exclusion or pending-provider statements are not current release instructions.

## 1. Freeze the exact candidate and export in Unity

From `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration`, record HEAD, dirty paths, integrated lane commits, installed SDK versions and gate results. Integrate the metadata repair, timeout patch, input repair and completed asset work first. Re-run only the R0/core checks invalidated by those changes; retain unaffected green evidence. Do not run Unity beside Blender or a Worker compiler job.

Set an explicit fresh `WORDBOUND_PUBLIC_RELEASE` (1–24 letters, digits, underscore or hyphen) and `WORDBOUND_PUBLIC_SEEDS` (exactly four distinct lowercase 16-digit hexadecimal seeds). Include `0123456789abcdef`, because the five native public fixtures select that seed. Record all four values before exporting; never reuse a release identifier for changed bytes. Existing source does not choose the other three seeds automatically.

```powershell
$wordboundRoot = 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration'
Set-Location $wordboundRoot
if (-not $env:WORDBOUND_PUBLIC_RELEASE -or -not $env:WORDBOUND_PUBLIC_SEEDS) { throw 'Set the explicit release and four seeds first.' }
$wordboundSeeds = $env:WORDBOUND_PUBLIC_SEEDS.Split(',').Trim()
if ($wordboundSeeds.Count -ne 4 -or @($wordboundSeeds | Select-Object -Unique).Count -ne 4 -or $wordboundSeeds -notcontains '0123456789abcdef') { throw 'Four distinct seeds including the native public fixture seed are required.' }
node Tools/kit/gate/game_gate.mjs exec Wreckabulary.EditorTools.WordboundDeliverySetup.PrepareFromEnvironment --cold --json --timeout 3600
if ($LASTEXITCODE -ne 0) { throw 'Native delivery preparation did not pass.' }
$env:WORDBOUND_PUBLIC_CATALOGUE = Join-Path $wordboundRoot 'Assets/_Project/Resources/Wordbound/public-catalogue.json'
```

Run commands sequentially and stop at each nonzero result; do not launch later gates merely because an earlier command printed a result. Gate exit 75 means a held editor/lock: report and stop, with no retry loop or process-name kill. Exit 3 is infrastructure, not a code pass/fail.

R0 command, when invalidated: `node Tools/kit/gate/game_gate.mjs check all --json`. Bounded core command, when affected: `node Tools/kit/gate/game_gate.mjs core 'FullyQualifiedName~Wordbound&FullyQualifiedName!~FlagshipSeeds&FullyQualifiedName!~ArenaSeeds' --json`. Do not replace that filter with an unfiltered suite: the latter includes the roughly 8,000-case flagship seed sweep and previously ran for about an hour. Root retains the affected deployment-test repair evidence (14/14). Prior 1200-second compile / 1800-second build timeouts are retained failures, so delivery preparation gets a 3600-second allowance. Root chooses final native/build durations from the observed imports and available memory.

`--cold` matters: a resident warm editor does not inherit newly set arbitrary `WORDBOUND_*` variables from the invoking shell. Cold export/test receives these variables. Do not silently fall back to warm execution.

Delivery preparation imports detailed equipment, creates missing authoring defaults while retaining existing assets, exports from the actual installed item/rule JSON, imports the exact UTF-8 catalogue and assigns it to `Resources/Wordbound/LaunchAssets.asset` with the same release. Expected result: 36 rows, nine variants for each seed: TimeRun/Solo; TeamRace and BrawlRace Solo/Duo/Squad; FreeForAll/Solo; Freestyle/Solo. Confirm the actual TextAsset assignment and SHA-256; generated JSON alone is insufficient.

## 2. Exact native catalogue and service gates

Run from the integration `OnlineServices` directory. Use a new release evidence folder outside private credential storage. Define `$wordboundEvidence` to that existing folder; the activation and live result files must be new. Keep immutable native-export bytes as evidence.

```powershell
Set-Location (Join-Path $wordboundRoot 'OnlineServices')
node --test checks/public/bindings.nodecase.mjs
node --test checks/public/catalogue.nodecase.mjs
npm run check
node node_modules/typescript/bin/tsc --noEmit --project tsconfig.public.json
node node_modules/vitest/vitest.mjs run --config public-entry.vitest.config.ts
node scripts/project-public-catalogue.mjs --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --output "$wordboundEvidence/activation.json" --write-config wrangler.production.jsonc
node scripts/project-public-catalogue.mjs --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --output "$wordboundEvidence/activation.json" --verify-config wrangler.production.jsonc
```

The actual native catalogue test file has four Node tests. For 36 exported rows, the actual Worker file should execute 41 cases after the metadata repair: 36 positive bindings, provenance, altered/private content rejection, two mode/format mismatch rejections, and whole-party SQLite/signed-start/cleanup. Require actual reported counts and zero skipped tests; do not infer a pass from the expected count. Synthetic Node sharding fixtures prove encoding, not native content provenance. Re-run the baseline Worker suite after service changes; the metadata repair adds two cases to the previous 98.

The repaired deployment rows contain exactly `{content,format,mode}`. Nested Content remains byte-identical to the native export. `builtinContentJson` retains the original content-only list for comparison; `publicBindingsJson` and `bindingsSha256` cover the entire ordered metadata list. Activation schema is 3; sharded manifest schema is 2. Nonempty old content-only arrays/manifests fail closed. Explicit `[]` remains the closed state and must have no shard variables.

Every value is at most 4096 UTF-8 bytes. Only `BUILTIN_CONTENT_JSON` and `_0` through `_7` are accepted. Missing/excess/stale/mixed shards, changed metadata, duplicate content identities and digest mismatches fail closed. Config replacement removes stale shards and preserves unrelated vars/config. Inspect the config diff before staging; no account, routes, Durable Object migration, secret or trust changes belong in this activation.

## 3. Stage and deploy the same artifact

Target: account `d105a82bc26b6913575355352c2d1bb1`, worker `wreckabulary-wordbound-coordinator`, origin `https://wreckabulary-wordbound-coordinator.pagna.workers.dev`. Use the existing James Cloudflare connection (`jamesung.kh@gmail.com`), as authorized by the user. No website routes or other worker are in scope.

```powershell
npm run production:preview
node scripts/prepare-connector-upload.mjs --mode update --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --private-dir "$wordboundPrivate"
```

Set `$wordboundPrivate` to the existing protected production secret directory, not a newly generated record. The original online lane has `.harness/online-secrets`; root must use the verified existing record. Update mode requires that record and preserves remote `secret_text` bindings using `keep_bindings`. It must not emit new secret values, rotate keys, add migrations, enable logs or alter routes. Never print `production.json`, decompressed private payloads or authentication query parameters.

Before deployment, record the current service version/deployment, read-only settings, three secret names, DO namespace IDs, workers.dev/preview state and source artifact hash. Compare `public-handoff.json` only by its public fields: exact account/worker, module SHA, 36 rows, native source SHA, metadata list SHA, issuer/key ID/app ID and pinned public-key fingerprint `43ea939c715a181912177bff3e98c647d5690ec2b0b40085a8bb6c047ce41dce`.

Root uses the prepared multipart upload and the connected account API. Discover the current endpoint schema first; do not paste private payload data into conversation. The prepared `upload.json.gz` includes update metadata and exact staged `index.js`. Upload once to `/accounts/{account_id}/workers/scripts/{script_name}`. If the outcome is uncertain, inspect remote state before retrying. No deployment is performed by this checklist author.

Read back through the authenticated Cloudflare API after success: script download, `/settings`, `/deployments`, `/secrets` (names only), `/subdomain`, and the selected `/versions/{version_id}` when needed. Decode the returned plain-text primary/shards with `readPublicBindings` and compare canonical rows to the activation artifact. Compare downloaded module bytes to `moduleSha256`; compare namespace IDs, retained secret names, compatibility date/flags, disabled logging/tracing/query redaction, existing workers.dev and disabled preview. Confirm the deployed version is the current deployment. Record only public metadata/hashes. A successful upload response alone is not parity evidence.

## 4. Live HTTP canary and native online cases

```powershell
node scripts/verify-public-entry.mjs --live --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --evidence "$wordboundEvidence/public-live.json"
```

This canary creates four real service guest identities in two complete Duo parties, verifies the exact signed admission/start and whole-party roster, then returns/cancels/leaves and verifies empty membership. It uses the first TeamRace/Duo catalogue entry in an isolated `pub-*` pool. Require both `success:true` and `cleanup:true`. It tests actual HTTP public entry but not Photon, every seed/mode, production population or a puzzle. Do not substitute the historical `verify-live-coordinator.mjs`: it is a different private/provider check with a fixed output file and weaker failure cleanup.

Then from the integration root, set:

```powershell
Set-Location $wordboundRoot
$env:WORDBOUND_EXTERNAL_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY = '1'
```

Use this command form for a focused method, or batch compatible non-Explicit fixtures to avoid repeated editor startup. Root chooses the exact batch and timeout from observed durations; inspect each case's XML/log result, not only the batch exit:

```powershell
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.PublicTeamRacePreservesWholeDuoParties' --cold --json --timeout 660
```

| Required public method | Human peers | Specific check |
| --- | ---: | --- |
| PublicTeamRacePreservesWholeDuoParties | 4 | Two intact Duo parties |
| PublicBrawlRacePreservesWholeDuoParties | 4 | Two intact Duo parties |
| PublicTimeRunRequiresTwoRealHumans | 2 | Existing party, Fill Off |
| PublicFreeForAllAllocatesTwoIndividuals | 2 | Independent guests, actual coalescing delay |
| PublicFreestyleFillsTwoRealHumansWithCoopRules | 2 | Independent guests, Fill On, cooperative rules |

These are EditMode fixtures that enter Play Mode themselves. They validate exact player-assigned catalogue bytes and release, actual Photon authentication, Fusion canonical bodies, remote movement, command acknowledgement/deduplication and checkpoint replication. All five use seed `0123456789abcdef`; they complement the local 36-binding service test. A skipped/ignored native case is NOT-RUN, not PASS.

For example, `--category ExternalPhotonPublicGameplay --cold --timeout 3600` selects the five public fixtures in one native session; the per-test 600000ms timeout still applies. Do not combine an Explicit detail test into an unfiltered batch and assume it ran.

Use the same filter prefix for `TwoAuthenticatedPeersDownloadAndPlayExactAuthoredWorkshopMap`. It must create a changed authored document, upload through the real PartyCoordinator, join/download on the second coordinator, Ready both parties, launch and compose each peer from its independent source. Require equal complete source, SourceHash, Course.Hash, bundle identity, catalogue/rules, world manifest and frame identities. This is private authored sharing and does not replace any public test. Cleanup failure fails acceptance. If creation has an uncertain response before a party ID is recorded, inspect each test guest's actual membership before claiming cleanup.

Also run the existing physical/HUD and scale/recovery methods appropriate to final candidate evidence: `AuthenticatedPeersJumpCarryCraftEquipAndStrikeProps`, `HostAndRemoteHudChooseVisibleLetterAcrossActualWallOcclusion`, `HostAndRemoteHudRefreshDefenseAndReleaseForKeySpellMenuFocusAndSelection`, `EightAuthenticatedPeersComposeFlagshipTeamRaceSquad`, and `FourAuthenticatedPeersRestoreFlagshipBrawlDuoAfterHostDeparture`. The generic command-transport fixture does not prove completed physical puzzles; retain the dedicated flagship puzzle/motion gates in root's plan. Same-process peers do not prove separate-device connectivity or arbitrary network-loss recovery.

## 5. Unity editing and Windows delivery

```powershell
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.Tests.WordboundAuthoringTests' --cold --json --timeout 660
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.Tests.WordboundAuthoringPersistenceTests' --cold --json --timeout 660
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.EditorTests.WordboundEquipmentDetailTests' --cold --json --timeout 660
```

The detail test class is Explicit and must be selected deliberately; require both detailed map/mesh cases to execute rather than be skipped. The two authoring classes currently contain seven tests each: independent saved-source cloning, invalid edit retention, changed-source recovery, lock/corruption handling, reload identity, scene restoration and camera/presentation safety. They do not by themselves prove the complete valid playtest user flow.

Manually/native-UI exercise saved course selection, drag/move module handles with attached ranges/endpoints, bounded random seeds/ranges, Apply/Save/reload, invalid pending edit recovery, preview, Playtest, Return and restoration of the original saved plus untitled additive scene. Change camera/presentation profiles and confirm the native view honors them without code. Inspect material/mesh/LOD/grip and collision behavior separately. Exercise keyboard/mouse and controller PartyMenu/navigation, text entry, menu close/reopen, device removal, focus loss and return; inspect actual rendered layout and motion rather than only synthetic input callbacks.

After final native acceptance, build the QA player, inspect the exact build ID from its gate result, and use it for the workshop scenario:

```powershell
node Tools/kit/gate/game_gate.mjs build --profile release-qa --cold --json --timeout 3600
node Tools/kit/gate/game_gate.mjs smoke --build $wordboundQaBuildId --scenario wordbound_workshop_flow --seconds 240 --json
```

Then build the stripped release from the same integrated assets/catalogue and source, capture its own exact build ID, and smoke that artifact:

```powershell
node Tools/kit/gate/game_gate.mjs build --profile release --cold --json --timeout 3600
node Tools/kit/gate/game_gate.mjs smoke --stripped --build $wordboundReleaseBuildId --json
```

Set each build-ID variable from the corresponding successful gate output before invoking its smoke. Do not use `latest` or omit `--stripped` for the final release: the generic smoke expects a QA build and can fail with `wrong_build_kind`. Verify the actual gate-reported Windows path and build SHA, launch the delivered `Wreckabulary.exe`, capture logs/screenshots/motion and the multiplayer/user-flow result. Keep dependency/license notices and editable Unity sources with the delivery instructions. Publish the executable location and a short launch/control guide; distinguish implemented, agent-verified and owner visual/feel review. Do not mark production ready while any required row above is pending or failed.

# Wordbound implementation status

STATUS: IN PROGRESS

Owner: James (SethyPagna). Started 2026-10-20.

## Goal

Deliver the complete approved [plan](PLAN.md). Source implementation, runtime verification, generated assets, service deployment and human usability acceptance are separate evidence.

## Current baseline

- Integration branch: `sethypagna/wordbound-integration`.
- Starting revision: SethyPagna/main `f7c3c90301817a033c98de6e32635d6d56de39de`.
- Incoming revision: cchayadap/ICY `fb94c9a0e159c792d4d0264329bfa66f56e9fc77`, including james-v1 history.
- Reconciled baseline committed as `12842c75608694f36819edd3bd4dc104ece7f79c`, with the recorded regressions verified.
- The original main checkout, modified font and unfinished Lunar sources are preserved.
- Portable harness run: `run-7971495a-26f3-4750-8c9c-dad132175de8`.

## Milestones

| Milestone | Status | Evidence |
| --- | --- | --- |
| M0 Safe baseline | VERIFIED | Commit 12842c7; editor and runtime regressions pass across the recorded suite and focused repair runs; final check and 225 core tests pass. |
| M1 Online proof | IN PROGRESS | Historical private 2/4/8-peer reliable transport passes. The separate coordinator's live API and actual Photon custom authentication pass. Native gameplay composition and complete checkpoint recovery are being verified. |
| M2 Complete race loop | IN PROGRESS | Mode/progression, inventory snapshots and command journal are implemented. Complete authoritative runtime remains in progress. |
| M3 Large world and generator | IN PROGRESS | Course resolver and 11,000 seed checks cover eleven proof configurations. Full flagship content production depends on the network proof. |
| M4 Workshop and assets | IN PROGRESS | Twelve original equipment assets imported and verified in Unity. Workshop now includes a rendered 3D editor; native visual/input and human usability acceptance remain pending. |
| M5 Mode suite and public play | IN PROGRESS | Separate coordinator deployed, live eight-seat private API flow and native Photon authentication verified. Approved public content and native gameplay remain pending. |
| M6 Release candidate | NOT RUN | No Wordbound player build or human acceptance. |

## Confirmed baseline issues

- The existing StaminaTests used an NUnit constraint unsupported by the repository's lightweight .NET runner. Replaced it with an equivalent predicate assertion, preserving its failure condition.
- The first Web test run lacked installed dependencies in the new worktree. Restored the lockfile dependencies with `npm ci --ignore-scripts`; all 122 Web tests pass.
- Mixed stair/door Escape paths dropped earlier doors during predecessor reconstruction. Fixed and covered by 12 route regressions.
- Rebuilding materials would undo ICY's boots/glasses/satchel tint corrections. The material specification application now retains neutral albedo for those exact materials, with four editor regression cases.
- Restored Wall_Door to its catalogued path through Unity's AssetDatabase, retaining its GUID.
- Localized rebound input prompts now use their effective binding paths. Leaving Escape no longer replaces an explicitly selected map.
- The Hub editor test used scene APIs after entering Play Mode and expected a domain reload while the project disables it. Its restoration state now survives reloads and its entry expectation follows the actual editor settings; focused verification is running.
- Unused Sentis inference and its App UI dependency changed compiler defines during editor tests, triggering an unexpected assembly reload. Removed the unused direct inference dependency; settings reconciliation uses Unity APIs. No gameplay references or inference-model assets depend on it.

## Verification so far

- `dotnet run --project Tools/RulesHarness/RulesHarness.csproj`: PASS, 249 tests, 34.07 s after the stair-path regressions.
- `Web/npm test`: PASS, 122 tests after dependency restoration.
- `game_gate core`: PASS, 225 tests, 39.2 s after the new route tests (`Logs/gate_core_20261020T093352Z.log`).
- Initial cold Unity import/compile: PASS, 1041 s (`Logs/gate_compile_20261020T092501Z.log`).
- `game_gate check all`: PASS, 5.6 s after reference compatibility and fixture changes (`Logs/gate_check_all_20261020T095419Z.log`).
- Wall_Door reconciliation: PASS (`Logs/gate_exec_RestoreDoorAsset_20261020T094233Z.log`).
- Editor suite: 352/353 passed; the Hub lifecycle fixture is being repaired and checked separately. Evidence: `TestResults/editmode_20261020T094432Z.xml`. Imported models, wardrobe, home saves and pure rules passed.
- Focused Hub follow-up: FAIL on active-scene assertion (`TestResults/editmode_20261020T095426Z.xml`); corrected the test runner's domain-reload expectation. Rerun pending.
- Second Hub follow-up: runner aborted without results after an unexpected compiler reload (`Logs/gate_test_editmode_20261020T100032Z.log`). Package/settings reconciliation and a new run are in progress.
- Focused Play Mode baseline: 113/115 passed in 309.1 s (`TestResults/playmode_20261020T100300Z.xml`). Both failures were a new localized-input fixture inheriting an unregistered generic layout. The fixture now uses the Input System's documented default device type. Escape flat/walkup start/retry, lobby, appearance, authored houses and existing controls passed.
- Settings reconciliation: PASS in 86.6 s (`Logs/gate_exec_ClearUnusedInferenceSettings_20261020T100937Z.log`). UPM removed the unused inference/App UI dependencies.
- Final Hub follow-up: PASS, 1/1, 186.5 s (`TestResults/editmode_20261020T101104Z.xml`).
- Final localized-input follow-up: PASS, 2/2, 43.9 s (`TestResults/playmode_20261020T101411Z.xml`). All earlier failing baseline cases are now green; the complete suites were not redundantly rerun.
- Owner signed in and supplied the Wreckabulary Photon app ID on 2026-10-20. Chrome's tab inventory shows the matching management URL, but repeated browser inspection fails with an unattached debugger and tab creation times out. App type, CCU allowance and SDK compatibility remain UNVERIFIED. No Fusion package exists in Downloads as of this check. Owner asked to save the official Fusion 2.1.3 and Physics Addon 2.1.2 packages there; no passwords or private keys requested.
- All twelve individual image concepts and editable Blender meshes are staged outside the maintained checkout. Independent FBX/GLB reimports pass 24/24, with 1,676–4,296 triangles and two materials each. Bounds drift is at most 1.76e-7 m. Native mesh previews are rendering; held/placed/thrown gameplay checks have not run.

## Next recoverable action

Five isolated feature worktrees now exist: map-core, online, pvp-assets, puzzle-race and map-workshop, each on its matching `sethypagna/*` branch from baseline 12842c7. Map contracts, party coordinator and workshop have separate owners. Root owns gameplay rules and Unity integration.

The additive twelve-asset source/importer commit was integrated as 346f230. Native import and seven editor tests pass, including model/icon opt-in, unchanged default paths, all existing skin choices, exact geometry/grips and the original ninety-eight-model library. Evidence is in ASSETS.md. Held/placed/thrown checks remain pending. The workshop lane now works separately from the completed source asset lane.

The puzzle-race mode/progression and economy snapshot units were integrated as b1a59de and 703e002. The first course contracts/resolver checkpoint was integrated as 25e3dc0. Native Unity verification passes 37/37 tests in 68.4 s: `TestResults/editmode_20261020T104503Z.xml`, SHA-256 `61d26461c4c95112466461725d8b32289e2c31a991c324ba11f30a3603e64181`. This tests rules and course contracts inside Unity; it does not prove a playable Wordbound scene or network connection.

The integrated map stress, command journal, 3D workshop, coordinator client, complete snapshot codec, authoritative controller and recovery fixes pass 483 core tests (`TestResults/core_20261020T121859Z.trx`, 86.9 s). Static course geometry is integrated as `bf8a31b`; native collision checks are pending. The latest source check passes (`Logs/gate_check_all_20261020T122700Z.log`, 9.3 s).

Photon dashboard inspection now works and confirms Fusion 2 and 20 CCU. Both SDKs are installed and native live transport checks pass for 2, 4 and 8 peers, with clean teardown and 64 KiB byte-exact payloads both ways. `TestResults/editmode_20261020T120201Z.xml` passes 3/3; sanitized evidence and setup history are recorded in [PHOTON-SETUP.md](PHOTON-SETUP.md). All peers ran on one PC; physical gameplay, cross-device networking and migration remain unverified.

Root owns native SDK/physics integration. The online session checkpoint (`39047cc`) and actual authority bridge tests (`b06deb5`) are integrated. Unity generated their metadata. Independent review found stale asynchronous session completions and missing client verification of the host's signed roster membership; repairs and regression verification are underway. The gameplay lane is adding the bounded network body/physics adapter.

Native Fusion configuration and carrier prefab setup pass (`Logs/gate_exec_Configure_20261020T122120Z.log`, 99.6 s; `Logs/gate_exec_CreateSnapshotPrefab_20261020T122321Z.log`, 88.4 s). Explicit cloud checkpoints are enabled through application calls, automatic snapshots remain disabled, and client-to-client reliable forwarding is disabled. These setup results do not prove host recovery; the native recovery probe is pending.

The owner selected the existing website's Cloudflare account, `d105a82bc26b6913575355352c2d1bb1`. The first connector returned HTTP 403. The owner then connected James's account; a direct account read returned HTTP 200 for account **Apps** on 2026-10-20. The separate coordinator is deployed on that account, with no website route changes.

The first combined native check (`TestResults/editmode_20261020T122803Z.xml`) passed 12/21. It exposed a stage-offset workshop projection error, an over-wide stair opening, six Mono RSA fixture failures and an unweaved migration carrier. Their source fixes are integrated; native re-verification remains pending. Signed host proof and stale asynchronous session fixes have separate source-level adversarial checks and independent review.

The explicit clean rebuild completed but its test exceeded the default 180-second timeout (`TestResults/editmode_20261020T123501Z.xml`). This is a failed test, not weaving acceptance. The fixture now has a bounded fifteen-minute timeout plus a separate read-only generated-state check so normal verification does not repeat the clean rebuild.

The host physics adapter is integrated as `c9844ca`: canonical body identity, admitted input authority, runner-local queries, movement and snapshot restoration. Its installed SDK reference compile and 22 policy tests pass in the source lane. Integration R0 passes (`Logs/gate_check_all_20261020T124934Z.log`, 23.7 s). Native bodies, gameplay composition, migration and eight-peer physics remain pending.

Native foundation follow-up passes 47/48 (`TestResults/editmode_20261020T125740Z.xml`, 202.4 s): workshop 7/7, course geometry 11/11, Fusion authority 6/6, physics policies 22/22 and the generated carrier attribute 1/1. Migration is the remaining failure: the fixture checked recovered bytes before the asynchronous resume callback; a bounded wait and rerun are in progress. See [native evidence](evidence/native-foundation-2026-10-20.json). This does not claim physics gameplay or visual acceptance.

The separate coordinator is deployed at `https://wreckabulary-wordbound-coordinator.pagna.workers.dev`. Seven live HTTP stages pass, including whole parties of four, exact eight-seat signed admission, ticket replay rejection and terminal Return revocation. Test sessions were cleaned up. The existing website's modification timestamp is unchanged. Public trust configuration is `OnlineServices/production-client.json`; deployment and live evidence are in `BuildNotes/online/DEPLOYMENT-AUDIT.md`. Photon dashboard configuration and actual custom-auth Runner checks remain pending.

The next native migration run (`TestResults/editmode_20261020T130511Z.xml`) failed both cases after reaching the replacement host. It exposed an invalid raw NetworkObject ID copy in the probe and materialization of temporary resume objects in the large-packet reader. The source now copies every chunk's values and bytes during enumeration and reconstructs fresh carriers from recovered application state. The production caller remains lazy. Independent review is recorded in `BuildNotes/native-proof-review/REPORT.md`; native re-verification is pending.

Gameplay composition is integrated as `7323c16`, with fresh Runner readiness and delayed reconnect activation in `7726372`. It binds the canonical authority, isolated course geometry, exact body identity, admitted input, and snapshot restoration. Its source lane passed 16 focused policy tests and an installed SDK reference compile. Integration R0 passes in 34.1 seconds (`Logs/gate_check_all_20261020T132527Z.log`). Native configuration now includes the Gameplay assembly before its first import (`Logs/gate_exec_Configure_20261020T131241Z.log`, PASS, 717.3 seconds). Three physical network prefab creation helpers are prepared; actual bodies, movement, carried visuals, crafting, moving platforms and authenticated recovery still need native evidence.

Native gameplay policies now pass 16/16, and the complete single-carrier recovery passes. The combined run is FAIL, 17/18 (`TestResults/editmode_20261020T132615Z.xml`). The large-packet diagnostic returns only carrier 0 of 33. Read-only inspection of the installed SDK confirms that one push queues only a bounded incremental transfer and does not establish a complete cloud-acknowledged checkpoint. Coordinator-backed atomic checkpoints are being implemented and reviewed before gameplay recovery can be accepted. Exact evidence is in [native recovery investigation](evidence/native-recovery-2026-10-20.md).

Unity has now created, baked and registered the three network gameplay body prefabs (PASS, 229.1 seconds). The next native presentation/puzzle run passed 26/29: all nine puzzle policy checks passed, while three model horizontal-center checks need correction. See [native body and presentation evidence](evidence/native-gameplay-bodies-2026-10-20.md). Live physical gameplay remains pending.

Schema-2 deployment expiry is integrated as `b68c3e6`. R0 passes (`Logs/gate_check_all_20261020T134917Z.log`), followed by 138/138 affected core tests (`TestResults/core_20261020T135146Z.trx`, 34.2 seconds). The coordinator and client protocol update is in progress; previous schema-1 network evidence is historical.

The Photon provider secret was replaced after it was pasted into chat. Live verification confirms the old value is rejected and the replacement accepts a valid ticket; sanitized evidence is `BuildNotes/online/PROVIDER-ROTATION.json`. The replacement remains only in private local setup and service secret storage. The owner is completing the dashboard query parameters and rejection settings before native authentication tests.

The owner completed those settings. Actual Photon authentication now passes 3/3 with clean teardown (`TestResults/editmode_20261020T135810Z.xml`, 128.6 seconds): valid server-issued identities connect; anonymous and invalid-ticket clients are rejected. See [native authentication evidence](evidence/native-photon-auth-2026-10-20.md). Authentication configuration is complete; authenticated gameplay composition and checkpoint recovery remain in progress.

The model center repair is now verified: native importer PASS in 58 seconds and presentation tests PASS 23/23 in 106.7 seconds (`TestResults/editmode_20261020T140526Z.xml`). The separate Client assembly's camera/input component compiles, but it still needs launcher composition and live input verification. Three workshop viewport captures also pass and have been inspected; [their evidence](evidence/workshop-2026-10-20/README.md) explicitly excludes full editor controls and flagship content.

Combat and equipment selection are integrated as `5951865`. The new HUD and selected-item presentation compile natively (`Logs/gate_compile_20261020T141314Z.log`, 138 seconds), after R0 and 174/174 affected core tests pass. The launcher, live controls, combat physics and controller menu interaction remain pending. Equip/Attack transport requires the online lane's pending wire update; see `BuildNotes/client-view/PROGRESS.md`.

The reviewed checkpoint session and Equip/Attack wire update are integrated. The separate coordinator's durable checkpoint service is deployed with its existing identities and secrets preserved; deployment evidence is `BuildNotes/online/CHECKPOINT-DEPLOYMENT.md`. Root's match client now composes the session, camera, HUD, equipment presentation and exact-rules-bound puzzle effects. Unity compiled that composition in `TestResults/editmode_20261020T144759Z.xml`; 42/43 selected tests passed, with the authenticated two-peer case failing initial scene setup. Root is correcting that contract while separate lanes implement the party entry menu, workshop refinements and BALL collision handling. Full gameplay and host recovery remain unverified.

The initial scene and startup barrier repairs now pass with actual authenticated gameplay: **6/6** selected native tests in `TestResults/editmode_20261020T152312Z.xml`, including two peers, isolated canonical bodies, client movement, exactly-once command application and acknowledged durable state. Five accompanying thrown-body/contact cases pass. The latest aggregate source check and 259 affected core tests also pass. Full interactions, four/eight-peer gameplay and host recovery remain pending. [Startup evidence](evidence/initial-gameplay-scene-2026-10-20.md) records the prior failures and exact repair.

The authored flagship, local workshop playtest and in-world puzzle guide now pass their combined native batch: **93/93**, no skipped cases, clean exit, 535.9 seconds (`TestResults/editmode_20261020T165452Z.xml`). The actual launcher starts an edited full flagship with its exact manifest, player view, HUD and guide, then returns to the same undo/redo history after shutdown. Separate native Time Run and Freestyle cases pass movement, jump, landing and complete memory checkpoints with an offline Runner. R0 and all nine repaired lifecycle core cases pass. [Composition evidence](../../BuildNotes/client-view/LOCAL-COMPOSITION.md) records the failed first attempt, repair, exact scope and limits.

The frozen flagship seed sweep remains in progress; 6,000 of 8,000 cases have passed, with final evidence pending. Route/start authoring, full body-budget policy, human-visible UI captures, four/eight-peer gameplay, host recovery and Windows builds remain open. The configured Windows build-profile assets have not yet been created. Main `f7c3c90` and its original dirty font/Lunar files remain unchanged.

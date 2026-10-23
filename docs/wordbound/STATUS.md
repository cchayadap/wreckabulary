# Wordbound implementation status

STATUS: IN PROGRESS

Owner: James (SethyPagna). Started 2026-10-20.

## Goal

Deliver the complete approved [plan](PLAN.md). Source implementation, runtime verification, generated assets, service deployment and human usability acceptance are separate evidence.

## Current delivery snapshot — 2026-10-23 17:34 UTC

Implementation and integration are active. The first Windows QA build passes, but its actual workshop smoke fails on a stripped shader; no fully tested Windows delivery exists yet. Saved render material assets now pass native creation/verification. The empty party capture is independently confirmed as all-zero RGBA; render readiness is being instrumented before a rebuilt player is tested. Original Wreckabulary artwork remains the default (48/48 native presentation checks). Bounded public TimeRun and private exact authored-map transfer pass with two authenticated clients; public Team Solo and sustained full-match reliability remain open. Shared floor sensing now allows at most one millimetre below the floor, with horizontal and upper bounds preserved; 208 focused Rules cases pass and native FAN verification is running. Atomic snapshot reconciliation has independent source review but is not yet integrated or executed. See [progress.md](../../progress.md) for current acceptance. All seven worktrees, the recovery snapshot and original main/font/Lunar work are preserved. No further Photon or Cloudflare setup is required.

| Work package | What is verified | Work remaining |
| --- | --- | --- |
| Isolated development | Integration and feature worktrees exist; the original main/font/Lunar work is preserved. | Review and delivery branch; formal milestone closure. |
| Flagship world and randomness | Four destinations, layered three-lap content and six puzzle families are implemented. The frozen earlier content passed 8,000/8,000 seed cases. | Complete physical puzzle playthrough, final-content checks and the 18–25 minute human session target. |
| Five modes | Mode rules and local flagship playtest are implemented. The FFA recipe-access repair passes 19/19; the separate arena sweep passes 1,000 seeds. Practice/defense source is integrated; affected Rules pass 128/128. | Native practice, defense and full mode acceptance. |
| Workshop | 3D editing, course authoring, validation and exact-draft playtest/return are implemented. Native launcher/playtest/return preserves undo/redo history. Inspector course/camera/visual assets and exact online handoff source are integrated. | Final player edit/reload/profile interaction, sustained custom-map retention, full input and creator-usability checks; bounded exact two-client authored transfer passes. |
| Equipment and story | Twelve image concepts and editable Blender assets produced; 24 FBX/GLB reimports and 23 native presentation cases pass. Story signs, discovery banners and a collected-clue journal are integrated and source-reviewed. | Native story/readability checks and final held/placed/thrown/combat acceptance. |
| Online services | Photon authentication passes 3/3. Earlier authenticated two-peer gameplay, durable checkpoints and 2/4/8-peer transport pass. Current schema-3 service and public catalogue are deployed with exact readback and preserved storage/secrets. | Native schema-3 recovery, four/eight-player full gameplay, host recovery and sustained custom-map retention. |
| Public matchmaking | Exact 36-entry native catalogue deployed; current Node 10/10, Worker 41/41 and TypeScript checks pass. Live HTTP canary admits two intact Duo parties and cleans up successfully. | Nine native Photon mode/format fixtures, complete gameplay and player acceptance. HTTP admission alone does not prove these. See `BuildNotes/online/public-activation-20261023/`. |
| Windows delivery | Windows profiles verified; first QA build `20261023T170613Z` passes in 491.3 seconds. Its actual workshop smoke fails; exact evidence is preserved. | Rebuild with explicit render material references and observed rendering, complete player workshop/game/return checks, performance and final stripped release smoke. |

Current assignments: root owns integration, exact Workshop online handoff, native checks and player delivery; puzzle-race owns independent authoring/HUD reviews and controller input repair; map-workshop owns detailed equipment revision and independent bundle review; online owns public allocation, compatible custom-map transport and service/client gates. James/SethyPagna remains the human owner of the whole expansion.

Latest bounded Rules run: 409/410 PASS, followed by repair of the sole obsolete schema-2 assertion and 14/14 PASS in the affected deployment group. Earlier arena 1,000-seed and flagship 8,000-seed results remain recorded separately. Final assembled R0 passed in 140.6 seconds (`Logs/gate_check_all_20261023T094106Z.log`). Native batch `201950Z` previously hit its 1,200-second deadline during compilation before tests started. Caches are retained for the 3,600-second retry. The earlier physical fixture also has an unresolved runtime timeout; neither gap is closed by source checks.

Native camera, six detailed model/pivot cases, eight authoring-persistence cases and eleven workshop-recovery cases pass. Input passes 24/24 (`editmode_20261023T133902Z.xml`); assigned/template/flagship source binding passes 25/25 (`editmode_20261023T143652Z.xml`). Presentation passes 25 native behavior cases and three calibrated standalone .NET allocation cases; the unsupported Unity allocation counter failures remain recorded. Native profiling identified repeated static validation analysis as the dominant measured cost. Its reviewed immutable metadata cache passes 141 focused core tests, and the identical .NET flagship workload allocates 78.2% less for TimeRun and 74.8% less for Team Solo. These are allocation measurements, not Unity FPS or successful multiplayer evidence. Subsequent native/private results and latest allocation evidence are recorded in progress.md; sustained recovery remains pending.

The owner rejected DetailV2's appearance and chose the original Wreckabulary game assets as the reference. Both revisions are preserved; original artwork is the runtime default. SOFA experiments produced no convincing improvement, so its original remains. FAN's rear construction is promising in the inspected matched pair; HAMMER passes build guards but has no rendered review. Both source pilots are committed and isolated from runtime assets. Further views and final Unity/owner visual acceptance remain unfinished. The current checklist and evidence are in [progress.md](../../progress.md).

Evidence: [FFA checks](../../BuildNotes/ffa-content/PROGRESS.md), [flagship seeds](../../BuildNotes/flagship-content/SEED-SWEEP.md), [local composition](../../BuildNotes/client-view/LOCAL-COMPOSITION.md), [source acceptance review](../../BuildNotes/native-proof-review/REPORT.md), [story progress](../../BuildNotes/story-presentation/PROGRESS.md), [Windows delivery](../../BuildNotes/client-view/WINDOWS-DELIVERY.md). The chronological entries below retain earlier failures and results; this snapshot supersedes their old “next” statements.

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
| M0 Safe baseline | REGRESSIONS VERIFIED; GATE OPEN | Baseline fixes and focused follow-ups pass. Current closure dry-run fails: rows 01/02 have old source hashes, 03/06/07 lack recorded verdicts, 04 lacks its facts artifact, and 05 lacks owner confirmation. No milestone closure is claimed. |
| M1 Online proof | IN PROGRESS | Photon authentication and actual two-peer gameplay pass. Full four/eight-player gameplay and host recovery remain pending. |
| M2 Complete race loop | IN PROGRESS | Authoritative progression, inventory, recovery, physical puzzle and guide source are integrated; local world/player composition passes. Full physical puzzle completion and human playthrough remain open. |
| M3 Large world and generator | IN PROGRESS | Complete flagship source exists; frozen earlier content passed 8,000/8,000 seeds. New practice/defense and final-content checks remain pending. |
| M4 Workshop and assets | IN PROGRESS | Twelve assets and native presentation checks pass; exact workshop playtest/return preserves history. Custom online sharing and human usability acceptance remain open. |
| M5 Mode suite and public play | IN PROGRESS | FFA content repair and public catalogue validation pass. Practice/defense, final catalogue activation and live public play remain open. |
| M6 Release candidate | IN PROGRESS; NO VERIFIED PLAYER | First Windows QA build timed out. Cached rebuild, player smoke, release validation and human acceptance remain pending. |

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

## Historical implementation log

These entries preserve earlier results and superseded next steps. Current work follows the snapshot above and progress.md: complete the active native cache/readiness run, repair demonstrated failures, verify public/private/recovery behavior, then build and smoke the actual Windows player.

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

The frozen sweep subsequently completed **8,000/8,000 seeds**, covering eight configurations; [exact sweep evidence](../../BuildNotes/flagship-content/SEED-SWEEP.md) pins its pre-budget source and results. Stage 2 now reserves recovery and actor capacity within the unchanged 512-body limit, retires eligible loose objects deterministically, and rejects invalid restored reservations. Its focused pressure checks and all flagship modes/route alternatives pass; [capacity evidence](../../BuildNotes/entity-lifecycle/CAPACITY-RESULTS.md) distinguishes those checks from a complete physical playthrough. The route, checkpoint, lap and spawn editors are integrated with 15 focused tests passing. Both workshop cache repairs have separate assembly/reference checks; their native cases and the combined root integration check are pending.

Windows release/QA/development profiles were created and verified through Unity: **PASS**, 269.9 seconds (`Logs/gate_exec_Create_20261020T173007Z.log`). Their Windows target, scene order, defines and development flags were read back successfully. The workshop capture scenario compiled natively but has not run in a built player.

The assembled source check passed and **372/372 core tests** passed. The subsequent native batch was **FAIL: 56 passed, 16 failed, 0 skipped** (`TestResults/editmode_20261020T173612Z.xml`). All failures share a new budget assumption that every allowed mode recipe exists in the supplied catalogue. Existing partial catalogues are valid; a focused repair preserves that contract while retaining required puzzle validation. The 56 passing cases include local budget preflight, workshop history reset, exact flagship launcher/return, movement, jumping and checkpoints. Full physical puzzle and multiplayer fixtures are being prepared separately. Windows build and smoke remain **NOT-RUN**.

The catalogue repair is now verified: **16/16 GameplayPlan cases pass natively** in `TestResults/editmode_20261020T175216Z.xml`. The same run's new physical puzzle fixture failed its initial keepsake interaction; setup-pose observation and diagnostics are being corrected before retrying. R0 and the 29 affected core cases pass. The first Windows QA build (`20261020T175830Z`, source `4a0bbfa`) is **FAIL** because it exceeded 1800 seconds; its shader and Bee caches are retained for the retry. Actual player smoke, full physical puzzle completion and larger multiplayer recovery remain unverified. [Windows evidence](../../BuildNotes/client-view/WINDOWS-DELIVERY.md) records the exact failure and generated settings.

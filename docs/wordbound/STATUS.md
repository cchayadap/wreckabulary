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
| M1 Online proof | IN PROGRESS | Fusion 2 app, 20 CCU and local SDK installation verified. Live private 2/4/8-peer reliable transport checks pass 3/3. Authenticated gameplay, physics, host migration and deployment remain pending. |
| M2 Complete race loop | IN PROGRESS | Mode/progression, inventory snapshots and command journal are implemented. Complete authoritative runtime remains in progress. |
| M3 Large world and generator | IN PROGRESS | Course resolver and 11,000 seed checks cover eleven proof configurations. Full flagship content production depends on the network proof. |
| M4 Workshop and assets | IN PROGRESS | Twelve original equipment assets imported and verified in Unity. Workshop now includes a rendered 3D editor; native visual/input and human usability acceptance remain pending. |
| M5 Mode suite and public play | NOT RUN | No online service deployed. |
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

The integrated map stress, command journal, 3D workshop, coordinator client and complete snapshot codec pass 421 core tests (`TestResults/core_20261020T112920Z.trx`, 180.2 s). Snapshot integration is `5ee59b6`; the latest pre-transport source check also passes (`Logs/gate_check_all_20261020T120133Z.log`, 26.5 s). The authoritative controller and reviewed recovery/return follow-up are committed separately as `cd0fdea` and `4dfce93` with 133 focused Wordbound tests passing; they await integration.

Photon dashboard inspection now works and confirms Fusion 2 and 20 CCU. Both SDKs are installed and native live transport checks pass for 2, 4 and 8 peers, with clean teardown and 64 KiB byte-exact payloads both ways. `TestResults/editmode_20261020T120201Z.xml` passes 3/3; sanitized evidence and setup history are recorded in [PHOTON-SETUP.md](PHOTON-SETUP.md). All peers ran on one PC; physical gameplay, cross-device networking and migration remain unverified.

Root owns native SDK/physics integration. The online lane has an SDK-linked authenticated session checkpoint (`39047cc`, after `3f5a655`) and a metadata cleanup (`ce091c2`) ready for integration, with 47 service tests and 20 focused client tests passing. Unity will generate the new online metadata. The gameplay lane is adding static runtime geometry and the reviewer is checking snapshot/mechanism and admission boundaries. The Cloudflare deployment target needs clarification because the connected account differs from the repository's existing website account; no deployment or account switch has been made.

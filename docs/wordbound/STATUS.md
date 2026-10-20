# Wordbound implementation status

STATUS: IN PROGRESS

Owner: James (SethyPagna). Started 2026-10-20.

## Goal

Deliver the complete approved [plan](PLAN.md). Source implementation, runtime verification, generated assets, service deployment and human usability acceptance are separate evidence.

## Current baseline

- Integration branch: `sethypagna/wordbound-integration`.
- Starting revision: SethyPagna/main `f7c3c90301817a033c98de6e32635d6d56de39de`.
- Incoming revision: cchayadap/ICY `fb94c9a0e159c792d4d0264329bfa66f56e9fc77`, including james-v1 history.
- Merge applied without textual conflicts; the merge commit awaits baseline validation.
- The original main checkout, modified font and unfinished Lunar sources are preserved.
- Portable harness run: `run-7971495a-26f3-4750-8c9c-dad132175de8`.

## Milestones

| Milestone | Status | Evidence |
| --- | --- | --- |
| M0 Safe baseline | VERIFIED | Editor and runtime regressions pass across the recorded suite and focused repair runs; final source checks and commit next. |
| M1 Online proof | NOT RUN | Photon SDK/account access and Unity compatibility not yet established. |
| M2 Complete race loop | NOT RUN | No Wordbound runtime delivered yet. |
| M3 Large world and generator | NOT RUN | Depends on the network proof for full content production. |
| M4 Workshop and assets | IN PROGRESS | Twelve image concepts and editable meshes produced in separate staging; export checks pass. Unity integration and workshop remain outstanding. |
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
- Photon SDK access and Unity compatibility: NOT RUN. No account credentials or SDK are available in this project; owner asked why Photon was needed and received an explanation. Independent implementation continues.
- All twelve individual image concepts and editable Blender meshes are staged outside the maintained checkout. Independent FBX/GLB reimports pass 24/24, with 1,676–4,296 triangles and two materials each. Bounds drift is at most 1.76e-7 m. Native mesh previews are rendering; held/placed/thrown gameplay checks have not run.

## Next recoverable action

Finish final source checks with shared deterministic-RNG references, commit the reconciled baseline, then establish the five feature worktrees and exclusive path ownership. Continue simulation, map contracts, service and asset implementation independently of the unavailable network SDK.

# Wreckabulary: DEVLOG

Append only. One entry per milestone, per recovered failure, per wave result, and at least hourly during a run.

Entry format: a level-2 heading with the UTC time and a title (for example `2026-10-01T12:00:00Z Wave m1 integrated green` after the two hashes), then three short parts: **what** changed, **evidence** (file paths you opened), and **next**.

`gate_close` refuses a milestone whose newest entry is older than its newest evidence, so write the entry before you close. The file rotates to `Docs/devlog/<n>.md` at about 1 MB; the newest entries stay here.

## 2026-10-20T09:40:00Z Reconcile histories and verify pure rules

**What:** Created the isolated integration branch from f7c3c90 and merged ICY fb94c9a without textual conflicts. Fixed stair-path reconstruction, neutral wardrobe regeneration, localized rebound prompts, and explicit lobby map precedence. The original checkout remains unchanged. New toolkit reference compatibility uses a project-level Directory.Build.targets for Unity 6000.6's net472 NUnit and Unity.Scripting assembly; four capture tests now use an unambiguous UnityEditor.Editor type.

**Evidence:** docs/wordbound/STATUS.md; Logs/gate_core_20261020T093352Z.log (225/225 NUnit rules); lightweight harness output (249/249); Web test output (122/122). Initial Unity import still running in Logs/gate_compile_20261020T092501Z.log. R0 is being repeated after the last namespace correction. BAT concept is saved in the separate wordbound-assets-staging collection with its generation prompt and hash; it is not an integrated model.

**Next:** Complete import, restore Wall_Door through IntegrationReconciliation, run editor and runtime regressions, then commit and split feature worktrees.

## 2026-10-20T09:51:56Z Complete cold compile and inspect editor regression

**What:** Initial Unity cold compile passed (1041 s). R0 now passes using real Unity 6000.6 references. GUID-checked Wall_Door restoration passed (106.3 s).

**Evidence:** Logs/gate_compile_20261020T092501Z.log; Logs/gate_check_all_20261020T093933Z.log; Logs/gate_exec_RestoreDoorAsset_20261020T094233Z.log. Editor suite ran353 tests:352 passed,1 failed in HubPlayButtonTests SetUp/TearDown calling editor scene APIs after entering Play Mode. Full result: TestResults/editmode_20261020T094432Z.xml. All imported-art and wardrobe regeneration cases passed.

**Next:** Repair Hub test reload-safe cleanup, update expected lobby poster list to include Escape, add Escape runtime start/retry checks, then run focused editor and runtime regressions before baseline commit.

## 2026-10-20T10:10:00Z Verify existing gameplay and remove unstable unused dependency

**What:** Focused Play Mode regression completed115 tests:113 passed, with only two new localized-device fixture failures. The fixture incorrectly inherited an unregistered `InputDevice` layout; omitting that base uses Input System's default device type. Existing gameplay, Escape in flat/walkup, retries, map selection, controls and wardrobe passed. The Hub follow-up exposed an unexpected compilation during its Play Mode transition. Package source inspection identified Sentis and App UI startup callbacks that mutate compiler symbols; no gameplay uses inference. Removed the unused direct inference package and added a Unity API command to remove its generated settings reference and defines. Existing source/YAML serialization conventions remain intact; toolkit additions do not override broad Git attributes.

**Evidence:** TestResults/playmode_20261020T100300Z.xml; Logs/gate_test_editmode_20261020T100032Z.log lines537–552; installed AnalyticsDefineManager.cs and AppUIManager.cs; Logs/gate_check_all_20261020T100928Z.log (PASS8.6 s). Agent verified24/24 asset export reimports and all12 generated concept hashes; actual mesh previews were inspected independently for eight items.

**Next:** Finish ClearUnusedInferenceSettings, repeat Hub and localized-input tests, record the baseline and create the isolated feature branches. No Wordbound multiplayer or full map acceptance is claimed.


## 2026-10-20T10:16:00Z Baseline regressions pass

**What:** The final Hub check passed with fast Play Mode preserved, and both localized binding tests passed after correcting the fixture. Unused inference/App UI settings were reconciled through Unity APIs. The resulting empty Standalone define entry was the only remaining ProjectSettings change; restored the original equivalent empty-map serialization through Git. Added the installed deterministic RNG assembly to engine-free build references for upcoming course work.

**Evidence:** docs/wordbound/BASELINE-2026-10-20.json records exact result/log hashes. Hub: 1/1 PASS, 186.5 s. Localized bindings: 2/2 PASS, 43.9 s. All cases that failed in the 353-editor/115-runtime baseline suites now pass in focused follow-ups. Independent asset delivery validation also passed all twelve models, and root inspected all twelve actual three-quarter previews.

**Next:** Finish final R0/core checks, commit this reconciled merge, and create the feature worktrees from that revision. Source and service foundations can proceed without the network SDK; actual online acceptance remains NOT RUN.

## 2026-10-20T17:04:00Z Native local flagship composition

Integrated lifecycle test boundary repair, exact physical/flight checkpoint fixture and puzzle guide. Root bound both clients and verified the actual launcher-to-edited-flagship-to-workshop path. R0 PASS37.9s (Logs/gate_check_all_20261020T165327Z.log); lifecycle core9/9 PASS33.4s (TestResults/core_20261020T165403Z.trx); native93/93 PASS535.9s, no skipped cases and clean exit (TestResults/editmode_20261020T165452Z.xml). BuildNotes/client-view/LOCAL-COMPOSITION.md records exact scope and limits. Original main font/Lunar dirty state is unchanged. Seed sweep, body-budget policy, route/start authoring, player captures, four/eight-peer gameplay, host recovery and Windows delivery remain in progress.

## 2026-10-20T17:35:00Z Wordbound capacity and workshop route integration

Integrated Stage2 a1622ef, physical-editor cache 7e5ea22 and route/spawn authoring 02cd9b6. Root hooks ab4649a bind policy hash into fingerprint v2, preflight authored and arena content, reuse the shared physics/reconciliation cap and expose route controls. R0 PASS85.2s: Logs/gate_check_all_20261020T172654Z.log. R1 PASS372/372, no skipped cases,72.3s: TestResults/core_20261020T172842Z.trx. The three Client preflight cases and native cache cases are not part of this engine-free result and await Unity execution.

The frozen generator sweep completed8000/8000 seeds at4ae1ba1; its source predates Stage2, whose separate bounded mode/route acceptance is recorded in BuildNotes/entity-lifecycle/CAPACITY-RESULTS.md. Windows profile creation is currently running through the gate, with source/QA reviewed but native execution/build pending. Original main/Lunar work is outside these writes.


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


## 2026-10-20T17:53:00Z Catalogue repair and player verification queue

The Windows profile helper passed in269.9s and created/read back all three profiles (Logs/gate_exec_Create_20261020T173007Z.log); source/assets checkpoint737728c. Native batch173612Z was FAIL56/72, with all16 GameplayPlan failures caused by the new budget assuming absent mode recipes exist. Repaird127a7b reserves only available enabled recipes, preserves mandatory puzzle validation, and versions the bound entity-budget hash. Separate production-DLL checks passed10/10 plus the exact16 GameplayPlan cases.

The assembled integration now includes the six-family native fixture019a725 and flagship8-peer/4-peer recovery fixturee1df02b, each with independent review and separate installed-SDK reference compilation. Root R0PASS32.1s (Logs/gate_check_all_20261020T175053Z.log); affected core29/29PASS43s (TestResults/core_20261020T175127Z.trx). Root also corrected the QA scenario registration to AfterAssembliesLoaded after independent review found an order-dependent player-only startup failure. Native repair/physical checks are running; online flagship runs and Windows build/smoke remain pending. No full physical playthrough or release acceptance is claimed.

Native follow-up completed: GameplayPlan 16/16 PASS; new physical fixture FAIL at its initial keepsake pickup (OutOfReach), clean exit after 202.7 seconds. Evidence: TestResults/editmode_20261020T175216Z.xml. Fixture setup did not await canonical position or line of sight; its owner is diagnosing and repairing the setup. Production compilation passed. Windows QA build is a separate visual verification step; physical interaction acceptance remains open.

## 2026-10-20T18:34:00Z Preserve first Windows build evidence

QA build20261020T175830Z on4a0bbfa returned FAIL timeout1800s,1926.7s including shutdown. Log: Logs/gate_build_release-qa_20261020T175830Z.log; manifest: Builds/qa-20261020T175830Z/build.json. Burst queue timeout preceded cancellation; Unity subsequently completed the costly shader batch and reached Bee feature extraction292/572. No Unity/shader/Bee process remained after gate exit. Preserved caches and inspected Unity-generated URP/preload settings; ignored only generated Performance Testing measurement files and retained Resources folder metadata. Detailed evidence: BuildNotes/client-view/WINDOWS-DELIVERY.md.

Integrated physical setup observation repair0363205 and independent public-catalogue/audio reviews. A further fixture-only event-latching refinement is queued before native retry. Online catalogue reference compilation and17 focused cases passed in the source lane; root integration and native export remain pending. Next: verify queued code, retry cached QA build, smoke the exact successful player, then complete larger multiplayer and physical puzzle checks.

## 2026-10-20T18:53:13Z Native catalogue result and remaining implementation

The public catalogue slice62a959e passed root R0 and all17 native cases in TestResults/editmode_20261020T184007Z.xml. The same batch was FAIL overall: physical six-family fixture exceeded NUnit's default180000ms limit before mechanism evidence,538.3s total including197s refresh. Root added an explicit600000ms framework ceiling around its unchanged six-minute cancellation and per-operation deadlines, with stage diagnostics; physical acceptance is still open.

The intervening exporter183516 failed on Bee cache rename locks. A surviving backend40444 was verified against the original build log, dead parent51276, creation18:28:03Z and exactPlayer511a1575 DAG, then stopped by that single PID. This corrects the earlier absence observation. No cache files or unrelated processes were changed. Recovery evidence is in BuildNotes/client-view/WINDOWS-DELIVERY.md.

Independent acceptance review48a48fb identified five concrete gaps: arbitrary workshop online content, FFA weapon letters, Freestyle practice, defensive blocking and narrative presentation. Owners are implementing those within the approved plan. Root's narrative view now exposes existing authored clues through nearby signs, discovery text and an optional per-team journal. R0 PASS109s (Logs/gate_check_all_20261020T185125Z.log); focused native story tests and visual inspection are pending. Practice requires an explicitly coordinated schema/protocol3 cutover; no v3 service deployment or public activation has occurred.

## 2026-10-20T19:05:00Z Reconcile delivery status and integrate verified arena content

Answered the owner's status request against the approved plan, current Git state and lane evidence. Refreshed docs/wordbound/STATUS.md with the current delivery snapshot and corrected obsolete milestone wording. M0 closure dry-run remains FAIL: old source hashes on01/02, missing verdicts03/06/07, missing facts04 and owner confirmation05. No human confirmation was requested before a reviewable delivery, and no milestone was closed.

Integrated FFA production source4c9543a, focused evidence0965aac and bounded story reviewb965fff. The CourseTemplates add/add conflict was resolved using the reviewed exact lane file; only CombatArena differs from the root baseline. FFA overlay tests pass19/19 against prior root Rules; native arena interaction and the later1000-seed arena-only check remain pending. BuildNotes/ffa-content/PROGRESS.md preserves hashes and the corrected test-only first failure. Original main remainsf7c3c90 with the same font/Lunar dirty paths.

Practice Rules compilation and16 focused cases pass in its lane; bounded guide/content checks are running before integration. Sethy owns defense shared hooks only after the practice checkpoint/handoff. Online owns the coordinated schema3 service/golden update and automatic custom-map transfer. Root will next integrate ready source, recheck affected code, then rerun physical puzzle/story native checks before final player delivery. Photon configuration is complete; no account action is currently requested from the owner.


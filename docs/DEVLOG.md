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

## 2026-10-20T20:09:00Z Inspector controls and protocol integration

Integrated schema-3 checkpoint/wire validation, editable course/camera/visual authoring sources, persistent defense poses and the bounded detailed-asset importer source. Root wired camera profiles to local/online clients and live Inspector updates, visual replacements to canonical fitting, practice selection/guide and held defense to HUD input, and the authored course copy to the launcher workshop. Independent review found replacement-grip and custom-ID mapping defects; both are fixed with native regressions pending execution.

R0 passed in 198.2 seconds (`Logs/gate_check_all_20261020T200045Z.log`). Affected Rules passed 128/128 in 163.5 seconds (`TestResults/core_20261020T200545Z.trx`), including the shield-break rollback regression. The combined filter omitted the Explicit arena seed gate, so no 1,000-seed result is claimed. The later authoring/importer check, native camera/visual/course/puzzle tests, service v3 deployment, public activation, asset production and Windows player remain pending. Public Freestyle is authorized by the latest request and is a separate online slice. Original checkout and website Worker remain preserved.

## 2026-10-20T20:25:00Z Schema-3 deployment and arena verification

The standalone Explicit arena gate passed all1,000seeds in255.7seconds total (`TestResults/core_20261020T201057Z.trx`): maximum194 initial bodies, minimum213 optional reserve, cap512, exact replay/import and bounded placement/source checks. This corrects the prior combined-filter omission; native targeting remains separate.

Final assembled R0 passed148.3seconds (`Logs/gate_check_all_20261020T201557Z.log`). The verified schema-3 service was uploaded once and read back at100% traffic; existing secrets/DO namespaces/migrationv1 were preserved and the website timestamp stayed unchanged. Live API7/7passed with complete cleanup (`BuildNotes/online/V3-DEPLOYMENT.json`, `V3-LIVE-API-CHECK.json`). Public allowlist remains empty. Native local batch201950 is active; no runtime or Windows completion is claimed. Independent review found remote HUD lookup and occluded-target defects, now assigned for source repair plus actual remote-input regression.


## 2026-10-20T20:44:00Z Native compilation timeout and exact Workshop handoff

Native batch201950Z returned FAIL, exit1,1205.3seconds: the1200-second deadline expired during compilation. The log has no C# diagnostic and no native test result. Unity/Bee/VBCS from the gate process tree were absent after termination. Cache retained; no test was credited. Online owns the next compiler slot, followed by detailed Blender generation.

Integrated reviewed remote HUD canonical body lookup/LOS, saved CourseAsset launcher regression, Freestyle expectation correction and detail-revision preflight. The bundle durability review is retained with its source-only limits. Root added Workshop Host online handoff using captured draft/manifest/mode, a busy UI, preserved history on failure, and exact BundleId association for local CourseAsset camera/visual overrides. These source changes await Online A1 APIs and new R0/native checks. Controller review found default desktop-only gameplay binding; its isolated repair is assigned to the authoring/input lane.

Evidence: Logs/gate_test_editmode_20261020T201950Z.log; BuildNotes/unity-authoring/NATIVE-ACCEPTANCE-REVIEW.md; BuildNotes/durability-review/BUNDLE-REVIEW.md; progress.md. No verified Windows delivery or production-ready assertion.

## 2026-10-23T08:53:27Z Resume and preserve unfinished Wordbound work

Confirmed seven worktrees, integration HEAD a896be3 and the unchanged original main/font/Lunar direction. No surviving Unity/Bee/Blender/compiler job was found. Saved tracked binary/index patches and 719 dirty/untracked/authored files to the ignored `.harness/recovery/2026-10-23T08-53-03-542Z-da1d8676-0794-40fc-a246-cd5de5cc201a`; reread every copied file and verified its SHA-256 against the manifest. Caches and private service credentials remain in place.

Recovered the actual public C# 28/28 result. The interrupted Worker gate has no surviving verdict and must run; no success was inferred. Online owns those source gates; input owns controller/menu cleanup and transition safety; assets owns material/construction detail with measured static export mesh reduction. Root owns integration, native validation, final public activation and tested Windows delivery. No Windows player or production acceptance is claimed. Updated progress.md and stale Task/TODO/MAP pointers to match recorded state.

## 2026-10-23T09:47:00Z Integrate recovered source and retain bounded verification

Integrated all-five-mode public admission, private custom-map transfer, exact Workshop/profile handoff, controller gameplay/menu actions, draft recovery, detailed export builder and native transfer fixtures. Preserved and committed 37 Unity-generated metadata files from the prior native attempt. Root source commits53eea44/f18f5f0/0992883/e275a94 are coherent checkpoints; originals and private recovery copies remain intact.

Final assembled R0 PASS140.6s: Logs/gate_check_all_20261023T094106Z.log. Bounded Rules run409/410 exposed only an obsolete schema2 test; corrected test explicitly rejects both retired versions and affected deployment group14/14PASS in TestResults/core_20261023T093147Z.trx. The accidental broad seed run was cancelled through positively identified test PIDs and remains a FAIL/cancelled record. Earlier frozen seed evidence was not repeated or relabeled.

Public C#28/28, authored client41/41, unique Worker98/98, two TypeScript checks and six multi-seed binding/configuration tests PASS; evidence in BuildNotes/online/PUBLIC-BINDING-SHARDS.md and related handoffs. Public configuration remains closed pending native four-seed/nine-variant export and exact deployment. Final native fixtures are NOT-RUN.

Detailed Blender generation owns the heavy slot. Its first build stopped at an order-sensitive SOFA vertex comparison; the failed output is preserved and the geometry-preserving comparison is being corrected before retry. Independent usability review identified missing party-controller navigation and workshop focus protection; the input lane is implementing bounded repairs before native/player checks. No Windows player or production acceptance is claimed.

## 2026-10-23T10:23:22Z Freeze source repairs and verified asset candidate
Integrated controller delivery patch with verified SHA256529D5021B0AFF9FDE033B9A8E829576C50E3A6B5778B6DC48E89BDA935C54CC5 as4d1f3fb; baseline f2152ba was not cherry-picked. Independent source review PASS is recorded in BuildNotes/online/CONTROLLER-SOURCE-REVIEW.md. Nine added native party/workshop cases remain NOT-RUN. New camera Inspector1f1903d preserves stored radians, exposes degree angles and metre labels, and uses Unity serialization/Undo; independent input-lane source review found no concrete defect. Native rendered editing remains pending.

Public admission source d03d77b binds exact approved mode/format/content and rejects old nonempty metadata-free allowlists. Six Node metadata/configuration cases and both TypeScript checks pass; the final Worker suite is still running. Added plain-language public-map errors17c5f38. Public remains closed until exact native export, deployment parity and live acceptance.

Detailed asset generation,24independentFBX/GLBreimports and final48renderedviews completed. Delivery integrity reports12models/24exports/36previews/12icons with complete channel/alpha/hash/provenance checks. Final measured original mesh count is174, correcting the earlier193 hand count; final27deliverymeshes,275editablecomponents,max4908triangles. This is84.5%fewer delivery meshes, not an FPS claim. Side-view review caught and repaired the original fan's floating motor with an axle/support; root independently inspected its repaired side plus BAT/SOFA/SHIELD/TABLE/BED/CLOCK views. User style and Unity appearance acceptance remain separate. Renderer-only six-thread pass took647.539s for44views; repairedFANfourviews took155.970s at2threads. Root asset integration, finalR0, native import and Windows delivery remain next.

## 2026-10-23T11:01:44Z Source candidate and native preparation

- Integrated detailed assets through 93d4e35. Root delivery validator passes all 12 models and 24 exports: 174 original meshes to 27 delivery meshes, 275 editable components, maximum 4,908 triangles. Root also inspected the final HAMMER three-quarter render. Unity renderer/pivot and frame evidence remains pending.
- R0 runtime/editor PASS in 479.8 seconds: Logs/gate_check_all_20261023T103849Z.log. Cold DeliverySetup is active in gate session 57841, log 104740Z, source baseline fc7cae3. Explicit release: wordbound-20261023-v1; seeds: 0123456789abcdef, 0000000000000001, 0000000000000002, 0000000000000003. Burst/ILPP startup delays are observed; no native pass yet.
- Online metadata source/evidence integrated as 6ec7e27 and deployment review as fc7cae3: retained 95/100 full run plus exact 5/5 rerun; Node 6/6 and earlier TypeScript checks pass. Live readback confirms previous deployment 912bdaa6-61ef-40f3-8fad-d567cde59ccd / version 387b0d2b-5836-4485-b651-b52ce5b49f0e, closed allowlist, three secret names and three DO IDs retained, workers.dev enabled and preview disabled. No deployment this turn.
- Pending after Unity exits: asset detail test 4c87c2b and reviews 6636c6d / 2d1fda0; QA source 296f60c / f78e86e / e2b5860 / d6d672c and report 9618460 (df8b7f6 already integrated as 3b58546). Compile before native tests/build; integration Assets remain frozen during import.
- Online lane owns isolated transient reconnect implementation after read-only audit found ordinary disconnect and hard-loss timing gaps. Writer TTL/election is unchanged until separate review. Input/Assets review is source-only. Existing online ModeDefinition dirty patch matches committed integration practice hash lines 66–67; historical evidence and original font/Lunar files remain preserved.

## 2026-10-23T11:07:35Z Compiler scheduling observation
The PC reported 100% CPU across 22 logical processors. This gate's verified Unity child compiler PIDs 45024 (VBCSCompiler) and 51980 (Unity.ILPP.Runner), both parented by gate-owned Unity PID 38176, were BelowNormal. Changed only those two helpers to Normal priority; other projects/processes are untouched. Subsequently observed Rules Csc completion and Bee's second pass in Logs/gate_exec_PrepareFromEnvironment_20261023T104740Z.log. This is a scheduling observation, not a benchmark or final native pass. Unity-generated metadata remains untracked until the import completes and its diff is reviewed.

## 2026-10-23T11:30:00Z Recovery checkpoint and native retry

- Confirmed integration branch b4a2cca clean before retry; preserved all seven worktrees and the verified recovery snapshot. Portable harness checkpoint revision37 remains active.
- R0 PASS386.4s in Logs/gate_check_all_20261023T111607Z.log after direct Presentation reference repair and integrated QA/detail tests.
- Cold DeliverySetup retry started from b4a2cca with explicit wordbound-20261023-v1 release/four saved seeds; log Logs/gate_exec_PrepareFromEnvironment_20261023T112358Z.log. No result claimed while active.
- Online source checkpoint e3194fe remains isolated pending independent review, tests and integration. Separate guide/packaging review owns only documentation. Public activation and Windows delivery are pending.

## 2026-10-23T11:28:41Z Timestamp correction and owned helper scheduling

The preceding recovery-checkpoint heading was typed as 11:30:00Z before that time; its actual append preceded the 11:28:14Z observation. This entry records the correction with machine UTC. The still-active retry uses Unity PID27028. Only its identity-checked Unity SDK compiler/Burst child processes below Normal priority were raised to Normal; no unrelated project process or permanent scheduler setting changed. Native result remains pending.

## 2026-10-23T11:40:56Z Native import interruption preserved

- Cold retry log Logs/gate_exec_PrepareFromEnvironment_20261023T112358Z.log confirms native compilation completed in320.615637s with no CS0012 recurrence, followed by asset import. The terminal session ended exit1 without gate JSON or a history row; final log line is Shut down. Import/export and completion remain NOT VERIFIED.
- Fresh process inspection found gate PID52684, Unity PID27028 and its owned helpers exited. Application event inspection found no matching Unity/node/dotnet crash event in the inspected20-minute window. The cause is undetermined; no failure cause is inferred. Gate status sees a stale lock owned by52684; the gate will handle its own stale lock on the next invocation.
- Preserved12 native-generated/normalized metadata files; existing ten GUIDs match HEAD. No caches deleted. Source import had not produced a new public catalogue or DetailV2 active revision.
- Viewed BROOM/SPEAR/PLATE/STOOL three-quarter previews: distinct construction, grips/bristles/rim/support shapes visible. This completes root review of all12 final model silhouettes alongside earlier views; runtime appearance remains pending.
- Integrated guide fcfe325 and starting-angle correction b9a02bd. Native screenshots and packed-asset evidence remain pending.

## 2026-10-23T11:55:47Z Integrate reviewed reconnect and native fixture repairs

Integrated source812a000/b851895/94f1bde and independent review47a38e1; four race findings closed at source, runtime NOT RUN. Timing probead659fc records detected-loss/restored/ready events without changing10s/15s settings. Fixture ae1d671 routes around the actual CLOCK with existing8s deadline; independent review1567f66. Fresh R0 running in Logs/gate_check_all_20261023T115335Z.log. Native interruption review2b95b6d found no supported crash/OOM/timeout cause. A private hidden-wrapper script is prepared to retain next gate stdout/stderr/exit status; no second Unity job launched. Public catalogue/deployment/player remain pending.

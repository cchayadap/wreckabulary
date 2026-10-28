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

## 2026-10-23T12:02:44Z Reconnect reference compilation passed

R0 Logs/gate_check_all_20261023T115335Z.log failed357.5s on31 identical Assert ambiguity errors in the new reconnect test file; runtime passed. Added its file-local NUnit Assert alias. Fresh full R0 PASS130.9s, Logs/gate_check_all_20261023T115910Z.log. This verifies assembled reference compilation, not native/runtime acceptance. The next cold native preparation uses the existing gate via a hidden project-local PowerShell wrapper, preserving durable stdout/stderr/status and the gate's own3600s timeout. No source, cache or lock bypass is involved.

## 2026-10-23T12:08:43Z Retained native preparation job ownership

Cold retry runs via hidden wrapper27484, gate49544, Unity8580, log Logs/gate_exec_PrepareFromEnvironment_20261023T120315Z.log; status/stdout/stderr live under ignored .harness/delivery/native-preparation-*. The gate replaced the dead52684 lock itself. Root verified Unity executable/command identity before raising only its compiler children11208/12224 from BelowNormal to Normal; Burst child5412 was already Normal. No other project process/settings changed. Burst named-pipe startup errors are recorded again; independent read-only investigation assigned, no suppression/package change. Gate result remains pending.

## 2026-10-23 native preparation verified

Native preparation now passes: `Logs/gate_exec_PrepareFromEnvironment_20261023T120315Z.log`, 871 seconds, completed 2026-10-23T12:17:48Z. Unity imported DetailV2, created the camera/presentation/Freestyle/flagship authoring assets, and exported/assigned 36 exact public bindings (nine mode/format combinations, four seeds each) for `wordbound-20261023-v1`. The launch asset references the actual catalogue and profiles. Existing model/icon GUIDs and grips remain intact. This verifies preparation, not appearance, gameplay, public service activation or the Windows player. Burst startup pipe errors remain visible in the successful gate result and are documented in `BuildNotes/client-view/BURST-STARTUP-REVIEW.md`; their cause is undetermined. Next: selected native editor/physics/network gates, exact catalogue service validation, and QA/release build verification.

## 2026-10-23T12:27:34Z selected native editor verification

The 18-fixture native batch completed FAIL in 399 seconds: 123 executed, 97 passed, 26 failed, zero skipped. Evidence: `TestResults/editmode_20261023T122055Z.xml` and `Logs/gate_test_editmode_20261023T122055Z.log`. All nine recovery-window cases, eight gameplay shutdown cases, seven basic equipment cases, three detailed maps/bounds/renderer-count cases, seven authoring basics, three workshop handoff cases, 27 presentation, 11 defense-pose, five story, three practice, and seven focus/GUI/cache cases pass.

Outstanding: three articulated transform basis mismatches; one EditMode camera SendMessage assertion; three saved-form/scene persistence failures; eleven input, five party input, three workshop input cases. No failure is suppressed. Camera fixture now invokes its actual managed update directly (as an EditMode fixture), with camera-value/invalid-profile assertions unchanged; native gameplay still verifies natural player updates separately. Pivot tests now print expected and actual native matrices before the unchanged assertions, to distinguish coordinate conversion from an asset defect. Input and authoring repairs are assigned to isolated lanes. Unity also migrated TimeManager to its native rational timestep serialization; scale remains 1 and the original approximately 0.02-second timestep is retained. Public activation and Windows delivery remain pending.

## 2026-10-23T12:41:54Z owner rejects DetailV2 art direction

James says the assets look worse than the original Wreckabulary game assets: old, generic and not detailed. The original game assets are the explicit visual reference, not the first Wordbound set. Gallery and progress now mark appearance rejected; all source revisions are preserved. Root inspected original supplied-item contact sheet and native fan/home/workshop captures, plus original and DetailV2 sofa/fan previews: the new set retains boxy forms and earthier surfaces instead of the original clean rounded silhouettes and differentiated materials. Art lane has paused broader template UI implementation at its committed checkpoint and is comparing the original assets before a small revised sample. Gameplay/native/build work continues separately; no art acceptance or production-ready claim.

## 2026-10-23T12:54:00Z native repair integration and original-art reference

Integrated reviewed workshop recovery, authoring persistence and native input fixture repairs, with independent review reports, through `bc3d345`. The focused prior native run `TestResults/editmode_20261023T123439Z.xml` failed 4/8: three pivot bases and TABLE craft wait; camera and three detailed import checks pass. Actual pivot matrices confirm the exporter retains local axes, so the test conversion changes from C*M*C to C*M*F with unchanged strict tolerances and unchanged asset bytes. Physics diagnostics are assigned separately; no deadline is loosened. New R0 passes in 82.5 seconds (`Logs/gate_check_all_20261023T125200Z.log`). Bounded workshop core and selected native tests are next.

The actual original game comparison is integrated at `BuildNotes/equipment-polish/ORIGINAL-GAME-VISUAL-REVIEW.md`; representative original bytes match preserved main. The gallery now presents the original showcase and removes its stale claim that DetailV2 retained the original style. A separate editable original-based sofa/fan/hammer pilot is being prepared, starting with SOFA. No replacement sample is yet produced, no visual acceptance is inferred and no live public activation or verified Windows player exists.

## 2026-10-23T13:31:00Z Public catalogue activation and original-based pilot review

Deployed the exact 36-entry native public catalogue to the isolated Wordbound coordinator. Independent readback matches module SHA-256 3329fc54ebe2373c1f50a776b6731f243127fdcb24a502376102af8ef0be55f5 and catalogue SHA-256 4633207d08f8acce18ca3463bb86a71b1f3eaba41a2adfd475b46e9e5826ea23; existing storage namespaces and secret names are retained and the website is unchanged. Evidence: BuildNotes/online/public-activation-20261023/readback-verification.json. The live HTTP canary admitted two intact scripted Duo parties, verified exact content and signed certificates, and cleaned up successfully: public-live.json. This is not Photon gameplay evidence. Actual-catalogue Worker 41/41 and Node 10/10 pass; both TypeScript checks pass.

Root and asset reviewer directly inspected V2 sofa three-quarter original/candidate renders: original sage/timber silhouette retained and V1 floating seams/exposed trim resolved in that view. V1 remains preserved and rejected. V2 side/detail views are being rendered; no complete visual or owner acceptance is claimed. Reviewed fixture-only input cleanup/frame/repaint repairs are integrated after the recorded 15/23 native result. Workshop source-binding 25-case execution, physical puzzle diagnosis, public/private/recovery gameplay and Windows player remain open.

## 2026-10-23T13:42:00Z Native input passes; two workshop composition failures remain

Assembled R0 first failed only an unsupported NonParallelizable annotation, then passed in 12.9 seconds after removing it (Logs/gate_check_all_20261023T133804Z.log). Native batch TestResults/editmode_20261023T133902Z.xml completed 69 total: 67 passed, 2 failed, zero skipped, 162.6 seconds. All24 input cases and20 existing workshop/recovery cases pass. Source binding23/25: deferred choice under gameplay-block and same-ID Flagship draft serialization fail; assigned to source diagnosis without weakening validation. Three Unity-generated metadata files are preserved. Five public two-client cases are now running through the cold gate; no result yet.

Direct V2 close-up inspection found an angular seam kink; this blocks visual completion. V2 six images/source/receipts and exact review are saved under ArtSource/Collections/WordboundPvP/OriginalBasedPilotV2. Both earlier versions remain intact. V3 source/contact feasibility is under review before another render.

## 2026-10-23T14:03:00Z Isolate public fixture startup and repair large-course recovery

Public native5 failed0/5 in381.6s (TestResults/editmode_20261023T134237Z.xml). Exact admission certificates were observed but the nested IEnumerator wrapper did not surface EnterPlayMode to the installed Editor test runner; no Entering Playmode marker occurred, and cleanup called Destroy from EditMode. The unchanged direct private2 fixture passes1/1 with real bodies, client input, acknowledged command and cleanup (TestResults/editmode_20261023T135039Z.xml,99.4s gate). Reviewed public/HUD wrapper flattening, explicit PlayMode guard and exact-owned-prefix EditMode cleanup are integrated. No Photon settings, SDK or deadlines changed. Assembled R0 passes84.9s (Logs/gate_check_all_20261023T135858Z.log); public rerun remains pending.

Large valid workshop recovery documents previously exceeded the generic string-token bound because the course was embedded as one string. The narrow root-course recovery exception retains outer byte/depth/token bounds and strict inner course validation. Core66/66 pass in21.8s (TestResults/core_20261023T135423Z.trx). Corrected native source-binding25 remains pending. Original-based V3 raised seam failed numerical contact feasibility before Blender; preserved evidence led to V4 shallow material-normal treatment. V4 source builds successfully and its first close-up pair is rendering; no appearance acceptance yet.

## 2026-10-23T14:30:00Z Original artwork verified; public failures narrowed

Actual original Wreckabulary artwork is the default via the editable presentation profile. Native presentation passes 48/48; workshop source binding passes24/25; public five fail. Combined run:72/78 passed,6failed,0skipped,407.034s case duration,510.5s gate (`TestResults/editmode_20261023T141231Z.xml`, `Logs/gate_test_editmode_20261023T141231Z.log`). Public wrappers now enter PlayMode. Three failures reach the canonical writer-lease expiry in Poll; two pass body/readiness checks but fail admitted client movement. No online success or Windows delivery claimed. A bounded workshop fixture wait-order repair is integrated, native rerun pending. Operation/tick/input/frame-gap observations are being added without changing deadlines or product authorization.

SOFA V4 matched close-ups were inspected: no convincing improvement, retain the original. All revisions preserved. Reviewed FAN/HAMMER construction source is integrated outside runtime Assets. FAN build passes (`.harness/delivery/fan-pilot-v1-build.log`); rendering/review pending. The gallery now leads with actual originals and labels rejected/experimental work. No visual owner acceptance claimed.

## 2026-10-23T15:08:26Z Native diagnostics and preserved original-art pilots

- Native diagnostic run ended FAIL: 28 executed,25 passed,3 failed,zero skipped,671.1s gate (`TestResults/editmode_20261023T143652Z.xml`, matching gate log). All25 workshop source-selection cases pass. Physical six-family case reaches its overall six-minute lifetime during CollectLetter before the second TABLE craft; first HAMMER and TABLE craft/hold and CollectLetters completion are observed. Public Team Solo writer expiry and Time Run checkpoint-ack failure coincide with long main-loop gaps; neither is a public gameplay pass.
- Indexed presentation source is integrated and independently reviewed. Native compile first caught three inaccessible test property writes; public-accessor repair preserves behavior assertions. R0 passes15.5s (`Logs/gate_check_all_20261023T145607Z.log`). Native result is25 behavioral PASS plus3 allocation FAIL (`TestResults/editmode_20261023T145714Z.xml`): Unity Mono returns zero for both byte measurements. This does not prove zero allocation or regression. A supported measured probe and opt-in native CPU scopes are in progress.
- FAN/HAMMER editable pilot output folders, receipts and source snapshots are preserved in commit5ced4a4. FAN rear construction is useful in the inspected detail pair, further views pending; HAMMER is built but unrendered. No pilot replaces the default original Wreckabulary artwork. Windows delivery, full public/private/recovery matches and final visual/human acceptance remain open.

## 2026-10-23T15:32:52Z Native CPU attribution and actual flagship baseline

- Native profile run150959Z ended FAIL0/3,zero skipped,592.6s gate. Physical finishes CollectLetters and CratePress before total fixture expiry at tick3480; TeamSolo loses writer lease after88ticks amid11.958s framegap; TimeRun readinessexpires with noframe and maxgap0.245s. Distinct failure evidence is preserved. Independent review: BuildNotes/equipment-polish/NATIVE-PROFILE-20261023T150959Z-REVIEW.md.
- Physical recorder retains all64frames: Validate127330.950ms, PhysicsApply130675.734ms and Presentation1613.030ms. Reconcile is inclusive, do not add it to children. Online frame-sum buffers wrapped; diagnostic source now records actual marker invocations with explicit limits, native rerun pending. Cache work targets immutable course/mode derivations while retaining every dynamic validation.
- Calibrated presentation .NET probe PASS3/3, same outputs and positive65560-byte controls; allocation42400/149472/300448bytes versus467680/6590176/25763552 at32/128/256bodies across4passes. Saved source/probe hashes in BuildNotes/online/presentation-probe-20261023.json. This is not Unity throughput.
- Actual flagship validation baseline PASS120/120 on unchanged validation: TimeRun94bodies and TeamSolo186bodies, fixed seed0123456789abcdef. Full input/state hashes,6timing/allocation batches and positivecontrols saved in BuildNotes/online/validation-baseline-20261023.log/.json. Root soleheavy, no Unity active afterjobcompleted; no verified Windowsplayer. Allchanges/pilots/recovery and originalwork preserved.

## 2026-10-23T15:49:00Z Measured validation improvement and native rerun

Immutable course/mode validation metadata cache is integrated and independently reviewed without caching validity or skipping dynamic checks. R0 passes 17.3 seconds; affected core passes 141/141 in 31.6 seconds (TestResults/core_20261023T153611Z.trx). Identical flagship probe and inputs pass 120/120 validations before and after with positive allocation controls and unchanged snapshots. Mean allocation per validation falls 2,599,998.7 to 565,950.4 bytes for TimeRun (78.2%) and 5,043,607.5 to 1,270,324.3 for Team Solo (74.8%). Exact comparison is BuildNotes/online/validation-comparison-20261023.json; variable timing does not prove native FPS or writer stability.

Fixture-only public readiness state and per-invocation profiling are integrated. Fresh R0 passes 19.6 seconds (Logs/gate_check_all_20261023T154457Z.log). The unchanged three-case native public/physical test batch is running under durable wrapper30324 since15:45:41Z; its result is pending. Historical progress prose is preserved in BuildNotes/client-view/RECOVERY-AND-VERIFICATION-HISTORY-20261023.md, leaving progress.md focused on the current checklist and evidence. Original artwork remains default; no Windows delivery or visual approval is claimed.

## 2026-10-23T16:04:17Z Native cache result and isolated QA storage

Native154541Z ends FAIL1/3,2fail,zero skipped,572.3s gate. TimeRun passes two scripted authenticated peers, exact public admission, bodies/movement/command/durable acknowledgement and cleanup; final hostpaused limits sustained-health claims. Physical reaches tick4560 but FANarrival exceeds12s (15.301sobserved), lifetimefalse. TeamSolo FormatException originates ReceiveSnapshot. Exact log/XML hashes and24validnonwrappedprofilerrecords saved in BuildNotes/online/native-cached-validation-20261023.json; independent review f47271a.

QA storage fix b40671e uses fresh output-child storage before real OpenWorkshop, preserving default owner drafts/recovery guards. Session safe rejection diagnostics4b16348 and failure-only FAN factsb0c808f reviewed, assembled R0PASS53.6s (Logs/gate_check_all_20261023T160005Z.log). Root started native-transfer-wind-diagnostics wrapper26208 at16:02:16Z, onlyheavyjob. Remaining staticindexes source is in independentreview, notintegrated. No Windowsplayer or productionreadyclaim.

## 2026-10-23T16:24:00Z Exact failures and indexed-validation evidence

Native160217Z ended FAIL0/2, zero skipped,443.7s gate. FAN is Armed with359progress ticks and only.014m receiver travel; field/start checks pass, arrival is false. TeamSolo matching chunks are stale at17972ms versus10000ms, without identity/hash/count/length mismatch or duplicate. Hashed evidence: BuildNotes/online/native-transfer-wind-diagnostics-20261023.json; independent review: BuildNotes/equipment-polish/FAN-NATIVE-MOTION-REVIEW.md. No deadlines, sensor bounds or force changed.

Static immutable lookup indexes pass R0(50.3s,Logs/gate_check_all_20261023T161123Z.log) and core170/170(36s,TestResults/core_20261023T161250Z.trx). Unchanged probe/exact inputs pass120/120 with65560-byte controls and unchanged snapshots. Additional per-validation allocation reduction: TimeRun565950.4→523899.2(7.43%), TeamSolo1270324.3→1190317.5(6.30%). Evidence: BuildNotes/online/validation-indexed-comparison-20261023.json. Variable standalone timings do not prove Unity frame rate. FAN contact calibration and per-runner batch diagnostics are in source preparation; Windows delivery remains open.

## 2026-10-23T16:38:53Z Native solver isolation and fixture repairs

FAN162601Z failed the original contact arrival assertion; all three native controls ran. Default-contact maximum travel.01389885m and noarrival reproducefullroute; no-wind0m; zero-friction reachesactualarrival at27ticks(.45s) butcoasts, so it is not a productionfix. Hashedresults: BuildNotes/online/native-fan-contact-calibration-20261023.json. A moderate receiver-only material candidate is being prepared before prefabassignment.

Tickprobe163048Z failed beforeRunnerstart because Unity rejected an Editor-only assembly component; no scheduling evidence. Runtime-assembly UNITY_EDITOR callbackadapter90ac700 is integrated, with explicit NUnit assertionqualification. Privateauthored163328Z failedpreparation; source confirmsfixturemustPoll torefreshassignmentafterQueue. Fixture nowdoes so, preservingproductionflow. Bounded fourmethod/type faultorigins exclude exceptionmessages and payloads. R0PASS11.9s(Logs/gate_check_all_20261023T163750Z.log); native rerunpending.

## 2026-10-23T16:49:14Z Exact private transfer passes; host batching measured

Native163900Z ended FAIL1/2,zero skipped,302.1sgate,118.4srefresh. Private exactauthored transfer passes66.573732s includingteardown, remotecontent/body/input/command/durableACK. Finalhostpaused limits ongoinghealthclaims. TeamSolo fails82.230695s withstale21052/10000ms matchingchunks. Pairedobserver recordsactualhost60tickforwardbatch6482.646ms; callbackgap is not socketarrivaltiming. StartupBurst60000msqueuetimeout retained. Hashedcase/profilerecord: BuildNotes/online/native-team-private-repaired-20261023.json.

Resource-indexd881a31 andmoderateFANcandidatee46ca51 integratedwithsourcereview. R0PASS15.8s(Logs/gate_check_all_20261023T164551Z.log). Core188/189PASS,1FAIL(TestResults/core_20261023T164636Z.trx): newlaptestindexeszeroresource minimalFreestylefixture; producerrepairpending, no productionfailureidentified. Nativecandidate/benchmarkwaitforgreenrung.

## 2026-10-23T16:58:47.687Z Resource validation regression and measured comparison

Source `02738cd` passes R0 (`Logs/gate_check_all_20261023T165005Z.log`) and all 189 focused core cases (`TestResults/core_20261023T165505Z.trx`, 48.1 seconds). The repaired nonrace lap test now exercises the actual populated Freestyle flagship. The unchanged probe passes 120 validations with identical inputs/probe hash and positive 65,560-byte controls. Versus the prior indexed run, mean allocation falls from 523,899.2 to 455,446.4 bytes for TimeRun and 1,190,317.47 to 1,055,937.6 for Team Solo (13.07%/11.29%). Later elapsed timings are much slower; scheduling was uncontrolled and no speed gain is claimed. Evidence: `BuildNotes/online/validation-resources-20261023.{log,json}` and comparison JSON. The physical-validation metadata path was corrected after execution without source changes. FAN candidate native execution is in progress; no player or production acceptance is claimed.

## 2026-10-23T17:06:09.095Z Moderate FAN contact native result

`TestResults/editmode_20261023T165742Z.xml` is FAIL (1 case, 0 skipped; gate 276.2 seconds). The candidate moves 3.953 m, exits wind at tick 75 and naturally stops, with zero recorded non-floor proximity and zero last-second speed/drift. It never satisfies the exact 3D pivot arrival; samples place the pivot at Y15.9999886 against authored minimum16. The source lane is investigating production sensing parity. No material/prefab assignment occurred; the failed assertion is retained. Exact results/hashes: `BuildNotes/online/native-fan-moderate-contact-20261023.json`. Starting the QA Windows build on existing verified-source baseline for actual visual/smoke evidence; it is not release acceptance.

## 2026-10-23T17:21:08.801Z Windows build succeeds; player exposes stripped preview shader

Build `20261023T170613Z` passes R4, 491.3 seconds, exact HEAD d7b5a1b. Normal Windows smoke171517Z fails: missing preview shader, then dependent playtest/return/Undo failures. Inspected party screenshot is entirely black. Evidence/hashes and unchanged owner-workshop check are in `BuildNotes/client-view/windows-first-player-20261023.json`. Explicit saved material templates replace Shader.Find-only workshop/guide dependencies; root R0 passes171900Z. Native asset generation/rebuild and independent black-frame investigation follow.

## 2026-10-23T17:34:00Z Floor-boundary rules pass; Windows material assets preserved

The native material creator passes (Logs/exec/20261023T172111Z.json; 96.9 seconds gate), and Unity-generated opaque/transparent templates and metadata are committed. Source verification now checks blend, alpha, depth, queue and RenderType without overwriting edited assets. R0 passes 172632Z; core passes 208/208 at 173137Z in 46.2 seconds, including 19 floor-boundary cases. Candidate native FAN test is running from 8174c8d via wrapper 48928. Exact first Windows build/smoke evidence remains preserved; the all-zero RGBA party capture is not visual acceptance. Atomic reconciliation source review is complete through b7202ff, execution pending. No release or owner visual approval is claimed.

## 2026-10-23T17:38:30Z Native FAN boundary candidate passes; atomic rules pass

Native candidate 173251Z passes 1/1 in 172.7 seconds: wind arrival at tick 43 with a 0.011444 mm lower-floor gap, wind exit at tick 75, natural rest and zero recorded obstacle proximity. Exact raw observations and hashes are in BuildNotes/online/native-fan-floor-boundary-20261023.json. The item contact setup and actual-prefab regression merge cleanly while retaining shared sensor/raw diagnostics. Independent atomic source chain is integrated, excluding baseline-only eaa766a. R0 passes 173658Z (14.6 seconds); combined Rules pass 233/233 at173734Z (41.4 seconds), including25 new atomic cases. Native prefab creation is running; actual-prefab/full physical/local lifetime/native public verification remain pending. Original assets stay default, with no release or visual-approval claim.

## 2026-10-23T17:48:00Z Native local and actual contact checks pass; sorting-arm route remains open

Native174133Z returns FAIL18/19, zero skipped,301seconds. All8 atomic/local gameplay cases pass, both wind/contact cases pass, and8 collision/motion-policy cases pass. Fullsix-family case223.48seconds fails at Cross slowed native sorting arm with the keepsake (ExpectedComplete,Waiting; WordboundNativePuzzleActions.cs329). Raw hashes/cases/observations saved in BuildNotes/online/native-local-atomic-contact-20261023.json. Assets owns bounded diagnosis; Input reviews evidence. QA party rendering observation1cd2690c is integrated and R0PASS174719Z16.3seconds. It preserves actual route/deadlines and first failed capture while requiring splash/repaint/camera completion and nonblank pixels. Windows QA rebuild follows; no full-route, public or production acceptance is claimed.

## 2026-10-23T18:15:00Z Visible Windows failure preserved; targeted repairs underway

QA build174755Z passes232.5seconds. Official smoke175202Z fails rendering readiness and later playtest; normal-window comparison175916Z produces real party/workshop captures but native-crashes entering play, exit0xC0000005. Matching installed UnityPlayer and PDB identity verified; offline DbgHelp lookup lacks a helper (Winerror126), so bounded public-symbol parsing follows. Raw dump remains private under ignored .harness; hashes and actual captures are preserved in BuildNotes/client-view/windows-visible-crash-20261023.json. Independent crash/workshop reports committed50520a4c/263b2612. No kit edits or driver cause claims. QA persistence and viewport rendering fixes have disjoint lane owners. R0 after CLOCK diagnostics passes180336Z in25.9seconds. Native targeted CLOCK/public Team Solo run starts18:11:13Z via wrapper30160; no outcome yet. Original models/defaults and all existing work remain preserved.

## 2026-10-23T18:29:00Z Workshop fixes compile; native failure causes separated

Native181114Z fails both targeted cases, zero skipped,383seconds. CLOCK first waypoint is reached within2.285mm by canonical physics but missed by all4coroutine samples across240ticks; public TeamSolo matchingchunks expire14766/10000ms. Evidence preserved in BuildNotes/online/native-clock-atomic-team-20261023.json. Fixture input-cadence repair and bounded snapshot-capture optimization have separate owners; production clock-field cause remains open.

QA exact autosave/capture prerequisites and workshop clipped/decluttered overlay changes are integrated, independently source-reviewed. R0 passes182307Z in29.7seconds. First overlay native invocation182644Z mistakenly used namespaceWreckabulary.Tests and ranzero tests (FAIL); corrected actual namespaceWreckabulary.EditorTests is running182832Z. Unity-generated metadata is committed3069ca1c. Existing failures remain intact. Matching PDB public symbols point to shader-variant monitoring. Official Unity6000.6.4f1 notes list candidatefixUUM-150584; isolated non-elevated local editor download is in progress, with no project-version/settings change yet. No tested Windows release claim.

## 2026-10-23T18:31:00Z Workshop overlay native calculations pass

Corrected overlay class182832Z passes13/13, zero skipped,109.8seconds including82.5seconds refresh. Full two-pixel line bounds, rejected segments, label priorities/collision, active/hidden-floor camera framing and near-plane projection pass. Actual Windows screenshots and interactive acceptance remain pending. No further suite repetition planned absent changes. Source/report/progress committed; engine candidate download continues in ignored local toolchain, with original editors/project version unchanged.

## 2026-10-23T18:49:00Z Preserve combined-check failures and measured packaging evidence

Capture optimization041ce4a8 passes R0 and35 authority core tests183349Z; native timing remains pending. Integrated CLOCK steering, editor-only observer and bounded aggregation. CombinedR0 184145Z fails on obsolete GetInstanceID and FusionNetworkBool/readback compatibility; source owners are repairing those exact errors without changing physical predicates or deadlines. Optional equipment is5,344,466packedbytes in QA174755Z; report BUILD-ASSET-SIZE-REVIEW.md records dependency/compatibility analysis and defers migration. Original artwork and all editable revisions remain preserved. Unity6000.6.4f1 download completed3,851,126,776bytes; CLI still requested runas despite no-elevate and is waiting on Windows approval. User was asked to handle the actual OS prompt. Existing Unity/project versions remain unchanged. No new native or Windows PASS claim.


## 2026-10-23T18:50:00Z Combined diagnostic compile repaired; native comparison running

The preceding entry headed18:49 was written at18:45UTC; that heading was rounded incorrectly. Its content and failure evidence are preserved. Diagnostic fixes9ef21365 and65d1b4ea replace obsolete IDs with opaqueEntityId, initialize input readback and compare NetworkBool explicitly. CombinedR0 184701Z PASS13.5seconds. Native184749Z now runs six-family, publicTeamSolo and eight local/atomic cases on unchanged6000.6.3f1. Wrapper52492/gate49388 owns the only active editor. No outcomes yet. Unity6.4installer signature is Valid, signerUnityTechnologiesSF; MD5 68A3DAD8CC78181AA7D088D974ADEECD matches officialdownloadchecksum. OSapproval remains pending, target unchanged and empty. Report-only compatibilityaudit d4753442 identifies exact version metadata and optional profilehelper guard requirements; no package change required.


## 2026-10-23T19:00:03Z Native input repair verified; gameplay and admission faults separated

Native184749Z FAIL8/10, zero skipped,333seconds. Eight local/atomic cases pass. CLOCK206.979seconds:300 injected inputs match300 post-physics reads; all three approach waypoints advance and arrival is observed. Actual CLOCK decisions are295LineBlocked/5Applied; course-surface endpoint geometry is under source/native repair. Public TeamSolo17.399seconds fails before gameplay at connection authorization: signedJWT lifetime15seconds, lease remaining15004ms. Snapshot comparison was not reached and empty profiler data is unavailable, notzero. Exact hashes/results retained in native-clock-steering-capture-20261023.json. Input owns collider-center repair; Online owns signed-time lease consistency with strict expiry/bindings retained; Assets reviews both. Root QA rebuild started18:58:59Z on unchanged6.3/source85764fe7 toinspect existing workshop/autosave repairs. Candidate6.4 still awaits OS approval. No source integration during build.


## 2026-10-23T19:19:00Z Physical route passes; Windows and public transfer remain open

Native191029Z on1ba367ca completes all six physical puzzle families and the collider-centre floor/wall regression:2/3 tests pass, zero skipped,385.7s overall. Public Team Solo reaches movement and authenticated command but fails matching snapshot transfer at15,117ms against the unchanged10,000ms limit. Exact gate/XML hashes and markers are preserved in BuildNotes/online/native-clock-center-admission-20261023.json. Unity-generated metadata is committed cb2a4831.

QA185900Z passes154.3s. Official190228 hidden launch fails readiness. Visible190422 passes exact primary save/edit, return and Undo/Redo without native crash or console errors, but exact_playtest exceeds90s. BuildNotes/client-view/windows-workshop-fixes-20261023.json binds actual checks/images/source. QA substage/frame/tick observations plus a genuine readiness capture are integrated6d8ad406 and independently reviewed77849c22; R0 passes191738Z17.1s. New QA build runs from77849c22 to distinguish readiness from post-ready frame progress. No deadlines, original artwork, production startup or SDK changed. Unity6.4 installation still awaits Windows approval; production acceptance remains open.

## 2026-10-23T19:35:00Z Windows slowdown identified; count comparison not yet reached online

Release-QA191831Z passes130.3s. Actual normalvisible192105 reaches readiness6.611s and produces a nonblank original-avatar/world/HUD capture, but times out in wait_90_frames with53 yields at91.086s/tick3300. Each15-frame interval advances900 canonical ticks. Exact save, return/history/cleanup and Undo/Redo pass, no console errors or native crash. Source/raw/image hashes: BuildNotes/client-view/windows-playtest-throughput-20261023.json; independent image review WINDOWS-LOCAL-READY-192105-REVIEW.md. No precise per-tick CPU/GPU cause is claimed; ProfilerMarker.Auto scopes are unavailable in this nondevelopment QA profile.

Count-only capacity sourcee5bce9f1 passes R0(192729Z14.8s) and65/65 focused core cases(192744Z22.5s). Native192928Z fails startup writer-lease expiry atcanonical0; eight local cases skip because root omitted WORDBOUND_LOCAL_GAMEPLAY=1. This is not a count-performance result. Full failed/skip evidence is in BuildNotes/online/native-capacity-count-startup-20261023.json. Windows count comparison build runs under owned wrapper13136. Next combined native job must restore the local enabling flag. No deadlines or TTLs changed.

The reviewed windowed smoke proposal remains unapplied; owner bounded approval is pending. Its file was normalized to LF after CRLF prevented git apply --check; check now passes and scoped .gitattributes preserves LF. Unity6.4 installer still awaits actual Windows approval. No kit or engine change has been applied.

## 2026-10-23T19:57:00Z Windows comparison and first full online completion attempt

QA build193501Z passes111.1s; visible193821 reaches readiness4.033s and completes70/90 frame waits before the unchanged90s limit, compared with53 previously. Save, return and Undo/Redo pass; no console errors or native crash. Exact evidence: BuildNotes/client-view/windows-capacity-count-comparison-20261023.json. Performance remains FAIL.

The same-tick confirmed-result host-frame repair passes R0 and36/36 authority cases (core194255Z). Native194601Z passes8/8 local/atomic cases, but the new full TimeRun fixture fails coordinator preparation before runners; no full-match claim. BuildNotes/client-view/NATIVE-FULL-TIMERUN.md preserves failure and cleanup evidence. Safe setup diagnostics are next. Online owns isolated collision registration optimization/tests; Assets owns isolated readability controls; Input reviews both and owns the full-match fixture. Original artwork and all other worktrees remain preserved. Kit window-launch fix and actual Unity6.4 Windows installation approval remain pending; neither source kit nor editor version changed.

## 2026-10-23T20:14:00Z Actual Windows readability and remaining movement failures

Native200052Z passes63/65: ten local/atomic collision cases plus53 input/HUD/menu/guide/story checks, with no skipped cases. Public Team Solo has zero client input samples during the failed movement phase; compact TimeRun passes preparation/input/command acknowledgement, then its low analog steering stops outside the unchanged target radius. Both cleanups succeed. Exact artifacts: BuildNotes/client-view/native-collision-readability-20261023.json. The reviewed test-only steering repair preserves physics and deadlines.

QA200608Z builds in112.1s. Visible200812 confirms clearer actual HUD/story/checkpoint captions and original artwork, but only78/90 frame waits complete before timeout. Four other workshop steps pass, with zero console counts and no native crash. BuildNotes/client-view/windows-readability-collision-20261023.json binds19 exact files including shipping assemblies and inspected captures. Original style approval, FPS, full multiplayer and stripped release acceptance remain open.

Three membership substitutions pass166 Rules cases. The subsequent temporary retirement index preserves ordered resource/body processing, spending, generations and mutable demand, and passes R0 plus167 Rules cases (201209Z). Native public/full-TimeRun comparison201239 is running. No kit or SDK/editor changes, no deadline relaxation and no modification of original main/font/Lunar work. Pending bounded kit/actual Windows installation approvals remain unchanged.

## 2026-10-23T20:21:00Z Retirement checks and first actual compact socket completion

Native201239 finishes0/2PASS,0SKIP. Public Team Solo now records474 client input reads,358 moving reads, but fails its unchanged10s movement requirement. Inclusive Reconcile391/4659.606ms and Advance390/3090.472ms do not establish player FPS or exclusive costs. Compact TimeRun physically collects five letters, crafts and places TABLE, holds the real socket30ticks and solves it at tick1807; the next fixture Single lookup fails because normal lifecycle compacts the consumed body. Input owns a bounded assertion correction independently source-reviewed by Assets. No lap/result pass. Both cleanups succeed. Exact XML/log hashes and observations: BuildNotes/client-view/native-retirement-full-online-20261023.json.

Retirement EntityBudget/Practice suites add36/36PASS in15.4s to the167 earlier focused Rules passes. QA201839 builds PASS107.4s, source449f292e, Unity6.3 nondevelopment GSK_QA. Same normal-window workshop diagnostic202045 is running. Official smoke is still pending the bounded kit change; Unity6.4 actual installation remains pending. No runtime/editor/SDK/kit changes during these runs.

## 2026-10-23T20:24:00Z Actual retirement Windows comparison preserved

QA201839PASS107.4s; visible202045FAIL with75/90 waits, readiness5.671s, no console errors/exceptions/warnings or recorded native crash. Exact save183314bytes, return/history and Undo/Redo pass. Root and Assets directly inspected actual captures; original avatar/readable HUD/captions remain. BuildNotes/client-view/windows-retirement-20261023.json binds19 artifacts including actual Rules and game assemblies. Previous78 and current75 are uncontrolled separate runs, not isolated causal performance evidence.

Consumed-table assertion repairb8e5df98 integrated32e2c99c after independent source review; R0202315PASS11.9s. Full-only native job native-full-consumption-2023 started20:23:43Z, wrapper47316, unchanged8min fixture/10min NUnit limits. Online owns isolated compile-gated Stopwatch phase instrumentation; no input-flow diagnostics, SDK or kit changes in that slice.

## 2026-10-23T20:28:00Z Complete compact authenticated TimeRun passes

Native202344PASS1/1,0skips,173.549497s case/216.4s gate, refresh21.7s, exit0 and no crash-after-quit. Laps complete at canonical3151/5819/8747 with3/6/9 objectives. Both clients confirm natural Win at8747; exact durable checkpoint is independently downloaded, imported/hash-verified and ACKed. Three fresh TABLE crafts/deployments/native socket holds are consumed and removed, keepsake deliveries use real input, and non-carrier finish protection passes. Cleanup/teardown assertions pass. Source32e2c99c; BuildNotes/client-view/native-compact-full-match-20261023.json binds exact artifacts. This is two scripted authenticated runners in one Editor on a compact private authored map, not public flagship, all modes, independent Windows clients or human feel acceptance. Timing instrumentation is the next bounded performance step.

## 2026-10-23T20:37:00Z Public TeamSolo control and timing semantics pass

R0203206PASS8.5s and ordinary core203228PASS149/14918.6s. Native203305PASS20/20 in140.7s (refresh53.5s), including19 native phase-capture cases and publicTeamSolo62.359596s. Public movement7.088s, command3.544s, exact durable ACK8.744s; cleanup passes. PHASES0 emits no phase windows. Exact XML/log/job hashes: BuildNotes/client-view/native-phase-control-20261023.json. This is one bounded successful entry after intermittent failures, not stable performance/allmodes.

Enabled comparison uses identical executable source and exact20-casefilter with only PHASES1, wrapper30324, started20:36:04Z. Head2eec7d91 adds Unity-generated metadata only. The source-reviewed additive QA timeout boundary fixef202945 is deliberately not integrated during this native comparison; it must be applied before actual QA capture. No SDK/kit changes.


## 2026-10-23T20:53:00Z — Actual phase results and deeper attribution

Native203605 passes20/20, including public TeamSolo and19 timing collector checks; disabled control203305 also passes20/20. Enabled ready window is valid, with FullValidation3858.095ms/459 calls; the uncontrolled pair is not a speedup claim. Exact source/results/hashes: BuildNotes/client-view/native-phase-enabled-20261023.json and independently reviewed BuildNotes/online/PHASE-CAPTURE-RESULTS.md. QA203920 passes102.6s. Actual visible204132 fails81/90 update waits, with exact save/return/history/Undo PASS and console0/0/0. Both timing windows close before teardown; ready FullValidation19601.250ms/5061 calls. See windows-phase-capture-20261023.json and QA-PHASE-CAPTURE-REVIEW.md. Original assets remain default. Integrated reviewed14-phase attribution slice95b43ecd+a7324be9 as5dce1034+0d6857f6; source preserves checks/order and includes the test-only precedence correction. R0205051 passes21.8s; core205136 passes149/149 in22.8s;21 native cases are next. Full delivery, mode matrix, prior crash and owner kit/UAC actions remain open.


## 2026-10-23T21:12:45Z — Native original camera passes; remaining UV lifecycle and public failures

Fourteen-phase native205256 passes21/21 and identifies BodiesValidation as largest full-validation child. Reviewed per-call mechanism grouping ea1505bc preserves every check/error; R0210222 passes10.3s and core210239 passes159/15920.8s. Same public workload210425 fails client movement deadline (20/21 overall, collector20/20, cleanup0); partial ready Body4.468ms/call versus prior4.136 shows no established speedup. Exact evidence: BuildNotes/client-view/native-body-lookup-20261023.json. Camera runtime look sourcef4c7af48 remains; first native205848 had2pass/1fail because the owned-camera test incorrectly ran a runtime-only API in EditMode. Test-only0ae86290 entersPlayMode with guaranteed exit. UV sourceebc11fba restores original scale via uncached owned meshes, independently reviewed. R0210827 passes11.3s; native210954 passes23/24: camera3/3, original Surfaces4/4, CourseWorld16/17. Direct-parent-destruction mesh cleanup fails; Assets/Input own repair/review. See native-original-presentation-210954.json. No new Windows build or production acceptance claimed. Original models and other worktrees remain preserved.


## 2026-10-23T21:23:00Z — Original surface lifetime and camera checks pass

Native211545 on7c76f151 passes24/24, zero skips,66.9s gate/38.3s refresh, exit0/no crash-after-quit. CourseWorld17 includes direct-parent deletion, explicit disposal and failed construction of owned UV meshes; original Surfaces4 and camera3 also pass. Source19615189 adds ExecuteAlways to the owned lifetime component without changing gameplay or geometry. Exact evidence: BuildNotes/client-view/native-original-presentation-211545.json. QA build started21:21:31Z on the same executable source; no pending lane source is integrated during build. Next inspect actual Windows floor scale/camera/original models against204132, then integrate reviewed capture/Freestyle/timing slices. Public mode matrix, frame throughput, earlier crash and production acceptance remain open.


## 2026-10-23T21:29:00Z — Windows originals and Editor equipment captures

QA212131PASS118.8s. Visible212401FAIL76/90 waits, readiness5.449s, other four workshop steps PASS, console0/0/0 and normal shutdown without new native crash. Root/independent Assets compared actual ready images: correct original-scale floor/ceiling boards, richer original avatar shading, readable unchanged HUD/captions. Exact23-artifact record: BuildNotes/client-view/windows-original-presentation-20261023.json. Reviewed fifteen-phase source, shared Freestyle test and original-art Editor capture helper pass R0212640 in21.7s. Native helper212716PASS80.7s creates two original-mesh PNGs and receipt under BuildNotes/equipment-polish/native-originals/original-baseline-2127. Root sees apparent separation between held BAT and visible mitten despite socket alignment; Assets investigates preview-versus-runtime pose before visual acceptance. Raw captures preserved. Next targeted timing/public/Freestyle checks. No deadlines relaxed or kit/SDK/editor/original-main changes.


## 2026-10-23T21:35:00Z — Shared Freestyle and public timing batch pass

Native212947 onac4071d3PASS22/22,0skip,230.9s gate/14.4srefresh, exit0/no crash-after-quit. PublicTeamSolo64.191s and compact private authored Freestyle125.103s both pass, plus20 collector cases. Freestyle selects/solves actual TABLE with teammate, verifies exact GIFT mint/replay/fresh repeat, preserves inventory through next station, and observes5 active native/canonical attack opportunities without teammate damage. Service cleanup0 both cases. Native-source-freestyle-20261023.json binds raw evidence. Public ready20.042s: BodySource1190.839ms nested within Bodies1599.910ms/Full3803.429ms (449fullcalls,70942sourcecalls). Online owns reviewed per-call membership candidate; no speedup claim. Diagnostic-only HostStep c2726d7c integrated after the batch, native NOT RUN. Original-capture skinning follow-up remains Editor-only; original models unchanged.


## 2026-10-23T21:50:00Z — Public FFA and Freestyle pass; Brawl and fixture repair remain

Native213856 on81a8ca42 passes2/3: FFA55.969s and Freestyle48.756s pass public entry/movement/command/exact ACK; BrawlSolo fails40.122s on transfer age14,209ms versus unchanged10,000ms. Packet headers agree, not proof of complete payload reconstruction/validation. Every service cleanup count0, exit1/no crash-after-quit. Exact artifact hashes checked in native-public-three-20261023.json; independent BRAWL-ADMISSION-REVIEW.md preserves the boundary. Current per-call BodySource lookup038219de passes R0214340 but core214428147/156: nine added Item tests fail selecting a nonexistent protected Item before validation. Test-only repair975b4d14 uses public Economy craft/drop and requires a fully valid baseline; Input review08c279a0 passes, integrated asbb737f10/e27283fa. R0215008PASS10.1s; corrected core rerun is running. No deadlines, kit or editor versions changed.


## 2026-10-23T21:59:00Z — Corrected core passes; native throughput remains open; original preview corrected

R0215008PASS10.1s and core215023PASS192/19230.6s after test-only Item fixture repair. Before/after frozen probe120/120 both, exact input/hash/calibration: median allocation reductions14.235% TimeRun and30.996% TeamSolo, with slower median standalone times26.453ms/58.419ms. Native215152on3aa74c6fFAIL21/23 in260.1s: timing20 and sharedFreestyle80.000sPASS, Brawl48.640s movement deadlineFAIL after all-ready, TeamSolo40.859s stale initial transfer15,006ms against10,000msFAIL. Cleanup0all, no crash-after-quit. Exact native-source-index-20261023.json; no speedup claim. First incorrectly formed local job was rejected before Unity (exit2/NOT-RUN); corrected job preserved separately. Original preview215708PASS43.6s, pair and receipt under native-originals/original-baseline-2157; hashes verified and root directly inspected both: all4original world models visible, heldBAT no longer visibly floats over mitten. Neutral Editor preview only, held hood cropped, no runtime input/equip or owner acceptance. Assets independently reviews. No production model/grip changes. Next latest QA16-phase Windows observation.


## 2026-10-23T22:09:00Z — Visible workshop passes; immutable capture-key candidate verified in core

QA215906PASS112.7s onexactecd968fd; visible220124PASS5/5 exit0, console0/0/0, readiness4.301s,90waits76.228s, complete79.579s. Save183314bytes/return/draft/Undo exact. Windows-source-index-20261023.json hashes14artifacts. Root viewed world/menu/return: originals correct, menucontrast/defaultstyling and sparse world remain production gaps. Supplemental diagnostic only, not officialsmoke/FPS or fullonline acceptance.16phase ready75.279s: HostStep54.460s/4338, FullValidation15.879s/4482, BodySource1.532s/354078. Whole-player CPU95.031s, peakmemory unavailable.

Next bounded optimization caches only immutable nonnegative resource/mechanism slash sort keys; negativeTeam preserves live culture formatting, stable Freeze/codec unchanged. Independent source review PASS. New5codec cases cover prefix/punctuation/equal-key order, all24team/lap combinations and cross/mutatedculture, retained bytes/hash and replacement. R0220712PASS17.9s; core220731PASS218/21828.8s. Opt-in unchanged-authority capture probe has120/120 matching-hash captures before/after; exact results in capture-sort-key-probe-20261023.json. Allocation medians fall70.265%TimeRun/72.481%TeamSolo; timings remain bounded standalone evidence, native/Windows pending.

## 2026-10-23T22:22:00Z Native capture compatibility passes; full FFA exposes allocator exhaustion

Exact source6c1937ab, editmode221108:48/49PASS,0skips,269.614s tests/331.5s gate, exit1/no crash-after-quit. All26 codec and20 phase cases pass. Public TeamSolo55.507s and BrawlSolo56.071s pass their bounded entry/movement/command/ACK contract. Full FFA fails155.974s with Fusion allocator OutOfMemory Size1260; the first occurrence is during first hammer craft, before combat. Both crafts and first real hit/replay later complete, but a victim-return walk expires35.059s; fault-contaminated timing does not isolate its steering cause. No ten-KO/respawn/result completion. Service cleanup failures0. Raw hashes and outcomes: BuildNotes/client-view/native-capture-ffa-20261023.json; independent review: PUBLIC-FFA-SCORED-FINISH-REVIEW.md and CAPTURE-KEY-REVIEW.md.

Test-only source7ff66792 adds opt-in five-second per-runner allocator/object occupancy logging to that existing fullFFA fixture; capacity, SDK, runtime, deadlines and assertions remain unchanged. R0221940PASS9.7s, core222035PASS26/26 in7.6s. The isolated native diagnostic started22:21:05Z as native-ffa-memory-2221, wrapperPID8672. Initial-only bootstrap ACK implementation and original-palette editable theme remain isolated pending review/integration. Production acceptance remains open.

## 2026-10-23T22:37:00Z Initial bootstrap execution and isolated allocator diagnosis

Native223028 on32306015 passed58/60 with zero skips: all57 OnlineTests and actual two-peer host-departure recovery passed. Every initial host started at canonical tick0 only after exact replica import/ACK. Recovery took18.762s from graceful stop to all-ready; the ten-second target remains unmet. Public Team/Brawl both failed the immediate unpaused assertion after native body replication: host ticks137/145, no session fault, temporary durable-checkpoint backpressure under investigation. No failure is suppressed. Exact evidence: BuildNotes/client-view/native-bootstrap-barrier-20261023.json and original XML/log. Gate279.8s, exit1, no crash; all service cleanup counts0.

The separate FFA allocator diagnostic222105 failed1/1 in280.596s, with84 sampled rows and402 allocator exception headers preserved in native-ffa-memory-20261023.json. Installed SDK inspection identifies bounded60-entry object histories;1260-byte body state uses1280-byte allocator segments. The roughly178-body arena requires more than8MiB for full history. The maximum512-body envelope motivates a64MiB per-heap online policy (~128MiB per Runner); full-case and maximum-capacity execution remain required. Review found actual local playtests shared the online factory, so the pending fix separates that route rather than increasing offline memory. SDK files and deadlines remain unchanged.

## 2026-10-23T22:46:00Z Resource identity checks pass; online and editable theme ready for native checks

R0224454PASS12.1s and corrected core224523PASS223/22330.5s on0516c1b8. The previous212/213 failure was an unsupported FFA fixture; the repaired case uses the flagship's supported non-race Freestyle and asserts that branch. Production tuple identity validation is unchanged. Frozen120-validation before/after probes preserve all input hashes and zero errors: median allocation falls16.7683%TimeRun and18.3594%TeamSolo. TimeRun median elapsed is slower; no FPS or speed improvement claimed. Exact batches and raw hashes are in BuildNotes/client-view/resource-identity-probe-20261023.json.

Online64MiB per-heap policy now preserves the actual local client's original8MiB route and passes independent source review. Joint current-readiness/body-replication test fix preserves its25-second deadline and all subsequent assertions. New original cream/teal/Nunito UI theme, opaque menus, measured row heights and workshop pointer bounds are integrated and source-reviewed. Unity-generated default asset, native UI/configuration checks and rendered Windows acceptance are next; none is inferred from source checks. Original artwork remains default; original/ICY/uncommitted work is preserved.

## 2026-10-23T22:59:00Z Default theme created; native failures isolated and repaired for verification

Unityexec224624PASS94.2s, refresh75.6, exit0/no crash creates Interface.asset and original Nunito font references, and adds only its launch assignment. Root inspected generated YAML/GUIDs; metadata and creation evidence committed dc60af3b. Native224855 then108/111PASS,0skip,108.5s gate: all three heap/configuration checks pass. Two new UI checks expose50.90625px text in a50px module and an incorrect synthetic-Repaint ordering assumption. Reviewed source now measures minimum heights without overwriting designer assets and verifies actual Repaint-to-Layout boundaries. Existing draft recovery also hits Windows file replacement failure; reviewed fix avoids replacing byte-identical saves, retains atomic changed-content replacement and memory recovery, and adds an actual Windows read-share lock/BOM regression. No retries or file deletion fallback. R0225821PASS11.2s; native18-case theme/persistence rerun is underway on73a3326c.

Native225207 on66a0546d passes2/3,192.052s tests/242.6s gate, no crash/zero cleanup failures: publicBrawlSolo58.027s and TeamSolo58.496s pass entry/movement/command/exact ACK with current readiness and configured64MiB heaps. FullFFA75.283s fails its second CollectLetter with OutOfReach before crafting/combat. Four allocator samples have no OOM headers but do not reach sustained history; no repaired full-capacity claim. The remote player's position lies between the collector and tile; the fixture's distance-only approach does not establish real LOS. A reviewed parking/actual-LOS fixture repair is next; production reach limits remain unchanged. Exact outcomes/raw hashes: native-ui-heap-20261023.json and native-online-heap-20261023.json. Full production and Windows delivery remain open.

## 2026-10-23T23:25:16Z Original-style Windows flow and remaining FFA failure

QA build20261023T230521Z passes on1ab60670; actual visible230748 passes5/5, console0/0/0, exact183314-byte draft/return/Undo preserved and normal exit. Six images and payload hashes are preserved in BuildNotes/client-view/windows-original-theme-20261023.json. Root and Assets inspected the originals and the cream/teal panels; Search wrapping, workshop status contrast and old party styling remain visible in this executable. Independently reviewed f82d7610/e4e133f0 are now integrated for native and fresh player acceptance. No owner style approval or sustained FPS claim.

Native231117 on8a2e061b finishes1/2: isolated null/named replacement diagnostic passes all six observations; full FFA fails485.215717s after several real knockouts/respawns because the eight-minute fixture lifetime expires during a return walk. No full result is accepted.172 memory samples contain zero OOM headers; last client allocated segment capacity14,404,608/free1595pages is not live usage or plateau proof. Exact evidence: BuildNotes/client-view/native-ffa-draft-20261023.json and TestResults/editmode_20261023T231117Z.xml. The isolated IO result does not close actual changed-draft publication failure. Native UI/input and capacity preflight are underway on e0cb5625, no external capacity run yet. Root source-reviewed ec0d0821 QA-only bounded frame observation; runtime measurement remains pending.

## 2026-10-23T23:53:00Z Original-style Windows evidence and full authoring persistence

QA Windows232641 passes113.4s on8c02f106; visible232850 passes5/5, exit0 and console0/0/0, preserving exact183314-byte draft/return/Undo. Root and Assets inspected all six captures. Search/footer/party theme corrections are visible with original artwork; frame observation remains90 updates in7.240s with479 canonical ticks and696.499ms maximum gap. Exact payload/capture hashes are in BuildNotes/client-view/windows-original-theme-polish-20261023.json. Camera callbacks do not establish GPU time or presented FPS. Compact party layout/help is integrated for native verification; original/ICY/font/Lunar work is preserved.

Native233231 passes3/3: actual changed-save/BOM regression, retained-temporary IO sequence and registered-PLATE capacity preflight. Native233437 then passes all13 authoring persistence cases; full public FFA fails487.762312s after six scored knockouts/respawns. Its final5.999s movement ends with lifetimeCancelled=True, below its own35s limit. No terminal result.162 memory samples and no observed OOM do not prove capacity/leak freedom; late object capacity is stable while client general-used still increases. XML/log hashes, IO rows and review are in native-ffa-authoring-20261023.json and NATIVE-FFA-AUTHORING-233437-REVIEW.md. Prior1175 remains unattributed; no new production IO workaround.

Reviewed mechanism tuple identity change c14dfc69 passesR0234800 and198/198 focused core234828. The unchanged sequential before/after probe preserves input/snapshot hashes and passes120 validations each with positive byte calibration. Median allocations fall7.9829%TimeRun/8.5800%TeamSolo; elapsed medians are lower in three sampled batches, not a stable-speed/FPS claim. Evidence: BuildNotes/client-view/mechanism-identity-probe-20261023.json. Native UI18/phase20/publicTeamSolo is running onbfed9990; checkpoint-pause test diagnostics remain under development. No production acceptance claim.

## 2026-10-24T00:12:00Z Native checkpoint measurements distinguish the client stall

Native000038 on e8dddc6b passes45/47: all8 checkpoint diagnostic,20 phase and9 theme cases pass. PublicTeamSolo still fails movement; exact-case records show12 accepted durable publications without failed requests, mean1075.294ms publication-to-ACK and1417.311ms subsequent gap. Host519 durable-gap samples are not wall-time duty cycle. Client has no completed SDK batch for11092.939ms before stop while Unity updates continue; during10.876s movement no new local input is read. This locates an execution boundary, not a proven underlying cause. Source-reviewed test-only SDK/input/latest-server frontier facts are integrated for the next exact case. Cadence remains unchanged: more frequent publication could add measured synchronous work/request load. Evidence: native-checkpoint-viewport-20261024.json, CHECKPOINT-PACING-000038.md, PUBLIC-MOVEMENT-234950-REVIEW.md.

The narrow-card fixture now uses its actual viewport and same-Repaint boundaries. Native000747 on3392dbb4 passes17/18; its12-layout trace exposes a real404/425px oscillation at420px window width, while700px is stable364px. A production correction is in progress, with strict convergence retained. Exact failure and traces: native-party-stability-20261024.json. No new Windows build is claimed;232641/232850 remains the last tested review build. Original artwork and all parallel work remain preserved.

## 2026-10-24T00:21:00Z Compact party layout verified; public frontier divergence preserved

Native 001905 on source669b08fe passes19/19 theme/party tests in91.4s, refresh28.8s, normal exit. Actual fitted-card convergence, first/last child bounds, resize, long-error overflow, real wheel scrolling and shrinking pass after the reviewed direct-content/stable-scrollbar correction. Evidence: BuildNotes/client-view/native-party-fit-20261024.json and TestResults/editmode_20261024T001905Z.xml. Fresh Windows verification is next; no owner visual acceptance is claimed.

Isolated public TeamSolo001235 still fails its unchanged movement deadline. LatestServerTick reaches1467 while client SDK state/input remain1268/1249; client simulation completion is approximately5.163s old while Unity Updates continue. This does not prove complete payload or host motor input arrival. Raw lastBatchAgeAtStopMs is age since batch start; the independent report corrects that interpretation without rewriting evidence. Record: BuildNotes/client-view/native-public-frontiers-20261024.json; review: PUBLIC-MOVEMENT-234950-REVIEW.md. No SDK/config/deadline change was made. Original artwork and all earlier work remain preserved.

## 2026-10-24T00:28:00Z Fresh Windows review build and actual six-image inspection

QA002136 passes146s on exactf1b20833; actual normal-window002434 passes5/5 with exact183314-byte draft, return, Undo/Redo, console0/0/0 and exit0. Root inspected all six captured PNGs: fitted initial party card, original avatar/timber surfaces and readable cream/teal/Nunito workshop/equipment UI. Independent image review remains pending. Current build instructions, gallery, status and progress now identify this exact candidate; old artifacts remain preserved. Evidence: BuildNotes/client-view/windows-original-party-fit-20261024.json, with48 artifact hashes including actual game DLL/data payloads.

The local playtest completes13.767s. Its90-update observation spans9.371s, canonical37→640, maximum55ticks/update and895.562ms gap; assigned-camera89pairs total223.312ms/max7.305ms. No missing/unpaired snapshots or callback pairs. This is not presented FPS or a demonstrated speedup. Startup composition, held/combat views, online reliability/full modes/large parties/recovery, official smoke and final release remain open. All19 native party/theme cases pass separately. No SDK/frame-time/cadence settings were changed.

## 2026-10-24T00:32:00Z Actual Advance baseline and independent Windows review

Independent Assets review personally inspects all six002434 PNGs and confirms exact hashes, fitted party actions, readable workshop/equipment UI and preserved original character/timber. Sparse room, large avatar, pale world text and actual held equipment remain open. Review: BuildNotes/client-view/WINDOWS-ORIGINAL-PARTY-FIT-002434-REVIEW.md. A separate bounded original-equipment Windows inspection scenario is being prepared; no new art generation.

The independently source-reviewed optional Advance probe runs successfully BEFORE production synchronization changes oncd4ec714:360 measured advances,64warmup, six exact batches, unchanged counts/retained snapshots, full validation and positive65536-byte calibration controls. Record: BuildNotes/client-view/advance-probe-before-20261024.json. This is an idle real-flagship whole-Advance .NET baseline, not native physics or FPS. Only a per-call defensive team-copy reuse is authorized next; source review and identical after measurement remain required.

Actual frame-delta observer source passes R0 in19.5s. Native four collector cases plus isolated publicTeamSolo is running on59682248; no SDK, scheduling or deadline changes. Original assets, rejected variants and other worktrees remain preserved.

## 2026-10-24T00:36:00Z Actual client frame deltas and later snapshot-age failure

Native003140 on59682248 passes4/5: all four frame-delta collector cases pass; publicTeamSolo passes movement7.223s and authenticated command3.177s but fails the subsequent durable ACK wait7.810s. Exact session facts identify an11485ms snapshot transfer exceeding10000ms; matching mark/count/length/hash and no duplicate chunk exclude those alternative branches. This is not malformed JSON evidence. Raw record: BuildNotes/client-view/native-public-delta-20261024.json; independent report: PUBLIC-MOVEMENT-234950-REVIEW.md.

The actual client uses UnscaledDeltaTime and records9frames above1second, maximum1835.871ms; no invalid/unavailable samples. Host batches of53/60ticks precede observed client frames1786.904/1588.851/1553.745ms with zero observed TicksExecuted and no retained same-frame batch. These support the installed >1s clock-gate mechanism; MonoBehaviour.Update observations do not directly trace the provider or establish exclusive causality. No SDK/config/deadline change is made. QA Windows002136/002434 remains the latest passing review build; production acceptance stays open.

## 2026-10-24T00:44:00Z Synchronization allocation reduction verified; online still fails

Per-call defensive team-copy reuse passes R0 in15s and65/65 authority/lifecycle/practice cases in18.1s (core003718). The identical actual Advance probe passes both360-step runs and64warmup each; all input rows, runtime, start/end snapshots, times, epoch/counts and calibration match. Only AuthoritativeMatch changed among78 measured source/content files. Median allocated bytes fall25.4909%TimeRun and25.5788%TeamSolo. All three elapsed samples per mode are retained, without statistical/native/FPS claims. Exact comparison: BuildNotes/client-view/synchronize-advance-comparison-20261024.json. Independent artifact review also confirms hashes and counts.

Native003910 on0adab099 still fails its unchanged movement deadline; gate113.8s, refresh47.8s, no crash. Ready503steps span16.0679429s; Synchronize1377.433ms, HostStep9779.482ms, ObserveHost1576.921ms, FullValidation2849.340ms/519. Client7frames exceed1second, maximum2032.150ms. Different counts/windows prevent a controlled overall-performance claim. Record: BuildNotes/client-view/native-public-team-copy-20261024.json. This optimization reduces repeated work but does not solve online reliability.

Original-equipment Windows QA source is under independent review, using actual map pickups, letter crafting/equip and explicitly labelled inspection camera. No runtime art, SDK, validation, cadence or deadline change. The existing normal-window helper is backed up locally and accepts the new named scenario; its wrapper allows the scenario's summed step budgets, leaving the workshop default unchanged.

## 2026-10-24T00:52:41Z Original-equipment QA compiles

Native compile PASS in 33.3 seconds, refresh 22 seconds, with no reported errors or crash after quit. Source e34293a2bee931bde5760738ab2bbfa48610f605. Unity generated metadata for the two new QA scripts. Evidence: BuildNotes/client-view/compile-original-equipment-20261024.json and Logs/gate_compile_20261024T004801Z.log. Actual Windows captures and visual acceptance remain pending.


## 2026-10-24T01:01:26Z Windows originals tested; Freestyle equipment issue preserved

Build 20261024T005254Z PASS109.5s on exact c7b0d8a933c2235ba7cca7c195799c7964ae3b33. Actual normal-window workshop 20261024T005757Z-visible-wordbound_workshop_flow PASS5/5, exit0, console0/0/0, exact draft/return/UndoRedo preserved. Root viewed all six PNGs. Evidence: BuildNotes/client-view/windows-synchronize-workshop-20261024.json (48 hashes). The 90-update window is5.004s, worst678.386ms, tick44→393; no causal FPS claim. First equipment005453 fails after2/5 steps: exact sequence1 CollectLetter→OutOfReach, with actual avatar rootY2.548599 at capturetick40. Root viewed the first world PNG; original meshes/unit scale match, but grounding and held/return acceptance fail. Evidence: windows-original-equipment-first-20261024.json (45 hashes). Freestyle initial keepsake/player overlap is being investigated; all artifacts retained in99b701ec. No production physics/reach workaround or new artwork generation.

## 2026-10-24T01:08:37Z Authored observation lookup measured in Unity

R0 PASS13.7s at010409. Exact selected Explicit native010454 PASS1/1 in55.3s, refresh30.7s, no crash. Source ee3b09582bbf6a034d359d2e055ca5a0332fe84c. Construction-inclusive median batch102.6385→30.7367ms across seven samples/path,64iterations and144queries/iteration; all14raw samples retained, output/reference/state hash parity pass. Every65536-byte allocation control reports0, so bytes are unavailable. This is lookup-only elapsed, not whole-observer/FPS. Record: BuildNotes/client-view/native-observe-lookup-measure-20261024.json, committed95219ddd. Focused authored correctness, six-family native flow, CLOCK collider and unchanged public movement are running separately; no reliability claim yet.


## 2026-10-24T01:24:00Z Approved visible smoke fix applied; first failure preserved

Owner approved the exact two-file kit patch; f865c037 applies only optional windowsHide with unchanged hidden default and the smoke player opt-out. Syntax checks pass. Official smoke011426 on existing005254 player opens an actual1280x720 window and produces three nonblank PNGs personally inspected by root, but times out at the unchanged120-second total deadline without checks.json. Exact player PID12536 was absent after gate cleanup. Active approved Unity installer51284 overlapped this run and wrote32.49MB during a19-second sample; no clean performance comparison or full smoke PASS is claimed. Evidence: official-visible-smoke-first-20261024.json and UNITY-6000.6.4-INSTALL-VERIFICATION.md.

Native010750 passes24, fails publicTeamSolo movement and skips one. A summary transcription was corrected before committing its evidence: the skipped case is six-family physical flow because WORDBOUND_NATIVE_PUZZLES was absent; the Explicit lookup measurement did run and pass. The22 ordinary authored cases and CLOCK collider pass. Client canonical tick28 remains unchanged during10.079s movement; latest server tick557 also remains unchanged in this run. Evidence: native-observe-coverage-20261024.json. Earlier six-family191029 PASS remains separate. No SDK/deadline or production physics adjustment. Reviewed equipment startup diagnostics503e11f3 are integrated; actual Windows execution awaits the clean toolchain.

## 2026-10-24T01:27:00Z Clean official failure and isolated patch candidate

The second official smoke012415 on unchanged005254 player still fails the120-second total deadline, with no checks.json; installer51284 and CLI39804 were already finished. Root inspected all three fresh PNGs: fitted party, readable workshop and grounded original character. Exact41660 player PID was absent after gate cleanup. This excludes ongoing installation as a necessary cause, without assigning a new cause. Record: official-visible-smoke-clean-20261024.json. Private normal-window005757 remains a different launch result, not an official PASS.

The approved isolated6000.6.4f1 install completed01:21:57UTC, exit0; Windows Mono playback/compiler/package files verified. Integration-only ProjectVersion now truthfully selects6000.6.4f1(12bfff696524); profile factory allows only the prior.3 and candidate.4 while retaining every profile/scene/define/reflection check. Gate jobs use process-localGSK_UNITY for the isolated executable, without cache edits or global registration. Packages manifest/lock hashes remainDB1C638BD54A4BB37306CCAEDED26E463FB1EF2A3FC249BE09348DFA435196E3 and028AB0CCF1DA79ACC545B2BF9A55A9F26BF5EDAB950E664CC7D550E138D6A61C. Candidate R0/core/native/profile/build and Windows execution are next; installation is not crash-fix proof. Existing.3 build and all prior failures remain preserved.

## 2026-10-24T01:49:00Z Partial patch installation diagnosed; bounded optimizations checked

Candidate profile import013000 on eeee132f fails after370seconds with13 package compilation errors before the profile method runs. Exact record: BuildNotes/client-view/unity-patch-first-import-20261024.json. Independent diagnosis UNITY-PATCH-PACKAGE-DIAGNOSIS.md identifies missing files at the259/260-character extraction boundary, including Sprite source metadata and the270-character Core dispatcher assembly definition; the .3/.4 core managed module is byte-identical. Installer exit0 and selected playback files were insufficient verification. The fresh same-version install targets C:/Users/mrkl6/wb64/6000.6.4f1, preserving both prior editors. No cache purge, manual metadata, package patch or global editor registration change.

Existing validated map-placement dictionary lookup9d303c29 and successful body-presentation tuple reuse fcd9070b are integrated and independently reviewed. R0 on6eed388b passes51.6seconds (014533); focused core014652 passes233/233 in65.6seconds. Evidence: TestResults/core_20261024T014652Z.trx and Logs/gate_core_20261024T014652Z.log. The tuple change retains immediate shape/collider/callback/visibility work and adds a native lifecycle case, which has NOT RUN. No native frame-rate or allocation improvement is inferred from source review/core success. Download overlaps only these source checks, not a claimed hardware measurement. Latest tested player remains005254; public reliability, official smoke, original held-equipment and production acceptance remain open.

## 2026-10-24T02:02:00Z Complete snapshot checks preserved while removing repeated work

Renderer traversal ef571f4e keeps a fresh include-inactive hierarchy query every Apply, using cleared instance list storage instead of a new result array; callback/reentry/visibility ordering remains intact. R0015648 PASS23.3s and independent RENDERER-STORAGE-REVIEW.md. Economy a63699c2 retains the entire replacement validation and exact errors but omits installing the result into a temporary validator that is immediately discarded; public Restore is unchanged. Export2f9fc549 returns its already computed canonical hash to PublishSnapshot, preserving full EnsureValid, final Preflight and publication registration order. Bridge full import/validation, compression, ACK, cadence and deadlines are unchanged. Independent reviews are ECONOMY-VALIDATION-ONLY-REVIEW.md and CHECKPOINT-MAIN-THREAD-AUDIT.md.

Combined R0015905 PASS25s; core020024 PASS268/268 in49.9s on4c3de75e. Exact source/artifact hashes: BuildNotes/client-view/snapshot-work-source-checks-20261024.json. Tests cover retained snapshots, exact invalid-state errors, fresh mutable callback dependencies, canonical JSON/hash/culture/size parity, failed publication sequence reuse and authority/lifecycle/practice. Native tuple/visibility and rebuilt Windows verification remain NOT RUN; the short-path installer is downloading. No measured speedup or production acceptance is claimed. The .4 candidate is now stated accurately in CLAUDE.md while identifying the previously tested .3 player.

## 2026-10-24T02:28:00Z Initial Freestyle spawn fix passes source gates

Initial-only placement now selects a clear unconditional authored recovery anchor in the starting module after complete composition. It excludes declared current/future player starts, props, supplies, moving fixture envelopes and connector solids/floor openings. No candidate leaves an inactive bounded keepsake and rejects initial publication/advance with an actionable message; only a successful fully validated exact durable restore clears that guard. Existing restored poses, recovery/drop behavior, pickup range, artwork and physics dimensions remain unchanged. Independent review: BuildNotes/client-view/FREESTYLE-SPAWN-FIX-REVIEW.md.

Combined source0ff2996d passes R0022342 in18.8s and core022440307/307 in42.9s. Record: BuildNotes/client-view/initial-keepsake-source-checks-20261024.json. First022318 attempt was INFRA because process-local GSK_UNITY was omitted; corrected source gates used managed references from the incomplete isolated installation, not native execution. The new tick-zero native support/penetration test and rebuilt Windows equipment/official smoke are NOT RUN pending the complete short-path editor. The older airborne image remains preserved without claiming initial overlap as its sole cause.

The repaired installer downloaded fully and is awaiting the second Windows permission prompt already presented to the owner. The historical draft-file error is separately clarified: unexpected numeric HRESULT was not captured; later13/13 authoring cases pass, but failed newest edits currently depend on static RAM for automatic recovery. Store/Inspector-only ordered durable pending recovery is being implemented in the isolated assets lane, with independent review. No uncommitted original work or unrelated feature paths were changed.

## 2026-10-24T02:32:17Z Shipped Freestyle maps pass startup with one and eight seats

Added eight focused cases for the four current public Freestyle catalogue seeds, each with one and eight seats. Real flagship generation, complete authority publication/import, active free keepsake on an unconditional starting-room anchor, acknowledgement and first advance all pass. R0022904 PASS11.7s; core022935 PASS8/8 in10.4s. Input independently source-reviewed the exact fixture. Evidence and modified test SHA are appended to BuildNotes/client-view/initial-keepsake-source-checks-20261024.json. These are engine-free startup checks, not native floor, eight-peer transport or deployed public-admission proof.

Timestamp clarification: the preceding entry heading02:28 was a transcription rounded ahead of its write. Its evidence record was created02:26:13UTC; the quoted gate times are exact. Subsequent headings use the current UTC clock directly. Native and Windows gates remain pending the repaired editor; no additional installer or approval request was issued.

## 2026-10-24T02:46:00Z Durable Inspector draft recovery passes source review and type checks

Integrated the bounded Store/Inspector/test chain through37491ee6. New pending edits are serialized into a versioned checksummed record, flushed/read back and committed before the existing primary Replace/Move. Exact legacy drafts remain readable; ambiguous legacy temporary content requires an explicit choice. Read sizes and candidate scans are bounded. Retry can reclaim only validated older records while retaining the newest complete candidate. An explicit discard commits its higher revision before cleanup; both Apply and Discard reset logical Inspector state even when cleanup fails, preventing later close/autosave from resurrecting discarded edits. The original file error stays visible with plain backup/retry wording.

Input independently reviewed Inspector/lifecycle/API and final tests; Online independently reviewed ordering, corruption, cap recovery and sole-candidate retention. Root caught and closed a missing serialized Format check. R0024311 PASS22.7s and final024444 PASS14.5s. Exact hashes: BuildNotes/client-view/durable-draft-source-checks-20261024.json. The35 persistence cases are NOT RUN in Unity; no historical IO-cause fix, hardware durability or visual acceptance is claimed. Gameplay source remains at the307-case plus8 shipped-startup passing state.

Short-path installer34936 still had consent52800 and no Editor executable at02:43:26UTC. The existing approval question remains pending; no new installer or confirmation flow was started. Prepared native-local-authoring-after-short-install-job.json is NOT RUN: after verified installation/profile import it selects12 local gameplay cases plus35 authoring cases. Build, actual equipment, official smoke and public reliability follow only after the relevant native gates. All source slices and unrelated work are preserved; goal remains active.

## 2026-10-24T03:00:20Z Story, endings and player-experience design written and reviewed

Added docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md: full Lost Address plot, three-chapter routes, session/mode expectations, three proposed principal endings and competitive/sandbox conclusions, optional cooperative restoration, predicted player strategies, recovery/abuse cases, rating distinctions, Unity narrative authoring and acceptance. Existing source features remain distinct from new design. Three independent source/document reviews pass after correcting landmark implementation, optional-versus-essential repairs, saved Undo scope, hazard damage, inclusive pacing and existing user text. Evidence: BuildNotes/story-design/REVIEW-20261024.md. All46 local Markdown references resolve and git diff --check passes. No new gameplay or human acceptance is claimed.

Linked the design from the approved plan and refreshed progress.md, docs/PLAN.md and STATUS. Current risks include72 puzzle sites under25minutes, active keepsake withholding and repeated shared-station changes. Public full-match reliability, native draft/placement tests, exact Windows smoke, performance and owner visual/feel acceptance remain open. James approved the repair installer prompt during this work. At02:59:30UTC wrapper34936 was running, consent52800 gone, target editor absent; finish/install-integrity verification must precede native gates. No duplicate installer or deployment started. Goal active; existing source and unrelated work preserved.

## 2026-10-24T03:07:40Z Short-path editor repair verified; source checks pass

The approved repair completed03:04:46.7793093UTC exit0 at C:/Users/mrkl6/wb64/6000.6.4f1. Independent read-only verification confirms ProductVersion6000.6.4f1_12bfff696524, valid Unity publisher signature,14/14 critical source/meta/bridge files, expected bridge assembly name, Sprite253/Core3107 files and unchanged project manifest/lock. Selected CoreModule/ObjectDispatcher hashes match the prior binaries/source. Root read the complete receipt and report; no metadata or package patch was needed. Evidence: BuildNotes/client-view/unity-short-install-integrity-20261024.json and UNITY-SHORT-INSTALL-VERIFICATION.md.

Root R0030600 passes runtime/editor checks28.5s with process-local GSK_UNITY selecting the repaired installation; log Logs/gate_check_all_20261024T030605Z.log. Earlier source tests remain unchanged and green. Normal profile import and the prepared47 native local-gameplay/persistence cases are next, before rebuild/equipment/official smoke and public reliability. No native result, shader-crash closure or production readiness inferred from installation integrity.

## 2026-10-24T03:19:33Z Preserve incomplete package caches for normal fresh resolution

Short-editor profile030743 fails189.3s with the same13 Sprite/Core compiler errors. Fresh installed files exist; the current log and resolution JSON instead register incomplete Library/PackageCache copies created01:30:49. Independent diagnosis confirms missing Sprite/Core/URP/ShaderGraph metadata and the Core friend-assembly boundary in those cached copies. Reports: SHORT-INSTALL-RESIDUAL-PACKAGES.md and unity-short-resolve-recovery-20261024.json under BuildNotes/client-view. No gameplay-source defect follows from these package errors.

With Unity/package workers absent, root validated all paths beneath integration and moved only four affected cache directories plus three generated resolution files into .harness/cache-recovery/20261024T031434Z. All10,255 files/345,619,813 bytes have exact before/after hash verification and preserved source-to-slot mapping; manifestSHA61f71c41c61dcfeff8938be0657e92e06ee6da7bc84235245c5d087df0d7d357. No deletion, invented metadata, global-cache change, package-version edit or Bee reset. Project manifest/lock hashes remain exact. A fresh normal profile import is next; recovery of cache bytes alone is not compile success.

## 2026-10-24T03:27:19Z Repaired package resolution passes

Gate031937 profile creation passes in179.1s with no compiler errors and no crash after quit. All20 checked resolved source/metadata files match the complete short-path Unity6000.6.4f1 installation. The original10,255 cache files remain hash-verified in the recovery manifest; no source/package/profile changes were generated. Opened evidence: BuildNotes/client-view/unity-short-resolve-recovery-20261024.json, Logs/exec/20261024T031937Z.json and the gate stdout/log. Next: prepared47 native local/authoring cases, then rebuild and Windows equipment/official smoke. This is toolchain recovery, not release acceptance.

## 2026-10-24T03:29:57Z First repaired-editor native suite

Native gate032722 completed72.4s:44/47PASS,3FAIL,0SKIP, no compiler errors or crash. Keepsake startup assertion cannot find keepsake:0 in the world dictionary; two durable draft tests still encounter File.Replace IOException after releasing locks. Exact XML/gate/hash record opened and saved in BuildNotes/client-view/native-local-authoring-short-editor-20261024.json. Input owns persistence repair and Assets owns startup-test lifecycle diagnosis in isolated lanes; no failures waived. Public capacity source independently reviewed and integrated; live acceptance pending.

## 2026-10-24T03:36:04Z Current public race checks pass

Public gate033100 on932784f0 passes2/2,0SKIP in129.5s. Opened XML and recorded native-public-races-short-editor-20261024.json: Brawl45.937335s and Team42.830920s, both service teardown failures=0, no compiler errors/crash. Actual authenticated allocation, remote movement, command and exact durable ACK remain within unchanged deadlines. Earlier failures and fullmatch/4–8peer/recovery/performance gaps remain. Reviewed startup fixture correction40d99ee5 only skips pre-physics publication and requires paused tick0; no gameplay assertions removed.

## 2026-10-24T03:41:57Z Initial geometry and publication reproductions pass

Native033851 on25d2fb86 finishes47/48PASS,1FAIL,0SKIP85.9s. Initial native keepsake shape/clearance/five-ray support passes at tick0 paused; all12local cases pass. Both original Store failure reproductions and new actual read-only-primary recovery test pass. Only NativeDraftSequence still expects old File.Replace sharing HRESULT0x80070020 for the Store locked step, while new MoveFileEx reports0x80070005; preserve failure and correct the test's API-specific error contract without weakening lock/data checks. Opened evidence: TestResults/editmode_20261024T033851Z.xml and BuildNotes/client-view/native-publication-repair-first-20261024.json. No build/readiness claim yet.

## 2026-10-24T03:45:40Z All current draft persistence cases pass

Native034318 on8b0b6e3d passes36/36,0SKIP in84.7s, no compiler errors/crash. Exact failed sequence now reaches and passes its retained direct IO control; original BOM/released-lock failures and readonly recovery pass. Native033851 already passes all12local cases including paused tick0 floor/clearance; subsequent changes are test-only error-contract and reports. Opened XML/receipt: BuildNotes/client-view/native-authoring-lock-contract-pass-20261024.json. Production helper and journal guarantees verified on this Windows Editor; not a general cross-platform/power-loss certification. Next frozen-source QA rebuild, official smoke and original equipment visual acceptance.

## 2026-10-24T04:03:19Z Tested Windows review build ready

Build 20261024T034544Z passes its gate in 237.3 seconds (build manifest: 236.3 seconds), exact source 9e013dc9e766a616ae1d45ed037cbbd863dbf89e, Unity 6000.6.4f1, non-development release-QA. Opened its build.json and gate log. Official workshop run 20261024T035028Z passes 5/5 in 60.7 seconds within the unchanged 120-second deadline, normal exit and zero reported console errors, exceptions and warnings. The exact draft, return state and Undo/Redo history are retained. Evidence: BuildNotes/client-view/official-short-editor-pass-20261024.json and its actual checks.json. No further Tools/kit changes were made.

The same build's supplemental normal-window original-equipment run 20261024T035159Z passes 5/5 with 13 matching-payload successful commands (nine collection, two crafting, two equip), unchanged draft and clean exit. Actual physics evidence reports initial native/visual player grounding and keepsake floor support without overlap. Opened BuildNotes/client-view/windows-original-equipment-pass-20261024.json and command receipts; root personally inspected all six official workshop and three equipment PNGs. Original character and models are visible. Held BAT/head occlusion, incomplete grip visibility, sparse rooms and all-state motion/ordinary-camera acceptance remain open. Independent review and updated gallery are in BuildNotes/equipment-polish/WINDOWS-ORIGINAL-REVIEW-20261024.md and ASSET-REVIEW.html.

Independent observer review pins exact process identities, script and output hashes, observes the official main window visible, and records nonzero measurement overhead. The 90-update interval takes 7.694559 seconds; this does not measure presented FPS or establish a controlled launcher/Unity speedup. Performance acceptance remains open. Runtime Workshop/Home stores have separate historical evidence; current Editor persistence success and fresh-primary Windows smoke do not certify repeated-save/restart recovery. Reviews: BuildNotes/client-view/OFFICIAL-OBSERVER-RESULTS-20261024.md and RUNTIME-SAVE-COVERAGE-20261024.md.

Updated TRY-WORDBOUND.md with the exact executable, controls, online and Unity editing instructions and known limits. Refreshed progress.md and STATUS.md; preserved their detailed earlier evidence in VERIFICATION-HISTORY-BEFORE-035028-20261024.md and STATUS-HISTORY-20261024.md. All 55 checked local links resolve and git diff --check passes. The plot/anticipated play/endings/ratings document remains complete as design; new endings and optional restoration are not implemented. Full online outcomes/maximum parties/network recovery, performance, visual polish, human usability and final stripped release remain open. Original and isolated uncommitted work are preserved; goal remains active.

## 2026-10-24T04:09:22Z All five modes have current two-client public entry receipts

Gate 040530 on 74f76f3be2b56a526cfaed4e70bdf72c2accafc7 passes 3/3, zero skipped, in 201.3 seconds, with no compiler errors or crash. Opened native-public-remaining-short-editor-20261024.json and actual XML: FFA 67.019067 seconds, Freestyle 62.400751 seconds and Time Run 33.552702 seconds. Each exact service teardown reports zero failures. Existing real allocation, authenticated Runners, canonical bodies, remote movement, command and durable acknowledgement checks remain unchanged. Together with race run 033100, all five modes now have bounded current entry evidence; earlier failures, full-party outcomes, separate Windows clients and recovery targets remain open. Review executable 034544 is preserved unchanged. Next prepared maximum-roster batch has not yet run.

## 2026-10-26T02:02:10Z Recovery and maximum-roster contract verification

After the credit interruption, inspected all seven worktrees and verified 632 dirty/untracked/authored files in the local recovery snapshot recorded in BuildNotes/experience-refresh/root.progress.md. Preserved the original font/Lunar work, unrelated online changes, the previous Windows review build and all isolated commits. Recorded failed capacity run 041032 before repair. The installed Fusion.Runtime.xml documents remote GetPlayerUserId access as server-only in Client/Server mode; the corrected fixture checks each client's exact native PlayerRef roster, the host's signed/authenticated identities, current production admission and all canonical seats. No runtime auth/config/deadline was changed.

R0 014332 passes in 44.7 seconds with the existing short-path editor selected through GSK_UNITY. The preceding R0 attempt reported missing editor discovery because that process variable was absent; no installer or permissions were needed. Native 014532 on c23590b7674dc244f7670ef7d115d7f78bfb8b44 finishes FAIL 1/3, zero skips, 697.1 seconds, no quit crash. FFA eight-player case passes in 128.264201 seconds with cleanup zero. Freestyle eight-player case reaches exact maximum signed/native/canonical roster and gameplay markers, but teardown fails with 24 failed service operations (return/cancel/leave eight each). Time Run fails public coordinator preparation with a sanitized HttpRequestException after creating one client and no party. Exact XML and receipt opened: BuildNotes/client-view/native-public-max-rosters-contract-20261026.json. Root does not claim the failures' network cause or capacity acceptance; online lane investigates before any retry.

New minimal shop browsing, native outfit portrait rendering source, authored concise story profiles/rubric and confirmed melee animation source are committed in isolated lanes and await integration/execution. Player-facing minimalism does not remove the words needed for the game's puzzles or optional readable help. Original-style geometry variants remain a concrete proposal, not generated assets. Production goal remains unfinished.

## 2026-10-26T02:34:00Z Experience refresh native slice

Integrated concise shop categories, original-avatar portrait rendering, editable story profiles and launch wiring, confirmed melee animation sampling, and the reviewed gameplay rubric. R0 and 58 selected core cases pass. Native EditMode 022355 passes 92/92 (61 presentation, 10 story plan, 13 profile, 7 readability, 1 persistence); PlayMode 022713 passes 58/58 shop/lobby/collection cases. Evidence and hashes: BuildNotes/experience-refresh/native-slice-20261026.json. The misspelled Input assembly omitted MenuGui; that fixture is still pending. Generated the actual default Story Profile via Unity. Added no-code editing steps to docs/wordbound/UNITY-EDITING-GUIDE.md.

Original avatar portrait lighting failed direct visual review despite successful import, so it is being corrected before acceptance. Built-player motion, current UI capture and story-view lifecycle remain open. Packing Club generator/spec is source-only; no new geometry or online skin synchronization is claimed. Current review Windows build 034544 is preserved. Operator SELECT-only evidence confirms abandoned Freestyle match CLOSED but eight test guest parties/memberships remain; source/TTL and operator evidence are preserved in experience-refresh. Production acceptance remains open.

## 2026-10-26T04:07:19.472303+00:00 Refined collection and integrated experience; native compiler wait

Integrated six confirmed own-team epilogues and editable copy, optional journal display, compact loadout navigation and a bounded real-attack frame capture. Combined R0 034211 passes in209.5seconds; the prior selected core/native results remain scoped to their earlier slices. Packing Club build-b produces editable source, eight passing export round-trips and sixteen matched renders. Root inspected BAT front/three-quarter and SHIELD front/three-quarter/side/rear; proceed to optional Unity comparison, with original runtime artwork preserved. Evidence: packing-club-build-b-20261026.json and PACKING-CLUB-REVIEW.html in BuildNotes/experience-refresh.

Portrait probe031649 times out after300seconds; retry034659 times out after600seconds while waiting on IL postprocessor connectivity. Neither reaches the diagnostic method or produces images. Observed CPU100%/4337MiB free and a Kestrel heartbeat warning do not establish causality. Four known owned build processes are confirmed gone after cleanup. Exact gate/hash receipt: BuildNotes/experience-refresh/native-experience-blocker-20261026.json. No new Windows build, native epilogue/loadout/motion pass or capacity retry is claimed. Original review build034544 and all prior failures remain intact. A bounded cleanup scheduling repair is separately authorized in the isolated online lane; no service writes or gameplay timeout changes are part of that work.

## 2026-10-26T04:25:03Z Recovery evidence and portrait draft preservation

Verified all 15 source, PNG and metadata files against the private portrait-draft-20261026T041709Z manifest and their current originals. Restored only ShopPreviewImporter's rejected lighting experiment to its committed baseline; generated draft portraits remain preserved and are not visually accepted. No original avatar material or palette was changed.

Corrected the two R3 log references and hashes in BuildNotes/experience-refresh/native-experience-blocker-20261026.json after independent review. The original logs are unchanged. BuildNotes/experience-refresh/ilpp26-diagnosis.md records the inspected compiler startup failure, uncertainty and retry criteria. Current machine sampling still reports 100% CPU; no native job or new Windows build is running.

Packing Club build-b already has eight verified exports and sixteen renders, recorded in packing-club-build-b-20261026.json. Updated progress.md to reflect this result and the implemented epilogue source without claiming native or owner acceptance. Online cleanup commit 96a334792759bb82869bc25cca23f94e0f36b7e3 is frozen in its isolated worktree and under integration review; its 15 fake tests are not yet run.

## 2026-10-26T04:41:55Z Integrate bounded online cleanup and pass source checks

Integrated cleanup as 0c083004 after independent source review and verification of the real return response. All 15 new fake tests compile in the whole-project Editor check; execution is NOT-RUN. R0 042624 passes in 196 seconds. See BuildNotes/experience-refresh/continuation-source-20261026.json for source hashes, scope and limits. The optional Packing Club importer selects verified build-b; native import remains pending. Original runtime assets, the tested 034544 Windows build, unrelated worktree dirt and historical failure evidence are preserved.

## 2026-10-26T04:45:38Z Compiler isolation result preserved

Gate043145 FAIL after605.7 seconds at its unchanged600s deadline; no portrait callback/images. Session-only Burst disable did not prevent ILPP connectivity waits. Restoring four exact owned helper processes from BelowNormal to Normal coincided with later compilation progress but did not complete the diagnostic. No cause or production fix is claimed. All owned Unity/compiler processes are gone; an unrelated reused PID was excluded and untouched. See BuildNotes/experience-refresh/portrait-compiler-isolation-20261026.json for13 verified artifacts, exact phases and limitations. No new Windows build or live capacity retry ran. Updated progress.md and root.progress.md with the remaining queue and current blocker.

## 2026-10-26T05:38:00Z Developer workspace source and preservation review

Extended the existing Unity workspace with Scenes/Design/Assets navigation, real Lobby/Shop/Loadout Play shortcuts, original-asset categories and independent saved material/prefab copies. Added missing-only equipment/character studio and runtime-connected lobby typography setup. Imported paths remain unchanged because they encode model identities and rig settings. Generated typography and studios are still pending native execution; runtime-built Shop/Loadout layout conversion remains explicit unfinished work.

Independent review corrected subasset browsing, active-scene isolation during copying, CharacterDefaults collision handling and preview recovery after editor-test interruption. Added source coverage for 20 workspace, 6 browser and 6 typography cases, not yet executed. R0 053133 passes Runtime and Editor in 36.6 seconds after correcting an obsolete Unity API and a separate missing-editor-path invocation. Exact source/metadata hashes and prior attempt details are recorded in BuildNotes/developer-workspace/source-20261026.json.

All 34 dirty/untracked files at the first source checkpoint were copied and SHA-256 verified in .harness/recovery/developer-workspace-2026-10-26T05-18-53-957Z. Rejected portrait drafts remain preserved. The owner explicitly kept other jobs running; native setup 053311 is serialized with a 900-second deadline and normal Burst configuration. Only verified owned toolchain descendants may change from BelowNormal to Normal. No unrelated process, original checkout, current release or public service was changed.

## 2026-10-26T06:03:49.943Z Saved Unity workspace native verification

Native setup054612 PASS74s and repeat054859 PASS46.4s create two studios,24 original-model prefab variants,three saved fonts,five material presets and a typography profile. All77 output files remain byte-identical after repeat and after tests. Native055552 passes36/36 focused cases, zero skipped; fixtures are WorkspaceEditor20, AssetBrowser6, Studio4 and Typography6. R0 055415 PASS15.2s.

The first native setup failure remains recorded: Unity disallowed a second untitled additive scene. Prefab copying now instantiates directly in a hidden preview scene and saves a variant; studio creation stages an owned saved scene and publishes only after success. The first test run passed35/36; the remaining exact Color assertion was replaced with Unity ColorEqualityComparer for serialized floating-point values. Runtime materials were unchanged. Native receipt: BuildNotes/developer-workspace/native-20261026.json.

Read-only audit confirmed original FBX links, scene camera/light composition, saved atlas/material references, no duplicate Assets GUIDs and no temporary studio leftovers. Actual visual review, real Play/Stop, Windows and online appearance remain separate. Shop layout proposals stay under BuildNotes/developer-workspace/shop-layout and are not integrated by this checkpoint. Other jobs stayed running.

## 2026-10-26T07:15:00Z Saved UI and authored-font preservation follow-up

The saved workspace/studios are committed at a351d799. PlayMode060642 passed59/59 functional cases but modified three saved TMP font assets. Their exact mutated bytes are retained in .harness/recovery/developer-workspace-2026-10-26T06-39-36-388Z; exact authored restoration is recorded in BuildNotes/developer-workspace/font-cache-restoration-20261026.json. Runtime ownership is being repaired, including saved Shop templates; functional tests alone do not establish asset preservation.

Shop065958 (TestResults/editmode_20261026T065958Z.xml) passed7/12. The render-target creation repair allowed exact pixels to be checked. Four stale Sprite-wrapper unload assertions and one material managed-wrapper identity assertion failed; the independent lane is strengthening persisted identity and disk-vs-cache checks. Native062041 passed143/144; WordboundStoryEpilogueTests now preserves rejection of the contradictory timeout snapshot and constructs valid non-actionable recovery state. R0 065657 passed before the new typography work; both revised native slices remain pending.

The five portrait controls are copied into Editor/Art with their draft hashes unchanged. They have not yet run. Other user jobs remain running. Root serializes this project's heavy gates, preserves source snapshots, and keeps Windows/public-match acceptance open.

## 2026-10-26T08:08:58Z Shop persistence and runtime font ownership pass

Native073048 passes all12 Shop persistence cases; its6 font failures exposed Unity's non-readable static fallback atlas. After exact-format owned GPU copy repair, native075254 passes8/8 ownership cases, zero skipped, including three-mip Alpha8 GPU bytes before/after owned disposal. R0 074834 passes. The original77 authored outputs still match their pre-Play baseline. Full Play lifecycle and live saved-Shop integration are not yet verified.

Evidence: TestResults/editmode_20261026T073048Z.xml, TestResults/editmode_20261026T075254Z.xml and BuildNotes/developer-workspace/continuation-20261026.json. Recovery snapshot .harness/recovery/developer-workspace-2026-10-26T07-54-41-050Z preserves108files. Root started explicit saved-Shop setup080701; camera and Loadout drafts remain outsideAssets. All15 cleanup cases passed in062041; live capacity retries and historical service cleanup remain pending.


## 2026-10-26T08:37:03Z Saved Shop and ending recovery checkpoint

Setup080702 and repeat081136 pass; all39 Shop outputs are identical after repeat and Play. Play081310 passes61/61 and retains all77 existing workspace bytes relative to the exact post-Shop-creation baseline. Creation initially normalized4 derived ratio fields in2 generated font assets; font-derived-ratios.review.md distinguishes this from authored style loss and retains both versions. New font creation now initializes those values before saving; R0 082836 and all6 typography cases083206 pass. Existing valid assets remain verification-only.

Native081750 passes144/144 presentation/story/profile/menu/cleanup cases, including22 epilogues and15 transport-cleanup cases. Portrait controls082029 pass with original sources/global state unchanged and no remaining owned objects. All5 images were inspected: A/B/D retain a dark face; C/E show cream albedo. Front-light rotation affects clothing but not sampled face pixels; a rear-key diagnostic remains a separate prepared draft. These are test/source checkpoints, not Windows or full public-match acceptance. See BuildNotes/developer-workspace/continuation-20261026.json for logs/XML hashes.

Checkpoint scope remains saved Shop authoring, typography ownership and the tested epilogue fixture; camera/Loadout proposals and rejected portrait drafts are preserved separately for subsequent work.

## 2026-10-26T09:24:00Z Saved Loadout and lobby camera

Saved LoadoutPage/RecipeTile prefabs and profiles now drive the actual page, with Inspector editing links, binding validation and a separate saved lobby framing profile. Workspace setup is missing-only and publishes valid profiles after their dependencies. Editor090419 passes16/16; setup090949 and repeat091403 pass; Play091621 passes82/82, zero skipped. All125 existing/new authored outputs remain byte-identical through repeat and Play. The combined suite includes exact screenshot preference restoration across completion, exception, disposal and teardown. Evidence: `BuildNotes/developer-workspace/loadout-camera-20261026.json` and `loadout-output-preservation-20261026.json`; instructions: `docs/wordbound/LOBBY-EDITING-GUIDE.md`.

Relevant rules090104 pass41/41. Broad core084834 was explicitly cancelled after unintentionally including long map seed sweeps outside this UI scope; its FAIL and cancellation record remain preserved, with no seed-sweep pass claimed. Source check090306 passes after the diagnostic mask cast, Inspector and capture-cleanup changes. Other user jobs remained running. Interactive editor cycles, portrait correction, current Windows build and full online acceptance remain open; the Home draft is isolated outside Assets.

## 2026-10-26T09:37:29Z Portrait startup verification deferred

Portrait control attempts092340 and093055 failed before the diagnostic: first during UDS shared-memory/lock initialization, then the unchanged retry exited1 after a Data Store initialization timeout. Only the exact owned failed Unity subtree was stopped; caches, shared services and other user jobs were not changed. Capture02 was not created. The successful16 Editor/82 Play cases and125 authored-file preservation checks remain separate evidence. No new Windows build is claimed. See BuildNotes/developer-workspace/portrait-startup-recovery.md and loadout-camera-20261026.json. Home and brow-control drafts remain outside Assets for later review/execution.

## 2026-10-26T10:43:00Z Restart recovery, saved Home and asset intake

After the owner rebooted, the pinned editor completed three versioned portrait controls. Capture04 identifies imported head normals opposing outward face geometry; an owned recalculated copy restores face lighting while preserving all original source/mesh/global data. The runtime correction and animation review remain pending. Evidence: BuildNotes/developer-workspace/portrait-controls/POST-RESTART-REVIEW.md and capture04/diagnostics.json.

Saved Home prefab/profile and Workspace Design shortcuts are integrated. The reviewed draft first required two escaped-character syntax repairs; its original patch is preserved. R0 passes, Home Editor102944 passes5/5, setup103708/repeat103916 pass, Play104015 passes70/70 with no skips. All146 authored output files remain unchanged across repeat/Play. Exact sources, gates and limits: BuildNotes/developer-workspace/home-recovery-20261026.json. Editing: docs/wordbound/LOBBY-EDITING-GUIDE.md.

Supplied Kenney/KayKit intake verified193 distinct CC0 furniture designs and preserved1985 source files. Eight original FBXs/license are staged in ArtSource/Collections/HouseholdExpansionV1. FANV2 has editable Blender source, both FBX/GLB reimport receipts and three matched renders; independent review requires grille/finish/rig/LOD work before art acceptance. Evidence: BuildNotes/asset-expansion/local-intake.md, household-staging.md and fan-v2/first-review.md. Story implementation status was reconciled without claiming optional restoration, new playable rooms or current Windows acceptance. Original assets, other branches and running jobs remain untouched.

## 2026-10-26T11:27:00Z Saved Household studio and first avatar surface validation

Household importer110408 passes4/4 after preserving the unsaved-scene failure and resolving the measured10x FBX/GLB unit mismatch with explicit.1 import scale. Setup111505 and repeat111943 pass;8categorizedprefabs,43primaryassets/102files saved;275source/authoredfiles preserved. Capture112050 passes with4rear-biasedviews,272file/23material/globalpreservation andzeroownedpreviewobjects. Root inspectedall4; surfacesremainbasicandfrontviewsarepending. Evidence:BuildNotes/asset-expansion/household-native-20261026.json.

Avatar setup112202 saves a derived head mesh/profile without changing originalFBX. NativeEditor112311 passes10/10 including27pose/expressioncombinations andsetup/source/fontpreservation; Play112517 passes1/1 authoredPlayerAppearancecase. Root then found a realcallerwrapper mismatch in theWindowsQAheadallowance; repair, Editorstale-sourceguards, actualportraits/currentWindowsverificationremainpending. Evidence:BuildNotes/asset-expansion/avatar-native-stage1-20261026.json. No production/owner-art-acceptance claim.

## 2026-10-26T11:57:00Z Avatar correction and current presentation regression

The exact saved head correction is now verified through the real Wordbound artwork wrapper. Editor source validation supports an explicit disabled setting for new/raw spawns while preserving existing renderer overrides. R0 114237 passes; native Editor 114449 passes all 79 character, source-validation, animation-binding and presentation cases; Play 114921 passes all 102 appearance, animation, lobby, Shop, Loadout, Home, camera and cleanup cases with zero skips. Existing 146 authored files remain unchanged.

Portrait rebuild 114818 passes with six exact corrected heads, stable image GUIDs and 256x320 imported dimensions. All old PNGs and metadata are backed up; source/memory/global preservation passes. Root inspected every before/after pair: face lighting is corrected, but clothing remains muted under the default preview light colors. A neutral-light comparison is pending; this is not final art acceptance. Exact gates, source hashes and limits: BuildNotes/asset-expansion/avatar-integrated-20261026.json.

## 2026-10-26T12:21:00Z Transparent portraits and construction checks after restart

Native portrait comparison 120957 passes with three matched lighting controls and native RGBA readback. Every alpha/transfer check passes (maximum RGB channel delta 1), sources/current portraits/global state are preserved and owned previews are cleaned. A-current PNG exactly matches the installed courier hash. Softer comparison 121611 also passes for both controls; root inspected all ten diagnostic PNGs and selected D (.8 key/.4 fill) for preserved cream-face shading and clearer mint clothing. Production adoption and actual shop composite remain pending. Evidence: BuildNotes/developer-workspace/shop-portraits-corrected-lighting/20261026T121120648Z-cceec7e1/receipt.json and 20261026T121705173Z-54f5a1c3/receipt.json. R0 121809 and focused core121256 pass (41/41).

The FAN V3 build-01 probe fails tilt clearance at +6/+8/+10 degrees (92/232/216 intersecting triangle pairs), despite passing neutral blade/hub separation. Its diagnostic-only blend and FAIL receipt are preserved; no candidate exports or runtime adoption occurred. Original V2 sources remain unchanged. Blender's process exit0 did not override its Python exception/FAIL receipt. Evidence: ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-01/clearance-probe.json and build-receipt.json.

The all-mode scope audit confirms five additional original house modes are still local. They are now explicit unfinished progress items rather than counted as the five online Wordbound modes. Root corrected the lobby explanation and existing wording assertion while preserving the blocked unsupported route; final capture/test remains pending. All previous work is retained in the verified111-file snapshot .harness/recovery/developer-workspace-2026-10-26T12-18-04-710Z. No production-ready or owner visual-approval claim.

## 2026-10-26T12:28:00Z Six transparent shop portraits accepted for Windows review

Rebuild122319 PASS58.5s publishes all six selected-D native RGBA portraits with stable GUIDs, exact256x320 uncompressed sRGB imports and alpha verification (max RGB transfer delta1). All392 source/file proof records,92 memory objects and global state remain preserved; every owned preview is cleaned. Final capture122426 PASS2/2 in79.9s covers the19-image menu capture and corrected local-versus-online wording contract. Root and independent reviewer inspected all six new portraits and the actual Shop; cream rectangles are removed, face/color shading is balanced and no obvious halo or stretching appears at1600x900. Root also checked the new LOCAL PARTY heading/text fits. James's art approval and Windows evidence remain separate.

All146 existing authored files and the separate275-file Household set remain unchanged. Exact receipts and source/image hashes: BuildNotes/developer-workspace/shop-transparent-20261026.json, shop-portraits-corrected/production-acceptance-20261026.md and BuildNotes/asset-expansion/household-preservation-2026-10-26T12-26-58-048Z.json. Next: commit the verified presentation slice, then build the current release-qa Windows player and run its three exact smoke scenarios. The failed FAN tilt probe and all original-house online scope work remain open.


## 2026-10-26T12:57:24Z Public capacity retries and current Windows evidence

Public TimeRun4 native124741 PASS1/1 in96.4s; Freestyle8 native125219 PASS1/1 in157.3s. Both use fresh isolated public pools, exact authenticated signed/native/canonical rosters, real gameplay smoke, first-attempt service requests and clean teardown. TimeRun has9stages/22requests; Freestyle24/56; zero failures. Raw private selectors remain in ignored logs/XML. Sanitized hashes/counts/scope: BuildNotes/client-view/native-public-capacity-retries-20261026.json. Full matches, separate Windows clients, recovery and old failed-run residue remain open.

Windows build122831 on3cec5407 passes163.8s. Frontend123152 PASS3/3; workshop123303 FAIL90s while unfocused, then unchanged-build profiled123805 PASS5/5 and plain124326 PASS5/5. Source audits found no concrete simulation/presentation regression; matching content and changing runtime conditions prevent a causal performance claim. Equipment124029 PASS6/6 with real BAT receipt/39canonical callbacks/8phase images, but root and independent review found vertical readback inversion in all motion images. Normal captures are upright. Source draft4b6fa402 adds same-frame measured orientation and a natural settle frame before Attack; R0125530 passes and focused native tests125627 are active. Initial failed/inverted evidence preserved in BuildNotes/client-view/windows-current-stage1-20261026.json. No production-ready claim.



## 2026-10-26T12:59:39Z Measured motion export orientation ready for Windows

R0 125530 PASS; native Editor125627 PASS5/5, zero skipped, covering identity/vertical alignment, odd middle-row preservation, symmetric ambiguity, RGB mismatch and invalid input rejection. The QA recorder measures a same-frame upright reference before Attack, lets one ordinary frame advance, then preserves its bounded actual phase capture and exports measured orientation afterward. Native Windows calibration remains pending. Evidence: BuildNotes/restart-20261026/motion-orientation-native-20261026.json. Existing inverted PNGs and timing failure are preserved.

## 2026-10-26T13:15:00Z Windows baseline and character/UI correction scope

Windows build20261026T125941Z (source78ac4514810e7267887e2ca7fa549f427d3474de) passes Home3/3, Workshop5/5 and equipment6/6 with normal exit, no timeout and clean reported console. Same-frame motion calibration measures exact vertical row match, one ordinary settle frame,39 canonical callbacks/0 missed and eight phases. Evidence: BuildNotes/client-view/windows-reviewed-baseline-20261026.json. Root inspected Home, gameplay, equipment menu and multiple upright action frames; independent motion review inspected all8 and BAT/HAMMER stills. This is a functional baseline, not visual acceptance.

Owner now explicitly requests substantial held-item, action/facial and excessive-text corrections. Three disjoint draft lanes in BuildNotes/character-experience-20261026 are active. Initial review identifies discarded authored grip rotation and absent Wordbound facial state driving; neither is yet repaired in this build. progress.md and delivery notes identify this boundary. Earlier workshop timeout and original sources remain preserved.

Reviewed FANV3 build-02 was executed serially with Blender --python-exit-code1. It fails LOD bounds tolerance (0.000531420111656189m versus0.00001m) after all11 tilt samples show zero surface overlap. Source-preservation PASS. The failed receipt and outputs remain at ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV3/build-02; no Unity admission or completed art claim.

## 2026-10-26T13:39:03Z Character and HUD correction first native pass

Full authored socket rotation is now respected for Wordbound BAT/HAMMER; saved Presentation holding settings preserve hand contact, original models and combat timing. Shared original-rig facial driver/profile is integrated into local and Wordbound paths with state priority/expiry/reset. Gameplay HUD is compact, context-first, uses the editable Interface theme and optional Help; persistent recovery notices stay visible while action refusals expire. Workspace exposes the saved expression profile.

R0 133440 PASS, core13252441/41, Editor132959102/102, Play13312417/17 (zero skips). Native setup132613/132815 saved profiles. Facecapture133514 produces10 actual crops,322 inputs preserved, recorded scenes/globals unchanged, owned previews released. Root inspected five fronts: effort/hurt distinct, idle still stern and recovery similar; native tuning comparison is next. Functional/source passes do not imply art acceptance or Windows delivery. Evidence: BuildNotes/character-experience-20261026/native-20261026.json. Initial R0132347 namespace failure remains preserved; setup uses EditorTools after correction.

Timestamp correction: the preceding entry's manually supplied13:15:00Z heading was approximately one minute ahead of its actual13:14Z append/commit. Subsequent machine evidence retains its exact returned UTC timestamps.

## 2026-10-26T13:55:28Z Character comparison and intermediate Windows evidence

Windows133920 on1b15272e passes equipment134114 six steps and Workshop134216 five steps, normal exits and clean reported console. Root and independent review see improved upright BAT/HAMMER placement and reduced gameplay panels. All8 BAT phases preserve contact; windup still occludes the weapon from this view and does not prove clearance. Equipment menu remains too text-heavy; bounded original-icon recipe grid is in progress.

R0134745 PASS; matched facial exec134831 PASS. All5 native controls have exact target weights, identical camera/scale and322 input proofs preserved. Root and independent reviewer select Idle75/0 and Hurt18/60; saved profile not yet changed. Earlier55/0 and4/38 controls remain intact. Evidence: BuildNotes/character-experience-20261026/comparison-20261026.json. No final visual/production claim. Recovery snapshot13-54-45-530Z verifies346 files.

## 2026-10-26T14:42:18.520Z Tuned expressions, action mapping and visual recipe grid

Saved Idle75/0 and Hurt18/60 through guarded Unity authoring; repeat is byte-identical and does not save. Actual local spelling/craft and confirmed Wordbound craft/furniture drive facial state; living confirmed battle winners play the existing Celebrate clip. Original-art recipe tiles replace the long list, with one selected detail and separately guarded deferred Craft. Axis transitions now respond immediately. Native Editor142701 passes80/80, Play143003 passes19/19 and UI/input143633 passes52/52, zero skipped. R0143805 passes. Evidence: BuildNotes/character-experience-20261026/final-native-20261026.json and tuned-expressions-20261026.json.

Holding diagnostic failures141517 and142324 remain preserved. Read-only mesh access and explicit owned tactile release corrected the first; compensated BakeMesh coordinates with a paired independent world mapping and per-render skin refresh corrected the second. Native143836 passes18views and preservation/cleanup checks without relaxed thresholds. Root inspected six images with synchronized mittens and weapons; this is sampled visual evidence, not collision or owner approval. Final Windows build/smokes remain pending. Recovery snapshot14-40-39-651Z verifies514 files, including earlier uncommitted drafts.

## 2026-10-26T14:53:03.913Z Windows recipe QA correction and vivid icons

Build144235 passed, but equipment144544 failed an introduced QA comparison of two defensive PlayerInventory copies. This always-false reference assertion is corrected to retained snapshot identity plus full canonical content/letter preservation; named evidence separates command/menu/state/restore failures. No gameplay authority or test timeout changed. Workshop144717 passes5/5 on that build and root inspected its actual visual grid/world. Unavailable recipes had washed-out art; removing only the alpha multiplier keeps original colors while existing Need letters/disabled Craft conveys availability. R0145113 and UI14514752/52 pass; rebuilt Windows remains pending. Evidence: BuildNotes/character-experience-20261026/recipe-followup-native-20261026.json. Original failed Windows evidence is preserved.

## 2026-10-26T15:00:04.714Z Combined character/UI Windows review build

Build20261026T145311Z onbe648835b38f00cb38f47e76bb8de5bd14b49674 passes equipment6/6, Workshop5/5 and Home3/3 with clean reported console and normal exits. Root inspects actual equipment/world/menu pixels; independent review covers holding and UI. Positive grid Craft receives exactly one accepted command; stale letter/readiness/focus actions submit nothing, and temporary QA fields restore. Earlier failed build/test evidence remains intact. Evidence: BuildNotes/client-view/windows-character-ui-20261026.json. Delivery notes and current progress now identify this executable; full online, all-action/all-equipment, human visual/no-code usability, performance and stripped-release acceptance remain open.

## 2026-10-26T16:10:00Z Lobby organization and settings refinement

Separated Local/Online/Create with accurate human/bot summaries and original art; shortened cards, clarified Home/Customize/Shop/local-party labels, removed routine greeting, and added responsive navigation. Settings now groups actual options and preserves scroll/focus; pending display confirmation reserves space on all tabs. Native Editor155841 passes12/12; Home save160028 changes onlythree texts and repeat160158 makes no save. Play160302 passes90/91; the intentionally reduced card-height expectation is corrected and targeted160635 passes2/2 including12 settings captures. Core160630 passes41/41. All276 authored files match after repeat and Play. Failure evidence and imported-font preservation corrections are retained in BuildNotes/lobby-experience-20261026. Windows build/walkthrough remains pending; original house modes remain local.


## 2026-10-26T16:23:00Z Visible settings scrolling

The first Windows lobby walkthrough passed all five stages with 15 actual captures. Independent visual review found weak scroll discoverability. Settings now provides a small teal scrollbar beside overflowing lists, without more instructional text. Native Play 161936 passes all 21 focused cases; capture 162055 passes 2/2, including all 12 normal/pending settings views. All 276 authored outputs remain unchanged. Final Windows QA additionally reaches the last Controls bindings and requests a 16th capture.


## 2026-10-27 Final lobby screenshot corrections

Windows walkthrough 162634 passes 5/5 with 16 captures. Independent review finds a truncated Random house placeholder and stale map-change toast; the card now uses the shared poster layout and map changes update their existing selection/footer without a redundant toast. R0, core 41/41 and the full lobby Play suite 91/91 pass on this correction. All 276 saved authored files match the post-Home baseline. Final Windows rebuild follows this source checkpoint.


## 2026-10-27 Lobby Windows delivery

Build 20261026T164019Z uses source abeba5411e4f087a372ea57371edd7354157c1d2; final lobby walkthrough 20261026T164249Z-lobby_experience passes 5/5 with 16 real Windows captures. Full native lobby91/91, core41/41 and prior Home12/12 pass. The corrected Random house card and Home notice cleanup are independently reviewed. All 276 authored files are preserved. Try guide, before/after viewer, progress and Unity editing notes now identify this exact build. Online navigation only is checked here; full remote matches, original-house online support, physical input/editor acceptance and production release remain open. Existing character/art drafts and older failures remain preserved.

## 2026-10-26T17:46:00Z Fenced writer completion and preserved native failures

The new engine-free writer lease completes pure renewal I/O without Unity's synchronization context, while main-thread Poll retains scheduling. Immutable writer identity, local expiry, cancellation/retirement and monotonic admission/expiry merges remain enforced. Independent source review is in BuildNotes/completion-20261027/network-draft/lease-completion/review.md. R0 172230 passes; focused core172353 passes222/222 including18 new lease and10 checkpoint-service cases. A separately reviewed recovery fixture maps remote identities only on the elected server and retains exact native/canonical survivors. Unity generated both new metadata files.

Native165734 fails at writer lease expiry before the scored FFA body; cleanup succeeds. Native172541 then fails its600s timeout after a521.620s update gap, with six failed cleanup stages. Windows Modern Standby events confirm17:29:02→17:37:37UTC; evidence and hashes are in ffa-suspended-172541.json. Native acceptance and the scored finish remain pending. No service TTL, deadline, score, authority rule or machine power policy changed. Snapshot17:45:27 verifies828 modified/untracked files. Exact abandoned remote state is being inspected before a retry; the existing verified Windows lobby build remains the delivery.

## 2026-10-26T18:08:00Z Playable creator starter and uninterrupted FFA evidence

The Workshop Race Circuit now starts in exact TimeRun with three physical STOOL sockets, renewable letters, ordered checkpoints and a keepsake delivery in an editable room. Original CourseTemplates/public content and existing autosaves remain unchanged. Source review is in BuildNotes/completion-20261027/workshop-draft/review.md. R0 180525 passes, core180207 passes117/117 and Editor180545 passes133/133 with zero skips. The additive actual local launcher/three-lap/return fixture compiles but has not run; its scripted movement does not establish physical keyboard/controller acceptance. Snapshot18:07:16 preserves865 files.

The interrupted FFA's exact remote state was inspected with SELECT only: both returned seats, CLOSED match/no tickets, two retained party memberships. Fresh FFA175059 reaches real combat with no session fault and zero cleanup failures, but its480s fixture budget expires before the complete result. Existing bounds remain intact; observed durable-save stalls are under investigation. See ffa-172541-remote-readback.json and ffa-full-match-175059.json. No new player build or overall multiplayer acceptance is claimed.

## 2026-10-26T18:13:00Z Real starter completion and editable return

Native180952 passes1/1 in146.2s, completing the exact fresh Workshop RaceCircuit through three physical STOOL puzzle laps. It collects15 fresh renewable letter entities, crafts/deploys/consumes three original stools, observes native socket holds and three keepsake deliveries, confirms a Win, then calls production ReturnToWorkshop. Exact saved draft, same history, real Undo/Redo and zero-failure cleanup all pass. The controlled movement delegate and public command route do not prove physical keyboard/controller input, Windows rendering or multiplayer gameplay. Exact source/gate hashes and observations are in BuildNotes/completion-20261027/workshop-starter-verification.json. A separately reviewed failure-path cleanup split remains a follow-up; no acceptance condition was weakened.

## 2026-10-26T18:46:00Z Online shell and cadence checkpoint

Integrated reviewed online-shell revision3 and checkpoint-cadence revision2. R0 182632 and183303 pass; focused rules182017 pass222/222; native182759 passes51/51 including cadence, stop/fault, party input/focus, GUI and theme cases. Capture183533 passes with16 actual GameView frames, exact source/theme/font preservation and no cleanup errors. Responses are in-memory synthetic, so this does not establish physical input or online-match behavior. Root viewed all16 frames: Cancel and Retry remain readable; Create is still dense at854x480 and needs further simplification. Exact hashes are in BuildNotes/completion-20261027/online-shell-verification.json.

Successful checkpoint starts use the existing configured interval without adding an extra delay after ACK; failure backoff, one in flight and current-runner authority checks remain. Full public FFA184207 is running with unchanged limits; no new match or performance PASS is claimed. The strengthened starter fixture compiles but needs its full traversal rerun. FAN05 passes all four exports/reimports and geometric source checks;16 rendered comparisons expose LOD shading defects, so build06 source preserves eight parts under the unchanged65% limit. No new model is admitted to Unity. Snapshot18:45:13 hash-verifies1034 modified/untracked files. Latest Windows delivery remains164019Z.

## 2026-10-26T18:51:00Z Preserve full FFA failure and grip admission finding

Online shell/cadence source is committed as cd716e6a. Full public FFA184207 fails502.4s after an unchanged35.012s contact-return deadline, not the global lifetime; zero sessionfaults and cleanup failures. ffa-full-match-184207.json pins the exact native log/XML and metrics. Reduced ACK-to-next scheduling gap does not close full-match/performance acceptance. A source-only trajectory/timing review follows; no automatic unchanged repeat or relaxed bound.

Original FAN source/catalogue grip differs from inherited pilot identity marker. Build06's LOD source review remains valid but its native admission is held for a fresh original-contact correction. No live art override. Strengthened starter185112 is the sole running native job; ordinary Windows and owner visual acceptance remain open.

## 2026-10-26T18:57:00Z Strengthened starter verified

Native185112 passes the exact three-lap creator fixture1/1 in154.1s with zero skips/cleanup failures. Revision2 requires the deployed collider to be active/enabled and separates input restoration from observer removal on cleanup. Three genuine STOOL crafts/socket holds/consumptions and keepsake deliveries lead to a Win, followed by same saved draft and Undo/Redo through production ReturnToWorkshop. The cadence source is included. Evidence: BuildNotes/completion-20261027/workshop-starter-revision2-verification.json. Physical devices, Windows visuals and multiplayer remain separate.

## 2026-10-26T19:01:00Z Direct offline Wordbound entry verified

Play/Local now separates original House games from Wordbound solo using original item art. TimeRun and Freestyle use the installed flagship and saved presentation/camera/story/UI profiles; launch consumes its intent before online configuration. It preserves house selections and guards extra local seats. Return stops the local session then restores Hub/solo. R0 185433 and core117/117 pass; lobby185537 passes7/7; native185743 passes3/3 with no cleanup errors. Both modes start with missing online trust/snapshot/pool configuration, preserve56 dependency files and return normally; the fixture also checks actual safe runtime fault and missing-body preflight return. Exact hashes and limits are in solo-entry-verification.json. Physical input, rendered layout and Windows acceptance remain pending. Workshop aliases are preserved.

## 2026-10-26T19:50:00Z Compact online shell and safe failure return

Focused192038 passes53/53 with original assertions after expanded forms retain full-height scrolling. Actual GameView capture193149 passes21 states/cleanup0, including long-scrolled-form to Retry and larger-text Retry. Root opened Create, Join, party, private, search and failure images: default854x480 Create/Back fit; error and Retry are immediately visible, larger-text secondary controls retain vertical scroll. Earlier190656 hidden-Retry images and191259 focus failure remain preserved. These coordinator responses are synthetic; no online match or physical device acceptance is implied.

Startup failure now has deferred mouse, Escape and controller Back through existing input ownership/lease/focus guards. Native194030 initially failed1/6 because its EditMode test yielded without requesting engine frames. The test-only correction uses the established QueuePlayerLoopUpdate pattern, asserts the immediate lease quiet interval and its actual frame expiry, and writes no production/private state. R0 194205 and native194330 pass6/6. Combined194544 passes all3 solo launch/fault/return cases plus the actual three-lap starter; cleanup0. New QA sources compile with owned nested cleanup and internal shutdown margin; Windows scenarios remain unrun.

FAN07 native build passes original-contact selection, four exchange reimports, clearance and source preservation; independent readback pins69 unchanged inputs and8775/15032 LOD triangles. Matched rendering is the current sole native job, launched19:48:41. No Unity artwork override or new Windows delivery yet. Original art/defaults and all other jobs remain unchanged. Source recovery snapshot19:38:28 verifies1205files.

## 2026-10-26T20:18:00Z Direct local creator verified

Create now separates Home & tutorial from Wordbound maps. The local editor consumes its one-shot intent before online configuration, retains the assigned course/autosave route, and gives Back to lobby. Existing online Workshop hosting and original home tools remain. Local playtest uses one seat while retaining the full local party for return. The guide now identifies saved prefab/profile controls and still-generated UI.

R0 201421 passes. Lobby201009 passes9/9; creator201550 passes3/3 with zero cleanup errors. It exercises actual edited Single playtest, same History Undo/Redo, exact saved close, two-seat/non-default courtyard preservation, safe invalid-course return and explicit unfocused/focused recovery handlers. Two prior failures remain: UI200347 selected unsupported home-builder flat before its family action; native201133 recovery return timed out after an unprocessed queued decision (background focus is a hypothesis, not recorded at that point). Fixture-only corrections use a supported non-default map with preconditions and explicitly validate focus branches. Runtime/deadlines unchanged. local-creator-verification.json pins exact sources/results/failed receipts. Physical focus and Windows acceptance remain open.

Windows QA adds one creator-selection screenshot (20 lobby frames total), retains the20s Create step with15s owned work/cleanup, and never opens the user's editor/autosaves. New solo9-frame scenario remains pending player execution. FAN07 build/render complete code0;16 matched views permit optional Unity intake, with a small close-view LOD motor crease. Original equipment stays active; optional importer remains a source draft.

## 2026-10-26T20:51:25Z Compact Solo/Create panels and candidate evidence

Compact parent-size observation passes native204939 10/10 after preserving first204407 9/10 resize failure. R0204844 passes. Exact source and results: BuildNotes/client-view/windows-202047-compact-play/native-verification.json. Failed Windows202047 receipt preserves build/lobby PASS, Freestyle readiness FAIL and4190 authored-file preservation; no delivery promotion. Mode-aware readiness patch source-reviewed and compiled, still requires fresh Windows. Borrowed Solo controls correction remains separate draft. Original assets and other jobs preserved.

Next: finish borrowed-input source review, run native controls/entry regressions, then build and inspect a fresh Windows candidate.

## 2026-10-26T21:03:08Z Exact local controls and minimal hints

Fixed-device borrowed input adapters preserve the original Session binding/map/device while adding Wordbound menu, aim, held attack and equipment cycling. HUD uses real remapped labels or concise generic actions. R0210044 and native210101 pass51/51 with six zero-failure scene cleanups. First48/51 fixture failure and missing QA InputSystem compile reference remain preserved in BuildNotes/client-view/solo-input-hints/native-verification.json. Destruction-handler tests are distinct from fresh Windows actual teardown assertions, which now read disposed state and preserve caller map. Next: commit source, baseline4190 authored inputs, build exact Windows and run both scenarios.

## 2026-10-26T21:20:24Z Windows lobby and Solo delivery

Exact source0ba2309a builds210349 PASS108.1s. Lobby210547 passes5/5 with20 actual captures; Solo210721 passes3/3 with9captures, both DesktopBinding wrappers disposed through actual RequestReturn/Hub and original maps enabled. Console0/0/0 in both. All4190 authored inputs preserved. Reviewed delivery verifier passes; independent review openedall29 with no new primary text/button layout blocker. Evidence: BuildNotes/client-view/windows-lobby-solo-20261027.json, windows-210349-authored-preservation.json and windows-210349-visual-review.md/json. TRY-WORDBOUND and controls/editing guide updated. Remaining: partly hidden lobby avatar, sparse rooms, mode-specific control guidance, editable Play/Settings frames, physical-device/performance/full-match/art/release acceptance.

## 2026-10-26T22:07:30Z Saved lobby frames and clear control families

Saved Play and Settings prefab/profile frames now have Workspace edit and real preview routes. The missing-only authoring setup preserves existing edits. Controls distinguish House and Wordbound with a persistent heading and accurate shared keyboard/fixed controller guidance. Compact Solo/Maps use existing left-side character framing and restore centred House/Home views.

R0 220330 passes. Editor 214606 passes 27/27; setup 214714 and repeat 214750 pass; Play 220430 passes 31/31, zero skips. All 4,208 recorded authored files remain unchanged after setup (4,190 pre-existing files plus 18 new prefab/profile/graphic files and metadata). Exact sources, native results and preserved failures: BuildNotes/client-view/lobby-frames-verification-20261027.json. Earlier fixture issues and the measured nested-layout settling correction remain recorded; no bounds or text assertions were removed.

Next: commit this verified source, build Windows, run the updated 22-image lobby and 9-image Solo walkthroughs, inspect actual images, and update the user guides. Physical controls, complete public matches, broader art/story and production release remain open.


## 2026-10-26T22:35:00Z Windows lobby frames delivery

Source c4810d6a builds 221541 PASS. Lobby 221653 passes 5/5 with 22 actual captures; Solo 221852 passes 3/3 with nine. Both normal exits and console 0/0/0; all 4,208 authored inputs preserved against the fresh exact-source prebuild baseline. Strict verifier revision4 passes: BuildNotes/client-view/windows-lobby-frames-20261027.json. Independent visual review opened all 31 images; compact avatar composition and mode-specific controls are clear. The observed trial-version overlay and sparse room/checkpoint polish remain open. Earlier 220835 game smokes passed but delivery provenance was rejected because the baseline preceded the source commit; that rejection and its unchanged evidence remain saved.

Updated TRY, controls/editing guides, before/after viewer and consolidated progress checklist. The entire previous progress file is archived intact. No public-match, physical-device, full artwork or production-release acceptance is inferred.

## 2026-10-26T23:01:00Z Full public FFA scored-result pass

Added bounded host-owned contact observations to the unchanged full public FFA test. R0 224216 passes; focused core 224311 passes 222/222. Native 224529 passes 1/1 in 456.9 gate seconds, zero skips, normal exit. Independent raw-log/XML audit confirms all 40 contact returns, two real hammer crafts, ten knockouts, nine respawns, protected-hit/replay checks, both peers' Win and the exact independently downloaded durable checkpoint ACK. Cleanup reports zero return/cancel/leave/dispose failures for two clients and two parties.

The four test files, including Unity-generated metadata, are saved in 547f258f. No production gameplay change was made by this diagnostic. All 111 contact records have valid host facts; durable-window pauses still occur and recover in this run. Earlier failures remain unchanged. Exact evidence and timing limits: BuildNotes/completion-20261027/network-draft/durability-flow/final-224529.md and .json. This is two authenticated scripted clients in one Editor process; remote-attacker, eight-player, timeout/draw, separate-PC, recovery and other full modes remain open. The tested Windows UI delivery remains 221541.

## 2026-10-26T23:43:24Z Workshop ranges and current Windows review delivery

Existing random zones now stage X/Z/world-height bounds, quarter-turn choices and floor permissions until Apply; Cancel restores saved fields and an unchanged Apply preserves Redo. Permission metadata retains the owning room; it does not add random cross-floor spawning. Source359047ca passes core230139 89/89 and Editor230329 35/35. Invalid syntax/range order/choices are rejected before history mutation; room-fit and clearance still belong to Preview validation.

QA capture sourcefe7f1738 adds two actual range views with exact view/history/snapshot/revision/Undo/primary/manifest preservation. Intermediate Windows231729 passes behavior but exposes clipped Group/Ungroup/floor labels. Sourceec4b127b separates Group/Ungroup from a full-width Move to floor row; R0232539 passes. Earlier failed visual evidence remains unchanged.

Current release-qa Windows232801 builds PASS139s. Workshop233042 passes5/5 with8 actual images, lobby233250 passes5/5 with22, Solo233412 passes3/3 with9; all normal exits. Independent reviewers opened all39 actual images, and the corrected action row/range controls fit. Strict lobby/Solo receipt windows-lobby-solo-232801.json and separate workshop acceptance addendum bind the evidence. All4208 scoped authored inputs remain unchanged against the fresh ec4b prebuild baseline. Scenario console counters are0/0/0, while the workshop raw log retains four unsupported/stripped postprocessing-shader diagnostics; build provenance's dirty=true and ignored-input limits are documented.

TRY, progress and Unity/control guides now point to232801. Saved Home/Play/Settings/Shop/Loadout frames remain editable; generated inner controls are explicitly identified. Sparse rooms, unknown trial label, physical-input/owner acceptance, complete online/recovery/house-mode coverage, richer art/story, measured performance and stripped production release remain open. No native job is running at this checkpoint; original assets, unrelated jobs and unfinished drafts are preserved.

## 2026-10-28T15:30:00Z Menu redesign and isolated Parcel Hall checkpoint

Recovered all interrupted dirty/untracked work into verified local snapshots; latest .harness/recovery/developer-workspace-2026-10-28T15-21-59-256Z contains1983 files. Root remains on sethypagna/wordbound-integration. No original checkout, ICY/Lunar direction, SDK, harness source or other heavy job changed.

Saved minimal menu migration143423 passes and archives exact prior bytes for six menu/theme assets. Equipment/input GUI144044 passes58/58. Lobby150522 passes101/102; the remaining Help title height is corrected and targeted Settings/Controls150912 passes17/17. Earlier143734 and144421 failures remain recorded. New rows preserve original modes, appearance actions, public/client recipe admission and return routes. Solo selection offers Lost Address by default and a separate Parcel Hall, preserving local house and party state.

Parcel Hall v2 adds18 real canonical furniture pieces and one protected renewable HAMMER supply, preserving the original course. Core152127 caught a supply/table collision; moved supply and core152351 passes12/12 over192 resolutions/eight configurations. The earlier overbroad145251 core sweep was explicitly cancelled at its owned worker, recorded in map/overbroad-core-cancelled.json; its aborted result is not a code defect or a completed sweep. Saved variant setup152619 and native architecture/UV/collision/cleanup/factory152720 pass23/23. Root fixed Editor lifecycle disposal and keeps borrowed-material baseline tests separate from owned architecture-material tests.

Matched Editor capture151008 generated14 images with preserved source/scene state. Root opened arrival before/after plus kitchen/attic: the new floor lost wood texture, repeated window/picture panels look artificial and gold baseboard forms a band. These visual choices are rejected for correction despite passing structural checks. This checkpoint is recoverable implementation, not final artwork, Windows delivery, owner acceptance or production release. Previous Windows232801 remains the delivered historical review build until fresh smokes and pixel review pass.

Evidence and current recovery actions: BuildNotes/redesign-20261028/ROOT.md; saved-menu-preservation.json; post-first-native-preservation.json; prebuild-native-preservation.json; map/independent-review.md; creator-scope.md; TestResults/editmode_20261028T152720Z.xml. Creator audit retains missing custom rule/scoring/team authoring, exact seed file export and public custom-map admission. Next: correct map material defaults, recapture, build Windows, verify lobby24/Solo13/workshop8 screenshots, update TRY and full progress. Goal remains unfinished.

## 2026-10-28T15:45:00Z Preserve textured floors and remove repeated room panels

Root viewed the first matched native captures and rejected flat plaster floors, repeated faux windows/pictures and gold baseboard. The correction retains the canonical Planks albedo/normal/UVs with editable tint, removes the baseboard and defaults wall insets to zero. Unity setup154104 preserved the original RoomArchitecture asset outside Resources and changed only MaximumInsetsPerWallPiece. A repeat preserves later owner edits; an unsaved-profile guard was added after independent review.

Focused native154226 passed24/24: geometry/route/collider preservation, borrowed material ownership, exact plank textures/UVs, disposal and Parcel Hall factory identity. Capture154322 completed fourteen fresh matched V2 images, preserving files, memory, scenes and globals. Root viewed arrival, Kitchen and Attic styled images at BuildNotes/redesign-20261028/map-capture/captures/20261028T154341Z-23c34185. The specific material regressions are corrected, but large sparse rooms, landmarks and overall environmental design remain unfinished. No owner approval is claimed. Next: fresh committed-source Windows build with lobby24/Solo13/workshop8 captures and preservation verification.
## 2026-10-28T15:57:30Z Preserve playable creator share settings

Integrated the frozen creator bundle and native fixture after exact original/candidate hash checks. Share now exports a bounded versioned map bundle containing canonical course, exact seed, mode and team format plus catalogue/rules compatibility hashes. Legacy course-only sharing/import remains available. Invalid bundles are rejected before changing history/selection/mode; imported map+seed Undo is one transaction. Selected mode remains a separate session setting and is not restored by Undo or old autosave recovery; custom scoring/rules and public-map admission remain open.

Official R0 155447 PASS, rules155512 PASS35/35, Unity155526 PASS48/48, zero skips. Evidence: BuildNotes/redesign-20261028/creator-seed-bundle/root-integration.json and TestResults/editmode_20261028T155526Z.xml. Actual new Share panel/input and small-screen usability still need review.

Windows build154606 compiledPASS94s. Its Solo155013 and Workshop155114 smokes passed, including all three Solo entry/return paths and original Workshop save/playtest/return. Lobby154752 failed only its stale uppercase queue-label expectation; other steps passed. Updated that expectation to the intended Local/Online/Create labels without removing any contract/layout assertions. All4228 authored files remain unchanged (windows154606-preservation.json). Rebuild with this correction and creator bundle, then repeat the final three scenarios. Failed evidence is preserved.
## 2026-10-28T16:33:00Z Play organization: full surface and retained party

Owner prefers the earlier colourful artwork and clearer organization, with no character in Play, less margin and optional Roboto typography. Integrated the four exact draft files from BuildNotes/play-organization-20261029/play/manifest.json; restored coloured mode/map cards without rays, fixed actions and a wrapping choices grid. Play now uses a 12-unit outer gutter and hides its existing avatar, guests and contact shadow while preserving party, bindings and outfit. Home/Customize restore the same avatar.

Official R0 gate163056 PASS, core162833 41/41, native visibility/camera162848 4/4 and Play/settings/navigation163111 32/32 PASS. Evidence: TestResults/playmode_20261028T162848Z.xml and TestResults/playmode_20261028T163111Z.xml. The source snapshot .harness/recovery/developer-workspace-2026-10-28T16-32-29-175Z preserves 2096 unfinished files. Font/style migration and actual new Windows screenshots remain pending; this is not owner visual approval or full production completion.

## 2026-10-28T16:59:00Z Roboto typography and compact warm menu organization

Integrated official Roboto v3.016 hinted static Regular/Medium/Bold fonts with exact pinned OFL and source hashes. Native Apply165008 creates editable TMP assets/materials, preserves the active profile GUID, backs up seven prefabs/profile plus their metadata, saves full-width Play layout and warms named panel surfaces. Reopened repeat165137 PASS; independent saved-file review verifies font/atlas/material/weight references and untouched legacy fonts. OFL is copied into StreamingAssets for player distribution.

Restore compact Customize colour-part chips, palette, accessory and finish cards; retain the700-unit saved panel and recipe fit. Settings retains one option per row with shorter panels and clearer step controls. Official R0 PASS165739; core41/41 PASS164952; Editor font/frame tests25/25 PASS165248. Combined runtime165423 passed93/94 and exposed one real quit-heading ellipsis. Shortened the heading to Quit game?, retained default Stay and save explanation, and aligned the Play footer with its concise card names; targeted runtime165755 passes14/14. The initial failure is preserved. All4268 authored files remain unchanged after tests; see BuildNotes/play-organization-20261029/typography/native-tests-preservation.json. Fresh Windows pixels are the next check, not yet visual acceptance.

## 2026-10-28T17:16:17.467Z Organized Play Windows checkpoint and full image review

Windows170019 builds exact587ecf84 on Unity6000.6.4f1. Lobby170223 passes5/5 with24 original images, Solo170426 passes4/4 with13, Workshop170534 passes5/5 with8; all exit normally. The strict delivery audit binds45 captures to the exact build. The4268-file preservation pass and exact distributed OFL hash are saved beside the audit. Three independent reviewers opened all45 original PNGs and recorded exact rosters; no blocking clipping or overlapping action was observed. Root also opened Create Wordbound, Escape and Dibs images.

Remaining visual findings are explicit: single-choice map cards stretch across a whole row, sparse states leave too much vertical blank space, Solo cards use BOX/TABLE identifiers instead of actual map previews, Online entry retains older form styling, worlds remain sparse, and Workshop controls remain dense. The current bounded card-width correction follows that evidence. Full matches, separate PCs, physical input, owner art acceptance and production remain open. Each raw player log retains four stripped/unsupported postprocessing diagnostics. No trial-label removal was attempted. Reports/gallery/audit: BuildNotes/play-organization-20261029/review and windows170019-*.json. Previous builds and unfinished drafts remain preserved.

## 2026-10-28T17:22:45.969Z Prevent single map choices from stretching across Play

The reviewed Windows170019 images show Random house and Wordbound maps stretching across the entire row. Grid capacity now determines width even when only one real card exists; those two routes use the normal house-card footprint and left alignment. The editor caption is shortened to Build & playtest. No fake options, handlers, saved assets, mode identities or fixed actions changed. The tutorial note, vertical whitespace, actual map preview art and Online styling remain open.

R0172009 PASS; core17204241/41; native17205833/33, zero skips. The new comparison test checks three viewport widths against actual Pinwheel card dimensions, unchanged fixed actions, selection, caption fit and no unintended creator launch. All4268 authored files remain unchanged. Exact draft/integration and evidence: BuildNotes/play-organization-20261029/play-polish. Fresh Windows verification is next.

## 2026-10-28T17:44:10.220Z Preserve final technical checkpoint and owner visual rejection

Windows172247 exact74ffae03 passes lobby1723565/5 and Solo1724434/4. Workshop172606 passed scenario actions but strict audit rejected party/playtest focus=false. Preserved windows172247-first-audit-failure.json; repeat1728015/5 passes unchanged focus checks. Final strict45-image audit and4268 authored-file preservation pass. Three independent reviewers opened all final45 images and recorded fit/selection evidence separately from appearance. Root inspected both corrected single cards.

Owner explicitly rejected current imagery, colours and plain Local appearance; Workshop remains text-heavy and insufficiently detailed. Active work is now BuildNotes/visual-coherence-20261029/ROOT.md: truthful map captures with saved editing controls, a shared coherent palette, original-art object tiles and icon-led Workshop tools, and actual environment/asset detail. The172247 build remains a recoverable technical checkpoint, not visual acceptance. Current uncommitted source adds preview Sprite consumers/fallback and original Workshop object tiles; R0174214 and core17423882/82 pass; native verification is in progress. Preserve all earlier source/art/drafts; do not claim production complete.

## 2026-10-28T17:46:48.562Z Original-art Workshop tiles and editable map-preview consumers

Object picker previously exposed AssetIcon but had no source assignment. It now borrows original item textures when no override exists, draws proportional image tiles with short names and clear selected style, and preserves custom icon providers. Solo map cards accept two optional saved Sprite references, keep original art only as per-map fallback, avoid intercepting input and preserve selected check layering. No actual map screenshot imported or palette promoted yet.

R0174214 PASS; workshop core17423882/82; Editor17431460/60; Play17443516/16; no skips. All4268 authored assets unchanged. Evidence: BuildNotes/visual-coherence-20261029/first-native.json. Current full Windows checkpoint172247 predates these source changes and remains visually rejected. Next: integrate real-content capture with editable framing, shared neutral theme and icon-led Workshop tools, then inspect actual native/Windows outputs.

## 2026-10-28T18:06:34.528Z Compact Workshop tools and optional help

Added twelve original analytic editor icons without changing previous glyph shapes. Rotate/duplicate/delete/group/ungroup, Undo/Redo and seed reroll use compact tool buttons with named controls/tooltips. Original-art object tiles remain. Rooms use readable labels/search; templates, mode options and instructions are optional sections. Active controller tools still appear automatically even when help is closed. Actions and stored IDs/transactions are unchanged.

R0180038 PASS; native input/source-binding18014328/28 and icon registry1803531/1 PASS. Actual small-icon/layout review remains pending the new palette/capture slice. Snapshot .harness/recovery/developer-workspace-2026-10-28T17-47-16-334Z preserves2264 unfinished files. Full production remains open.

## 2026-10-28T18:28:49.128Z Shared editable palette and truthful map capture

Integrated one saved neutral interface palette across seven authored frames and Online, preserving original art pixels and prior editable archives. Fixed constructor-time resource loading exposed by native migration; fixed XP distinction and dark fallback illustration/font ink. Native shared-theme21/21, lobby/settings/shop/loadout/navigation97/97 and scoped core41/41 pass; repeated migration is clean. Exact4268-file comparison allows only eight intended UI asset changes, with16 exact original/meta backups. Map camera settings and capture pipeline now render both installed maps, but root rejected their empty composition and has not imported them. Evidence: BuildNotes/visual-coherence-20261029/shared-theme/native.json and migration-preservation.json. Owner style acceptance, new Windows review and complete production remain open.

## 2026-10-28T18:43:31.216Z Original timber and route door detail

Native architecture/course/variant tests pass27/27 after a shader float precision assertion repair; scoped variantcore12/12. Root added saved-profile compatibility check with exact savedbytes preserved. Matched same-seed/framing captures show original timber on ramps and clipped doorway surrounds; canonical colliders/manifests unchanged in tests. Closer camera settings remain editable but both thumbnailcompositions are rejected for generic sparse rooms and are not imported. Evidence: BuildNotes/visual-coherence-20261029/environment/native.json. Substantive roomcomposition and Windowsreview remain.

## 2026-10-28T18:49:37.850Z Original artwork and context in Workshop

Integrated nine independently reviewed source files: original/profile-aware preview meshes, canonical fit, context-only labels, smaller handles, assigned-course initialization ordering and generated fixed-zone drag mapping. Preview lights enable only during the owned camera render and disable before other cameras; disposal unregisters them. Native87/87 passed after a2mm floorposition tolerance reflecting existing20km previewoffset; first85/87 result preserved. Evidence: BuildNotes/visual-coherence-20261029/workshop-world/native.json. Actual Windowsimages, ordinaryphysicalinput and broadproduction remainpending.

## 2026-10-28T19:07:25Z Windows review checkpoint and focused presentation fixes

Windows184940/c4258b18 passes lobby1851325/5, Solo1854154/4 and Workshop1856185/5. Strict audit binds the45 PNGs and matching production source; preservation covers4288 prior authored files. Evidence: BuildNotes/visual-coherence-20261029/windows184940-audit.json and windows184940-preservation.json. Independent24-lobby-image review confirms remaining placeholder map imagery, excess empty space and mismatched background detail. Technical checks are not owner style acceptance.

Settings now hides the avatar/contact shadow while open and restores the same character/party/outfit on return. Workshop exposes existing focus-selection and whole-floor camera actions through compact icon tools. R0190432 PASS; scoped core19045741/41 and native presentation1905464/4 PASS. These source fixes postdate184940 and still need the next Windows build. Recovery snapshot .harness/recovery/developer-workspace-2026-10-28T19-06-28-109Z verifies2379 unfinished files. Parcel Service composition/source-binding drafts are underway outside Assets; existing courses/public contracts are preserved.

## 2026-10-28T19:15:20Z Compare existing lobby backdrop sources

Added a bounded explicit capture using the existing preference/render-state teardown. Native191136 passes1/1 with six original1600x900 Home/Shop/Customize images; root opened all six. The existing real3Droom matches character style more closely but is sparse at this camera; a wholesale background switch is not adopted. The detailed illustration remains different in realism. Preserve both sources and improve purposeful furnishing/framing before changing defaults. Evidence: BuildNotes/visual-coherence-20261029/background-comparison/REVIEW.md and its dated ui-journey.json. No new visual approval or production completion is claimed.

## 2026-10-28T19:49:48.831261+00:00 Save Parcel Service and bind independent Local/Workshop source

Separate three-room course saved at Resources/Wordbound/LocalCourses/ParcelServiceV1.asset and launch assignment saved through Unity. Original course/public identities are unchanged. BatchA native19242119/19 and core19230019/19 pass; prior4288 authored files preserved at creation. BatchB native19315691/92 first failed because its new test helper built an empty course; root repaired helper to use actual Parcel factory, preserving assertions. Targeted19343135/35 passes. Bind193605 PASS with exact prior launch backup. R0194614 PASS; refreshed core19463719/19 and practice-guide19474114/14 PASS. Full physical native194837 is not yet verified.

Five preview scripts independently reviewed; exact source hashes/one-map import preserve original thumbnails. Actual native capture193745/ParcelService.png opened by root: pale identical furniture and blank teal walls remain visually insufficient; no PNG imported. New native physical completion and separate Windows QA are source-checked only. Evidence: BuildNotes/visual-coherence-20261029/map-composition manifests, integration-1942.json and map-previews/captures/20261028T193758Z-37c05442/receipt.json.

## 2026-10-28T20:11:30.615702+00:00 Course-local finishes and compact Workshop tools

Native200159 material/source/input/overlay52/52 and200808 theme21/21 pass. Exact-source remaps preserve normal mesh/grip/skin/tactile path, and saved room style now follows local/network/editor/Workshop/thumbnail sources. Guarded setup200411 first refused Unity compiled URP Lit Shader dirtiness before any writes; root narrowly exempts that exact package Shader cache state while retaining importer+source hashes. Setup200521 and reopen200848 PASS. Five new editable assets are saved under Art/Wordbound/ParcelService; only Parcel course visual assignment changed among4290 prior authored files (4289 unchanged). Fifty capture input files remain unchanged after reopen.

Root and independent reviewer opened full960x540 captures200337before/200615after: red counter bodies/oak tops/walnut chest/blue walls retain grain/hardware; blank walls and sparse composition remain. No visual acceptance claimed. Workshop seed controls collapse behind an icon with unchanged seed/history actions and shared dynamic hit exclusion. Existing theme fixture updated for closed/open/closed bounds; line endings normalized after draft transfer. Windows render still pending. Evidence: map-composition/material-authoring-setup/preservation.json, material-review/render-review.md and workshop-toolbar/independent-review.md.

## 2026-10-28T20:26:00Z Parcel Service local gameplay and real preview

Native202225 passes all six Freestyle physical sites with actual crafting and clean shutdown, following strict held-item ownership repair (20162911/11). TimeRun195312 case already completes three laps. Exact camera/preview import201528 is saved. See BuildNotes/visual-coherence-20261029/map-composition/physical-play/NATIVE-GAMEPLAY.md and map-previews/captures/20261028T201427Z-f2844736/import-receipt.json. Windows and visual detail remain pending; no full production or online acceptance claim.

## 2026-10-28T21:08:00Z Save editable Parcel fixtures, previews and camera clearance

Saved three original-art fixture prefabs and optional per-module visual bindings shared by runtime/Workshop/previews. Spawn migration205132 preserves other course edits and moves only Start0 away from the rear wall. Rules20470820/20 and combined native20490092/92 pass. Fixture create205804 and no-write repeat210047 pass; prior authored preservation4299 unchanged plus intended profile update. Exact three-map preview import210504 passes with backups. Root opened all3 current captures; rooms remain sparse and visual acceptance is open. Evidence and limits: BuildNotes/visual-coherence-20261029/map-composition/SAVED-PRESENTATION.md. Physical rerun210548 is active; final Windows review pending. Recovery snapshot21-06-14 verifies2809 unfinished files.

## 2026-10-28T21:12:00Z Final Parcel physical route rerun passes

Native210548 passes2/2 after current saved spawn and dressing. Freestyle168.51s, TimeRun164.27s; six physical socket completions each and zero cleanup failures. Exact XML and limits appended to map-composition/physical-play/NATIVE-GAMEPLAY.md. Fresh Windows build211157 is active from66687eb7. Authored baseline .harness/delivery/ui-authored-before-2026-10-28T21-08-33-016Z.json covers4306 files.

## 2026-10-28T21:32:00Z Workshop cutaway correction verified natively

Root integrated exact three-file workshop-depth manifestdc684301 after hash checks and independent source review. Opaque preview materials now clone original Lit surfaces; transparent ghosts and borrowed author materials remain intact. Cutaway hides wall-only fixtures while full worlds keep them. R0212919 PASS; native212943 passes13/13 with source, clone lifecycle, complete texture/UV/keyword preservation and no authored-row mutation checks. Actual new Windows rendering remains pending. Preview0.3 comparison212401 retained/not imported after root and independent reviewers found old maps too dim; root's0.5 comparison follows. Windows211157 remains strict63-audited, with its known floating-cutaway defect preserved.

## 2026-10-28T21:38:00Z Select brighter shadowed previews and save Workshop correction

Final preview213535 retains original0.7 fill and enables soft key shadows. Root opened all3 images; lower0.3/0.5 renders remain preserved and were not imported. Exact import213721 passes with previous images/meta/layout backed up; all input/source/global preservation flags true. Workshop13/13 native and independent ownership review pass. Stage/material/cutaway source plus selected3PNGs are ready for fresh Windows verification. This selects a bounded render correction, not final map art approval. Recovery21-32-27 verifies2883 unfinished files.

## 2026-10-28T21:51:00Z Preserve floor appearance while selecting Workshop rooms

Actual213901/214134 Windows comparison exposed an overly bright mint selected floor. Root limits the owned Unlit selection cue to four existing border meshes; the real floor always keeps its authored Lit material. Source/picking/ghost geometry is unchanged. Independent selection review passes. Native214636 retained12/13: only exact colour-equality assertion failed on float precision. Tight RGBA-distance tolerance preserves every substantive check; R0214814 and native21492213/13 PASS. New Windows rendering still required. The213901 build remains an intermediate render candidate, with only ParcelWorkshop checked.

## 2026-10-28T22:08:22Z Final visual-correction Windows review build

Build215158/source4ee1d37a passes all five Windows flows: lobby2155175/5, Solo2155364/4, Workshop2156005/5, Parcel Solo2156183/3, Parcel Workshop2153494/4. Both strict evidence audits pass for63 original screenshots;4306 authored files remain unchanged. Evidence: BuildNotes/visual-coherence-20261029/windows215158-audit.json, windows215158-parcel-audit.json and windows215158-preservation.json. Three independent visual reports cover24+21+18 images. Root opened final Shop, Sorting close-up and Parcel map chooser: wood-preserving selection, original material detail and real previews verified; sparse rooms, pale props, technical advanced controls and Shop/map style mismatch remain open.

TRY-WORDBOUND.md now links the exact215158 executable and63-image review gallery. UNITY-EDITING.md describes saved course/material/prefab editing and Templates → Parcel Service. Gallery static references/JavaScript parse pass65images; browser rendering is NOT-RUN because browser automation rejects local file URLs. No alternative browser or serving workaround was attempted. Full production, physical-input, richer content, motion/story/creator and separate-PC public multiplayer/recovery acceptance remain unfinished in progress.md. Existing failed/rejected evidence and unfinished work are preserved.

## 2026-10-28T23:02:49.516Z Contextual creator and original-art Shop verification

Integrated Objects/Puzzles/Room Workshop panels, friendly exact-ID choices, craftable-only recipe tiles and an additive inspector capture flavor. Shop preview/ownership/equip behavior is explicit; selected footer reduced to96 and common item artwork now prefers the original game model image. Duplicate BAT overlay removed from finish cards. Equipment opening suggests the bound local-team puzzle prerequisite/tool without auto-crafting or replacing manual choices in the same context.

Full core221322 passes1114 with one explicit bounded-arena skip; R0 225834 passes. Unity Editor225849 passes142/142; Shop230013 passes31/31, zero skips. Shop224315 captured14 untouched native images with complete cleanup; independent7-small/root7-large reviews identified the now-corrected shield icon mismatch and oversized footer. Current14-image repeat230150 is running; Windows delivery is still the earlier215158 build. Evidence: BuildNotes/creator-clarity-20261029/ROOT.md and shop/capture/{root-review,compact-review}.md; exact source manifests/peer reports are under that wave.

Full-map native diagnostic remains open:224431 exposed a test approaching the checkpoint before picking up the keepsake; corrected fixture225343 now proves the first carrier checkpoint/door/letters/craft but pushes the marked TABLE while approaching through it. No production physics was changed to satisfy that fixture. Next correction must use real near-side collider bounds and admitted motor input. Full34-room/72-site/three-lap acceptance, public/team/recovery/House networking, richer map composition and owner visual/feel acceptance remain unfinished. Recovery22:43:40 verified3047 modified/untracked files; original ICY/Lunar checkout is untouched.

## 2026-10-28T23:34:00Z — Creator clarity native verification

Final Shop behavior232639 PASS31/31 and native capture232744 PASS1/1 with14 original images. Root opened all seven720p states; shop/capture/root-final232744.md records improvements and unresolved art differences. The one-time Unity footer setup232335/repeat232527 preserves12,267 unrelated serialized properties; only ShopPage.prefab receives96-unit footer/default action width. Post-save4306-file preservation PASS is BuildNotes/creator-clarity-20261029/shop/compact-correction/final-capture-preservation.json. Native first-leg231532 PASS proves three rooms/two puzzles/16 letters/three crafts from ordinary spawn with actual motor, melee and sensors; no injected recovery/completion. Exact evidence and remaining full-route limits are flagship/NATIVE-FIRST-LEG.md. Source is being checkpointed before fresh Windows verification. No owner visual or release approval inferred.

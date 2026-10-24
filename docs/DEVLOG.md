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

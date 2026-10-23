# Wordbound production delivery

Owner: James / SethyPagna. Updated 2026-10-23 (Asia/Hong_Kong).

Legend: `[ ]` unfinished; `[~]` implementation or verification in progress; `[x]` completed with the stated evidence. Source completion is separate from player/release acceptance.

## Goal

Deliver a tested Windows build with five modes, a changing four-destination/four-layer/three-lap world, physical word-and-equipment puzzles, public/private human multiplayer and recovery, a comprehensive workshop, detailed editable models, and Unity authoring controls requiring no code changes for ordinary design work.

The latest instruction includes public matchmaking for **all five multiplayer modes**, including Freestyle, superseding its original private-only restriction. Public games must never silently substitute AI or a solo session. Designs need believable construction/material detail, readable silhouettes and consistent physical scale.

## Preserve existing work

- [x] Compare James/ICY/Sethy histories; create isolated integration and feature worktrees.
- [x] Reconcile ICY changes and preserve original main, modified font and unfinished Lunar work.
- [~] Maintain coherent commits, exact evidence and recoverable checkpoints.
- [x] Recover all seven worktrees after interruption; preserve 719 dirty/untracked/authored files with binary patches and verified SHA-256 copies (2026-10-23).
- [ ] Prepare final review branch/PR and attach it to this chat.

## Gameplay, world and story

- [x] Implement five mode definitions, inventory/crafting/interactions, command deduplication, checkpoints, keepsakes and three-lap results.
- [x] Author four destinations, layered routes and six puzzle families with deterministic manifests and validation.
- [x] Pass 8,000 seed cases on the recorded frozen flagship revision; this does not prove final content or physical playthrough.
- [x] Repair FFA supplies for all 13 enabled recipes; 19 focused cases pass.
- [x] Run 1,000 arena seeds: exact replay/import, source/spawn/placement bounds and budgets pass (`TestResults/core_20261020T201057Z.trx`, 255.7 seconds).
- [~] Verify native letter selection/collection; remote HUD lookup and occluded-target repairs/regressions are integrated, native execution pending.
- [x] Implement Freestyle station selection, shared keepsake and persistent practice; 63 related source cases pass.
- [~] Integrate Freestyle HUD/guide and verify native practice.
- [~] Implement/test SHIELD/PLATE direction, raising, wear/breakage, movement and input.
- [~] Pass all six physical puzzle mechanisms natively; earlier keepsake/timeout failures remain recorded.
- [x] Implement story signs, discovery messages and collected-clue journal.
- [~] Verify native story readability, progression, team isolation and restoration.
- [ ] Complete full three-lap Time Run, Team Race and Brawl Race matches.
- [ ] Verify FFA knockouts/respawns/10-point finish/eight-minute timeout/draws and multiplayer Freestyle without damage/timer/winner.
- [ ] Walk all destinations/layers/representative routes; measure 18–25 minute sessions and recover from wasted resources/falls/disconnects without permanent softlocks.

## Workshop and Unity editing without code

- [x] Implement 3D editing, layers, random zones, validation and route/checkpoint/spawn authoring.
- [x] Verify exact-draft playtest/return preserves undo/redo history.
- [~] Verify drag/rotate/resize/multi-select/group/snap/controller workflows, autosave/recovery and JSON sharing.
- [~] Finish automatic exact custom-course sharing in private online sessions; exact-map/profile handoff is integrated, client checks pass 41/41 and unique Worker checks pass 98/98. Real two-peer authored transfer remains pending.
- [~] Expose camera FOV/distance/sensitivity/obstruction settings through validated Inspector assets; source and live-edit regression are integrated, native test pending.
- [~] Expose equipment visuals/materials/grips and presentation settings without code changes; replacement-bound/grip/custom-ID regressions are integrated, native test pending.
- [~] Provide Unity course assets/editor controls for modules/layers/spawns/puzzles/random ranges, with preview/validate/playtest actions; native default asset creation passes, editor interaction/persistence verification remains pending.
- [~] Verify edit/save/reload/playtest, invalid-value errors, old-save compatibility and online content identity. Pending Inspector forms now survive selection changes and script reload through bounded recovery files, with explicit conflict handling and in-memory fallback; native regressions remain pending.
- [~] Save reusable prefabs/assets and an illustrated Unity editing guide. The source-verified no-code guide and native default assets are available; final Unity screenshots remain pending.
- [ ] Human usability: four of five new testers create/play a randomized course in ten minutes.

## Models and presentation

**Owner direction, 2026-10-23:** DetailV2 appearance is rejected: old, generic and insufficiently detailed. The reference is the **original Wreckabulary game assets**, not the first Wordbound set. Preserve both Wordbound revisions, compare against the original supplied-item contact sheet and native game captures, and revise a small representative sample before rolling the new treatment across the collection. Existing technical passes remain valid only for their recorded bytes; they do not imply visual approval.

- [x] Produce 12 concepts and editable Blender models: BAT, BROOM, HAMMER, SPEAR, SHIELD, PLATE, TABLE, SOFA, BED, STOOL, FAN, CLOCK.
- [x] Pass 24 FBX/GLB reimports and native presentation checks.
- [~] Inspect actual assets close-up/held/placed/at gameplay distance for believable detail, scale, materials and readability.
- [~] Improve deficient models/materials while preserving editable sources and appropriate rendering/collision costs. DetailV2 build, all 24 independent reimports, 48 final views and delivery-integrity checks pass; exported meshes fall from the verified original count of 174 to 27 (84.5%), with a maximum 4,908 triangles per model. Earlier hand-summed reports said 193; the final manifest comparison corrects that baseline. Side-view review found and repaired the fan's missing shaft/support. Unity appearance remains pending.
- [ ] Verify Unity prefabs/materials/grips/icons and all held/placed/thrown/used/destroyed states.
- [ ] Capture final assets/gameplay and obtain owner visual-quality/feel review on the reviewable build.

## Public and private human multiplayer

- [x] Configure Fusion/Physics Addon and deploy a separate coordinator on the correct account.
- [x] Pass Photon authentication 3/3 and actual authenticated two-peer gameplay/durable checkpoint acknowledgement.
- [x] Pass earlier 2/4/8-peer transport; final full-gameplay acceptance remains separate.
- [~] Integrate and validate schema/protocol v3 across practice, defense, clients and service.
- [~] Verify private codes, whole-party readiness/allocation, Solo/Duo/Squad and Fill On/Off.
- [~] Verify concurrent last slots, cancellations, oversize parties and expired grants.
- [x] Export and deploy the exact 36-entry public catalogue (nine mode/format combinations, four seeds). Deployment and module/catalogue readback pass; live HTTP party allocation/content admission and cleanup pass. Native Photon gameplay remains below.
- [ ] Public Time Run: human cooperative allocation and playable match.
- [ ] Public Team Race: Solo/Duo/Squad, intact parties and clean-team isolation.
- [ ] Public Brawl Race: Solo/Duo/Squad, intact parties and enemy combat.
- [ ] Public FFA: multiple humans, individual scoring and respawns.
- [ ] Public Freestyle: multiple humans, shared exploration/practice, no damage/timer/winner.
- [ ] Prove public allocator use without private-code substitution, bots or silent solo fallback.
- [ ] Verify automatic custom-map upload/download, exact content validation and retention after host departure.
- [ ] Verify full four/eight-player flagship gameplay, reconnect and automatic host replacement.
- [ ] Exercise 2/4/8 clients at 0/100/150 ms RTT and packet loss.
- [ ] Force host loss during pickup/craft/lap/final delivery; check no duplicates and measure ten-second recovery/two-second rewind targets.

## Tested Windows delivery

- [x] Create/read back Windows release, QA and development profiles.
- [~] Complete QA build; first attempt exceeded 30 minutes, caches retained.
- [ ] Launch QA player and complete workshop→game→workshop smoke; inspect frames/motion.
- [~] Exercise keyboard/mouse/controller, focus loss, cameras, reconnect, return and quit. Controller selection/menu repair, party navigation, workshop focus protection and allocation cleanup are integrated and independently source-reviewed; 29 native input/menu regressions await execution.
- [ ] Profile eight players/active equipment against 60 fps at 1080p Low on Core Ultra 7 + Intel Arc.
- [ ] Recheck affected lobby/wardrobe/Escape/controls/local multiplayer/home-save regressions.
- [ ] Build release player and smoke-test the exact stripped executable.
- [ ] Package build identity, controls, multiplayer instructions, known limitations and Unity editing guide; give James the tested build.
- [ ] Close production acceptance only after required automated, visual and human evidence exists.

## Active work and evidence

### Recovery and cleanup — 2026-10-23

- [x] Confirm recovery baseline `a896be3`, original main/font/Lunar files and all feature branches. Final detailed assets are integrated through `93d4e35`; no Unity or Blender job remains from the interrupted work.
- [x] Save a private recovery snapshot under `.harness/recovery/2026-10-23T08-53-03-542Z-da1d8676-0794-40fc-a246-cd5de5cc201a`; its manifest verifies every copied file. Existing Git history, caches and private service credentials remain in place.
- [x] Recover the actual public C# result: 28 executed / 28 passed. No Worker success was inferred from the interrupted run.
- [x] Integrate recovered public/all-five-mode and private custom-map sources; C# client 41/41 and pre-metadata-repair Worker 98/98 checks pass. Final Node binding checks pass 6/6 and both TypeScript checks pass.
- [~] Remove repeated input allocations; gameplay and party controller navigation, neutral input release and workshop focus guards are integrated and reviewed. Public map labels are cached by mode/format and unsupported Fill is normalized. Native verification remains pending.
- [x] Deliver DetailV2 source/models with 24 independent FBX/GLB reimports, preserved bounds/grips and 174-to-27 mesh reduction. The integrated-checkout integrity validator passes; Unity renderer counts, pivots, appearance and frame rate remain separate pending checks.
- [x] Bind public admission to the approved map's exact mode and team format in source and service tests. Final Worker run passed 95/100; four cases hit the 20-second harness deadline and one orphan-expiry test assumed uploads existed at setup time. The test now uses persisted expiry; the exact five-case rerun passes 5/5. All 100 unique cases have passing evidence across these runs, not one clean full-suite pass. Production deadlines are unchanged. Actual native catalogue and live activation remain pending.
- [~] Present camera settings in clearly labeled Inspector groups, with metre distances and degree angles. Source saved; native appearance and live behavior checks pending.
- [ ] Validate the assembled source, import final content, run native gameplay/public/recovery checks, then build and smoke-test the exact Windows delivery.

There is still no verified Windows player. The public catalogue is now deployed and its live HTTP allocation/admission check passes. Native public Photon gameplay and final release acceptance remain unfinished.

Root: integration, HUD/player/guide, native verification, content export, coordinated deployment and Windows delivery. Puzzle-race: independent defense review and additive Unity authoring tools. Map-workshop/assets: defense/arena verification and asset quality. Online: protocol v3, all-mode public allocation and private-map sharing.

The bounded Rules run passed 409/410 cases (`TestResults/core_20261023T092813Z.trx`); its only failure was an obsolete schema-2 assertion. The corrected deployment group passes 14/14 (`TestResults/core_20261023T093147Z.trx`), explicitly rejecting both old schema versions. An earlier overbroad filter accidentally started the already-covered flagship seed sweep; it was stopped through verified test-process PIDs and is recorded as a cancelled/failed run, not a pass. The prior 8,000 flagship and 1,000 arena seed evidence remains unchanged.

Assembled R0 passed in 479.8 seconds (`Logs/gate_check_all_20261023T103849Z.log`) with the controller/camera source. Cold native preparation then failed after 1,189.4 seconds with two HUD-test assembly-reference errors (`Logs/gate_exec_PrepareFromEnvironment_20261023T104740Z.log`); asset import/export had not executed. Commit `32a0588` adds the missing direct presentation reference and preserves 32 Unity-generated metadata files. The six detail tests and reviewed QA workload are integrated; fresh R0 passes in 386.4 seconds (`Logs/gate_check_all_20261023T111607Z.log`). The cold retry in `Logs/gate_exec_PrepareFromEnvironment_20261023T112358Z.log` completed native compilation, then ended during asset import without a gate JSON/history verdict. Completion is unverified; its cause is undetermined. Twelve additional native metadata files are preserved, with existing GUIDs unchanged. The next retry retains caches, release `wordbound-20261023-v1`, and seeds `0123456789abcdef`, `0000000000000001`, `0000000000000002`, `0000000000000003`. Earlier Burst/IL post-processing startup delays remain recorded. Schema-3 service still has live API 7/7 PASS, preserved credentials/state and an unchanged website (`BuildNotes/online/V3-DEPLOYMENT.md`); public activation and a tested Windows player remain pending.

Performance audit found that the existing workshop smoke is solo at 720p. A project-owned 1080p Low synthetic eight-character/equipment render workload is integrated and independently source-reviewed, awaiting native execution. It counts eight synthetic rigs plus one local actor and cannot replace real eight-peer network/combat acceptance. Other projects are running on this PC, so current wall-clock test duration is not a representative performance measurement.

Network acceptance review found that graceful host departure does not prove hard-loss recovery and ordinary disconnect had no automatic rejoin path. The generation-fenced transient reconnect loop is now integrated with the existing identity/room and absolute grace deadline; explicit leave remains terminal. Four independently identified callback/token/retry races are corrected. The 15-second writer lease and 10-second transport detection timeout are unchanged: the accepted recovery target starts at detected loss, so actual overlap and restoration time must be measured before changing those settings. Native fixtures now record stimulus, per-player detection/restoration/readiness and observed frame ticks. Lease/election timing changes remain a separate design, with no live service change. The public allowlist remains closed while required verification is unfinished.

Reconnect source and review are integrated through `47a38e1`, timing probes through `ad659fc`, and the reviewed CLOCK fixture route correction through `ae1d671`/`1567f66`. Fresh R0 passes in 130.9 seconds (`Logs/gate_check_all_20261023T115910Z.log`) after fixing the new test file's NUnit/Fusion `Assert` naming collision. Nine recovery-policy tests and four native recovery fixtures are not yet executed. The CLOCK fixture now walks around its placed solid object while preserving the same eight-second deadline and actual arm/arrival/keepsake assertions. The [Unity editing guide](docs/wordbound/UNITY-EDITING-GUIDE.md) and [packaging source review](BuildNotes/equipment-polish/PACKAGING-SOURCE-REVIEW.md) are integrated; actual Unity captures and final packed-asset verification remain pending. All native work remains serialized through the root gate process.

Native preparation now passes: `Logs/gate_exec_PrepareFromEnvironment_20261023T120315Z.log`, 871 seconds, completed 2026-10-23T12:17:48Z. Unity imported DetailV2, created the camera/presentation/Freestyle/flagship authoring assets, and exported/assigned 36 exact public bindings (nine mode/format combinations, four seeds each) for `wordbound-20261023-v1`. The launch asset references the actual catalogue and profiles. Existing model/icon GUIDs and grips remain intact. This verifies preparation, not appearance, gameplay, public service activation or the Windows player. Burst startup pipe errors remain visible in the successful gate result and are documented in `BuildNotes/client-view/BURST-STARTUP-REVIEW.md`; their cause is undetermined. Next: selected native editor/physics/network gates, exact catalogue service validation, and QA/release build verification.

## 2026-10-23T12:27:34Z selected native editor verification

The 18-fixture native batch completed FAIL in 399 seconds: 123 executed, 97 passed, 26 failed, zero skipped. Evidence: `TestResults/editmode_20261023T122055Z.xml` and `Logs/gate_test_editmode_20261023T122055Z.log`. All nine recovery-window cases, eight gameplay shutdown cases, seven basic equipment cases, three detailed maps/bounds/renderer-count cases, seven authoring basics, three workshop handoff cases, 27 presentation, 11 defense-pose, five story, three practice, and seven focus/GUI/cache cases pass.

Outstanding: three articulated transform basis mismatches; one EditMode camera SendMessage assertion; three saved-form/scene persistence failures; eleven input, five party input, three workshop input cases. No failure is suppressed. Camera fixture now invokes its actual managed update directly (as an EditMode fixture), with camera-value/invalid-profile assertions unchanged; native gameplay still verifies natural player updates separately. Pivot tests now print expected and actual native matrices before the unchanged assertions, to distinguish coordinate conversion from an asset defect. Input and authoring repairs are assigned to isolated lanes. Unity also migrated TimeManager to its native rational timestep serialization; scale remains 1 and the original approximately 0.02-second timestep is retained. Public activation and Windows delivery remain pending.

## 2026-10-23 native repair and original-art pilot

- [x] Camera profile fixture passes in the focused native run, `TestResults/editmode_20261023T123439Z.xml`. Detailed maps, dimensions and renderer counts pass again. The same run fails the three pivot basis assertions and the physical-puzzle TABLE craft wait: 8 executed, 4 passed, 4 failed, zero skipped; 504.5 seconds overall.
- [~] Correct the pivot fixture's source-to-native basis conversion, confirmed by actual matrices and exporter settings. Origins, basis tolerances and separate mesh ownership remain strict; asset bytes are unchanged. Rerun pending.
- [~] Integrate reviewed input fixture sampling, exact authoring-draft collection restoration and explicit authored-workshop recovery choices. Fresh assembled R0 passes in 82.5 seconds, `Logs/gate_check_all_20261023T125200Z.log`; affected core and native tests follow.
- [~] Diagnose the physical-puzzle failure. CollectLetters completed at tick 2160; TABLE crafting was accepted but the held output was not observed before the fixture ended. Check craft/frame state and fixture lifetime before changing any deadline or gameplay rule.
- [~] Produce a small original-based SOFA/FAN/HAMMER pilot. Original-model hashes match the preserved main checkout. The twelve-item comparison is in `BuildNotes/equipment-polish/ORIGINAL-GAME-VISUAL-REVIEW.md`; the gallery now shows the original showcase and clearly labels DetailV2 rejected. Pilot production and review are pending.

All seven worktrees and existing versions remain preserved. Public activation is verified as recorded below; completed native gameplay and a tested Windows build remain unfinished.

## Current verification — 2026-10-23T13:31Z

| Work | State and exact evidence |
| --- | --- |
| Public service | [x] Deployed version `05384420-7e14-40c2-b965-0e8a91681df7`; exact module/catalogue readback, preserved storage/secret names and unchanged website. `BuildNotes/online/public-activation-20261023/readback-verification.json`. |
| Live allocation/admission | [x] Two scripted Duo parties, four authenticated guest identities, exact content and signed start certificates; cleanup successful. `BuildNotes/online/public-activation-20261023/public-live.json`. Isolated pool; no Photon or production-population claim. |
| Service validation | [x] Actual native catalogue: Node 10/10, Worker 41/41, both TypeScript checks. Worker evidence in the activation folder. |
| Pivot/authoring/recovery | [x] Native pivot/detail 6/6, authoring persistence 8/8, workshop recovery 11/11 and existing workshop 9/9. `TestResults/editmode_20261023T125443Z.xml`; entire batch failed because input cases failed. Affected workshop core 23/23: `TestResults/core_20261023T125341Z.trx`. |
| Input | [x] Focused native input 24/24 now pass, including real menu repaint/focus and held-input protection. `TestResults/editmode_20261023T133902Z.xml`. Production input protections unchanged; Windows-player handling remains separate. |
| Workshop source selection | [~] Native source-binding 23/25 pass; deferred source choice under a gameplay blocker and same-ID Flagship draft serialization fail. The same batch passes existing workshop/recovery 20/20: total 67/69, zero skipped. Both failures remain under investigation. |
| Physical puzzles | [~] TABLE craft wait remains unresolved. Diagnostic instrumentation now distinguishes per-action deadline from fixture lifetime; strict rerun pending. |
| Original-based models | [~] SOFA V1 and V2 preserved. V2 three-quarter/side pairs retain the original silhouette/colours; close-up exposes an angular seam kink. V3 feasibility/repair underway. Full visual acceptance and Unity integration pending. FAN/HAMMER next. |
| Public modes/build | [~] Five two-client native public cases now running. Duo/Squad, private-map transfer, recovery, full-match gameplay, QA/player performance and release build remain required. |

Earlier paragraphs above retain the sequence of failed and superseded checks. This table is the current acceptance state; a technical asset pass does not reverse the owner's DetailV2 rejection.

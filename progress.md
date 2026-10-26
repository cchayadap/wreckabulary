# Wordbound production delivery

Owner: James / SethyPagna. Updated 2026-10-26 (Asia/Hong_Kong).

Legend: `[ ]` unfinished; `[~]` implementation or verification in progress; `[x]` completed with the stated evidence. Source completion is separate from player/release acceptance.

## Unity developer workspace — 2026-10-26

- [x] Audit existing scenes and asset identity. Imported model paths drive logical keys and rig settings; original assets and accessory submeshes stay intact.
- [~] Extend the existing workspace with Scenes / Design / Assets, direct Lobby / Shop / Loadout previews, categorized browsing and independent material/prefab copies. Source and 36 focused Editor cases pass; interactive Play/Stop checks remain pending.
- [x] Create saved Equipment Studio and Character Studio scenes, 24 categorized editable prefab variants, three saved fonts and five text-material presets. Native setup and repeat pass; all77 outputs are preserved and36 focused editor tests pass. Live interaction/render review remains separate.
- [~] Convert runtime-built layouts into authoritative saved Unity UI assets. Shop and Loadout panels/cards and their profiles are saved. Home and inner wearable/detail layouts remain generated; a Home draft is preserved outside Assets for review.
- [x] Save editable Loadout panel/recipe cards and lobby-camera framing. Native Editor `090419` passes 16/16; setup `090949` and repeat `091403` pass; combined Play `091621` passes 82/82, zero skipped. All 125 existing/new authored outputs remain byte-identical through repeat and Play. The checks include screenshot preference cleanup and typography ownership. [Evidence](BuildNotes/developer-workspace/loadout-camera-20261026.json), [editing guide](docs/wordbound/LOBBY-EDITING-GUIDE.md).
- [ ] Verify ordinary edit → save → reopen → Play workflows, camera controls, preview restoration, authored-asset preservation, and the current Windows build.

Other heavy jobs remain running at James's request. Only this task's Unity work is serialized. Current source work and existing drafts remain preserved; no new Windows build or production-ready claim is implied by these authoring improvements. See [developer workspace plan](docs/wordbound/DEVELOPER-WORKSPACE-PLAN.md).

Latest verification limit: portrait launches `092340` and `093055` failed during Unity Data Store startup, before the diagnostic or capture02 output. The first failed owned launch was stopped after its shared-memory/lock errors; the single unchanged retry exited1 after a Data Store initialization timeout. Existing tests and 125-file preservation remain green. Further native visual/build work is deferred pending startup recovery; no caches or shared services were changed. [Startup evidence](BuildNotes/developer-workspace/portrait-startup-recovery.md).

Workspace R0 `055415` passes Runtime and Editor in15.2seconds. Native setup `054612` and repeat `054859` pass; native `055552` passes36/36 cases, zero skipped. The first setup and first test failures are preserved with their corrections. [Native and preservation evidence](BuildNotes/developer-workspace/native-20261026.json).

Checkpoint `8304fa93` commits the saved Shop layout and runtime typography ownership, after `a351d799` saved the workspace and studios. Shop persistence passes all 12 cases in `073048`; font ownership passes all 8 in `075254`; fresh typography passes all 6 in `083206`. Earlier PlayMode `060642` rewrote dynamic font data; both versions were preserved and the original assets restored before the ownership fix. Subsequent PlayMode `081310` preserves authored outputs against the post-Shop-creation baseline. Interactive editor Play/Stop remains pending. [Continuation evidence](BuildNotes/developer-workspace/continuation-20261026.json).

Story/presentation/menu/cleanup `081750` passes all 144 cases, zero skipped. Its corrected fixture preserves rejection of contradictory recovery state and verifies a consistent non-actionable timeout snapshot. PlayMode `081310` passes all 61 cases. The first Shop creation normalized four derived material ratios in two font assets; those exact changes are preserved and documented separately. All 77 workspace files are unchanged through Play relative to that post-creation baseline, and all 39 Shop outputs remain unchanged. Portrait diagnostic `082029` completed five controls with source/global preservation; rear-light controls are the next diagnostic. The dark-face cause remains unresolved. No current Windows build has been produced.

## Goal

Deliver a tested Windows build with five modes, a changing four-destination/four-layer/three-lap world, physical word-and-equipment puzzles, public/private human multiplayer and recovery, a comprehensive workshop, detailed editable models, and Unity authoring controls requiring no code changes for ordinary design work.

The latest instruction includes public matchmaking for **all five multiplayer modes**, including Freestyle, superseding its original private-only restriction. Public games must never silently substitute AI or a solo session. Designs need believable construction/material detail, readable silhouettes and consistent physical scale.

## Experience direction — 2026-10-26

Continue from the tested build after the credit interruption. Keep player-facing UI minimal and visual: short labels, contextual controls, immediate previews and optional detail. Preserve the original Wreckabulary material, character and silhouette language while improving equipment, adding distinct designs and skins, and reviewing animation in motion. Shop/loadout browsing, preview, ownership and equip state must be easy to understand. Cosmetic purchases retain their existing earned-coin rules and do not change gameplay stats.

- [x] Recover all seven worktrees again; verify 632 dirty, untracked and authored files in the local 2026-10-26 snapshot. Preserve the failed capacity receipt and unfinished lane commits.
- [~] Audit current shop/loadout and redesign browsing around a small number of visual categories with a clear preview and minimal repeated copy.
- [~] Inspect current models, skins, grips and animation, then implement a coherent first collection with editable assets and measured runtime cost.
- [x] Write and review the [gameplay experience rubric](docs/wordbound/GAMEPLAY-EXPERIENCE-RUBRIC.md): eight dimensions, seven player perspectives and ten measurable acceptance rows. Player scores and human tests remain unmeasured.
- [~] Make story presentation editable in Unity and introduce the story through concise contextual cues and optional journal detail.
- [ ] Verify the combined experience in Unity and the Windows player, including navigation, motion, readability, saved choices and online consistency.

Current native checks: R0 passes; 58 selected engine-free cosmetic/combat cases pass. EditMode `022355` passes 92 story/presentation/persistence cases, including 13 new melee cases; PlayMode `022713` passes 58 shop/lobby/collection cases, zero skipped. The configured default Story Profile is generated and wired into online, Workshop and Inspector playtests. Shop categories and actual-outfit portraits are implemented; portrait lighting and screen captures remain under review. These changes are not yet in a new Windows build. [Exact evidence](BuildNotes/experience-refresh/native-slice-20261026.json).

Packing Club build-a contains editable Blender source, four item/LOD models, eight verified FBX/GLB exports and sixteen matched review renders. Agent visual review rejected its flat BAT crown and oval SHIELD silhouette; all draft evidence is preserved. Build-b restores the angular shield and adds rounded construction details; all eight exports and sixteen review renders pass their recorded checks. The optional Unity comparison importer is implemented but not yet run; no selectable online skin is claimed. [Refined evidence](BuildNotes/experience-refresh/packing-club-build-b-20261026.json), [original and draft comparison](BuildNotes/experience-refresh/PACKING-CLUB-REVIEW.html). The skin review found that new catalogue entries require a versioned public-content rollout, and online outfit initialization/thrown appearance need wiring. Existing Candy/Arcade preferences can preserve item catalogue bytes but still require an explicitly compatible admission path.

The portrait diagnostic timed out at both its 300-second (`031649`) and 600-second (`034659`) limits during compiler startup; neither produced diagnostic images. A separate Burst-disabled diagnostic (`043145`) also timed out at 600 seconds. Restoring only verified owned compiler helpers to Normal priority coincided with progress into script compilation, but the probe did not execute and the cause remains unproven. Its owned Unity processes are gone. No persistent settings, caches or original assets were changed. [Initial failure evidence](BuildNotes/experience-refresh/native-experience-blocker-20261026.json), [controlled diagnostic](BuildNotes/experience-refresh/portrait-compiler-isolation-20261026.json).

First story setup/persistence passes remain valid; repeat-idempotence remains pending. Optional epilogues, loadout changes, attack capture and the cleanup fixture repair are integrated. Native `062041` passes 143/144, including all 15 cleanup cases; its one epilogue fixture correction awaits rerun. Shop/lobby/collection PlayMode `060642` passes 59/59, with saved-font mutation separately under repair. Portrait diagnostic `061411` produced all three images; additional controls are prepared. Current Windows and fresh public-capacity verification remain pending. [Continuation evidence](BuildNotes/developer-workspace/continuation-20261026.json).

Public capacity `014532`: FFA8 passes with clean cleanup. Freestyle8 reaches exact roster/gameplay checks but cleanup fails; TimeRun fails at its first guest request. Read-only operator verification confirms the Freestyle match closed, with eight temporary test parties/memberships still present. [Cleanup evidence](BuildNotes/experience-refresh/online-cleanup-operator-20261026.json). The bounded cleanup repair is integrated and independently reviewed; all 15 fake-transport cases pass in `062041`. Fresh isolated public retries remain NOT-RUN; [the exact retry runbook](BuildNotes/developer-workspace/online-next.md) separates maximum-roster entry from full-match acceptance. Source changes do not remove those historical temporary parties or turn the failed receipt into a pass.

## At a glance — 2026-10-26

| Area | Actual progress | Still required |
| --- | --- | --- |
| Plot and player experience | Full [story and play design](docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md), eight-dimension rubric and editable chapter/clue profile; six optional result-aware epilogues and Inspector copy controls now implemented in source | Run new epilogue/UI/persistence cases; implement optional restoration; verify comprehension and pacing with players |
| Core gameplay/world | Five modes, layered flagship, six physical puzzle families, story journal and deterministic content implemented; compact two-client Time Run completes all three laps | Full flagship matches, FFA ten-point finish, remaining recovery/drop cases and all supported formats |
| Public online | All five modes have two-client public entry/movement/command/acknowledgement passes; FFA also passes the eight-player case with clean teardown | TimeRun4 preparation and Freestyle8 cleanup retry; full matches, all race formats, network stress, recovery and online appearance admission |
| Workshop/Unity editing | Windows exact-draft playtest/return passes 5/5; editable camera/course/equipment controls exist; corrected Windows recovery publication passes all 36 native persistence cases | Finish full controls and human no-code usability checks; verify final built-player workflow |
| Artwork and UI | Original assets remain default; revised BAT/SHIELD have editable source, eight verified exports and matching review renders. Shop categories, concise loadout navigation and accepted melee animation source are integrated | Fix portrait rendering; native import and current shop/loadout screenshots; actual attack sequence, all equipment states, online skins and James's visual review |
| Windows release | [Review build 034544](BuildNotes/client-view/TRY-WORDBOUND.md) remains the tested executable; workshop 5/5 and original equipment 5/5 pass on that build. Current combined source R0 passes | New build is pending: normal startup and the controlled compiler diagnostic timed out before the portrait probe. Resume native tests after a concrete correction or improved machine availability, then full acceptance and stripped release packaging |

The earlier short-path Unity repair and package resolution passed on 2026-10-24. Signed version 6000.6.4f1 and all 14 critical installed files were verified; all 20 checked resolved source/metadata files matched it. Profile import `031937` passed in 179.1 seconds with no compiler errors; R0 passed in 28.5 seconds. Incomplete prior cache copies are preserved with verified hashes and package versions are unchanged. Those passes and the `034544` review rebuild do not resolve the new compiler IPC wait above. [Repair evidence](BuildNotes/client-view/unity-short-resolve-recovery-20261024.json).

Native run `034318` passes 36/36 persistence cases, zero skipped, in 84.7 seconds, including the original failure reproductions, read-only-primary recovery and the complete retained direct replacement control. Native run `033851` passes all 12 local cases, including initial keepsake collision and floor support; its one remaining persistence diagnostic was corrected in `034318`. These are two separate native runs. [Persistence receipt](BuildNotes/client-view/native-authoring-lock-contract-pass-20261024.json), [local/first repair receipt](BuildNotes/client-view/native-publication-repair-first-20261024.json). Earlier failures remain preserved. Public run `033100` passed both race entry cases in 129.5 seconds with zero service cleanup failures; it does not erase prior intermittent failures or certify full matches. [Public receipt](BuildNotes/client-view/native-public-races-short-editor-20261024.json).

## Story, endings and player behaviour

- [x] Write the full Lost Address plot and three-chapter route, proposed staging and expected session flow in [STORY-AND-PLAYER-EXPERIENCE.md](docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md).
- [x] Define proposed success, optional full-restoration and timeout endings, plus competitive/FFA/Freestyle/interruption presentations; distinguish them from existing Win/Draw/TimeExpired rules.
- [x] Review anticipated first-time, expert, speedrun, mixed-skill, silent-public, solo, accessibility, language and disruptive play from narrative, player-experience and online-rule perspectives.
- [x] Document unassigned age classification, unranked run feedback, map-feedback principles, no-code narrative authoring and concrete playtest targets. No predicted store score or official age rating is claimed.
- [~] Move current story text/presentation into validated Unity assets and provide chapter/ending preview controls without code editing. Chapter/notice profile, validation, native save/reload and default asset wiring pass; ending copy and final Editor interaction remain in progress.
- [x] Implement result-aware epilogues from exact confirmed state; native `081750` passes all 22 epilogue cases within the 144-case suite, including ties, timeout, team isolation, reconnect and duplicate-outcome prevention. Six outcomes and optional journal integration are implemented. Human pacing and complete match acceptance remain separate.
- [ ] Implement four optional cooperative restoration tasks and their versioned authoritative state; verify recovery, solo validity and the richer ending.
- [ ] Resolve active keepsake withholding and repeated shared-practice station changes with tested player-facing rules.
- [ ] Test story understanding, meaningful contributions and full-course pacing. Current 72 puzzle sites make the 18–25-minute target an explicit risk, not a measured result.

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
- [x] Pass all six physical puzzle mechanisms with native sensors and canonical checkpoints (`TestResults/editmode_20261023T191029Z.xml`); full matches and human traversal remain separate.
- [x] Implement story signs, discovery messages and collected-clue journal.
- [~] Verify native story readability, progression, team isolation and restoration.
- [x] Complete compact private authored three-lap Time Run with two authenticated scripted clients and exact confirmed durable result (`editmode_20261023T202344Z.xml`).
- [x] Verify compact shared Freestyle with two authenticated scripted clients: real station solve, exact one-time reward/replay, inventory retained across selection and observed attack without teammate damage (`editmode_20261023T212947Z.xml`). Public flagship/hazards/recovery remain open.
- [ ] Complete full three-lap Time Run, Team Race and Brawl Race matches.
- [ ] Verify FFA knockouts/respawns/10-point finish/eight-minute timeout/draws and multiplayer Freestyle without damage/timer/winner.
- [ ] Walk all destinations/layers/representative routes; measure 18–25 minute sessions and recover from wasted resources/falls/disconnects without permanent softlocks.

## Workshop and Unity editing without code

- [x] Implement 3D editing, layers, random zones, validation and route/checkpoint/spawn authoring.
- [x] Verify exact-draft playtest/return preserves undo/redo history.
- [~] Verify drag/rotate/resize/multi-select/group/snap/controller workflows, autosave/recovery and JSON sharing.
- [x] Share an exact authored workshop map between two authenticated clients: independent content download, canonical bodies, remote input, command, durable acknowledgement and cleanup pass (`TestResults/editmode_20261023T163900Z.xml`, private case). Full-match health and host-departure retention remain below.
- [x] Expose camera FOV/distance/sensitivity/obstruction settings through validated Inspector assets; native camera-profile regression passes. Windows interaction and human usability remain below.
- [~] Expose equipment visuals/materials/grips and presentation settings without code changes; native original/optional/override presentation checks pass 48/48. Final held/placed/combat appearance remains pending.
- [~] Provide Unity course assets/editor controls for modules/layers/spawns/puzzles/random ranges, with preview/validate/playtest actions; native default asset creation passes, 36-case persistence verification passes; full human Editor interaction remains pending.
- [~] Verify edit/save/reload/playtest, invalid-value errors, old-save compatibility and online content identity. Current native authoring persistence passes 36/36; historical workshop recovery 11/11 and source selection 25/25 remain recorded. Private native online transfer and the current Windows workshop flow pass within the scopes above; repeat-save/restart player recovery and human interaction remain pending.
- [~] Save reusable prefabs/assets and an illustrated Unity editing guide. The source-verified no-code guide and native default assets are available; final Unity screenshots remain pending.
- [ ] Human usability: four of five new testers create/play a randomized course in ten minutes.

## Models and presentation

**Owner direction, 2026-10-23:** DetailV2 appearance is rejected: old, generic and insufficiently detailed. Use the **original Wreckabulary game assets** as the reference and runtime default. Preserve both Wordbound revisions and experimental pilots. No further generation is underway. Check the original assets in actual held, placed and gameplay views; earlier import checks do not imply visual approval.

- [x] Produce 12 concepts and editable Blender models: BAT, BROOM, HAMMER, SPEAR, SHIELD, PLATE, TABLE, SOFA, BED, STOOL, FAN, CLOCK.
- [x] Pass 24 FBX/GLB reimports and native presentation checks.
- [~] Inspect actual assets close-up/held/placed/at gameplay distance for believable detail, scale, materials and readability.
- [x] Restore the original game assets as the default and preserve the rejected variants. Current Windows run `035159` shows four representative originals and actually crafted BAT/HAMMER in held views; the character is grounded. Held-item occlusion and animation remain open. This is agent inspection, not owner visual approval.
- [x] Restore the original cream/teal/Nunito menu style through an editable Unity theme; correct panel contrast, multiline rows, Search wrapping, footer status and party consistency. Native run `232239` passes all 96 relevant UI/input/workshop cases, including nine theme and seven party cases; actual Windows runs `232850` and current `035028` show these visible corrections. Further compact layout and human usability work remain open.
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
- [~] Public Time Run: current native run `040530` passes exact public entry, two authenticated scripted clients, movement, command and durable acknowledgement, with zero service cleanup failures. Sustained full-match and human acceptance remain open.
- [~] Public Team Race: two authenticated Solo parties pass entry, remote movement, command and exact durable acknowledgement in current native run `033100`, with zero service cleanup failures. Duo/Squad, full matches and clean-team isolation acceptance remain open.
- [~] Public Brawl Race: current native run `033100` passes bounded public entry, remote movement, command replay and exact durable acknowledgement, with zero service cleanup failures. Earlier transfer/movement failures remain recorded; repeat reliability, Duo/Squad, combat and full match remain open.
- [~] Public FFA: current native run `040530` passes public entry, remote movement, command and exact durable ACK for two authenticated scripted individuals, with zero service cleanup failures. Actual scoring, respawns, full outcomes and capacity remain open.
- [~] Public Freestyle: current native run `040530` passes public entry/movement/command/ACK for two authenticated scripted players with Fill enabled, with zero service cleanup failures. Shared practice/rewards/no teammate damage separately pass private run `212947`. Sustained public practice and capacity remain open.
- [~] Prove public allocator use without private-code substitution, bots or silent solo fallback across all supported formats and maximum rosters. Bounded two-client public cases already use the real allocator; remaining formats and scale are unfinished.
- [ ] Verify automatic custom-map upload/download, exact content validation and retention after host departure.
- [ ] Verify full four/eight-player flagship gameplay, reconnect and automatic host replacement.
- [ ] Exercise 2/4/8 clients at 0/100/150 ms RTT and packet loss.
- [ ] Force host loss during pickup/craft/lap/final delivery; check no duplicates and measure ten-second recovery/two-second rewind targets.

## Tested Windows delivery

- [x] Create/read back Windows release, QA and development profiles.
- [x] Produce Windows QA build `034544` on exact source `9e013dc9` with Unity 6000.6.4f1; build gate PASS in 237.3 seconds. It includes the original models, initial keepsake placement and current runtime optimizations. Final stripped release remains separate.
- [x] Official Windows workshop run `035028` passes 5/5 in 60.7 seconds, within the unchanged 120-second bound: exact save/draft/return/UndoRedo, normal exit and zero console errors, exceptions and warnings. [Evidence](BuildNotes/client-view/official-short-editor-pass-20261024.json).
- [x] Original equipment run `035159` passes 5/5 with 13 successful collection/craft/equip receipts, a grounded player and an unchanged return draft. Root inspected world/held BAT/held HAMMER images. This is representative static inspection; final style and animation approval remain open. [Evidence](BuildNotes/client-view/windows-original-equipment-pass-20261024.json).
- [~] Exercise keyboard/mouse/controller, focus loss, cameras, reconnect, return and quit. Controller selection/menu repair, party navigation, workshop focus protection and allocation cleanup are integrated; focused native input passes 24/24. Final player handling and reconnect acceptance remain pending.
- [ ] Profile eight players/active equipment against 60 fps at 1080p Low on Core Ultra 7 + Intel Arc.
- [ ] Recheck affected lobby/wardrobe/Escape/controls/local multiplayer/home-save regressions.
- [ ] Build release player and smoke-test the exact stripped executable.
- [x] Provide the tested review build with exact identity, controls, multiplayer instructions, known limits and Unity editing guide in [TRY-WORDBOUND.md](BuildNotes/client-view/TRY-WORDBOUND.md).
- [ ] Package the final production release after its remaining acceptance checks.
- [ ] Close production acceptance only after required automated, visual and human evidence exists.

Earlier recovery and verification entries are preserved in [the evidence history](BuildNotes/client-view/RECOVERY-AND-VERIFICATION-HISTORY-20261023.md).

## Latest verification and next steps

The interrupted maximum-roster run `041032` failed all three cases because the new assertion queried remote user IDs from clients, whereas the installed Fusion API exposes those IDs to the host. The corrected assertion preserves exact native PlayerRef rosters on every client, the host's authenticated identity/admission mapping and all canonical seats. R0 passes. Rerun `20261026T014532Z` passes FFA with eight players and clean teardown. Freestyle reaches eight-player gameplay and exact rosters but fails all 24 return/cancel/leave cleanup requests; Time Run fails coordinator preparation with an HTTP request exception. These remain failures under investigation, not full capacity acceptance. [Original failure](BuildNotes/client-view/native-public-max-rosters-first-20261024.json), [rerun receipt](BuildNotes/client-view/native-public-max-rosters-contract-20261026.json).

The current review build is available for James to try. Official workshop and equipment flows pass; all six workshop screenshots and three equipment screenshots were inspected and retained. The 90-update workshop observation takes 7.694559 seconds, with substantial stalls. This does not measure presented frame rate or establish a controlled speedup; the 60 FPS acceptance target remains open. Original assets are visible and the character is grounded. Held-item occlusion, sparse rooms and motion quality remain to improve. See the independent [timing review](BuildNotes/client-view/OFFICIAL-OBSERVER-RESULTS-20261024.md), [visual review](BuildNotes/equipment-polish/WINDOWS-ORIGINAL-REVIEW-20261024.md) and [runtime save coverage](BuildNotes/client-view/RUNTIME-SAVE-COVERAGE-20261024.md).

Current public run `040530` passes the remaining three modes in 201.3 seconds, with zero skips and zero service cleanup failures. Combined with race run `033100`, all five modes have current two-client entry/movement/command/durable-acknowledgement receipts. These scripted clients share one Editor process; full matches and separate Windows clients remain open. [Three-mode receipt](BuildNotes/client-view/native-public-remaining-short-editor-20261024.json).

Next: all maximum public rosters; full-match/draw/recovery acceptance; measured performance and all equipment states; player repeat-save/restart recovery; narrative implementation and human no-code/feel review. New full-duration fixtures are source-reviewed in an isolated lane and have not run.

Earlier detailed runs are preserved in [the verification history](BuildNotes/client-view/VERIFICATION-HISTORY-BEFORE-035028-20261024.md), including failed attempts and all limitations.

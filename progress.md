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
- [~] Provide Unity course assets/editor controls for modules/layers/spawns/puzzles/random ranges, with preview/validate/playtest actions; source and reference compile complete, native asset creation/persistence pending.
- [~] Verify edit/save/reload/playtest, invalid-value errors, old-save compatibility and online content identity. Pending Inspector forms now survive selection changes and script reload through bounded recovery files, with explicit conflict handling and in-memory fallback; native regressions remain pending.
- [ ] Save reusable prefabs/assets and an illustrated Unity editing guide.
- [ ] Human usability: four of five new testers create/play a randomized course in ten minutes.

## Models and presentation

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
- [~] Export final catalogue and deploy its exact public allowlist.
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

There is still no verified Windows player. Public matchmaking remains closed until the final matching catalogue and service are validated and deployed. This recovery does not change those acceptance states.

Root: integration, HUD/player/guide, native verification, content export, coordinated deployment and Windows delivery. Puzzle-race: independent defense review and additive Unity authoring tools. Map-workshop/assets: defense/arena verification and asset quality. Online: protocol v3, all-mode public allocation and private-map sharing.

The bounded Rules run passed 409/410 cases (`TestResults/core_20261023T092813Z.trx`); its only failure was an obsolete schema-2 assertion. The corrected deployment group passes 14/14 (`TestResults/core_20261023T093147Z.trx`), explicitly rejecting both old schema versions. An earlier overbroad filter accidentally started the already-covered flagship seed sweep; it was stopped through verified test-process PIDs and is recorded as a cancelled/failed run, not a pass. The prior 8,000 flagship and 1,000 arena seed evidence remains unchanged.

Assembled R0 passed in 479.8 seconds (`Logs/gate_check_all_20261023T103849Z.log`) with the controller/camera source. Cold native preparation then failed after 1,189.4 seconds with two HUD-test assembly-reference errors (`Logs/gate_exec_PrepareFromEnvironment_20261023T104740Z.log`); asset import/export had not executed. Commit `32a0588` adds the missing direct presentation reference and preserves 32 Unity-generated metadata files. The six detail tests and reviewed QA workload are integrated; fresh R0 passes in 386.4 seconds (`Logs/gate_check_all_20261023T111607Z.log`). Cold native preparation is running in `Logs/gate_exec_PrepareFromEnvironment_20261023T112358Z.log`, with caches retained, release `wordbound-20261023-v1`, and seeds `0123456789abcdef`, `0000000000000001`, `0000000000000002`, `0000000000000003`. Earlier Burst/IL post-processing startup delays remain recorded. Schema-3 service still has live API 7/7 PASS, preserved credentials/state and an unchanged website (`BuildNotes/online/V3-DEPLOYMENT.md`); public activation and a tested Windows player remain pending.

Performance audit found that the existing workshop smoke is solo at 720p. A project-owned 1080p Low synthetic eight-character/equipment render workload is integrated and independently source-reviewed, awaiting native execution. It counts eight synthetic rigs plus one local actor and cannot replace real eight-peer network/combat acceptance. Other projects are running on this PC, so current wall-clock test duration is not a representative performance measurement.

Network acceptance review found that graceful host departure does not prove hard-loss recovery, the current 15-second checkpoint writer lease conflicts with the 10-second recovery target, and ordinary disconnect has no proven automatic rejoin path. The online lane is implementing a generation-fenced transient reconnect loop with the existing identity/room and absolute grace deadline; explicit leave remains terminal. Lease/election timing changes require a separate reviewed step and real timing evidence. The public allowlist remains closed while these requirements are unfinished.

Reconnect source is checkpointed in online-lane commit `e3194fe`, with independent race/cancellation review and targeted fixtures in progress. It is not yet integrated or compiled. The Unity editing guide and packaging source review are assigned separately; they must use real menu/asset names and distinguish pending Unity captures from verified screenshots. All native work remains serialized through the root gate process.

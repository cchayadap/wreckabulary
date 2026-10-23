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
- [~] Exercise keyboard/mouse/controller, focus loss, cameras, reconnect, return and quit. Controller selection/menu repair and allocation cleanup are integrated and independently source-reviewed; 20 native input/menu regressions await execution.
- [ ] Profile eight players/active equipment against 60 fps at 1080p Low on Core Ultra 7 + Intel Arc.
- [ ] Recheck affected lobby/wardrobe/Escape/controls/local multiplayer/home-save regressions.
- [ ] Build release player and smoke-test the exact stripped executable.
- [ ] Package build identity, controls, multiplayer instructions, known limitations and Unity editing guide; give James the tested build.
- [ ] Close production acceptance only after required automated, visual and human evidence exists.

## Active work and evidence

### Recovery and cleanup — 2026-10-23

- [x] Confirm integration HEAD `a896be3`, original main/font/Lunar files, all feature branches, and no surviving Unity/compiler/Blender jobs.
- [x] Save a private recovery snapshot under `.harness/recovery/2026-10-23T08-53-03-542Z-da1d8676-0794-40fc-a246-cd5de5cc201a`; its manifest verifies every copied file. Existing Git history, caches and private service credentials remain in place.
- [x] Recover the actual public C# result: 28 executed / 28 passed. No Worker success was inferred from the interrupted run.
- [x] Integrate recovered public/all-five-mode and private custom-map sources; C# client 41/41 and unique Worker 98/98 checks pass. Additional multi-seed binding configuration checks are running separately.
- [~] Remove repeated input allocations; gameplay controller navigation and neutral input release are integrated and reviewed, native input/menu verification pending. Cache public map labels by mode/format and normalize Fill for custom modes that do not support it. Additional party-screen controller navigation and workshop focus guards are in progress.
- [~] Improve material/construction detail; preserve editable Blender components while combining static export meshes to reduce Unity renderer overhead. Measure before/after counts and verify all FBX/GLB bounds/grips.
- [~] Bind public admission to the approved map's exact mode and team format. Source repair and negative Worker cases are integrated; new gates await the asset slot. Earlier green Worker results do not validate this later change.
- [~] Present camera settings in clearly labeled Inspector groups, with metre distances and degree angles. Source saved; native appearance and live behavior checks pending.
- [ ] Validate the assembled source, import final content, run native gameplay/public/recovery checks, then build and smoke-test the exact Windows delivery.

There is still no verified Windows player. Public matchmaking remains closed until the final matching catalogue and service are validated and deployed. This recovery does not change those acceptance states.

Root: integration, HUD/player/guide, native verification, content export, coordinated deployment and Windows delivery. Puzzle-race: independent defense review and additive Unity authoring tools. Map-workshop/assets: defense/arena verification and asset quality. Online: protocol v3, all-mode public allocation and private-map sharing.

The bounded Rules run passed 409/410 cases (`TestResults/core_20261023T092813Z.trx`); its only failure was an obsolete schema-2 assertion. The corrected deployment group passes 14/14 (`TestResults/core_20261023T093147Z.trx`), explicitly rejecting both old schema versions. An earlier overbroad filter accidentally started the already-covered flagship seed sweep; it was stopped through verified test-process PIDs and is recorded as a cancelled/failed run, not a pass. The prior 8,000 flagship and 1,000 arena seed evidence remains unchanged.

Final assembled R0 passed in 140.6 seconds (`Logs/gate_check_all_20261023T094106Z.log`), and Online's final Worker 98/98, both TypeScript and six shard/configuration checks pass. Detailed Blender generation now owns the heavy-process slot. A separate usability review found missing party-screen controller navigation and workshop focus protection; bounded repairs are in progress. Native physical/story/profile/authoring batch `editmode_20261020T201950Z` previously failed at its 1,200-second limit during compilation before tests; caches are retained for a 3,600-second retry. Schema-3 service remains deployed with live API 7/7 PASS, preserved credentials/state and unchanged website (`BuildNotes/online/V3-DEPLOYMENT.md`). Public activation, real final multiplayer checks and a tested Windows player are still pending.

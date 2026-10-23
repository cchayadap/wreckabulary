# Wordbound production delivery

Owner: James / SethyPagna. Updated 2026-10-24 (Asia/Hong_Kong).

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
- [x] Expose camera FOV/distance/sensitivity/obstruction settings through validated Inspector assets; native camera-profile regression passes. Windows interaction and human usability remain below.
- [~] Expose equipment visuals/materials/grips and presentation settings without code changes; native original/optional/override presentation checks pass 48/48. Final held/placed/combat appearance remains pending.
- [~] Provide Unity course assets/editor controls for modules/layers/spawns/puzzles/random ranges, with preview/validate/playtest actions; native default asset creation passes, editor interaction/persistence verification remains pending.
- [~] Verify edit/save/reload/playtest, invalid-value errors, old-save compatibility and online content identity. Native authoring persistence passes 8/8, workshop recovery 11/11 and source selection 25/25. Exact native online transfer and Windows interaction remain pending.
- [~] Save reusable prefabs/assets and an illustrated Unity editing guide. The source-verified no-code guide and native default assets are available; final Unity screenshots remain pending.
- [ ] Human usability: four of five new testers create/play a randomized course in ten minutes.

## Models and presentation

**Owner direction, 2026-10-23:** DetailV2 appearance is rejected: old, generic and insufficiently detailed. The reference is the **original Wreckabulary game assets**, not the first Wordbound set. Preserve both Wordbound revisions, compare against the original supplied-item contact sheet and native game captures, and revise a small representative sample before rolling the new treatment across the collection. Existing technical passes remain valid only for their recorded bytes; they do not imply visual approval.

- [x] Produce 12 concepts and editable Blender models: BAT, BROOM, HAMMER, SPEAR, SHIELD, PLATE, TABLE, SOFA, BED, STOOL, FAN, CLOCK.
- [x] Pass 24 FBX/GLB reimports and native presentation checks.
- [~] Inspect actual assets close-up/held/placed/at gameplay distance for believable detail, scale, materials and readability.
- [~] Improve deficient models/materials while preserving editable sources and rendering/collision costs. The original game assets are restored as the default. DetailV2's technical import/mesh checks remain archived evidence for that rejected style, not visual acceptance. SOFA pilots did not improve the original; isolated FAN/HAMMER construction pilots await full multi-view and Unity review.
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
- [~] Public Time Run: exact public entry, two authenticated scripted clients, movement, command and durable acknowledgement pass. Sustained full-match and human acceptance remain open.
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
- [~] Exercise keyboard/mouse/controller, focus loss, cameras, reconnect, return and quit. Controller selection/menu repair, party navigation, workshop focus protection and allocation cleanup are integrated; focused native input passes 24/24. Final player handling and reconnect acceptance remain pending.
- [ ] Profile eight players/active equipment against 60 fps at 1080p Low on Core Ultra 7 + Intel Arc.
- [ ] Recheck affected lobby/wardrobe/Escape/controls/local multiplayer/home-save regressions.
- [ ] Build release player and smoke-test the exact stripped executable.
- [ ] Package build identity, controls, multiplayer instructions, known limitations and Unity editing guide; give James the tested build.
- [ ] Close production acceptance only after required automated, visual and human evidence exists.

Earlier recovery and verification entries are preserved in [the evidence history](BuildNotes/client-view/RECOVERY-AND-VERIFICATION-HISTORY-20261023.md).

## Current verification — 2026-10-23T16:24Z

| Work | State and exact evidence |
| --- | --- |
| Public service | [x] Deployed version `05384420-7e14-40c2-b965-0e8a91681df7`; exact module/catalogue readback, preserved storage/secret names and unchanged website. `BuildNotes/online/public-activation-20261023/readback-verification.json`. |
| Live allocation/admission | [x] Two scripted Duo parties, four authenticated guest identities, exact content and signed start certificates; cleanup successful. `BuildNotes/online/public-activation-20261023/public-live.json`. Isolated pool; no Photon or production-population claim. |
| Service validation | [x] Actual native catalogue: Node 10/10, Worker 41/41, both TypeScript checks. Worker evidence in the activation folder. |
| Pivot/authoring/recovery | [x] Native pivot/detail 6/6, authoring persistence 8/8, workshop recovery 11/11 and existing workshop 9/9. `TestResults/editmode_20261023T125443Z.xml`; entire batch failed because input cases failed. Affected workshop core 23/23: `TestResults/core_20261023T125341Z.trx`. |
| Input | [x] Focused native input 24/24 now pass, including real menu repaint/focus and held-input protection. `TestResults/editmode_20261023T133902Z.xml`. Production input protections unchanged; Windows-player handling remains separate. |
| Workshop source selection | [x] Native source-binding 25/25 pass (`TestResults/editmode_20261023T143652Z.xml`), including the large-course recovery and bounded quiet-frame wait repair. Affected core 66/66 pass (`TestResults/core_20261023T135423Z.trx`). Human usability and Windows-player editing remain separate. |
| Physical puzzles | [~] Latest native run reaches Armed FAN with 359 progress ticks, but the STOOL travels only 0.014 m toward its 0.800 m arrival requirement; the 12-second wait fails. Start-volume and field checks pass. Contact/solver isolation is pending; no sensor, force or deadline relaxation. `BuildNotes/equipment-polish/FAN-NATIVE-MOTION-REVIEW.md`. |
| Original-game artwork | [x] Actual original models are now the default; optional Wordbound equipment remains an Inspector choice and explicit item overrides still win. Native presentation 48/48 pass (`TestResults/editmode_20261023T141231Z.xml`), including all twelve original model bounds/grips and collection opt-in. No owner visual acceptance is implied. |
| Original-based models | [~] SOFA V4 has no convincing improvement; retain the original. FAN rear construction passes internal review of the matched detail pair; other views and Unity remain pending. HAMMER build passes geometric guards but is unrendered. Both editable pilot folders and receipts are committed (`5ced4a4`); neither replaces runtime artwork or has owner approval. |
| Presentation lookup | [x] Native behavior passes 25/25. Calibrated standalone .NET measurement passes 3/3, allocating 42,400 versus 467,680 bytes at 32 bodies, 149,472 versus 6,590,176 at 128, and 300,448 versus 25,763,552 at 256, including context construction. `BuildNotes/online/presentation-probe-20261023.json`. This is pure presentation evidence, not Unity frame-rate acceptance; unsupported native counter failures remain recorded. |
| Validation optimization | [x] Immutable metadata cache and lookup indexes preserve dynamic validation; latest R0 passes and focused core passes 170/170 (`TestResults/core_20261023T161250Z.trx`). Same probe and exact flagship inputs pass 120/120 with positive controls. Indexes further reduce allocation from 565,950 to 523,899 bytes per TimeRun validation and 1,270,324 to 1,190,317 for Team Solo. `BuildNotes/online/validation-indexed-comparison-20261023.json`. Native latency/frame-rate acceptance remains separate. |
| Public modes/build | [~] TimeRun retains its bounded two-client entry/movement/command/durable-ACK pass. Latest Team Solo fails because snapshot chunks arrive after 17,972 ms against the 10,000 ms bound; identity/count/length/hash match and no duplicate chunk is detected. `BuildNotes/online/native-transfer-wind-diagnostics-20261023.json`. Tick-batch diagnostics are in preparation. Duo/Squad, remaining modes, private transfer, recovery, full matches and Windows delivery remain required. |

Latest native diagnostic batch is 0/2 PASS, 2 FAIL, zero skipped (`TestResults/editmode_20261023T160217Z.xml`); its failures are retained above. The subsequent index optimization passes source/core checks and the exact comparative probe; native testing remains pending. QA scenarios select isolated storage before workshop recovery detection. No verified Windows player or owner visual approval is claimed.

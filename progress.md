# Wreckabulary: Wordbound — production progress

Owner: James / SethyPagna. Updated 29 October 2026 (Asia/Hong_Kong).
Goal: deliver the complete [approved plan](docs/wordbound/PLAN.md): a tested Windows game with five Wordbound modes, an evolving four-destination/four-layer/three-lap world, physical word puzzles, human online play and recovery, a usable workshop, vivid editable assets and ordinary Unity design work without code.
The owner's extension also requires online support for the original multiplayer house modes. The full production goal remains unfinished.
Legend: `[ ]` unfinished; `[~]` partly implemented or not fully verified; `[x]` complete only within the stated evidence scope.

## Current visual correction — 29 October

- [x] **Saved implementation:** shared editable charcoal palette, Roboto, compact Workshop icon tools and original object thumbnails/meshes; full-width Play without a character or decorative stripes. All three Solo map cards now show actual map captures with larger image areas. Settings hides the full character. Original assets and earlier rejected designs remain preserved.
- [x] **Parcel Service:** a separate editable Service / Sorting / Dispatch course, with 21 original gameplay props, 90 optional letters and three seeded parcel placements. Red/oak desks, walnut storage, blue rooms and eight original wall fixtures are saved in course-specific materials and prefab groups. [Unity editing guide](BuildNotes/visual-coherence-20261029/UNITY-EDITING.md).
- [x] **Full current local routes:** native `210548` passes both complete tests after the spawn/fixture changes: Time Run three laps, six crafts, 30 letters and 12 objectives; Freestyle all six stations plus rejection of an ordinary map table at a crafted-item socket. Both clean up successfully. These are scripted local checks, not human-controller or online acceptance. [Exact evidence](BuildNotes/visual-coherence-20261029/map-composition/physical-play/NATIVE-GAMEPLAY.md).
- [x] **Focused checks:** combined native `204900` passes 92/92 for source/persistence, materials, dressing, HUD and Workshop interaction. Map-card sizing `203948` passes 18/18. Final Workshop material/selection checks `214922` pass 13/13. Saved fixture creation/repeat and exact three-image import pass with backups; earlier failed assertions remain preserved.
- [x] **Workshop and preview correction:** selected rooms keep their original Lit wood floor with a thin coloured border. Wall decoration is omitted from the wall-free editing cutaway and retained in full preview/playtest. All three map cards use real captures with soft shadows; two darker alternatives were compared and retained without importing them.
- [~] **Actual visual review:** all 63 final Windows screenshots were opened across three reviewers. No new blocking clipping or missing-art regression was found in these captured states. Sparse rooms, weak first-action guidance, pale Workshop props, excess Shop/Loadout space and advanced Workshop text remain. The detailed lobby still sets a richer expectation than the maps. Neither this review nor technical passes establish owner visual approval.
- [~] **More maps, equipment, skins and story remain in scope.** Parcel Service is one compact added course; it does not complete the larger four-destination/four-layer flagship or the full content plan.
- [~] **Creator:** existing map manipulation, random zones, layers, templates, exact playtest/return and Undo are retained. Map + settings export/import has 35 rules / 48 Unity checks. Finish visual objective/link pickers, team/scoring/victory-rule authoring, Share input/restart testing and separate-client custom sessions; custom maps are not automatically admitted to public matchmaking.

## Current Windows technical checkpoint

- [x] Build **20261028T215158Z**, source **4ee1d37a4e64751e093d136dd52a6d99be7aa53f**, Unity **6000.6.4f1**. All five Windows scenarios pass and exit normally: lobby `215517` 5/5, original Solo `215536` 4/4, original Workshop `215600` 5/5, Parcel Solo `215618` 3/3 and Parcel Workshop `215349` 4/4. [45-image audit](BuildNotes/visual-coherence-20261029/windows215158-audit.json) · [18-image audit](BuildNotes/visual-coherence-20261029/windows215158-parcel-audit.json).
- [x] **4,306 authored files unchanged** after build and all five runs. [Preservation receipt](BuildNotes/visual-coherence-20261029/windows215158-preservation.json). Production source/assets are committed; prior uncommitted drafts and rejected candidates remain preserved in the checkout and verified recovery snapshots.
- [x] Root opened all three current Parcel gameplay views: the earlier wall-squeezed giant-head camera is corrected, with the whole character and surrounding room visible. Gameplay HUD uses readable neutral panels. Source fix moves only Start0 and retains the other seven spawns and camera collision.
- [x] **Review build ready:** [Try this Windows build](BuildNotes/client-view/TRY-WORDBOUND.md) and [open the actual screenshot gallery](BuildNotes/visual-coherence-20261029/review/windows215158/REVIEW.html). The gallery contains 63 game screenshots and two matched map images. [Lobby review](BuildNotes/visual-coherence-20261029/review/windows215158-lobby.md), [original Solo/Workshop review](BuildNotes/visual-coherence-20261029/review/windows215158-world.md) and [Parcel review](BuildNotes/visual-coherence-20261029/review/windows215158-parcel.md) record remaining concerns. Local file references and JavaScript syntax pass; automated browser rendering was not run because the browser tool rejects file URLs. Earlier failed/rejected checkpoints remain available.
- [ ] Final owner visual/feel approval, physical keyboard/mouse/controller, sustained separate-PC multiplayer/recovery, original House online modes, performance and production release acceptance remain unfinished. The detailed backlog below remains authoritative; no percentage or production-ready claim is inferred from these local passes.

## Earlier rejected menu revision — retained checkpoint

- [x] Build **20261028T155755Z**, source **75b02aff8e7b27e4e1c45e9444c7225a5d4841dd**, compiled successfully. Windows lobby **155953: 5/5**, Solo **160051: 4/4**, Workshop **160232: 5/5** all pass and exit normally. The original uppercase-label QA failure from build154606 remains preserved.
- [x] New captures include **24 lobby + 13 Solo + 8 Workshop images**. Lost Address Time Run/Freestyle and Parcel Hall Freestyle launch the real local game and return to the chooser. These checks do not establish a full course finish, physical input or multiplayer acceptance.
- [x] Exact post-run preservation comparison: **4,228 authored files unchanged**. [Receipt](BuildNotes/redesign-20261028/windows155755-preservation.json).
- [x] The [strict 45-image evidence audit](BuildNotes/redesign-20261028/windows-audit/windows155755-root.json) passes for that exact build and those three runs. Validator regressions pass **62/62**. This checks provenance and the scenario contract, not approval of all pixels.
- [~] [Try the checkpoint build](BuildNotes/client-view/TRY-WORDBOUND.md) and [compare the screenshots](BuildNotes/redesign-20261028/review/REVIEW.html). James rejected this newer styling and prefers the earlier visual identity with improved organization. Sparse environments, Equipment scrollbar/preview contrast and final visual/feel approval remain open.
- [~] Each final raw player log retains four unsupported/stripped depth-of-field/Panini diagnostics even though scenario console counters are zero. Those postprocessing passes do not execute. This remains a release limitation, along with the unchanged trial label.
- [~] Original PNG pixel checks confirm visible Online/Close glyphs in the disputed header images; the initial displayed-image impression did not establish a game rendering defect. No speculative source change was made.
## Previous tested Windows review build

- [x] Native saved-frame checks: Editor **27/27**, combined Play **31/31**, zero skips; saved setup/repeat pass. Independent rehash verifies **4,208** authored files unchanged against the post-creation baseline. [Root receipt](BuildNotes/client-view/lobby-frames-verification-20261027.json), [independent review](BuildNotes/client-view/independent-native-220430.md).
- [x] Save editable Play/Settings frames; show accurate House/Wordbound control legends with a fixed family title; position the character beside compact Solo/Maps panels. Native tests cover focus, rebind cancellation, layout, family return and projected character bounds.
- [x] Review build **20261026T232801Z**, source **ec4b127b23c9d084aa281f2248a362d92b313399**, Unity **6000.6.4f1**: build passed in 139 seconds; lobby **233250** passed 5/5, Solo **233412** passed 3/3 and workshop **233042** passed 5/5. All exit normally. Strict lobby/Solo audit and separate workshop audit pass; 4,208 authored files remain unchanged against the fresh exact-source prebuild baseline. [Lobby/Solo receipt](BuildNotes/client-view/windows-lobby-solo-232801.json), [workshop audit](BuildNotes/client-view/windows-workshop-232801-verification.md).
- [x] Independently inspect all **39 current images**: clear character framing and control legends, original Shop/Customize art, readable workshop actions and visible range controls. [Lobby/Solo review](BuildNotes/client-view/windows-232801-lobby-solo-visual-review.md), [workshop views](BuildNotes/client-view/WORKSHOP-REVIEW-232801.md), [try this build](BuildNotes/client-view/TRY-WORDBOUND.md). Owner visual/feel approval remains separate. The [earlier before/after viewer](BuildNotes/client-view/UI-REVIEW-221541.html) retains its historical build.
- [~] Scenario console counters are zero, but the workshop raw player log retains four unsupported/stripped postprocessing-shader diagnostics. These passes do not execute. Build provenance reports dirty=true; the audit separately verifies matching current production sources and records the limits of untracked/ignored inputs.
- [~] A small **“trial version”** label is visible at the lower right of final images. Its origin is unconfirmed; investigate it as a release limitation without assuming a licensing cause. No removal has been attempted.
- [ ] Obtain James's visual/feel approval, physical-input and ordinary Unity editing acceptance. Native tests and scripted smokes do not establish these.

## Clear play choices and minimal interface

- [x] Review official navigation, play-menu and accessibility references, applying clear grouping and consistent return routes while retaining Wreckabulary artwork. [Research and decisions](BuildNotes/lobby-experience-20261026/PLAN.md).

- [x] Local / Online / Create have distinct routes; Home exposes Local Play, Online Play, Customize and Shop. Local party labels describe this PC's devices.
- [x] Direct local Wordbound Time Run/Freestyle and map creation/playtesting preserve original house selections and the invoking device. The fixed-device adapter preserves shared bindings and releases only its owned wrapper on return.
- [x] Original Dibs/Duos have local bot filling. Moving Out, Moving Day and Escape remain local human modes. Wordbound online uses human players; neither menu names nor similar rules make house modes online.
- [x] Compact gameplay HUD, optional Help/Journal and original-art recipe tiles replace repeated instructions. Earlier Windows equipment checks prove one accepted Craft and reject stale readiness/letter/focus actions. [Character/UI evidence](BuildNotes/client-view/windows-character-ui-20261026.json).
- [~] Online entry/party UI is aligned with the editable theme; compact Create, visible Ready/Find match/Cancel and recovery controls have source/native UI evidence. Synthetic capture fixtures do not prove real networking or physical controller use.
- [ ] Finish physical keyboard/mouse/controller navigation, scrolled context, focus loss, reconnect/quit, accessibility/readability and real display-switch Keep/Revert acceptance.

## Complete human multiplayer — highest remaining gameplay risk

- [x] Fusion/Physics setup, Photon authentication and the separate coordinator work. All five Wordbound modes have bounded public allocator, movement, command and durable-acknowledgement evidence; public Freestyle is included.
- [x] Recorded FFA8, TimeRun4 and Freestyle8 capacity checks pass with cleanup. These are authenticated scripted clients in one Editor process, not full matches or separate Windows PCs. [Capacity receipts](BuildNotes/client-view/native-public-capacity-retries-20261026.json).
- [~] The recorded 36-entry public catalogue covers nine mode/format combinations and four seeds. Finish schema/protocol compatibility and exact final catalogue/client/service admission checks; bounded deployment/readback does not establish complete native gameplay.
- [~] Writer-lease completion, checkpoint cadence and authenticated recovery-roster corrections have focused source/regression evidence. Complete native recovery and sustained-match acceptance remain open; preserve earlier startup, suspension and cleanup failures.
- [ ] Complete public full flagship Time Run, Team Race and Brawl Race, all supported Solo/Duo/Squad formats, team isolation and simultaneous finish/timeout/draw rules.
- [x] Complete one public FFA ten-point match with two authenticated scripted clients: 40 contact returns, two crafted hammers, nine respawns, protected-hit/replay checks, both peers' result and the exact downloaded durable-result ACK. Native **224529** passes 1/1 with zero cleanup failures; independent audit verifies all 111 bounded host observations. [Exact result and limits](BuildNotes/completion-20261027/network-draft/durability-flow/final-224529.md).
- [ ] Finish FFA remote-attacker, full eight-player, eight-minute timeout/draw and adverse-network acceptance. Earlier contact-return failures remain preserved; the successful diagnostic run did not change production code, and durable pauses still occur. [Full-goal audit](BuildNotes/completion-20261027/scope-audit.md).
- [ ] Verify sustained public Freestyle practice/rewards with no damage, timer or winner; retain its valid solo and private paths.
- [ ] Prove whole-party readiness/admission, private codes, Fill On/Off, concurrent last slots, oversized/undersized parties, expired grants, cancellation and partial-party failure across the supported formats.
- [ ] Force host loss/reconnect during pickup, craft, lap and final delivery; assert exact survivor identities and no duplicate inventory/objectives/rewards. Verify ten-second recovery and two-second rewind targets with repeated elections.
- [ ] Test separate Windows clients and 2/4/8 peers at 0/100/150 ms RTT plus packet loss, including simultaneous grabbing, pickups and moving-platform boarding. Never substitute bots, private allocation or solo play for public evidence.
- [~] Exact private workshop-map download and canonical play have a bounded two-peer pass. Finish automatic upload/download, validation, host-departure retention and a full custom-map session.
- [ ] Implement the original Dibs/Duos/Moving Out/Moving Day/Escape authority/state/content/recovery bridge and separate-client acceptance while preserving their own rules. [House-mode scope](BuildNotes/restart-20261026/all-modes-online-scope.md).

## Flagship world, puzzles and generation

- [x] Implement the defining loop: explore → smash → letters → spell useful equipment → solve/disrupt → progress. Inventory, deduplication, checkpoints, keepsakes and three-lap result rules exist.
- [x] Author Packing Yard, Kitchen Works, Attic Stacks and Rooftop Garden, four height levels, 34 rooms/modules, 72 puzzle sites and six physical puzzle families. Native mechanism checks and compact two-peer Time Run/Freestyle proofs pass within their recorded courses.
- [~] The complete flagship remains unproven. Traverse all destinations/layers/routes and all three evolving laps using real movement, crafting, placement and sensors; do not inject completion state as a substitute.
- [ ] Verify solo solutions, teams on different laps, clean-race object/resource isolation, Brawl progression protection, keepsake loss/withholding, falls, inventory overflow, wasted letters and repeated recovery without permanent softlocks.
- [~] Resource/entity lifecycle reservations and bounded retirement have focused tests. Verify repeated replenishment and active held/deployed objects during full native sessions under the existing body limit.
- [~] Finish full-session letter targeting/collection/occlusion, Freestyle guide/practice and SHIELD/PLATE direction, raising, movement, wear and breakage checks; retain their existing focused regressions.
- [~] Historical flagship 8,000-seed and arena 1,000-seed sweeps pass on their recorded content. Reconcile final catalogue/factory/rule hashes; rerun affected configurations only when changes justify it, retaining 1,000 seeds per shipped configuration and deterministic replay/import.
- [ ] Measure readable landmarks, route variety and 18–25-minute pacing with players. The 72-site route is a pacing risk; geometry length and test count do not establish enjoyable sessions.

## Workshop — complete creator workflow

- [x] Provide offline 3D selection/manipulation, layers, random zones, validation, route/checkpoint/spawn controls, exact playtest/return and history preservation.
- [x] Add a fresh playable Race Circuit with three physical STOOL laps and renewable supplies. Native traversal confirms normal craft/socket/keepsake steps, durable win and return to the same draft/Undo history. [Starter proof](BuildNotes/completion-20261027/workshop-starter-verification.json).
- [~] Keep Race Circuit, Combat Arena and Freestyle World as distinct starting templates. Finish fresh-creator and private two-peer acceptance for the shipped templates without changing recovered drafts' selected modes.
- [x] Edit an existing zone's X/Z and world-height bounds, quarter-turn choices and floor permissions without changing the draft until Apply; Cancel restores saved values, and an unchanged Apply preserves Redo. Relevant core **89/89** and Editor **35/35** pass. The Windows workshop run preserves exact save/playtest/return/Undo state, with eight actual images reviewed. [Native evidence](BuildNotes/completion-20261027/workshop-range-native.md), [editing instructions](docs/wordbound/CONTROLS-AND-EDITING.md#map-randomness).
- [~] Floor choices are permissions; spawns remain in their owning room. Physical range editing, changed-value Apply in Windows, cross-floor generation and general creator acceptance remain open. Group/Ungroup and Move to floor now have separate readable rows; the earlier clipped images remain preserved.
- [ ] Add explicit geometry-surface snapping where approved; current surface placement is the active horizontal floor. Verify slopes, stairs and locked/hidden layers.
- [ ] Improve module thumbnails and visual puzzle/link pickers with approved original art; keep optional advanced IDs, concise errors and accessible keyboard/controller focus.
- [~] Finish usable preset-graph, route-variant, theme/lighting/sound-zone and narrative-event authoring. Keep approved asset IDs and bounded data; imported map files must not execute scripts or arbitrary downloads.
- [~] Verify drag/rotate/resize/multi-select/group/duplicate, grid snap, layer ghost/lock, pools/count/chance/spacing, seed lock/reroll and invalid constraints through visible controls.
- [ ] Complete save/export/import/restart/recovery, old home-save compatibility and exact online content identity in a fresh player process. In-session Undo history is preserved; disk reopen is not cross-scene Undo history.
- [ ] Have at least four of five new testers create and play a randomized course within ten minutes without developer help.

## Unity editing, organization and saved assets

- [x] Save Home, Shop, Loadout, Play and Settings frames/profiles; Equipment, Character and Household studios; categorized editable variants, fonts and material presets. Workspace shortcuts expose real previews and Inspector assets.
- [x] Provide validated camera, course, equipment/material/grip, expression, story and interface profiles. Existing native persistence/source-preservation checks pass in their recorded scopes. [Workspace plan](docs/wordbound/DEVELOPER-WORKSPACE-PLAN.md), [editing guide](docs/wordbound/LOBBY-EDITING-GUIDE.md).
- [~] Saved frames are editable; their inner mode cards, Settings/rebinding rows, top navigation, online layout, wearable/detail contents and HUD placement remain generated. Finish the approved authoring controls without claiming every screen is freely prefab-edited.
- [ ] Verify ordinary edit → save → close/reopen → Play for camera/framing, zones/puzzles, narrative, materials and previews. Test invalid references/values, source-change guards, typography ownership, original dimensions and preview restoration.
- [ ] Finish illustrated current editing instructions and human Unity usability, including missing-only creation/repeat, compatible old saves and assigned custom profiles.

## Art, skins, equipment and animation

- [x] Restore original Wreckabulary art as the production default and preserve rejected generic variants, concepts and experimental Blender/export sources. More polygons or a generated image do not imply approved game quality.
- [x] Save the derived head-normal correction without altering the original rig/FBX; verify pose/clip bindings and transparent outfit portraits. Original colours and vivid recipe pictures remain visible.
- [x] Correct original BAT/HAMMER grip orientation and review bounded motion/pose captures; save tuned Idle/Hurt facial profiles and confirmed-state reactions. [Holding/expression evidence](BuildNotes/character-experience-20261026/final-native-20261026.json).
- [~] Finish accepted throw/deploy/drop/puzzle/outcome reactions, remote transitions and continuous animation readability across every skin; preserve injury priority, cancellation and original skeleton/weights.
- [~] FAN07 construction, source/exchange checks and matched render review pass. Optional Unity pilot intake, hand contact, motion and final promotion remain unfinished; original FAN stays the default.
- [~] Packing Club refinements remain optional drafts; import/admission, online outfit initialization and thrown appearance need completion before claiming selectable online skins.
- [~] Finish visual Shop/Loadout collections, concise categories, previews and clear owned/equipped states. Preserve existing earned-coin purchase rules; cosmetics must not change gameplay statistics.
- [x] Preserve supplied licensed Kenney/KayKit sources and inventory 193 static designs. Household Studio has eight prefabs/materials and repeat/preservation proof. These packs provide no authored rigs/animation or detailed PBR maps.
- [ ] Finish Household materials, seven missing prop colliders, parcel dynamics where needed, clearance/LODs and playable-map registration. Review vivid destination kits, landmarks, furnished rooms and larger-map traversal.
- [ ] Finish the twelve-asset batch: BAT/BROOM/HAMMER/SPEAR, SHIELD/PLATE, TABLE/SOFA, BED/STOOL, FAN/CLOCK. Verify editable source, provenance, scale/origin/Grip_R, icons, materials, gameplay colliders and held/placed/thrown/used/destroyed states; disabled ROPE/MAGNET remain concepts.
- [ ] Produce varied fully painted wearable collections; check seams, weights, deformation, motion/collision and close/gameplay/rear/underside views. Keep cosmetic dimensions/stats identical and use compatible versioned online catalogue admission.
- [ ] Obtain James's comparison and final visual/feel approval for the playable assets, maps, Shop and Loadout.

## Lost Address story and player experience

- [x] Write the three-chapter plot: find the address, follow the wrong delivery, then put home back together. Review first-time/expert, mixed-skill, silent-public, solo, accessibility/language and disruptive play. [Story design](docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md).
- [x] Implement chapter/clue profiles, discovery/journal and six confirmed-result epilogues with native team/reconnect/tie/timeout safeguards. These are result presentations, not six completed branching campaigns.
- [ ] Implement four optional cooperative restoration tasks and versioned authoritative state, preserving solo validity, team isolation and recovery; then verify the richer restoration ending.
- [~] Introduce the story through environmental changes, motion and short contextual cues, with detail in the optional journal. Finish designer previews and actual full-course readability, contribution and pacing checks.
- [ ] Resolve repeated shared-practice changes and active keepsake withholding with tested player-facing rules; competitive matches must not stop for mandatory narrative scenes.
- [ ] Measure the [experience rubric](docs/wordbound/GAMEPLAY-EXPERIENCE-RUBRIC.md), comprehension and replay appeal. Age classification is unassigned; keep run feedback/map feedback distinct from official age ratings, store scores and persistent rankings.

## Performance, regression and production release

- [ ] Profile a populated full map with eight real networked players, active equipment and gameplay physics on Core Ultra 7 / Intel Arc: target 60 fps at 1080p Low, record frame-time/memory/entity costs, then optimize measured bottlenecks.
- [~] Synthetic render workloads and earlier timing/focus comparisons remain useful scoped evidence, not proof of full multiplayer performance or a controlled speedup.
- [ ] Recheck affected lobby, wardrobe colours, Escape, local multiplayer, controls and furnishing-save regressions; verify ordinary launch, focus loss, cameras, return, reconnect and quit on the final candidate.
- [ ] Complete separate-machine multiplayer, physical controls, real display rollback, human creator/usability, accessibility and owner visual acceptance.
- [ ] Build and smoke the exact stripped production executable, package source-matched assets/licences/instructions, and publish the final review branch/PR with its evidence attached.
- [ ] Close production acceptance only after the approved gameplay, online/recovery, workshop, asset, performance and human gates pass; a QA build is not the finished release.

## Preservation and next actions

- [x] Preserve James/ICY/Sethy histories, isolated worktrees, original main/colour/Escape directions, unfinished Lunar work, rejected art, failed runs and uncommitted drafts. Other heavy jobs remain running as requested.
- [~] Keep coherent green commits, exact source/build hashes and recoverable snapshots; root serializes this task's native jobs. The previous progress file is preserved intact in the [history snapshot](BuildNotes/client-view/PROGRESS-HISTORY-BEFORE-221541.md); older successes and failures keep their original scope.
- [ ] Next: finish full flagship/recovery and remaining FFA cases, original-house networking, creator geometry/link tools, richer content and measured performance. The review-build overlay, owner/physical-input follow-up and final production acceptance remain open.

The earlier 220835 candidate passed game smokes but its preservation-baseline commit check failed; the [rejection](BuildNotes/client-view/delivery-220835-provenance-rejection.json) remains recorded. Corrected 221541 and current 232801 deliveries use fresh exact-source prebuild baselines. Intermediate 231729 passed workshop behavior but exposed clipped action labels; its [review](BuildNotes/client-view/windows-workshop-231729-visual-review.md) remains unchanged.

Detailed history remains in the [full-goal audit](BuildNotes/completion-20261027/scope-audit.md), [earlier verification history](BuildNotes/client-view/VERIFICATION-HISTORY-BEFORE-035028-20261024.md), and the linked exact-run receipts. Historical passes and failures keep their original source/build scope.

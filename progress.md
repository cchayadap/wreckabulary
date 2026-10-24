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
- [~] Provide Unity course assets/editor controls for modules/layers/spawns/puzzles/random ranges, with preview/validate/playtest actions; native default asset creation passes, editor interaction/persistence verification remains pending.
- [~] Verify edit/save/reload/playtest, invalid-value errors, old-save compatibility and online content identity. Native authoring persistence passes 8/8, workshop recovery 11/11 and source selection 25/25. Exact native online transfer and Windows interaction remain pending.
- [~] Save reusable prefabs/assets and an illustrated Unity editing guide. The source-verified no-code guide and native default assets are available; final Unity screenshots remain pending.
- [ ] Human usability: four of five new testers create/play a randomized course in ten minutes.

## Models and presentation

**Owner direction, 2026-10-23:** DetailV2 appearance is rejected: old, generic and insufficiently detailed. Use the **original Wreckabulary game assets** as the reference and runtime default. Preserve both Wordbound revisions and experimental pilots. No further generation is underway. Check the original assets in actual held, placed and gameplay views; earlier import checks do not imply visual approval.

- [x] Produce 12 concepts and editable Blender models: BAT, BROOM, HAMMER, SPEAR, SHIELD, PLATE, TABLE, SOFA, BED, STOOL, FAN, CLOCK.
- [x] Pass 24 FBX/GLB reimports and native presentation checks.
- [~] Inspect actual assets close-up/held/placed/at gameplay distance for believable detail, scale, materials and readability.
- [x] Restore the original game assets as the default and preserve the rejected variants. The2157 neutral Unity preview shows all four representative originals and visible held-BAT contact; actual Windows220124 shows the original character/world. This is agent inspection, not owner visual approval.
- [x] Restore the original cream/teal/Nunito menu style through an editable Unity theme; correct panel contrast, multiline rows, Search wrapping, footer status and party consistency. Native232239 passes all96 relevant UI/input/workshop cases, including theme9 and party7; actual Windows232850 confirms these visible corrections. Further compact layout and human usability work remain open.
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
- [~] Public Team Race: two authenticated Solo parties pass entry, remote movement, command and exact durable acknowledgement in both native203305 and203605. Duo/Squad, full matches and clean-team isolation acceptance remain open.
- [~] Public Brawl Race: Solo221108 passes bounded public entry, remote movement, command replay and exact durable acknowledgement in56.071s. Earlier transfer/movement failures remain recorded; repeat reliability, Duo/Squad, combat and full match remain open.
- [~] Public FFA: two authenticated scripted individuals pass public entry, remote movement, command and exact durable ACK in213856. Actual scoring, respawns, full outcomes and capacity remain open.
- [~] Public Freestyle: two authenticated scripted players with Fill enabled pass public entry/movement/command/ACK in213856. Shared practice/rewards/no teammate damage separately pass private212947. Sustained public practice and capacity remain open.
- [ ] Prove public allocator use without private-code substitution, bots or silent solo fallback.
- [ ] Verify automatic custom-map upload/download, exact content validation and retention after host departure.
- [ ] Verify full four/eight-player flagship gameplay, reconnect and automatic host replacement.
- [ ] Exercise 2/4/8 clients at 0/100/150 ms RTT and packet loss.
- [ ] Force host loss during pickup/craft/lap/final delivery; check no duplicates and measure ten-second recovery/two-second rewind targets.

## Tested Windows delivery

- [x] Create/read back Windows release, QA and development profiles.
- [x] Produce QA Windows builds: latest `20261024T002136Z` passes146s on exactf1b20833; includes original assets, the corrected compact party card and the latest mechanism-allocation optimization. Final stripped release remains separate.
- [~] Visible workshop→game→workshop002434 passes5/5: playtest13.767s, exact save/return/draft/Undo preserved, console0/0/0, exit0. Six actual screenshots show the compact card, original artwork and readable workshop/equipment menus. The measured90-update window takes9.371s with603 canonical ticks and an895.562ms maximum update gap; smoothness remains below acceptance. Supplemental normal-window diagnostic only; official smoke and sustained frame rate remain open. `BuildNotes/client-view/windows-original-party-fit-20261024.json`.
- [~] Exercise keyboard/mouse/controller, focus loss, cameras, reconnect, return and quit. Controller selection/menu repair, party navigation, workshop focus protection and allocation cleanup are integrated; focused native input passes 24/24. Final player handling and reconnect acceptance remain pending.
- [ ] Profile eight players/active equipment against 60 fps at 1080p Low on Core Ultra 7 + Intel Arc.
- [ ] Recheck affected lobby/wardrobe/Escape/controls/local multiplayer/home-save regressions.
- [ ] Build release player and smoke-test the exact stripped executable.
- [ ] Package build identity, controls, multiplayer instructions, known limitations and Unity editing guide; give James the tested build.
- [ ] Close production acceptance only after required automated, visual and human evidence exists.

Earlier recovery and verification entries are preserved in [the evidence history](BuildNotes/client-view/RECOVERY-AND-VERIFICATION-HISTORY-20261023.md).

## Current verification — 2026-10-24T00:27Z

The original game artwork remains the default. Windows QA build `20261024T002136Z` and its visible workshop/playtest/return run `002434` pass all five workflow checks. Root inspected all six images: the party panel fits its content, and original cream/teal/Nunito menus, Search, footer status and workshop guidance remain readable. Independent image review is in progress. See [the current build instructions](BuildNotes/client-view/TRY-WORDBOUND.md).

- [x] All 13 Unity authoring persistence cases pass in `233437`, including actual changed-save/BOM handling, retained drafts, custom themes, course assets and scene restoration. The earlier intermittent Windows replacement error remains unexplained; no new production IO workaround was added.
- [x] Capacity preflight `233231` passes with 512 reserved / 366 initial live bodies using registered PLATE props. This is validation before live allocation, not a maximum-capacity gameplay result.
- [x] Mechanism identity optimization passes R0 and 198 focused core cases (`234828`). The unchanged standalone probe preserves every input/snapshot hash and passes 120 validations before and 120 after. Median managed allocation falls 7.98% for Time Run and 8.58% for Team Race Solo. These are .NET measurements, not Unity frame-rate results.
- [x] Native `000038` passes all eight checkpoint diagnostic cases, all 20 timing-collector cases and all nine theme cases. Its total is 45/47 because the narrow party fixture and public Team Race Solo fail. The record separates synthetic diagnostics from the exact live public case.
- [x] Repair compact party-card feedback: native `001905` passes all 19 party/theme cases, including actual narrow/wide layout convergence, first/last control clipping checks, resize, long-error overflow, wheel scrolling and shrinking. Fitted content draws directly; true overflow reserves scrollbar width. Prior `000747` failure remains preserved. Windows visual verification follows the next build.
- [~] Full FFA `233437` reaches six scored knockouts and respawns, then the eight-minute fixture lifetime expires. Its final return operation lasts 5.999 seconds, below its separate 35-second limit. No ten-point result is accepted. The 162 allocator samples show no observed OOM; stable late object capacity does not prove leak freedom or maximum capacity.
- [~] Checkpoint measurements are now available: 12 exact accepted publications, mean 1,075 ms from publication to ACK and mean 1,417 ms from ACK to next publication. These are inclusive observations, not isolated network or controller-idle time. All requests succeed. Cadence is unchanged because increasing it could add CPU/request cost.
- [~] Isolated public Team Solo `001235` still fails its unchanged movement deadline. The client's latest confirmed server tick reaches 1467, matching the host, while its own simulation/input ticks remain 1268/1249. Unity updates continue, but no client simulation batch completes for approximately 5.163 seconds before observation stops. This is receive-progress evidence, not proof that complete snapshots or host motor input arrived. Installed SDK scheduling/buffer conditions are under read-only investigation; timing and capacity settings remain unchanged. See `native-public-frontiers-20261024.json` and `PUBLIC-MOVEMENT-234950-REVIEW.md` under `BuildNotes/client-view/`.

Actual Windows frame observation still shows substantial stalls: 90 updates in 9.371 seconds, 603 canonical ticks and an 895.562 ms maximum update gap. The assigned camera records 89 matched callback pairs; those are not GPU completion or presented-FPS measurements. This fresh run does not establish a frame-rate improvement. Larger parties, full-match outcomes, recovery targets, final release and human acceptance remain open.

### Earlier verified checkpoint — 22:47 UTC

Initial-bootstrap native223028 passes58/60: all57 OnlineTests and actual host-departure recovery. Every initial host waits for exact imported bootstrap acknowledgements before its first canonical advance. Recovery18.762seconds misses the ten-second target. PublicTeam/Brawl fail a subsequent temporary-pause assertion; reviewed test repair keeps the existing25-second deadline and strict checks. Online heap capacity is now64MiB per heap with actual solo/local8MiB preserved; fullFFA and maximum-body execution remain pending. Evidence: `BuildNotes/client-view/native-bootstrap-barrier-20261023.json`, `native-ffa-memory-20261023.json`, `BuildNotes/online/FUSION-ONLINE-HEAP-POLICY.md`.

The original-style editable UI theme is integrated and source-reviewed, with Unity default-asset creation underway. New resource tuple identity validation passes223/223 focused core224523 after correcting one invalid test fixture. Fixed-workload validation allocation medians fall16.7683%TimeRun and18.3594%TeamSolo; elapsed results are mixed. Native UI/configuration checks and a new Windows build remain next. Exact evidence: `BuildNotes/client-view/resource-identity-probe-20261023.json`. Earlier executable215906 does not contain these changes.

Capture-key sourcec298d77d passes218/218 core220731; identical120capture probe reduces median allocations70.265%TimeRun/72.481%TeamSolo with exact unchanged hashes. Native221108 passes48/49: codec26, timing20 and publicTeamSolo/BrawlSolo pass. FullFFA fails on Fusion allocator exhaustion during first craft; later first real hit/replay succeeds before a return walk expires. Ten knockouts, respawns and terminal result remain unverified. A separate diagnostic rerun samples allocator occupancy without increasing its capacity or deadlines. Evidence: `BuildNotes/client-view/native-capture-ffa-20261023.json`. Actual Windows220124 passes the complete five-step workshop flow, with UI/art/performance limits recorded separately.

Native213856 passes2/3 public cases: FFA and Freestyle PASS; BrawlSolo FAIL on stale transfer age, with cleanup0 and no crash-after-quit. Evidence: `BuildNotes/client-view/native-public-three-20261023.json`. The test-only Item fixture correction now passes192/192 core215023. The identical120-validation probe allocates14.235% less TimeRun memory and30.996% less TeamSolo memory, with slower standalone timings. Native215152 passes21/23: timing20 and sharedFreestyle pass, but Brawl misses movement deadline and TeamSolo rejects stale initial transfer15,006ms/10,000ms. No throughput improvement or production acceptance is claimed.

| Work | State and exact evidence |
| --- | --- |
| Public service | [x] Deployed version `05384420-7e14-40c2-b965-0e8a91681df7`; exact module/catalogue readback, preserved storage/secret names and unchanged website. `BuildNotes/online/public-activation-20261023/readback-verification.json`. |
| Live allocation/admission | [x] Two scripted Duo parties, four authenticated guest identities, exact content and signed start certificates; cleanup successful. `BuildNotes/online/public-activation-20261023/public-live.json`. Isolated pool; no Photon or production-population claim. |
| Service validation | [x] Actual native catalogue: Node 10/10, Worker 41/41, both TypeScript checks. Worker evidence in the activation folder. |
| Repeated snapshot work | [~] Native191029Z records 764 full captures for 763 advances, versus 1,337 for 360 in181114Z. Host preparation falls from35.782 to12.779 seconds and the maximum observed 60-tick batch from4,746.743 to2,637.959ms. Public transfer still exceeds10,000ms. Different run lengths and inclusive scopes prohibit an FPS or full-success claim. `BuildNotes/online/native-clock-center-admission-20261023.json`. |
| CLOCK field target | [x] Actual collider-centre targeting clears the false floor-endpoint blockage while retaining real wall occlusion. Native geometry regression and the complete six-family physical fixture pass in191029Z. R0 passes; no collision/field/route deadlines were relaxed. Full-match and human feel remain separate. |
| Economy capacity counts | [~] Direct live counts replace a full economy snapshot used only for capacity checking. R0 and 65/65 focused core cases pass. Native192928Z failed before gameplay; the corrected194601 job passes all eight local cases. Visible Windows193821 completes 70/90 frame waits versus 53/90 previously, still FAIL. These separate runs do not establish controlled FPS improvement. `BuildNotes/client-view/windows-capacity-count-comparison-20261023.json`. |
| Full-match completion | [~] Compact private TimeRun native202344 passes1/1 in173.549s (gate216.4s): two authenticated scripted clients, all three laps/nine ordered objectives, fresh crafted/consumed TABLE per lap, native carrier delivery, same confirmed Win and independently downloaded/imported/ACKed durable result. Cleanup passes. Public flagship/full-mode/human/Windows acceptance remains open. `BuildNotes/client-view/native-compact-full-match-20261023.json`. |
| Final result display | [x] Same-tick durable result confirmation now invalidates the cached host frame. R0 and 36/36 focused authority cases pass (`TestResults/core_20261023T194255Z.trx`). Compact private three-lap native202344 also verifies matching confirmed results; flagship/public acceptance remains open. |
| Startup collision registration | [x] Incremental registration preserves immediate native collision permissions and guards aborted/retried composition. All ten local/atomic cases pass in200052, including new real-collider pair checks. This does not establish a startup/FPS guarantee. `BuildNotes/online/COLLISION-PAIR-REGISTRATION.md`. |
| HUD and world labels | [x] Compact wrapped panels, actual binding hints and readable checkpoint/story captions pass53/53 native cases. The actual200812 Windows capture was inspected independently and shows improved static readability with the original character. Interactive controller/owner acceptance remains open. `BuildNotes/client-view/HUD-READABILITY-IMPLEMENTATION.md`. |
| Steady gameplay cost | [~] Three internal membership checks avoid unnecessary sorted puzzle copies. Per-call retirement indexing preserves letter multiplicity, spending, generations and reset order. R0 and167/167 focused rules cases plus36/36 entity-budget/practice cases pass (`TestResults/core_20261023T201209Z.trx`, `core_20261023T201805Z.trx`). Native201239 still fails public movement; aggregate timings do not isolate causes or establish FPS. QA201839 builds successfully; visible202045 still fails75/90 waits. Fourteen opt-in phases now identify body validation as the largest full-validation child. The mechanism lookup passes159 core cases but210425 fails movement;212947 later passes all22cases. BodySource accounts for1190.839ms of1599.910ms in the public body-validation child. The source index passes corrected192core cases and reduces standalone allocations; native215152 remains21/23 due public movement/transfer deadlines. Subsequent capture-key candidate passes218core cases; native and Windows impact are pending. |
| Build size audit | [x] Measured optional equipment at 5,344,466 packed bytes (5.10 MiB), despite original artwork default. Editable sources are not shipped. Exclusion needs loader/profile compatibility work and is deferred until gameplay is stable; no deletion or memory/FPS claim. `BuildNotes/client-view/BUILD-ASSET-SIZE-REVIEW.md`. |
| Pivot/authoring/recovery | [x] Native pivot/detail 6/6, authoring persistence 8/8, workshop recovery 11/11 and existing workshop 9/9. `TestResults/editmode_20261023T125443Z.xml`; entire batch failed because input cases failed. Affected workshop core 23/23: `TestResults/core_20261023T125341Z.trx`. |
| Input | [x] Focused native input 24/24 now pass, including real menu repaint/focus and held-input protection. `TestResults/editmode_20261023T133902Z.xml`. Production input protections unchanged; Windows-player handling remains separate. |
| Workshop source selection | [x] Native source-binding 25/25 pass (`TestResults/editmode_20261023T143652Z.xml`), including the large-course recovery and bounded quiet-frame wait repair. Affected core 66/66 pass (`TestResults/core_20261023T135423Z.trx`). Human usability and Windows-player editing remain separate. |
| Physical puzzles | [x] Native191029Z completes all six authored families with actual sensors and canonical checkpoints in258.311s; the isolated CLOCK floor/wall geometry case passes in1.314s. Earlier FAN contact/material, motion/policy and local/atomic checks remain valid. This fixture does not finish three laps or prove human traversal/feel. `TestResults/editmode_20261023T191029Z.xml`. |
| Original-game artwork | [x] Actual original models are now the default; optional Wordbound equipment remains an Inspector choice and explicit item overrides still win. Native presentation 48/48 pass (`TestResults/editmode_20261023T141231Z.xml`), including all twelve original model bounds/grips and collection opt-in. No owner visual acceptance is implied. |
| Original-based models | [~] SOFA V4 has no convincing improvement; retain the original. FAN rear construction passes internal review of the matched detail pair; other views and Unity remain pending. HAMMER build passes geometric guards but is unrendered. Both editable pilot folders and receipts are committed (`5ced4a4`); neither replaces runtime artwork or has owner approval. |
| Presentation lookup | [x] Native behavior passes 25/25. Calibrated standalone .NET measurement passes 3/3, allocating 42,400 versus 467,680 bytes at 32 bodies, 149,472 versus 6,590,176 at 128, and 300,448 versus 25,763,552 at 256, including context construction. `BuildNotes/online/presentation-probe-20261023.json`. This is pure presentation evidence, not Unity frame-rate acceptance; unsupported native counter failures remain recorded. |
| Validation optimization | [~] Immutable metadata/resource indexes have measured allocation reductions; the reviewed atomic reconciliation now passes R0 and combined core 233/233 (`TestResults/core_20261023T173734Z.trx`), including 25 new mutable-state/abort cases. All eight native local/atomic cases now pass (`TestResults/editmode_20261023T174133Z.xml`); public Team Solo comparison181114Z still fails matching snapshot transfer at14,766ms against10,000ms. Full multiplayer/performance acceptance remains pending. Prior resource indexes reduce allocation 13.07% for TimeRun and 11.29% for Team Solo versus the preceding indexed version; uncontrolled elapsed timings do not establish speed/FPS improvement. `BuildNotes/online/validation-resources-comparison-20261023.json`. |
| Public modes/build | [~] Public TeamSolo passes bounded entry/movement/command/exact ACK in203305,203605,205256 and212947. After mechanism lookup,210425 fails the unchanged movement deadline; its20 timing cases pass and cleanup succeeds. No established speedup or reliable/all-mode/Windows acceptance. `BuildNotes/client-view/native-validation-children-20261023.json` and `native-body-lookup-20261023.json`. |
| Original surface/camera presentation | [x] Native211545 passes24/24: metre-scale default floor/wall/sloped connector UVs, owned-mesh cleanup, unchanged custom materials/physics and original rendering settings on owned runtime cameras. Supplied cameras remain unchanged. Actual Windows/owner appearance acceptance remains open. `BuildNotes/client-view/native-original-presentation-211545.json`. |

Native191029Z passes the six-family physical route and collider-centre regression. The later200052 batch passes63/65: ten local/atomic and53 UI/input/guide/story checks; both online movement cases fail, with cleanup successful. Compact private TimeRun subsequently passes all three laps in202344; full flagship and remaining modes stay open. Retained exact evidence: `BuildNotes/client-view/native-collision-readability-20261023.json`. Full-match gaps remain mapped in `BuildNotes/client-view/FULL-MATCH-ACCEPTANCE-REVIEW.md`.

Latest QA build212131Z passes118.8s; visible212401 reaches readiness5.449s but fails76/90 update waits. Exact save, return/history and Undo/Redo pass; console0/0/0, no observed native crash. Root and independent reviewer see correct discrete floor/ceiling boards and original avatar colors/shading. Timing windows close validly; performance remains unacceptable. `BuildNotes/client-view/windows-original-presentation-20261023.json` binds23 actual artifacts. The unchanged wait remains90frames/90s; no controlled speedup or owner visual acceptance is claimed. The reviewed two-file official smoke-launch fix awaits bounded owner approval; the isolated Unity6000.6.4 installer awaits actual Windows approval. Neither kit source nor editor version changed. Original artwork remains default. No tested Windows delivery or owner visual approval is claimed; previous failures remain preserved.

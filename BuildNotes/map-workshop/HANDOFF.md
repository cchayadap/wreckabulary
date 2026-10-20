# Workshop lane

STATUS: SOURCE READY FOR INTEGRATION; NATIVE UNITY NOT RUN

Owner: James / SethyPagna. Worktree: `wordbound-map-workshop`, branch `sethypagna/map-workshop`. Clean base: `12842c75608694f36819edd3bd4dc104ece7f79c`.

Scope: new Rules/Wordbound/Workshop*.cs, Scripts/Workshop/Wordbound* runtime files, matching tests and workshop documentation. Root owns scene/menu entry, shared hooks, metadata and native Unity gates.

Course contracts are supplied by the map-core lane. Draft transactions preserve structurally valid course data while allowing incomplete topology; full validation gates preview/playtest. Selection, groups, floor visibility/locking, grid and preview seed live in a separate `wreckabulary.workshop` recovery envelope. Shared JSON is the strict `wreckabulary.course` document, never HomeLayout.

Implemented immutable snapshots, bounded undo/redo, atomic command execution, module/group movement and rotation with attached point transforms, layer commands, duplicate subgraphs, delete, random zone and puzzle/gate link commands, and atomic autosave/share storage with a previous-save backup.

First rules increment passed 14/14 engine-free tests against map-core commit `2ce1906`: alias isolation, transaction rollback preserving redo, attached-point transforms, floor changes, lock/group behavior, duplicate subgraph references, bounded history, recovery/share separation, seed locking, backup recovery/path rejection, all three template resolution checks and fixed-placement transforms.

The initial template checks exposed an invalid free-text default seed; switched snapshots to the core's required sixteen lowercase hexadecimal digits and reran green. Fixed zones now transform their authored position/yaw with their owning module.

The second increment adds the top-down Unity authoring surface: palette, selection/multiselection/marquee/drag, rotation, groups, floor visibility/locking, weighted random zones and resize handles, fixed placements, approved puzzle/letter setup, connection/gate links, exact-seed preview/playtest event, sharing and recovery. Named keyboard controls and tooltips are included; native interaction/controller/visual acceptance remains outstanding.

Current engine-free result: **17/17 passed** against map-core through `58cac35`. The added cases cover out-of-bounds rollback, invalid probabilistic minimum rejection and recipe/locked-gate rules. Backup recovery also covers a missing primary file. Focused Unity 6000.6.3f1 reference compilation passes with **0 warnings and 0 errors**. These results are not a native Unity test run.

Three native editor tests are supplied for template recovery, explicit-document behavior and playtest/template return fidelity. **NOT RUN in this lane.** Root must run them after import.

Integration order: map-core `2ce1906` and `58cac35`, workshop first commit `3a70be6`, then the second workshop source commit. This branch intentionally does not import the map-core commits; local ignored check projects link their source read-only from the sibling worktree. Existing shared scenes, catalogs, HomeLayout code and metadata are untouched.

Runtime entry: add `Wreckabulary.Workshop.WordboundWorkshop`, then `OpenTemplate(WorkshopTemplate.RaceCircuit)` or `Open(course, catalogue, requirements, folder)`. Subscribe to `PlaytestRequested` for the exact resolved manifest and `CloseRequested` for navigation. Set `Visible = false` during playtest and call `ResumeEditing()` to return. Reopened templates automatically recover saved data; explicit supplied documents remain authoritative. Root owns menu routing and scene/input isolation.

Review fixes: template reopening restores autosave before writes; issue navigation resolves owning modules; hidden floors hide zones; the sharing panel blocks background pointer actions. The validation layer accepts unfinished topology for editing but rejects malformed data atomically. Full resolver validation gates playtest.

Next recoverable action: root integrates both workshop commits after map-core, imports native metadata, runs `Wreckabulary.EditorTests.WordboundWorkshopTests`, and manually exercises drag/undo/floors/zone resize/share/playtest return. Remaining final UX: art thumbnails, free-camera 3D surface editing, advanced route/lap controls, controller focus and novice-user acceptance. Proof templates are not the final flagship map.

## 3D upgrade checkpoint

STATUS: IN PROGRESS. The next source increment replaces the plan-view world with an owned RenderTexture camera and 3D stage, reusing `Surfaces` meshes/textures and `ModelVisual` for approved item visuals. Camera, render target and private material instances are disposed without touching the game camera. Layer 31 and a preview stage at world Y -20000 keep the render-only stage away from live geometry; the integrator should keep the workshop context isolated.

Orbit, pan, zoom, active-floor surface selection, X/Z/plane/quarter-turn handles, marquee selection, floor ghosting, preview-only drag transforms and deterministic placement visualization are implemented. A completed handle/stick drag publishes one history transaction; a changed revision rejects a stale drag. Controller tools provide action navigation, selection, grid movement, orbit/zoom, rotate, multiselect, undo/redo, frame, preview and playtest.

Current checks: **21/21 rule cases pass**. Focused Unity reference compile succeeds with **0 errors**, and two expected CS0436 warnings because it links the already-integrated older Wreckabulary assembly for existing art facilities while compiling this newer workshop class. Five native editor fixtures are authored, including projection/picking and owned-resource disposal, but **NOT RUN** here. Next: improve approved object selection/fixed-object manipulation, polish panel flow, then final source verification and root native gate.

## 3D completion handoff

The follow-up adds independent fixed-object surface picking and snapped move/rotate/duplicate/delete, searchable approved-object buttons, optional icon/model provider hooks, stale fixed-placement rejection, and explicit controller/keyboard help. Fixed-object drags leave room transforms unchanged; invalid bounds roll back. Current rule suite: **23/23 PASS**. Reference compile: **PASS, 0 errors**, with the two explained overlay warnings. Five native fixtures now cover fixed-object picking as well.

Integration sequence after the original two commits: `0730513` (3D viewport/handles/controller), then this fixed-object/picker follow-up. Set optional `SpawnAssetVisual` and `AssetIcon` before opening to bind Wordbound assets. Default visuals reuse ModelVisual; no catalogs or shared art files were changed. Root must test URP render-target output, input, resizing, floors, preview/playtest return and lifecycle in native Unity.

Acceptance remains **NOT RUN** for real controller/mouse interaction, final layout at target resolutions, performance, a packaged build, arbitrary imported-mesh surface picking and the four-of-five novice-user ten-minute exercise. The current geometry is the approved rectangular module contract with floor edges and rendered connections, not the final landmark art. Advanced route/lap authoring remains outside this surface increment.

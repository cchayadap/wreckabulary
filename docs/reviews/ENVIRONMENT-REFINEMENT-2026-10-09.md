# Unity environment refinement

STATUS: VERIFIED

## Goal and checkpoint

Upgrade rooms, architecture, floors, ceilings, sky, sunlight and practical fixtures into a coherent, varied toy-house setting. Preserve gameplay, local multiplayer and editable current scene assets. Verify in native Unity captures and tests, and push verified stages as SethyPagna without co-author trailers.

The previous presentation and authoring checkpoint is preserved in commit `c69253c` and pushed on `codex/environment-refinement`. Its evidence is in `UNITY-AUTHORING-2026-10-09.md`. The checkpoint passed 201 EditMode tests, 135 PlayMode tests, two rendered capture journeys and a Windows build smoke test. These results describe that earlier checkpoint.

Fetching GitHub found 58 newer commits on `origin/main`, ending at `3d28cf0`. They add three vertical maps, a new lobby/HUD, settings, creative tools, updated art and rebinding. Integration uses these newer systems as the base while retaining authored asset persistence, UI state transitions and presentation fixes.

## Ownership and verification

- Camera lane: camera/controls, authored avatar and movement compatibility.
- UI lane: latest lobby/HUD, state transitions, player labels and workspace links.
- World lane: all five maps, authored world reset, persistent generated textures and safe migration.
- Lead: remaining integration, native scene migration, tests, screenshots, Git commits and pushes.

The delivered environment stage is commit `29b2728`, pushed on `codex/environment-refinement`. It includes the five current worlds, Hub/Tutorial dressing, lighting, camera fixes and reviewed native screenshot evidence. The verification record below distinguishes initial failures from their passing affected-case reruns.

## Native integration checks

- Engine-free rules harness: 225 passed, zero failed (`Logs/environment-merge-rules.txt`).
- Native Unity setup: zero compile errors; 98 imported models, 73 materials, 18 textures (`Logs/environment-merge-setup.log`).
- Explicit authoring migration preserved older worlds/scene overrides in `Editor/Legacy` and saved all five current map prefabs. The first run exposed Unity's prohibition on cloning GPU-only textures. The save path now reads pixels through the graphics device with matching color space and sampler settings.
- Native repair converted 39 generated textures in place, retaining GUIDs and all material references (`Logs/environment-merge-texture-repair.log`).
- A separate Unity process passed all 274 EditMode cases, including saved pattern pixels, linear/sRGB readback, mipmaps, five-map persistence and authored-edit preservation (`Logs/environment-merge-editmode.xml`).
- Full PlayMode suite: 251 passed, five integration failures and 12 explicit capture tests skipped. Fixes addressed duplicate fixture audio listeners, workshop CanvasGroup lifecycle, pause crosshair visibility and asset-name-dependent surface assertions.
- Focused regression after those fixes: 36 passed, zero failed. This is an affected-case rerun, not a second full-suite run (`Logs/environment-merge-regression.xml`).
- Graphics-enabled native presentation journeys: two passed, 13 PNG captures at 16:9 and 21:9. Reviewed centered behind-player framing, lobby, stacked gear slots, local couch play and furniture lifecycle. Evidence: `evidence/unity-environment-merge-2026-10-09`.
- The merged-revision Windows build was deferred to the environment stage. That baseline still had plain walls and solid-color sky; final environment/build results appear below.

Recovery snapshot before the texture repair: `Logs/checkpoints/2026-10-09-environment-merge-in-progress` (tracked binary patch, 1,120 untracked files and Git merge identity).

Harness run: `run-aae3cb89-49c7-45fd-a718-6803c2e6c944`.

## Environment implementation

Merged baseline `25724f4` is pushed as SethyPagna. The delivered stage adds room-specific paint, ceiling/roof panels with stair openings, upper wall infill, cornices, imported windows/curtains/sconces, warm bulbs and framed generated artwork. Hub and Tutorial retain their original placements with a rear canopy and added wall detail. The five current prefab worlds remain authoritative.

Lighting is controlled by the saved `SunlitHouse` profile and `DaylightSky` material. A scene-owned sun/fill and a bounded pool of unshadowed practical lights replace the solid teal horizon. The lobby keeps its own showroom background. A review caught and corrected workshop suspension of the lighting controller and late scene-load fog overrides.

Camera cutaways now apply only during the requesting camera's render and restore exact renderer state afterward. They preserve authored wall transforms/meshes, independent upstairs/downstairs views, unregistered cameras and standalone Hub/Tutorial canopies. Ceilings constrain the camera boom without adding gameplay colliders.

The FOAM visual now uses a transparent iridescent shell and seven small pearl meshes, sharing one saved mesh/material. Its original shield ownership, timing and letter accounting remain authoritative.

- Native authoring completed with no C# or shader errors (`Logs/environment-stage-authoring.log`).
- All 12 new saved-world/lighting editor cases passed. Full editor run: 285 passed and one teardown failure; the fallback-light creation during Play exit was removed. The affected Hub Play regression then passed (`Logs/environment-stage-editor-regression.xml`).
- Initial full gameplay verification: 262 passed, one failed and 13 explicit captures skipped. The failure was an active craft refund emitting cosmetic pickup feedback during scene teardown and recreating `Transient`; the lifecycle fix, unchanged resupply case and three new regressions passed in the affected-case run below.
- The first graphics-enabled environment journey wrote all 25 frames, then failed editor resolution cleanup. That cleanup now removes only its own absolute custom-size indices and verifies restoration; the expanded 26-frame rerun passed.
- Visual review found real defects not exposed by visibility-plan tests: GPU Resident Drawer retained roof geometry in overview/couch cameras, and imported window/sconce root transforms lost the FBX axis conversion. Runtime cutaway companions and a native pixel regression address the first; a bounded anchor migration addresses the second. Door lintels were added without altering openings or existing artist transforms. The first capture set is diagnostic evidence, not final visual acceptance.
- The foam shell, centered solo camera, painted room surfaces, generated wall print and procedural sky/sun appeared in actual Unity frames. Final corrected captures and a Windows build remain required.

Source recovery snapshot before the final camera tests: `Logs/checkpoints/2026-10-09-environment-source-in-progress`. The final source and generated assets are recoverable from pushed commit `29b2728`.

## Corrected native review

- Additive fixture migration saved upright imported frames, curtains and sconces. Door lintels close the space above existing door openings. The 23 affected editor cases passed, including all five saved prefabs, scene lighting, artist-pose preservation, repeat migration, and Scene-view ceiling visibility restoration (`Logs/environment-stage-repair-editmode.xml`).
- Graphics-enabled regressions: 56 passed, with one failure in the new synthetic pixel fixture's manual single-camera render request. All 26 environment captures, lobby/workshop checks, Moving Day checks and the three active-craft cleanup regressions passed. The pixel fixture was corrected to use normal camera frames and its focused rerun passed (1/1). Its four native images prove green floor / red independent roof / red interior ceiling / green overview after other cameras. This was an affected-case rerun, not a second full-suite run.
- Reviewed corrected native frames at `evidence/unity-environment-2026-10-09`: centered active Pinwheel gameplay, five distinct interiors, full-height walls and ceiling detail, upright window/sconce assemblies, visible Garden sky/sun, upstairs rooms, mixed-floor couch views, lobby and Hub exploration. The roof no longer covers overview/couch gameplay. The lobby key/rim balance is warmer and less cyan.
- Cutaway-controlled renderers use the regular renderer path because GPU Resident Drawer caches visibility before per-camera callbacks. The opt-out is limited to affected renderers; the rest of the project keeps GPU batching. Runtime companions unregister when props are destroyed, including across repeated rounds.
- Workspace ceiling hiding changes editor visibility only, restores before scene/prefab closing, and preserves objects the artist already hid. The default Play entry remains the current Hub.
- Independent visual review accepted corrected frames 01–05, 09, 12, 18 and 23–26: no remaining clipping, missing fixture, or roof occlusion was identified. Opaque stylized window glazing remains a deliberate surface; no traversable window opening was added.

Initial diagnostic frames are preserved locally under `Logs/checkpoints/2026-10-09-environment-first-render`, rather than mixed into the current screenshot directory. The checked-in XML includes initial results and the passing follow-up runs for traceability.

## Windows delivery

`ProductionBuild.Windows` succeeded with zero errors and reported 206,188,632 bytes. The executable started on Direct3D 11 / Intel Arc Graphics, remained responsive for 103 seconds, and logged no exceptions. This is a startup smoke test; gameplay and local couch behavior were exercised in the native PlayMode/capture runs. Evidence: `windows-build-result.txt`, `windows-build-smoke.json` and `windows-startup.log` beside the final screenshots.

Build-only render-pipeline serialization caches, tactile seed display-name changes and the generated TMP fallback cache were preserved under `Logs/checkpoints/2026-10-09-build-cache` and restored to their pre-build repository state. No authored environment assets were reverted. Existing local multiplayer is preserved and verified; this stage adds no online networking service.

Open `docs/UNITY-WORKSPACE.md` for the current edit entry points. The editable lighting profile, all five house prefabs, current scenes and inventory assets are linked from **Wreckabulary → Open Current Workspace**. The built player is `Builds/Windows/Wreckabulary.exe` (local build output, not committed).

The verified environment and build record have been fast-forwarded to GitHub `main`; the local checkout also uses `main`. The feature branch retains the same delivery history. New commits use SethyPagna's configured identity and contain no co-author trailers.

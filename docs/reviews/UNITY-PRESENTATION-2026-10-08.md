# Unity presentation refresh — 8 October 2026

STATUS: COMPLETE — LOCAL IMPLEMENTATION AND NATIVE VERIFICATION

## Scope and baseline

- Maintained checkout: `wreckabulary`, branch `main`, baseline `4ae98b0`; clean before work. The separate `wreckabulary-james-v1` extraction remains untouched.
- User acceptance: centered third-person view; compact HP/letters/two-slot HUD; remove floating health pips; CanvasGroup state controller; cached world billboard; inventory UXML/USS; vivid art and skill feedback; actual Unity screenshots and local multiplayer checks.
- Preserve hub and furniture placement. Online matchmaking is outside the existing local multiplayer implementation and is not claimed by this work.
- Unity 6000.6.3f1 is licensed and imports the project successfully. The original FBXs, fonts and textures were Git LFS pointers; `git lfs pull` restored their actual bytes and `git lfs fsck` passed.
- Existing native project verification entry points are used. The optional Game Studio kit is not installed; no kit installation, hooks, package server, or release gate is being claimed.

## Ownership

- Camera lane: CameraRig, player/input integration, PlayerHud, Popup, WorldSpaceBillboard.
- UI lane: GameHud, FrontDoorMenu, UIStateController, inventory markup/styles and UI tests.
- Visual lane: room color/lighting and skill-specific effect presentation.
- Integrator: generated assets/import settings, BackToHub routing, engine runs, screenshots, verification and checkpoint.

## Recovery

Meta-harness run: `run-30b4b406-855c-42c1-81dc-acf6b56ec59e`.
Saved source snapshot: `Logs/checkpoints/2026-10-08-presentation-final/` contains the exact baseline, tracked binary patch and copied untracked files. Changes remain local and uncommitted. To continue, open the maintained checkout in Unity 6000.6.3f1 or run the Windows build below; consult the scoped visual limitations before extending asset work.

## Implemented behavior

- A single local seat uses a centered perspective chase camera, including matches with AI. Mouse middle-drag or gamepad right stick orbits; movement follows the camera. Nearby cutaway walls hide visually while their collision remains intact. Two local seats retain the shared house camera so both players remain visible.
- The gameplay HUD has one HP display per player, an eighteen-letter bag and two vertically stacked item slots alongside it. The local floating name/health pips are removed; other players retain identification.
- Main menu, gameplay and pause use an event-driven `UIStateController` with CanvasGroup fades and EventSystem focus. Pausing preserves crafting/deployment reservations, countdowns and previous actor freeze state. Start/retry controls cannot receive the Resume input during the fade.
- `WorldSpaceBillboard` caches or accepts a camera and supports a follow target, structural offset and optional upright axis. It serves TMP labels and world-space canvases without a per-frame Camera.main search.
- The separate inventory deliverable is UXML/USS presentation with the requested charcoal/gold tokens, four columns and right stats. The hierarchy and styling notes are in `docs/UI-INVENTORY.md`; no inventory application logic is implied.
- Three ChatGPT-generated raster assets are imported and used: menu house art, roommate illustration and transparent impact spark. Prompt/provenance records are in `docs/art/GENERATED-UI-2026-10-08.json`. No Higgsfield was used. Gameplay keeps the supplied actual 3D meshes.
- Room palettes, floor joins and daylight are richer, with existing lobby/map placement preserved. Distinct feedback covers wood debris, letters, crafting, SOAP, FOAM, BOMB, BED, MAT and clear-out warnings. Wear stages add visible cracks/cloth damage before the existing letter payout.
- The default unsaved outfit uses the red hoodie/satchel direction. Imported blink/brow morphs, layered locomotion and action poses provide expression; miniature gear follows the animated hand. Saved wardrobe choices stay valid.

## Native verification

- Project setup/import succeeded with real model assets and animation clips.
- Final EditMode: **187 passed, zero failures**, `Logs/presentation-editmode-final.xml`.
- Regular PlayMode: **130 cases passed** across the full run and focused reruns. The full run had one tutorial fixture timing failure: it jumped directly from dodge to craft while the authored dodge was still active. Waiting for `!IsDodging && CanAct` fixed the fixture; the tutorial rerun passed. Production craft/dodge rules were preserved.
- The two explicit rendered capture journeys passed separately in a graphics-enabled editor. Latest native outcomes total **132 PlayMode/capture passed, zero failed**, with two older explicit capture fixtures skipped. `evidence/unity-2026-10-08/test-results.json` records each latest result and raw XML source. Raw XML/logs remain under local `Logs/`.
- Rendered captures use `ScreenCapture.CaptureScreenshotAsTexture` after `WaitForEndOfFrame`, including overlay UI. Thirteen views cover menu, hub, behind-camera gameplay at two ratios, pause, FOAM, two-seat Duos, inventory at two ratios, furniture lifecycle, pickups and character actions.
- Initial captures exposed a distant camera, wall occlusion, pause backdrop sizing, ultrawide stat compression, unstable squash and interpolated held-item lag. These were corrected and the final rendered frames reviewed. The closer pickup capture also exposed back-facing glyphs and detached underside TMP; the corrected letter faces and pooling/physics regression passed a focused native rerun.
- Wall-interaction test fixtures had moved only Rigidbody.position before same-frame closest-point queries. Native diagnostics confirmed stale Transform positions. Fixtures now set both, with production interaction assertions unchanged; all five focused wall tests pass.
- Online matchmaking, physical gamepad/mobile testing and device performance are not covered by the local two-seat checks.
- Windows build: **Succeeded, zero errors**, 136,822,510 reported bytes. Entry point: `Builds/Windows/Wreckabulary.exe`; keep its accompanying data/runtime folders. Native build log: `Logs/presentation-windows-build.log`.
- Standalone startup smoke: responsive process, Direct3D 11 graphics device, assembly load, physics and input initialization; no exception/error lines before controlled shutdown. This is startup evidence, not a second complete standalone playthrough. Log: `Logs/presentation-windows-smoke.log`; manifest: `evidence/unity-2026-10-08/windows-build.json`.
- Incidental build-time URP shader-prefilter caches, TMP fallback cache and GameData field-order serialization were restored. The functional model-library animation references and avatar blendshape import changes are retained.

## Reproduction

Use `C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe` with this checkout as `-projectPath`.

- Setup: `-batchmode -quit -executeMethod Wreckabulary.EditorTools.ProjectSetup.Run`.
- EditMode or PlayMode: `-batchmode -runTests -testPlatform EditMode` (or `PlayMode`) with `-testResults` and `-logFile`. Do not add `-quit` to a test run.
- Screenshots: omit `-batchmode`; `-runTests -testPlatform PlayMode -testFilter Wreckabulary.Tests.PresentationCaptureTests`. The fixture chooses 1600×900 and 2100×900 views and records actual final frames.
- Windows: `-batchmode -quit -executeMethod Wreckabulary.EditorTools.ProductionBuild.Windows`.

## Visual scope

The generated portrait and key art are raster illustrations. They do not replace the supplied 3D mesh topology with the higher-detail reference character. Runtime improvements use the existing rig, wardrobe, imported materials and new animation/feedback presentation. Furniture damage is a layered runtime wear treatment rather than a complete set of separately sculpted damaged models. Every skill cue was source-reviewed and existing mechanics tests passed; representative effects were captured, not an exhaustive per-skill visual certification.

## References

- [Unity CanvasGroup](https://docs.unity3d.com/2022.3/Documentation/Manual/class-CanvasGroup.html): alpha, interaction and raycast visibility are independent controls.
- [Unity screen capture](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/screencapture/capturescreenshotintorendertexture): capture the final rendered frame, including overlay UI.

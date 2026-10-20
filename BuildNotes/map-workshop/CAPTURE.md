# Workshop viewport capture

STATUS: SOURCE READY; NATIVE EXECUTION AND VISUAL REVIEW NOT RUN

Entry point: `Wreckabulary.EditorTools.WordboundWorkshopCapture.Run`.
Editor menu: **Wreckabulary → Wordbound → Capture workshop templates**.

Run in the integration project with its actual Unity editor and URP configuration. For batch execution use `-executeMethod Wreckabulary.EditorTools.WordboundWorkshopCapture.Run` with graphics enabled; do not use `-nographics`. Root owns native execution.

The helper resolves the existing RaceCircuit, CombatArena and FreestyleWorld templates at their saved seed. Each image uses the real `WordboundWorkshopViewport`, its native camera, normal model lookup and render target. It submits the camera to URP, then writes the unedited GPU readback at 1280×720:

- `Logs/wordbound/workshop-race-circuit.png`
- `Logs/wordbound/workshop-combat-arena.png`
- `Logs/wordbound/workshop-freestyle-world.png`

Each PNG has a neighboring JSON record containing course ID, seed, active layer, source and manifest hashes, camera pose, dimensions, engine version and capture time. This is a viewport geometry capture, not a screenshot of the complete workshop panel or evidence of successful pointer/controller interactions.

Every capture owns a separate preview scene, camera hierarchy, light, render target, readback texture and viewport materials. Camera scene culling isolates existing editor objects even if they use layer 31. The helper closes only its preview scene and restores the prior render target and asynchronous shader compilation setting in `finally`. It does not open, replace or save normal scenes, enter Play mode, mutate a workshop document, invoke autosave, or change render pipeline assets.

Static checks: APIs checked against the installed Unity 6000.6.3f1 managed documentation and installed URP `UniversalRenderPipeline.SingleCameraRequest` implementation. No Unity launch or image generation occurred in this lane. A source reference compile is deferred while root's native clean compile is active. Next action: integrate this additive helper, compile it in Unity, run it, inspect all three actual PNGs, and record render/cleanup results before claiming visual acceptance.

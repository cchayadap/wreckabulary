# Workshop overlay repair

Status: SOURCE PASS after independent online-lane review of the production delta and all 13 focused cases. No Unity, compiler, test, player or build execution in this lane. Visual acceptance remains pending a new visible Windows capture.

Baseline: root `305ff093`, isolated branch `sethypagna/workshop-overlay-cleanup`. The prior CLOCK/QA branch and unrelated `BuildNotes/flagship-content/focused-first-attempt.log` remain preserved. Original Wreckabulary artwork remains the default; no model, material, source art or QA workflow is changed.

## Actual evidence

Opened the actual 1280×720 image `docs/qa/runs/20261023T175916Z-visible-wordbound_workshop_flow/wordbound_workshop.png` in root. Its recorded SHA-256 is `662FCC1E6B9A3AEDCD3E8C648E88B94BE42FDEB04F4C8DB1AED76E2B5F2AF9AB`. Module and prop-range IDs overlap around the small central map, and cyan line fragments extend into the palette/header. The existing save-path error and subsequent native crash are separate issues; this patch claims no correction to those.

The source printed every module/zone ID without layout checks, framed all course floors, and rotated line textures by multiplying a viewport-local translation into `GUI.matrix`. Unity's public `GUIUtility.RotateAroundPivot` accounts for the current GUI clipping offset before rotation; the removed custom transform did not. [Unity reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/GUIUtility.cs)

## Bounded changes

- Project line endpoints in camera space, clip a crossing segment to the camera's near plane, and clip its 2D segment to the viewport with a 1.5-pixel inset for the existing 2-pixel line. Reject wholly behind-camera, degenerate, nonfinite and wholly outside segments. There is no sentinel-to-real endpoint line and no forced minimum length extending beyond an endpoint.
- Draw with the public group-aware rotation helper and restore both matrix and color in `finally`. The viewport GUI group also closes in `finally`. All zone ranges, selected puzzle volumes/arms and transform axes use this one path. No raster texture, upload, mesh, shader or additional render resource is introduced; work scales with line count, not viewport pixels.
- Place labels after reserving the seed/grid panel and visible handle boxes. Selected physical controls, other physical controls, selected zones/modules and hover labels take priority over overview labels. A finite set of nearby anchor positions must fit wholly in the viewport and avoid reserved/previous label rectangles; otherwise the label is culled. No forced overlapping fallback exists.
- Overview labels omit the repeated active-floor prefix and truncate long captions. Full IDs remain in tooltips and the existing Inspector. Random/fixed zone captions appear only on selection/hover; random captions show count range, fixed captions show the asset. Small overview rooms omit labels until selected/hovered. Geometry and range lines remain visible.
- “Frame map” fits visible active-floor modules to the current camera aspect/orientation with padding. Switching the active floor reframes that floor. If it is hidden/empty, only actually rendered adjacent geometry is considered; hidden/distant floors do not enlarge the fit. Explicit selection framing still works.

Input/selection/drag/rotation/range commands, hit tests, undo, source switching, autosave and QA scenario steps are unchanged. Handle box rendering now rejects behind-camera positions but uses the same existing size/location and hit-test path. Projection retains its legacy sentinel only for old point-query consumers; line drawing uses the new safe segment path.

## Required verification

Source regression fixture: `Wreckabulary.EditorTests.WordboundWorkshopOverlayTests` (13 cases). It covers full-width line-quad containment, reversed/diagonal/vertical clipping, outside/nonfinite/degenerate rejection, dense label collision/culling and reserved controls, actual viewport near-plane crossing, active/hidden-floor framing, and unchanged course data. These tests are **NOT RUN** here.

Root should compile and run that fixture plus existing `WordboundWorkshopTests` projection/picking/disposal and workshop input/placement/physical-site regression filters. Source math tests do not establish IMGUI clipping behavior on the Windows renderer.

Then inspect actual visible Windows captures at 1280×720: initial flagship overview, selected and hovered room/zone, a selected physical site, floor switch, zoomed editing, and oblique pan/orbit with lines crossing all viewport edges and the near plane. Confirm no cyan/yellow/axis strokes appear in the palette/header/Inspector/footer, labels do not overlap each other or handles/seed controls, and drag/range/rotation/F-frame/undo still work. Full-ID hover tooltips use the existing footer path; its separately reported status/tooltip competition is not changed in this rendering-only slice.

The mathematical clipping contract and group-aware API are source evidence, not a new screenshot or owner usability approval. No overall Windows smoke, crash fix, performance result or production-readiness claim is made.

## Native regression result — 2026-10-23T18:31Z

Root R0 passes (`Logs/gate_check_all_20261023T182307Z.log`,29.7seconds). The first native invocation used the wrong namespace and correctly failed withzero tests; it remains in `TestResults/editmode_20261023T182644Z.xml`. The corrected exact class `Wreckabulary.EditorTests.WordboundWorkshopOverlayTests` passes13/13, zero skipped,109.8seconds with82.5seconds refresh: `TestResults/editmode_20261023T182832Z.xml`, `Logs/gate_test_editmode_20261023T182832Z.log`. Native camera framing, near-plane projection and layout/clip calculations are verified. Actual rendered Windows line containment, readability, hover/selection and interaction remain pending; these calculations alone do not prove IMGUI pixels. Unity-generated new source/test metadata is committed3069ca1c.

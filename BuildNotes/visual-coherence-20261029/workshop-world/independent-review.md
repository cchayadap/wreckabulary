# Independent Workshop world preview review

STATUS: COMPLETE — REPAIRED FROZEN SOURCE REVIEW; NATIVE/IMAGE ACCEPTANCE PENDING

Scope: original six-file draft plus three root-authorized source-ordering/test files, nine total under workshop-world. Check canonical fit against physical bounds, borrowed model/material ownership, isolated lights/layers, external delegate objects, generated placement-to-zone mapping, contextual labels/handles and retained picking/undo. No live edits or native execution. This report is the only write.

Final verdict: no remaining blocking source finding in the repaired frozen candidate. The initial findings and repairs are retained below; native tests and actual image review remain required.

## Confirmed findings in initial candidate

- Assigned-course ordering: new launcher artwork delegate calls `InputsForWorkshop`, while `OpenAuthored` calls `Open` and refreshes the preview before setting `UsesAssignedCourse = true`. `Open` explicitly clears that flag. The initial assigned preview can therefore use default items/profile and fail on assigned-only artwork IDs. Sent to author/root; author confirmed and is preparing a bounded ordering repair plus binding test.
- Handle GUIStyle lifetime: initial Overlay candidate caches viewHandle/activeViewHandle from normalBox/selectedBox without the existing ui.Generation reset. Those source textures may be replaced/released when the theme changes. Sent to author; author is preparing a cache check/repair.

## Lighting isolation concern to resolve

Two new directional preview lights currently use object layer31/cullingMask31 but default URP rendering layers. Local installed URP chooses the brightest visible directional light when no RenderSettings.sun is present (`UniversalRenderPipeline.GetBrightestDirectionalLightIndex`); enabled light layers use UniversalAdditionalLightData.renderingLayers in ForwardLights. The current fixture verifies properties but cannot establish actual camera isolation. No native visual contamination is claimed from this source review. Author is considering enabling the new lights only during the Workshop camera's render callbacks, with cleanup/unsubscription, rather than treating the culling mask alone as proof.

Other reviewed changes so far: per-axis artwork fit agrees with canonical bounds and existing game resolver fitting; external factory objects are rejected before mutation; generated fixed placement ZoneId now maps to the authored zone for transient drag; labels reserve the original click targets and keyboard/undo handlers are unchanged. Final verdict awaits revised frozen manifest.

## Repaired candidate recheck

All three concerns have a bounded source repair:

1. Public `Open` retains its signature and delegates to private `OpenSource(..., false)`; `OpenAuthored` calls `OpenSource(..., true)`. The source flag is set before `ResetView` invokes `Frame` and before `Refresh`. New binding tests provide different assigned/default STOOL model overrides, assert source identity during every factory call, exercise template/assigned round-trip history, and retain an explicitly supplied factory.
2. `ViewHandle` invalidates both cached styles when `styleGeneration` changes. No old handle style remains selected after the parent theme rebuild.
3. New directional lights are disabled at creation, enabled only by `BeginPreviewCamera` for the Workshop lens, disabled on any unrelated camera begin and on the lens end, and disabled/unsubscribed during Dispose. No existing light property, RenderSettings value or shared asset is modified. Revised tests exercise this mechanism as well as owned hierarchy cleanup. This is source validation of the isolation mechanism; actual URP execution still needs the native run.

## Preservation and scope

- `FitArtwork` measures mesh bounds in its owned fit transform and scales per axis to the catalogue dimensions with ground contact at y=0. This matches the existing gameplay resolver's non-player fitting policy; it does not introduce new physical bounds or colliders. Mesh and material assets are borrowed. Degenerate/missing mesh artwork uses an explicit canonical placeholder.
- A delegate result outside the supplied fit hierarchy is rejected before the viewport changes that result's transform, layer or physics settings. Only the newly owned anchor is destroyed on that branch; the foreign-object test checks its position/scale/collider and survival. Factories remain responsible for returning a newly created child, rather than moving external objects themselves.
- Preview descendants are placed on layer31. Existing 3D/2D colliders are disabled and rigidbody simulation is disabled only on the returned owned instance. Original source prefabs, mesh arrays and material arrays are checked after disposal in the added tests. Root destruction also releases existing TactileMaterials scopes through their existing lifecycle.
- Generated placements now carry their source ZoneId into Asset. When that ID names a fixed zone, its visual anchor uses the fixed ID and joins fixedVisuals. Preview drag/cancel therefore addresses the same authored zone before and after random resolution. The new resolved/nonresolved fixtures check canonical picking, unchanged recovery JSON/hash and transient movement/cancel without publishing history.
- Room labels appear for selected/hovered modules; zone bounds remain for selected/hovered zones and validation issues. Selected physical-puzzle handles retain their 14-pixel click radius, and X/Z/plane/rotation hit tests remain unchanged despite smaller visuals. Label reservation uses the hit region, preserving room for the click target. Existing pointer commit, Ctrl+Z/Shift+Ctrl+Z/Ctrl+Y and controller undo handlers are unchanged.
- The resolver cache now also tracks BaseArtwork mode, so changing that choice on the same profile creates a matching resolver. Other profile overrides remain referenced by the resolver and are evaluated when the next model is created.

## Required native and visual follow-up

Run the actual Workshop and relevant source-binding/viewport tests, then inspect original captures. Check the real props match catalogue footprint, fixed prop selection/drag/rotate/cancel/undo after a generated preview, template-to-assigned artwork switching, contextual label readability, hover/puzzle-control discoverability, and return/close cleanup. Rendering isolation should also be observed alongside an unrelated camera; direct callback tests are not screenshot proof. Smaller handles preserve hit targets but remain an aesthetic/accessibility review item. No native pass, actual rendered quality, multiplayer behavior or owner approval is claimed here.


## Frozen identity

Manifest SHA-256: 0791323299860ef25d3b99c341c57474d18c173f06646731dd9c07a2bb4cc53d. All nine baseline/candidate pairs matched (18 checks). Read supplied five-assembly semantic receipt: zero errors. No compiler/native rerun was performed by this reviewer.

| Candidate path | SHA-256 |
| --- | --- |
| Assets/_Project/Scripts/Online/Client/WordboundLauncherWorkshopSource.cs | 16c4be007458d56208508106b830389ec37af827cc8eee60dc83c995bdfd8e90 |
| Assets/_Project/Scripts/Online/Client/Tests/Input/WordboundWorkshopSourceBindingTests.cs | bfaad89aea099b0ca747a8c36aebc9251764fe66f06dd378337f2fe6e9cffb64 |
| Assets/_Project/Scripts/Workshop/WordboundWorkshop.cs | c8be4a9e10bfd48bb906686d0197afd3eb299cfadd31b15a0ecef6c543c50f3e |
| Assets/_Project/Scripts/Workshop/WordboundWorkshopInteraction.cs | 25c4753f6ffc2347def920151c0b1dc1ee1d5a4c6b4068589355787ebef85bc3 |
| Assets/_Project/Scripts/Workshop/WordboundWorkshopOverlay.cs | e12a814a20f2f0da00e55a615861267cd0208d3641738ec247342302f1c58eba |
| Assets/_Project/Scripts/Workshop/WordboundWorkshopRecovery.cs | 7bf6693efe40af42fedc8f8c0622acdc50498ea51a9fc9c57ca832663a547a72 |
| Assets/_Project/Scripts/Workshop/WordboundWorkshopSites.cs | 680b1803da45351f9329f642db41dd31c61f0794b5a11dc93e69f7ae000e279c |
| Assets/_Project/Scripts/Workshop/WordboundWorkshopViewport.cs | ca8366afd5646dbd9d15d9c76653d88c166f6959375f9afdc3af415bcb94b7cd |
| Assets/_Project/Tests/Editor/WordboundWorkshopViewportArtTests.cs | a3bd90b8e955f7c7aed63ee60e4b3b1adc5cec3759977f8b2f74d47118fb55de |

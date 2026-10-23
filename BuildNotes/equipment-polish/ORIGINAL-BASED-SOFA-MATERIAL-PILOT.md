# Original-based SOFA V4 shallow material-seam pilot

STATUS: BUILT AND DETAIL PAIR REVIEWED — no convincing visible improvement; original SOFA remains the baseline

## Completed root render and independent visual result

Root built V4 with Blender 5.2.2 LTS and rendered the matched detail pair at 960 × 960, 64 samples, six CPU threads. This lane directly viewed both actual PNGs and independently checked their SHA-256 values, the saved blend hash and every paired metadata field except variant, image hash and elapsed time. They match the same camera, lighting source, blend and render settings.

The floating line and kink seen in prior geometric experiments are absent, but the shallow normal seam is barely discernible. I do not see a convincing visible quality improvement over the actual original model. Root independently reached the same conclusion. **Keep the original SOFA surface; stop further SOFA render iterations.** V4 stays preserved as an optional editable material experiment, with no appearance approval, bake, Unity rollout or production-art claim.

| Artifact | Verified SHA-256 |
| --- | --- |
| Editable V4 blend | `ac3185ffff0295cc0de6dc8fb08f2a2fcc840c774034dfd1a1f8512a6ec555ae` |
| Detail original PNG | `7e1e5793814555e61ea1d1c34e28b2de73b6edbeda1aa49ed8f214309671c4ad` |
| Detail candidate PNG | `3103ec66e5dd06c313b5ae1c0e5709d755b33ae311115241968db7923a27dd37` |

Actual files are in `ArtSource/Collections/WordboundPvP/OriginalBasedPilotV4/Previews/` in the integration checkout. Root render times were 122.430 seconds for original and 169.623 seconds for candidate; these are source-render timings, not runtime performance. The receipt confirms unchanged original bounds, 16 mesh parts, 6,248 triangles and no added curve welts. The plan below is retained as the historical experiment specification; its build destination now exists and must not be overwritten.

V1 had floating trim; V2 seated the trim but exposed a kink in the original mesh cross section. V3's smooth wire fit failed direct source-triangle contact checks before native execution. Those revisions and evidence remain preserved (`43191ef` failed fit source, `660798c` feasibility evidence). No limit was loosened. Root authorized a separate shallow material treatment on the original cloth.

V4 removes the added welt geometry and keeps every original sofa component, face, vertex, UV, transformed split normal, dimension and grip. Two back-cushion materials receive an editable procedural **normal crease only**. Original color inputs remain unchanged; there is no painted dark outline, displacement, geometry seam or contact-pass claim. The fabric micro-normal feeds the new seam normal rather than being replaced. The original sage/timber style remains the reference, and the finished look must still be judged from rendered close-ups.

The cover-panel field uses a smooth rounded-rectangle distance in the cushion's measured principal frame, with a 16 mm inset and corner radius 40% of its smaller half-extent. Its normal profile is a shallow Gaussian valley (1.5 mm width) with small smooth shoulders, multiplied by a smooth front-only fade. Editable nodes are arranged by dependency in a labeled frame. The bump uses 0.18 mm distance at 0.70 strength. These values are intended to suggest a compressed cover join; they are not a physical cut/stitched mesh or a claim that the source contains full upholstery construction.

## Numerical check before native work

Executed in this lane:

```powershell
python BuildNotes/equipment-polish/check-v3-seam-feasibility.py Web/public/art/items/SOFA.glb --material-profile
```

The additive material mode reads the exact original GLB dimensions and evaluates the intended analytical profile from the current builder source without importing Blender. It checks 100,001 samples per profile. Equivalent profile relief is approximately -0.125783 mm to +0.003449 mm; maximum slope is 0.084565, corresponding to about 4.834° normal tilt. Including a conservative bound for the front fade on both original 95.105 mm half-depth cushions gives **5.210°**, below the stated 6° bound. Values are finite and the builder repeats these bounds. This establishes restrained intended profile magnitude only; it does not execute GPU shader nodes or prove their appearance. The earlier wire mode still preserves/reproduces the failed geometry fit.

## Root-owned build and first close-up

Run only when root releases the heavy slot and after integrating the new source. New destination V4 is distinct from the preserved prior pilots. The build refuses an existing destination.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_original_pilot.py' -- --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotV4' --stage build
```

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_original_pilot.py' -- --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotV4' --stage render --views detail --size 960 --samples 64 --threads 6
```

Inspect the previously problematic upper-left corner and lower run: the detail should be smooth, shallow and part of the cloth response, without a drawn line, detached wire, ridge or kink. If it does not improve the original, keep the original surface. Only after a convincing close-up should three-quarter/side/clay/scale/front/rear follow. No production replacement or broad art rollout is implied.

If the appearance succeeds, bake the approved cloth/seam normal into a dedicated UV texture with tangent-space normal import, pack roughness into the project's smoothness convention, and compare the same close-up inside Unity. The current procedural source has **no baked Unity maps or delivery exports**. Runtime material budgets, actual avatar/game-scale views and owner acceptance remain pending. FAN/HAMMER stay separately scoped original-based pilots with useful construction changes.

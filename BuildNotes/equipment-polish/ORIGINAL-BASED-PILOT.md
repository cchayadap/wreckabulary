# Original-based Wreckabulary art pilot

STATUS: SOURCE PREPARED — Blender build, renders and appearance review NOT RUN

The owner rejected DetailV2 and identified the original Wreckabulary game artwork as the reference. This pilot starts with those original bytes. It does not replace runtime assets, overwrite either Wordbound revision, or establish that any artwork is ready to ship. Root owns the serial Blender/render slot.

## SOFA first

Source: `Web/public/art/items/SOFA.glb`, SHA-256 `dfaca31fdc676de5d2d7fac74a4b6b26cbadea62bbd523f0739cbf7411705def`. This is the model used by the original game icon and `Web/public/art/supplied-showcase.png`; it is not a new concept. Its original Unity-space bounds are `[-0.76, 0, -0.39]` to `[0.76, 0.93, 0.423]` metres; grip `[0, 0.465, 0.0165]`.

Keep the original sage upholstery, two-seat silhouette, continuous rounded arms/back, pale timber feet, cream seat welts and small back buttons. The script separates the actual original sixteen material/connectivity components into editable named parts while copying their UVs and transformed split normals. The original geometry remains separately available in the same scene and as an unchanged GLB copy. The four cushion components receive one subdivision after coincident-vertex welding and coplanar triangle joining, then return to their exact original component bounds. This is an experimental curvature refinement, not a claim of better appearance until rendered.

Two restrained 1.7 mm matching-fabric back welts make the upholstery assembly clearer. Existing fabric colors/textures remain intact; a small-scale normal response and modest sheen supplement them. Timber, feet, arm, back, base and button geometry stay from the original model. No orange/brown aging, broad grain noise, olive/red color replacement, extra plinth, third cushion or generic decorative hardware is introduced. The pilot does not claim new joinery that it has not modeled.

Review the cushion highlight flow, corner pinching, welt contact/placement, button contact and fabric response closely. If subdivision makes the original shape worse, retain that original component and revise its local surface deliberately; do not compensate with more noisy maps. A measured topology report follows generation, but visual quality is the first decision. Earlier DetailV2 budgets must not silently remove useful original curvature. Later runtime exports, baked maps, collision/grip validation and performance measurements remain separate work.

## Exact root-owned commands

Run only after the current heavy gate releases. These commands assume the script commit is integrated into root. The build refuses an existing destination and verifies the exact original SOFA hash. It writes only the new `OriginalBasedPilotV1` folder. A failed/abandoned build is preserved; choose a new numbered revision instead of deleting or overwriting it.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_original_pilot.py' -- --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotV1' --stage build
```

First render only the matched three-quarter pair. Both variants use the same camera matrix, framing, light rig, resolution, sampling and color transform. Inspect them before spending time on the rest.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_original_pilot.py' -- --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotV1' --stage render --views three-quarter --size 960 --samples 64 --threads 6
```

If that pair supports continuing, render front, side, rear, detail, neutral clay and original-avatar/table scale pairs with the same command, changing only `--views` to `front,side,rear,detail,clay,scale`. The scale image is a static studio composition, not a gameplay capture. Render receipts contain image SHA-256, source blend hash, renderer script hash, Blender version, camera, resolution, samples, thread count and elapsed time. Existing images are skipped only when their hash and every current render setting match; partial or changed images fail with a new-revision instruction. `PILOT-REVIEW.html` links only completed, hash-verified images, refuses pairs with different recorded settings and labels unrendered counterparts. A source review caught and repaired the initial hash-only resume path before any interrupted pair was resumed.

Prerequisites checked read-only: Blender executable exists at the path above, original GLB exists in the lane/root, and script parses as Python. Blender's `bpy`, `bmesh` and bundled NumPy APIs have not been executed for this script. The build includes all imported images in its saved `.blend`. Procedural fabric nodes are editable source materials; they are not yet Unity-ready baked maps. No new FBX/GLB delivery exports or production defaults are created.

## FAN and HAMMER queued after SOFA review

| Pilot | Preserve from the actual original game model | Construction improvements to model and inspect | Required views |
| --- | --- | --- | --- |
| FAN | Slim powder-blue base/rim, slender silver stand, open silver grille, pale blades, small warm centre cap; original `0.522 × 0.821 × 0.25 m` bounds and `[0, 0.4105, 0]` grip | Use original curves and light silhouette. Model a coherent rear motor cover/vents, visible shaft, tilt fork/joint and small grille fasteners. Keep base slim; no layered blocks, heavy cream rim or broad decorative trim. Subtle powder-coated metal response; clean silver grille; no fabric-like housing noise. | Matched front/side/rear, close view through the grille, base controls, avatar scale. Side view must show uninterrupted support from base through motor to rotor. |
| HAMMER | Rectangular blue-gray metal head with thin pale-brass ends, pale wooden handle, existing brown grip rings; original `0.345 × 0.635 × 0.175 m` bounds and `[0, 0.13, 0]` grip | Preserve the metal tool identity and original handle/head proportions. Add a credible handle eye and wedge/ferrule only where structurally appropriate, smooth metal edge highlights, restrained machining response, longitudinal pale grain and coherent grip contact. No cloth/ivory mallet head or unrelated green trim. | Matched full-object and head/eye close-ups; actual-avatar held alignment at the preserved grip; gameplay-distance scale. Label any static hand pose as a source preview until Unity handling is observed. |

These two scripts have not been implemented or rendered. Their source GLBs have been inspected read-only: FAN SHA-256 `67bd384322b83617ac801211fb487f6be4e57c9d712f1b294660c13957700cd7`; HAMMER SHA-256 `b397ce961e7d7369118ca573e4bd5853391a3a1ea4c59076c686f13f4312b67f`. All original game assets and earlier Wordbound revisions remain preserved. Original editable vault `.blend` sources have not been located; the pilot's editable Blender scene is derived from the supplied GLB geometry and does not misrepresent its provenance.

## Acceptance and recovery

- [x] Correct original-game references inspected; wrong DetailV2 direction recorded as owner-rejected.
- [x] SOFA source geometry/component structure inspected without Blender; isolated script and root command prepared.
- [ ] Native Blender build executes; component count, preserved bounds/grip, packed sources and receipt verified.
- [ ] Matched three-quarter original/candidate images directly inspected by producer and root.
- [ ] Front, side, rear, detail, clay and original-avatar scale views directly inspected; any construction defects repaired in a new revision.
- [ ] Owner appearance acceptance; then bounded FAN and HAMMER implementations/reviews.
- [ ] Approved visuals translated to runtime maps/exports, Unity authored materials/pivots, actual held views and performance validation.

Current evidence is static source preparation only. No generated preview, Blender API pass, material-quality pass, Unity appearance acceptance or production-ready claim is made.

# Original-based Wreckabulary art pilot

STATUS: V1 REJECTED; V2 BUILD AND FIRST PAIR REVIEWED — more views and owner acceptance pending

The owner rejected DetailV2 and identified the original Wreckabulary game artwork as the reference. This pilot starts with those original bytes. It does not replace runtime assets, overwrite either Wordbound revision, or establish that any artwork is ready to ship. Root owns the serial Blender/render slot.

## SOFA first

Source: `Web/public/art/items/SOFA.glb`, SHA-256 `dfaca31fdc676de5d2d7fac74a4b6b26cbadea62bbd523f0739cbf7411705def`. This is the model used by the original game icon and `Web/public/art/supplied-showcase.png`; it is not a new concept. Its original Unity-space bounds are `[-0.76, 0, -0.39]` to `[0.76, 0.93, 0.423]` metres; grip `[0, 0.465, 0.0165]`.

Keep the original sage upholstery, two-seat silhouette, continuous rounded arms/back, pale timber feet, cream seat welts and small back buttons. The script separates the actual original sixteen material/connectivity components into editable named parts while copying their original positions/faces, UVs and transformed split normals. The original geometry remains separately available in the same scene and as an unchanged GLB copy. V2 retains all original cushions without subdivision or shape replacement.

Two restrained 1.3 mm matching-fabric back welts follow the actual cushion side-band cross section where a front cover panel can meet its boxing strip. Each of 256 curve points is ray-cast onto the original mesh; the centreline sits less than one radius from that surface, so the tube contacts it. Four probes per segment check both endpoints and interpolated spans, reject floating/excessively buried segments, and record signed distances in the build receipt. This replaces V1's guessed rectangular outline on the front face. Existing fabric colors/textures remain intact; a small-scale normal response and modest sheen supplement them. Timber, feet, arm, back, base and button geometry stay from the original model. No orange/brown aging, broad grain noise, olive/red color replacement, extra plinth, third cushion or generic decorative hardware is introduced. The pilot does not claim new joinery that it has not modeled.

Review cushion highlight flow, welt contact/placement, button contact and fabric response closely. The numerical seam checks establish geometric contact only; they do not prove good upholstery design. A measured topology report follows generation, but visual quality is the first decision. Earlier DetailV2 budgets must not silently remove useful original curvature. Later runtime exports, baked maps, collision/grip validation and performance measurements remain separate work.

## V1 result and correction

Root built V1 with Blender 5.2.2 LTS and rendered the original/candidate three-quarter pair at 960 pixels, 64 samples, six CPU threads in one uninterrupted invocation. This lane and root directly viewed both images. Pale sage/timber identity and the original overall shape are retained, but **V1 fails**: back welts float visibly at the inner/lower edges and the altered seat curvature exposes a tiny cream trim fragment. Subdivision did not establish a worthwhile visual improvement. V1 remains preserved at `ArtSource/Collections/WordboundPvP/OriginalBasedPilotV1`; do not render the remaining V1 views or treat its gallery as accepted artwork.

V2 removes cushion subdivision entirely and replaces the guessed front-face piping with the measured side-band seam above. No production model is changed. The script's resume guard additionally rejects existing images whose source/settings differ, and the gallery refuses mismatched pairs. Independent input-lane source review confirms that isolation/comparison fix; it is not appearance acceptance.

Root built and rendered V2 with Blender 5.2.2 LTS. This lane directly inspected its build receipt and both completed three-quarter images, then independently checked the source blend and PNG hashes and matched-pair metadata. V2 retains the original sixteen mesh components and 6,248 primary triangles. Each welt has 1,024 contact probes: maximum centreline-to-surface distance about `0.000775 m`, below the `0.000975 m` check limit and the `0.0013 m` tube radius; deepest sampled burial about `0.000046 m`. These probes cover each polyline vertex and its following segment at 25%, 50% and 75%.

In the viewed V2 pair, the floating inner/lower loops and cream seat-trim protrusion are resolved. The seams sit subtly near the cushion cover edge, while the original sage palette, timber feet and rounded cushion/arm silhouette remain coherent. This is a restrained construction/material refinement, not proof of a broad realism upgrade. Producer judgement: continue to side/detail/clay and then scale views; do not declare art accepted from one view.

V2 evidence: blend SHA-256 `8047e5018b04c9d421d8688f7a445d778adf003dfb1dde7ff0c9fa54a3e85da2`; original PNG `f501ba6a7aa72a4e2cedca51934c2f9891f132309f2e5257aba3f085985f8ecf`; candidate PNG `5afb8e150c8d6e1b411cfa8390781ab2aa684fd0bd8858a7b7ee068eb5869dda`. Recorded rendering times are 98.484 s original and 111.366 s candidate at 960 pixels, 64 samples and six threads. See root `ArtSource/Collections/WordboundPvP/OriginalBasedPilotV2/PILOT-REVIEW.html` and its per-image receipts. Root owns all native execution; this lane performed read-only artifact verification and image inspection.

## Exact root-owned commands

Run only after the current heavy gate releases. These commands assume the script commit is integrated into root. The build refuses an existing destination and verifies the exact original SOFA hash. It writes only the new `OriginalBasedPilotV2` folder. A failed/abandoned build is preserved; choose a new numbered revision instead of deleting or overwriting it.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_original_pilot.py' -- --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotV2' --stage build
```

First render only the matched three-quarter pair. Both variants use the same camera matrix, framing, light rig, resolution, sampling and color transform. Inspect them before spending time on the rest.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_original_pilot.py' -- --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotV2' --stage render --views three-quarter --size 960 --samples 64 --threads 6
```

If that pair supports continuing, render front, side, rear, detail, neutral clay and original-avatar/table scale pairs with the same command, changing only `--views` to `front,side,rear,detail,clay,scale`. The scale image is a static studio composition, not a gameplay capture. Render receipts contain image SHA-256, source blend hash, renderer script hash, Blender version, camera, resolution, samples, thread count and elapsed time. Existing images are skipped only when their hash and every current render setting match; partial or changed images fail with a new-revision instruction. `PILOT-REVIEW.html` links only completed, hash-verified images, refuses pairs with different recorded settings and labels unrendered counterparts. A source review caught and repaired the initial hash-only resume path before any interrupted pair was resumed.

Prerequisites checked read-only: Blender executable exists at the path above, original GLB exists in the lane/root, and script parses as Python. Root executed V1 and V2 build/first-pair render paths, including V2 BVH contact checks. V2 already exists, so do not repeat its build command; use only the render stage for unrendered views, or a new destination for future geometry revisions. The build includes all imported images in its saved `.blend`. Procedural fabric nodes are editable source materials; they are not yet Unity-ready baked maps. No new FBX/GLB delivery exports or production defaults are created.

## FAN and HAMMER queued after SOFA review

| Pilot | Preserve from the actual original game model | Construction improvements to model and inspect | Required views |
| --- | --- | --- | --- |
| FAN | Slim powder-blue base/rim, slender silver stand, open silver grille, pale blades, small warm centre cap; original `0.522 × 0.821 × 0.25 m` bounds and `[0, 0.4105, 0]` grip | Use original curves and light silhouette. Model a coherent rear motor cover/vents, visible shaft, tilt fork/joint and small grille fasteners. Keep base slim; no layered blocks, heavy cream rim or broad decorative trim. Subtle powder-coated metal response; clean silver grille; no fabric-like housing noise. | Matched front/side/rear, close view through the grille, base controls, avatar scale. Side view must show uninterrupted support from base through motor to rotor. |
| HAMMER | Rectangular blue-gray metal head with thin pale-brass ends, pale wooden handle, existing brown grip rings; original `0.345 × 0.635 × 0.175 m` bounds and `[0, 0.13, 0]` grip | Preserve the metal tool identity and original handle/head proportions. Add a credible handle eye and wedge/ferrule only where structurally appropriate, smooth metal edge highlights, restrained machining response, longitudinal pale grain and coherent grip contact. No cloth/ivory mallet head or unrelated green trim. | Matched full-object and head/eye close-ups; actual-avatar held alignment at the preserved grip; gameplay-distance scale. Label any static hand pose as a source preview until Unity handling is observed. |

These two scripts have not been implemented or rendered. Their source GLBs have been inspected read-only: FAN SHA-256 `67bd384322b83617ac801211fb487f6be4e57c9d712f1b294660c13957700cd7`; HAMMER SHA-256 `b397ce961e7d7369118ca573e4bd5853391a3a1ea4c59076c686f13f4312b67f`. All original game assets and earlier Wordbound revisions remain preserved. Original editable vault `.blend` sources have not been located; the pilot's editable Blender scene is derived from the supplied GLB geometry and does not misrepresent its provenance.

## Acceptance and recovery

- [x] Correct original-game references inspected; wrong DetailV2 direction recorded as owner-rejected.
- [x] SOFA source geometry/component structure inspected without Blender; isolated script and root command prepared.
- [x] V1 native build and matched first pair directly inspected; explicit rejection saved for floating seam and exposed trim.
- [x] V2 native build executed by root; original primary geometry/bounds/grip, seam contact and receipt checked in this lane.
- [x] V2 matched three-quarter original/candidate images directly inspected by producer; visible V1 defects resolved in that view.
- [ ] Independent root V2 appearance judgement and the remaining views.
- [ ] Front, side, rear, detail, clay and original-avatar scale views directly inspected; any construction defects repaired in a new revision.
- [ ] Owner appearance acceptance; then bounded FAN and HAMMER implementations/reviews.
- [ ] Approved visuals translated to runtime maps/exports, Unity authored materials/pivots, actual held views and performance validation.

Current V2 evidence includes root's native build/first-pair render and this lane's direct receipt/hash/image review. V1 failed visible construction review and remains preserved. No overall material-quality approval, Unity appearance acceptance, runtime replacement or production-ready claim is made.

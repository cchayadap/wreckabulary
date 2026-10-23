# Original-game FAN and HAMMER construction pilots

STATUS: BOTH PILOTS BUILT BY ROOT — FAN rear-detail pair reviewed; HAMMER unrendered; neither approved for runtime replacement

## Native build evidence addendum — 23 October 2026

Root built HAMMER V1 with Blender 5.2.2 LTS in `ArtSource/Collections/WordboundPvP/OriginalBasedPilotHAMMERV1`. This lane read `.harness/delivery/hammer-pilot-v1-build.log`, the receipt and archived builder. The log reaches `TOOL_PILOT_BUILT` after saving the blend, with no bypass around the construction guards. I independently verified the saved blend, archived original GLB and both archived script SHA-256 values against the receipt, and confirmed the current original GLB still has the same hash.

The executed candidate-only seam weld and exact Boolean therefore passed the required closed-manifold checks before/after, unchanged steel-head bounds, five empty-eye section rays and two retained-steel rays. Those probes execute against the steel head before adding the wood tenon/wedge. Overall original/candidate bounds also match exactly in the receipt: Blender metres `(-0.1725000143, -0.0875000283, 0)` to `(0.1725000143, 0.0875000283, 0.6349999905)`. Grip Z is `0.1300000101` m, and configured held scale is `0.5669`. Original components: 11; candidate mesh parts: 13; candidate triangles: 6,880. These are source-pilot counts, not accepted delivery budgets.

| HAMMER artifact | Verified SHA-256 |
| --- | --- |
| Editable blend | `302328200307283b91648326c6ccd2187d5839ba25ff11372f052f779f843832` |
| Original source GLB | `b397ce961e7d7369118ca573e4bd5853391a3a1ea4c59076c686f13f4312b67f` |
| Builder | `b81bb6e9387a3bd247c63af9bd6b93676646e251dc139691b6a8039877193ff5` |
| Shared helper | `5f1d81614050a55cc1be070b47deb804d728a2154e7ebf23accc535a2bf9c900` |

The HAMMER preview folder contains **zero PNGs** at this check. Manifold/section success does not establish head shading, visible joint quality, hand fit or owner approval. No export, Unity import, runtime validation or visual pass is claimed. Root also completed FAN's build and one actual rear-detail pair; [its separate review](FAN-ORIGINAL-PILOT-V1-REVIEW.md) records useful construction from that angle and the remaining side/front/held/Unity gates. Original Wreckabulary remains the default. No additional build or render was run by this lane; the commands below are retained as the original staged specification, and the V1 build destinations now exist and must not be overwritten.

These are separate experiments built from the actual original game GLBs, not from either rejected Wordbound artwork revision. The current original artwork stays the presentation baseline. Preserve original assets, DetailV2 and all SOFA pilots. No source file is overwritten and no runtime asset is replaced. Root owns every Blender invocation and the shared heavy-work slot.

`Tools/AssetPipeline/build_wordbound_tool_pilots.py` imports the common preservation, component and studio helpers from the adjacent `build_wordbound_original_pilot.py`. Integrate both files before execution. It verifies the original source hashes, splits each item into its eleven original material/connectivity components with original transformed positions, UVs and split normals, and uses copies of the original materials. FAN retains every original component. HAMMER joins coincident steel-head seam vertices before cutting the handle eye; original source bytes remain untouched. Build refuses an existing output directory and rejects changed overall bounds beyond 0.01 mm. Original grip world matrices and configured miniature held scales are recorded.

| Item | Original source SHA-256 | Original dimensions (Unity metres) | Grip (Unity metres) | Held scale |
| --- | --- | --- | --- | --- |
| FAN | `67bd384322b83617ac801211fb487f6be4e57c9d712f1b294660c13957700cd7` | 0.522 × 0.821 × 0.250 | (0, 0.4105, 0) | 0.4385 |
| HAMMER | `b397ce961e7d7369118ca573e4bd5853391a3a1ea4c59076c686f13f4312b67f` | 0.345 × 0.635 × 0.175 | (0, 0.130, 0) | 0.5669 |

## Intended construction and review questions

FAN keeps the powder-blue base/rim, slender silver stand/front grille, pale blades and warm rotor cap. Additions are a connected rear motor shell and shaft, open rear ventilation over a recessed interior, a stand neck and tilt supports/axles, a rear wire guard with its own inner support ring, rim clips and a small base speed dial. Their purpose is to make the existing appliance construction understandable from the side and rear. They should not turn the fan into a bulky industrial machine. No dirt or brown surface noise is introduced. Source coordinates place the new parts inside the original bounds and deliberately overlap their intended joints; actual contact and visual normal quality still require native inspection.

HAMMER keeps the blue-gray steel head, original thin warm end caps, pale timber handle and seven dark grip rings. An exact Boolean cuts an elliptical handle eye through the steel-head interior; a matching wood tenon overlaps the original handle and fills that eye, with a small inset steel wedge at its top. The detail should explain how head and handle join. Check that the Boolean has not damaged head shading or its outer silhouette, that the tenon does not float, and that the wedge reads as a fitted joint instead of a decoration. No mallet silhouette, replacement caps, aging or extra bolt clutter is added.

Direct read-only GLB topology inspection found 342 steel-head vertices at 218 unique positions: 240 one-face boundary edges in the UV-split source, but a closed manifold when coincident positions are joined. The pilot therefore welds **only the candidate steel head** at 0.0000001 m before the Boolean. Native build guards require a closed manifold before/after, unchanged head-specific outer bounds, five vertical section rays that miss steel inside the new eye and two outside-eye rays that hit retained surrounding steel. These checks prevent a failed Boolean from masquerading as success just because the unchanged end caps preserve overall bounds. They are native assertions prepared in source, not executed results; head shading and joint fit still need rendered review.

Both pilots retain separate editable mesh parts and original material nodes in a packed `.blend`. They are intentionally visual construction experiments: no two-material/6,000-triangle runtime-budget claim, no FBX/GLB delivery exports, no map baking and no Unity replacements. After a visually worthwhile result, group suitable delivery meshes/materials, bake any required maps, compare exports/reimports and measure runtime cost without degrading the accepted silhouette. Original editable components remain available.

## Root-owned staged commands

Prerequisites: original `Web/public/art/items/{FAN,HAMMER}.glb`, `Web/public/art/avatar.glb`, `Web/public/art/manifest.json`, actual `Assets/_Project/Data/Config/items.json`, the two adjacent builder scripts, Blender 5.2.2 LTS and a free heavy slot. No paid provider is used. The commands below create new isolated V1 destinations only.

Build FAN:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_tool_pilots.py' -- --item FAN --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1' --stage build
```

First render FAN's motor/construction detail pair, then inspect before adding other views:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_tool_pilots.py' -- --item FAN --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1' --stage render --views detail --size 960 --samples 64 --threads 6
```

Build HAMMER:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_tool_pilots.py' -- --item HAMMER --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotHAMMERV1' --stage build
```

First render HAMMER's head/handle joint pair:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_tool_pilots.py' -- --item HAMMER --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotHAMMERV1' --stage render --views detail --size 960 --samples 64 --threads 6
```

Only if those details are useful and clean, repeat the corresponding render command with `--views three-quarter,side,rear`, then `--views front,clay,scale,held`. Do not render a whole set through a visible construction fault. Preserve a failed revision and fix the source for a new destination. No assertions should be weakened to force acceptance.

## Evidence and limits

The build receipt records item source, builder/helper/blend hashes, Blender version, unchanged overall bounds/grip, actual component/triangle counts, the added construction and limitations. It copies the unmodified original item GLB and both scripts into `Source/`, and packs the editable blend. A render receipt records camera matrix, framing, samples, threads, resolution, Blender version, source/render hashes and PNG hash. Existing previews are reused only when every current setting and the PNG hash agree. The local gallery includes only actual completed receipt-verified images and rejects unmatched pair settings.

Original/candidate views use identical studio lighting and camera framing. The `scale` view adds the original avatar; `held` uses its original `Hold_OneHand` action and right-grip pose at frame 1, with configured original item held scale. It restores the matching imported NLA action slot explicitly and fails if the action, slot or grip is absent. **This is a static Blender source reference, not Unity hand-fit or gameplay evidence.** Blender bone orientation and runtime avatar setup must still be checked in Unity. No mock screenshots or generated concept images stand in for rendered output.

The original source-preparation check completed Python AST syntax, original GLB SHA checks, bounded construction-coordinate/connection review, direct original-head topology inspection, exact output-path and resume-metadata review. Independent input-lane source review passed after adding the candidate-only weld/section guards and explicit animation slot: the reviewer checked preservation, isolated output refusal, world-coordinate component copies, unchanged bounds, held-root grip algebra, and render/gallery pair consistency. The reviewer confirmed the steel-only section rays execute before adding the tenon/wedge and use correct identity-transform candidate coordinates. Subsequent root build evidence is recorded in the dated addendum above; held-pose imagery, HAMMER appearance and all export/Unity gates remain pending.

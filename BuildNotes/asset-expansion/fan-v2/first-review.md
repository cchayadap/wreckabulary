# FAN V2 first independent visual review

STATUS: REVIEW COMPLETE — REVISE BEFORE VISUAL ACCEPTANCE. 2026-10-26. Reviewer: story_status, independent of the producer. Only this report was written; no asset/source edits, imports, rendering or native tests.

Inspected all three 640×640 three-quarter images: [original](../../../ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV2/Previews/FAN-three-quarter-original.png), [construction V1](../../../ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV2/Previews/FAN-three-quarter-construction-v1.png), [vivid V2](../../../ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV2/Previews/FAN-three-quarter-vivid-v2.png), their JSON capture records and [build receipt](../../../ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV2/build-receipt.json). Camera matrices, lighting records, orthographic scale (1.0508799553), resolution, neutral-studio world, AgX transform and zero exposure match. These are Blender studio comparisons, not original-game or Unity gameplay captures.

## Verdict

**V2 is a visible material improvement over V1, but is not yet the detailed, physically convincing finished asset James requested.** The stronger blue enclosure and warmer blades/hub give clearer colour separation and retain the original fan identity. The next useful step is a small construction/readability correction and all-angle/motion review, not more wire detail or blanket gloss.

## Findings from the images

| Criterion | Observation | Consequence / next change |
| --- | --- | --- |
| Vividness | V2's blue base/rim is distinctly stronger; the cream blades and gold hub separate better than the pale V1 image. | Keep this palette direction for another comparison. Do not infer it will match in Unity until the shader/lighting transfer is checked. |
| Original identity | Circular blue rim, three broad pale blades, gold centre, single stem and rounded rectangular foot remain recognizable. | Preserve these major shapes. The improved version should still read as Wreckabulary's household FAN beside original characters/equipment. |
| Guard construction | Several spoke ends project beyond their circular support, especially at the upper-left, left edge and lower-right of the front cage. Similar protrusions are already visible in the original; added rear wires increase the number of competing ends on the right. | Terminate each structural spoke at a credible captured/welded joint or rounded clamp. Check the projected silhouette from front/side/rear before adding detail. Original provenance is not a reason to keep visibly unfinished joints. |
| Blade readability | Broad blades remain legible at this large size, but the V1/V2 rear spokes form a dense network behind the original front cage. Some thin parts cross the blade edges and centre. | Keep front/back protection visually distinct in depth and thickness. Simplify redundant rear detail where it adds no readable construction; test actual game-camera size before deciding density. |
| Material character | Blue enamel now has highlights; metal is more distinct. Large surfaces still have very uniform finish, with no visible painted motif, localized roughness or manufacturing detail that establishes a distinctive crafted object. | Add restrained, purposeful treatment: enamel lip/recess separation, a small motif/colour block, or directional metal finish. Avoid random scratches/noise. Full textures are not automatically better than well-authored material/geometry detail. |
| Controls | The small ivory base dial is visible but visually weak; its tiny dark mark does little to explain motion or state at this view. | Enlarge or clarify a simple index/tick relationship if this is intended as a readable control. A decorative control must not falsely imply an interactable action. |
| Supports and rotor | The rear motor and support hardware add plausible construction, but this angle hides enough of the shaft, tilt supports and depth that their attachment/clearance cannot be judged reliably. | Side/rear/detail views and a real rotor/tilt sweep are required. No claim that the blades clear the guards or the neck supports the head is justified by this still. |

This is an art/design assessment, not a claim that the asset is physically unsafe or that a stylized fan must copy a commercial product. The requested grounded fantasy benefits from readable attachment, weight and movement even when its power source or effects are fictional.

## Receipt findings and their limits

- V2 adds no geometry to V1: **68 mesh parts and 16,006 triangles** before/after. These are source counts, not measured draw calls or approved runtime budgets.
- Five material roles are specified: blue `#2362C3` (metallic 0, roughness .42), metal `#99AAB5` (.9/.28), ivory `#F0D99F` (0/.5), gold `#EAB44B` (.85/.28), recess `#202A34` (0/.76). V2 includes no new painted texture, skeleton, animation clip, collider or Unity asset.
- `FAN_RearMotorShell` has **no UV layer** and records **64 boundary / 64 nonmanifold edges after diagnostic welding**. `FAN_OriginalFrontGrille_07` records **6 nonmanifold edges after diagnostic welding**. Inspect these specifically; a vented/open visual shell can have intentional boundaries, so counts alone do not prove a bad mesh or invalid physics. They also do not certify a collider or a watertight printable object.
- The file reports preserved source assets and retains the original 0.4385 held scale. GLB/FBX reimport checks and rigid-control discovery exist; none establishes current Unity material fidelity, hand fit, motion clearance or a playable online item.
- The hierarchy separates rotor/base/tilt-head groups; it is a rigid articulation layout, not skeletal skinning. This is appropriate for a fan if pivots, limits and parent relationships pass motion checks.
- No LODs or full-rotation/held/physics/online/player-build tests exist in this receipt. The review page explicitly has no front/side/rear/detail renders yet.

## Next comparison and acceptance work

1. **Repair visible construction first.** Round/capture protruding grille ends and clarify mount relationships in an isolated candidate. Preserve original files and retain the large silhouette. Do not solve the issue by simply hiding the cage from one camera.
2. **Render matched front, side, rear and detail views.** Include original, V1 and revised candidate at the same scale/light/camera. Check guard depth, shaft/bearing connection, tilt stops, base support and dial mounting. Keep the current three-quarter comparison as history.
3. **Check real motion.** Sweep the rotor through a full turn and tilt through its intended limits, with clips/frame sheets and minimum clearances. Check guard/blade/shaft intersections, pivot drift, shadow flicker and stop/restart. Preserve the gameplay action timing; a mechanical animation must not promise a different force or activation window.
4. **Review in the game context.** Use the normal camera, held scale and original character. Check resting, carried, equipped, thrown and active states in bright/dark rooms and at 720p/1080p. A large studio render cannot establish silhouette or detail readability in play.
5. **Author materials for Unity.** Verify colour space, material assignments, metallic/roughness conversion, coat support and normals. Check that ivory remains nonmetallic and that metallic parts remain visible in actual environment lighting. Add purposeful painted variation only after the base material transfer is correct. Fix the missing UV before any texture task that depends on it.
6. **Measure and simplify.** Count actual renderers/material slots/draw calls and texture/memory cost after import. Consolidate compatible static parts while retaining rotor/tilt authoring controls. Add LODs that retain the blue ring/blade/hub silhouette; inspect switching and wire aliasing. Do not assume 68 source parts require 68 draw calls, or that a 16k-triangle hero asset is acceptable across many simultaneous items.
7. **Verify physics separately.** Use an intentional collider/center-of-mass design for resting/held/thrown behavior; visual guard wires are not automatically useful colliders. Check floor contact, pivot/grip alignment, collisions under intended tilt, multiplayer synchronization and reproducibility. No real-world mass value is established by this review.

The improvement can proceed as a candidate; it should not replace the accepted default or be described as fully detailed/production-ready until these checks and James's visual review are complete.

## Evidence identity

| File | SHA-256 |
| --- | --- |
| build-receipt.json | `777c7a47d0590de0e7e2d0ff4148f5f67ce90c84e8e8a5428ce4ba278beb6e10` |
| FAN-three-quarter-original.png | `fcf0103438f7dff95483081a15846b4c1a5214e2b46b534e227ce6836b28571e` |
| FAN-three-quarter-construction-v1.png | `1106591187faad75a8b7b8f894c7df2731e4cca6a3de38dfc2fb7669d7e65652` |
| FAN-three-quarter-vivid-v2.png | `1a3a57fcf8bd46e96b46f07be91d87623f00323387a78fa08547f637c568daab` |

No new tests were run. Receipt fields were inspected, not regenerated. This review deliberately leaves unseen angles, motion and gameplay behavior unscored.

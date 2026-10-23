# Wordbound equipment — DetailV2

Twelve rounded household objects with wood grain, woven fabric, glazed ceramic, painted surfaces and brass fittings. The set keeps the original palette, dimensions, hand grips and gameplay statistics.

## Edit the designs

Open `Source/WordboundPvP.blend` in Blender. Each item has its own named collection and separated components; only BAT is initially visible. Use the Outliner to show another collection. The original shape parts, new seams, grille rings, joinery and fittings remain individually editable. The two shared materials use the small source atlases in `Exports/`.

The FBX and GLB files are delivery versions. Static components sharing a material and parent pivot are merged to reduce rendering overhead. FAN's rotor and CLOCK's hands retain separate pivots. The editable Blender source is not merged.

## Use and customize in Unity without writing code

After this verified source folder is in the Unity project, use **Wreckabulary → Art → Build Detailed Wordbound Equipment Collection**. It validates the delivery before importing the twelve models, icons and material maps into the dedicated Wordbound collection. **Build Wordbound Equipment Collection** restores the original revision. Rebuilding deliberately reapplies generated material settings, so keep personal designs in separate prefabs and materials.

For a personal design, create a prefab variant or a separate prefab from the imported model. Change its renderer materials and visual transforms in the Inspector. Keep `Grip_R` at the intended hand position, retain positive scale and omit physics components, cameras, lights and scripts. The gameplay system supplies collision and behavior separately.

Create or select a **Wreckabulary → Wordbound → Visual profile** asset. Add an override using the item ID (for example `BAT` or `SOFA`) and drag the personal prefab into **Model**. Leave **Materials** empty to keep the individual renderer materials assigned in that prefab. Assign the profile to the course or launcher. Held-defense offsets and rotations are editable in the same profile.

Camera settings live in the separate **Camera profile** asset: distance, field of view, pivot height, pitch limits and input sensitivity are Inspector controls. The project menu **Tools → Wordbound → Create or verify Inspector authoring assets** prepares the default course, camera and presentation assets. The integrated Unity authoring guide covers course placement, preview and playtest return.

## Measured delivery budget

- 275 editable mesh components; 27 delivery meshes across twelve assets. The original delivery used 174 meshes.
- Two shared materials per item; 4,908 maximum triangles, below the 6,000 limit.
- Shared 512 × 256 color, normal, roughness and Unity mask sources. Unity consumes color, normal and mask; Blender/glTF use roughness.
- Color is sRGB. Normal is linear tangent-space +Y. Unity mask uses red for metallic (zero on paint) and alpha for smoothness (`1 − roughness`). Brass remains the second material.
- Original dimensions, feet origin and `Grip_R` are unchanged within the recorded numerical tolerances. The 24 independent reimports measure all empty/pivot transforms too.

The reduction is a measured mesh/material-draw count, not a claimed frame-rate improvement. Native Unity validation, game-camera appearance and performance are separate gates owned by the integrator. Owner style/feel acceptance remains pending.

## Evidence and provenance

`manifest.json` binds source, generator, concepts, maps, exports and previews by SHA-256. `export-verification.json` records the 24 independent FBX/GLB reimports. `delivery-verification.json` records passing channel, dimension, budget, alpha and file-integrity checks. `visual-review.json` records 25 inspected final views and their limits. The local gallery is `BuildNotes/equipment-polish/ASSET-REVIEW.html`, relative to the repository root.

The exact geometry builder, later render-only builder and original base builder are archived in `Source/`. `source-provenance-verification.json` verifies their hashes and the two render batches. The final geometry was not rebuilt for the six-thread preview setting; the manifest preserves the original geometry-build hash separately. Local `.gitattributes` keeps these receipt bytes unchanged on Windows.

Concept images and their original prompts/provenance are copied without modification from the original revision. New geometry details and material maps are local procedural sources; no paid provider was called. Original source files remain available one directory above this revision and are not overwritten.

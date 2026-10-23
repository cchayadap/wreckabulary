# Detailed equipment — independent Unity source review

STATUS: SOURCE REVIEW COMPLETE; ONE NATIVE COVERAGE GAP; NO BLOCKING SOURCE DEFECT OBSERVED

Scope: read-only review of `wordbound-integration` at observed HEAD `93d4e35`, covering detailed import, material maps, geometry/grips/pivots, runtime colliders and presentation, mesh/LOD treatment, and player packaging. Assets are frozen. No Unity process, compiler, test suite or source modification is part of this review.

The asset lane has independently verified 24 Blender reimports and the final file/channel/hash receipts. Those checks do not establish Unity rendering, runtime performance or Windows build contents. This report separates source findings from native evidence still owned by the integrator.

## Finding: native mesh/pivot evidence is incomplete

`WordboundEquipmentImporter.AssetRecord` (line 89) does not deserialize `export_mesh_count`, `material_draw_count` or `export_pivots`. `ValidateImported` (line 225) verifies actual vertex bounds, ground origin, `Grip_R`, front marker, triangles, UVs and the two remapped materials. The current detailed native test (line 54) calls that validator but does not assert the exact optimized renderer totals or FAN/CLOCK pivot matrices.

This is an evidence gap, not an observed import failure. Blender independently reimported every authored pivot, and Unity sets `optimizeGameObjects = false`. Native Unity still needs to establish that the 27 delivery meshes and articulated pivots survived its own importer. Expected mesh/material-draw counts are two per item except FAN = three and CLOCK = four. Compare the authored FAN rotor and both CLOCK hands against the manifest after converting Blender's Z-up coordinates into Unity's Y-up coordinates; retain scale and orientation, not just pivot position. Root authorized a bounded follow-up in the dedicated detail test class only.

## Source-level closure

| Concern | Observed path and conclusion |
| --- | --- |
| Verified revision | Importer lines 118–158 requires the twelve approved IDs, passing delivery/24-format receipts, matching catalogue dimensions/grips/scales and exact source hashes. `BuildDetailed` explicitly selects DetailV2; the active receipt is written after native validation. The older menu explicitly selects Original. |
| Color and data maps | Importer lines 51–87 sets color/icons to sRGB, normal/mask to linear, normal type with no height conversion or green flip, mask alpha from input, bilinear clamp, 512 cap and mipmaps except icons. Lines 324–342 assign URP/Lit color, normal and metallic/smoothness maps; detailed paint uses smoothness multiplier one, alpha channel zero and matching keywords. Brass has no normal/mask. No map-channel defect found in this source path. Native compression/render appearance remains unverified here. |
| Materials stay distinct | Lines 194–197 remap both named source materials; lines 262–267 reject any other or missing renderer material and require both shared materials. `ModelVisual.Spawn` lines 12–21 returns the Wordbound instance before legacy tactile skin material processing. Detailed standard equipment therefore keeps its maps; non-standard skins intentionally retain their existing library path. |
| Geometry and grips | Importer lines 31–46 preserves file scale, bakes axis conversion, imports normals, computes Mikk tangents, disables hierarchy optimization and mesh compression, and does not create colliders. Lines 225–267 measure imported vertices, feet and the authored grip/front at 1 mm tolerance. Resolver lines 96–109 uses the measured collection bounds and finds the authored grip after canonical fitting. `PlaceAtHand` line 169 offsets by the transformed grip after held scale/rotation, preserving hand contact. |
| Gameplay collision | `WordboundGameplayBody.Apply` lines 44–54 sets approved root capsule/box dimensions independently. Presentation lines 110–115 requires a unit-scale collider-free visual child. Resolver lines 86–90 rejects visual 2D/3D colliders and rigidbodies. Added seams, grilles and fittings therefore do not become extra gameplay colliders. |
| Defense pose | Gameplay presentation lines 209–224 checks the exact held defensive item and valid lease, then rotates around its authored grip. `WordboundDefensePose.PlaceAroundGrip` line 40 subtracts the transformed grip from the hand position; zero offsets retain contact. This is visual posing, not collider movement. |
| Meshes and LOD | The importer instantiates the already grouped FBX hierarchy; it does not generate or flatten a new LOD hierarchy. No collection LOD construction exists in the reviewed presentation path. The source budget is at most 4,908 triangles per item. Native renderer counts and frame time remain required; no FPS or LOD savings are inferred from the export count. |
| Inspector customization | `WordboundVisualProfile` validates render-only prefabs with positive scales, preserves their own bounds/grips, and offers optional per-renderer slot overrides. The delivered README correctly recommends leaving the override Materials array empty to preserve different materials on separate one-slot meshes. The importer deliberately reapplies generated material settings; personal prefab/material variants remain the documented editing path. |

## Player contents

`BuildFrom` copies only twelve FBX models, twelve icons, the color atlas, normal map and Unity mask into the dedicated `Assets/_Project/Art/Collections/WordboundPvP` folder. The runtime Resources collection references those models/icons/materials. These three imported material maps are intended player dependencies.

It does not copy the standalone roughness PNG, GLB exports, editable `.blend`, archived builder scripts, concept images, review previews or gallery. Those remain under root `ArtSource` or `BuildNotes`, outside Assets. `ProductionBuild.Build` (line 41) sends registered scenes to `BuildPipeline.BuildPlayer` and has no source/gallery copy step. `ProjectSetup.Run` refreshes the separate imported-art library; `ImportedArtSettings.IsImportedArt` is restricted to `Assets/_Project/Art/Imported/` and does not override this dedicated collection's settings. No source/gallery inclusion path was found in the reviewed project build scripts.

This is a source conclusion, not a sealed player-file audit. After building, inspect `packed_assets.json` plus loose output files: the existing `BuildReportExport` writes both packed source-asset paths and player files from Unity's actual BuildReport. Confirm the three runtime maps are present as dependencies and the source-only items above are absent. No new player has been inspected in this review.

## Evidence and handoff

- Importer SHA-256: `645b06db3081b4be08f7ce5b74be6922a60270aaad52d776c6c1ce2983189c47`.
- Existing detail-test SHA-256: `bc97f2868fec1fe0722cbf47868a48d0ad3500c96179910dbd8d5f0cb50ccb33`.
- Resolver SHA-256: `f57b1ea182dbce95cca4efa93655e0d13cd6814c1227a144cb369ae8f4126508`.
- Gameplay presentation SHA-256: `898ece4c72be5ac3e735c24ad231212422cbab84aeffbbf232883d5a7df590ea`.

No importer/runtime source was changed. No Unity/native execution occurred. Next: add the authorized exact renderer/pivot assertions in the dedicated test class, then root compiles and runs that explicit fixture against the imported detailed revision, followed by game-camera, performance and packed-player verification.

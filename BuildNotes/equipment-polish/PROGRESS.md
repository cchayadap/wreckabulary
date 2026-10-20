# Equipment detail and editable presentation

- [x] Inspected all twelve actual Wordbound three-quarter renders and the legacy Arcade/Winter shield native screens.
- [x] Confirmed current editable Blender source, two-material pipeline, 512px texture cap, 6000-triangle cap and immutable catalogue dimensions/grips.
- [x] Audited current Unity usability: collection entries expose model/icon/size/grip/heldScale but imported FBX roots are not independent authoring prefabs; the dedicated importer overwrites material values from constants; default held placement follows the hand without a defensive pose. James owns the new persistent visual/camera profiles and authoring workflow.
- [~] Added persistent WordboundDefensePose type and narrow GameplayPresentation hook. Profile field is James-owned; resolver Profile exposure is root-owned. Compilation and native tests NOT RUN for this new slice.
- [~] Prepared separate DetailV2 Blender builder, independent delivery validator, bounded material-atlas importer and two explicit native tests. Source only; no geometry/textures have been generated and no native tests have run. Root reserved WordboundEquipmentImporter.cs for this lane.
- [ ] Blender build, independent export validation, actual previews and source comparison after root grants a heavy-process slot. No render or model batch has run in this slice.
- [ ] Native material/prefab/pose validation and owner acceptance remain pending.

Visual direction: preserve the rounded household-object silhouettes, sage/cream/terracotta identity and readable game scale. Improve material differentiation with directional wood grain, restrained textile weave, glazed ceramic and painted-metal surface response, plus selective seams/joints/fasteners. All twelve current models have useful component separation, but their shared smooth palette finish makes these materials hard to distinguish. This is an agent inspection, not human style approval.

Defensive pose source uses actor-local rotations around the authored grip. Defaults keep the grip exactly at the hand; PLATE rotates its horizontal dish 90 degrees to face forward. Persistent offsets are bounded artist hand-placement corrections. Pose applies only to the exact selected Held item named by the canonical defense lease; release/expiry/migration/fault/unavailable world resets it immediately. Render blending is visual only. Existing carried furniture and keepsake paths are preserved.

Integration: `c235a46` imports the already-integrated selected-equipment presentation baseline and must not be cherry-picked into root. Apply the later pose delta over the root's current GameplayPresentation plus James's VisualProfile and root's VisualResolver.Profile hook. Root owns native Unity execution and all generated metadata.

## DetailV2 source checkpoint

The new builder uses the original explicit builder file as a library and refuses to overwrite an existing output directory. It copies the original concept/prompt/provenance bytes, builds a separate editable blend with named per-item collections, and exports FBX/GLB without changing the catalogue. Every original dimension, Grip_R and held scale remains the target of the existing independent export verifier. New details are bounded seams, bristle fibres, joinery, tuft buttons, grille rings and clock markings; measured triangles remain pending, with the existing 6000 cap enforced.

Lightweight source checks: Python AST parse with explicit UTF-8 and Node `--check` passed; `git diff --check` passed. The initial Python parse used the machine's cp950 default and failed decoding the UTF-8 collection label; the explicit UTF-8 retry passed. These are syntax checks, not Blender execution, Unity compilation or visual acceptance.

Four 512x256 source maps represent eight existing palette colors across wood/textile/ceramic/paint rows. Color is sRGB. Tangent normals are linear +Y; Unity explicitly uses NormalMap with no height conversion or green flip. The linear URP mask uses R=0 metallic and A=1−roughness; the separate linear grayscale roughness map feeds Blender/glTF. The delivery validator decodes map pixels, verifies those channels and ranges, and binds copied concepts and geometry contracts to the untouched original revision. Brass remains the second material. Native material correctness is still NOT RUN.

`WordboundEquipmentImporter.BuildDetailed()` selects the verified DetailV2 source and writes the active source receipt only after completing native model validation. Existing `Build()` restores the original source and clears normal/mask references and shader keywords. `ReadVerifiedManifest()` follows that bounded receipt, so existing native triangle checks match the imported revision. Original source files remain intact; the active dedicated collection is intentionally replaceable through either menu. Shared ModelLibrary, skins, equipment stats and catalogues are not edited.

Next recoverable commands, only after root grants the heavy-process slot:

```powershell
$blenderPath = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
$originalPath = 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP'
$baseBuilderPath = 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_pvp.py'
$detailPath = 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-map-workshop/ArtSource/Collections/WordboundPvP/DetailV2'
& $blenderPath -b --python Tools/AssetPipeline/build_wordbound_detail.py -- build --base-builder $baseBuilderPath --source $originalPath --output $detailPath
& $blenderPath -b --python Tools/AssetPipeline/build_wordbound_detail.py -- verify --base-builder $baseBuilderPath --source $originalPath --output $detailPath
& $blenderPath -b --python Tools/AssetPipeline/build_wordbound_detail.py -- render --base-builder $baseBuilderPath --source $originalPath --output $detailPath --items BAT SOFA FAN
```

Inspect those actual three-quarter images before finishing the remaining native Blender previews. Then run `node Tools/AssetPipeline/verify_wordbound_detail.mjs <DetailV2-folder> <original-folder>`. Root alone runs the native importer and exact explicit `Wreckabulary.EditorTests.WordboundEquipmentDetailTests` plus existing collection/presentation tests. The current preparation is not a completed art delivery or a human usability/style acceptance.

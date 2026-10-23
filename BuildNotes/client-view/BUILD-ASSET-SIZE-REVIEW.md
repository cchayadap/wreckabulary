# Build asset size review

STATUS: READ-ONLY AUDIT COMPLETE; proposed packaging change NOT IMPLEMENTED. The latest requested QA build includes5,344,466 packed bytes of optional Wordbound equipment despite the original artwork default. No build, Unity, deletion, source/config/asset mutation or unrelated project scan. Only this report is owned by the online lane. Existing unrelated dirty ModeDefinition and historical logs are preserved.

## Evidence identity and measurement

Root source observed at audit start: `5d332dfe`. Inspected `Builds/qa-20261023T174755Z/{build.json,packed_assets.json}`, actual build files, current imported artwork/metadata, DetailV2 manifest, collection/model library, profile, importer and runtime callers. The build is Unity6000.6.3f1, release-qa, development=false, profile `Assets/Settings/Build Profiles/Win-QA.asset`, build source `a4f9c1fc195c3ec23536053cd9cc9c71ac5d0c10`, recorded tree `c99251f2d4a6c9799d21b06c9de29c9391cc0e7caa4aa38f51ba410ebbf52f8f`.

Packed manifest SHA256 is `69b748b08d27e04e2ec412f0ff81916c0ef449ef0c1464a24d418ea4d2dcda6b`, length6,198,719bytes. Its27,344 content records sum to140,684,717 packed bytes. The kit exporter copies Unity BuildReport.packedAssets contents' sourceAssetPath, GUID, type and packedSize directly (`Packages/com.gamestudiokit.core/Editor/Ship/BuildReportExport.cs:88`). Summation retains separate subobjects and streaming records rather than incorrectly deduplicating paths.

The227 manifest build-file records sum to246,335,117bytes. The actual build directory currently totals252,537,674bytes including exported audit/build metadata and other files. Those figures are not equivalent to packed asset content, a distributable archive size or resident memory. No installed size or fps saving is promised.

## Optional content that actually ships

All rows below appear in `resources.assets` and, for mesh/texture streams, `resources.assets.resS`:

| Optional content | Distinct paths | Packed bytes |
| --- | ---: | ---: |
| `Art/Collections/WordboundPvP/Models/*.fbx` |12|1,651,750|
| `Art/Collections/WordboundPvP/Icons/*.png` |12|3,163,160|
| Palette, normal and mask maps |3|524,864|
| WordboundPvPPaint / WordboundPvPBrass materials |2|3,556|
| `Resources/Collections/WordboundPvP.asset` |1|1,136|
| **Total** |**30**|**5,344,466**|

This is5.0969MiB, or3.7989% of the manifest's total packed content. The same30 source files total3,337,868bytes on disk, excluding metadata. Texture import/serialized objects explain why source-byte size is not packed-byte size. Shared shaders are deliberately excluded from this removable-group total: URP/Lit is used elsewhere and cannot be credited as optional-only savings.

The12 optional models/icons are BAT, BROOM, HAMMER, SPEAR, SHIELD, PLATE, TABLE, SOFA, BED, STOOL, FAN and CLOCK. The largest optional model is SOFA307,534packed bytes, followed by BED200,342 and CLOCK188,142. All12 icons are packed at roughly263–266KB each and collectively exceed the model bytes.

There are **zero manifest paths containing DetailV2**, which does not mean that revision is absent. `Assets/_Project/Art/Collections/WordboundPvP/SourceRevision.json` says DetailV2; the importer deliberately copies the selected revision into shared paths. Read-only SHA256 comparison found all12 current FBXs, all12 icons and all3 maps match `ArtSource/Collections/WordboundPvP/DetailV2/manifest.json`. Git diff from build head to current root found no changes in these imported files, collection, original model library, resolver/profile or relevant model/icon loading code. Thus the active imported dependency group in this build is the DetailV2 selection, even though the path omits that revision name.

There are zero `ArtSource/*` packed records. Editable Blender sources, concepts, historical exports and previews outside Assets are not shown as shipped content in this manifest; deleting them would not address this measured dependency group.

The12 corresponding original `Art/Imported/Items/*.fbx` paths are also present, totaling1,256,052packed bytes. All12 original GUIDs are referenced by `Resources/ModelLibrary.asset`. That number covers those model files only, not the entire original material/skin/avatar library. Keep original assets and the other game directions intact.

## Why the default does not remove the optional group

`WordboundVisualProfile.BaseArtwork` defaults to OriginalWreckabulary(0); the current Presentation.asset has no serialized override of that new field and its Overrides list is empty. LaunchAssets and assigned FlagshipCourse both reference that profile. WordboundVisualResolver chooses the optional collection only when the caller allows it and a present profile explicitly selects WordboundEquipment; overrides still win. ModelVisual's ordinary path uses ModelLibrary and the existing skin/tactile materials.

That is a runtime selection, not build dependency selection. `Resources/Collections/WordboundPvP.asset` is always a Resources root; its12 entries directly reference every optional model and icon. FBX importer remaps point to the2 collection materials; the paint material references all3 maps. This matches Unity's documented rule that Resources assets and their dependencies are included independently of scene use. [Unity Resources loading and dependencies](https://docs.unity3d.com/6000.0/Documentation/Manual/assets-resources-system-load.html), [Unity Resources API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resources.html).

Searching serialized `.asset`, `.prefab` and `.unity` files under this project's Assets for the24 model/icon GUIDs found only the collection asset. The collection GUID itself had no other such serialized reference. This is source evidence for a single removable dependency root, not a complete AssetDatabase dependency query or rebuilt exclusion proof.

`WordboundEquipmentCollection.Load()` is a lazy Resources.Load. Production callers are ModelVisual's explicit wordbound branch, the resolver's selected collection metadata, the QA workload's selected-artwork observation and GeneratedItemArt's explicit wordbound=true branch. Current production GeneratedItemArt caller ItemArt uses its default false; true icon callers found in source are tests. A normal default original rendering need not load this optional collection, but it still ships. Do not infer that all5.1MiB is resident or causing the measured CPU/network failure.

## Smallest safe packaging direction

Propose an ordinary direct-reference optional collection, not a new Addressables/server/storage subsystem: move the collection asset out of every Resources directory through Unity's AssetDatabase, preserving its GUID and all editable models/materials/source history. Update the project-owned importer so rebuilding either optional revision continues to write that non-Resources collection. Keep the default original profile free of a serialized reference to it; an explicitly selected optional profile should own the collection reference so Unity includes its exact dependencies when that profile is built. Existing per-item override references must continue to include their selected models/materials naturally.

For a no-code workflow, an editor inspector/setup action should assign the collection when WordboundEquipment is chosen and leave a clear missing-selection diagnostic. Original selection must not retain a hidden collection reference: a serialized reference pulls dependencies into a player even if runtime code never reads it. Optional source assets remain selectable and editable in Unity; retain Editor asset lookup for preview/tests rather than a global player Resources root.

Compatibility must be decided explicitly before implementation. Null-profile resolver calls currently permit the legacy collection, and public ModelVisual/GeneratedItemArt wordbound=true calls obtain it through Load. Moving only the asset would silently fall back to originals and break explicit optional behavior. Preserve those APIs in Editor and in builds that intentionally include the collection, and make the built player's configured collection choice explicit; do not redefine null-profile semantics accidentally to achieve a smaller build. The current production local/online paths already pass the configured profile, while authored preview can have a null profile. A complete proposal therefore includes loader/profile/importer and focused compatibility tests, not just an asset move.

Do not remove imports, clear collection entries, lower global texture settings, strip shared shaders, alter ModelLibrary, edit metadata by hand, or temporarily move Resources assets around a build with unverified restoration. All asset movement remains integrator-owned through the gate; no source or asset change has been made by this audit.

## Verification needed for an approved change

Run R0 and the affected presentation/equipment tests first. In native Unity verify default originals, explicit optional selection, null-profile compatibility, per-item overrides, skinned fallback and actual mesh/bounds/grip parity. Build the same QA profile and inspect the fresh packed_assets manifest: an original-only build should have no optional collection/model/icon/map/material records unless an intentional override references them. Then build a profile that opts in and verify the selected collection is present and works in a visible player. Compare actual packed totals and build-file totals separately, with source/build IDs and hashes; do not assume an exact5,344,466-byte package reduction.

Reproduction of the core measurement requires only reading the retained manifest: flatten `packed[].contents[]`, select paths beginning `Assets/_Project/Art/Collections/WordboundPvP/` plus exact `Assets/_Project/Resources/Collections/WordboundPvP.asset`, and sum packed_size. Group those rows by path/type/packed file for inspection. This audit ran only that JSON aggregation, scoped file-length/SHA checks, Git comparisons and source/GUID searches; no editor import, compilation or build was run.

# Art delivery packaging source review

STATUS: COMPLETE — source review only; final Windows packed-asset verification pending

Reviewed root checkpoint `b4a2cca` without Unity, builds, source edits or deletion. Scope: ArtSource versus runtime Assets inclusion, dedicated equipment import, existing runtime references and bounded safe optimization candidates. Final packed-asset evidence remains root-owned.

## What crosses the import boundary

`WordboundEquipmentImporter.BuildDetailed()` verifies `ArtSource/Collections/WordboundPvP/DetailV2` and copies an explicit allowlist into `Assets/_Project/Art/Collections/WordboundPvP`. It does not copy the source directory recursively.

| Source | Unity destination / treatment |
| --- | --- |
| Twelve `Exports/<ID>.fbx` files | `Models/<ID>.fbx`; imported models referenced by the collection |
| Twelve `Icons/<ID>.png` files | `Icons/<ID>.png`; imported sprites referenced by the collection |
| `Exports/WordboundPvPPalette.png` | `WordboundPvPPalette.png`; sRGB paint atlas |
| Manifest normal and mask maps | `WordboundPvPNormal.png` / `WordboundPvPMask.png`; linear maps referenced by the paint material |
| Paint and brass settings | Two generated shared materials under `Materials/` |
| Verified entry metadata | `Assets/_Project/Resources/Collections/WordboundPvP.asset`, containing models, icons, size, bounds, grip and held scale |
| Active revision | `SourceRevision.json` under the art root; read by the Editor verifier, not referenced by the runtime collection |

The `.blend`, builder receipts/scripts, GLB alternatives, standalone roughness map, concepts, prompts, provenance, verification JSON and preview renders remain under `ArtSource`. The review gallery/reports remain under `BuildNotes`. Neither root is a player-content directory, and this import path does not place them in Resources or StreamingAssets. The GLB and roughness files are valid alternate-format/source deliverables, not required duplicates in the Unity player.

The two build paths inspected do not recursively package these folders: `WordboundDeliverySetup` calls the bounded detailed importer, authoring setup and public-course export; `ProductionBuild` invokes normal Unity BuildPlayer with enabled scenes after project setup. This source review covers those paths, not every possible custom packaging script or a future manual ZIP of the entire checkout. Deliver the built-player folder rather than the repository when preparing the Windows download.

## Runtime dependencies that should remain

The Resources collection loads at `Collections/WordboundPvP`. Its model/icon references, model material remaps and material map references make those intended runtime dependencies. The launch Resources asset at `Assets/_Project/Resources/Wordbound/LaunchAssets.asset` also references the assigned authoring course and camera/visual profiles. User-assigned override prefabs/materials reachable from those profiles are intentional player content even if their folders sit outside Resources.

The existing `ModelLibrary`, `MaterialLibrary`, original item icons, seasonal collections and tactile shader seeds in Resources are not established dead weight. `ModelVisual.Spawn` selects the dedicated collection for standard Wordbound items, while other skins, other visual keys and legacy modes still use the shared libraries. Removing the apparent duplicate legacy BAT/FAN/etc. assets solely because DetailV2 exists would break that fallback or another work direction. No such deletion is recommended.

## Concrete bounded optimizations

- **Already applied:** delivery-only grouping by pivot/material reduces the twelve exports from 174 original meshes to 27 delivery meshes/material draws, while retaining 275 separately editable source components. This is measured structural reduction, not a measured frame-time gain. FAN and CLOCK articulation pivots remain separate.
- **Already applied:** twelve models share two materials and three 512×256 runtime atlas maps. The separate roughness source is not copied because its smoothness information is packed into mask alpha. Import disables model animation/blendshapes/cameras/lights, collider generation and Read/Write; texture Read/Write is off, texture cap is 512, compression is CompressedHQ, and icon mipmaps are disabled. No new compression or LOD changes are justified without native appearance measurements.
- **Preserve the narrow import:** keep future editable sources, galleries and alternate exports outside Assets. Add only runtime-required files to the explicit importer; use profile references for intentional overrides. This prevents source bloat without sacrificing editability.
- **Inspect the actual player before pruning:** the existing `BuildReportExport` produces `packed_assets.json` with source path, GUID, type and packed size. For the final successful Windows build, verify the twelve intended models/icons and material dependencies, confirm no source/gallery/GLB/roughness input leaked into the build, and rank real packed sizes before proposing removals. The source receipt should not be treated as runtime-required merely because it sits under Assets. No current packed-size saving is claimed.

No safe additional deletion was demonstrated by this source-only review. Library/import caches, earlier art revisions and recovery evidence were left untouched. The source already prevents the main editable-delivery duplication; runtime shared-library pruning requires cross-mode dependency evidence.

## Evidence and limits

Read source: `Assets/_Project/Editor/Art/WordboundEquipmentImporter.cs` (constants, preprocessing, BuildFrom, material creation); `Scripts/Art/WordboundEquipmentCollection.cs`; `Scripts/Art/ModelVisual.cs`; `Scripts/Online/Client/WordboundLaunchAssets.cs`; `Scripts/Online/Authoring/Editor/WordboundAuthoringSetup.cs`; `Editor/Online/WordboundDeliverySetup.cs`; `Editor/ProductionBuild.cs`; `Packages/com.gamestudiokit.core/Editor/Ship/BuildReportExport.cs`. Paths abbreviated beneath `Assets/_Project` where unambiguous. Asset grouping evidence is the verified DetailV2 manifest and delivery report, already committed separately.

No native build, packed-assets parse, memory measurement, removal, cache cleanup or compression change was performed in this review. The root gate must establish final player size, runtime references and appearance after import. See [Unity editing guide](../../docs/wordbound/UNITY-EDITING-GUIDE.md) for the no-code workflow and [actual asset gallery](ASSET-REVIEW.html) for completed Blender views; final Unity captures remain pending.

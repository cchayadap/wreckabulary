# Art delivery packaging source review

STATUS: COMPLETE — source review only; final Windows packed-asset verification pending

## Current Windows packaging follow-up — 23 October 2026

The original artwork default has changed since the initial review below. `WordboundVisualResolver.cs:24` now selects the optional collection only when allowed by the caller and explicitly selected by the supplied profile; assigned `Presentation.asset` defaults to Original Wreckabulary. `Resources/ModelLibrary.asset` directly references original item/avatar/environment meshes. `Resources/Collections/WordboundPvP.asset` directly references the optional twelve models and icons, which remain a supported Inspector choice. `GeneratedItemArt.cs:32` also loads that collection for explicit Wordbound icon requests. Merely changing the default does not make either library an unused dependency. Do not delete originals, optional imports or their source revisions.

I rechecked `WordboundEquipmentImporter.cs:171–194`: only the selected FBX/icon/map inputs cross the explicit import boundary. It does not copy `.blend`, pilots, alternate GLB exports, galleries or reports. No `Assets/StreamingAssets` directory exists in this checkout. The production entry point still calls ordinary `BuildPipeline.BuildPlayer` (`ProductionBuild.cs:53`); delivery setup imports the selected runtime collection and creates authoring/public-course assets, without copying ArtSource. Source-only evidence therefore supports exclusion of the following editable inputs from these build paths. A final packed report must confirm the actual player.

Observed local source-file sizes, from bounded directory metadata reads (not packed/runtime memory sizes; child rows overlap the parent total):

| Local path | Files | Bytes | MiB |
| --- | ---: | ---: | ---: |
| `ArtSource/Collections/WordboundPvP` | 260 | 115,104,968 | 109.773 |
| `…/DetailV2` | 101 | 35,420,803 | 33.780 |
| `…/OriginalBasedPilotV1` | 10 | 8,360,897 | 7.974 |
| `…/OriginalBasedPilotV2` | 18 | 12,064,103 | 11.505 |
| `…/OriginalBasedPilotV4` | 10 | 8,326,478 | 7.941 |
| `…/OriginalBasedPilotFANV1` | 10 | 8,429,427 | 8.039 |
| `…/OriginalBasedPilotHAMMERV1` | 5 | 6,688,696 | 6.379 |
| `Assets/_Project/Art/Collections/WordboundPvP` | 63 | 3,428,416 | 3.270 |
| `Assets/_Project/Art/Imported/Items` (all 40 models plus meta) | 80 | 3,852,584 | 3.674 |
| `Assets/_Project/Resources/UI/Items` (all 40 icons plus meta) | 80 | 1,789,951 | 1.707 |

The optional runtime-input folder contains 999,520 bytes of twelve FBXs, 2,121,409 bytes of twelve PNG icons and 8,105 bytes of two material files (these subtotals exclude `.meta`; atlas maps/receipt account for other files). The matching twelve original FBXs total 1,254,512 bytes and their twelve original icons 457,640 bytes, also excluding meta. These are different artwork inputs with supported references, not byte-identical accidental duplicates. The largest optional source files are CLOCK/BED/SOFA/STOOL icons at 236,378 / 221,621 / 214,402 / 210,948 bytes; source PNG length does not establish packed texture cost.

The existing `Builds/qa-20261020T175830Z/build.json` has `ok:false`, recorded head `4a0bbfafb4b5ff942736637ce116974beef8c2e9`, and no `packed_assets.json`. Its corresponding build log ends during build-program work with `Shut down.` rather than a successful final summary. Although partial player files exist, this is not a successful current player and provides no reliable final package-size baseline. No total Windows size or expected saving is claimed.

Concrete low-risk actions are to keep the narrow import boundary, package only a successful player output directory, and inspect its generated `packed_assets.json` before pruning anything. `BuildReportExport.cs:85–103` already records actual source paths, GUIDs, types, packed sizes and build files. Require no `ArtSource/`, `BuildNotes/`, pilot `.blend` or alternate GLB entries; rank actual packed textures/models. Optional icons are the largest measured optional source category and a sensible future packed-size/visual sampling target, but their importer already caps at 512, uses CompressedHQ, disables Read/Write and omits icon mipmaps (`WordboundEquipmentImporter.cs:68–73`). No lower texture setting is justified from this source audit alone. Separating optional icons/models into independently loaded content could be investigated if measured residency matters, but it is an architectural change, not a low-risk deletion or a demonstrated current saving.

No safe additional deletion was identified. No cache, source, artwork choice, import setting, player file or default profile was changed. Existing revisions and partial-build evidence remain preserved. The remainder below records the earlier source review; its statement that the dedicated collection is the default is superseded by this addendum.

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

The existing `ModelLibrary`, `MaterialLibrary`, original item icons, seasonal collections and tactile shader seeds in Resources are not established dead weight. `ModelVisual.Spawn` can select the dedicated collection when explicitly requested for a standard-skin Wordbound item; the current assigned profile defaults to original artwork, while other skins, other visual keys and legacy modes also use the shared libraries. Removing the apparent duplicate legacy BAT/FAN/etc. assets solely because DetailV2 exists would break the default, fallback or another work direction. No such deletion is recommended.

## Concrete bounded optimizations

- **Already applied:** delivery-only grouping by pivot/material reduces the twelve exports from 174 original meshes to 27 delivery meshes/material draws, while retaining 275 separately editable source components. This is measured structural reduction, not a measured frame-time gain. FAN and CLOCK articulation pivots remain separate.
- **Already applied:** twelve models share two materials and three 512×256 runtime atlas maps. The separate roughness source is not copied because its smoothness information is packed into mask alpha. Import disables model animation/blendshapes/cameras/lights, collider generation and Read/Write; texture Read/Write is off, texture cap is 512, compression is CompressedHQ, and icon mipmaps are disabled. No new compression or LOD changes are justified without native appearance measurements.
- **Preserve the narrow import:** keep future editable sources, galleries and alternate exports outside Assets. Add only runtime-required files to the explicit importer; use profile references for intentional overrides. This prevents source bloat without sacrificing editability.
- **Inspect the actual player before pruning:** the existing `BuildReportExport` produces `packed_assets.json` with source path, GUID, type and packed size. For the final successful Windows build, verify the twelve intended models/icons and material dependencies, confirm no source/gallery/GLB/roughness input leaked into the build, and rank real packed sizes before proposing removals. The source receipt should not be treated as runtime-required merely because it sits under Assets. No current packed-size saving is claimed.

No safe additional deletion was demonstrated by this source-only review. Library/import caches, earlier art revisions and recovery evidence were left untouched. The source already prevents the main editable-delivery duplication; runtime shared-library pruning requires cross-mode dependency evidence.

## Evidence and limits

Read source: `Assets/_Project/Editor/Art/WordboundEquipmentImporter.cs` (constants, preprocessing, BuildFrom, material creation); `Scripts/Art/WordboundEquipmentCollection.cs`; `Scripts/Art/ModelVisual.cs`; `Scripts/Online/Client/WordboundLaunchAssets.cs`; `Scripts/Online/Authoring/Editor/WordboundAuthoringSetup.cs`; `Editor/Online/WordboundDeliverySetup.cs`; `Editor/ProductionBuild.cs`; `Packages/com.gamestudiokit.core/Editor/Ship/BuildReportExport.cs`. Paths abbreviated beneath `Assets/_Project` where unambiguous. Asset grouping evidence is the verified DetailV2 manifest and delivery report, already committed separately.

No native build, packed-assets parse, memory measurement, removal, cache cleanup or compression change was performed in this review. The root gate must establish final player size, runtime references and appearance after import. See [Unity editing guide](../../docs/wordbound/UNITY-EDITING-GUIDE.md) for the no-code workflow and [actual asset gallery](ASSET-REVIEW.html) for completed Blender views; final Unity captures remain pending.

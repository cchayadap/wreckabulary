# Household expansion staging

STATUS: PASS — source staging verified 2026-10-26. Owner: asset_intake. Scope: a non-destructive eight-model source package in `ArtSource/Collections/HouseholdExpansionV1`, with byte-identical FBX files, the supplied license and explicit verification/provenance. No Assets writes, imports, native jobs, normalization or runtime replacement. This verdict is not Unity, physics or visual acceptance.

Selection uses one supplier, Kenney Furniture Kit 2.0, to avoid introducing a second palette/scale convention in the first batch. It includes three kitchen fixtures, two attic storage objects, two rooftop props and one Packing Yard parcel. The drawer cabinet is a drawer cabinet, not a hinged chest; the cardboard box is a parcel, not a wheeled trolley or wooden crate.

Package: [HouseholdExpansionV1](../../ArtSource/Collections/HouseholdExpansionV1/README.md). Receipt: [manifest.json](../../ArtSource/Collections/HouseholdExpansionV1/manifest.json).

The package contains exactly eight copied FBX files and one copied license, totaling **324,813 bytes** before its README/manifest. Exact selected models are `kitchenStove`, `kitchenFridge`, `kitchenCabinet`, `bookcaseClosedDoors`, `cabinetBedDrawer`, `pottedPlant`, `bench` and `cardboardBoxClosed`. The GLB counterparts total **1,962 triangles**. No alternate format duplicates or preview image bundles were copied.

Every FBX and the license was checked against the earlier immutable intake hash before copying, then its destination bytes were compared. The original eight FBXs, eight measurement GLBs and license were rechecked after staging. Existing differing destination bytes would have stopped the operation rather than being overwritten.

The eight selected FBX files are ASCII FBX 7.3. Their declared Model objects are rigid Mesh objects; they contain zero AnimationStack, AnimationLayer, AnimationCurve, Deformer, Texture or Video objects. The corresponding GLBs also contain no skins, animations or morph targets. No shared texture was omitted: these selected models use material color factors, with no supplied texture references. All eight declare Y-up, positive front Z and UnitScaleFactor 100. Actual Unity scale, handedness, imported materials and pivot behavior remain NOT-RUN.

Dimensions in the package manifest were measured from the actual GLB POSITION values transformed through scene nodes, not only guessed from filenames or supplier preview images. The manifest keeps the measurement-source hash separate from the FBX hash; this does not assert identical FBX import geometry. The fridge has separate `doorFreezer`/`doorFridge`; the bookcase has `doorLeft`/`doorRight`; the drawer cabinet has `door`/`drawer`; the stove and kitchen cabinet have `door`. Those names can support later hinge/slide setup. The plant is a separate rigid child, not a wind skeleton.

## Unity importer pattern for the root

The simplest existing preservation-oriented model is `Assets/_Project/Editor/Online/WordboundPackingClubSetup.cs`:

- `CheckCopy` / `CopyNew` around lines 464–477 compare hashes and copy only missing source files; a changed existing file is preserved and rejected for review.
- `EnsureFolder` around line 479 uses `AssetDatabase.CreateFolder`, letting Unity create metadata.
- `ImportModel` around line 294 performs synchronous `AssetDatabase.ImportAsset`, configures rigid models, disables automatic colliders/animation and creates material remaps only on first import.
- The create-or-validate prefab flow around lines 61–79 uses `PrefabUtility.SaveAsPrefabAsset` and preserves already saved assets.

Use that **pattern**, not its current entry list or exact validator. Its validator requires one mesh, two submeshes and two palette materials; this furniture batch has multiple rigid child nodes and varying material counts, so that invariant is unsuitable. Preserve the source model hierarchy in an editable wrapper and validate the expected child names/material list from the new manifest instead. Do not reuse a builder that rewrites existing materials on every run.

Recommended follow-up: new isolated furniture art/material/prefab folders; per-model target dimensions and bottom-centered wrapper origin; existing character/camera comparisons; explicit simple colliders; door/drawer joints only after axes and clearance checks; a saved review room with all eight props. Keep the runtime map catalogue and existing equipment unchanged until imported visuals and physics have been verified.

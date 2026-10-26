# Household importer draft

STATUS: READY FOR SOURCE REVIEW. Native compilation, FBX import, EditMode tests, visual review and physics checks are NOT-RUN. This folder is outside Unity's Assets tree; none of these C# files is active yet.

`patch` adds three files, whose paths and hashes are recorded in `manifest.json`:

- `WordboundHouseholdSetup.cs`: a missing-only **Wreckabulary → Authoring → Create Missing Household Studio** menu tool.
- `WordboundHouseholdSettings.cs`: an Inspector asset with eight explicit initial target heights. Existing saved prefab dimensions remain authoritative on repeat setup.
- `WordboundHouseholdSetupTests.cs`: four EditMode cases covering complete output preservation, corrupt source rejection, manifest path traversal and destination path traversal.

The tool reads `ArtSource/Collections/HouseholdExpansionV1/manifest.json` and verifies the exact nine source payloads before creating project assets. It copies FBXs/license into `Assets/_Project/Art/Collections/HouseholdExpansionV1`, keeps original rigid child hierarchies, disables animation/automatic colliders, and creates editable URP materials from each imported material's color and surface values. The source stove/fridge glass has half-opacity; the material setup retains transparency rather than turning it opaque.

Saved prefab paths are `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/<Kitchen|Attic|Rooftop|PackingYard>/<sourceName>.prefab`. Each wrapper scales uniformly to its explicit intended height and centers its base. The source FBX remains unchanged. Only the closed cardboard parcel gets one conservative static box collider; the other seven prefabs explicitly retain collision-review-pending names, with no Rigidbody, joints or invented animation.

Settings are saved at `Assets/_Project/Data/Wordbound/Authoring/HouseholdExpansion.asset`. Defaults: stove/counter 0.9 m, fridge/bookcase 1.8 m, drawer cabinet 0.6 m, plant 1.2 m, bench 0.9 m, parcel 0.45 m. These are initial review values, not tested physical dimensions. Change saved prefab transforms directly to adjust an already created asset; changing initial settings never resets authored prefabs.

`HouseholdStudio.unity` is created through `WorkspaceStudioSetup.CreateSceneIfMissing`, using its additive temporary-scene workflow. It saves eight prefab instances, a neutral review floor, camera and light, and preserves the user's current scene. Existing materials, prefabs, settings and scene are loaded without rewriting them. Source byte conflicts or incomplete/changed importer configuration fail for inspection rather than overwriting user work. Missing folders and metadata are created by Unity.

No gameplay catalogue, recipe, original equipment, build scene registration, networking, package, harness or runtime source changes are included. The scene and assets are optional authoring content outside Resources.

## Integration and verification

1. Review this draft and apply the patch only after checking no destination file now exists. Let Unity generate metadata.
2. Run the project's official gate R0 and relevant engine-free checks, then compile and run `Wreckabulary.Tests.Editor.WordboundHouseholdSetupTests` in EditMode.
3. Run `Wreckabulary.EditorTools.WordboundHouseholdSetup.CreateOrVerify` through the official gate `exec`, record the first outputs, then repeat and verify preservation.
4. Inspect the saved scene in Unity at game-camera scale and close-up. Confirm axis/scale, glass, normals, source colors, underside geometry and editing. The tool deliberately fails if imported dimensions differ from the GLB measurements by more than 5% (or 2 mm); investigate conversion before changing that check.
5. Author and test appropriate colliders, hinges, drawer stops, weights and break behavior before adding any interactive map use. No current physics or visual acceptance is implied.

The existing Packing Club importer informed the missing-only source copy pattern; its one-mesh/two-submesh validator was not reused because these furniture models contain multiple rigid parts.

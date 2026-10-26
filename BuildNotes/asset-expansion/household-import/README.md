# Household importer draft

STATUS: FROZEN REVIEW DRAFT — revision 2 was applied by the root, with the integration correction below. This folder remains outside Unity's Assets tree and records the reviewed draft; the root owns current live source and native verification.

`patch` adds three files, whose paths and hashes are recorded in `manifest.json`:

- `WordboundHouseholdSetup.cs`: a missing-only **Wreckabulary → Authoring → Create Missing Household Studio** menu tool.
- `WordboundHouseholdSettings.cs`: an Inspector asset with eight explicit initial target heights. Existing saved prefab dimensions remain authoritative on repeat setup.
- `WordboundHouseholdSetupTests.cs`: four EditMode cases covering complete output preservation and changed-import-scale rejection, corrupt source rejection, manifest path traversal and destination path traversal.

The tool reads `ArtSource/Collections/HouseholdExpansionV1/manifest.json` and verifies the exact nine source payloads before creating project assets. It copies FBXs/license into `Assets/_Project/Art/Collections/HouseholdExpansionV1`, keeps original rigid child hierarchies, disables animation/automatic colliders, and creates editable URP materials from each imported material's color and surface values. The source stove/fridge glass has half-opacity; the material setup retains transparency rather than turning it opaque.

Saved prefab paths are `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/<Kitchen|Attic|Rooftop|PackingYard>/<sourceName>.prefab`. Each wrapper scales uniformly to its explicit intended height and centers its base. The source FBX remains unchanged. Only the closed cardboard parcel gets one conservative static box collider; the other seven prefabs explicitly retain collision-review-pending names, with no Rigidbody, joints or invented animation.

Settings are saved at `Assets/_Project/Data/Wordbound/Authoring/HouseholdExpansion.asset`. Defaults: stove/counter 0.9 m, fridge/bookcase 1.8 m, drawer cabinet 0.6 m, plant 1.2 m, bench 0.9 m, parcel 0.45 m. These are initial review values, not tested physical dimensions. Change saved prefab transforms directly to adjust an already created asset; changing initial settings never resets authored prefabs.

`HouseholdStudio.unity` is created through `WorkspaceStudioSetup.CreateSceneIfMissing`, using its additive temporary-scene workflow. It saves eight prefab instances, a neutral review floor, camera and light, and preserves the user's current scene. Existing materials, prefabs, settings and scene are loaded without rewriting them. Source byte conflicts or incomplete/changed importer configuration fail for inspection rather than overwriting user work. Missing folders and metadata are created by Unity.

Review revision 2 also creates each new wrapper inside its own explicitly active additive empty scene, closes only that temporary scene, and restores the previous active scene in `finally`. It never creates a transient wrapper in the user's scene. Repeat validation checks every setting pinned during import, including global scale, visibility, mesh compression, normals, tangents and readability. The prior draft patch/manifest/README remain in `review-v1/`.

No gameplay catalogue, recipe, original equipment, build scene registration, networking, package, harness or runtime source changes are included. The scene and assets are optional authoring content outside Resources.

## Integration and verification

1. Review this draft and apply the patch only after checking no destination file now exists. Let Unity generate metadata.
2. Run the project's official gate R0 and relevant engine-free checks, then compile and run `Wreckabulary.Tests.Editor.WordboundHouseholdSetupTests` in EditMode.
3. Run `Wreckabulary.EditorTools.WordboundHouseholdSetup.CreateOrVerify` through the official gate `exec`, record the first outputs, then repeat and verify preservation.
4. Inspect the saved scene in Unity at game-camera scale and close-up. Confirm axis/scale, glass, normals, source colors, underside geometry and editing. The tool deliberately fails if imported dimensions differ from the GLB measurements by more than 5% (or 2 mm); investigate conversion before changing that check.
5. Author and test appropriate colliders, hinges, drawer stops, weights and break behavior before adding any interactive map use. No current physics or visual acceptance is implied.

The existing Packing Club importer informed the missing-only source copy pattern; its one-mesh/two-submesh validator was not reused because these furniture models contain multiple rigid parts.

## Root integration evidence

Root applied revision 2 (patch SHA-256 `df9a016afeaf975f2d436092952447e6515fa3c18cc3ac1772800ee3c8064e88`). Independent source review found no remaining concrete blocker; it does not establish native behavior.

The first R0 check, `Logs/gate_check_all_20261026T104906Z.log`, failed because the harness's NUnit stub does not define `[NonParallelizable]`. Root removed only that redundant attribute from the live test fixture, preserving the four tests and all assertions; this historical draft and patch retain it. The subsequent `Logs/gate_check_all_20261026T105013Z.log` check passed in 26.5 seconds. No Tools/kit changes were made for this compatibility correction.

The root started the native EditMode `WordboundHouseholdSetupTests` gate at 2026-10-26T10:50:53Z with the pinned Unity editor. Its outcome is pending at this handoff; no native pass, visual acceptance or physics acceptance is claimed here. See `.harness/delivery/workspace-householdtests-2026-10-26T10-50-53-255Z-start.json` for the exact launch record. Do not reapply the historical patch over the root's live files.

Follow-up native evidence supersedes that pending state: `TestResults/editmode_20261026T105053Z.xml` passed 3/4, failing the new-additive-empty-scene call with an untitled unsaved scene. Root replaced it with copy-template/OpenScene(Additive)/close/delete-owned-scene handling. `TestResults/editmode_20261026T105601Z.xml` passed 3/4, failing the stove bounds check. `TestResults/editmode_20261026T110009Z.xml` passed 3/4 and confirmed a 10× FBX/GLB discrepancy: imported (4.3, 4.5, 4.5), GLB (0.43, 0.45, 0.45), root scale (1,1,1). The live importer now applies and validates an explicit `FbxUnitScale = .1f`; the per-item bounds check was not weakened. The historical draft retains the earlier code for provenance and must not overwrite these fixes. No all-eight native pass or visual acceptance is claimed by this report.

**Final importer test result for this handoff:** the corrected live implementation passed `TestResults/editmode_20261026T110408Z.xml`, **4/4 with zero failed/skipped**, in **118.6 seconds** at the official gate (fixture approximately 32.17 seconds). This supersedes the earlier native-failure state while retaining those failures as evidence. The tests exercise all eight imported models, saved height/base centering, glass alpha/material setup, collider boundaries, authored-output preservation, changed-scale rejection, corrupt-source rejection and traversal rejection. The permanent-path setup/repeat and rendered visual acceptance remain separate root-owned checks. The frozen draft itself is historical; the passing result belongs to the root's corrected live files.

# Household output and repeat-preservation inventory

STATUS: READ-ONLY PLAN — exact intended paths derived from the live importer and the eight supplied FBX material names. This is not a native setup result or a baseline hash capture.

Expected: **43 primary assets**, **102 files including their metadata and 16 generated folder metadata files**. The eight copied FBXs and copied license must match their staged source hashes.

Machine-readable complete lists: [household-output-paths.json](household-output-paths.json).

| Kind | Exact intended path |
|---|---|
| license | `Assets/_Project/Art/Collections/HouseholdExpansionV1/License.txt` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/bench/wood.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/bookcaseClosedDoors/metal.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/bookcaseClosedDoors/wood.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/cabinetBedDrawer/_defaultMat.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/cabinetBedDrawer/metal.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/cabinetBedDrawer/wood.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/cardboardBoxClosed/wood.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/cardboardBoxClosed/woodDark.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenCabinet/metal.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenCabinet/wood.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenCabinet/woodDark.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenFridge/glass.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenFridge/metal.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenFridge/metalDark.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenFridge/metalLight.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenStove/carpetWhite.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenStove/glass.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenStove/metal.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenStove/metalDark.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/kitchenStove/wood.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/pottedPlant/plant.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/pottedPlant/wood.mat` |
| material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/pottedPlant/woodDark.mat` |
| review-material | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/StudioFloor.mat` |
| model | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Models/bench.fbx` |
| model | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Models/bookcaseClosedDoors.fbx` |
| model | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Models/cabinetBedDrawer.fbx` |
| model | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Models/cardboardBoxClosed.fbx` |
| model | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Models/kitchenCabinet.fbx` |
| model | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Models/kitchenFridge.fbx` |
| model | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Models/kitchenStove.fbx` |
| model | `Assets/_Project/Art/Collections/HouseholdExpansionV1/Models/pottedPlant.fbx` |
| settings | `Assets/_Project/Data/Wordbound/Authoring/HouseholdExpansion.asset` |
| prefab | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Attic/bookcaseClosedDoors.prefab` |
| prefab | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Attic/cabinetBedDrawer.prefab` |
| prefab | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Kitchen/kitchenCabinet.prefab` |
| prefab | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Kitchen/kitchenFridge.prefab` |
| prefab | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Kitchen/kitchenStove.prefab` |
| prefab | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/PackingYard/cardboardBoxClosed.prefab` |
| prefab | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Rooftop/bench.prefab` |
| prefab | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Rooftop/pottedPlant.prefab` |
| scene | `Assets/_Project/Scenes/Authoring/HouseholdStudio.unity` |

Each primary asset also requires its `.meta`. Folder metadata is enumerated in the JSON, not inferred as a proof of completed setup.

For the repeat, snapshot all actual files under the Household art and prefab roots after the first stable successful setup, plus the saved settings/scene and their metadata. Compare the actual set against these43 intended paths; unexpected material names or files must be inspected. Union those outputs with the package source files, live source/helper files, original gameplay template, package/build scene configuration and the existing **146-file Home/Shop/Loadout/developer workspace baseline**. The proposed union is 275 unique paths. Keep the existing authored baseline intact.

The repeat must leave byte hashes and file membership unchanged, with no retained `HouseholdTemp-*` or `WorkspaceStudioTemp-*` scenes. Run the native capture only after this preservation check; capture output lives under BuildNotes and is deliberately outside the asset-preservation set.

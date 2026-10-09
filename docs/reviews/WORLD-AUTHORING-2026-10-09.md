# Editable world migration — 9 October 2026

STATUS: COMPLETE FOR WORLD LANE

World lane scope: `RoomBuilder`, `AuthoredHouse`, `HousePresentation`, `WorldAuthoring`, and `AuthoredHouseTests`. The coordinator owns scene organization, Unity execution, combined verification, and the final build.

## Saved worlds and runtime behavior

The one-time editor bake creates normal editable `Assets/_Project/Resources/Worlds/PinwheelHouse.prefab` and `GardenCourtyard.prefab` assets from the current shared layouts and imported furniture. Existing world assets are skipped, so rerunning the workspace upgrade cannot overwrite artist edits. Scene overrides remain authoritative when the saved `AuthoredHouse` map matches the selected map. Selecting another map loads its resource prefab. JSON generation remains a fallback when the selected authored prefab is absent.

The saved geometry retains colliders and transforms. Generated floor seams become persistent mesh assets; transient palette materials and property-block colors become persistent material assets under `Assets/_Project/Worlds/Generated`. `HousePresentation` is marked authored after persistence, preventing runtime repaint or regeneration. Directional lighting changes during the one-time bake are restricted to its active temporary scene.

Round reset preserves edited furniture transforms, materials and serialized component fields by cloning the initial authored hierarchy. The template lives under an inactive hidden parent before cloning, so template behaviors never receive `Awake`, `OnEnable` or `Start`. Restored live copies activate normally. An empty Moving Day reset does not discard the initial template. The template belongs to the scene world and is not placed under the transient gameplay-effects root.

Logical room boundaries, connectivity, spawn rules and objectives still come from JSON. Changes to room dimensions/topology must update that data deliberately; decorative placement and furniture edits use the saved prefab or scene override directly.

## Preservation and APIs

Editor API namespace: `Wreckabulary.EditorTools`.

```csharp
public static void WorldAuthoring.BakeWorldAssetsIfMissing();
public static void WorldAuthoring.UpgradeSceneWorld();
public static void WorldAuthoring.PersistPresentation(GameObject root, string assetStem);
public static void WorldAuthoring.OrganizeLegacyDependencies();
public static void WorldAuthoring.ArchiveLegacySceneDecor();
```

Upgrading a supported gameplay scene archives only the legacy root objects named `Room` and `Furniture` into a separate prefab under `Assets/_Project/Editor/Legacy/Scenes`. It removes those roots only after successful save and preserves unrelated scene roots. Existing authored scene instances are reused. The coordinator additionally byte-backs up the original scene and Player files in `Logs/authoring-backups`.

Archived prototype meshes/materials belong under `Assets/_Project/Editor/Legacy/Dependencies`. The dependency organizer moves only direct `legacy_*.mat` and `legacy_*.asset` files from the two generated-world dependency folders using `AssetDatabase.MoveAsset`, preserving GUID references. It selects a unique destination when the original filename already exists and leaves current-world dependencies untouched. Repeated runs are idempotent.

## Verification boundary

The coordinator reported the first migration process exited 0. Both world prefabs and the LivingRoom/MovingDay prototype archive prefabs were observed on disk. This lane reviewed source and checked diff whitespace; it did not launch Unity.

After the first migration, the snapshot was strengthened with an inactive parent before cloning, and legacy dependencies were redirected into the editor archive. These final changes were included in the native runs reported below.

Regression coverage added in `AuthoredHouseTests`:

- Edited geometry plus furniture position/rotation/scale/health/material survive Play and round resets; empty resets retain the original template.
- Courtyard selection loads its authored resource while disabling a saved Pinwheel instance.
- Snapshot creation does not execute artist-added behavior lifecycle callbacks; one live restore executes them once.

Native results are recorded below. Actual authoring workspace captures remain part of the coordinator's combined report.

## Final migration inspection

The second native migration log, `Logs/authoring-migration-final.log`, records `AUTHORING_WORKSPACE_READY` followed by a successful batch shutdown. A read-only scan found no `error CS` or `Exception` entries. There are 315 archived mesh/material assets under `Editor/Legacy/Dependencies` and zero `legacy_`-prefixed files remaining in the current-world generated dependency folders.

## Native regression results

This lane independently parsed the coordinator's final native XML results:

- `Logs/authoring-playmode.xml` (2026-10-09 00:48:39 local): 135 passed, 0 failed, 4 skipped. All three `AuthoredHouseTests` above passed, including the dormant template lifecycle regression.
- `Logs/authoring-editmode.xml` (2026-10-09 00:46:06 local): 194 passed, 0 failed, 7 skipped; aggregate result is `Skipped:Ignored`. All four scene cases in `SceneAuthoringTests.SavedScenesHaveCurrentPersistentVisualsAndThirdPersonPreview` passed.

Authored-world behavior and persistent scene/presentation checks are now natively verified. The coordinator owns final workspace captures and the refreshed Windows build. No additional world-lane source edits are pending.

World prefab SHA-256 after the non-overwriting migration:

| Prefab | SHA-256 |
| --- | --- |
| PinwheelHouse | `f453005844d66c83b0334d8a9486e4553ec939ef89f4df7477d6c719d0957723` |
| GardenCourtyard | `a8a063525398e32d7d860a0c523403a1033e7bca8cc21816f8aaa50b04056ce8` |

## Saved-scene decor correction

The coordinator's actual saved-scene screenshots revealed prototype text left outside the archived Room/Furniture roots. A narrow follow-up archive targets only these original root-name/text combinations:

| Scene | Root name | Exact TMP text |
| --- | --- | --- |
| LivingRoom | Sign | `<i>Home Sweet Home</i>` |
| MovingDay | Sign | `<i>Moving Day</i>` |
| MovingDay | Floor Label | `LIVING ROOM` or `BEDROOM` |

`ArchiveLegacySceneDecor` saves those roots into a separate `<scene>_LegacyDecor.prefab` before removing them. It is called before the workspace migration-label early return, so existing V1 scenes receive this correction while retaining their saved cameras, overrides and other custom objects. Hub signs and differently named/texted roots are preserved.

Final follow-up validation: `Logs/authoring-editmode-final.xml` reports **201 passed, 0 failed, 0 skipped**, including all four scene-authoring cases. Both LegacyDecor prefabs exist, and a read-only exact-text scan finds none of the legacy strings in LivingRoom/MovingDay scenes. This lane independently viewed all four replacement 00:59 local Unity LivingRoom/MovingDay saved-camera and overview images: the floating Home Sweet Home and Moving Day signs and old floor words are absent; colored materials, imported furniture and floor seams are intact. The replacement Hub camera image retains WRECKABULARY/DIBS/TYPE signs. These captured saved-scene states are visually accepted.

| Actual saved-scene capture | SHA-256 |
| --- | --- |
| LivingRoom-editor-overview.png / MovingDay-editor-overview.png | `b10de6cd948ac85bd42dac6bd73e3772e3201844a91d6582f46630fa2ebef4c0` |
| LivingRoom-saved-camera.png / MovingDay-saved-camera.png | `febd72264a132f73666ada4e6677475b21ad2f18bb857dc5410734b90b9d8df2` |
| Hub-saved-camera.png | `e177e88e81851e616ab355603818e84f418615f78597cb4917d1cb52f11b2215` |

Capture directory: `docs/reviews/evidence/unity-authoring-2026-10-09`. No additional source changes are pending from this lane.

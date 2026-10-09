# Current Unity authoring workspace — 9 October 2026

STATUS: COMPLETE — LOCAL IMPLEMENTATION AND NATIVE VERIFICATION

Maintained checkout: `wreckabulary`, branch `main`, baseline `4ae98b0`. All previous presentation work is retained, uncommitted. Portable harness run: `run-07108348-fb0a-4539-83ae-52448cf57be4`.

## Confirmed problem

The four saved scenes and Player prefab still contained prototype visuals. Arena Play mode hid the saved Room/Furniture and regenerated JSON content, so scene edits did not survive Play or round resets. Imported avatars existed only at runtime. The prototype rebuild command could overwrite live scene authoring and leave the test scene open.

## Work and ownership

- Root: scene migration, scene/camera preview, native import/tests/captures, launcher and source guide.
- Player lane: persistent imported Player avatar, stable runtime reuse, preview and regression tests.
- World lane: persistent editable map prefabs, preserve scene instances and reset templates, material/mesh serialization.
- Workspace lane: clear scene/source navigation, safe Hub startup and Play entry, isolated legacy factory.

## Preservation

Previous checkpoint: `Logs/checkpoints/2026-10-08-presentation-final`. Migration copies original scenes and Player prefab plus metadata to `Logs/authoring-backups` before any save. World/player migrations skip existing authored assets. Legacy scene geometry is preserved separately before removal from current scenes. Sibling extracted projects and ZIP archives remain untouched.

## Verification

- Native migration completed successfully in Unity 6000.6.3f1. Both map prefabs and all four scenes were saved; Player uses a nested imported avatar. A second migration applied persistent scene labels and moved 315 legacy mesh/material dependencies under Editor/Legacy with their GUIDs intact. The live generated mesh folder now contains eleven current meshes.
- Final full EditMode: **201 passed, zero failed, zero skipped**, `Logs/authoring-editmode-final.xml`. The initial workspace-fixture skips were fixed by allowing the disposable batch-runner scene while preserving interactive unsaved-work protection.
- Full PlayMode: **135 passed, zero failed**, `Logs/authoring-playmode.xml`. Four explicit capture cases were skipped by that normal run. The two current capture journeys then passed separately in a graphics-enabled editor, producing **137 latest passing PlayMode/capture cases**; two older explicit capture fixtures remain skipped.
- Twenty-one actual Unity images are in [the current gallery](evidence/unity-authoring-2026-10-09/index.html): eight saved-scene camera/overview renders and thirteen runtime screenshots including overlay UI. Saved scenes are captured without Play; the first camera frame is primed before readback to avoid first-upload white material placeholders.
- Visual review found separate legacy wall signs and Moving Day floor labels outside the original Room root. A narrow scene/name/TMP-text filter now archives those exact objects to LegacyDecor prefabs. Replacement images confirm the floating labels are gone and Hub signs remain.
- Final runtime capture refresh passed after the decorative cleanup. Windows build **Succeeded, zero errors**, 137,145,350 reported bytes: `Builds/Windows/Wreckabulary.exe`. The standalone startup smoke was responsive, initialized Direct3D 11 and logged no exceptions/errors before controlled shutdown. This is a startup check, not a second full standalone playthrough.
- Raw XML/logs are local under `Logs/authoring-*`; the gallery's `test-results.json` records latest outcomes and their exact raw source files, and `windows-build.json` records the built executable and smoke result. No gameplay code changed after the full gameplay suite.
- Incidental URP build-prefilter caches, TMP fallback cache and GameData field ordering were preserved in `Logs/authoring-generated-cache.patch` and reverted from the delivered diff. Pre-existing GraphicsSettings/UnityConnectSettings changes were retained.

## Implemented authoring behavior

- Four canonical scene names/GUIDs remain stable, with Hub first in build settings. Current Workspace exposes their edit buttons, both world prefabs, Player, inventory and the actual HUD/menu source.
- Play uses matching authored world instances and falls back to the selected saved map prefab. Furniture reset clones the initial authored state from a dormant snapshot, preserving transforms, materials and component overrides.
- Current palette, floor seam meshes and default outfit materials are persistent assets. Artist edits are not repainted or regenerated during Play.
- Scene migration uses a persistent SceneAsset label, independent of editable hierarchy names. The visual-only authoring player is disabled at runtime and stripped from builds. Scene inspection tests use isolated preview scenes; captures restore inspection cameras and refuse to displace unsaved authoring work.
- Historical prototype generation is isolated under Editor/Legacy/Generated and rejects production overwrite before any mutation. Old scene geometry is retained under Editor/Legacy/Scenes, separate from live scenes.
- The wrapper folder contains START-HERE.md and OPEN-LATEST-UNITY.cmd so older extracted copies cannot be mistaken for the maintained launcher target.

## Scope

House geometry/furniture and the player are directly editable. Logical room graphs, spawn rules and objectives still come from JSON and must be kept in sync when changing room topology. HUD/menu layouts remain C# authored; inventory UXML/USS is a separate UI Builder asset. Online networking and a replacement 3D character sculpt are outside this authoring cleanup.

## Recovery

Final local snapshot: `Logs/checkpoints/2026-10-09-authoring-final/`, containing the tracked binary patch, copied untracked files, wrapper launcher/guide and status. Changes remain local and uncommitted. Open the wrapper launcher or this checkout in pinned Unity, then use Current Workspace. The next optional work is ordinary level/UI authoring; no migration, test or build task remains pending.

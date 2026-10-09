# Editing the current Unity game

Open this checkout with Unity **6000.6.3f1**. Use **Wreckabulary → Open Current Workspace**. The Hub is the default Play entry; choose **Play the opened scene** to test the level you are editing. Your open scene is restored after Play.

| Edit | Current source |
| --- | --- |
| Hub layout, desk, door, decorations | `Assets/_Project/Scenes/Hub.unity` |
| Arena presentation / scene overrides | `Assets/_Project/Scenes/LivingRoom.unity` |
| Shared Pinwheel house geometry and furniture | `Assets/_Project/Resources/Worlds/PinwheelHouse.prefab` |
| Garden Courtyard geometry and furniture | `Assets/_Project/Resources/Worlds/GardenCourtyard.prefab` |
| City Flat | `Assets/_Project/Resources/Worlds/CityFlat.prefab` |
| Terrace House | `Assets/_Project/Resources/Worlds/TerraceHouse.prefab` |
| Walk-up Apartments | `Assets/_Project/Resources/Worlds/WalkupApartments.prefab` |
| Shared environment lighting profile | `Assets/_Project/Resources/Environment/SunlitHouse.asset` |
| Daylight sky material | `Assets/_Project/Materials/Environment/DaylightSky.mat` |
| Saved world materials / textures | `Assets/_Project/Worlds/Generated/Materials` / `Textures` |
| Cooperative Moving Day scene | `Assets/_Project/Scenes/MovingDay.unity` |
| Tutorial layout / teaching anchors | `Assets/_Project/Scenes/Tutorial.unity` |
| Character, imported model, outfit preview | `Assets/_Project/Prefabs/Player.prefab` |
| Gameplay HUD / lobby | `Scripts/Game/GameHud.cs`, `GameHudCards.cs` / `Scripts/UI/Lobby` under `Assets/_Project` |
| Inventory layout | `Assets/_Project/Resources/UI/Inventory/Inventory.uxml` and `.uss` |
| Recipe balance and enabled powers | `Assets/_Project/Data/Config/items.json` |
| GPT item illustration source PNGs | `Assets/_Project/Art/Generated/Items` |
| Runtime item Sprite references | `Assets/_Project/Resources/UI/Generated/PowerItemArt.asset` |
| Room boundaries, door graph, spawn rules, objectives, recipes | `Assets/_Project/Data/Config` |

The house prefabs are real editable geometry and furniture. Play uses the authored scene instance when its map matches the selected map, otherwise the selected map prefab. Round reset restores the initial authored furniture transforms and components. Edit the shared prefab to affect all modes; apply scene overrides deliberately when you want to share them.

The workspace's **Worlds** section opens all five current prefabs directly. Save your prefab changes, return to the Hub, then choose that house in **PLAY** to inspect the actual game. The four scenes are mode entry points; the five house prefabs supply their environments. There is no need to create a separate scene copy for each house.

Use **Hide Ceilings in Scene View** to reach room interiors while editing. It uses Unity's editor visibility only, preserves objects you already hid, and restores its own temporary visibility changes before a scene or prefab closes, or when the workspace window closes. Saved geometry and gameplay remain unchanged.

Use **Lighting & Materials** to select the shared Sunlit House profile, daylight sky, or current material assets in the Inspector. Edit the saved profile for shared lighting changes; edit a renderer's assigned material for its surface. These navigation buttons do not regenerate geometry or apply a migration. Keep imported source art under `Art`, active world assets under `Resources/Worlds` and `Worlds/Generated`, and historical references under `Editor/Legacy`.

The profile controls sun direction, warm and cool light colors, ambient fill, fog, exposure, contrast, saturation and the practical-light budget. Its settings are applied when the scene enters Play, so save the profile and restart Play to review an edit. Edit the profile's sun direction instead of the runtime Sun transform. Individual practical lights belong to their world prefab; graphics quality controls how many nearby lights are enabled. **Wreckabulary → Authoring → Add Daylight and Practical Lighting** installs missing scene lighting and preserves an existing profile.

JSON still defines logical room boundaries, connectivity, objectives and spawn rules. Keep those in sync when changing room dimensions or adding rooms; moving a decorative prop does not require regenerating JSON. Existing world prefabs are never silently regenerated from JSON.

The saved camera and **Authoring Preview (Editor Only)** show the current character and close third-person lens before Play. This visual preview contains no player controller, disables itself on Play, and is stripped from builds. Edit the Player prefab for actual character changes. Runtime cameras follow player count and input, so tune `CameraRig` for gameplay framing.

Hub/menu and gameplay HUD layouts are currently built in C#. Their workspace buttons open the actual source. The inventory layout is a separate UI Builder presentation asset. Empty legacy TMP seed labels in the HUD supply references used by the current runtime controller.

The [recipe guide](RECIPES.md) covers all 24 usable items. **Power Item Illustrations** selects editable PNGs; **Power Item Sprite Library** selects their runtime references. After deliberately replacing or adding generated illustrations, use **Art → Rebuild Generated Item Library** to import the six power icons and save their references. Other recipe icons continue to use their imported item thumbnails. JSON controls mechanics; artwork never changes damage, timing or letter costs.

**Authoring → Upgrade Current Scenes** creates missing authored assets and preserves edits within the current authoring version. A version migration archives the previous worlds and scene instances, including overrides, before installing current geometry. It also backs up original scenes/prefabs to local `Logs/authoring-backups`. The former prototype rebuild is isolated under **Wreckabulary → Legacy** and cannot overwrite production scenes. Preserved prototype geometry lives under `Assets/_Project/Editor/Legacy`; it is excluded from shipped scene content. Test scenes under `Assets/_Project/Tests` are test fixtures, not game entry points.

Current build scenes remain Hub, Tutorial, LivingRoom and MovingDay with Hub first. No version-numbered scene copies are used by the build.

The explicit `EnvironmentCaptureTests` fixture records native runtime frames of all five houses: overview, centered third-person interior, upward architectural detail, the Garden sky, upper floors, and mixed-floor couch play. It writes a JSON frame manifest beside the PNGs, including actual camera state and lighting. These frames complement the camera, lighting and cutaway regression tests; an editor overview alone does not verify what a player sees beneath a ceiling. The fixture restores its temporary Game view resolution entries after capture.

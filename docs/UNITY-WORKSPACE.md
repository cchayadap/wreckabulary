# Editing the current Unity game

Open `wordbound-integration` with Unity **6000.6.4f1**, as recorded in `ProjectSettings/ProjectVersion.txt`. Use **Wreckabulary → Open Current Workspace**. Work in Edit Mode, save the actual scene/prefab/profile, then try the result in Play.

The [lobby editing guide](wordbound/LOBBY-EDITING-GUIDE.md) lists the exact saved Shop/Loadout, card, camera, font and material controls, with a short save-and-test workflow and the remaining generated sections.

The workspace has **Scenes**, **Design** and **Assets** navigation. Native studio/typography creation and repeat setup pass; all 77 generated files remain byte-identical after the repeat. Ordinary human Play/Stop authoring checks remain open. Current native and Windows verification is tracked in [progress](../progress.md). The [developer workspace plan](wordbound/DEVELOPER-WORKSPACE-PLAN.md) tracks the remaining authoring work, including saved shop/loadout UI layouts.

## A short editing loop

1. Use **Scenes** to open the environment, **Design** to select a profile, or **Assets** to locate the existing model, material or font. Assets categories are **Weapons, Furniture, Tools, Characters, Accessories, Materials, Fonts, Animation, Audio, UI**. Use **Find**, then select an entry to ping the actual Project asset; it does not regenerate it.
2. Change scene objects in Scene View, prefab contents in Prefab Mode, or saved profile fields in the Inspector. Duplicate a profile/course for a variant when you want to keep the original. A duplicate affects play only after it is assigned to the relevant launch/course reference.
3. Save the scene, prefab or asset. For a Wordbound course form, use **Apply form edits and save asset**; an unapplied form is a recoverable draft, not the saved course.
4. Close and reopen the edited asset to check persistence. Use **Play Lobby**, **Play Shop** or **Play Loadout** for a one-shot preview of the real Hub menu page. Stop Play and confirm the authoring scene and previous Play target are restored.
5. Review the actual Game view at the intended window sizes. Scene/Inspector previews help editing; a Windows player and online session still need their own verification.

The Hub is the normal Play entry. **Play the opened scene** is the persistent workspace preference for testing your current scene. The three one-shot page previews temporarily use Hub, prompt to save modified scenes, preserve that preference and restore the previous Play-start override after stopping. They do not replace your opened authoring scenes. A compile failure or aborted Play must not leave the override behind; that restoration is part of the pending native checklist.

**Play Lobby / Shop / Loadout use your real local player saves.** Buying or equipping there follows the normal game behavior and can save currency, ownership, outfits or themes. Stopping Play restores the editor's scene/entry setup; it does not undo player progress. These shortcuts are real-game trials, not isolated sample-data previews.

**Copy** on supported model/material entries opens a save dialog. Suggested destinations are `Assets/_Project/Materials/Authored` for materials, `Assets/_Project/Prefabs/Furniture` for furniture, and `Assets/_Project/Prefabs/Equipment/Weapons` or `Tools` for equipment. Choose a new name inside `Assets/_Project`; existing files are not replaced. A prefab copy is an editable variant of its source and retains shared mesh/material references. To change a surface without changing shared original materials, copy that material too and assign the copied material to the prefab's renderer.

Neither a copied prefab nor a copied material is automatically equipped, assigned to another renderer or added to a runtime visual profile. Assign it explicitly after inspection. Original imports and their stable lookup keys stay in place.

### Saved studios and typography

**Create missing developer assets** (also **Wreckabulary → Authoring → Create Missing Developer Assets**) is an explicit setup action. It created 24 categorized prefab variants of enabled original items under `Prefabs/Equipment/Weapons`, `Prefabs/Equipment/Tools` and `Prefabs/Furniture`, and two saved scenes under `Scenes/Authoring`: **EquipmentStudio.unity** and **CharacterStudio.unity**. Existing destination assets are preserved. These outputs are saved in this checkout; routine opening and editing do not require running setup again.

**Open Equipment Studio** and **Open Character Studio** provide saved review layouts with cameras and lighting. Equipment Studio placement is a review arrangement; dragging an instance or its Studio Camera changes that studio only. For a reusable item change, open the copied prefab, edit and save it, then assign it to the intended `Presentation.Overrides` entry and save the assigned profile. Use the render-only validation and check the original bounds/grip before trying it in a Wordbound course. The original lobby item preview uses its own model library and will not automatically show a Wordbound override. Character Studio uses the original shared rig preview; it does not create independent accessory rigs or replace the saved Player prefab.

The same setup calls the typography setup, whose saved outputs are `Resources/UI/LobbyTypography.asset`, saved TMP fonts under `UI/Fonts`, and text material presets under `UI/Materials`. **Lobby fonts & material presets** in Design selects the runtime-connected profile after creation. Its **Display**, **Body** and **Black** fields reference the actual TMP font assets; **Stroke**, **Drop** and the foreground fields reference the actual text materials. Select the assigned object from its Inspector field to edit it, save, then restart Play to check the lobby. The runtime caches these assignments; a live refresh after changing them is not promised.

The font and its material must use matching atlas/shader settings and valid bold-typeface assignments. If the profile is invalid, the source logs a warning and uses generated defaults, so check the Console if a saved choice does not appear. Native creation/persistence and runtime ownership checks pass; authored fonts are preserved during the recorded Play tests. Physical Play/Stop interaction and rendered appearance remain under review. Home, Shop and Loadout panels now use saved prefabs; inner generated controls remain listed in the [lobby editing guide](wordbound/LOBBY-EDITING-GUIDE.md). Keep original source TTF files and their licenses in `Resources/Fonts`.

### Household furniture studio

The Household importer passes all four Editor tests. Permanent setup has created the expected 102 files, and the existing 146 authored files remain unchanged. **Repeat preservation and rendered review are still being verified.**

1. Choose **Workspace → Scenes → Open Household Studio** for `Assets/_Project/Scenes/Authoring/HouseholdStudio.unity`. Its camera, light and eight furniture instances are editable. If setup is needed, use **Design → Workspace Tools → Create missing Household Studio**; the menu equivalent is **Wreckabulary → Authoring → Create Missing Household Studio**. Setup preserves existing assets.
2. Move or rotate a scene instance to change this studio's arrangement, then save the scene. To change the reusable item, use **Design → Household furniture prefabs**, open its prefab in Prefab Mode, edit and save it. Apply scene overrides to the prefab only when you intend to share them.
3. Select a renderer's saved material under `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/<sourceName>/`. Editing that material affects every referencing instance. For a local color/style variation, duplicate the material and assign the copy to the intended prefab or instance. The source model files stay under the collection's `Models/` folder; use the game-owned wrappers below for placement.
4. **Design → Household initial sizing** selects `Assets/_Project/Data/Wordbound/Authoring/HouseholdExpansion.asset`. Its heights are creation defaults for missing prefabs, not controls that overwrite existing ones. Resize an existing saved prefab directly in Prefab Mode. Reopening or repeating setup preserves that edit.

| Area | Exact wrapper prefab path |
| --- | --- |
| Kitchen — stove | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Kitchen/kitchenStove.prefab` |
| Kitchen — fridge | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Kitchen/kitchenFridge.prefab` |
| Kitchen — cabinet | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Kitchen/kitchenCabinet.prefab` |
| Attic — bookcase | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Attic/bookcaseClosedDoors.prefab` |
| Attic — drawer cabinet | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Attic/cabinetBedDrawer.prefab` |
| Rooftop — plant | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Rooftop/pottedPlant.prefab` |
| Rooftop — bench | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/Rooftop/bench.prefab` |
| Packing Yard — parcel | `Assets/_Project/Prefabs/Furniture/HouseholdExpansionV1/PackingYard/cardboardBoxClosed.prefab` |

Household Studio is an art review scene. Seven props still need collision authoring; only the parcel has a simple static box collider. None has tested dynamic physics, hinges, drawer motion or breakage. Complete those checks before interactive map use. Setup does not add these props to gameplay catalogues or replace existing equipment.

## What is saved and editable now

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
| Online Wordbound entry / gameplay shell | `Assets/_Project/Scenes/WordboundLobby.unity` / `WordboundGameplay.unity` |
| Character, imported model, outfit preview | `Assets/_Project/Prefabs/Player.prefab` |
| Wordbound launch bindings | `Assets/_Project/Resources/Wordbound/LaunchAssets.asset` |
| Wordbound course forms / seed / placements | `Assets/_Project/Data/Wordbound/Authoring/FlagshipCourse.asset` / `FreestyleCourse.asset` |
| Wordbound camera | `Assets/_Project/Data/Wordbound/Authoring/PlayerCamera.asset` |
| Wordbound artwork selection / render-only model overrides | `Assets/_Project/Data/Wordbound/Authoring/Presentation.asset` |
| Wordbound fonts, colors and spacing | `Assets/_Project/Data/Wordbound/Authoring/Interface.asset` |
| Lost Address chapter / notice / epilogue copy | `Assets/_Project/Data/Wordbound/Authoring/LostAddressStory.asset` |
| Gameplay HUD / lobby | `Scripts/Game/GameHud.cs`, `GameHudCards.cs` / `Scripts/UI/Lobby` under `Assets/_Project` |
| Separate inventory layout study | `Assets/_Project/Resources/UI/Inventory/Inventory.uxml` and `.uss`; not verified as the runtime HUD source |
| Recipe balance and enabled powers | `Assets/_Project/Data/Config/items.json` |
| GPT item illustration source PNGs | `Assets/_Project/Art/Generated/Items` |
| Runtime item Sprite references | `Assets/_Project/Resources/UI/Generated/PowerItemArt.asset` |
| Room boundaries, door graph, spawn rules, objectives, recipes | `Assets/_Project/Data/Config` |

Opening the Wordbound Gameplay shell does not supply a resolved course, player session or matchmaking state. Use the course Inspector's local playtest for authored course work, and the real Wordbound lobby for online flows.

### Wordbound profiles and courses

Select **PlayerCamera** for field of view, distance, pivot, lift, wall probing, pitch and input sensitivity. Its Inspector exposes angles in degrees. Valid edits update the running Wordbound camera; the starting angle takes effect when the camera is recreated. Save the asset and restart Play to check the complete result. The original game's `CameraRig` and the Wordbound camera profile are separate camera systems.

Select **Interface** for Wordbound menu fonts, colors, sizes and spacing. Its Inspector includes a control sample. The profile is used through `LaunchAssets.UiTheme`; it is not a saved Canvas layout for the original lobby, shop or loadout.

Select **Presentation** for the original Wreckabulary artwork and explicit item visual overrides. An override must be a render-only model with valid materials/transforms, without gameplay scripts, physics, cameras or lights. It changes course/client presentation; it does not grant or equip a per-player online skin. Keep model bounds and grip alignment compatible with the original item.

Select **LostAddressStory** to edit copy for the existing chapters, notices and epilogues. These fields do not add new puzzle conditions or change ending rules. Preserve the existing story identifiers and verify the matching ending in play.

For a course, edit its Inspector form and Scene tools, then choose **Apply form edits and save asset**, **Validate and preview exact seed**, and **Play exact preview locally**. A source/seed change invalidates an older preview. The current local authoring playtest supports **TimeRun and Freestyle**; it does not publish a course or prove multiplayer compatibility. **Tools → Wordbound → Restore authoring scene setup** is the explicit recovery action for that temporary course playtest.

**Tools → Wordbound → Create or verify Inspector authoring assets** creates missing defaults and preserves existing assets and assigned launch references. Use it deliberately if the assets are missing; opening the workspace does not need a regeneration step.

## Houses, lighting and the original character

The house prefabs are real editable geometry and furniture. Play uses the authored scene instance when its map matches the selected map, otherwise the selected map prefab. Round reset restores the initial authored furniture transforms and components. Edit the shared prefab to affect all modes; apply scene overrides deliberately when you want to share them.

The workspace's **Worlds** group opens all five current prefabs directly. Save your prefab changes, return to the Hub, then choose that house in **PLAY** to inspect the actual game. The four original scenes are mode entry points; the five house prefabs supply their environments. There is no need to create a separate scene copy for each house.

Use **Hide Ceilings in Scene View** to reach room interiors while editing. It uses Unity's editor visibility only, preserves objects you already hid, and restores its own temporary visibility changes before a scene or prefab closes, or when the workspace window closes. Saved geometry and gameplay remain unchanged.

Use **Lighting & Materials** to select the shared Sunlit House profile, daylight sky, or current material assets in the Inspector. Edit the saved profile for shared lighting changes; edit a renderer's assigned material for its surface. These navigation buttons do not regenerate geometry or apply a migration. Keep imported source art under `Art`, active world assets under `Resources/Worlds` and `Worlds/Generated`, and historical references under `Editor/Legacy`.

The profile controls sun direction, warm and cool light colors, ambient fill, fog, exposure, contrast, saturation and the practical-light budget. Its settings are applied when the scene enters Play, so save the profile and restart Play to review an edit. Edit the profile's sun direction instead of the runtime Sun transform. Individual practical lights belong to their world prefab; graphics quality controls how many nearby lights are enabled. **Wreckabulary → Authoring → Add Daylight and Practical Lighting** installs missing scene lighting and preserves an existing profile.

JSON still defines logical room boundaries, connectivity, objectives and spawn rules. Keep those in sync when changing room dimensions or adding rooms; moving a decorative prop does not require regenerating JSON. Existing world prefabs are never silently regenerated from JSON.

The saved camera and **Authoring Preview (Editor Only)** show the current character and close third-person lens before Play. This visual preview contains no player controller, disables itself on Play, and is stripped from builds. Edit the Player prefab for actual character changes. Original runtime cameras follow player count and input, so tune `CameraRig` for their gameplay framing.

Boots, glasses, satchel and other wardrobe pieces are skinned meshes inside `Art/Imported/Avatar/Avatar.fbx`, not separate accessory prefabs. `Data/Config/wardrobe.json` maps their mesh and material names. Keep those names and rig/subasset references intact. The `Materials/CharacterDefaults` and `Materials/Library` folders contain different parts of the appearance setup; inspect the renderer's actual assignment before editing a material.

## UI editing: current limit

Home, Shop and Loadout panels have saved prefab/profile assets. Their inner wearable/detail controls and the gameplay HUD still include generated layout. The Canvas hierarchy visible during Play is temporary; moving a card there does not save a reusable menu layout. Edit the saved prefab outside Play instead. The inventory UXML/USS is a separate UI Builder presentation asset, and the source audit did not find a runtime loader for it. Empty legacy TMP seed labels in the HUD supply references used by the current runtime controller.

The Shop has saved `Prefabs/UI/Shop/ShopPage.prefab` and `ShopOfferCard.prefab`, assigned by `Resources/UI/LobbyShopLayout.asset`. Workspace **Design → Edit Shop panel / Edit offer card** opens the actual prefabs. Move and resize their elements in Prefab Mode, save, then use **Scenes → Play Shop**. The layout profile and view Inspectors provide **Check references**. Edit the panel, grid, picture area, title style and action placement; the game supplies item names, prices, pictures and equipped state. Leave the reserved Motion wrapper for button animation and edit its Body/children. Gear's recipe chooser and Extras still use generated layouts. Home and Loadout have their own saved panels/cards and Design shortcuts; use the [lobby editing guide](wordbound/LOBBY-EDITING-GUIDE.md) for exact paths and remaining generated controls.

Native setup `080702` created these assets, and all 12 focused Shop persistence cases pass. All eight font-ownership cases also pass after the static-atlas repair. Real-page Play/Stop preservation, rendered review and Windows acceptance remain pending; the generated Play hierarchy itself is temporary.

The next authoring milestone is a saved authoritative menu prefab/profile that the real runtime instantiates, with persistent camera, layout, font, material and card-template references. Until that is implemented and verified, the page previews provide a quick way to try the current game, not a complete no-code UI editor. See the plan's acceptance checklist before marking this complete.

## Folder map and safe organization

Paths below are relative to `Assets/_Project/` unless shown otherwise. The studio and typography outputs described above are saved in this checkout: two studios, 24 prefab variants, three fonts and five material presets. Native setup/repeat and 36 focused editor cases pass; actual interaction, Play and font-preservation acceptance remain unfinished. The Assets browser groups content by use while retaining its existing physical path.

| Category | Current saved location and boundary |
| --- | --- |
| Scenes | `Scenes/`: the six game scenes listed below |
| Original character / accessories | `Art/Imported/Avatar/Avatar.fbx`; `Prefabs/Player.prefab`; `Data/Config/wardrobe.json` |
| Original weapons, shields, tools, furniture, food | `Art/Imported/Items/` (40 FBX); lookup keys such as `Items/BAT` |
| Original environment / letters / effects | `Art/Imported/Environment/`, `Letters/`, `VFX/`, `Textures/` |
| Imported collection models and icons | `Art/Collections/WordboundPvP/{Models,Icons,Materials}` plus palette maps; `Resources/Collections/WordboundPvP.asset` |
| Seasonal assets | `Art/Seasonal/Winter/{Animations,Finishes,Props}`; `Resources/Collections/Winter.asset`, `WinterDecor.prefab`, `Resources/AudioPacks/Winter.asset` |
| Editable material assets | `Materials/Library/`, `CharacterDefaults/`, `Environment/`, `Generated/`, `Wordbound/`, plus root materials |
| Fonts | `Resources/Fonts/`: Lilita One and Nunito files with OFL licenses; vendor TMP fonts remain under `Assets/TextMesh Pro/` |
| Item icons / input glyphs / themes | `Resources/UI/Items/`, `Glyphs/`, `Themes/`, `Generated/`; shop portraits under `Resources/UI/Shop/Looks/` |
| Wordbound profiles / course data | `Data/Wordbound/Authoring/`; runtime bindings and public client/catalogue files under `Resources/Wordbound/` |
| Network body prefabs | `Prefabs/Wordbound/Network/`: player, item, platform and snapshot chunk; registered references must stay consistent |
| Original model / material registries | `Resources/ModelLibrary.asset`, `MaterialLibrary.asset`, `GameAssets.asset`, `GameData.asset` |
| Shared worlds / lighting | `Resources/Worlds/`, `Resources/Environment/SunlitHouse.asset`, `Materials/Environment/DaylightSky.mat` |
| Saved generated world dependencies | `Worlds/Generated/{Materials,Meshes,Textures}`; these are referenced assets, not disposable cache |
| Historical editor-only content | `Editor/Legacy/{Scenes,Worlds,Dependencies}`; dependencies contain Materials, Meshes and Textures |
| Build profiles | `Assets/Settings/Build Profiles/Win.asset`, `Win-QA.asset`, `Win-Dev.asset` |
| Editable external art sources | Project-root `ArtSource/Collections/`: Blender sources, exports, manifests and comparison renders |

Packing Club source versions are under `ArtSource/Collections/PackingClub/V1/build-20261026-a/` and `build-20261026-b/`, including `Source/PackingClub.blend`. At the folder audit, they had not been imported into `Assets/_Project/Art/Collections/PackingClub/V1`. The optional importer must validate the export receipts before creating that collection and its preview profile. A Blender file or preview render alone is not a runtime asset.

Keep existing `Resources` relative paths, imported model paths, material family names and `.meta` identities stable. `ModelLibraryBuilder` derives `Items/BAT` from the physical FBX path; moving that file into a Weapons folder would change its key. Import rules also depend on the Avatar/Environment subfolders. Category browsing should improve discovery before any physical relocation.

The names **Generated** and **Library** do not mean safe to delete or overwrite. The material library rebuild reapplies specifications to existing materials, so it can replace Inspector edits. Use explicit generation/import commands only with a preservation plan for authored choices; opening, browsing, saving or entering Play must not run them automatically. A future physical migration must retain GUIDs, subasset references and runtime lookup names, and pass save/reopen/player checks.

The [recipe guide](RECIPES.md) covers all 24 usable items. **Power Item Illustrations** selects editable PNGs; **Power Item Sprite Library** selects their runtime references. After deliberately replacing or adding generated illustrations, use **Art → Rebuild Generated Item Library** to import the six power icons and save their references. Other recipe icons continue to use their imported item thumbnails. JSON controls mechanics; artwork never changes damage, timing or letter costs.

## Migrations and verification

**Authoring → Upgrade Current Scenes** creates missing authored assets and preserves edits within the current authoring version. A version migration archives the previous worlds and scene instances, including overrides, before installing current geometry. It also backs up original scenes/prefabs to local `Logs/authoring-backups`. The former prototype rebuild is isolated under **Wreckabulary → Legacy** and cannot overwrite production scenes. Preserved prototype geometry lives under `Assets/_Project/Editor/Legacy`; it is excluded from shipped scene content. Test scenes under `Assets/_Project/Tests` are test fixtures, not game entry points.

`ProjectSettings/EditorBuildSettings.asset` currently lists **Hub, LivingRoom, Tutorial, MovingDay, WordboundLobby and WordboundGameplay**, in that order. Verify the selected Windows build profile as well before a player build. No version-numbered scene copies are used as entry points.

The explicit `EnvironmentCaptureTests` fixture records native runtime frames of all five houses: overview, centered third-person interior, upward architectural detail, the Garden sky, upper floors, and mixed-floor couch play. It writes a JSON frame manifest beside the PNGs, including actual camera state and lighting. These frames complement the camera, lighting and cutaway regression tests; an editor overview alone does not verify what a player sees beneath a ceiling. The fixture restores its temporary Game view resolution entries after capture.

That fixture description is not a claim that the current workspace changes have passed native review. The integrator runs Unity checks through the project gate, records the exact revision/results, and separately verifies the shipped Windows player and multiplayer behavior.

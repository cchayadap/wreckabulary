# Edit the lobby in Unity

Open **Wreckabulary → Open Current Workspace → Design**. Edit outside Play Mode.

Home, Shop, Loadout, lobby-camera and typography assets are saved in this checkout. For a grey asset button, scroll to **Design → Workspace Tools → Create missing developer assets**. Setup preserves existing authored assets; repair any reported invalid reference. Home's five Editor cases and 70 combined Play cases pass, with all 146 authored files preserved. Loadout/camera checks also pass. Physical editor interaction, visual and current Windows checks remain in progress. [Verification](../../BuildNotes/developer-workspace/home-recovery-20261026.json).

| Design button | What to edit | Saved location within the Unity project |
| --- | --- | --- |
| **Edit Home dock & turn hint** | Next-match dock, hint placement, action spacing and static button copy | `Assets/_Project/Prefabs/UI/Home/HomePage.prefab` |
| **Home layout assignments** | Which saved page the Home screen uses | `Assets/_Project/Resources/UI/LobbyHomeLayout.asset` |
| **Edit Shop panel** | Panel size, tabs, scroll area and offer-grid spacing | `Assets/_Project/Prefabs/UI/Shop/ShopPage.prefab` |
| **Edit offer card** | Picture area, title font/size and action placement | `Assets/_Project/Prefabs/UI/Shop/ShopOfferCard.prefab` |
| **Shop layout assignments** | Which saved page and card the Shop uses | `Assets/_Project/Resources/UI/LobbyShopLayout.asset` |
| **Edit Loadout panel & recipe shelf** | Panel, tabs, body slots, recipe grid and Finish placement | `Assets/_Project/Prefabs/UI/Loadout/LoadoutPage.prefab` |
| **Edit recipe card** | Icon rectangle, letter-row slot, letter size/width budget and selection color | `Assets/_Project/Prefabs/UI/Loadout/RecipeTile.prefab` |
| **Loadout layout assignments** | Which saved page and recipe card Loadout uses | `Assets/_Project/Resources/UI/LobbyLoadoutLayout.asset` |
| **Framing, height & field of view** | Lobby camera distance, eye height, aim height and field of view | `Assets/_Project/Resources/UI/LobbyCamera.asset` |
| **Lobby fonts & material presets** | Generated text's Display, Body and Black font roles and matching presets | `Assets/_Project/Resources/UI/LobbyTypography.asset` |
| **Saved fonts** / **Saved text materials** | Saved TMP fonts and their material presets | `Assets/_Project/UI/Fonts/` and `Assets/_Project/UI/Materials/` |

1. Open a panel/card. In Prefab Mode, select an element and use the Rect Tool or Inspector to move/resize it. On grids, edit **Cell Size** and **Spacing**.
2. Select text to edit its font, matching Material Preset and size. Button colors use **Idle Ink** / **Active Ink**; recipe selection uses **Selected Ink**. Edit visual children inside the **Motion (runtime animation)** wrapper.
3. Save, leave Prefab Mode, then choose **Workspace → Scenes → Play Lobby**, **Play Shop** or **Play Loadout**. Check long names, hover/selection and scrolling. Stop, reopen the prefab and confirm the saved edit. Changes to the temporary Play hierarchy are not saved. The preview is designed to restore your previous scene and Play entry.

These previews run the real game and use your player save. Purchases and equipment changes can persist.

For the lobby camera, **Distance** moves nearer/farther, **Eye Height** changes camera height, **Look Height** changes its aim and **Field Of View** changes the lens angle. Save and restart Play; small rooms still limit distance. **Design → Player camera** opens the separate Wordbound gameplay-camera asset.

Saved fonts are `LilitaOne-Regular SDF.asset`, `Nunito-ExtraBold SDF.asset` and `Nunito-Black SDF.asset`. Presets include `Lobby Display Stroke.mat`, `Lobby Display Drop.mat` and `Lobby … Foreground.mat`. Keep presets paired with their font. Swapping a role in `LobbyTypography.asset` does not reassign existing prefab labels: edit those labels too. Shared font/material edits affect every referencing label; duplicate and assign for a local variation. Restart Play afterward.

Keep Finish, scroll/grid and header controls outside **Recipe detail**. Keep card visuals/motion outside **Letters (runtime contents)**. Both slots are refilled during play. Shop and Loadout Inspectors' **Check references** catches broken bindings; it does not replace a visual/navigation check.

For Home, keep **Turn hint** separate from **Next match**. Keep Start, mode, house, Wordbound and its note independently visible inside the dock. The default Vertical Layout Group controls action positions; edit its spacing and child Layout Elements, or deliberately replace it for free placement. Current mode/house labels, countdown visibility, availability and callbacks are runtime-owned. **Wreckabulary → Authoring → Create or Verify Saved Home Layout** validates an existing layout without replacing it.

Still procedural: wearable pickers/swatches/accessories/finishes, recipe-detail contents and letter-tile styling, Shop's Gear recipe picker and Extras, and gameplay HUD. Names, pictures, prices, ownership and actions come from game data. Blank recipe/detail slots in Prefab Mode are expected; a data-filled editor preview remains future work.

## Household props and review layout

The importer passes four Editor tests, and permanent setup has created the expected 102 files. **Repeat preservation and rendered review remain pending.** Use **Workspace → Scenes → Open Household Studio**. If the shortcut is disabled because assets are missing, **Design → Workspace Tools → Create missing Household Studio** creates missing defaults and preserves saved work.

Move an instance or camera and save `Assets/_Project/Scenes/Authoring/HouseholdStudio.unity` to change the review layout. For a reusable prop edit, choose **Design → Household furniture prefabs**, open the prefab and save in Prefab Mode. The eight exact paths, grouped into Kitchen, Attic, Rooftop and Packing Yard, are in the [workspace guide](../UNITY-WORKSPACE.md#household-furniture-studio).

Saved surfaces are under `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/<sourceName>/`. Material edits affect all references; duplicate and assign a material for a local variation. **Household initial sizing** opens `Assets/_Project/Data/Wordbound/Authoring/HouseholdExpansion.asset`: its heights apply when a missing prefab is created. They do not resize existing prefabs or overwrite Inspector edits.

Seven props still need colliders; only the parcel has a static box collider. These are review assets awaiting gameplay/physics acceptance. Moving one into this studio does not place it in the Hub, shop or a playable map.

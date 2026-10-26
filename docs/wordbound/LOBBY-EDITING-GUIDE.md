# Edit the lobby in Unity

Open **Wreckabulary → Open Current Workspace → Design**. Edit outside Play Mode.

Shop, Loadout, lobby-camera and typography assets are saved in this checkout. For a grey asset button, scroll to **Design → Workspace Tools → Create missing developer assets**. Setup preserves existing authored assets; repair any reported invalid reference. Loadout/camera Editor persistence and combined runtime checks pass; visual and Windows checks remain in progress.

| Design button | What to edit | Saved location within the Unity project |
| --- | --- | --- |
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
3. Save, leave Prefab Mode, then choose **Workspace → Scenes → Play Shop** or **Play Loadout**. Check long names, hover/selection and scrolling. Stop, reopen the prefab and confirm the saved edit. Changes to the temporary Play hierarchy are not saved. The preview is designed to restore your previous scene and Play entry.

These previews run the real game and use your player save. Purchases and equipment changes can persist.

For the lobby camera, **Distance** moves nearer/farther, **Eye Height** changes camera height, **Look Height** changes its aim and **Field Of View** changes the lens angle. Save and restart Play; small rooms still limit distance. **Design → Player camera** opens the separate Wordbound gameplay-camera asset.

Saved fonts are `LilitaOne-Regular SDF.asset`, `Nunito-ExtraBold SDF.asset` and `Nunito-Black SDF.asset`. Presets include `Lobby Display Stroke.mat`, `Lobby Display Drop.mat` and `Lobby … Foreground.mat`. Keep presets paired with their font. Swapping a role in `LobbyTypography.asset` does not reassign existing prefab labels: edit those labels too. Shared font/material edits affect every referencing label; duplicate and assign for a local variation. Restart Play afterward.

Keep Finish, scroll/grid and header controls outside **Recipe detail**. Keep card visuals/motion outside **Letters (runtime contents)**. Both slots are refilled during play. Shop and Loadout Inspectors' **Check references** catches broken bindings; it does not replace a visual/navigation check.

Still procedural: Home, wearable pickers/swatches/accessories/finishes, recipe-detail contents and letter-tile styling, Shop's Gear recipe picker and Extras, and gameplay HUD. Names, pictures, prices, ownership and actions come from game data. Blank recipe/detail slots in Prefab Mode are expected; a data-filled editor preview remains future work.

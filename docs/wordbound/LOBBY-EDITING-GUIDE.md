# Edit the lobby in Unity

Open **Wreckabulary → Open Current Workspace → Design**. Edit outside Play Mode.

The 28 October menu redesign is saved in the existing Play, Settings, Shop and Loadout prefabs. Customize uses a wider panel, one clothing/accessory/finish choice per row and an inline colour palette. House choices scroll when space is short; Play stays in the footer. Settings uses a compact panel height for each category. There are no decorative ray backgrounds in these menus.

The original five prefabs and Wordbound interface theme are preserved in `Assets/_Project/UI/DesignArchive/BeforeMinimal20261028/`. These are comparison copies, not the active layouts. The one-time **Authoring → Apply Minimal Menu Redesign** command checks an already-applied design without resetting subsequent edits. Do not delete the archive to force it to run again.

The in-game equipment menu uses `Assets/_Project/Data/Wordbound/Authoring/Interface.asset`: **Menu Width** is 780 by default. Edit its colours, spacing and text sizes in the Inspector. Its recipe list, preview and Craft action are still generated from the current inventory; their placement is not a free-form prefab editor.

## Quick edit, save and try

Open a saved asset from **Design**, edit and save it outside Play, then choose **Scenes → Play Lobby / Play choices / Play Settings / Play Shop / Play Loadout**. Stop Play and reopen the asset to confirm the edit. These previews use your actual player save, including purchases and equipped items.

| Change | Open and save | Try it |
| --- | --- | --- |
| Home buttons and spacing | Design → Edit Home dock & turn hint | Scenes → Play Lobby |
| Play frame and compact size | Design → Edit Play panel / Play layout assignments | Scenes → Play choices |
| Settings frame and confirmation space | Design → Edit Settings panel / Settings layout assignments | Scenes → Play Settings |
| Shop grid and offer picture | Design → Edit Shop panel / Edit offer card | Scenes → Play Shop |
| Customize panel and recipe cards | Design → Edit Loadout panel & recipe shelf / Edit recipe card | Scenes → Play Loadout |
| Lobby framing | Design → Framing, height & field of view | Restart Play Lobby |
| Gameplay camera, colours or held pose | Design → Launcher / assigned profiles; follow its Camera, Visual and Ui Theme references | Play → Local → Wordbound solo → GO |
| Character expressions | Design → Facial reactions & blending | Try the matching action; inspect both sides |

For a local variation, duplicate the profile/material/prefab and assign the copy where it is used. Moving a prop in Equipment Studio changes that saved review scene only; use a **Presentation → Overrides** entry to put a compatible copy into gameplay. Preserve the original imported models and rig. Reopen the saved asset after stopping Play to verify the intended edit survived.

**What is still generated:** Play mode cards and their controls, Settings tabs/option/rebinding rows, top navigation, online party layout and the inside of wearable/recipe pickers. Home, Play, Settings, Shop and Loadout have saved frames; Shop/Loadout also have saved offer/recipe cards. Generated children inside those frames are temporary, so dragging them during Play does not save a layout. `Interface.asset` styles Wordbound menus; it does not replace lobby fonts or lobby prefab colours.

**Map-editor route:** **Play → Create → Wordbound maps → OPEN** opens the assigned course locally without online configuration. **Back to lobby** saves the draft and returns to Create; local seats are retained. Playtests use one player and return to the same editable history. Closing the scene retains the recovery draft but not Undo. **Home & tutorial** keeps the original learning room and home builder. The Online screen still offers Workshop for private online hosting. Use **Scenes → Edit Flagship course** for the saved course Inspector; workshop autosaves do not overwrite that Project asset.

For an existing random zone, edit world-height/X/Z bounds and optionally expand **Rotation & floors**. Changes wait for **Apply**; **Cancel** restores the saved values. Floor permissions retain the owning room and do not create cross-floor spawning. [Range instructions](CONTROLS-AND-EDITING.md#map-randomness) · [Actual Windows views](../../BuildNotes/client-view/WORKSHOP-REVIEW-232801.md).


## Character reactions and held items

**Facial reactions & blending** opens `Assets/_Project/Resources/Art/AvatarExpressions.asset`. Select a state in **Poses** and adjust **Brow Relax** / **Blink** (0–100); use **Blend Seconds** for transition softness. This preserves the original mouthless character. Local and Wordbound characters use the saved default; an assigned component profile can override it. Save outside Play and try the action again. These controls change appearance, not combat rules.

The reviewed defaults use Idle **75 / 0** and Hurt **18 / 60** (Brow Relax / Blink). The native save/reload and repeat checks preserve every other pose, timing and metadata file. The one-time **Art → Apply Reviewed Calm Idle and Hurt** command refuses customized or unsaved Idle/Hurt settings; use the Inspector for your own tuning.

**Gameplay artwork & held pose** opens `Assets/_Project/Data/Wordbound/Authoring/Presentation.asset`. Under **Holding Pose → Items**, BAT and HAMMER align their full authored grip frame. **Rotation Trim** adjusts degrees around the hand without shifting grip contact. Other handling families still require individual review. Save, reopen and playtest; inspect idle, windup, hit and recovery from both sides. Do not edit the original FBX to tune a held pose.

The compact Wordbound HUD uses **Interface fonts, colours & spacing** (`Interface.asset`): **Primary** controls the panel colour, **Primary Text** its lettering and **Body Size** its type size. Full controls are in the player's **Menu → Help**; quick tips can be hidden there. HUD positions remain generated in this correction, so dragging temporary Play-mode panels is not a saved layout edit.

The same Interface asset now has an **Online room** group. **Online Use Lobby Background** follows the room selected on Home. Drag a texture into **Online Background** for a separate backdrop, and adjust **Online Background Shade** to darken it behind the menu. Clear the override to follow Home again. These fields are Inspector editable; this update does not overwrite your saved theme or textures. Online party controls still use generated layout. Their 16-image Unity review includes normal/narrow windows and larger type; ordinary Inspector save/reopen and physical navigation remain separate acceptance work.

## Saved lobby layouts

The lobby refinement keeps the original room and saved layouts. **Customize** opens the existing Loadout editor; **Shop** is visible beside the coin balance. **Local 1/4** counts players on this PC. Play separates **Local**, **Online** (the actual Wordbound lobby), and **Create** (learning and the home-building workshop). The selected mode shows its objective and real human/bot composition once. These labels do not add online support to the original house modes.

Home's saved **LOCAL PLAY** action still starts the selected local mode; its mode and house buttons open the chooser. **ONLINE PLAY** opens Wordbound. Edit these static labels in HomePage.prefab. The one-time **Authoring → Apply Reviewed Home Navigation Labels** command accepts only the complete old or new default label set and preserves custom text rather than overwriting it.

For the puzzle world, use **Play → Local → Wordbound solo**, choose **Time Run** or **Freestyle**, select **Lost Address** or **Parcel Hall**, then **Play**. Lost Address is the original course; Parcel Hall revision 2 adds 18 usable furniture pieces and a renewable HAMMER letter supply to its arrival room. Both use the saved gameplay camera, presentation, story and interface profiles and work without online configuration. **Return to lobby** restores the solo mode and map selection. Switching families keeps house selections and local seats; remove extra local seats explicitly before launching solo. Home's quick Local Play button still launches the selected house game. Compact Solo and Wordbound maps choosers frame the character beside the panel; House and Home restore centred framing.

To edit room appearance, select `Assets/_Project/Resources/Wordbound/RoomArchitecture.asset`. Change a destination's colours, drag materials into its slots, adjust wall-inset dimensions, or toggle **Enabled**, then re-enter Play. Geometry collision and map identity remain controlled by the course data.

To edit a furnished course copy, select `Assets/_Project/Data/Wordbound/Authoring/Variants/ParcelHallArrivalV2.asset`. Use **Apply form edits and save asset → Validate and preview exact seed → Play exact preview locally**. The earlier six-piece copy is preserved as `ParcelHallArrival.asset`. These editable copies are separate from the packaged Solo preset; editing them does not replace public matchmaking content or the packaged map factory. Use the existing course export/private sharing workflow for a custom version. [Map editing details](../../BuildNotes/redesign-20261028/map/INTEGRATION.md).

Settings groups the existing options into **Profile / Audio / Gameplay / Graphics / Controls / Help**. Gameplay includes reduced lobby theme motion and combat feedback. Controls has a **House games / Wordbound** legend selector; the heading stays visible while scrolling to show the current context. Both legends edit the same saved keyboard/mouse bindings; controller mappings stay fixed. Wordbound includes **Equipment 1 / 2** on keyboard **1 / 2** and cycling on **Tab / R3**. Help labels its original instructions **House games**; Wordbound has its own **Menu → Help**. Overflowing lists have a visible scrollbar and keep their tab position. Display confirmation retains its timed rollback across tabs; this delivery captured the pending layout without actually switching resolution. The Settings frame is now a saved prefab; individual settings controls and the responsive top navigation remain generated.

Home, Play, Settings, Shop, Loadout, lobby-camera and typography assets are saved in this checkout. For a grey asset button, scroll to **Design → Workspace Tools → Create missing developer assets**. For only the new frames, use **Wreckabulary → Authoring → Create or Verify Saved Play and Settings Frames**. Setup creates missing defaults and preserves saved edits; repair any reported invalid reference.

Native verification: **27/27 Editor cases** and **31/31 Play cases** pass with none skipped; setup/repeat checks pass and all **4,208 recorded authored files** remain unchanged after Play. The Play checks include frame integration, controls legends, navigation and avatar projection. [Editor evidence](../../BuildNotes/client-view/lobby-authoring-draft/independent-editor-214606.md), [Play results](../../TestResults/playmode_20261026T220430Z.xml), [preservation](../../BuildNotes/client-view/lobby-authoring-draft/native-play-220430-preservation.json). Current Windows review build **20261026T232801Z**, source **ec4b127b**, passes lobby 5/5, Solo 3/3 and workshop 5/5 with all **39 actual images reviewed**. A separate Workshop suite passes **35/35 Editor** checks, including 26 placement cases. [Try the build](../../BuildNotes/client-view/TRY-WORDBOUND.md), [review current images](../../BuildNotes/client-view/windows-232801-lobby-solo-visual-review.md), or [compare earlier lobby layouts](../../BuildNotes/client-view/UI-REVIEW-221541.html). Ordinary physical editor/controller interaction remains separate acceptance work.

| Design button | What to edit | Saved location within the Unity project |
| --- | --- | --- |
| **Edit Home dock & turn hint** | Next-match dock, hint placement, action spacing and static button copy | `Assets/_Project/Prefabs/UI/Home/HomePage.prefab` |
| **Home layout assignments** | Which saved page the Home screen uses | `Assets/_Project/Resources/UI/LobbyHomeLayout.asset` |
| **Edit Play panel** | Panel/title/subtitle style, queue strip, Choices spacing and Summary bounds | `Assets/_Project/Prefabs/UI/Play/PlayPage.prefab` |
| **Play layout assignments** | Saved `Page` and responsive `Compact Maximum` | `Assets/_Project/Resources/UI/LobbyPlayLayout.asset` |
| **Edit Settings panel** | Panel/title, Tabs, content-stack spacing and display confirmation height | `Assets/_Project/Prefabs/UI/Settings/SettingsPage.prefab` |
| **Settings layout assignments** | Which saved `Page` Settings uses | `Assets/_Project/Resources/UI/LobbySettingsLayout.asset` |
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
3. Use **Check references**, save, leave Prefab Mode, then choose **Workspace → Scenes → Play Lobby**, **Play choices**, **Play Settings**, **Play Shop** or **Play Loadout**. Check long names, hover/selection and scrolling. Stop, reopen the prefab and confirm the saved edit. Changes to the temporary Play hierarchy are not saved. The preview is designed to restore your previous scene and Play entry.

These previews run the real game and use your player save. Purchases and equipment changes can persist.

For Play, `Compact Maximum` defaults to **1100 × 600** and accepts **900–1920 × 580–1080** UI units. Compact mode owns the panel's responsive root placement; edit its inner regions and profile size. Keep Queues, Choices and Summary separate, and keep Title/Close outside them. Title and Subtitle need separate text references; the game supplies the subtitle text.

For Settings, adjust Tabs width and the content stack's left inset together. Keep Content flexible. **Display confirmation → Layout Element → Preferred Height** reserves **64–200** units while Keep/Revert is shown; the default is **80**, plus **12** stack spacing. Keep Min Height no greater than Preferred Height and Flexible Height at zero. Keep Title/Close outside Tabs, Content and the hidden confirmation region. The timer and settings behavior stay runtime-controlled.

For the lobby camera, **Distance** moves nearer/farther, **Eye Height** changes camera height, **Look Height** changes its aim and **Field Of View** changes the lens angle. Save and restart Play; small rooms still limit distance. **Design → Player camera** opens the separate Wordbound gameplay-camera asset.

Saved fonts are `LilitaOne-Regular SDF.asset`, `Nunito-ExtraBold SDF.asset` and `Nunito-Black SDF.asset`. Presets include `Lobby Display Stroke.mat`, `Lobby Display Drop.mat` and `Lobby … Foreground.mat`. Keep presets paired with their font. Swapping a role in `LobbyTypography.asset` does not reassign existing prefab labels: edit those labels too. Shared font/material edits affect every referencing label; duplicate and assign for a local variation. Restart Play afterward.

Keep Finish, scroll/grid and header controls outside **Recipe detail**. Keep card visuals/motion outside **Letters (runtime contents)**. Both slots are refilled during play. Play, Settings, Shop and Loadout Inspectors' **Check references** catches broken bindings; it does not replace a visual/navigation check.

For Home, keep **Turn hint** separate from **Next match**. Keep Start, mode, house, Wordbound and its note independently visible inside the dock. The default Vertical Layout Group controls action positions; edit its spacing and child Layout Elements, or deliberately replace it for free placement. Current mode/house labels, countdown visibility, availability and callbacks are runtime-owned. **Wreckabulary → Authoring → Create or Verify Saved Home Layout** validates an existing layout without replacing it.

Still procedural: wearable pickers/swatches/accessories/finishes, recipe-detail contents and letter-tile styling, Shop's Gear recipe picker and Extras, and gameplay HUD. Names, pictures, prices, ownership and actions come from game data. Blank recipe/detail slots in Prefab Mode are expected; a data-filled editor preview remains future work.

## Household props and review layout

The importer passes four Editor tests, and permanent setup has created the expected 102 files. **Repeat preservation and rendered review remain pending.** Use **Workspace → Scenes → Open Household Studio**. If the shortcut is disabled because assets are missing, **Design → Workspace Tools → Create missing Household Studio** creates missing defaults and preserves saved work.

Move an instance or camera and save `Assets/_Project/Scenes/Authoring/HouseholdStudio.unity` to change the review layout. For a reusable prop edit, choose **Design → Household furniture prefabs**, open the prefab and save in Prefab Mode. The eight exact paths, grouped into Kitchen, Attic, Rooftop and Packing Yard, are in the [workspace guide](../UNITY-WORKSPACE.md#household-furniture-studio).

Saved surfaces are under `Assets/_Project/Art/Collections/HouseholdExpansionV1/Materials/<sourceName>/`. Material edits affect all references; duplicate and assign a material for a local variation. **Household initial sizing** opens `Assets/_Project/Data/Wordbound/Authoring/HouseholdExpansion.asset`: its heights apply when a missing prefab is created. They do not resize existing prefabs or overwrite Inspector edits.

Seven props still need colliders; only the parcel has a static box collider. These are review assets awaiting gameplay/physics acceptance. Moving one into this studio does not place it in the Hub, shop or a playable map.

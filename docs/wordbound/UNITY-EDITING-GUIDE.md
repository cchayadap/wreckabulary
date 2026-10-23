# Edit Wordbound in Unity without writing code

Use the isolated `wordbound-integration` Unity project for the assembled game. This guide follows the source at `b4a2cca`; it describes the available controls, not a completed native acceptance test. Final Unity screenshots, Windows interaction checks and owner appearance approval are pending. The [equipment gallery](../../BuildNotes/equipment-polish/ASSET-REVIEW.html) contains the actual Blender previews of all twelve detailed models.

## Start with your own course and profiles

1. In Edit Mode, choose **Tools → Wordbound → Create or verify Inspector authoring assets**. It creates missing defaults and preserves existing assets and launcher assignments. The prepared project must already contain its Wordbound launch assets.
2. In the Project window, open `Assets/_Project/Data/Wordbound/Authoring`. Select `FlagshipCourse.asset` for the full course or `FreestyleCourse.asset` for the smaller example. Duplicate the course into your own folder before experimenting.
3. Duplicate `PlayerCamera.asset` and `Presentation.asset` if this course needs its own look. In the course Inspector, expand **Mode, seed, gameplay assets, geometry and presentation** and assign those copies to **Camera Profile** and **Visual Profile**.

To create another starting course, use **Assets → Create → Wreckabulary → Wordbound → Flagship course copy** or **Freestyle course with defaults**. These commands fill the item/rule assets and geometry catalogue; assign camera/visual profiles afterward. **Editable course** creates an empty asset requiring those references. **Camera profile** and **Visual profile** are also in that Create menu.

The launcher is `Assets/_Project/Resources/Wordbound/LaunchAssets.asset`. Its **Workshop Course** selects the starting document for the in-game workshop; **Camera Profile** and **Visual Profile** are the launcher defaults. Assigning a shared default affects every route that uses it. A course-specific profile takes precedence for that course's local playtest.

## Edit a saved course in the Inspector

Choose **Edit section**, then **Selected** to edit a layer, module, connection, puzzle, supply, spawn, recovery anchor, checkpoint, route, lap, zone or physical site. **Add** and **Remove** manage that list. Coordinate fields marked **(mm)** use millimetres: `1000` means one metre. Scene handles use Unity metres.

For a module, enable **Show Scene handles**, choose its ID under **Scene module**, and click **Frame module**. Drag the position handle in the Scene view to move it across X/Z. The handle keeps its floor height. Use **Move to layer** to change floors and **Rotate module 90°** to turn it. Attached course points move with the module. Selecting the module is done through the Inspector dropdown; this route does not provide the workshop's box selection or arbitrary mesh-surface picking.

Click **Apply form edits and save asset** to commit the form. Pending form edits have local recovery, but they are not the saved course until applied. **Discard unapplied form edits** returns to saved data. If Unity reports that the course changed elsewhere, choose **Restore pending edits into the form** to review your draft, or **Discard pending form and use saved course**. Apply only the version you intend to keep. A recovery-file error needs resolving before applying; the previous saved document remains intact.

Then click **Validate and preview exact seed**. The **Wordbound exact preview** window shows the resolved static geometry and props: left-drag to orbit and scroll to zoom. Dynamic puzzles and players appear in the playtest. After a content or seed edit, regenerate the preview.

Use **Play exact preview locally**, or **Play this exact preview locally** in the preview window. Local authoring play supports **TimeRun** and **Freestyle**, with one player. Unity offers to save modified scenes, enters the isolated playtest, and restores the previous scene setup when it stops. **Tools → Wordbound → Restore authoring scene setup** is the recovery command if restoration needs retrying.

## Use the workshop for direct dragging

Open **Wreckabulary → Open Latest Hub**, enter Play Mode, choose **WORDBOUND**, then **Workshop** on the Wordbound party screen. This is separate from the older home-design Workshop queue in the main lobby.

| Action | Control |
| --- | --- |
| Add a room/module | Choose a **Palette** entry, then click the map |
| Select | Click a module or drag a selection rectangle; Shift adds |
| Move | Drag the selection or its X/Z handles; centre handle moves on the floor |
| Rotate / duplicate / delete | **Rotate**, **Duplicate**, **Delete**, or R / Ctrl-D / Delete |
| Group | **Group** or **Ungroup** in Properties |
| Navigate the view | Right-drag orbit, middle-drag pan, wheel zoom |
| Frame | F for selection; **Frame map** for everything; **Top view** for plan view |
| Undo / redo / cancel | Ctrl-Z / Ctrl-Y or Ctrl-Shift-Z / Escape |
| Exact placement | **Advanced position**, enter X/Z, then **Move** |
| Change grid | **Grid** cycles 0.5 m, 1 m and 0.1 m |

Keyboard shortcuts act when the viewport has focus, leaving text entry alone. A completed drag is one undo step. Escape cancels the pending gesture. Hidden/locked floors cannot be picked for editing. Under **Floors**, choose the active floor, use **Hide floor**/**Show floor** and **Lock**/**Unlock**, or enter **New floor height (m)** and press **+ Floor**. Select modules and use **To this floor** to move them. Adjacent floors are shown as context.

Controller basics: D-pad selects, left stick moves by grid, right stick orbits, triggers zoom, X rotates, Y toggles multiselect, A frames, LB/RB undo/redo and B cancels. Start opens **Controller tools**; D-pad chooses an action and A applies it. Detailed names and numeric fields still use pointer/keyboard input.

## Set randomness and location ranges

Select a room, then use **Placement**:

| Choice | What you edit |
| --- | --- |
| **Fixed** | Choose one object, press **Place object**, then click its position. Select it later to move, rotate, duplicate or delete it. |
| **Zone** | Choose one object, set **Min**, **Max**, **Chance %** and **Spacing (metres)**, then **Draw zone** and drag its bounds. |
| **Pool** | Choose several objects and their relative weights; set the same count/chance/spacing controls and draw a zone. Higher weights make an object more likely. |

For an existing zone, drag its corner handles or edit **World range · metres → X min / X max / Z min / Z max**, then **Apply placement settings**. **Redraw bounds** replaces the drawn rectangle. **New placement** starts another zone. Counts support 0–256, spacing 0–10 m and whole-number weights 1–10,000. Chance accepts 0–100% in 0.1% steps; a positive minimum count requires 100%. Keep bounds inside the selected room and leave enough space for the objects and required paths. Validation reports incompatible bounds or counts.

Enter sixteen hexadecimal digits and press **Set seed**. **Locked** keeps that seed; switch to **Unlocked** to enable **Reroll**. Preview resolves the current draft and seed, so the same valid inputs reproduce the same placement and route choices. Randomness selects among authored options; it does not automatically invent new floors or rebuild the entire room layout.

For route variety, expand **Routes & checkpoints → Routes**, create a route and use **Add weighted branch**. Set **Relative weight**, connection order and checkpoint order, then **Apply branch**. The seed chooses one branch per section and lap. Under **Laps**, set the ordered sections; race courses require three connected laps. **Safe**, **Stops** and **Spawns** provide recovery anchors, checkpoints and player starts. Use the shown capacity/clearance guidance and validation before playing.

To build a chained puzzle, use **Puzzle link → Add puzzle + letters**, then **Link prerequisite** or **Complete puzzle → open gate**. Link the puzzle to a checkpoint under **Stops**. Under **Physical puzzle**, choose the puzzle and **Add physical controls**; drag the marked controls or edit metre values, then **Apply physical controls**. A recipe/link alone does not create all physical race controls.

## Preview, save, reload and share

Choose the supported mode under **Preview / playtest mode** before **Preview**. The **Validation** list identifies errors; selecting an issue focuses its entity where available. Local **Playtest** supports TimeRun/Freestyle. **Host online** prepares the exact validated map for a private party: connect as a guest first and leave any existing party. It does not publish the draft into public matchmaking; public queues use their separately approved catalogue.

Workshop changes autosave locally. **Recover autosave** restores the saved recovery document; **Back to party**/**Close** saves before leaving. **Share → Save file** writes a `.course.json` file and displays its location. **Copy**, **Paste**, **Import** and **Done** provide text sharing. The runtime storage folder is `Application.persistentDataPath/wordbound-workshop`. Workshop recovery includes seed, selection, floors and grid, but not the undo stack.

For a Project course asset, use **Export course JSON** and **Import course JSON** in its Inspector. Course JSON contains the document only: keep the matching geometry catalogue, item/rule references, seed, selected mode and camera/visual profiles separately. Imported IDs must exist in that course's catalogue. Workshop edits do not automatically write back to the source `.asset`; export/share them and import into your intended course asset, then preview again. Keep the seed with any reproduction notes.

## Adjust the gameplay camera

Select your assigned camera profile. Its Inspector groups **View and follow**, **Look controls**, and **Walls and visibility**. Use field of view, follow distance, look-at height, lift and starting/minimum/maximum angles to compose the view. Angle sliders display degrees. Adjust mouse sensitivity and controller speeds separately. Wall probe radius, clearance, closest distance and return speed control obstruction handling.

Valid profile edits update the gameplay camera during play. Invalid settings display an error and leave the last valid settings active. Save the asset to retain your edits, then inspect a clear room, a wall/corner and a change of floor. Profile assets persist independently of temporary scene GameObjects. The workshop orbit camera is a separate navigation view, not this follow camera.

## Edit equipment appearance and grips

The twelve active models, icons, materials and maps live in `Assets/_Project/Art/Collections/WordboundPvP`; their runtime collection is `Assets/_Project/Resources/Collections/WordboundPvP.asset`. Keep personal prefabs/materials in a separate folder. The importer rebuilds generated collection values, so make a prefab variant or separate render-only prefab for your override instead of relying on edits to generated materials.

In your **Visual profile**, add an **Overrides** entry with the canonical **Visual Key** (for example `BAT`, `FAN` or `SHIELD`) and drag your prefab into **Model**. Assign materials on that prefab's renderers. Leave the profile's **Materials** array empty to preserve them: its slot override is applied to every renderer, so slot 0 would replace both paint and brass on the grouped models. Player avatars and letter tiles are not supported override keys.

Keep the model grounded at Y=0, facing +Z, with positive scales and a child named **Grip_R** at its hand contact. The resolver fits artwork to the item's existing gameplay dimensions and places that grip at the hand; visual edits do not change the gameplay collider or item statistics. Preserve FAN rotor and CLOCK hand pivots if editing those models. Prefabs must contain renderers/materials only, without gameplay scripts, physics, lights, cameras or audio sources.

**Defense Pose** exposes shield/plate offsets, rotations and blend time. Offsets are in character-local axes and limited to ±0.25 m; zero preserves hand contact. The default plate X rotation is 90°. Restart/recreate the playtest after changing model assignments or grip transforms; unlike the camera profile, live rebuilding of existing equipment instances is not provided.

The detailed atlas uses color, tangent normal and a URP metallic/smoothness mask; the mask stores metallic in R and smoothness in A. Preserve their importer settings. **Wreckabulary → Art → Build Detailed Wordbound Equipment Collection** imports the verified DetailV2 revision; **Build Wordbound Equipment Collection** selects the original revision. These rebuild the active generated collection. The editable Blender source remains in [DetailV2/Source](../../ArtSource/Collections/WordboundPvP/DetailV2/Source), outside Unity's runtime Assets folder. Component mesh sculpting is done in Blender; Unity provides prefab transforms, material assignment, grips, profiles and placement without code.

Before calling a course finished, preview several seeds, play the exact one you intend to share, inspect equipment from front/side/behind in the game camera, and check return/save/reload. The current [gallery](../../BuildNotes/equipment-polish/ASSET-REVIEW.html) is detailed stylized artwork, not a substitute for final Unity captures or multiplayer acceptance.

# Edit and try the current UI

Use the isolated `wordbound-integration` project with Unity6000.6.4f1. Keep edits in this project; the original checkout and other directions remain separate.

| What to change | Open in Unity | Editable control |
| --- | --- | --- |
| Shop panel and selected action | `Assets/_Project/Prefabs/UI/Shop/ShopPage.prefab` | `LobbyShopPageView`: Selection Height96, Selection Action Width200; normal RectTransforms and category bindings |
| Shop cards | `Assets/_Project/Prefabs/UI/Shop/ShopOfferCard.prefab` | Saved art, name, ownership and action layout |
| Shop assignments | `Assets/_Project/Resources/UI/LobbyShopLayout.asset` | Page and Offer references; preserve those relationships when replacing prefabs |
| Original item pictures | `Assets/_Project/Resources/UI/Items/` | Matching game-model images now take precedence over alternate generated illustrations |
| Parcel Service map | `Assets/_Project/Resources/Wordbound/LocalCourses/ParcelServiceV1.asset` | Saved course plus its item/rule, camera and visual profiles |
| Parcel materials and fixtures | `Assets/_Project/Art/Wordbound/ParcelService/Profiles/` | Course-specific visuals and room materials; also see the prior visual-coherence editing guide |

In-game Workshop: Objects controls placement and randomness; Puzzles controls recipes/objectives and their links; Room controls transforms, grouping, routes and checkpoints. Exact identifiers remain available under Advanced IDs. Tab changes do not apply edits or erase Undo. Select an existing object to reach its settings; use Change objects to reopen image choices. New puzzle offers only craftable recipes.

The compact-Shop setup command is a completed one-time migration. After completion it preserves later valid author edits instead of resetting the footer. Edit the prefab directly for future design changes. The exact original six-file backup and completion receipt are under `shop/compact-correction/native-migration/`.

Known authoring gap: `Data/Wordbound/Authoring/FlagshipCourse.asset` is the Workshop source, but the current default Lost Address Solo path still builds its document from the flagship factory. Editing that saved asset does not yet change default Solo. That binding and richer flagship composition remain open implementation tasks; Parcel Service already uses its assigned saved source.

The in-game Workshop remains its existing scripted IMGUI authoring surface. Its map editing is interactive; the panel layout itself is not a freely rearrangeable Unity Canvas. Do not confuse editable map data and saved game profiles with a completed no-code interface redesign.

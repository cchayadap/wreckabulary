# Workshop icon inventory and bounded recommendation

STATUS: READ-ONLY SOURCE / ASSET INSPECTION COMPLETE — RECOMMENDATION ONLY

Inspected the current Wordbound Workshop implementation, common Wordbound UI styles/theme, LobbyIcons, the ActionIcons atlas pixels, GameHud atlas slicing and original home Workshop source. No Workshop source or assets changed.

## Existing resources

| Resource | Confirmed use / limit |
| --- | --- |
| `Scripts/Workshop/WordboundWorkshopInteraction.cs:28` | Existing `Func<string, Texture> AssetIcon` hook. The two-column object picker invokes it at line512 and supplies the resulting image to `GUIContent`. A source search across all `_Project` C# found only declaration/invocation, no assignment; verify the launcher integration before assuming these thumbnails are active. |
| `Scripts/UI/Lobby/LobbyIcons.cs:9` | Existing simple glyphs: Home, Settings, Power, Trophy, Cart, Coin, Close, Turn, Play, Locker, Badge, Lock, Check, Arrow, CoinW, Chevron, Party, Plus, Glasses, Satchel. Suitable direct matches include Play, Lock, Turn, Plus and Close. No explicit Undo, Redo, Delete, Duplicate, Move, Grid, Frame or Reroll glyphs. Do not label unrelated icons as those actions. |
| `Resources/UI/ActionIcons.png` | Directly opened the actual atlas. Its nine detailed illustrations represent Attack, Dodge, Jump, Craft, Block, Grab, Deploy, Drop and Swap, matching `Input/TouchBinding.cs` and the sprite slicing in `Game/GameHud.cs:1267`. These are gameplay action illustrations, not a coherent map-editor toolbar. They should not be reused as ambiguous delete/save/undo symbols. |
| `Scripts/UI/Wordbound/WordboundUiStyles.cs:127` | Existing GUIContent button path can show images and retains disabled rendering; no new UI framework is necessary. Theme exposes typography, colour and spacing but currently has no editor-icon slots. |
| `Scripts/UI/CreativeWorkshop.cs` | Original House Workshop is a separate uGUI implementation with different callbacks. Its existing Chevron glyph is not evidence that Wordbound Workshop has a complete icon system. Keep the two creator paths distinct. |

## Smallest useful approach

1. Bind the existing object-picker thumbnail hook to the actual catalogue's current item art, including a correct UV/sprite-region treatment. ItemArt can return an atlas plus UV; passing the whole atlas as a Texture would display the wrong image. Borrow matching standalone icons or use a cached correctly sliced icon provider with explicit owned-resource cleanup. Keep a short object name and selected check, with ID/details available on focus or hover.
2. Keep one small fixed tool strip for frequently used Select/Move/Rotate/Undo/Redo/Delete and one clear Playtest action. Use truthful icons with short one-word labels until each symbol is established. Preserve existing named controls, enabled guards, callbacks, text-entry focus exclusion, keyboard/controller access and target sizes. Add only the genuinely missing editor glyphs through editable assets; do not invent their appearance in this note.
3. Move seed, seed lock, reroll, grid and advanced placement settings into one compact settings area rather than leaving both rows permanently over the map. Keep current values and lock state visible in its closed summary. Retain relevant placement details beside the selected object; show longer help only on focus/hover or an explicit Help control. Do not hide validation failures, unsaved/recovery state, destructive choices or local/online playtest distinctions.
4. Preserve the existing separate Preview / Playtest semantics. Preview refreshes generation; Playtest enters the playable map. A shared triangle alone would make these actions hard to distinguish. Likewise retain Share and Close as concise named actions until their icon design is reviewed.

This is an inventory-backed direction, not an implemented result or visual approval. The root's colour and Workshop lanes should choose the final glyph treatment and composition, then test pointer selection, keyboard/controller focus, field typing, disabled commands, recover/autosave, source switching and both creator routes with actual Windows captures. Do not claim all controls can become self-explanatory icons without checking those states.

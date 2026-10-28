# Workshop source peer review

STATUS: COMPLETE WITH ONE BOUNDED UI FINDING. Read-only review; no production/native actions.

Frozen manifest SHA256: `c110fb8c6587487547b9ae2300af84cd6ccb3e8b24a1818d9b90f7836c607fae`. All eight candidate hashes and seven baseline hashes match the manifest. All eight live files matched their candidate hashes at review time. The manifest is the exact file/hash roster.

## Confirmed finding

The new `WordboundWorkshopInspector.PuzzleRecipePicker` enumerates every `Catalogue.Assets.Keys` entry as a recipe image tile. Parcel Service includes original BOX/CHEST/CRATE/DESK/SHELF furniture with `CanCraft=false`, so these become offered recipe choices. `WorkshopCommands.SetPuzzle` correctly rejects them as uncraftable; the result is a new, avoidable selection-and-error dead end, not corrupted data or a validation bypass. Filter choices using `CanCraft` before search/order/Take(6), retaining the original placement catalogue, and cover a mixed catalogue with both craftable and furniture-only assets. Root and the author have been notified.

## Verified source behavior

- Existing room transforms, object placement, puzzle creation/linking, physical controls, routes and validation still invoke the original command/history APIs. No course schema, authority, catalogue, save or networking mutation was added by the UI refactor.
- Tabs are outside the inspector scroll. Changing tabs cancels an unfinished gesture and clears GUI text focus/typing; it does not execute a history command or rewrite pending field buffers. Added tests exercise complete recovery JSON, revision, redo retention and pending placement/range/link/route values.
- Selecting a zone opens Objects, selecting a physical site opens Puzzles, and explicit room selection/controller cycling opens Room. Exact existing IDs remain stored and passed to commands. Friendly captions are presentation only, with typed reference categories preventing room/asset collisions and ordinals disambiguating matching authored references. Advanced IDs and control names retain exact IDs.
- Range min/max and socket X/Y/Z use vertical columns. Site field dictionary keys retain their full original labels, so abbreviated visible X/Y/Z labels do not alias the backing values. Existing apply/cancel/history handlers are retained.
- Range QA preserves the same names, checks and original image captures. It uses actual rendered range/floor anchors, requires positive finite anchors, waits three frames after selection and scrolling, records requested/actual offsets, and restores captured tab/picker state plus existing selection/history/storage invariants. It remains an internal view-only capture, not pointer editing proof.
- Existing controller focus gating, typed-input suspension, selection movement and Undo/Redo handlers are unchanged. The new tab change does not turn an inspector choice into a gameplay or history action. New tests are source-behavior tests, not full device or native presentation acceptance.

## Native/visual limits to keep open

No Unity/native job or image review was performed in this peer review. Verify fixed tabs and columns at 720p and narrow widths, long readable connection captions, New puzzle/Room route states, actual text focus/controller use and range-height/floor captures. The default smoke roster alone does not expose every new inspector tab.

Manual tab changes deliberately retain the selected object/site. Keyboard/controller transforms still operate on that current viewport selection, even when another inspector tab is shown; this is preserved selection semantics but should be checked for understandable visible selection feedback. Clicking a validation issue still focuses its owner without automatically opening the matching contextual tab. Neither point is asserted as a source corruption defect.

## Recipe-filter correction review

Reviewed separate frozen correction manifest `0c2d6a89a554919a65ffc5b0729383dd9930df079a3d0bc228d7536856bdcb88`. Both baseline/candidate hashes verified. Actual picker now calls PuzzleRecipeChoices filtering Catalogue.Assets.Values by CanCraft before search/order/Take6. Mixed-catalogue test uses seven leading noncraftable IDs plus original furniture, verifies six real initial recipes, all craft recipes searchable and accepted by SetPuzzle, noncraftable BOX excluded and draft recovery JSON unchanged. Confirmed finding resolved in source; no remaining blocking source issue in this bounded correction. Native/layout acceptance remains separate.

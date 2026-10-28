# Contextual recipe selection independent review

STATUS: COMPLETE — both contextual guidance findings and controller reveal defect resolved in the exact five-file revision below; no remaining blocking source finding. Read-model and Editor GUI tests do not prove native gameplay completion or physical-device behavior.

Final recheck: the generic-letter shortcut is removed. An active correctly team-bound marked source with no usable held HAMMER suggests HAMMER even if TABLE letters are already available; the regression now expects that. Held usable tool/no active source can suggest the main recipe, and recipe is part of the context key, so a phase change can update on opening without repeatedly replacing a manual choice. The test uses the actual TileSpawn.TileId API. No automatic crafting or runtime rule changes were introduced. The checks below document the review history; findings are resolved in the final hashes.

## Confirmed finding

P2: `WordboundMatchHudRecipes.RecipeSuggestion` always returns `puzzle.RecipeId`. For the full flagship's first `CollectLetters` objective this is TABLE, but `WordboundFlagshipLayout` and the physical evaluator require a HAMMER to destroy the marked TABLE before its TABLE socket can qualify. Suggesting TABLE as the first contextual action can send a new player to the recovery TABLE letters while the required smash step remains unsatisfied. The five new tests instantiate Parcel Service, whose first site is PlaceObject, so they cannot catch this.

Bounded fix: use the site's approved ToolRecipeId while its correctly team-bound marked prop has not been destroyed; then suggest the puzzle's main recipe. Include the phase/recipe in the context key so the same site's transition can update once when reopened. Test the actual flagship, own versus other-team marked prop, and the tool-to-main transition while retaining manual choice within an unchanged phase. Do not craft automatically or alter puzzle/letter rules.

## Other inspected behavior

- Course, mode, manifest and source hashes are checked; migrating/finished/disconnected/returned/eliminated states decline new suggestions. Race uses the current actor's team and its current lap/checkpoint; Freestyle uses the selected practice site then nearest unfinished site. Freestyle is one shared team, matching Completed's team0 query.
- PrepareRecipeMenu runs on menu opening, not ordinary RefreshRecipes; it leaves search and active crafts alone. Same context does not replace a manual recipe choice. An objective change intentionally supplies a new default when reopened.
- SelectRecipe only changes selection; the changes contain no BeginCraft submission. Craft remains an explicit enabled action.
- Keyboard/gamepad Craft opening focuses the chosen visible recipe. Repaint scrolling can reveal the chosen recipe without forcing GUI focus unless it is also the controller-focused row. Native scroll/navigation rendering still needs the root gate; the read-model tests do not prove device behavior.

## Revised five-file review

The new ToolRecipeId branch, correctly team-bound placement lookup, held-tool guard and recipe phase in context address the original missing-tool path. A remaining P2 is the main-letter shortcut: generic TABLE letters do not establish that the required marked TABLE was smashed. The course provides TABLE recovery letters, and letters can also come from other furniture. With no usable held HAMMER and the marked source still active, `FamilyReady(CollectLetters)` continues to require that marked prop's destruction. The revised test expecting TABLE merely because inventory contains TABLE letters encodes an invalid prerequisite assumption. Root has been asked to remove that shortcut and expect HAMMER while the required source remains active and no usable held tool is present.

The controller scroll repair is sound in source: chosen-recipe auto-reveal is now restricted to `!view.UsesGamepad`, so an earlier selected BAT cannot consume reveal before a later controller-focused STOOL. The GUI regression requests reveal explicitly and asserts positive scroll for the two smaller window cases. It retains the existing deferred-selection assertion and explicit action to apply the pending choice. The larger case still checks layout, while exact scroll/device acceptance remains native.

No other blocking source defect was confirmed in the revised five-file diff. No live edits/native actions by this reviewer.

The remaining finding concerns guidance, not an authority bypass: crafting TABLE before HAMMER can be a valid inventory-order choice, but the marked-smash prerequisite remains. If that priority is intentional, its comment/test should demonstrate the subsequent HAMMER suggestion rather than assert the tool is unnecessary.

Final reviewed SHA256 (five files under `Assets/_Project/Scripts/Online/Client/`):

| Path suffix | SHA256 |
| --- | --- |
| WordboundMatchHud.cs | 583d85cec0f5373b545558e26ac74cc70e241515ebd75ec6216a3736652f36c7 |
| WordboundMatchHudMenu.cs | b80d62e09b999ca9aa9af8de44c7932e756cd79b38eb05c2b1338a79f8cfb05a |
| WordboundMatchHudRecipes.cs | 5b0ee5421c168e1894b10e415e943d17d6d088141fcca8eb05e57782d0009538 |
| Tests/Input/WordboundRecipeGridTests.cs | 70b15aad7dfb03846142fe2aa7266f81b2997c4e13dcb980fca2be08ed9fa7f9 |
| Tests/Input/WordboundRecipeGridGuiTests.cs | 7f58400cbace9779de019f8ab364aae1b034e4d21dd157e3115dc06255dd3246 |

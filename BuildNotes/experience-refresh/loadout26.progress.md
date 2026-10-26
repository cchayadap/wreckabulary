# Loadout interaction and layout improvement

STATUS: FROZEN — bounded UI change ready for root-owned validation. Root owns integration, heavy jobs and commits.

## Scope and checkpoint

- Root checkout: wordbound-integration; inspected HEAD c16bbd7ac9c3e576abdfc9e6b35dd8dcb73343f1.
- Found LoadoutPage inside Assets/_Project/Scripts/UI/Lobby/LobbyPages.cs:548, not a standalone LoadoutPage.cs. Sent root the actual path and requested class-region ownership confirmation before editing source.
- Preserve public APIs, local saves/ownership and per-recipe finishes, keyboard/controller navigation, original art, other lobby pages and all root/agent dirt. No catalogue/runtime/service/metadata edits or heavy jobs; no commits from this task.
- Initial root dirt includes preview tooling, story/controller/HUD edits, melee QA edits, generated build-b/portraits and progress. These remain outside this lane.

Next: inspect LoadoutPage, callers and existing tests; send a concrete minimal layout proposal; implement only the agreed class region and focused verification changes if required.

## Implemented and frozen for root

Root confirmed exclusive ownership of only the LoadoutPage class region in Lobby/LobbyPages.cs, this report and focused existing tests. The concrete layout proposal was sent and approved before implementation. Source freeze sent to root so its combined R0/portrait run can begin.

- Kept the 600px one-page locker and live avatar. Removed the repeated subtitle; all existing outfit, colour, accessory, recipe and finish controls remain available.
- Gear heading explicitly says “Gear · apply to all”, with a compact “By recipe” action. Mixed/all-recipe status remains intact; opening either page does not alter the chosen per-recipe finishes.
- Recipe detail now has a stable footer action showing its current admitted finish. It opens the existing ShopPage.SelectRecipe flow for that displayed item. The button sits outside the detail subtree rebuilt by hover/focus, preserving keyboard/controller selection. Navigation alone does not equip, purchase or save.
- Replaced repeated hardcoded keyboard instructions with the input-neutral “Spell this word in a match”. Original recipe art, item descriptions, names and public APIs are retained.
- Closed restores Menu.Outfit after a temporary colour preview. This also applies on page refresh; preview callbacks cannot keep an unselected colour on the lobby avatar.

## Scope and verification

Changed files:
- `Assets/_Project/Scripts/UI/Lobby/LobbyPages.cs` — LoadoutPage region only (36 additions/15 removals at freeze).
- `Assets/_Project/Tests/ShopCollectionsTests.cs` — one focused integration test (29 lines).
- This report.

Read-back comparison against HEAD confirmed both the complete prefix before LoadoutPage and suffix beginning at CareerScreen are unchanged. `git diff --check` passed for the edited source/test. No shared helper, other page, catalogue, model/art, metadata, authority or entitlement code was changed. No test/native/heavy job was run and no commit was made, as root requested.

Added `LockerRecipeFinishOpensTheSelectedGearWithoutChangingTheOutfit`: enter Recipes from the locker, pin SHIELD, move controller/keyboard focus to the finish button, ensure that button survives detail refresh, open the Gear shop on SHIELD, and verify unchanged outfit text, persisted outfit and coin balance plus focus on Choose recipe. This is NOT-RUN here. Existing locker all-in-one, wardrobe/focus, per-recipe ownership and explicit apply-all tests remain unchanged.

Root validation: run the focused ShopCollectionsTests and existing LobbyTests/recipe captures with the combined gate; inspect the compact heading and footer at actual lobby resolutions; verify hover colour -> close/tab/Shop transition restores the saved look. Source checks cannot approve rendered layout or actual controller navigation.

STATUS: FROZEN — implementation ready for root-owned validation/integration. No further source writes from this lane unless root reports a failure. All pre-existing root and online-worktree dirt remains excluded.

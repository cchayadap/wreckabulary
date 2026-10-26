# Independent settings review

STATUS: SOURCE REVIEW COMPLETE; two test-integration corrections identified. Native tests/render not run by reviewer.

Reviewed patch `f06e26745488bacce3b49aecc47c636e7818630d9c402aedc8c68841a71c2ae9`, runtime candidate `2a8d49a1022d5340c0a55fa042b4aa49952853ee8564b75185c10ed26f6ee27e`, four-case fixture `39e92c05adcdae6c04e01e08cb174dbd10d725910d447adf06f87a9a3c5f2784`. Strict whitespace apply check passed before root integration.

## Required bounded corrections

1. `LobbySettingsNavigationTests.IntKeys` incorrectly includes `LobbyMenu.TurnHintKey`. `LobbyMenu.Turning` writes this key with `PlayerPrefs.SetString(..., "seen")` (current line 390). Move it to StringKeys so a prior user's string preference is not restored as an integer. Root and author informed. The reviewer's first pass incorrectly accepted this type; source recheck confirmed the correction.
2. Existing `LobbyTests.ReduceThemeMotionUpdatesTheStageAndSavedPreference` opens Settings and immediately clicks the motion toggle. The new default is Profile. Navigate to `Settings tab gameplay` first; retain the original saved-preference/stage assertions. Root owns this shared-file adjustment. Root already adapted the six-tab loop in `SettingsHasTabsAndGraphicsThatReallyChange`.

## Runtime review

No source blocker found for the bounded grouping change:

- `ShowTab` validates/normalizes aliases before changing state. `Closed` stores scroll against `builtTab`, so changing the destination tab does not overwrite its old position with the previous tab's scroll.
- Content is rebuilt, laid out, then its previous normalized scroll is restored. Setting callbacks reselect the exact named control. `LobbyScrollFollow` subsequently reveals a control if it lies outside the viewport; it does not blindly reset a tab to top.
- `ControlsPanel.Close` cancels rebinding and deactivates its GameObject; `OnDisable` releases the gameplay input lease. The new page drops the old reference only after `Closed`, avoiding a lost lease on tab changes.
- The confirmation gutter applies to every tab, including Controls and Help. The existing timer remains on LobbyMenu with the same `Ends`/`Done`; switching tabs changes only its label target. Keep stops it; Revert restores the original resolution and refreshes only an active page. The patch does not extend the countdown or reset it on tab switches.
- Audio reset remains scoped to mute, sound pack and volume. Graphics options store their keys as strings; mouse sensitivity and inversion keep their actual float/int types. The TurnHint fixture issue above is the exception.

## Evidence limits

The four new tests use actual UI callbacks and rectangles, but the pending-display fixture injects only presentation state and verifies Keep. It deliberately never changes Screen resolution. Actual fullscreen/windowed changes, timeout rollback while another tab is open, physical keyboard/gamepad navigation, short-window layout and screenshots remain native verification work. Existing ControlsSettingsTests still supply rebinding/cancel/conflict coverage. No whole-game, saved-Settings-prefab or online-settings acceptance follows from this review.

## Revision 2 recheck

Patch `59f9de3527b3d00963cce33fba480508d0053e21e8935d1ba7f7ca5e6199f175` / fixture `9346f33305824495a65d312f1320220f86e494e488806a51961b104709a402b2` moves TurnHintKey into StringKeys and removes it from IntKeys. Confirmed against the actual candidate. The other runtime change is only the visible Profile action label, **Customize**, retaining its `Open loadout` control name and callback. No remaining source blocker found in these deltas. Root owns the existing motion-tab test adjustment and native verification.

# Original-style Wordbound interface implementation

STATUS: SOURCE CHECKPOINT — independent review and native execution pending.

This implements the bounded proposal in `ORIGINAL-UI-THEME-PROPOSAL.md`. The original GameHud/LobbyKit, ICY work, models, cameras, course data, controls, commands and preferences are unchanged. Pre-edit normalized bytes for the existing files matched integration; there is no baseline-only commit to exclude.

## Changes

- `UI/Wordbound/WordboundUiTheme.cs` exposes the original cream/teal/orange palette, original Nunito Font references, sizes, row heights, padding and corner radius as a ScriptableObject. Invalid ranges, translucent or nonfinite colours fail validation. A bad runtime assignment falls back to owned readable defaults without modifying the assigned asset.
- `WordboundUiStyles.cs` owns one cloned skin and eight 32×32 state textures per active owner. It rebuilds on a changed theme at Layout, retains that exact configuration for Repaint, and releases owned skin/textures on replacement, construction failure or disposal. Borrowed fonts and source skins are retained. Each scope restores skin, matrix, enabled and all three global GUI colours in `Dispose`. Disabled buttons retain the actual disabled `GUILayout.Button` control and receive a noninteractive opaque label overdraw on Repaint; pad eligibility and command handling remain in the existing caller.
- The launch asset's optional `UiTheme` is forwarded through real online/local/Inspector playtest routes and the workshop. `Tools/Wordbound/Create or verify Inspector authoring assets` creates `Assets/_Project/Data/Wordbound/Authoring/Interface.asset` through Unity and assigns it only if the launch slot is empty. Existing custom theme assignments are preserved. The new Inspector previews normal/selected/disabled/multiline controls with the same owned renderer. No asset or metadata has been hand-written.
- Equipment/journal/crafting use an opaque card, primary Resume, flat readable recipes and selected accent. Passive gameplay HUD panels are omitted while the menu is open. Connection/waiting/result text is retained inside the card and frozen at Layout. Deferred menu actions, IDs, focus registration, search buffer, scroll and escape behavior remain unchanged.
- Workshop chrome and modal branches use the same skin. Multiline modules have a separate minimum height. Theme changes invalidate dependent overlay/recovery style caches. Header/footer height and map viewport account for editable row/padding metrics. The displayed map toolbar, its label reservation, and both ordinary/site pointer exclusions use the same local rectangle; no hard-coded 448×106 exclusion remains. Drag, orbit, zoom, selection, content and recovery code are unchanged.

## Source checks and tests to run

`git diff --check` passes. No compiler, Unity, test runner, player or rendering job was run in this lane.

Seven new cases in `WordboundUiThemeTests` cover invalid configuration, exceptional GUI-state restoration and borrowed-skin identity, Layout-only rebuilding and native texture disposal, invalid-theme fallback, two viewport/custom-row toolbar configurations, and real font measurement of the two-line module plus bounded menu geometry. These use actual EditorWindow Layout/Repaint events, not calls to GUI APIs outside OnGUI. The existing direct-menu test explicitly releases its manually driven HUD styles because that EditMode fixture does not exercise the production PlayMode destruction lifecycle.

Root should run `WordboundUiThemeTests` plus existing `WordboundMenuGuiTests`, `WordboundInputBindingTests`, `WordboundPartyMenuInputTests`, `WordboundWorkshopInputSafetyTests`, `WordboundWorkshopRecoveryTests`, `WordboundWorkshopSourceBindingTests` and workshop overlay/authoring regressions. Test names and filters must be reconciled with the actual native inventory. Required asset creation follows compilation through the existing authoring setup menu/gate entry point.

## Acceptance still open

Source/tests do not establish rendered readability or owner approval. Repeat the actual 220124 Windows menu/workshop route at 1280×720, including disabled recipes, gamepad focus, search, two-line modules, toolbar-edge pointer movement, recovery and online-preparation panels. Check smaller windows and edited font/row sizes; verify Inspector save/reload and assignment survives setup rerun. Confirm the card does not cover critical state or leave interactive controls underneath it. Native destruction and custom-font measurement remain unexecuted. There is no FPS, completed multiplayer match, final art quality or production-ready claim here.

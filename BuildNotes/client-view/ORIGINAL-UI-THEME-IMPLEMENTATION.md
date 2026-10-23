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

The two toolbar cases establish rectangle consistency after resize/custom row metrics; they do not draw the actual Footer rows or prove text/content fits under wrapping or custom fonts. Only the module-font case performs actual text measurement. Toolbar content fit remains an explicit visible/native acceptance item. Independent review identified this distinction; no rendered toolbar-fit claim is made.

Root should run `WordboundUiThemeTests` plus existing `WordboundMenuGuiTests`, `WordboundInputBindingTests`, `WordboundPartyMenuInputTests`, `WordboundWorkshopInputSafetyTests`, `WordboundWorkshopRecoveryTests`, `WordboundWorkshopSourceBindingTests` and workshop overlay/authoring regressions. Test names and filters must be reconciled with the actual native inventory. Required asset creation follows compilation through the existing authoring setup menu/gate entry point.

## Acceptance still open

Source/tests do not establish rendered readability or owner approval. Repeat the actual 220124 Windows menu/workshop route at 1280×720, including disabled recipes, gamepad focus, search, two-line modules, toolbar-edge pointer movement, recovery and online-preparation panels. Check smaller windows and edited font/row sizes; verify Inspector save/reload and assignment survives setup rerun. Confirm the card does not cover critical state or leave interactive controls underneath it. Native destruction and custom-font measurement remain unexecuted. There is no FPS, completed multiplayer match, final art quality or production-ready claim here.

The disabled overdraw uses `GUIStyle.Draw` directly during Repaint, after the actual disabled `GUILayout.Button`. This is explicitly draw-only and introduces neither a second GUI control nor a layout element. The installed 6000.6.3f1 IMGUIModule XML confirms this public overload; compilation/native execution remains pending.

Edited row height also applies to ordinary equipment actions, Clear search, menu search, and the workshop title/seed inputs. Module rows retain their separate multiline height. This is a size-only closure; control order and callbacks are unchanged.

## Theme persistence follow-up — source only

Two native cases were added to `Wreckabulary.Tests.WordboundAuthoringPersistenceTests` (now ten cases). `SerializedThemeColoursMetricsAndOriginalFontReferencesSurviveSaveAndReload` uses `SerializedObject` edits for nondefault colours, all spacing/type metrics and deliberately swapped original Font references, saves the asset, makes unsaved conflicting edits, unloads it and asserts the saved values/GUID/fonts after reloading.

`AuthoringSetupPreservesReloadedCustomThemeAndExistingDefaultThenFillsOnlyAnEmptySlot` runs the complete authoring setup against a temporary launch asset and destination. It checks generated original Font paths and profile/course destinations, retains a custom assigned theme through save/unload/reload and repeated setup, retains edited default-theme values/GUID, then proves an explicitly empty slot is filled and saved. It also compares production launch/default-theme file bytes without writing those files. Both cases clean their unique `Assets/__WordboundAuthoring_<guid>` folder through the existing AssetDatabase cleanup in `finally`; borrowed original fonts/configuration are never deleted.

To exercise the real flow without swapping user assets, the existing public `CreateDefaults()` delegates to private `CreateDefaultsAt(launch, destination)`. The public menu/gate entry name, default paths, full validations and existing-only assignment rules are unchanged. The uniquely named private helper avoids an overloaded gate entry. Its only callers are the menu's fixed destination and the native fixture's temporary destination; no runtime API is added. Tests explicitly reference the existing `Wreckabulary` and `Wreckabulary.Online.Client` assemblies for theme and launch types.

`git diff --check` passes; compiler/native execution is NOT RUN in this lane. The combined previously inventoried 108-case filter becomes 110 cases after these two additions; the optional real authored-launch gameplay case remains separate. These persistence assertions do not prove Inspector button rendering, custom-font row fit, Windows readability or owner approval.

Independent input-lane review gave bounded SOURCE PASS on `84003ee6`: unchanged public setup defaults, actual shared setup flow, temporary path ownership/cleanup, serialized save/reload/GUID/font references, preserved custom assignment and positive empty-slot control, plus explicit assembly references. No source blocker was found; compiler/native persistence acceptance remains pending. The earlier root default-creation run precedes this refactor and cannot stand in for its tests.

## Native 224855 failures and bounded repair

The actual integration XML `TestResults/editmode_20261023T224855Z.xml` recorded 108/111 passed and three failures. The seven theme cases were 5/7: `MultilineModuleAndMenuUseMeasuredAvailableSpace` measured the actual Nunito two-line button at **50.90625 px**, exceeding the requested 50 px; `ThemeChangesWaitForLayoutAndReleaseOnlyOwnedSkinAndTextures` expected generation 1 after `SendEvent(Repaint)` but observed 2. The third failure is an authoring draft IOException owned by the input lane, unrelated to the theme's persistence additions. The raw failed run remains evidence.

The font repair changes the new profile's requested Module Height to 56 px and derives effective single-row/two-line minima from the actual button/text-field styles at rebuild time, rounded up to whole pixels. An existing 50 px asset is preserved and receives a sufficient measured runtime minimum. The original strict measured-height assertion remains, now exercised against both an existing 50 px request and the new default. This is a two-line minimum, not a guarantee that every long label/custom font fits every narrow panel without additional wrapping.

The cache test no longer treats `SendEvent(Repaint)` as proof that Unity delivered no Layout first. It edits the theme *inside an observed Repaint callback*, asserts unchanged generation/skin and live old textures in that same event, then asserts exactly one rebuild and old-resource release on the first observed subsequent Layout. A bounded actual event/generation trace is retained in test output. This preserves the Layout-only requirement and tests the real event boundary; the earlier XML did not itself record enough events to prove the synthesized-Layout explanation.

These source repairs have not been compiled or run in this lane. The theme fixture remains seven cases; no timeout, assertion tolerance or test skip was added. Generated default asset values are not overwritten.

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

Independent input-lane review gave bounded SOURCE PASS for `0872ec66`: actual styled-font/padding measurement, preservation of existing asset values, Layout-only cache behavior, strict old/new resource checks inside observed events, and both legacy/new default font-fit assertions. Native rerun and actual Windows content fit remain pending.

## Native 225846 outcome — independently read

Integration `TestResults/editmode_20261023T225846Z.xml`, source `73a3326cfbe8fb7d09c67fac355bd5dc0c3d64e9`, records **17/18 passed, 1 failed, 0 skipped**. All **seven theme tests passed**, including both original failed cases and strict font measurement for existing/new profile sizes. The actual cache trace is `Layout:0->1, Layout:1->1, repaint:1->1, Layout:1->2`: the edited Repaint retained its skin and the next actual Layout rebuilt it.

Both new theme persistence cases passed: serialized colours/metrics/original font references survived save/unload/reload, and complete isolated setup preserved a custom assignment and existing default edits before filling/saving an empty slot. The original repeated-draft case also passed. The run is still a failed overall gate because `IdenticalDraftBytesAvoidReplacementButChangedLockedContentRemainsRecoverable` threw `IOException: Unable to remove the file to be replaced`; the input lane owns that separate investigation. No whole-suite success is claimed.

The XML SHA256 is `DB5B49BA46801DD9D7864A85A91CFE6D6A50B68A23AD70EB41E25863E1DCA229`; root summary `BuildNotes/client-view/native-ui-persistence-20261023.json` SHA256 is `ACD975EA78A7ADC060719EC3032B2FBF8D76A9FDE08709842CD5B6114AC7D47F`. These native results close the bounded theme geometry/cache/persistence checks, not actual Footer wrapping, Windows visuals, controller interaction in the player, owner approval or performance acceptance.

## Windows build 230031 compile failure

Root reported build 230031 failed on source `02a3a2ea` with CS1061 at `WordboundUiStyles.cs:113`: `GUIStyleState.scaledBackgrounds` is unavailable to player code. No new player was produced. Independently read the installed Unity 6000.6.3f1 `UnityEngine.IMGUIModule.xml`: its property documentation explicitly says this is Editor-only, not copied to player data and inaccessible from player code.

The bounded repair wraps only the clearing of `scaledBackgrounds` in `#if UNITY_EDITOR`. Runtime `background`/`textColor` assignments, textures, cache lifetime and measured geometry are unchanged. Previous Editor tests could not establish player API availability; a fresh Windows build is required. No job was run in this lane.

Input independently gave bounded SOURCE PASS for `12d305a7`, including the installed Editor-only API documentation. Root then produced Windows QA build 230521 successfully (reported 119.5 s, source `1ab60670`).

## Actual Windows 230748 review and small follow-up

Directly inspected all six actual 1280×720 PNGs under `docs/qa/runs/20261023T230748Z-visible-wordbound_workshop_flow`, plus its checks and player log. All five functional steps pass; console counters are zero errors/exceptions/warnings. Readiness is 4.042 s; 90 ordinary update waits finish at 16.807 s and exact playtest returns at 17.240 s. The separate frame audit records the timing limits; this is neither 60 FPS nor full multiplayer acceptance.

Against the directly inspected 220124 baseline, equipment now has an opaque cream card, strong dark labels and readable unavailable recipes. Underlying top/bottom HUD panels no longer overlap the menu. Workshop palette/properties/toolbars use the same original Nunito/palette; the actual two-line FlagshipRoom / 24 × 24 m item fits. Cyan overlays remain contained and the room labels remain separated in this captured view. The original red avatar, black bag/boots, cream cuffs, wood planks, checkpoint and world framing remain consistent. Large-avatar/empty-room composition, other camera angles, actual controller/disabled/fault/modal states, smaller windows/custom fonts and final owner approval are still open.

Two concrete defects remain in these images: Search wraps as `Searc` / `h` beside its field, and `Preview ready · exact draft and seed` appears dark on the dark area below the footer. The workshop also displays internal spelling `TimeRun`; the party first screen still uses the previous black/gray gradient skin. The party labels are ordinary readable English (`Guest name`, `Connect`, `Workshop`, `Back`) but visually inconsistent; no party-theme appearance is claimed from this run.

The bounded first follow-up measures Search using a cached no-wrap themed label style and the actual font, reserves a measured status row inside the existing opaque footer, and derives the viewport bottom from that same footer rectangle. Tooltip/status share one row; controls and callbacks keep their order and deferred-input behavior. Workshop mode labels display `Time Run` without changing mode IDs, content or signatures. Two focused native cases use real Layout/Repaint to check Search beside the field at body sizes 15/22, plus footer/status bounds and text height at normal/custom metrics. They do not establish actual full-footer fit or Windows appearance. Source review/native execution for this new slice are pending.

Exact artifact SHA256: checks `2B0A544CA625BE26767739129D5C6CCAC4F116E76D3385E237299BB8C224972C`; player log `CFAD9BBAB832B5740E38F52BA7409D818047372F7FF8854B1B2754AFDE1E8D86`. The images belong to the preceding build, not the unexecuted corrections.

Input independently gave bounded SOURCE PASS for the stable Search/status slice: shared measured no-wrap style, unchanged control/action order, status/tooltip inside the opaque footer, exact viewport exclusion and display-only mode names. The two new cases establish native geometry/layout intent; compile/native and fresh Windows contrast/content-fit acceptance remain pending.

## Wordbound party theme follow-up — source only

The existing Wordbound launcher assigns the same `LaunchAssets.UiTheme` to its party menu before initialization. The party's real DrawPartyGui owns the same style scope and measured menu rectangle, applies original heading/body fonts and uses the existing scrolling, field and control order. Connect/Quick Play use the primary fill; focused/current toggle controls use the selected style. Actual `GUILayout.Toggle` remains a toggle, with explicit on/off prefix; ordinary buttons still use the same deferred action handlers, control names, context guard, neutral gate, retry/back/return/fault paths and network operations. Disabled controls use the existing draw-only label treatment after their actual disabled control, without adding interactions.

Owned styles are disposed in the party's OnDestroy finally; assigned theme/fonts remain borrowed. A new case in `WordboundPartyMenuInputTests` draws the actual fake-transport party with a custom theme, checks restored borrowed skin/no dispatch and explicit owned resource cleanup. Existing six actual-control/input cases now also pass through themed DrawPartyGui; fixture teardown explicitly releases styles in EditMode. Native/player execution is pending. Original Lobby/GameHud/ICY assets, scene layout, content, profiles and networking configuration are unchanged. Party styling remains a separate follow-up from the Search/status correction.

Input independently gave bounded SOURCE PASS on the stable party slice: assigned-asset forwarding, owned-scope restoration/cleanup, unchanged real control types/names/order/deferred actions, and draw-only disabled treatment. The seven party input cases, nine theme cases and adjacent menu/workshop regressions must still run natively, followed by actual 1280×720 party/equipment/workshop captures. No source-derived focus/contrast or visual approval is claimed.

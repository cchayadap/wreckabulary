# Saved Home layout draft

STATUS: SOURCE DRAFT ONLY. No live Assets, metadata, setup, Unity test, build or render was changed/run by this lane. Root owns integration after the current Loadout/camera slice. Native verification is NOT-RUN.

## Scope

Home's turn hint and compact next-match dock become a saved prefab selected by a saved profile. The dock keeps Start, mode, optional house and optional Wordbound entry. The adapter calls the existing `LobbyMenu.Go`, `OpenPlay` and `OpenWordbound` methods and returns the same initial focus. The top bar, party panel, feed, avatar preview, game selection screens, countdown/cancel controller, network rules and wardrobe internals are unchanged.

There is one default geometry factory: `LobbyHomeDefaultLayout.Create`. Missing-only Editor setup persists its output, including generated graphic dependencies through the existing reviewed Shop persistence helper. When no Home profile exists, the runtime compatibility path uses that same factory. A saved profile bypasses construction and instantiates its assigned prefab with `LobbyRuntimeTypography` before TMP activation. A present malformed profile fails with a clear validation error; it does not silently replace personal work with defaults.

The only shared Editor change is `LobbyShopLayoutSetup.SaveTemplate` becoming `internal`; its implementation is unchanged. Home setup validates its paths and existing assets before calling that helper. There is no new texture readback or image identity implementation and no duplicate authored/runtime Home layout builder. `LobbyPages.cs` changes only `HomePage.Build`; the captured baseline already includes the integrated Loadout adapter.

## Saved authoring outputs after explicit setup

Run **Wreckabulary → Authoring → Create or Verify Saved Home Layout** after the source compiles and saved lobby typography exists:

- `Assets/_Project/Resources/UI/LobbyHomeLayout.asset`
- `Assets/_Project/Prefabs/UI/Home/HomePage.prefab`
- Missing generated sprite/texture assets under `Assets/_Project/UI/Home/Graphics/`

Setup preserves and validates existing profiles/prefabs and interrupted-run outputs. The profile is published last. Existing conflicting files, invalid bindings and orphan metadata stop setup without replacement. Unity generates metadata. Root can later wire this setup and profile/prefab into the workspace's missing-assets button and Design tab; those shared UI edits are intentionally outside this draft.

Open `HomePage.prefab` in Prefab Mode. Drag the **Next match** dock or **Turn hint**, resize the dock and edit its padding/spacing. Default dock children use a Vertical Layout Group: reorder children or change their Layout Element sizes. For free placement, deliberately replace that group's layout behavior in the authored prefab before dragging its children. Runtime binding does not reapply default anchors, sizes, positions, fonts or material presets. Keep button motion inside its separate **Motion (runtime animation)** wrapper.

| Authoritative saved choice | Runtime-owned state |
| --- | --- |
| Dock/hint placement, panel design, action layout, fonts/material presets, static Start/Wordbound/hint/note copy | Hint visibility/dismissal, countdown visibility and enabled actions |
| Action Idle Ink/Active Ink and typography | Current mode/house text, hover/focus colors, stable object names and callbacks |
| Home profile's prefab assignment | Game/queue/map selection, party membership, saved outfit and launch behavior |

Keep Hint separate from Dock; keep each action and the Wordbound note independent inside Dock. This prevents hiding an optional action or dismissing the hint from hiding required controls. Names `Turn hint`, `Next match`, `GO`, `Choose mode`, `Change house` and `Wordbound` remain controller/focus contracts and are set during binding. Turning still lets the existing controller dismiss the runtime hint and save its `seen` flag. Do not save Play-mode instances as replacement authoring assets.

Save, leave Prefab Mode and use **Workspace → Scenes → Play Lobby**. Check the dock, mode/house navigation, party join/leave focus, turn-hint dismissal and countdown cancellation. The preview uses the player's real save. Stop before editing again. Saved Home labels retain their explicit font references; changing the typography role profile alone does not reassign them. A missing-profile fallback uses the current role defaults. No visual approval or complete no-code conversion is claimed.

## Prepared validation

Five EditMode cases are source-prepared in the existing Editor test assembly:

1. Save/reimport authored dock/hint/action geometry and font/material preset; bind and repeat setup; preserve prefab/profile/material bytes, persistent GUID/local file IDs and visual fields.
2. Rebind callbacks once, then block Start, hide optional controls and enter countdown state; hidden/unavailable actions have no retained callbacks even when invoked directly.
3. Two malformed visibility hierarchies (hint owns dock; optional house owns Start) reject setup while preserving their saved bytes for repair.
4. Creation preserves typography/dependency bytes, open scene/roots/dirty state and player preferences; no temporary scene remains.

Two PlayMode cases require explicit generated Home assets. They verify saved geometry/typography, actual mode/house picker routes, focus through party join/leave, blocked queue/Tutorial visibility, real turn-hint dismissal and countdown/cancel. Countdown delay is temporarily long, canceled before yielding, and restored in `finally`/teardown. Wordbound availability is checked without invoking a scene/network transition. Player preferences and scene/session state are restored by the fixture.

These direct callback and drag-handler checks do not replace physical pointer/controller testing. Keep the existing `LobbyTests` dock, turn, start/cancel and blocked-queue cases; `LobbyNavigationRefinementTests.StartDockShowsChosenModeAndTextLeaderboardKeepsCareerPositionAndReturnFocus`; and `LobbyMapSelectionTests`. Run the existing Lobby suite before explicit Home setup to exercise compatibility, then the focused saved-view cases after setup. Repeat actual save → reopen → Play → Stop and inspect rendering at target resolutions. The legacy queue's existing matchmaking block is preserved; this draft does not establish online acceptance or change Wordbound matchmaking.

## Review package

`baseline/` contains exact pre-draft `LobbyPages.cs` and `LobbyShopLayoutSetup.cs`; `draft/` contains eight proposed source files. `home-layout.patch` and `manifest.json` are prepared for parent review, not applied here. Current source checks/review findings are recorded in `progress.md`. No commit belongs to this lane.

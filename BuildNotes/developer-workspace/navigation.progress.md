# Developer workspace navigation

STATUS: FROZEN FOR ROOT REVIEW — 2026-10-26. Root-authorized bounded editor navigation slice; no Unity/test/build jobs or metadata writes.

Owned source: `Assets/_Project/Editor/Authoring/WorkspaceWindow.cs`, `WorkspaceBootstrap.cs`, new `WorkspacePlayPreview.cs`, and meaningful additions to `Assets/_Project/Tests/Editor/WorkspaceEditorTests.cs`. This progress file is also owned. Root owns `WorkspaceAssetBrowser.cs`, gallery generation, all native validation and commits.

Inspected checkpoint: integration HEAD `4b04437f`; untracked Shop portrait output is outside this scope and remains unchanged.

Contract: retain the existing Workspace window and its safe scene-opening/temporary-ceiling behavior. Add grouped scene/design/assets navigation and GUID-based Wordbound scene links. One-shot Lobby/Shop/Loadout previews use the actual Hub and `LobbyMenu.Open`, save-prompt before entry, preserve the existing opened-scene preference and restore the prior play-start override. No shortcut selects an online connection or writes PlayerPrefs itself. Runtime-created lobby UI is labelled honestly.

Asset helper interface sent to root: `Wreckabulary.EditorTools.WorkspaceAssetBrowser.Draw()` (`public static void`, editor-only helper in the same assembly). Only the Assets tab calls it.

Next: implement a session-scoped preview override integrated with the existing bootstrap, add the window navigation, then inspect lifecycle/cleanup and add bounded source tests. Native execution remains root-owned.

## Source implementation checkpoint

- Existing window now has Scenes / Design / Assets tabs. Scenes exposes the two saved Wordbound scenes by their actual GUIDs, retains original scene links and opened-scene toggle, and explains the gameplay scene shell. Design selects existing course/camera/story/interface/presentation/launch assets, plus the preserved original character/world/material/source links. Assets delegates to root's agreed helper.
- New editor-only `WorkspacePlayPreview.Start(page)` accepts only `LobbyMenu.Home`, `.Shop` and `.Loadout`. It rejects automated/test/compiling/busy contexts, prompts to save, arms a SessionState override, and starts the real Hub without opening or replacing the authoring scene itself. No PlayerPrefs/EditorPrefs writes are made by the shortcut.
- Bootstrap consults the one-shot override only outside automated/test contexts. EnteredEditMode restores the captured play-start scene; a narrow restored-state hold prevents the deferred bootstrap callback from immediately overwriting it. The next ordinary Play or explicit opened-scene toggle releases that hold. The user's persistent opened-scene preference remains unchanged.
- Preview routing waits for the existing LobbyMenu and UI transition to be ready, calls `LobbyMenu.Open`, selects its runtime hierarchy root, and consumes the requested page once. Failure to become ready within 30 seconds stops the preview. Canceled Play entry restores the captured override.
- Added six source-defined native Editor cases: two saved-scene GUID/build-registration checks, three previous-entry restoration variants (null/Hub/other scene), and invalid/automated launch rejection. Tests retain existing scene-save safeguards, refuse an active human preview, and restore their temporary SessionState.

Pending: source read-back and root review. Native tests, actual Play/Stop behavior, window layout and screenshots are NOT-RUN.

## Source handoff

Read-back completed. `git diff --check` and new-file whitespace checks report no errors. All 33 asset-link occurrences in the window resolve to existing filesystem paths. Root's `WorkspaceAssetBrowser` exposes the agreed public static `Draw()` method. No launcher, runtime UI, scene, profile, build-settings or metadata file was edited by this lane.

`WorkspaceEditorTests` contains 15 source-defined cases (8 plain tests + 7 TestCase rows), including the six additions. Suggested root-native scope: `test editmode --cold --assemblies Wreckabulary.Editor.Tests --filter Wreckabulary.Tests.Editor.WorkspaceEditorTests --json`. This command was not run.

Manual verification still required: save-prompt cancellation; Play Lobby/Shop/Loadout then Stop from a different saved/additive authoring setup; null/custom previous play entry; opened-scene preference on/off; domain reload and canceled entry; actual focused page/runtime hierarchy; no online connection. Shortcut code contains no PlayerPrefs/EditorPrefs writes or service calls. Because it runs the real Hub, normal game startup and deliberate in-game user actions retain their existing save behavior.

All source is uncommitted as requested. Root owns final review, Unity imports, native checks, screenshots and integration commit. Existing root-owned browser/copy helpers, docs, gallery work and portrait artifacts remain outside this lane.

# Runtime typography ownership review

STATUS: SOURCE REVIEW COMPLETE — 2026-10-26. Reviewer: collection26. No concrete source blocker found in the reviewed hashes below. Read-only source review; no Unity, test, build, render or provider execution.

Scope: story26's live typography ownership changes, when frozen. Check that runtime font edits do not modify saved fonts, clones own their mutable materials/atlas data, cleanup releases only owned objects, and repeat Play remains safe with both domain and scene reload disabled. Root owns validation and integration. Concrete findings and reviewed hashes will be recorded here.

## Findings

- `LobbyRuntimeTypography.Instantiate` creates the copy under an inactive staging parent, remaps every TMP font/material before activation, then reparents it. Root's `ShopPage.cs` uses this helper for both saved page and offer. `LobbyKit.Text` also assigns its resolved font while the new label is inactive. Installed TMP `TextMeshProUGUI.Awake` calls `LoadFontAsset`, so this ordering addresses mutation during first activation; the native fixture must confirm it.
- `OwnedCache.CloneGraph` detaches the initial clone's inherited atlas/material references before registering ownership, clones atlas textures and material, checks for shared data tables, and remaps weight/fallback graphs through cached owned fonts. Dynamic source atlases must be readable. Presets retain properties and point to the corresponding owned atlas. Saved sources remain read-only in this code path; Edit Mode resolution preserves authoring references.
- Partial graph failure removes and releases only the newly created owned entries. Disposal excludes each font's base material from the separate material-release loop because installed `TMP_FontAsset.OnDestroy` destroys its atlas array and base material itself. This matches the package's actual destruction contract and avoids deleting the saved source references.
- `EditorSettings.asset` has Enter Play Mode options enabled with value `3`. At subsystem registration and after returning to Edit Mode, unused ownership is collected; a live TMP consumer prevents disposal. Event registration removes then adds the same handler. The consumer check avoids destroying fonts under a surviving UI when reloads are disabled. A surviving consumer deliberately keeps the prior clone: fresh authoring edits are not promised to appear until that consumer is rebuilt.
- The seven EditMode cases exercise nested glyph/table independence and source pixels, cyclic weights/fallbacks, preset preservation, retained-consumer cleanup, partial-failure cleanup, authoring reference identity and assembly-reload ownership transfer. The PlayMode case covers the actual saved Shop card, extended text, original glyph counts/pixels and preset ratios, plus shared runtime reuse. These are meaningful source-prepared checks, not results.

## Native acceptance still required

Run the focused fixtures and existing Lobby/Shop suites. Record before/after hashes of the saved fonts, atlas/material dependencies and typography profile through real Play/Stop, including the actual project settings with both reloads disabled. Repeat Play after changing an authored font/material, and inspect whether surviving scene consumers need explicit rebuild. Confirm generated runtime resources disappear once no TMP consumer remains, and that their destruction does not invalidate the original saved assets. No visual, glyph-rendering, memory-count or disk-preservation success is claimed by this review.

## Reviewed source identity

| File | SHA256 |
| --- | --- |
| `Assets/_Project/Scripts/UI/Lobby/LobbyRuntimeTypography.cs` | `f08659ab85a636bc3acc98a6c3259dfe6cfe41d5fd5302169459c84d92439e92` |
| `Assets/_Project/Scripts/UI/Lobby/LobbyTypographyReloadState.cs` | `609fc0d4f861942acb1300979564d46ba0078c57399019a6c3a8910f2b02b3c7` |
| `Assets/_Project/Scripts/UI/Lobby/LobbyFonts.cs` | `789b122ca49e8b352d8a2a9883af588fae4fa42e37768917b734a4a24317a2e6` |
| `Assets/_Project/Scripts/UI/Lobby/LobbyKit.cs` | `bef8b2c3618158e542b920f4c21a316bbdc35d1f1a9d757fbea77572f3064e4c` |
| `Assets/_Project/Tests/Editor/ShopLayout/LobbyRuntimeTypographyTests.cs` | `c84f85db369234a1ace803b8f0551a9868bc6c1fa62e7ebc4acc9920c278204e` |
| `Assets/_Project/Tests/LobbyRuntimeTypographyRuntimeTests.cs` | `535330d948af65636327c2721323c0bc9b7625290920ece310315ea22b4ac496` |

Package evidence inspected: `Library/PackageCache/com.unity.ugui@0727b404f3a0/Runtime/TMP/TMP_FontAsset.cs` (`Awake`, `OnDestroy`, `atlasTextures`, `ReadFontAssetDefinition`, `DestroyAtlasTextures`) and `TextMeshProUGUI.cs` (`Awake`, `LoadFontAsset`). Tracked typography diffs emitted no whitespace errors. The only review write is this report; source changes remain with story26/root.

## Final reload-handoff and ordinary-workflow review

The final helper hash above supersedes the initial `cbf6717f` review. The editor-only transient `LobbyTypographyReloadState` keeps original/owned font pairs, preset provenance and owned material references across assembly reload. Detach invalidates the old cache without releasing transferred objects; restore reconstructs the same mappings, then destroys only the plain state container. That container has no cascading destruction callback. The new test verifies original provenance, retained clone/preset identity and eventual owned-only disposal. `LobbyFonts.Load` also resets its role cache on Play/Edit context changes, keeping Edit-mode authoring from receiving retained runtime references. No concrete blocker found in this delta. The test exercises transfer methods; a real script/domain reload remains unexecuted.

Root's subsequent R0 caught an API accessibility error missed by this read-only review: `TMP_FontAsset.clearDynamicDataOnBuild` is not public in the installed package. Root removed that one unnecessary assignment; the copies are transient runtime assets, so this does not change source ownership or introduce a saved build asset. Confirmed the assignment is absent and updated the helper hash above from `fc3a0148` to `f08659ab`. R0 retry belongs to root; this report does not claim its outcome.

Ordinary Hub/Shop is expected to rebuild after Stop: `LobbyMenu.OnScene` creates a new `Lobby` GameObject during Play; its Canvas is on that root (`BuildCanvas`), and all page roots and saved Shop instances are children. `OnDestroy` clears the instance and removes the generated stage. No `LobbyMenu` script GUID reference was found in this checkout's project Scenes/Prefabs, and these UI paths do not call `DontDestroyOnLoad`. Thus the preserved-consumer case is a special surviving-object case, not the intended saved Hub authoring structure. Unity documents scene backup/restoration on leaving Play in its [reload details](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/code-reload-serialization/code-reloading-editor/configurable-enter-play-mode-details). Together, the source and documented restoration support the expectation that normal Stop removes the generated labels, the delayed collector retires their cache, and the next Play clones the latest saved choices. This is an expectation, not observed native interaction evidence; explicitly verify Stop → edit saved font/material → Play with the current reload flags before claiming that workflow complete.

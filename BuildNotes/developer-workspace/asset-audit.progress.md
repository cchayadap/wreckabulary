# Developer workspace asset audit

STATUS: DOCUMENTATION COMPLETE — 2026-10-26. Source/data audit and three documentation files handed off; native behavior is NOT-RUN in this lane.

## Scope and preservation

- Checkout: `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration`, branch `sethypagna/wordbound-integration`, audited starting revision `4b04437f`.
- Owned writes: this report, `docs/UNITY-WORKSPACE.md`, `docs/wordbound/DEVELOPER-WORKSPACE-PLAN.md`. No code, metadata, asset moves, imports, process changes, Unity, tests, builds, render jobs or commits.
- Preserved existing untracked `Assets/_Project/Resources/UI/Shop.meta` and `Shop/`; teammate navigation/browser source and `navigation.progress.md` remain separately owned. The original checkout's font/Lunar work and other branches are outside this lane.
- Read `CLAUDE.md`, `docs/kit/HARNESS_RULES.md` and the portable workflow. No `META-HARNESS.md` exists here. Root owns gates, native execution and `progress.md`.

## Confirmed findings

1. Six saved game entry scenes exist under `Assets/_Project/Scenes`: Hub, LivingRoom, Tutorial, MovingDay, WordboundLobby and WordboundGameplay. `ProjectSettings/EditorBuildSettings.asset` registers those six in that order. `ProjectSettings/ProjectVersion.txt` is `6000.6.4f1`; the existing workspace guide still said 6000.6.3f1 and four build scenes.
2. Five real editable house prefabs live in `Resources/Worlds`; the shared original player is `Prefabs/Player.prefab`. Wordbound has saved course, camera, visual, UI-theme and story profiles in `Data/Wordbound/Authoring`. These are usable authoring sources, with their own limited scopes.
3. Lobby, shop, loadout and gameplay HUD hierarchy/layout are still primarily built in C# (`LobbyMenu.CreateUI`, lobby page builders, `GameHud`; Wordbound launcher uses `OnGUI`). Saved `Interface.asset` changes Wordbound fonts/colors/spacing, not the main lobby's hierarchy. `Resources/UI/Inventory/Inventory.uxml`/`.uss` exist, but a C# search found only the workspace navigation link and no runtime UXML loading reference. Do not present that separate layout as the authoritative shipped inventory.
4. `Art/Imported/Items` mixes equipment, furniture and props (40 FBX); there is no separate imported Accessories folder. Boots, glasses, satchel and other wardrobe pieces are named skinned meshes in `Art/Imported/Avatar/Avatar.fbx`, mapped by `Data/Config/wardrobe.json` and `PlayerAppearance.Dress`. Splitting/renaming those meshes can break outfits and material tint slots.
5. `ModelLibraryBuilder.KeyOf` derives keys from paths below `Art/Imported`. Moving BAT from `Items/BAT.fbx` to a Weapons folder would change `Items/BAT` and break catalogue lookups despite preserving its `.meta`. `ImportedArtSettings` also derives rig/animation/UV behavior from the imported root and Avatar/Environment folders.
6. `Resources` relative names are runtime contracts: fonts, item/glyph/skin thumbnails, collections, environment, worlds, `Wordbound/LaunchAssets` and render materials are loaded by strings. GUID preservation alone does not preserve these names. Material family names (`_Classic`, `_Candy`, `_Arcade`, `_Winter`) and wardrobe mesh/material slot names also have meaning.
7. `MaterialLibraryBuilder.Build` reapplies specifications to existing materials in `Materials/Library`. A manual Inspector edit can be overwritten by that explicit rebuild. World assets under `Worlds/Generated` are saved reference targets, not disposable cache. Navigation must never invoke rebuild/import/upgrade commands implicitly.
8. `Art/Collections/WordboundPvP` is imported; Packing Club Blender sources, export receipts and the optional importer exist, but `Assets/_Project/Art/Collections/PackingClub` was absent at this audit. Source exports are not equivalent to selectable Unity assets, and a course visual override is not a per-player online skin.
9. Legacy archives are exactly `Assets/_Project/Editor/Legacy/Scenes`, `Worlds`, and `Dependencies/{Materials,Meshes,Textures}` (`WorldAuthoring.cs`). The existing organizer moves only direct `legacy_*` dependencies, not every generated/current asset. No explicitly named ICY/Lunar boundary was found in this checkout's Assets/ArtSource filenames; this does not establish absence in original/external work.

## Representative dependency chains independently checked

Read-only second review by `online26`; no writes or native jobs.

| Asset | Exact reference evidence |
| --- | --- |
| BAT | `Resources/ModelLibrary.asset:111-112`: key `Items/BAT`, GUID `93e906069a474aadcaacf0f260c6d1c7`, mesh file ID `-927199367670048503`. FBX importer references `cream_Classic` GUID `1cf78ec2796a16b4486b9b9ca316cf98` and `wood_light_Classic` GUID `2e90cf7537c859347b7878bf1937f5d8` in `Materials/Library`. |
| Original player | `Prefabs/Player.prefab` GUID `786da980fe0631447b88c6a7c203d101` is referenced by `Resources/GameAssets.asset:38` and original scenes. It embeds Avatar FBX GUID `135c19a8edb14d98112c58a9f53fb0f4` with subasset transform/material overrides. |
| Launch/profile chain | `WordboundLobby.unity:243` references LaunchAssets GUID `cd57f7a1b4008fe468a0aaca5110915a`. LaunchAssets and both course assets reference PlayerCamera GUID `aa3aabd6d6fb281488c422c770328a56` and Presentation GUID `8513f12a73657d54393cf5b4d5749239`. |
| Fonts | Player prefab uses LiberationSans SDF GUID `8f586378b4e144a9851e7b34d9b748ee` and its material subasset. `Interface.asset:15-16` references Nunito font GUIDs `879bef6e44e669d488c6e7d2499bffd0` / `a68083542e26d6d408320883f00ee81d`. `LobbyFonts` loads `Fonts/LilitaOne-Regular` by resource string. |

## Bounded organization recommendation

First deliver categories in the existing editor workspace, with search/select/ping/open actions targeting existing assets. Group Weapons, Shields, Tools, Furniture, Character/Accessories, Fonts, Materials, Icons, Animation and Collections without duplicating or moving source files. Read actual catalogue/model keys and distinguish missing assets, submeshes and runtime-generated views. Add new content in deliberate category folders; retain compatibility locations for original assets.

Before any physical migration, record old/new path, GUID, local file IDs, model/resource key, importer settings, material/rig/subasset references and dependent scenes/prefabs. Introduce explicit stable model-key mapping before moving imported FBX. Preserve runtime Resources paths or change every caller under a separate reviewed migration. Move only a small independent hand-authored set through Unity `AssetDatabase.MoveAsset`, then verify save/reopen, references, render bounds/grips and actual player behavior. Never blanket-move `Worlds/Generated`, vendor fonts or archived/original/ICY content.

Unity's [asset metadata guide](https://docs.unity3d.com/6000.6/Documentation/Manual/AssetMetadata.html) explains reference IDs and metadata preservation; [AssetDatabase.MoveAsset](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/AssetDatabase.MoveAsset.html) is the supported editor move API. Neither makes path-derived keys safe automatically.

## Useful implementation APIs

Paths below are relative to `Assets/_Project/`.

| Source | API / constraint useful for the workspace |
| --- | --- |
| `Editor/Authoring/WorkspaceWindow.cs` | `ShowWindow()`, `OpenScene(string)`; existing save-prompt and temporary ceiling restoration. GUID-based scene navigation is being extended. |
| `Editor/Authoring/WorkspaceBootstrap.cs` | `PlayOpenedScene`, `ApplyPlayEntry()`; preserve the persistent preference and automation safeguards. |
| `Editor/Authoring/WorkspaceAssetBrowser.cs` | New `Draw()`, `ReadEntries()`; source-only category index, no implicit import/rebuild. |
| `Editor/Authoring/WorkspaceStudioSetup.cs` | New `CreateOrVerify()`, `PrefabPath(ItemDefinition)`, `EquipmentScene`, `CharacterScene`; missing-only setup, explicit invocation. |
| `Editor/Authoring/PlayerAuthoring.cs` | `CreatePreview(Transform)`, `FramePreview(Camera, Transform)`, `PlayerPrefabPath`; preview original authored avatar. |
| `Scripts/Online/Authoring/Editor/WordboundAuthoringSetup.cs` | `CreateDefaults()` and saved profile/course path constants; missing defaults preserve assigned content. |
| `Scripts/Online/Authoring/Editor/WordboundCoursePreview.cs` / `WordboundCoursePlaytest.cs` | `Show(asset, exact)` / `Start(asset, exact, expectedInputHash)`; exact source validation, local TimeRun/Freestyle only, explicit scene-restore recovery. |
| `Scripts/Online/Presentation/WordboundVisualProfile.cs` | `Validate()`, `ValidateModel(GameObject)`, `TryInstantiate(...)`; render-only overrides, not network skin entitlement. |
| `Scripts/Art/ModelLibrary.cs` / `MaterialLibrary.cs` | Model `Load/Find/FindClip/Spawn`; material `Load/Find/ForSkin/ApplySkin/FamilyOf`; keys and material family names must stay stable. |
| `Scripts/Player/PlayerAppearance.cs` | `Dress(...)` can dress a preview clone; `ApplyOutfit` and `SetItemSkin` write player preferences, so do not call them just for browsing. |
| `Scripts/Online/Client/WordboundLaunchAssets.cs` | `ResourcePath`, `LobbyPath`, `GameplayPath`, assigned profiles and body prefabs; gameplay scene path/build registration is a runtime dependency. |

## Verification and limits

- Compared guide instructions with the saved Unity version/build scene list, course Inspector action labels, camera Inspector guidance, original asset paths and teammate source contracts.
- `git diff --check -- docs/UNITY-WORKSPACE.md` passed. All three written Markdown files were scanned for trailing whitespace: zero lines found.
- Documentation is saved and uncommitted by instruction. No runtime/Unity tests, imports, asset generation, scene creation, source edits, metadata changes or existing-job interruption occurred in this lane.
- Independent review covered the representative asset dependency chains above. The complete new docs received self-review; the peer was working on typography and did not claim a full documentation review.

## Recoverable next step

The user workflow guide and explicit acceptance plan are drafted. They now incorporate the team's stable labels and the new source-defined studio/typography outputs. `WorkspaceAssetBrowser` provides Weapons/Furniture/Tools/Characters/Accessories/Materials/Fonts/Animation/Audio/UI, Find, select/ping and independent model/material Copy. `WorkspaceStudioSetup.CreateOrVerify` targets categorized equipment/furniture copies and `Scenes/Authoring/{EquipmentStudio,CharacterStudio}.unity`. `LobbyTypographySetup` targets `Resources/UI/LobbyTypography.asset`, `UI/Fonts` and `UI/Materials`. All those new setup outputs remain pending native creation at this checkpoint; the guide explicitly distinguishes them from existing saved assets.

Source references read for this update: `WorkspaceWindow.cs`, `WorkspaceAssetBrowser.cs`, `WorkspaceStudioSetup.cs`, `LobbyTypographySetup.cs`, `LobbyFonts.cs`; navigation contract supplied by `story26` and setup contract by root. The independent reviewer is currently implementing typography, so a full independent documentation review is not claimed.

Next: root reviews the three saved docs with the related source, creates the missing assets through the gate when ready, and runs native save/reopen/Play restoration. Saved authoritative UI layout conversion and Windows/online consistency remain open acceptance work. No commit was made by this lane.

## Follow-up: smallest saved-layout conversion

STATUS: SOURCE AUDIT COMPLETE — 2026-10-26. Root requested a narrow read-only implementation-boundary audit while other jobs continue. Only this report was appended; no implementation, native execution or heavy commands.

Confirmed seam: `LobbyPage.Refresh()` in `Scripts/UI/Lobby/LobbyPages.cs` calls `Closed()`, clears the page root, resets `First`, and invokes `Build()`. `ShopPage.Build()` creates a side panel, category buttons, offers scroll/list and footnote. `ArtCard()` and `ActionFor()` create repeated offer views. The page's `Preview`, `Buy`, `Wear`, `SelectRecipe` and `EquipAccessory` already contain the player state behavior and can remain the controller.

Recommended first slice: a saved Shop page-shell prefab plus an offer-card prefab, instantiated by the existing Shop controller after each refresh. Typed serialized references identify the close button, categories host, scroll/content root, footnote, preview button, art slot, title, state outline and action button. Designers edit anchors, geometry, spacing, fonts/materials and card layout in Prefab Mode. Bind only offer text/images/state/callbacks at runtime; do not reconstruct or overwrite those authored layout values. A numeric layout profile alone would improve Inspector tuning but would not deliver the requested visible drag-editing workflow.

### Exact source map and boundary

All paths are relative to `Assets/_Project/`; line numbers refer to the working source inspected at this checkpoint.

| Source / entry | Current responsibility | First conversion boundary |
| --- | --- | --- |
| `Scripts/UI/Lobby/LobbyMenu.cs:99` `Start()`; `:255` `BuildCanvas()`; `:282` `BuildBar()` | Loads/saves career/outfit state, suspends Hub, creates Stage/Canvas/navigation and page controllers | Keep untouched except a read-only layout asset reference/load if required; do not capture or run this lifecycle in an editor preview |
| `LobbyMenu.cs:541` `Open()`; `:585` `Close()` | Closes prior preview, refreshes page, frames stage, selects first control and restores opener focus | Preserve callbacks and sequence; account for authored panel width in stage framing |
| `Scripts/UI/Lobby/LobbyPages.cs:27` `LobbyPage.Create()`; `:43` `Refresh()`; `:66` `Panel()` / `Side()` | Page host, destructive refresh, hard-coded side-panel construction | Initially keep refresh semantics. Shop instantiates a fresh saved view subtree instead of invoking `Side`; other pages keep their current builders |
| `Scripts/UI/Lobby/ShopPage.cs:49` `Build()` | Category tabs, footer, ScrollRect/list and per-category grids | Replace page-shell geometry with serialized view references; bind category/data/state only. Existing category IDs and selected recipe remain controller state |
| `ShopPage.cs:141` `ArtCard()` / `:158` `ActionFor()` | Repeated preview/action cards for looks, finishes, colors and rooms | Instantiate one saved card template; keep data/picture binding and Buy/Wear/Preview decisions in the controller |
| `ShopPage.cs:274` `Preview()`; `:295` `Buy()`; `:305` `Wear()`; `:328` `EquipAccessory()` | Preview restoration, ownership checks, purchase/save, selected-recipe equip and wardrobe validation | Preserve behavior. No serialized event may bypass these methods or grant state from the view |
| `LobbyPages.cs:548` `LoadoutPage`; `:575` `Build()`; `Locker()` / `Recipes()` | Locker/recipe tabs, scrolled position, part/finish/item selection and transient try-on | Follow-up reuse of the side-panel view. Keep `Opened/Closed`, scroll capture and `PutOn`/`PickFinish`/recipe behavior before replacing its templates |
| `LobbyPages.cs:194` `HomePage.Build()` | Home start dock, conditional mode/map/Wordbound entry and turn hint | Later saved Home dock; keep conditions, `Menu.Go` and scene/queue logic. Full top-bar/party conversion is a separate boundary |
| `Scripts/UI/Lobby/LobbyKit.cs` | UI factory, names, styles, Button/Body/Focus shape, ScrollRect/RectMask2D and `LobbyScrollFollow` | Use to seed an explicit setup only where dependencies can be persisted; do not call positioning/style factories over an authored view on every refresh |

Concrete proposed new assets: `Prefabs/UI/Shop/ShopPage.prefab`, `Prefabs/UI/Shop/ShopOfferCard.prefab`, and a saved binding/profile asset such as `Resources/UI/LobbyLayout.asset`. Proposed new runtime types are a typed Shop page view, offer-card view and layout reference/profile under `Scripts/UI/Lobby`; an explicit missing-only setup belongs under `Editor/Authoring`. These names are recommendations, not implemented files. The asset reference makes the shipped runtime use the saved prefab; an editor-only studio copy would not be authoritative.

The first slice provides the saved Shop shell and common offer cards, with the Looks shelf as the clearest acceptance case. Recipe chooser, accessory cards/emote controls and other special content may remain dynamic in explicitly named slots. That is useful partial authoring, not completion of every Shop/Loadout/Home layout. A small saved Shop studio may instantiate the same assets with inert sample data for Scene View; it must not create `LobbyMenu`, invoke `Buy/Wear`, save career or call `PlayerAppearance.ApplyOutfit` just to preview.

### Concrete hazards and required checks

1. **Transient graphic dependencies:** `LobbyIcons.Get` and `Slice` create Texture2D/Sprite objects (`LobbyIcons.cs:23-40`, `:115-137`); `LobbyKit.Paint/Icon/Frame` assign them. Saving a live hierarchy without persisting these dependencies can leave missing graphics after reload. The setup must create persistent sprite/texture assets or use a deliberate serialized procedural-graphic component with Edit Mode preview. Use the new saved TMP typography assets once native setup is validated; do not serialize runtime-only TMP/material instances. Validate every graphic/font/material dependency after reopen.
2. **Authored values overwritten:** `Side/Panel`, `Place/Pin/Size`, `Grid` and card creation currently hard-code offsets, sizes and colors. Replace those calls for converted elements; runtime binding must not reset designer changes. Dynamic grid cell size/spacing must come from the saved layout. Measure/use authored panel width for the `LobbyMenu.Open → LobbyStage.Frame` occupied-side calculation so a resized panel does not cover the character.
3. **Refresh and callbacks:** `LobbyKit.Clear` disables and destroys each old child; `ShopPage.Closed` restores theme, held-item preview and outfit. Preserve this lifecycle first. Bind each cloned button once, dispose only owned listeners, and avoid serialized persistent callbacks that duplicate or bypass the controller. Test repeated category changes/refreshes followed by a single click: one preview, purchase or equip action only.
4. **Names/focus/scroll are behavior contracts:** retain `Shop category <id>`, `Offer <offer.Id>`, `Buy/Wear/Wearing <offer.Id>`, `Close page`, `Choose recipe`, and required Body/Focus references, or deliberately migrate both selection code and tests. `First` and `Reselect` choose actual live Selectables; `LobbyScrollFollow`, masking, raycast flags, inactive templates and nested action buttons must keep keyboard/controller selection visible and clicks unambiguous.
5. **Minimal migration risk:** retain the existing C# controllers and transaction/career rules. During rollout, distinguish a missing optional layout asset from a malformed assigned layout. An explicit invalid-assignment diagnostic is safer than silently ignoring a designer's saved prefab. Setup creates missing assets and preserves authored destinations; never bake the current player's state or overwrite a saved layout on startup.

Relevant existing tests to preserve (source inspected, not executed here):

- `Tests/ShopCollectionsTests.cs`: `CategoriesIsolateShelvesAndRestoreUncommittedPreviews`, `GearSpotlightKeepsTheChosenRecipeAndTargetsThePurchase`, `ThemePreviewDoesNotPurchaseAndEquipPersistsAcrossLobbyReload`, `RecipeSkinUsesNativePreviewRequiresOwnershipAndChangesOnlySelectedGear`, `LockerRecipeFinishOpensTheSelectedGearWithoutChangingTheOutfit`, `LockerShowsMixedRecipesUntilApplyToAllIsExplicitlyChosen`, `CollectionCheckoutCreditsOwnedPartsAndAccessoriesStayFree`.
- `Tests/LobbyTests.cs`: `LoadoutAndShopChangeWhatYouWear`, `LockerExtrasHoodAndPartColours`, `TheShopOpensTheCategoryAndShowsTheOfferYouCameFor`, `RecipeDetailFollowsTheCardYouPick`; its helpers find controls by name.
- `Tests/LobbyNavigationRefinementTests.cs`: `CoinBalanceOpensShopThroughSubmitAndClosingReturnsFocusToItsLiveBalance`; it relies on `Safe area/Top bar` and the live opener. Keep that unconverted shell stable.

New evidence should test the specific failure this conversion prevents: change saved panel/card geometry and a material/font assignment, save/reopen, enter the real Shop, refresh/change category/buy/equip, and assert that those authored values persist while existing state/focus behavior remains correct. Also verify all dependency references persist; malformed bindings produce a clear diagnostic; preview/setup changes no player state; repeat setup leaves user values intact; actual mouse/controller scrolling and the Windows frame match the saved view. Tests belong to the later implementation slice; none were added or run in this audit.

Next: root can assign the bounded Shop shell/card conversion with these typed-binding and persistence requirements. Loadout can then share the same saved panel contract before Home/top-bar conversion. All read commands in this audit finished; no native job, source, metadata, plan document or process was changed.

## Follow-up: developer usability wording review

STATUS: DOCS FROZEN FOR ROOT REVIEW — 2026-10-26. Re-read latest `WorkspaceStudioSetup`, `WorkspaceAssetBrowser`, `WorkspaceAssetCopies`, `WorkspacePlayPreview`, `LobbyTypographySetup`, `LobbyTypographyProfile`, `LobbyFonts`, `LobbyStage.PreviewItem` and `WordboundVisualProfile`. Changed only the three owned documentation files; no code/native/heavy work.

Corrections made:

- The guide explicitly says Play Lobby/Shop/Loadout use real local player saves. Normal purchases, equips and theme changes can persist; Stop restores editor entry/scene setup, not career/outfit data. The plan separates harmless asset browsing from real game entry and future inert Edit Mode samples.
- Copy destinations are exact and the shared-dependency limit is explicit. A prefab copy wraps the source model, retaining mesh/material references. For an independent surface edit, copy the material and assign that saved material to the copied prefab. Copying never auto-equips or changes runtime bindings.
- Dragging a studio instance or Studio Camera affects only that saved studio. Gameplay requires editing/saving the reusable prefab and assigning it to the active Wordbound visual profile; original lobby item previews still use their model library. Studio changes do not silently become map/gameplay changes.
- Saved lobby typography is described through its real assigned Display/Body/Black font and Stroke/Drop/foreground material fields. Save and restart Play because runtime assignments are cached. Invalid profile settings warn and use generated defaults; individual UI label sizes/positions still belong to page builders.
- Confirmed at this read-only checkpoint that `Resources/UI/LobbyTypography.asset`, `UI/Fonts`, `UI/Materials` and both `Scenes/Authoring` studio files are absent. Source/setup availability remains separate from native generated-output success.

Usability conclusion: this slice improves select/find/copy/edit/save of existing assets and provides source for useful saved studios/typography. It does not yet allow drag-editing the real Shop/Loadout/Home layout. Highest next priority after current native checks is the saved Shop shell and offer-card slice described above: drag/resize an actual prefab element, save/reopen, and see the same edit in the real Shop through refresh. Then reuse that proven pattern for Loadout and Home. No further doc changes are planned until root requests them.

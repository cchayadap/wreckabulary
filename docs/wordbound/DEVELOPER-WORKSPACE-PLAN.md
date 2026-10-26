# Unity developer workspace: implementation and acceptance

Status: 2026-10-26. Saved workspace/studios and Shop layouts are committed through `8304fa93`. Loadout panels/cards and the lobby-camera profile are saved; 16 focused Editor and 82 combined Play cases pass. All 125 authored outputs survive repeat setup and Play unchanged. Interactive visual and Windows verification remains in progress. `[x]` means the stated fact is confirmed, `[~]` means work is in progress, and `[ ]` means the outcome remains unverified or unimplemented.

## Goal

James can open a saved scene, find a weapon/accessory/font/material, change its appearance or camera/UI setup in Unity, save it, reopen it and try the real game without writing code. The same authored result must reach the Windows player and the appropriate online clients. Navigation, preview and generators must preserve deliberate edits, existing original/ICY work, model keys and asset references.

The editor should expose a few clear categories and direct actions. The game itself should retain a minimal, immersive presentation; implementation details and development controls belong in the editor.

## Current baseline

| Area | Confirmed source/data | Remaining boundary |
| --- | --- | --- |
| Scenes and worlds | Six entry scenes, five saved house prefabs, saved Player prefab and lighting assets | Native workspace restoration and new page shortcuts still need verification |
| Wordbound design | Saved camera, visual, theme, story and two course assets with custom Inspectors | Different profiles have different scopes; a theme is not a complete saved lobby layout |
| Course editing | Form/Scene editing, save action, exact-seed preview and local TimeRun/Freestyle playtest APIs | Other modes, public publishing and online consistency require separate evidence |
| Home/shop/loadout | Saved runtime-connected panel/card prefabs and profiles; editable geometry, spacing and typography | Wearable pickers, recipe details and some Shop sections still use generated layouts; Play hierarchy edits remain temporary |
| New studio/typography setup | Two saved studios, 24 original-model prefab variants, three TMP fonts, five text materials and a runtime typography profile are generated | Native creation/repeat setup and36 focused cases pass; live editor interaction, rendered review and Windows acceptance remain |
| Assets | Original FBX, shared material library, wardrobe mapping, collection/seasonal assets, fonts and icons exist | Physical folder names are mixed; moving imported/Resources assets can break keys |
| Packing Club | Editable source and export receipts exist; optional validation/import source exists | Import, Unity appearance and owner acceptance must be recorded separately |

The exact current paths and normal editing steps are in [UNITY-WORKSPACE.md](../UNITY-WORKSPACE.md). Audit evidence and representative GUID chains are in `BuildNotes/developer-workspace/asset-audit.progress.md`.

## 1. One workspace with useful categories

- [x] Retain `Wreckabulary → Open Current Workspace` and its existing safe scene-opening and temporary ceiling visibility behavior.
- [~] Add **Scenes / Design / Assets** navigation in that same window.
- [~] Provide **Play Lobby**, **Play Shop**, **Play Loadout** one-shot entry into the real Hub and page controller. Preserve the normal Play target and opened-scene preference.
- [~] Add asset search and category browsing that selects/pings existing Project assets. Use model/catalogue identity and actual references; show when content is a submesh or generated at runtime.
- [~] Offer **Copy** with a save dialog for supported model/material entries. Preserve original imports and require an explicit runtime assignment. A prefab copy retains source mesh/material references; separate material copies are needed for independent surface edits.
- [x] Add **Create missing developer assets**, **Open Equipment Studio** and **Open Character Studio**. Native setup/repeat pass and preserve77 output files; dirty untitled-scene and failure-cleanup tests pass. Studio placement is a saved review layout, not a gameplay override.
- [ ] Verify keyboard focus, scrolling, clear selection feedback and useful empty/missing states at normal and compact editor window sizes.
- [ ] Confirm that asset selection/navigation does not mark scenes dirty, regenerate assets, connect online, alter inventory/currency or change outfit preferences. Keep this distinct from one-shot Play entry, which runs the real Hub and player saves; purchases/equips during that Play session retain their normal persistence.

Implemented browser categories: **Weapons**, **Furniture**, **Tools**, **Characters**, **Accessories**, **Materials**, **Fonts**, **Animation**, **Audio**, **UI**; **Find** filters the current category. These are discovery categories, not instructions to move existing files. Accessory entries identify original shared-rig meshes and link wardrobe data. Worlds remain directly accessible from Design. Shields and collections can become additional filters if actual usage shows a need, without relocating originals.

## 2. Saved authoritative UI authoring

This is required to meet the user's no-code UI request. A button that opens C# or a temporary Canvas does not satisfy it.

- [~] Create runtime-connected saved lobby typography through `LobbyTypographySetup.CreateOrVerify()`: `Resources/UI/LobbyTypography.asset`, `UI/Fonts` and `UI/Materials`. Native setup and persistence cases pass; actual rendered behavior remains pending. Verify actual assigned Display/Body/Black fonts and matching presets after restarting Play; invalid profiles warn and fall back to generated defaults.
- [~] Saved Home/Shop/Loadout panel and card prefabs, typography and lobby-camera profiles now exist. Inner generated sections remain outstanding. Each assigned saved surface is authoritative; missing-profile compatibility uses the existing default construction.
- [~] Real Home/Shop/Loadout controllers bind data/state to saved layouts. Home's five Editor and 70 combined Play cases pass; all 146 saved outputs remain unchanged. Inner generated sections and physical editor/player interaction remain. See `BuildNotes/developer-workspace/home-recovery-20261026.json`.
- [~] Shop/Loadout containers and cards expose Rect Transform/grid editing, fonts/materials, picture/letter slots and state inks; the lobby camera exposes framing. Focused Editor persistence checks pass; actual visual/navigation checks remain.
- [ ] Provide Edit Mode previews with sample data that never write purchases, currency, ownership, equipped slots, PlayerPrefs or network state.
- [ ] Keep action bindings explicit and validated. Warn on a missing button/view/reference before Play; retain the previous valid asset on invalid apply.
- [ ] Make duplicates/variants and Undo/Redo work. A saved variant must have an explicit runtime binding, not depend on an editor-only selection.
- [ ] Save a deliberate card size/font/material/camera change, close/reopen Unity, then launch the actual page and verify the same change. Compare with a Windows player from that revision.
- [ ] Verify mouse, keyboard and controller navigation; selected/locked/equipped recognition; readable text and icons at supported aspect ratios and UI scales. Tooltips may explain unfamiliar icons without forcing persistent paragraphs onto the player.
- [ ] Exercise empty, loading, unavailable, owned, locked, equipped and error states without overflowing or hiding the primary action. A preview state must not be mistaken for a successful transaction.

`Interface.asset` remains the current Wordbound menu theme until this conversion is deliberately integrated. The separate Inventory UXML/USS is not evidence that the current shop, loadout or HUD uses saved layouts.

## 3. Durable art and design editing

- [x] Expose the existing `PlayerCamera`, `Presentation`, `Interface`, `LostAddressStory`, `FlagshipCourse` and `FreestyleCourse` assets through the Design section.
- [ ] Verify that camera changes persist and the real runtime uses the assigned profile; distinguish starting-angle changes that require camera recreation from live valid updates.
- [ ] Provide clear Scene/Prefab inspection for equipment bounds, renderer materials and grip alignment. Show the original item beside a candidate where useful; preserve original source as the comparison baseline.
- [ ] Keep wardrobe mesh names, rig references and tint slots intact so boots, glasses and satchel retain their colors, including black.
- [ ] Separate local presentation overrides from per-player owned/equipped skins. The latter need stable IDs, validated ownership, replicated equipped selection and remote render checks before they are described as online cosmetics.
- [ ] Make each new collection discoverable with its saved materials, textures, models, icons, source location, source/export receipt and optional preview profile. Do not automatically replace the default presentation.
- [ ] Verify animations and held equipment in actual motion and gameplay camera views; still images cannot establish grip, transitions or clipping quality.
- [ ] Keep story copy editable without silently changing puzzle/ending rules. Preview existing chapter/epilogue copy and verify the corresponding actual outcome separately.

## 4. Canonical organization without breaking the game

Use the current folder map as the compatibility baseline. For future authored content, prefer the following destinations only when the relevant importer/runtime binding supports them:

| Purpose | Proposed destination / policy |
| --- | --- |
| Saved UI roots/pages/card variants | `Assets/_Project/Prefabs/UI/{Lobby,Shop,Loadout,Shared}`; new serialized references |
| UI presentation profiles | `Assets/_Project/Data/UI/`; keep current Wordbound profiles where they are unless explicitly migrated |
| Lobby typography setup outputs | `Assets/_Project/Resources/UI/LobbyTypography.asset`, `Assets/_Project/UI/Fonts`, `Assets/_Project/UI/Materials`; saved outputs exist and native setup/repeat passes; real Play and font-preservation acceptance remain unfinished |
| Saved authoring studios | `Assets/_Project/Scenes/Authoring/{EquipmentStudio,CharacterStudio}.unity`; saved outputs exist and native setup/repeat passes; separate from six game entry scenes; actual editing/Play interaction remains unfinished |
| New collection models/materials/textures/icons | `Assets/_Project/Art/Collections/<Collection>/<Version>/{Models,Materials,Textures,Icons}`; honor existing collection importer contracts |
| New standalone authored material variants | `Assets/_Project/Materials/Authored/<Category>/`; deliberate renderer/profile binding, outside the generator-owned library |
| New standalone reusable prefab variants | Setup uses `Assets/_Project/Prefabs/Equipment/{Weapons,Tools}` and `Assets/_Project/Prefabs/Furniture`; future character variants need their own deliberate bindings |
| Source Blender projects and export receipts | Project-root `ArtSource/Collections/<Collection>/<Version>/`; never treat source renders as installed Unity assets |
| Original models, fonts, Resources and archives | Keep existing locations and metadata until a separately validated migration supports every key and dependency |

- [x] Identify path-derived model keys, importer folder conditions, Resources strings, material family names and wardrobe mesh names before organizing assets.
- [~] Deliver a category index first. It should improve finding assets without copying files or changing dependencies.
- [ ] Define an explicit stable model-key registry/import mapping before any original FBX relocation. Preserve keys such as `Items/BAT`, `Avatar/Avatar` and all other referenced entries.
- [ ] Record each proposed move's old/new path, GUID, local file IDs, model/resource key, importer settings and dependent assets. Include FBX embedded meshes, materials, rig/animation subassets and prefab overrides.
- [ ] Start any physical migration with a small independent hand-authored set that is not loaded by Resources/path. Move through Unity's asset API, preserve metadata, and reject destination conflicts.
- [ ] Verify every impacted serialized reference and string lookup, reopen prefabs/scenes, and compare import settings, material assignments, dimensions/grips and animation bindings before expanding the migration.
- [ ] Preserve original/ICY assets and the separate original checkout's font/Lunar work. Absence of an ICY-named folder in this checkout does not authorize deletion or movement of another direction.

Do not relocate `Art/Imported/Items` into a Weapons folder just for appearance: `ModelLibraryBuilder.KeyOf` would change keys. Do not relocate runtime fonts, icons, collections or worlds out of their Resources paths without updating every caller and verifying the player. Do not flatten `Worlds/Generated` or `Editor/Legacy`; current saved worlds and historical references have different roles.

## 5. Generators must preserve authored choices

- [ ] Document each generator's owned outputs and the assets that developers may edit. Keep regenerable base outputs separate from deliberate variants.
- [ ] Navigation, inspection, save/reopen and Play must not implicitly run a rebuild, migration or importer.
- [ ] Existing materials/profiles/prefabs must survive a normal setup command. If a deliberate rebuild would overwrite authored fields, expose the exact scope before execution and preserve a recoverable prior version.
- [ ] Add meaningful preservation checks: edit a saved material/profile/prefab value, run the intended setup/refresh, reopen and assert that the authored value and reference remain.
- [ ] Keep partial-import failure recoverable: receipts, original sources and existing assets remain intact; no half-installed default is assigned.

The current `MaterialLibraryBuilder.Build` reapplies specifications to existing library materials. Until it supports an explicit authored-override policy, use deliberate material variants and bindings rather than promising that arbitrary library edits survive that rebuild.

## 6. Try, save, reopen and restore acceptance

Root runs native verification through the project gate and records revision, editor version, scene/profile paths and actual evidence. No row is complete from source review alone.

| Scenario | Required result | Status |
| --- | --- | --- |
| Open scene with unsaved edits | Save/Discard/Cancel is respected; Cancel neither opens another scene nor enters Play | NOT-RUN |
| One-shot Lobby / Shop / Loadout | Correct real page opens once; normal Play preference and prior start scene return after stop, aborted entry and domain reload | NOT-RUN |
| Real player saves during one-shot Play | Document and preserve normal Hub purchase/equip persistence; no extra state changes from the editor shortcut; stopping Play does not promise save rollback | NOT-RUN |
| Multi-scene editing | Existing scene setup, active scene and unsaved edits are preserved by the page shortcut | NOT-RUN |
| Scene/Prefab edit | Position/material edit survives save, close/reopen and actual runtime selection | NOT-RUN |
| Studio creation/repeat | Missing categorized prefabs/scenes are created once; deliberate saved edits survive repeat setup; original imports, scene setup and default gameplay bindings remain intact | Native setup/repeat PASS: 24 prefab variants, two studios, 77 preserved outputs across studio/typography setup and 36 focused editor cases; complete edit/Play workflow remains unfinished |
| Saved lobby typography | Profile/fonts/presets persist and the actual lobby uses the assigned assets; repeat setup preserves authored fonts/material values | Native asset creation/editor cases PASS; actual interaction, Play and preservation of saved fonts during Play remain unfinished |
| Camera/profile edit | Valid saved values reach the runtime; invalid edits retain the last valid presentation; restart uses saved values | NOT-RUN |
| UI authoring edit | Saved layout/font/card/camera edit appears in the real page after editor restart and in Windows player | NOT-RUN; authoritative UI conversion pending |
| Exact course preview | Save, resolve and local play use the same source/seed/content; stale preview is rejected; scene setup restores on exit | NOT-RUN for this workspace slice |
| Resource/model browsing | All required runtime keys resolve; no missing FBX subassets, materials, fonts or icons after any approved move | NOT-RUN |
| Setup/rebuild preservation | Intentional authored changes remain after supported refresh; no implicit rebuild from workspace actions | NOT-RUN |
| Preview cleanup | Temporary cameras, lights, RenderTextures, objects and scene visibility changes are removed/restored on close, stop and failure | NOT-RUN |
| Native player | Actual Windows views match saved authoring choices at tested resolutions; no editor-only dependency or missing reference | NOT-RUN |

The tests should target persistence and restoration failures, not merely repeat implementation details. Keep native runs serialized with other work; documentation and source review can continue independently.

## 7. Online consistency and release boundary

- [ ] Check cosmetic/profile assets without changing rule values, collider dimensions or authoritative item behavior.
- [ ] Exercise host and remote client views for every multiplayer/team mode in the supported-mode catalogue, including public matchmaking, equipment/skin selection, rejoin and late join where supported. Record exact modes actually exercised.
- [ ] Confirm that course documents, resolved seed/manifests and public catalogue bindings remain consistent after edits. A private local course preview must not silently enter the approved public pool.
- [ ] Validate matching visual identity for remote equipped cosmetics; reject unknown/unowned IDs without granting items or corrupting existing selection.
- [ ] Confirm that presentation-only asset edits do not create a misleading claim of network parity. Record canonical state and both clients' rendered outcome where the change affects what teammates see.
- [ ] Deliver the tested Windows build, a short no-code try/edit guide, and exact remaining limitations. Owner visual/feel acceptance stays separate from automated checks.

## Completion rule and next checkpoint

The current bounded workspace slice is complete when category navigation and one-shot page entry are reviewed and native save/restore checks pass. The overall no-code workflow is complete only when saved authoritative UI layouts, durable asset/camera editing, safe generator behavior and Windows/online consistency also pass their acceptance rows.

Native studio creation, repeat preservation and all 36 focused editor cases pass; see `BuildNotes/developer-workspace/native-20261026.json`. The **Shop panel and common offer card** are now saved and assigned after native setup `080702`; all 12 Shop persistence cases and eight runtime-font ownership cases pass. Purchase/equip behavior stays in the existing controller. Actual editor interaction, Play/Stop, rendered typography and file preservation through real Play remain unfinished. Next, verify: drag a card element or resize the panel, save/reopen, enter the real Shop, and see that edit survive refresh. Reuse the validated pattern for Loadout and Home afterward; their drafts are not installed. Navigation alone does not complete visible UI authoring. Preserve existing native jobs and uncommitted work. Root maintains the project-wide `progress.md`.

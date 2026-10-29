# Team division and merge-safety review

STATUS: COMPLETE for read-only ownership/ancestry review. Proposed boundaries below are not new human assignments or merge approval.

Recorded2026-10-29T01:21:23Z. Integration pinned to036f352b0a07ed132d971a7c2393ab1fbc4a4a41 on `sethypagna/wordbound-integration`. Root fetched current refs and supplied `../fork-heads-before.txt` and `../upstream-heads-before.txt`. GitHub PR observations are root-supplied; local ancestry/patch comparisons were executed here.

## What is already integrated and what is published

| Fresh ref | SHA | Integration ahead / behind | Meaning |
| --- | --- | --- | --- |
| cchayadap/main |4d5c162a2f6cc703bf70b0a5009993746a3cc9dd |1020 /0 | Ancestor; team baseline is older. |
| cchayadap/james-v1 |0140d96d8219cb98bb73e2f9a469c82eb90ac79d |952 /0 | Ancestor; existing James history is retained. |
| cchayadap/ICY |fb94c9a0e159c792d4d0264329bfa66f56e9fc77 |951 /0 | Ancestor; do not merge the same ICY revision again. |
| SethyPagna/main |f7c3c90301817a033c98de6e32635d6d56de39de |950 /0 | Ancestor; local expansion is not published by this main ref. |
| SethyPagna/codex/environment-refinement |3e730e6e6fe307ed94b9c0f3ab691c27ab6d04ee |966 /0 | Ancestor; not the current integration head. |

The fresh fork head list contains no `sethypagna/*` remote branches. Root found upstream PR14 **James v1** open/mergeable at0140d96 and PR11 **James** open/conflicting at fork e79ed4cc; no open fork PR. These old PRs do not expose the current local expansion. No push, PR edit or merge is performed by this review.

## People and concrete ownership

The source plan explicitly identifies James and SethyPagna as one person. The ICY-only commit is authored by Git identity `Mizuki0215`. `Chayada Pakpoomkamonlert` appears in Git history, but that alone does not establish the unnamed lobby/UI person's current assignment. Do not fill that name by inference.

| Work direction | Human ownership supported by the instructions | Suggested owned feature additions | Shared changes go through the integrator |
| --- | --- | --- | --- |
| Lobby, character and UI design | Existing UI owner; name not supplied | Dedicated page prefabs/layout profiles, icons/fonts/material variants, page-specific components and a separate UI review scene | LobbyMenu/LobbyPages/LobbyStage hooks, shared typography/theme, global wardrobe/player rendering and runtime page registration |
| Character colours; Escape | ICY / Git identity Mizuki0215 | Escape-specific helpers/content/tests and an isolated Escape review scene; retain boots/glasses/satchel support | Existing EscapeDirector, shared input hints/bindings, GameData/GameAssets/Session, rules/wardrobe JSON and player inventory/appearance |
| PvP equipment/furniture/weapons | James / SethyPagna | Per-item folders beneath existing `ArtSource/WordboundPvP` and `Assets/_Project/Art/Collections/WordboundPvP`; separate BELT/audio definitions/profiles and EquipmentStudio views | ModelLibrary/MaterialLibrary/WordboundPvP catalogue, global items.json, handling/authority/combat hooks and public catalogue admission |
| Puzzle mode direction / Wordbound suite | James / SethyPagna | New mode/content files under existing Rules/Wordbound and Online/Puzzles; course-local data/presentation assets and separate authoring scenes | ModeDefinition, canonical schema/hash/versioning, launcher selection and approved public courses |
| Workshop/map generation | Work package, no new human named | Course-specific authoring folders, independent tools/components and focused tests using existing creator contracts | Shared editor document/undo contracts, generic inspector/launcher source binding and global editor menus |
| Online services | Work package, no new human named | Existing OnlineServices and specific Online client/party/transport components/tests | Shared match/protocol/state definitions, SDK/packages, launcher/player lifecycle, services deployment and public matchmaking admission |

Assistant agents/worktrees are implementation lanes serving James's scope. Names such as map-core, puzzle-race and online must not be presented as additional human teammates or reassignment of ICY/UI ownership.

## Actual conflict exposure

ICY's one commit changes42 paths that also differ between James-v1 and the pinned integration, including core/session, input, lobby, wardrobe/materials, rules, HUD and package files. This is **future shared-edit exposure**, not an unresolved merge: ICYfb94c9a is already an ancestor. A new feature file reduces line overlap only if its registration and shared data changes stay small and are integrated once.

Protect these single-writer integration points:

- `Assets/_Project/Scenes/{Hub,LivingRoom,Tutorial,MovingDay,WordboundLobby,WordboundGameplay}.unity`, their scene metadata and `ProjectSettings/EditorBuildSettings.asset`.
- `Assets/_Project/Resources/Wordbound/LaunchAssets.asset`, global GameData/ModelLibrary/MaterialLibrary and approved public catalogues.
- `Assets/_Project/Data/Config/{items,rules,wardrobe}.json`; package manifests/locks and assembly contracts.
- Shared player/input/session, lobby navigation, protocol/state/hash and authoritative interaction implementations.

Preserve existing file paths/Unity GUIDs. New feature-owned assets should have unique feature names, Unity-generated `.meta` files and copied profiles/materials when edits must stay local. Do not relocate original Resources/imported meshes/fonts to make folders look tidier. Commit each asset with its `.meta`; folder discovery/navigation can provide organization without reference-breaking moves.

Current read-only tracked-status check also found:

- Original `wreckabulary` checkout: modified `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`.
- `wordbound-online`: modified `Assets/_Project/Scripts/Rules/Wordbound/ModeDefinition.cs`.
- The owner's font/Lunar preservation instruction still applies. This tracked-only inspection does not exhaust other checkouts' untracked/ignored contents.

## Historical worktrees require slice review

| Local branch / tip | Already equivalent non-merge patches | Other non-merge patches | Practical treatment |
| --- | --- | --- | --- |
| map-core /0c30a8a8 |3 |0 | Existing branch-only patches already represented; do not remerge solely by branch name. |
| pvp-assets /6827c50d |1 |0 | Same. |
| workshop-overlay-cleanup /710dc10a |1 |0 | Same. |
| windows-qa-delivery /4646fd9f |4 |0 | Same. |
| map-workshop /0a5dd5d3 |89 |11 | Review exact remaining slices and exclusions. |
| hud-readability /ee5dcd3f |78 |4 | Review exact remaining slices and exclusions. |
| puzzle-race /bcd8a69e |213 |47 | Review exact remaining slices and exclusions. |
| online /96a33479 |182 |75 | Review exact remaining slices and exclusions. |

Method: `git cherry 036f352b <branch>`. Positive/not-equivalent counts do not mean missing features: squashed, strengthened, abandoned, baseline-only and differently integrated commits can remain. Merge commits are excluded by this comparison. Never apply every positive commit automatically. Existing `BuildNotes/online/SYNCHRONIZE-TEAM-COPY.md` explicitly excludes baseline-only7d5d35a9.

There is a concrete label mismatch: `wordbound-map-workshop` currently checks out `sethypagna/hud-readability` at ee5dcd3f, while the local `sethypagna/map-workshop` ref points elsewhere at0a5dd5d3. Label handoffs by actual branch + full SHA + owned paths, not the folder's old name.

## Scenes: separate editing, explicit connection

Retain the six saved runtime scene entries and their existing navigation/launcher connection. Use separate saved development scenes for UI, CharacterStudio, EquipmentStudio, HouseholdStudio and each course/feature; keep their source course, copied LaunchAssets and visual/camera profiles together. `WordboundGameplay` remains the common runtime loader for the explicitly selected content rather than embedding every authoring environment into one giant scene.

The paused Kitchen setup is the correct additive pattern: course-local `FlagshipKitchenV1/{Course,Presentation,LaunchAssets}.asset` plus `KitchenAuthoring.unity`, changing only the copied scene's launcher reference. Those assets were still uncreated at this audit's start. Do not advertise that source code alone as a usable saved scene. Validate creation, reference wiring, reopen/Play/Stop/return and repeated setup/manual-edit preservation before claiming that entry works.

Each team handoff should supply: scene path, entry button/menu, course/profile/prefab dependencies, normal start, return route, exact source SHA, test evidence and any public-content compatibility limit. Design helpers/previews must not enter the shipped build list by accident. Public matchmaking must bind approved versioned content; a local authoring scene is not automatically a multiplayer map.

`.gitattributes` selects `merge=unityyamlmerge` for Unity YAML, but this checkout returned no `merge.unityyamlmerge.driver` or merge-tool configuration. Root can configure the verified local UnityYAMLMerge executable as a per-device Git driver; each teammate needs its own valid executable path. Smart Merge assists structural conflicts but does not replace testing saved references/behavior.

## Recommended integration sequence

1. Preserve the existing dirty/untracked snapshots and pinned heads. Keep the paused Kitchen source/data separate from verified commits; never blanket-stage all drafts or rewrite team branches.
2. Record this actual owner/branch/path matrix in the main team guide. One integrator serializes shared scenes/catalogues/packages/registrations. Choose future feature slices from the reviewed current integration SHA, not an old worktree tip.
3. Treat known upstream James/ICY revisions as already included. Fetch before each review and inspect only new commits since the recorded accepted tip; preserve author identity.
4. For historical assistant branches, use exact reviewed commit/path manifests and patch-equivalence checks. Exclude baseline-only/failed/abandoned slices; validate one coherent slice at a time before a commit.
5. Prefer new item/course/profile/prefab/component files with one small integration registration commit. Let Unity create metadata and save scenes. Test each dedicated entry and the shared runtime connection, then the affected original/ICY workflows.
6. Publish a clearly named integration review branch/PR only with the intended code/assets, metadata and relevant evidence; verify LFS objects and SDK restoration instructions. The current unpublished950-commit gap needs an explicit reviewable delivery, not editing PR11/14 to imply it contains these changes. Never mark a prototype/partly tested course production-approved.

Sources read: `CLAUDE.md`, `Docs/kit/HARNESS_RULES.md`, `docs/wordbound/PLAN.md`, `Docs/MAP.md`, `docs/wordbound/DEVELOPER-WORKSPACE-PLAN.md`, `.gitattributes`, the synchronize exclusion report and exact refs above. No gameplay/native verification or public mutation was performed by this lane.

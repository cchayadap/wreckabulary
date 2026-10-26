# Try the Windows QA candidate

**DRAFT — fresh build and smoke results pending.** This is a review candidate, not a completed production release. Root must replace every `<PLACEHOLDER>` below with verified evidence before delivery. Keep historical build folders and receipts intact.

Executable: `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Builds/qa-<BUILD_ID>/Wreckabulary.exe`. Keep the whole folder together, including `Wreckabulary_Data` and its DLLs. Built with Unity **6000.6.4f1**.

| Delivery check | Verified result to insert |
| --- | --- |
| Exact build / source / dirty-tree fingerprint | `<BUILD_ID>` / `<HEAD>` / `<TREE_FINGERPRINT>` |
| Build gate and executable hash | `<BUILD_RESULT_AND_RECEIPT>` / `<EXE_SHA256>` |
| Home smoke — 3 steps | `<FRONTEND_RUN_ID_AND_RESULT>` |
| Workshop smoke — 5 steps | `<WORKSHOP_RUN_ID_AND_RESULT>` |
| Original equipment and BAT motion — 6 steps | `<EQUIPMENT_RUN_ID_AND_RESULT>` |
| Actual Windows Home / Shop / Loadout and save/reopen check | `<MANUAL_RESULT_AND_CAPTURE_PATH>` |

For each smoke, verify the same build ID, all required steps, normal exit, no timeout, clean logs and actual current screenshots. An older passing build is not evidence for this candidate.

## Start here

1. **Home → Loadout / Shop**: try the original character, outfit colours, accessories and item finishes. Purchases use earned coins and do not change gameplay stats. Equipped items and purchases use your real local save.
2. **Home → Play → Practice**: try the original house modes locally. The separate **Wordbound** button opens its online lobby and map workshop.
3. **Wordbound → Workshop**: choose **Time Run** or **Freestyle**, edit, **Preview**, then **Playtest**. No online connection is needed for this local test. Use **E** to pick up, **Q** to craft, left mouse to smash and **Esc → Return to workshop** to continue editing.
4. **Wordbound online**: connect with a guest name, select matching mode/region/content settings, create or join a party, mark ready and use **Quick Play**. Public search needs compatible human players; it must not silently substitute bots or solo play. Use party invites for friends.

## What is online

| Game family | Current scope |
| --- | --- |
| Wordbound: Time Run, Team Race, Brawl Race, Free-for-All, Freestyle | Public/private online routes exist. Bounded two-client public entry/movement/command checks have passed for these five modes; FFA also has an eight-player entry check. Full matches, all party formats, recovery, capacity retries and separate Windows clients remain incomplete. |
| Original houses: Dibs!, Double trouble, The great escape, Moving day, The Escape! | **Local multiplayer only.** Their original rounds, revive, van, checklist and door/key rules are separate from Wordbound. Their online bridge and public matchmaking remain unfinished. |
| Original Tutorial / Home Tour | Local solo experiences. The original shared Hub is also local; an online party roster does not replicate that 3D house. |

The request to make **every** multiplayer/team mode work online is therefore still open. The [mode scope audit](all-modes-online-scope.md) records the actual runtime and service boundaries; [progress](../../progress.md) keeps the remaining tasks.

## The Lost Address

The roommates follow a misrouted keepsake through **Packing Yard → Kitchen Works → Attic Stacks → Rooftop Garden**. Broken delivery directions send them back down before they repair the route and bring it home. Smashing furniture supplies letters; spelling creates tools for physical puzzles.

Three chapters, 14 clues, an optional journal, editable story copy and **six outcome epilogues are implemented** in the current source. Their native story/presentation tests pass; this candidate still needs its own build evidence. FFA does not use the campaign, and Freestyle has no timer, winner or campaign ending. Optional restoration objectives and the richer full-restoration ending remain design work. The [story and experience document](../../docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md) separates those future additions from the implemented outcomes.

## Edit without code in Unity

Open the isolated `wordbound-integration` checkout with `C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe`, then **Wreckabulary → Open Current Workspace**. Work outside Play, save the actual prefab/profile/scene, reopen it, then use **Play Lobby**, **Play Shop** or **Play Loadout**. These previews use real player saves; stopping Play does not undo purchases. Do not open a second Editor on this project during an automated Unity job.

Paths below are under `Assets/_Project/`:

| Edit | Saved asset |
| --- | --- |
| Lobby scene / Home dock and turn hint | `Scenes/Hub.unity`; `Prefabs/UI/Home/HomePage.prefab`; `Resources/UI/LobbyHomeLayout.asset` |
| Shop panel and offer cards | `Prefabs/UI/Shop/ShopPage.prefab`; `ShopOfferCard.prefab`; `Resources/UI/LobbyShopLayout.asset` |
| Loadout panel and recipe cards | `Prefabs/UI/Loadout/LoadoutPage.prefab`; `RecipeTile.prefab`; `Resources/UI/LobbyLoadoutLayout.asset` |
| Lobby camera / typography | `Resources/UI/LobbyCamera.asset`; `LobbyTypography.asset`; saved fonts in `UI/Fonts/` and presets in `UI/Materials/` |
| Equipment / character / furniture review scenes | `Scenes/Authoring/EquipmentStudio.unity`; `CharacterStudio.unity`; `HouseholdStudio.unity` |
| Reusable equipment / household furniture | `Prefabs/Equipment/Weapons/`; `Prefabs/Equipment/Tools/`; `Prefabs/Furniture/`; household variants in `Prefabs/Furniture/HouseholdExpansionV1/` |
| Wordbound course, camera, art, interface and story | `Data/Wordbound/Authoring/{FlagshipCourse,FreestyleCourse,PlayerCamera,Presentation,Interface,LostAddressStory}.asset`; assigned through `Resources/Wordbound/LaunchAssets.asset` |

Move/resize saved panel elements in Prefab Mode. Use the Inspector for camera values, grid spacing, fonts, materials, seed and placement ranges. In course forms, choose **Apply form edits and save asset**, then validate and play the exact preview. Studio placement changes that review scene; a prefab/profile must be assigned to gameplay before it changes a playable map. Shared-material edits affect every user of that material; duplicate and assign for a local variation.

Some wearable pickers, recipe details, Shop Gear/Extras and HUD elements are still generated. Moving temporary Play-mode UI does not create a saved design. Full instructions and exact labels: [lobby editing](../../docs/wordbound/LOBBY-EDITING-GUIDE.md), [workspace and folders](../../docs/UNITY-WORKSPACE.md).

## Still unfinished

- **Art:** the original models remain the reference. The saved head-surface correction preserves the original FBX, rig and materials; current native pose/expression checks pass. The six native RGBA portraits and actual Shop capture now pass their bounded checks: cream rectangles are gone and the original outfit colours are readable. [Portrait evidence](../developer-workspace/shop-portraits-corrected/production-acceptance-20261026.md). This Windows presentation still needs its own verification. Fully painted skins, all grips/animations, detailed destination kits and owner visual acceptance remain open.
- **Furniture:** eight saved Household props are editable in their studio. Seven lack colliders; the parcel has a static box only. They are not registered into playable maps, and the supplied packs do not provide character rigs or animation clips. Their flat-colour finish is not the final requested polish. FAN V3 remains an experimental construction candidate; the first build failed positive-tilt overlap checks and is not accepted game content.
- **Performance:** 60 FPS is not established. Earlier Windows observations contain substantial update stalls; those timings are not presented frame rate. Other heavy PC jobs remain running at James's request, so comparisons need matching conditions.
- **Online and persistence:** complete five-mode Wordbound matches, remaining formats/capacity/recovery checks, separate Windows clients and all five original-house online modes remain open. Verify Workshop save/close/reopen recovery in this Windows process separately from in-memory/editor tests.
- **Release:** a stripped release, ordinary editor interaction, broader accessibility/usability checks and final production acceptance remain separate work. This note must not mark the overall plan complete.

Integrator evidence to attach: [recent avatar/menu native checks](../asset-expansion/avatar-integrated-20261026.json), [Household preservation](../asset-expansion/household-native-20261026.json), [static menu review](menu-visual-review.md), [Windows readiness checklist](../asset-expansion/windows-readiness-20261026.md). These support scoped prior results; fill the fresh build/smoke placeholders above before handing over this candidate.

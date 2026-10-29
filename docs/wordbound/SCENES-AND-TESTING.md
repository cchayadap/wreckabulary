# Open, edit and try the game in Unity

Use the `wordbound-integration` project with Unity 6000.6.4f1. Separate page prefabs, studios and course entries keep design work easy to find without putting all authored content in one scene. All four entry scenes are now saved. Creation, repeat, reference and navigation checks pass; exact saved-output verification is recorded in [the organization report](../../BuildNotes/team-organization-20261029/ROOT.md).

## Lobby, controls and customization

Open **Wreckabulary → Open Current Workspace**. Its direct Play buttons preview Home, Shop, Loadout, Play choices or Settings in the actual Hub. Stop Play to return to the previous editing scene and entry. Save design changes in these prefabs rather than the temporary Play hierarchy:

| Area | Saved prefab |
| --- | --- |
| Home | `Assets/_Project/Prefabs/UI/Home/HomePage.prefab` |
| Play | `Assets/_Project/Prefabs/UI/Play/PlayPage.prefab` |
| Loadout | `Assets/_Project/Prefabs/UI/Loadout/LoadoutPage.prefab` |
| Shop | `Assets/_Project/Prefabs/UI/Shop/ShopPage.prefab` |
| Settings | `Assets/_Project/Prefabs/UI/Settings/SettingsPage.prefab` |

These are separate saved design surfaces. Some inner controls/cards are populated at runtime. The shared Hub owns the environment and navigation. Use the Workspace Design controls for the saved layout, fonts, material profiles and camera settings; assign a local variant when a change should affect only one feature.

## Map and Workshop entries

Choose **Wreckabulary → Course entries → Open … for Play**, then press Unity **Play** and choose **Workshop**. These commands explicitly enable **Play Opened Scene** so the default Hub entry does not redirect your test. They ask you to save pending scene changes before switching. To return to the default Hub startup, disable **Wreckabulary → Play Opened Scene**.

| Entry command | Saved scene | Assigned course |
| --- | --- | --- |
| Open Flagship for Play | `Assets/_Project/Scenes/Authoring/Courses/Flagship/Workshop.unity` | `Assets/_Project/Data/Wordbound/Authoring/FlagshipCourse.asset` |
| Open Freestyle for Play | `Assets/_Project/Scenes/Authoring/Courses/Freestyle/Workshop.unity` | `Assets/_Project/Data/Wordbound/Authoring/FreestyleCourse.asset` |
| Open Parcel Service for Play | `Assets/_Project/Scenes/Authoring/Courses/ParcelService/Workshop.unity` | `Assets/_Project/Resources/Wordbound/LocalCourses/ParcelServiceV1.asset` |
| Open Kitchen pilot for Play | `Assets/_Project/Data/Wordbound/Authoring/FlagshipKitchenV1/KitchenAuthoring.unity` | `Course.asset` in that same folder |

Each of the first three scenes has its own adjacent `LaunchAssets.asset`. Kitchen has its own Course, Presentation and LaunchAssets assets. These small scenes bind the saved source explicitly. The original WordboundLobby, global Resources settings and release build list are preserved.

If restoring a fresh checkout, use **Tools → Wordbound → Courses → Create missing course entry scenes**, and **Create or select Kitchen pilot** for Kitchen. Repeats verify existing valid bindings and preserve saved camera/launch/course edits. Partial, dirty or conflicting output is reported with the files retained.

The connection is:

```text
Saved course entry scene
  → its own LaunchAssets
  → assigned Course + item/rule/geometry/presentation sources
  → Workshop editing
  → Playtest: shared WordboundGameplay scene + exact resolved course
  → Return to workshop: same draft/history
```

`WordboundGameplay.unity` is the shared lighting/gameplay shell. Maps are saved course documents with editable geometry/placement/seed fields and resolved into that shell. An empty-looking gameplay shell in Edit mode is not a missing baked map. The normal large-course start stays in Packing.

## Saved editing and changes made during Play

Select the assigned Course asset to change modules, spawns, random zones, seed and supported authoring fields. Use **Validate and preview exact seed**, choose a room in **View**, then **Frame**. Orbit/zoom to inspect it and use **Play this exact preview locally**. Editor framing does not move the live player's start.

For drag/move/rotate editing, use the entry's Workshop. Use its explicit save/reopen/share controls. Workshop drafts do not automatically overwrite the Inspector Course asset, and transient Play-mode changes are not saved scene edits. Keep the intended source and its installed catalogue together when sharing/testing. A local authoring entry does not add the course to public matchmaking.

## Separate asset studios and original modes

- `Assets/_Project/Scenes/Authoring/EquipmentStudio.unity`: held-item/equipment prefab review.
- `Assets/_Project/Scenes/Authoring/CharacterStudio.unity`: character and outfit review.
- `Assets/_Project/Scenes/Authoring/HouseholdStudio.unity`: furniture/material review.
- `Hub`, `LivingRoom`, `MovingDay` and `Tutorial`: original game entries. ICY's Escape and wardrobe integrations retain their existing owners/flows.

Studios are design/review scenes. Assign an approved prefab/profile variant to the relevant presentation settings before expecting it in gameplay. Preserve original mesh/material GUIDs; duplicate a material/profile before making a feature-local change. Only the integrator changes shared runtime registration or build settings.

## Checks and limits

The dated report records actual setup, repeat/edit-preservation, reference and entry tests. Human Save/Cancel dialog behavior, keyboard/controller use, visual quality, complete route traversal, multiplayer and production release acceptance remain distinct checks. Isolated Kitchen furniture physics passes030350; full-map traversal/physics and richer artwork remain unfinished. The current Windows review build remains001001 until a new build is explicitly verified.

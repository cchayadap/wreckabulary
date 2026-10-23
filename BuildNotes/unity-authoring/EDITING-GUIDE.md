# Edit Wordbound in Unity

Source implementation is pending combined reference/native verification. This guide describes the intended editor workflow; it is not a record of user acceptance.

## Create a saved course

For this project's reusable defaults, run **Tools → Wordbound → Create or verify Inspector authoring assets** once. It creates `Assets/_Project/Data/Wordbound/Authoring/{PlayerCamera,Presentation,FreestyleCourse,FlagshipCourse}.asset` through Unity's AssetDatabase and fills empty CameraProfile, VisualProfile and WorkshopCourse references on the actual LaunchAssets asset. Existing assets and assignments are preserved. The launcher workshop uses the saved course copy; public matchmaking remains separately bound. The helper validates both saved course assets before assigning them.

1. In the Project window choose **Create → Wreckabulary → Wordbound → Freestyle course with defaults**, or **Flagship course copy** for the complete authored course. This creates a separate editable asset and preserves the built-in course. The existing item/rule TextAssets are assigned automatically.
2. Select the asset. Expand **Mode, seed, gameplay assets, geometry and presentation** to choose TimeRun/Freestyle for local play, the fixed 16-digit seed, profiles and catalogue dimensions. Dimensions and positions use millimetres; Vector3Int fields use X=width, Y=height, Z=depth.
3. Choose an **Edit section**. Lists show one selected record at a time. Add/remove records, change IDs and references, and **Apply form edits and save asset**. Saved course changes use Unity Undo. Unapplied form edits are kept locally under `Library/Wordbound/InspectorDrafts` and restored when you reopen the asset, including incomplete values that still need fixing. If the saved course changed elsewhere, choose **Restore pending edits into the form** or **Discard pending form and use saved course**. Invalid edits never overwrite saved data. A recovery-file error is shown and retains the current draft in memory; resolve it before closing Unity. Apply edits before deleting the Library folder or transferring the project.

The structured forms cover every current course field, including supported modes, modules, layers, connectors, spawns, recovery anchors, supplies, checkpoints, route alternatives and three-lap selections. Puzzle forms expose recipes, prerequisites, letter costs and recovery supplies. Physical-site forms expose sockets, arrival/receiver volumes, floor pads, hold/window timing and moving-fixture endpoints/periods/phases. Random zones expose fixed/random placement, min/max counts, chance in permille, spacing/grid, permitted layers/yaws and weighted asset pools. No JSON typing is required.

## Move rooms and configure puzzles

Use **Scene module** to select a room and **Frame module** to find it. Drag its Scene position handle to move it horizontally. **Rotate module 90°** and **Move to layer** carry its owned spawns, resources, zone bounds, connection endpoints and physical puzzle sites together through the same workshop transform implementation. Add/edit floor heights in Layers first. Direct form edits are precise but do not infer topology repairs; validation reports missing or inconsistent links.

Use an existing authored station as a reference when configuring another puzzle. The Inspector has **Add** and **Remove**, but no record-copy button: add a record, then enter its puzzle/checkpoint/recovery references and sensor positions in the form. Preserve valid per-lap IDs and Solo-solvable recipes. A physical site is gameplay data: cosmetic labels alone do not complete a puzzle. Smash requires the actual prop source, wind requires a receiver and recovery source, and CLOCK requires actual moving fixtures. Full validation refuses missing physical requirements.

## Preview and play the same content

Choose **Validate and preview exact seed**. It runs document/manifest validation, the selected mode, authored physical-content validation and entity-budget preflight, then renders the actual generated static geometry and imported prop artwork. Drag to orbit and scroll to zoom. It displays the exact source, manifest and gameplay-content hashes. Dynamic actors and mechanisms are verified in local play.

Choose **Play exact preview locally**. It pins the saved course, geometry, selected mode, seed, item bytes and rule bytes across the Play Mode reload; changing them requires a new preview. It uses the existing Single runner, real physics, commands and authored sensors. Existing registered Wordbound launch assets and gameplay scene are required. It does not modify Build Settings. The editor asks to save modified scenes, uses a temporary empty scene and restores the prior saved scene setup after returning. If scene restoration fails, the restoration data remains in SessionState; use **Tools → Wordbound → Restore authoring scene setup** in Edit Mode.

Course JSON import/export uses the existing bounded format. Custom local edits do not register a course for public Quick Play, which uses approved content identities.

For private multiplayer, open Wordbound's party screen, **Connect** with a guest name and leave any current party. Open **Workshop**, select a mode and team size supported by the map, then choose **Host online**. Validation shares the exact draft and resolved map in a private party. On the party screen, invite friends, mark ready, create a private room and start it when the players are ready. Private admission verifies that exact shared bundle; it does not approve the map for public Quick Play. Use **Back to party** if preparation needs a retry or an old party must be left.

## Camera, models and materials

Create a **Camera profile** and assign it to the course (or LaunchAssets for the regular launcher). Its grouped Inspector exposes FOV, clipping, distance, pivot, pitch limits, collision probe and look sensitivity. Distances are labelled in metres and pitch is edited in degrees; the Inspector handles the stored radians. Inline validation explains invalid combinations. Defaults match the existing camera. Runtime edits apply live after validation; an invalid edit keeps the last valid applied values.

Create a **Visual profile**, add a canonical visual key such as `STOOL`, drag a render-only prefab into Model, and optionally assign replacement materials by slot. Add a `Grip_R` child to author the hand contact. Root resolver integration fits artwork to the unchanged canonical body dimensions; presentation models cannot add colliders, rigidbodies, scripts, cameras, lights or audio. Player rigs and letter tiles keep their existing rendering path. The profile also exposes the canonical shield/plate raised-pose offsets and rotation via DefensePose. Model/material changes are used by a fresh preview/spawn; they are not a gameplay-stat editor.

Gameplay item and rule changes go through their explicit TextAssets and alter content identity. Geometry dimensions and puzzle parameters belong to the course/catalogue forms, not model transforms. Save the asset to retain Inspector changes.

## Acceptance still required

- [ ] Combined separate-assembly reference compile and focused tests.
- [ ] Native editor create/save/reload/Undo, module handles and complete profile validation.
- [ ] Exact preview and local play with scene restoration on success and failure.
- [ ] Human editing/play acceptance, including camera feel, grip placement and all six actual puzzle families.

Screenshots should be added only after root's native editor capture confirms these views. No generated mock screenshots are used as editing evidence.

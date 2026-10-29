# Next flagship art implementation contract

STATUS: COMPLETE — evidence-only implementation contract, not implemented or visually accepted. No production/native/paid-generation actions. This is next full-goal work after the current UI Windows checkpoint; it must not delay that checkpoint.

## Decision

Use a new, local, versioned copy of the **full flagship** with one composed Kitchen arrival area: one FRIDGE, two COUNTERs and one STOVE. Existing `CourseAssetDefinition` plus `Purpose="Props"`, `Placement="Fixed"` zones can give these models real authority-owned collision, pickup, drop, throwing and break-to-word behavior. They are movable, breakable furniture, not permanent architecture. The large-map objective remains the same 34 rooms, four destinations/layers, three laps and 72 physical sites.

**A geometry catalogue entry alone is insufficient.** Add three complete, disabled item definitions to a new course-local copy of the existing item JSON, and point the new saved CourseAsset at it. The current item JSON contains none of `FRIDGE`, `COUNTER`, `STOVE`. The authority's throw path calls `catalogue.Get(item.Word)`, which throws on an unknown word; a map can construct successfully through `PlaceFurniture` yet fail when its new furniture is thrown. The matching item definitions also provide the normal original-model binding. Do not substitute TABLE/BOX letters for a fridge or add renderer-only colliders.

If the intended design is an **anchored, indestructible** fitted kitchen, stop at this distinction: `Fixed` controls initial placement only. That alternative needs a new canonical obstacle contract, not a material/prefab flag. The smaller breakable-furniture pilot is compatible with Wreckabulary's existing word mechanics and can be implemented without that schema.

This qualifies the obstacle-only recommendation in `../shop/environment-options.md`: its warning about nonphysical visual dressing is correct, but an additional valid route exists when the appliances are deliberately real word-bearing furniture. This report does not alter that earlier evidence.

## What the player should encounter

On arriving at Kitchen Works, the tall fridge, contrasting blue cabinet fronts, ceramic tops, metal stove and retained gold fittings should identify a kitchen before any explanatory text. A compact work run occupies the north-west perimeter of `Kitchen_ServiceLanding`, leaving the central cross-route and recovery point open. The four objects use normal furniture interaction and visibly yield their own words when broken. No cooking, powered controls or animated appliance doors are implied; the original import records contain no such actions.

This creates one recognizable encounter within the existing large route. It does not fill every 24 m room, deliver the named giant teapot landmark, fix all sparse destinations or prove the full route finish. Do not enlarge household meshes arbitrarily to fill the room, repeat fake windows, add stripes, or disguise usable BOOK/LAMP models as inert dressing.

## Exact original assets and placement envelope

All three originals are registered in `Assets/_Project/Resources/ModelLibrary.asset` and are under `Assets/_Project/Art/Imported/Environment/`. Dimensions below are X/Y/Z from the existing generated export report, **not a new native measurement**.

| Word / model key | Recorded dimensions (m) | Original identity to preserve |
| --- | --- | --- |
| FRIDGE / `Environment/Kitchen_Fridge` | .800 / 1.820 / .799 | Tall blue/ceramic body, gold hardware and small green/ivory/red details |
| COUNTER / `Environment/Kitchen_Counter` | 1.270 / .925 / .737 | Blue cabinet, ceramic worktop, gold fittings; two instances |
| STOVE / `Environment/Kitchen_Stove` | .820 / .942 / .7795 | Dark/metal cooking surface, ivory body, gold detail; one instance |

Exact source SHA256s and material names are recorded in `../shop/environment-options.md`: Fridge `76270646a81ee62aac47823f8cc6de8b6788c03ab3e1b722cd0fff415dac6274`, Counter `928db9ffd492a4edd95ab02131197bd74c17d053e0099728920429ec7bd67285`, Stove `7197cc46158d44a8fc1428690beb39ec706116ebc05fafd0112b3be280baab00`. Recheck them during setup. The original-vault provenance is recorded, but no new external licence conclusion is made. The separate Kenney CC0 notice does not license these originals.

The room centre is world **(-36, 8, -12) m**, with 24×24×6 m bounds. Proposed local envelope is x **-9.5…-4.0 m**, z **10.5…11.65 m**. Starting centres along the north wall: fridge x-8.9, counter x-7.65, stove x-6.40, counter x-5.15, z approximately11.1, y0. These are draft placements. Native inspection must establish actual front, bounds, pivot and floor contact, then round canonical sizes to millimetres consistently. Do not infer that yaw0 faces the room or that FBX pivot equals floor; the counter report starts at y.01.

This pocket is separate from the source-defined central ±1.8 m cross corridors, centre recovery, axis connection approaches and north-east random-prop zone at local x/z5…10. Service Landing is arrival room0, so its six puzzle-room sites/supplies do not occupy this pocket. Nevertheless resolver reservations and actual forward/reverse actor-camera clearance must pass before accepting the placements. Keep a clear standing lane south of the appliances and small visible gaps, rather than overlapping canonical boxes into an apparent fitted unit.

Start with only these four floor props: approximately15,900 original triangles before submesh/material cost. Sink remains excluded until support/recess alignment is inspected. One original `Wall_Shelf` and two `Wall_Sconce` fixtures may follow through the existing validated high-wall dressing API, with real wall support and no extra realtime lights. They are optional and must not substitute for the four physical floor objects.

## Existing contract traced end to end

| Source | Relevant existing behavior / consequence |
| --- | --- |
| `Scripts/Rules/Wordbound/CourseCatalogue.cs` | Asset ID, A–Z Letters, dimensions and CanCraft enter the geometry hash. No immovable/indestructible field exists. |
| `Scripts/Rules/Wordbound/CourseDocument.cs`, `CourseResolver.cs` | Fixed zones choose an initial pose. The resolver checks canonical rotated boxes, room/zone containment, reserved volumes, recovery/spawns, supply/connection clearance and other placements. Set min/max1, chance1000 and a one-entry pool for each appliance. |
| `Scripts/Rules/Wordbound/AuthoritativeMatch.cs:154`, `Scripts/Rules/Economy.cs:99` | Non-resource placements call `PlaceFurniture(Letters)` and create actual Item bodies. Map toughness is rule-derived, not crafted-item durability; minted letters equal the word length. |
| `Scripts/Rules/Wordbound/AuthoritativeMatch.cs:431`, `AuthoritativeThrownContacts.cs:28` | Throw/throw-state validation look up the item word. New geometry-only words are unsafe. Add course-local item definitions before exposing pickup/throw. |
| `Scripts/Online/Gameplay/WordboundGameplayPlan.cs`, `WordboundGameplayBody.cs` | Map visual identity/size comes from the placement/geometry; the gameplay body creates its root box collider with floor-centred height. Imported model colliders are not the authority. |
| `Scripts/Online/Presentation/WordboundVisualResolver.cs`, `Scripts/Art/ModelVisual.cs` | An item definition's Environment model key uses normal Spawn, source material/skin handling and tactile materials; fitting grounds/resizes the rendered mesh to canonical bounds. Keep this route, rather than whole-model override prefabs that bypass it. |
| `Scripts/Online/Authoring/WordboundCourseAsset.cs`, `Scripts/Online/Puzzles/WordboundPuzzleContent.cs` | Saved course owns item/rule references, seed/mode/format, editable geometry and presentation. Exact content identity binds document, geometry, item source, plan and effective rules. Validation/budget must use the new item source. |
| `Scripts/Online/Client/WordboundLauncherWorkshopSource.cs` | Assigned Workshop sources capture their own Items/Rules/geometry/visual profile. Actual Workshop visuals and local playtest therefore can use the augmented catalogue without a global replacement. |
| `Scripts/Online/Authoring/Editor/WordboundCoursePreview.cs`, `WordboundCoursePlaytest.cs` | Existing saved-course preview and exact local play use the same content. Play starts normally in Packing; a static Kitchen view is not proof that the route reaches it. |

`CanCraft=false` prevents selection as a recipe in the corrected Workshop picker; `enabled:false` prevents real crafting even if requested directly. Neither flag makes the furniture inert. Provide complete normal item definitions with valid letter counts, size, original model key, Classic skin and suitable carry/grip values even though disabled-item validation is less strict. Add no new Mode.Recipes, deploy ability, appliance animation or shop claim.

Four props mint **25 extra letters per resolved ownership copy**:6+7+5+7. With the current default map toughness rule1+.6×length, these have4.6/5.2/4.0/5.2 toughness; against current HAMMER break4.5 this is2/2/1/2 successful hits. Tests must derive values from the selected rules, not hard-code an art item's crafted durability. Extra resources can change puzzle-solving choices and must be considered as a balance change, not merely decoration. `CourseResolver.TeamScoped` duplicates Props for TeamIsolated ownership; TeamProtected duplicates Resources only. Count the actual resolved placements for each supported mode/format rather than assuming one copy in every mode.

## Smallest implementation and editable delivery

Proposed additive paths, subject to root's normal integration review:

1. `Assets/_Project/Scripts/Rules/Wordbound/WordboundKitchenPilotContent.cs`: a factory taking an exact valid ItemCatalogue, cloning the full current flagship document/catalogue, giving it a distinct versioned ID such as `wordbound-lost-address-kitchen-v1`, and adding only the three geometry definitions/four Fixed Props zones. Preserve modules, layers, all72sites, laps, connections, reserved volumes, recovery, ordinary starts and existing source zones. Record original source hashes; do not edit `WordboundFlagshipContent` or its public identity.
2. `Assets/_Project/Data/Wordbound/Authoring/FlagshipKitchenV1/Items.json`: deterministic copy of the approved item source plus the three disabled definitions. Preserve every original definition. No edit to global `Data/Config/items.json`, ModelLibrary, imported originals or source materials.
3. `Assets/_Project/Data/Wordbound/Authoring/FlagshipKitchenV1/Course.asset`: saved `WordboundCourseAsset` populated through Unity APIs, own Items reference, borrowed current RuleBook/camera, explicit TimeRun/Solo first, fixed seed, and the complete editable course. A separate copied Presentation.asset is needed only for later isolated visual edits; initial original materials require no remap. Keep the original flagship/Parcel assets untouched.
4. `Assets/_Project/Scripts/Online/Authoring/Editor/WordboundKitchenPilotSetup.cs`: bounded Create-or-Select and Inspect Sources actions. Reject dirty dependencies/occupied raw output paths; create only fresh paths via AssetDatabase; refuse partial conflicting outputs. Repeat selects/verifies existing authored assets without regenerating over manual edits. Record source/input/output hashes and original preservation; if a future migration modifies a saved asset, exact backup precedes changes.
5. Focused Rules/authoring/native tests under existing test assemblies. Reuse current exact-course preview/playtest and native first-leg helpers; do not introduce another renderer, network launcher or capture framework.

James's first no-code path is: select the new Course.asset → edit its normal saved catalogue/document fields and seed → **Validate and preview exact seed** → **Play exact preview locally**. For the existing drag/move/rotate Workshop, assign this asset to **a copied** `WordboundLaunchAssets.WorkshopCourse` in a separately saved authoring scene, using the existing assigned-course branch. Setup must preserve the normal LaunchAssets and lobby scene. This is a local authoring entry, not automatic exposure in public matchmaking. If scene wiring is deferred, report that limitation plainly; saving a Course.asset alone does not add a new lobby map card or replace Lost Address's factory-backed Solo entry.

The current full-course preview frames the whole map. A small selected-module framing action or the existing capture tooling can supply a matched Kitchen view; do not silently move the live player/start point to manufacture a successful route screenshot. Save reviewed camera settings if this is added. Workshop save/reopen/share must retain the course data; its bundle expects a matching installed catalogue and does not bundle arbitrary meshes/item definitions into a self-contained public map download.

## Gates before implementation is accepted

- **Source/data:** deterministic seed resolution, clone immutability and unchanged original/public hashes; all34rooms/72sites/three laps/four layers retained; exactly4new fixed placements and3matching disabled definitions; no crafting admission; all model keys resolve and canonical dimensions agree with measured native bounds. Include the negative case of missing FRIDGE/COUNTER/STOVE definitions in the pilot factory/setup.
- **Geometry:** validate the pocket against actual reserved paths, connection corridors, recovery, spawns, site bounds and the existing random zone across representative seeds and every claimed mode/format. Native floor/contact, facing, forward/reverse route, carried-item and camera clearance must pass. An overlapping fixed spawn is a failure, not a reason to skip resolver validation.
- **Authority/resources:** real spawn, carry, Drop and Throw for each new word; snapshot round-trip while world/carried/thrown; genuine hammer damage and exact word-letter output; no unknown-recipe exception. Run actual 512-body/8192-state budget and optional-resource validation with the increased placements/letters and maximum relevant teams. Keep existing recovery letters and mandatory physical recipe gates unchanged.
- **Editor preservation:** native Create/repeat/manual-edit/reopen checks, source bytes unchanged, separate assigned-source history, and Workshop drag/rotate/undo/redo/save/reopen/export/import using the same installed catalogue. Borrowed meshes/materials remain unchanged; transient previews are cleaned up through existing ownership paths.
- **Visible result:** compare identical seed/camera before/after at forward arrival, reverse arrival and closer working height. Require recognizable kitchen composition, original multi-material detail/hardware, grounded feet/contact shadows, correct fronts and no floating/embedded appliances. The central route/puzzle focus must remain readable; a thin distant row against an otherwise blank24m wall is only a limited gain, not full art acceptance. Review actual runtime, Workshop and authored preview independently, and measure incremental draws/frame cost.
- **Gameplay scope:** rerun the existing first-leg native proof on the new full-course variant as an unchanged-start regression, then extend normal motor traversal to the Kitchen encounter. A separate Kitchen-only physics test may accelerate item diagnosis but must be labelled an isolated encounter test, not full-route completion. The original231532 first-leg result remains valid only for its original inputs and three-room/two-puzzle scope.

## Multiplayer and alternative fixed architecture

This is a LOCAL content version; no change to approved public course lists, Photon/Cloudflare admission, public factory IDs or public catalogue is required or authorized by this contract. The variant intentionally changes document/geometry/item/content hashes. Any later private/public multiplayer rollout must carry the same item/model assets on every client and revalidate server admission, course/content mismatch rejection, team ownership and duplicate-body budgets, pickup/drop/throw/break replication, recovery, reconnect and host migration. Local rendering success cannot certify that release.

If root instead chooses permanently anchored kitchen fixtures, add an explicit canonical obstacle list with ID/model, pose, bounds and physical policy; include it in course serialization/hash/validation, authoritative world creation and collision snapshots, Workshop editing/selection/import/export and all preview paths. It must reserve and validate route clearance and obey network/budget rules. That is a separate schema feature and wider test surface. Adding colliders to a visual prefab or setting a rendered object static cannot supply these guarantees.

No implementation or new native result is claimed here. The immediate implementable choice is the four genuine breakable original kitchen props in a separately saved full-map variant, with course-local item definitions and the gates above.

# Parcel Service V1 — bounded composition plan

STATUS: PROPOSED FOR ROOT REVIEW. Plan only; no implementation or native execution. Current Lost Address, Parcel Hall V2, public geometry/admission catalogues, item flags, recipe lists and online contracts remain unchanged.

## Outcome and scope

Create `wordbound-parcel-service-v1`, a separate saved LOCAL course named **Parcel Service**. It contains three compact connected rooms using original Wreckabulary models. Support the existing TimeRun and Freestyle modes through the current schema2 course/puzzle/runtime paths. This is a small real destination, not a replacement thumbnail or a new gameplay framework.

The first slice uses original object materials and real map-origin props. It does not implement chest opening/storage, desk drawers, shelf inventory, lamp switching or six new crafting recipes. LAMP is a visible/carryable/breakable original prop, not a new light source. Static initial furniture stays on the floor; stacking or placing lamps on desks would require a separately validated support/physics design.

## Proposed physical layout (metres)

All rooms share one ground layer and a 3.6m ceiling, with wood floors and the now-integrated restrained route woodwork. No overhead ramp. No decorative stripes or repeated sofa/table fill.

| Module | Definition X × Z × Y | Centre X/Z | Interior purpose |
|---|---|---|---|
| Service | 8 × 8 × 3.6 | 0 / 0 | Two original DESK counters, rear SHELF storage, one CHEST and one LAMP. |
| Sorting | 10 × 8 × 3.6 | 9 / 0 | One TABLE sorting bench, parcel BOX/CRATE groups and two SHELF racks. |
| Dispatch | 6 × 8 × 3.6 | 17 / 0 | One DESK dispatch counter, two CHEST parcels, two BOX props and one LAMP. |

Adjacent floor boundaries are x=4 and x=14. `ServiceSorting` and `SortingDispatch` are existing Door-kind, bidirectional 2.5m-wide connections at (4,0,0) and (14,0,0). Existing canonical walls create those real apertures. The new geometry catalogue contains only these three module definitions plus actual gameplay asset dimensions. Total room area is192m² across three rooms, versus576m² for one current flagship room.

Reserve an unobstructed east-west route through both door centres, z∈[-1.25,+1.25], floor to2.5m; reserve additional checkpoint, source, recipe footprint and player approach volumes. Keep 0.1m wall thickness clearance plus at least0.25m extra gap for placed furniture. Use quarter-turn-aware bounds for each zone. All proposed positions below must pass the exact resolver and native collision tests before freezing data.

Eight distinct starts in Service preserve existing Freestyle capacity validation: x∈{-2.4,-0.8,0.8,2.4}, z∈{-2.6,-1.2}, y=0. Only one actor is admitted by the local launcher. Starting keepsake/recovery anchor at (0,0,+0.4), radius0.6m; it must clear all spawn and prop bodies. Sorting recovery at local(0,0,-1.5), Dispatch at local(0,0,-1.3), radius0.6m. Test start separation, recovery capsule/keepsake clearance, and entering either direction.

### Initial prop composition

Coordinates are local X/Z inside each module. This is23 real map props:20 fixed landmarks/fixtures plus3 BOX parcels in one bounded random storage bay. Same seed reproduces all placements. Randomness changes optional parcel positions, never required routes, stations or recovery supplies.

| Module | Objects and local centres | Count |
|---|---|---|
| Service | DESK(-1.2,2.4),(1.2,2.4); SHELF(-2.9,3.2),(2.9,3.2); LAMP(-3,1.4); CHEST(3,2.5) | 6 |
| Sorting | TABLE(0,2.1); SHELF(-3.5,3.3),(3.5,3.3); CRATE(-3.5,2.4),(-2.3,2.4),(-3.5,-2.8),(3.5,-2.8); fixed BOX(3.5,2.4), plus3 BOX parcels in the south storage bay | 11 |
| Dispatch | DESK(0,2.4); CHEST(-1.6,2.4),(1.6,2.4); BOX(-1.8,-2.6),(1.8,-2.6); LAMP(2.1,1.4) | 6 |

The south storage bay is one Random Props zone, local X[-2.9,+2.9], Z[-3.5,-2.1], Y=0, Grid250mm, Spacing250mm, MinCount=MaxCount=3, Chance1000, Pool={BOX:1}, yaw0/180. Bounds enclose whole box footprints; do not interpret them as centre-only limits. Existing 64-attempt sampling must resolve every tested seed; widen/reduce density if it fails rather than changing global sampling rules. Fixed door corridors include the existing additional0.7m approach margin (2.5m door gives1.95m reserved radius), which is why near-door storage is at z≥2.4m.

Facing must be verified from original imported model fronts; yaw0/180 for counters/shelves and fixed quarter turns only. Do not stretch these objects to fill rooms. Use each original item Size rounded to mm with the project's AwayFromZero convention, then current canonical visual fitting. The layout is a proposal: adjust a fixture whose model front, support silhouette or collision box is unsuitable, rather than disguise it with scaling. Native composition may justify omitting a floor lamp if its original model reads as a table lamp.

## Existing gameplay, minimum new authored content

Story premise: reopen a small parcel office by rebuilding its sorting stations, returning a mislabelled delivery and finally dispatching the keepsake. The three-lap structure already exists; do not claim new endings or narrative systems.

Use existing PlaceObject puzzle behavior for the first slice, following `WorkshopTemplates.PlayableRaceCircuit`. No new physics preset. Sorting asks for a crafted TABLE at an open marked floor socket; outbound Dispatch asks for a crafted STOOL parcel stand; inbound Service asks for a crafted PLATE sorting tray. These remain normal approved recipes. Map-origin TABLE/other furniture cannot substitute for the crafted-object requirement.

- Lap1 (`FindAddress`): Service → Sorting(TABLE) → Dispatch(STOOL), finish with keepsake at Dispatch.
- Lap2 (`WrongDelivery`): Dispatch → Sorting(TABLE) → Service(PLATE), finish with keepsake at Service.
- Lap3 (`RestoreHome`): Service → Sorting(TABLE) → Dispatch(STOOL), final keepsake delivery.

Use separate Outbound/Inbound route sections. Each alternative lists each connection once; do not repeat a connection ID inside an alternative (the current schema rejects duplicate ID lists). Intermediate checkpoints bind their proper incoming connection and safe recovery anchor. Reuse puzzle IDs across different laps only through separate per-lap physical sites, matching the existing workshop circuit pattern. Freestyle resolves the same rooms and physical stations through the existing practice policy without timed lap progression.

Each puzzle has an explicit protected renewable recipe supply with correct prerequisite IDs; authored socket location must leave the actual recipe footprint and approach clear. A protected renewable HAMMER practice supply allows optional dismantling. The23 props are optional resource sources:3 DESK,4 SHELF,2 LAMP,3 CHEST,1 TABLE,4 CRATE,6 BOX give98 letters before recovery supplies. Assert this from the exact factory roster in tests. Required puzzle feasibility must never depend on these optional objects surviving. A full smash/carry/drop run must conserve the exact98 optional letters and remain within entity budgets.

Do not add a CollectLetters gate that reuses the same once-destroyed marked prop over all three laps: either make such a later design use separate per-lap source props, or retain these simple PlaceObject gates initially. No door lock is implied; route progress comes from actual existing ordered checkpoints and puzzle satisfaction.

## Exact compatibility trace

1. `WordboundFlagshipContent` admits12 enabled equipment IDs and derives its geometry from them. None of the six proposed parcel IDs belongs to that geometry. Do not change that factory or its hash.
2. `items.json` already declares original `Items/BOX`, `CRATE`, `CHEST`, `DESK`, `SHELF`, `LAMP` models and sizes. Five are disabled crafting entries; LAMP is enabled but outside Wordbound Mode.Recipes. Disabled crafting does not prevent map-origin props.
3. A new local CourseCatalogue may add those six definitions with `Letters=id` and `CanCraft=false`. Geometry dimensions, not visual prefab guesses, control their body size. Existing approved TABLE/STOOL/PLATE/HAMMER definitions are retained as craftable only where needed.
4. `AuthoritativeMatch` creates every non-Resources placement via `Economy.PlaceFurniture(asset.Letters)`. Map toughness comes from rule base/per-letter values. Carrying and breaking map props work independently of Enabled/Mode.Recipes. Breaking emits the object's word exactly once.
5. `WordboundPresentationContext` uses placement AssetId and geometry dimensions; `WordboundVisualResolver.ModelKey` resolves existing item models without filtering Enabled. The registered ItemBody remains a canonical BoxCollider. These are honest box collision proxies, not detailed per-leg/concave collisions.
6. Puzzle RecipeId additionally requires geometry CanCraft, an enabled Mode.Recipes item, matching required letters and appropriate actual deploy/tool behavior. Therefore none of the six parcel furniture words becomes a puzzle recipe.
7. `WordboundAuthoredPuzzleContent` validates the exact resolved course, physical sites, rules and body budgets, then binds geometry+item+rules+plan hashes. New local content naturally receives a different catalogue/source/manifest identity; no public admission record is updated.
8. `WordboundCourseAsset` already saves document/catalogue, exact seed/mode, item/rule references and camera/visual profiles; its Inspector supports edit/apply/preview/play and module handles. Runtime Workshop edits are separately saved drafts; they do not automatically write into a Unity .asset. The UI must keep that distinction clear.

The independent exact item review is in `item-contract-review.md`.

## Source/data targets proposed before implementation

### A. Core composition and saved editable course (first implementation batch)

- NEW `Assets/_Project/Scripts/Rules/Wordbound/WordboundParcelServiceContent.cs`: bounded factory following existing content pattern; public CourseId, Geometry, CopyDocument and Create(ItemCatalogue). Independent immutable catalogue and document. No global registration.
- NEW `Assets/_Project/Scripts/Online/Authoring/Editor/WordboundParcelServiceSetup.cs`: CreateOrSelect menu; creates only a new saved course when absent, validates both modes, reselects existing edited asset without reset/overwrite. Uses Unity APIs and exact original model availability checks. No handwritten YAML/meta.
- NEW saved `Assets/_Project/Resources/Wordbound/LocalCourses/ParcelServiceV1.asset`: WordboundCourseAsset with explicit item/rule references, original artwork profile and existing camera. Resource path is deliberate so the Windows build contains the same editable content. Existing authoring assets stay untouched.
- NEW Rules tests `Assets/_Project/Tests/Rules/WordboundParcelServiceContentTests.cs`; NEW focused authored content tests in the existing Online.Puzzles test assembly; NEW setup/persistence tests in the existing Online.Authoring test assembly.

### B. Explicit local selection + Workshop access (root/UI integration batch)

- `WordboundLaunchAssets.cs`: additive nullable `ParcelServiceCourse` reference, preserving WorkshopCourse/public references. Native setup assigns only the newly added empty field after exact backup; never replaces an existing assignment.
- `Assets/_Project/Scripts/Core/WordboundSoloEntry.cs`, `Assets/_Project/Scripts/UI/Lobby/LobbyPlayLayout.cs`, `PlayPage.cs` and `LobbyPlayPageView.cs` in that same Lobby directory: explicit third local destination with a real accepted preview later. Preserve the two old map IDs/default and selection behavior.
- `WordboundLauncherSolo.cs`: additive exact-source branch for new map. Extend CreateSoloContent with optional `WordboundCourseAsset authored=null`; existing calls remain compatible. For ParcelService, resolve a copy of its saved document/catalogue at requested mode+seed without mutating asset.Seed/ModeId. Construct normal authored content from that asset's Items/RuleBook. Missing or invalid assigned content fails clearly, with no procedural replacement fallback.
- Root-owned preview stage/capture inputs: pass the same assigned course and capture actual resolved bodies. Only extend capture/import roster after the world has passed visual review; no icon pretending to be map art.
- `WordboundWorkshopSources.cs` / `WordboundWorkshop.cs`: one explicit ParcelService source choice using the already-existing remembered-source pattern (parallel to flagship). `ConfigureParcelService(document,catalogue,requirements,seed)`, `OpenParcelService`, `UsesParcelService`, and its own DraftSession/storage scope. This avoids overwriting the current Assigned course or silently reusing its Undo history.
- `WordboundLauncherWorkshopSource.cs`: capture the saved ParcelService item/rule/camera/visual references separately and select them only while UsesParcelService. Bind source identity before refreshing visuals; never resolve new objects against the previous source catalogue.
- Client source-binding/selection tests: switching between Assigned, ParcelService, Flagship and existing templates preserves each seed, mode, selection, Undo/Redo, autosave and captured item/profile inputs. This is a small additional source using existing machinery, not a new registry/schema.

B is necessary for convenient access in the shipped Windows menu and Workshop without replacing James's current assigned course. A alone gives an actual saved/playable Inspector course, but is not the full Windows/Workshop deliverable. Do not claim B complete merely because the asset exists.

No shared item/rule JSON, ModelLibrary, MaterialLibrary, public course allowlist, Photon settings, network protocol, ModeDefinition, CourseDocument schema, original imported mesh, or existing course asset is an implementation target.

## Safety/acceptance gates

- Determinism and immutability: same seed/mode yields same manifest; different saved edit changes source/content identity. Factory does not mutate the supplied item catalogue or any old course/catalogue. Pin and compare old public/current local hashes before/after.
- Geometry: both modes resolve across representative seeds; all23 props, including each seeded storage-bay placement, fit their original dimensions, yaw-aware zones, walls and floor; all8 starts, recovery anchors, route reserve and actual recipe footprints remain clear. Negative tests reject an obstructed door, colliding recovery and oversized/stale catalogue.
- Economy: create initial AuthoritativeMatch and validate its snapshot. Test each of six new prop IDs is a real active item body with canonical size, carry/drop works, destruction yields the exact word once, disabled recipe crafting remains rejected, and normal TABLE/STOOL/PLATE/HAMMER recipe behavior is unchanged. Exhaustively smash the23-prop roster; preserve98 optional letters and entity-budget headroom.
- Gameplay: TimeRun completes all3 laps using actual crafted objects/checkpoint/keepsake rules; wrong map-origin item cannot complete a socket. Freestyle activates and completes each station through existing practice behavior. Destroy all optional props and demonstrate renewable critical recovery still allows completion.
- Saved editing: native create/repeat setup, domain reload, edited asset reselection preserving edits/GUIDs, stale preview rejection. Inspector mode/seed edit plus exact local play; Workshop source switch and export/import preserve catalogue binding. Existing default assigned course and its pending draft remain recoverable.
- Visual/native: original six models/materials load, no missing shaders, canonical fit verified from multiple views, no floor-lamp silliness or stretched shelves; all furniture is reachable/collidable. Player-height views in all3 rooms and doorway views show useful grouping, readable route and vivid original objects. Current teal wall treatment remains a known shared-style limitation; do not silently recolour the global profile to disguise it.
- Shipping: parent runs fresh focused native tests, a Windows build and real Solo+Workshop checks. Only then capture actual destination imagery. Map completion and owner acceptance remain open until those results exist.

## Decision requested from root before coding

Approve the compact three-room layout and A batch, then sequence B with the UI/source-binding owners. The plan deliberately keeps optional repeated-circuit narrative and decor variety modest; it should first prove one coherent, editable, genuinely playable destination.


## Implemented deltas — 29 October 2026

The proposal above is retained as history. The final factory and saved ParcelServiceV1 course use **21 gameplay props / 90 optional letters**, not 23 / 98. The two LAMP placements were removed because the original model is a table lamp. The return socket uses deployable CLOCK instead of PLATE. Three BOX positions remain seeded; protected renewable recipe supplies make progress independent of optional furniture survival.

The later guarded camera repair changes only Start0 from (-2.4, 0, -2.6) to (-2.4, 0, 0). Eight original wall fixtures are separate visual-only meshes in three saved prefab groups: they do not add usable furniture, collision bodies, letters or puzzle targets. Current source is saved in commit66687eb7. See SAVED-PRESENTATION.md and physical-play/NATIVE-GAMEPLAY.md for bounded native results; the new combined physical run and Windows camera review are still pending at this note.

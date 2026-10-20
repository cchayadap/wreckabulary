# Wordbound course workshop

The workshop is an additive course editor. It uses the map lane's `wreckabulary.course` format and leaves the existing CreativeWorkshop and schema-1 HomeLayout saves unchanged.

## Available editing

The standalone `Wreckabulary.Workshop.WordboundWorkshop` component provides four panels: searchable module palette, rendered 3D map, selection properties and floor/history/preview controls. It does not take over an existing scene automatically.

- Click or drag a selection rectangle; Shift adds modules. Selecting a grouped module selects the group.
- Drag selected modules on the current floor. A completed drag is one undo transaction. Escape cancels a pending drag or placement.
- Right-drag to orbit, middle-drag to pan and use the wheel to zoom. Frame the map or selection, or choose Top view. Surface picking respects the active, visible, unlocked floor.
- Use X/Z handles for constrained movement, the centre handle for floor movement, or R for quarter-turn rotation. Temporary visuals do not change the document. Release commits one undo step; a stale drag fails if another edit changed the draft.
- Move by grid or enter exact X/Z values; rotate a selection by quarter-turns around its centre; duplicate or delete it.
- Duplicate copies attached puzzles, supplies, spawns, recovery anchors, checkpoints and zones, and remaps links within the selected subgraph. Connections to unselected modules are not duplicated. Route/lap membership is not silently copied.
- Add floors, choose the active floor, hide or lock it, and move selected modules to it. Adjacent floors appear ghosted. Locked members block an entire transform.
- Place a fixed object, draw a single-object random zone or define a weighted object pool. Edit count range, probability and spacing. Select a random zone and drag its corner handles to resize it.
- Choose approved objects through searchable buttons. Fixed objects can be selected, moved, rotated, duplicated and deleted independently from their room. Random-pool weights remain an advanced compact field.
- Connect selected modules, add an approved puzzle preset and its renewable letter supply, link prerequisites, or attach a puzzle completion condition to a gate.
- Lock the seed, set its sixteen hexadecimal digits, reroll when unlocked, inspect validation issues and preview the exact draft/seed.
- Share canonical course JSON through the text panel or save a local `.course.json` file. Autosave and recovery retain editor settings separately.

Keyboard shortcuts when the viewport has focus: Tab/Shift-Tab selects, F frames, R rotates, Delete removes, Ctrl-D duplicates, Ctrl-Z undoes and Ctrl-Y/Ctrl-Shift-Z redoes. These commands leave focused text fields alone.

Controller: D-pad selects modules, left stick moves the selection by grid, right stick orbits, triggers zoom, X rotates, Y toggles multiselect, A frames, LB/RB undo/redo and B cancels. A continuous stick movement commits once when released. Start opens the named action menu; D-pad navigates and A applies an action. The action menu includes placement, floors, duplicate, preview/playtest and sharing. Detailed text/weight entry still uses keyboard or pointer input.

Deletion deliberately leaves external references visible as validation errors rather than silently removing progression constraints. Incomplete geometry and puzzle topology are editable, but full validation must pass before a resolved playtest is dispatched.

## Three starting templates

`WorkshopTemplates.Create(WorkshopTemplate)` supplies Race Circuit, Combat Arena and Freestyle World, using the map lane's proof catalogue. Each template has a passing deterministic resolution test. These are functional authoring examples, not the final large flagship map or the final equipment-dimension catalogue.

| Template | Preview policy |
| --- | --- |
| Race Circuit | Team Race, two solo teams, isolated state |
| Combat Arena | Eight-player free-for-all |
| Freestyle World | Shared exploration, capacity eight |

## Immutable editing and recovery

`WorkshopSnapshot` stores canonical course JSON and freezes all editor state. `Course()` and `Edit()` return fresh mutable copies. Publishing a transaction performs structural codec validation and a strict import round trip; the prior snapshot and redo stack remain intact if a command throws.

`WorkshopHistory.Execute(label, transaction)` creates one undo step. `Select(...)` changes selection without adding an undo step. History is bounded by sixty-four snapshots and eight million JSON characters. Preview and playtest never mutate editing history.

`WorkshopStorage` writes only validated course IDs under its supplied folder. Autosaves use an atomic temporary-file replacement with a previous-save `.bak`; recovery tries the backup if the main file is corrupt. UTF-8 and file-size bounds are checked before parsing. Recovery stores the current draft, selection, groups, floor state, grid and seed; it does not persist the undo stack.

Shared files contain only the strict course document. The separate recovery envelope is `wreckabulary.workshop`, schema 1, with the canonical course JSON as a string. HomeLayout files, unknown fields and executable references are rejected by the course codec. No scripts, remote downloads or external asset URLs are accepted.

Default runtime storage is `Application.persistentDataPath/wordbound-workshop`, independent of the home editor's saves. Switching templates saves the outgoing draft and retains its in-memory history. Reopening a template restores its saved recovery document automatically; explicitly opening a supplied document keeps that supplied document and starts autosaving after the first edit.

## Runtime integration contract

Create the component in an isolated workshop context and call either:

```csharp
workshop.OpenTemplate(WorkshopTemplate.RaceCircuit);
workshop.Open(courseDocument, approvedCatalogue, playRequirements, storageFolder);
```

The root integration lane owns entry/back navigation, scene isolation and playtest routing. Subscribe to `PlaytestRequested` to receive the exact validated `ResolvedCourse`. The Playtest button stays unavailable without a subscriber. Set `Visible = false` during playtest and call `ResumeEditing()` to return to the same history. `CloseRequested` is emitted after a successful save. `History`, `Catalogue`, `PlayRequirements`, `LastPreview` and `Preview()` are available to integration.

Set optional `SpawnAssetVisual(assetId, parent)` and `AssetIcon(assetId)` delegates before `Open` to route an approved model/icon collection. The default model path uses the existing `ModelVisual.Spawn("Items/" + assetId, parent)` facility. Imported child transforms are preserved. The viewport otherwise displays an approved-dimension proxy if a model is unavailable. The root lane should bind the Wordbound collection and its individual icon textures.

The 3D stage uses its own camera/render target on layer 31 at world Y -20000; it does not move or reconfigure the game camera. It reuses `Surfaces` meshes/textures and owns only its private tint materials, camera, render target and stage objects. It adds no gameplay physics. Adjacent floors are translucent context; item visuals are shown on the active floor. Resolved Preview shows placements from the exact manifest that playtest receives. Course edits invalidate that preview.

The runtime surface uses Unity IMGUI with named keyboard controls and tooltips. Native layout/input/controller validation, final icon binding, authored landmark geometry, picking on arbitrary imported mesh surfaces, advanced route/lap editing and novice-user acceptance remain outstanding. Floor surface picking is implemented for the approved rectangular module contract. The proof palette currently contains one Room module; the contract supports additional approved definitions.

## Evidence

Engine-free tests cover snapshot alias isolation, atomic rollback/redo, attached-point transforms, floor changes, grouping/locks, duplicate references, bounded history, save recovery and path rejection, share-format separation, seed policy, all three template previews, fixed placement transforms, world bounds, zone probability and approved puzzle recipes.

`BuildNotes/map-workshop/rules-tests.log` records the current test result. `runtime-typecheck.log` records the Unity 6000.6.3f1 reference-assembly compile. Native Unity launch, scene integration and human interaction testing are **NOT RUN in this lane**. Root must generate metadata and run those checks after integrating the map contract and workshop source commits.

`WordboundWorkshopTests` supplies five native editor checks for template autosave restoration, explicit import behavior, playtest/template return fidelity, camera projection/surface and object picking, and owned-resource disposal. They are authored for the root's native Unity gate and have not been executed in this lane. The focused reference compile links the integrated Wreckabulary assembly for shared art facilities; its two CS0436 warnings identify the intentionally replaced workshop class, not a native project compile result.

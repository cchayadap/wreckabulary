# Workshop route authoring

STATUS: GREEN SOURCE CHECKPOINT — NATIVE UI TEST REMAINS ROOT-OWNED

Scope: new `WorkshopRoutes` pure helper, `WordboundWorkshopRoutes` UI partial and focused tests. Root owns the single `RouteInspector()` call in the shared main workshop inspector. Existing physical-site helpers and client launch code remain owned by their current lanes.

The persisted model already supports ordered route sections, weighted alternatives, per-lap section order, checkpoints, recovery anchors and puzzle references. This work exposes those structures without introducing a schema or silently replacing imported data. Existing “Add puzzle + letters” creates the protected renewable recipe supply; the route editor links that existing puzzle into an explicit checkpoint before the physical editor is used.

Implemented source controls:

- Add/edit/delete a recovery anchor on a selected module, with position, radius and explicit prerequisites.
- Add/edit a checkpoint using an existing recovery anchor and optional arrival connection; link existing puzzles and reorder checkpoints within a selected branch.
- Add a route section and weighted alternatives; append/remove/reorder existing connections, with visible traversal direction.
- Reverse a section only when every connection remains traversable; report incompatible checkpoint arrival references for explicit repair. Do not invert one-way connections or rewrite shared checkpoint IDs.
- Choose lap start, finish, story and ordered sections for each of the three laps. Preserve draft validation errors while incomplete; saving a route does not imply playtest acceptance.
- Remove references only through an explicitly named action. Refuse deletion of a checkpoint still used by physical controls.
- Add/edit/delete explicit Start and Respawn anchors, including module, floor position and quarter-turn facing. Enforce existing 0.5 m clearance and 1 m separation, show current capacity needs, and preserve the current mode-driven player/team allocation. No per-spawn radius/team fields were invented or added to the codec.

Every persisted mutation uses `WorkshopHistory.Execute` through the workshop's existing `Edit` helper, preserving undo, redo, autosave and preview invalidation. Imported stable IDs are retained; IDs are minted only when the user explicitly adds an entity.

Review correction: Sethy found that key/revision alone would preserve stale form inputs when opening a different `WorkshopHistory` with the same IDs and revision. `ReloadRoute` now also keys on history identity and invalidates branch state. A new actual-component Editor test opens two real histories and checks that pending old input is discarded. This test is not yet executed.

The new 15 pure tests cover authoring routes and starts from existing geometry, exact undo/redo/JSON persistence, deterministic weighted choices, invalid checkpoint order, explicit reversal repair, one-way rejection, recovery references/clearance, physical-control deletion guards, locked floors, branch/lap deletion, repair of an old missing connection, spawn bounds/spacing and capacity errors without replacement generation.

Root integration seam: add `RouteInspector();` in `WordboundWorkshop.Inspector()`, outside the selected-module block and immediately before the Validation section. The partial owns its collapsible panel and all state; no reset or lifecycle changes are required in the shared file.

Checks used actual integrated Rules/workshop sources at clean source revision `20fc3bed52b75e2a3115d737420e4a3a6fa5b311`. `RoutesTests.csproj` passed 15/15 real NUnit cases. `ReferenceCompile.csproj` passed against installed Unity 6000.6.3f1 references with zero errors and five CS0436 overlay/core duplicate-type warnings in existing WorkshopViewport/WorkshopPlacement references. `NativeTestsCompile.csproj` compiled the new Editor fixture in a separate assembly against that result with zero warnings/errors, so source flattening cannot conceal private/internal-access mistakes. Native execution has not run in this lane.

Exact verification commands and source hashes are in `VERIFICATION.md`. All three commands exited zero; the compiler slot was released directly to Sethy's cache reference check. Interop is already complete in `ac9474a`.

Next action: root cherry-picks the source checkpoint, adds the one inspector call, lets Unity generate metadata and runs the new native Editor cache regression with the normal workshop gate. No Unity launches, metadata authoring, external tools or service deployment occurred here.

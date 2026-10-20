# Route authoring integration

Add one call in the existing `WordboundWorkshop.Inspector()` before the Validation heading, after the selected-module block:

```csharp
RouteInspector();
```

The new partial shows five tabs: Safe, Stops, Routes, Laps and Spawns. It uses existing `Edit`, `Button`, `Field`, `History`, `Catalogue` and preview invalidation. Root should let Unity create metadata for the four new scripts; no serialized scene fields or prefab references are required.

Authoring sequence:

1. Place/connect modules using the existing workshop controls.
2. Add Start anchors on the selected first-lap module. The panel shows the selected mode's capacity requirement.
3. Add safe recovery anchors, then checkpoints referencing those anchors. Existing “Add puzzle + letters” creates an objective and protected renewable recipe supply; link that puzzle to a checkpoint.
4. Add a route section with explicit start/finish modules. Add its connections and checkpoints in travel order, then apply the branch. Additional weighted branches share the section endpoints and are selected deterministically by the existing seed resolver.
5. Add each lap's start, finish, story and ordered route sections. Use the existing physical controls editor for selected checkpoint puzzles.
6. Validate and preview. Draft edits may intentionally leave the course incomplete; successful editing is not playtest acceptance.

Reversing a saved section validates reverse traversal for every branch before changing anything. One-way connections and incompatible checkpoint arrivals are rejected. The author repairs arrivals explicitly; lap transitions, gate order and physical puzzles still require normal validation. Existing stable IDs and imported route data are never silently regenerated.

Deletion controls name their scope: checkpoint deletion detaches it from all branches, route deletion detaches it from all laps, and recovery deletion refuses referenced anchors. Physical controls prevent implicit checkpoint/puzzle removal.

Spawn clearance/team policy follows the existing schema and runtime. No HTTP, online reservation, catalogue registration or service deployment is involved.

The Editor cache regression is `Wreckabulary.EditorTests.WordboundWorkshopRoutesTests.OpeningAnotherHistoryWithSameIdsAndRevisionDiscardsOldRouteInputs`. It needs the normal Unity Editor test gate; the separate reference build establishes compilation only.

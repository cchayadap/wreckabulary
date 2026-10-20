# Workshop usability increment

STATUS: COMPLETE — SOURCE VERIFIED; NATIVE ACCEPTANCE NOT RUN

Owner: SethyPagna, `sethypagna/map-workshop`, clean starting commit `2e25a7d`.

Write scope: existing `Assets/_Project/Scripts/Workshop/WordboundWorkshop*.cs`, focused corresponding editor tests, and this BuildNotes folder. No shared launcher, scene, catalog, Rules sources, Unity metadata, dependencies or native Unity launch.

Confirmed gaps: existing placement settings have no Apply operation independent of drawing new bounds; that redraw can reset authored yaw. Pool weights require raw identifier syntax. Floor controls overflow a single horizontal footer as floors increase. Floor switching and property edits can leave a pending pointer gesture. Playtest already recomputes full validation, but the launcher would benefit from an immutable reference to the exact draft paired with the dispatched manifest. Correction after exact source read: WorldView already catches pointer exceptions; this increment retains it and consumes failed input.

Integration API remains `Open(course,catalogue,playRequirements,folder)` / `OpenTemplate(...)`, `PlaytestRequested(ResolvedCourse)`, `Visible=false`, then `ResumeEditing()`. Root owns the launcher and native acceptance. `LastPlaytestDraft` exposes the exact immutable editor snapshot paired with the event, including the seed and canonical course document. Read it inside the callback before any later `Open`. `ResumeEditing` retains the original history; a failed/unavailable playtest request clears the property so a stale success cannot be mistaken for the new request.

Implemented: immutable `LastPlaytestDraft` set immediately before dispatch and cleared on Open or failed/unavailable requests; placement Apply separate from redraw; X/Z min/max in world metres; clickable pool membership plus per-object integer weight; optional advanced text; ordered scrollable floors with explicit Show/Hide and Lock/Unlock; gesture cancellation for floor changes and undo/redo; placement refresh after drag/undo and owner-aware multi-selection inspector. Selected fixed objects frame their own position. Hidden/inactive/locked floors do not display active transform handles.

Placement updates preserve authored purpose, grid, layer list, and random rotation choices; fixed updates preserve fixed yaw. History remains the atomic commit/rollback boundary. Numeric and catalogue errors leave the old draft and redo intact.

Verification:

- Real NUnit **38/38 PASS**: 23 existing workshop rule tests plus 15 new placement cases. Covers immutable history/redo rollback, fixed yaw/grid/purpose preservation, one-transaction range/weight edits, mutable-input isolation, invalid numeric/weight/owner/locked-layer rejection, and random-to-fixed conversion at the selected room centre. Evidence: `placement-tests.log`; full local TRX under ignored `TestResults/placement-tests.trx`.
- Actual Unity **6000.6.3f1** reference compilation **PASS, 0 errors**. Six CS0436 warnings explicitly select this lane's source over older imported `WordboundWorkshop`/`WordboundWorkshopViewport` types in integration's `Wreckabulary.dll`. No warning was suppressed. Both the runtime source and native editor tests compile. Evidence: `reference-compile.log`.
- `git diff --check` clean. No Unity, scene, metadata, dependency, catalogue or shared launcher changes.
- Two intermediate test runs were 36/37: first a fixture used BAT outside the proof catalogue; next the fixture assumed pool insertion order despite canonical sorting. Corrected to BED and asset-ID lookup; final run is fully green.

Reproduce sequentially from this worktree with `dotnet test BuildNotes/workshop-usability/PlacementTests.csproj -p:UseSharedCompilation=false -m:1`, then `dotnet build BuildNotes/workshop-usability/ReferenceCompile.csproj -p:UseSharedCompilation=false -m:1`. These evidence projects use the existing sibling map-core source and installed Unity/integration assemblies. Allow normal restore when switching projects.

Native acceptance remains **NOT RUN** for this increment. Root should run `Wreckabulary.EditorTests.WordboundWorkshopTests` and `Wreckabulary.EditorTests.WordboundWorkshopPlacementTests`, including the exact-draft dispatch and invalid-draft refusal cases. Inspect the complete UI at 1280×720, not just the viewport render:

1. Select/rotate/drag FixedStool, change spacing, Apply, Undo/Redo; yaw and position must remain as authored.
2. Select GardenProps, use clickable pool members and numeric weights, set min 0/max 4/chance 37.5%, edit X/Z ranges, Apply and preview the exact seed.
3. Add several floors, use Show/Hide and Lock/Unlock, switch during an unfinished drag, and check no stale transform commits.
4. Dispatch playtest, keep `LastPlaytestDraft` with its manifest, return through `ResumeEditing`, and verify draft/seed/history remain available.

Controller tools retain selection, move, rotate, undo/redo, floor switching, preview and playtest. Numeric text editing and pool authoring still require keyboard/pointer; full controller-only property editing and novice usability are not claimed.

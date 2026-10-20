# Flagship content API

`WordboundFlagshipContent.Create(ItemCatalogue)` derives a local immutable geometry catalogue from the actual 12 approved item definitions. It does not modify item JSON, shared modes or existing templates.

```csharp
var content = WordboundFlagshipContent.Create(items);
var mode = ModeDefinition.For("TimeRun", RaceFormat.Solo);
var result = content.Resolve(seed, MatchStateValidation.Requirements(mode));
if (!result.Success) throw new InvalidOperationException(string.Join("; ", result.Issues));
var course = result.Manifest;
```

- `CopyDocument()` returns an independent schema-2 draft including all physical sites and moving fixtures. The canonical course codec owns import/export, source hashing and manifest hashing.
- `Geometry` is the exact geometry catalogue to supply to the resolver, renderer and runtime content bundle.
- `Hash` additionally binds the copied relevant equipment statistics and presentation/story metadata. Gameplay admission must still bind the complete item/rule sources and resolved physical plan through the normal content bundle.
- `Destinations`, `Rooms`, `PuzzleSites`, `Equipment` and `Story` are immutable presentation metadata. Runtime physical authority uses the persisted course sites, not a separately mutable sidecar.
- `WordboundFlagshipRoutes.Measure(course)` audits required centre-checkpoint traversal along real connectors. Main route: 751.2666 m; repair route: 799.2666 m. Puzzle-station movement and human completion time are outside this measurement.

The factory supports TimeRun, TeamRace and BrawlRace with the existing Solo/Duo/Squad race formats, plus the full reachable Freestyle world. Freestyle has no active race laps. FreeForAll is intentionally not advertised by this course.

There are 34 rooms, four destination floors at 0/8/16/24 m, 72 distinct per-lap puzzles, and 100 possible ordered checkpoints. Lap 1 uses the main route; lap 2 reverses the course and selects the main/repair branch from its seed; lap 3 requires the repair branch.

Each physical site has an author-facing one-based lap. Runtime progress uses zero-based lap indices. The six families are CollectLetters, PlaceObject, RedirectWind, TimedMechanism, BuildRoute and BalanceWeight. `CoursePhysicalSite` persists the exact checkpoint, protected renewable recovery sources, controls, tolerance/hold, receiver/arrival/pad volumes and moving fixtures. CLOCK arms have a 240-tick period; current gameplay tick rate is 60 Hz.

CollectLetters references an actual marked TABLE prop and supplies renewable HAMMER letters. Wind references an actual STOOL prop plus a separately identified renewable STOOL-letter recovery source; a replacement must be crafted and dropped, not assumed to respawn. BuildRoute uses a raised parcel marker beyond the real jump-pad footprint. BalanceWeight uses two pad volumes coverable by one real TABLE/SOFA. Every family has a one-player authored solution contract.

Schema-2 focused source checks passed 24/24; final seed evidence is pending in `PROGRESS.md`. `BODY-BUDGET.md` records a runtime blocker: startup fits 512 canonical bodies, but a three-lap refill/tombstone estimate exceeds that limit. Do not treat the factory's successful resolve as whole-match capacity acceptance. Native collision, field/arm timing, complete human puzzle solutions, presentation and the planned completion-time target also require separate runtime/native gates.

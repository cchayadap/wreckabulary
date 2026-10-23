# Per-call fixture mechanism lookup review

STATUS: SOURCE PASS — exact Online commit `df7fca65`. No compiler, Rules harness, Unity or measurement job run by this reviewer.

The production delta replaces only `ValidateBodies`' repeated mechanism scan with a lazy local `ILookup<(PuzzleId, Lap, Team), MatchMechanismState>`. Construction occurs at the original physical-fixture query, after the same body identity checks. It is not retained across calls or added to the immutable course cache. Tuple string equality matches the original case-sensitive string comparisons.

`ToLookup` retains duplicates rather than rejecting them eagerly. The selected group still calls the predicate overload of `SingleOrDefault`, so a duplicate for that queried key preserves the original exception message. An absent key still reaches the existing fixture-pose error. A duplicate not referenced by a fixture remains for the later `ValidateMechanisms` duplicate guard. Earlier resources, bounds, fixture identity and earlier-body pose errors retain their order. Snapshot construction already freezes non-null entries; selecting their get-only key properties adds no mutable dependency or validation result cache.

The **10 added cases** (expected existing fixture total **88**) exercise referenced missing/duplicate versus unreferenced duplicate, four earlier-error combinations, and TimeRun/TeamRace/Freestyle advanced-motion snapshots. The movement cases advance the real authority first, retain matching canonical fixture poses, reject a stale pose, then revalidate current and retained snapshots. They use public immutable constructors and existing fixture APIs. Exact predicate exception text is derived from the original LINQ operation on the same invalid snapshot, avoiding runtime-specific hardcoding. The production diff leaves every dynamic check, body iteration and public API unchanged.

No concrete source blocker found. Run the existing `Wreckabulary.Tests.WordboundSnapshotValidationTests` fixture and the phase/public comparison on the assembled source. The measured `205256Z` attribution motivated this change; source review does not establish a speedup, public timing margin or Windows acceptance.

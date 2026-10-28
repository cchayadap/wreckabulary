# Practice selection repair

STATUS: FROZEN — source check pass, independent source review found no blocker. Exact two-file candidate, no live/native writes.

The Parcel Service course has lap-specific stations at shared positions. Previously Nearest returned the same hash-sorted site after it was completed, while the normal HUD/menu refused completed sites. The remaining co-located stations were therefore not offered.

The production change preserves the existing actor/identity/mode/range guards and filters candidates by the requested distance first. It sorts unfinished sites first, then by distance and ordinal Hash. If all sites in range are completed, nearest completed feedback remains available. There is no new input route, authority mutation, new mode or change to completion rules.

Three additions to the existing WordboundPuzzleGuidePlanTests fixture cover: normal candidate/guide readiness advancing through the three co-located SortingBench lap sites as completed read-model data changes; nearby-completed fallback versus a farther unfinished site inside/outside the explicit range; all-completed fallback, stable tie order, and stale identity/unknown actor/disconnected/inactive/invalid-range guards.

These are deliberately presentation read-model fixtures. They are not an authority simulation or proof that a player completed any puzzle. Root/map lane must exercise actual native play using normal Nearest selection, crafting, deployment and completion.

Source check: four assemblies (Rules76,main180,Presentation13,Presentation.Tests10), zero errors, Roslyn C#9 with current Unity response-file references and in-memory upstream substitutions. No Unity process launched here. Root should verify both current live files still match baseline hashes, copy the two candidate files exactly, then run the existing guide fixture including all three new cases. Also run the actual Parcel Service six-site flow through the ordinary nearest-site candidate.

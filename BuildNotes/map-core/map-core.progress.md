# Map-core lane

STATUS: VERIFIED — ready for integration

Base: `12842c75608694f36819edd3bd4dc104ece7f79c`; branch `sethypagna/map-core`.

Owned scope: new Course*.cs under Rules/Wordbound, matching rules tests, docs/wordbound/MAP-CORE.md, and this directory. No Unity launches or shared catalog changes.

Contract: versioned course documents separate from home saves; resolved immutable three-lap manifests; installed GameStudioKit PCG/Hash64 streams; bounded validation and canonical SHA-256.

Completed: DTOs, strict codec/catalogue, geometry/resource validation, immutable resolver and three small proof templates. Explicit fixed prop placements share collision/protected-volume checks with random placements. Directed recovery paths must allow carrying letters back to the puzzle before its gate unlocks. Door width includes edge clearance; graph traversal uses adjacency maps.

Verification on 2026-10-20:

- Initial lightweight RulesHarness passed 255/255 (249 baseline + six course tests), 19.65 seconds.
- Focused lightweight course suite passed 71/71, 56.58 seconds.
- Final real NUnit core gate passed 300/300 (225 baseline + 75 course cases), 70.8 seconds; `Logs/gate_core_20261020T105418Z.log`, `TestResults/core_20261020T105418Z.trx`.
- Both broader suites include 11,000 successful seeds: 1000 each for TimeRun; TeamRace and BrawlRace Solo/Duo/Squad; FFA 2/8; Freestyle 1/8. Each seed independently validates the resolved manifest; every hundredth seed also replays and round-trips it.
- Malformed JSON, tampered/rehashed manifests, exact match/catalogue binding, resource multiplicity/consumption/recovery, directed supply traps, stairs, doorway edge clearance, fixed-placement collisions, 256-module capacity, source immutability, culture-independent hashing, independent streams and installed PCG reference vector are covered.
- No Unity launched and no generated asset or metadata files authored.

Commits: initial green API `2ce1906`; final hardening/verification is the commit containing this record.

Next recoverable action: integrator cherry-picks both commits, imports scripts through Unity, connects the resolved-course adapter and mode projection, and runs engine geometry/physics checks. The 24-module flagship and procedural module composition remain after the networking proof. This lane now moves to the separately assigned match snapshot codec scope.

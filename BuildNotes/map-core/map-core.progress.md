# Map-core lane

STATUS: IN PROGRESS

Base: `12842c75608694f36819edd3bd4dc104ece7f79c`; branch `sethypagna/map-core`.

Owned scope: new Course*.cs under Rules/Wordbound, matching rules tests, docs/wordbound/MAP-CORE.md, and this directory. No Unity launches or shared catalog changes.

Contract: versioned course documents separate from home saves; resolved immutable three-lap manifests; installed GameStudioKit PCG/Hash64 streams; bounded validation and canonical SHA-256.

Current step: DTOs, strict codec/catalogue, geometry/resource validation, immutable resolver and three small proof templates implemented. Explicit fixed prop placements share collision/protected-volume checks with random placements.

Verification: lightweight RulesHarness passed 255/255 on 2026-10-20 (249 baseline plus five mode round-trips and one immutable three-lap regression), 19.65 seconds test execution. No Unity run.

Next recoverable action: add 1000-seed checks per proof configuration, adversarial import/manifest tests, dependency/stairs/resource regressions; run real NUnit core gate. Public contracts communicated to integrator and workshop lane.

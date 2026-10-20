# Runtime course foundation

STATUS: SOURCE VERIFIED; NATIVE VERIFICATION PENDING

Base: `4dfce93`, shared `sethypagna/puzzle-race` checkout. Integrator owns commits and Unity execution.

Scope: additive static Unity geometry from the immutable resolved manifest; module floors/walls, connector clearance, anchors, semantic collision hooks, and focused Editor tests. No scenes, packages, metadata, shared catalogue, dynamic gameplay entities, or global collision settings.

Design: course coordinates are millimetres converted to metres under a single course root. Module roots retain authored position/yaw. All branch geometry exists together. Stairs/ramp top surfaces follow authored endpoints; upper floors and wall crossings are cut for clearance. Gated connectors expose their puzzle identity for the authoritative adapter. General physics `Traversal` connectors require an explicit adapter and are rejected by this bounded builder.

API:

- `WordboundCourseWorld.Build(course, catalogue, parent = null, options = null)` constructs one immutable branch union. The caller provides an already verified `ResolvedCourse`; this builder never resolves a seed. Catalogue hash mismatch and unsupported Traversal fail before object creation.
- `TryGetAnchor(kind, stableId, out Transform)` resolves layer/module/connection/from/to/puzzle/checkpoint/spawn/recovery/placement/supply identities. `ManifestHash`, `Course`, `Surfaces`, `ToWorld(CoursePoint)` and `ToCourse(Vector3)` expose the binding and coordinate conversion.
- Module roots preserve authored position and quarter-turn yaw. Puzzle anchors use their module's authored floor centre because the course schema has no independent puzzle position; checkpoint anchors use their explicit recovery position.
- `WordboundCourseWorldOptions` accepts borrowed floor/wall/connector materials and `ConfigureCollision(Collider, WordboundCourseNode)`. Tags provide stable ID, module, gate puzzle, ownership policy, owner team, and protection. Static structure is shared (`OwnerTeam = -1`) and protected; supply/placement anchors retain their manifest team/protection. No global physics matrix or project layer settings are changed.
- Gate barriers and active platform motion remain authoritative adapter responsibilities. The static connector support surfaces exist for every branch; this builder does not decide a team's current gate/lap state. It creates no gameplay players, resources, furniture, tiles, keepsakes, or platform entity duplicates. Source anchors are empty transforms for that later adapter.
- Stairs use smooth collision ramps with exact endpoint top surfaces, avoiding step snagging. Wall crossings and upper floor openings provide 2.4 m clearance. Decorative stair treads, production art, general Traversal physics presets and flagship geometry are deferred to the appropriate adapters/content lane.
- `Dispose()` deactivates and destroys only its owned root (deferred until frame end in Play Mode). Borrowed materials and shared surface-library materials survive. Failed construction performs the same cleanup. Parent metre scale is required; parent translation/yaw can be converted through the public coordinate helpers.

Verification:

- Focused .NET reference compilation of both runtime source files plus the Editor fixture against actual Unity 6000.6.3f1 reference assemblies, integrated game/rules assemblies and Unity NUnit: PASS, 0 warnings, 0 errors (final run 3.03 s). The local ignored `ReferenceCheck.csproj` only type-checks source and writes binaries under ignored `Logs/runtime-course/`.
- The new four-layer, bridge and unsupported-Traversal test documents were independently resolved through the already built engine-free rules assembly: all three succeeded. The four-layer document resolves five modules and four connected routes. This checks fixture validity without pretending to execute Unity physics.
- Native Unity tests NOT RUN in this lane. `WordboundCourseWorldTests` has nine native cases: three proof templates, branch surface/capsule clearance, bridge/ramp exact geometry, four layers with a rotated rectangular room, cleanup on success/failure, deterministic anchors under transformed parenting, and unsupported/binding/scale rejection.
- Pure source/shape review covers authored millimetres, all branch inclusion, no RNG, no duplicate gameplay entities, exact top surfaces, upper-floor cutouts, mid-ramp wall crossing and material ownership.

Next recoverable action: integrator commits the two runtime files, Editor fixture and this note; Unity generates metadata. Run `node Tools/kit/gate/game_gate.mjs test editmode --filter WordboundCourseWorldTests`, then render and movement playtest all proof branches before native acceptance. Source type-check cannot prove PhysX clearance or visual quality.

Separate assigned A4 snapshot follow-up completed and committed by root as `f3c50d6`: active unsolved timing requires the reached checkpoint/lap/prerequisites and the correct live deployed crafted item/team/module. Focused real-NUnit mechanism suite passed 18/18 (`TestResults/core_20261020T120607Z.trx`). Independent reviewer rechecked adversarial current/future/held/rival item mutations and 23 mechanism/return owner cases; report commit `2d92d3a`.

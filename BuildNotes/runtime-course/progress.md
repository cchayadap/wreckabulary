# Runtime course foundation

STATUS: NATIVE FOLLOW-UP READY; RERUN PENDING

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

Native follow-up (2026-10-20):

- Integrator's first native run executed all nine geometry cases: eight passed; the branch-walkability case found no floor on the Door approach at `(9.1, 6, 0)`. Evidence: integration `TestResults/editmode_20261020T122803Z.xml`.
- Cause: the initial slab subtraction removed the full projected stair span, even where the returning stair was far enough below the upper slab to preserve floor. It now starts at the point where ramp height plus 2.4 m clearance reaches the slab underside (including slab thickness), keeping safe overhead floor intact.
- The existing proof Door at `z = 0` also crossed the returning stair's genuine upper opening on its Side exit. Root explicitly approved moving only this proof connection's two endpoints to `z = -6 m`. IDs, room connections, puzzle gate, lap routing and rules are unchanged; new resolves correctly receive different source/manifest hashes.
- The regression still checks capsule clearance and floor support over every connection. Additional assertions preserve safe overhead floor and require the genuine UpA/UpB/Return headroom openings. Native rerun is pending; physics adapter files remain separate from this fix.
- Direction/degeneracy: the course validator requires strictly ascending Stairs/Ramp endpoint Y values. Four new pure cases explicitly reject descending and flat endpoints with `stairs.slope`; the builder rejects these before allocation or division as well. Two additional native cases exercise valid negative-X ascent for Stairs and Ramp, retaining the low-end floor while opening the high-end headroom.
- Follow-up verification: focused `core Course` PASS 80/80 in 87 s (`TestResults/core_20261020T123535Z.trx`), including the proof configurations' 1,000-seed checks. Geometry runtime + Editor fixture reference compilation PASS with zero warnings/errors (5.46 s). Native eleven-case rerun remains pending.

Separate assigned A4 snapshot follow-up completed and committed by root as `f3c50d6`: active unsolved timing requires the reached checkpoint/lap/prerequisites and the correct live deployed crafted item/team/module. Focused real-NUnit mechanism suite passed 18/18 (`TestResults/core_20261020T120607Z.trx`). Independent reviewer rechecked adversarial current/future/held/rival item mutations and 23 mechanism/return owner cases; report commit `2d92d3a`.

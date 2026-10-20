# Entity lifecycle progress

STATUS: STAGE 1 VERIFIED; FULL PRESSURE POLICY PENDING

Baseline: `17c30b4`. Root approved two stages and exclusive ownership of AuthoritativeMatch, new AuthoritativeLifecycle/Economy partial helpers, MatchStateValidation and focused tests. Stage 2 also needs a versioned policy helper consumed by the root-owned rules fingerprint; no Controller, Session or UI edits belong to this lane.

Stage 1 removes loose letters from a completed team lap or a puzzle supply whose last selected consumer has completed. Retirement increases Spent using checked arithmetic, preserving the existing conservation equation. It does not remove player letters, reserved crafts, held/deployed/thrown equipment, rival resources, or future source rows. Independent Practice supplies retain general behavior. Consumer lookup uses resolved supply SourceId and caches immutable consumer/route sets.

Destroyed crafted bodies are removed only after active melee/flight references and physical auxiliary references release them. Economy tombstones remain while a mechanism references the item. Authored map tombstones remain for exact source identity and physical smash evidence. Unreferenced crafted economy rows are forgotten without reusing IDs. Existing expiry and weapon-break tests now require these unreferenced entities to be absent.

Checkpoint validation rejects active tiles or pending deadlines belonging to retired puzzle scopes. Course/home/snapshot schema numbers and wire command shapes did not change in this stage.

Verification:

- Focused real NUnit: 152/152 passed, 20 seconds. Includes 9 lifecycle cases plus authority, deployment expiry, physical evidence, combat, mechanisms and BALL regression cases. Output: `TestResults/runtime-puzzles/lifecycle-stage1.trx`.
- First test compilation found a test-only TileSpawn.Id typo; corrected to TileId before the passing run.
- Targeted build uses the existing ignored DeploymentTests.csproj, with WordboundLifecycleTests included. Production source is compiled from the current lane.
- .NET Standard 2.1 Rules reference build: PASS, zero warnings/errors, 9.53 seconds. Output: `Logs/entity-lifecycle/rules-bin/Wreckabulary.Rules.dll`.
- Unity/native gameplay: NOT RUN in this lane. Root owns that gate.

No claim that Stage 1 guarantees the full 512-body limit or bounds endless Freestyle. The exact pressure/reservation implementation remains next. The 512 runtime cap is unchanged.

Compiler released to workshop owner after both checks. Rules source is frozen for that short gate. Next recoverable action: commit the exact Stage 1 paths, then implement the approved deterministic capacity policy as a separate increment after the workshop owner releases the source freeze.

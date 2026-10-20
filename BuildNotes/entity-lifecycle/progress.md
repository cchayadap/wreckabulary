# Entity lifecycle progress

STATUS: STAGE 2 SOURCE VERIFIED; NATIVE INTEGRATION PENDING

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

## Native test assembly repair

Root's Unity import caught three test compilation errors: the original focused harness compiled production and test source together, masking internal API access from the separate Rules.Tests assembly. Production API visibility remains unchanged. The retirement test now exercises public AuthoritativeMatch publication, import and committed host restoration; deliberate malformed EconomySnapshot construction uses narrowly scoped reflection.

Separate-assembly verification: PASS, 9/9 lifecycle cases in 5 seconds. The test assembly references the compiled `929240a` .NET Standard Rules DLL (SHA-256 `B5708A7CEEBB6A6AFAA23B345BF45C11BF18460C9254440B473299F017E60D0D`) and real NUnit 3.14, without linking production source. Test fixture CS0649 is suppressed in the isolated harness because its optional ReleaseAt field is assigned by other suites; no production warnings or access errors remain. Evidence: `TestResults/entity-lifecycle/lifecycle-separate-assembly.trx`. Native execution remains root-owned and NOT RUN here.

Compiler released to the online owner's Worker/SQLite gate. Stage 2 design and its uncompiled budget helper remain separate and unstaged.

## Stage 2 source checkpoint

STATUS: SOURCE DRAFT; VERIFICATION PENDING ROOT NATIVE WINDOW

Drafted WordboundEntityBudget v1, authoritative optional admission/recycling/deferred source retries, 512 global and equal-team pool enforcement, 8192 canonical section checks, and focused public-facade pressure tests. Mandatory equipment and designated recovery retain explicit reserves. Shared race debris counts conservatively against each pool. Authored map bodies, active throws, held/deployed equipment and physical evidence are excluded from eviction.

No Stage 2 compiler or Unity run has started. Root owns native session68121; this lane has the next bounded source window after it exits. Before committing, run the focused pressure/lifecycle/authority suites, separately compile tests against the production Rules DLL, audit actual flagship startup budgets, and verify malformed-source/pool snapshots. Stage 1's earlier passing results do not validate this draft.

## Stage 2 verified checkpoint

The approved capacity policy is implemented without increasing the 512-body limit. It reserves equal team pools for fixed authored roots, selected-route recovery prefixes, physical receiver evidence and player inventory/drop bursts. Optional loose releases may return `Capacity` before mutation. Optional initial/refill batches defer with a canonical retry deadline and unchanged generation; nonrenewable deferred sources still appear once. Eligible loose optional value retires in stable order with exact Spent accounting and no allocator reuse.

Snapshot validation now enforces both the gross pool and the reserved core invariant. Independent review B1 identified that a rehashed set of valid airborne BED items could fit the gross pool while consuming the recovery reserve, leading the next tick to fail. The validator and authority now share critical/auxiliary derivation, and the adversarial codec regression fits the gross pool but exceeds the reserved core by one body. Pending craft reservations are also checked.

Verification:

- Focused authority/lifecycle/deployment/physical/combat/mechanism/BALL/snapshot suite: 211 cases passed in the full rerun. Its one new adversarial test initially also exceeded the gross cap; correcting only its forged count produced a separate 1/1 PASS. No production source changed between these runs. Evidence: `TestResults/entity-lifecycle/lifecycle-stage2.trx` and `reserved-core-regression.trx`.
- Two earlier fixture-only failures were corrected: the long synthetic craft loop now acknowledges snapshots at the normal cadence, and an ownership test creates a real crafted BED instead of inventing an unsourced authored-map body.
- .NET Standard 2.1 production Rules build: PASS, zero warnings/errors, 8.45 seconds. DLL SHA-256: `81170B968DD146C326C93A86A662C6E362EAE8D36D0673B5AB6117DB02A8FFBC`.
- Tests compiled against that production DLL as a separate assembly: PASS, 11/11, six seconds. This includes all nine lifecycle cases, the reserved-core adversarial case and the corrected ownership case; all new budget tests compiled without internal production access. Evidence: `stage2-separate-assembly.trx`.
- Real flagship budget acceptance: PASS, 8/8 configurations and 22 authored alternatives covered in each race configuration. Source fixture and exact measurements: `FlagshipCapacityTests.cs`, `CAPACITY-RESULTS.md`; local result `flagship-stage2-capacity.trx`. These checks use the actual item catalogue and full-capacity rosters.
- `git diff --check`: PASS. No Unity, native physics, full played three-lap race or performance claim is made by this lane.

The historical 8,000-seed sweep used physical checkpoint `17c30b4` and factory `70723cd`; it remains generator/route evidence, not this pressure policy's evidence. The new acceptance harness has isolated output paths and does not overwrite that sweep.

Compiler released to the online route/spawn lane after all checks. Next recoverable action: root integrates this source checkpoint, binds `WordboundEntityBudget.Hash` as a distinct field in rules fingerprint version 2, replaces reconciliation's literal cap with `MaximumBodies`, and runs early content/workshop budget preflight plus native tests. No snapshot field, course schema or protocol change is introduced by this stage.

## Partial-catalogue native regression repair

Root's native batch passed 56 cases but all 16 GameplayPlan cases failed before exercising composition: budget reservation called `Get` for every allowed mode recipe, including absent BAT. Legal catalogues may contain only the available content. Reservation now filters the exact catalogue to enabled recipes allowed by the mode. The adjacent BALL startup check likewise validates thrown stats only for an available, enabled BALL. Required authored puzzle recipes still fail closed when absent or disabled; an available enabled BALL still requires valid supported thrown stats.

The approved policy descriptor explicitly adds `available-enabled-recipes` and advances entity-budget Version/hash prefix to 2. Its full-catalogue numeric reservations are unchanged. Root's rules fingerprint v2 already binds this hash, so old/new content bindings differ without a snapshot-field, course-schema or protocol-version change.

Verification:

- Production .NET Standard Rules build: PASS, zero warnings/errors, 6.9 seconds. SHA-256 `490372E344F223005657DE761F514606D2DBB3FAE30A46C46184E9137FD1A577`.
- Focused real NUnit tests referencing that separate production DLL: 10/10 PASS. Covers enabled/disabled/disallowed reservation inputs, all five modes with the actual proof-map recipe subset, required recipe absence/disablement, enabled BALL validation and existing budget refusal. Evidence: `TestResults/entity-lifecycle/partial-catalogue-separate.trx`.
- Exact integration GameplayPlan source and its unchanged tests, referencing the fresh Rules DLL: 16/16 PASS. Evidence: `TestResults/entity-lifecycle/partial-catalogue-gameplay-plan.trx`.
- `git diff --check`: PASS. No Unity was launched in this lane.

Next action: root integrates this repair independently of the new native six-family fixture, reruns affected native cases, and retains its current content-binding preflight. This lane now compiles only the additive physical fixture before handing the compiler slot to the online owner.

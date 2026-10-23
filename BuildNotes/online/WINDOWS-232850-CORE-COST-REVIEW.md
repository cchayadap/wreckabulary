# Windows 232850 core cost and mechanism identity

STATUS: SOURCE READY FOR INDEPENDENT REVIEW. Read-only audit complete; root approved one bounded mechanism-identity change. No compiler, core, probe, Unity or player job has run in this lane. Root's full FFA remains the sole heavy job. Existing ModeDefinition and historical untracked work are preserved.

## Actual player evidence

The successful supplemental visible run used QA build232641, source8c02f106, original artwork, one local Time Run actor and the exact workshop draft. The log SHA256 is `29D19B5D3D1C1F5B2556BC50CC0A28122E81DDEE63AB96D29219184BF8624674`. It completed all five workflow steps without console errors. This is not a replacement for the official gate or production performance acceptance.

The screenshot-excluding interval observed90 Update samples over7.240s, canonical tick39→518, maximum46 ticks in one Update and696.499ms maximum gap. Early gaps696/580/569ms accompanied46/42/35 ticks. Later148.759/151.590ms samples each carried only one tick, so accumulated simulation does not explain every gap. Assigned-camera89 matched callback spans total249.891ms/max6.895ms; they do not measure GPU completion or presentation. Settings remained60Hz simulation, timeScale1, unlimited selected frame cap/v-sync0,1280×720, MSAA4 and High shadows.

The wider ready window includes screenshot/menu work and is7.979185s, tick39→541,93 bodies/294 resources/216 mechanisms, valid capture/no open scopes:

| Inclusive phase | Calls | Total ms | Mean ms/call |
| --- | ---: | ---: | ---: |
| HostStep | 502 | 4783.849 | 9.530 |
| FullValidation | 520 | 1443.188 | 2.775 |
| Synchronize | 502 | 735.385 | 1.465 |
| ObserveHost | 502 | 600.249 | 1.196 |
| Prepare | 502 | 490.581 | 0.977 |
| BodiesValidation | 520 | 355.008 | 0.683 |
| ResourcesValidation | 520 | 268.378 | 0.516 |
| MechanismsValidation | 520 | 255.514 | 0.491 |
| ProgressRestore | 520 | 229.899 | 0.442 |
| BodyBudgetValidation | 520 | 135.545 | 0.261 |

These are inclusive Stopwatch elapsed observations, with nested scopes and combined validation callers. Do not add children to parents, divide the wider totals by the narrower frame interval, or equate their difference to GPU/GC/exclusive CPU. FullValidation remains the largest measured validation boundary; the proposed mechanism change concerns only a modest fraction of it.

## Actual local scheduling and preserved boundaries

LocalMatchClient calls the explicit CreateLocal configuration and starts GameMode.Single, one local player, Multiple-peer isolated physics. CreateLocal retains60Hz client/server selection and the original local heap. The online64MiB policy is not used by this path. Driver order900 calls PrepareHostPhysics; installed PhysicsAddon order1000 invokes BeforeSimulate, PhysicsScene.Simulate, then AfterSimulate synchronously. PhysicsWorld captures the actual bodies, invokes AdvanceHost and requires the resulting canonical revision to be applied before the next physics step. There is no project yield between those operations.

Thus multi-tick updates execute the observed per-tick work serially. This supports reducing real work instead of changing tick rate, timeScale or catch-up configuration. It does not identify every scheduler/render/native cost. Full reconciliation, rollback capture, mutable-rule revalidation, online authority, snapshot import/publication and deadlines remain necessary and untouched.

Synchronize still obtains defensive TeamProgress copies for CurrentLap/source predicates and repeats immutable supply lookup. Those are hypotheses inside its735ms boundary, not separately measured causes. BodyBudget still scans placements despite an immutable index; mechanism physical checks already fast-return on Waiting. No progress-query cache, validity cache or broad body/application rewrite is included.

## Smallest approved change

ValidateMechanisms begins by building invariant composite string keys from PuzzleId/Team/Lap and running ordinal Distinct. Replace only that transient identity operation with a value-tuple HashSet count, leaving the same initial Need/message and every subsequent mode/count/puzzle/team/physical/progression/economy check in place. Do not short-circuit enumeration or move this check ahead of Resources/Bodies/Thrown validation.

At216 mechanisms ×520 full checks, the old operation constructs112,320 composite keys in this window, plus its numeric formatting operations. This is an allocation/work-count argument, not a measured time saving. MatchMechanismState is immutable and constructor-bounded: PuzzleId excludes `/`, Team0..7, Lap0..2; snapshots freeze nonnull entries. Tuple default string equality is ordinal, matching the former invariant string identity. Different evidence/timing/solved fields must not make a duplicated identity unique. The set is local to one validation, never a retained dynamic cache.

Baseline-only alignment commit `9023da5d` copies the exact current root validator and fixture and MUST NOT be integrated. It only brings in root's already accepted resource tuple changes. Exact pre-edit hashes:

| Input/source | SHA256 |
| --- | --- |
| MatchStateValidation.cs | `10EF4AEDE04601DD5D9A942ED890B80576C328364AA411BC2514CCD538DA2EEE` |
| WordboundSnapshotValidationTests.cs | `5F56C6376CC4B04D9DB4FA4AC004A393310CFC430953F2E597E78763AA06C260` |
| ValidationProbe/Program.cs | `E5385C8BE93089745FB7AD59AF20CCAB7BA30E92178216DC97B7D8BAA9D0E582` |
| items.json | `79363C8F1F14E7ACA553718B552468D34B3140FE4BA88688FD4DED8330CF2416` |
| rules.json | `F183D9B79D1B438A78FB682455C6CE5E6B15CBD09D47B0831B3AF47B2F697D4C` |

## Required comparison, scheduled by root

Run the unchanged project-owned probe sequentially on root's before source and after the reviewed one-expression change, with no concurrent native/build load:

```powershell
dotnet run --project Tools/RulesHarness/ValidationProbe/ValidationProbe.csproj -c Release -- .
```

This uses actual item/rule files, effective Dibs→mode rules, flagship seed0123456789abcdef, two seats, TimeRun and TeamRace Solo, three batches of20 validations each. Keep all120 valid, all positive65536-byte calibration controls, unchanged input/snapshot hashes and every raw timing/allocation batch. It measures frozen full validation under .NET, not Unity throughput. This lane's unrelated ModeDefinition must never enter the comparison; use root's assembled source or the exact approved isolated projection.

Run root R0 and bounded SnapshotValidation/Authority/Lifecycle/Practice/EntityBudget core gates, then the same native public TeamSolo and visible workshop timing workload if the isolated change is retained. New regression coverage must establish identity across supported culture/team/lap values, same identity with differing evidence, earlier-error precedence and valid→invalid→valid isolation. Existing fixture-reference duplicate tests retain their earlier predicate SingleOrDefault error. Native counts/outcomes and any actual frame benefit remain pending.

## Implementation and focused cases

Production changes exactly one expression: a per-call tuple HashSet count replaces invariant composite-string Distinct count at the same first guard. No new persistent field, early exit or changed downstream guard was added. The existing MechanismsValidation phase still measures the whole unchanged method boundary.

Six cases are added to the existing snapshot-validation fixture (source count116, previously110): three culture cases compare supported team/lap/case-sensitive ID identities against the former invariant key semantics; two real TimeRun/Freestyle snapshots prove differing timing evidence cannot conceal a duplicate, that a genuine timing-only defect is later, and that missing resources still fail earlier; one actual FreeForAll snapshot proves duplicate identity still precedes its no-mechanisms mode rejection. Each retains valid snapshot checks and repeated invalid/valid checks. The existing referenced/unreferenced duplicate and moving-fixture cases remain unchanged.

Scheduled commands, only after root releases the active native workload:

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs core --filter 'FullyQualifiedName~WordboundSnapshotValidationTests|FullyQualifiedName~WordboundAuthorityTests|FullyQualifiedName~WordboundLifecycleTests|FullyQualifiedName~WordboundPracticeTests|FullyQualifiedName~WordboundEntityBudgetTests' --json
```

The unchanged standalone validation probe must first run before integrating this production expression, then after it with identical input hashes, source settings and no competing workload. Do not run the dirty lane's broader Rules projection as a substitute. Existing native20 collector cases plus the same public TeamSolo run establish diagnostic validity/gameplay, while a fresh visible workshop scenario supplies actual player cadence. These checks are pending, not inferred from source review or `git diff --check`.

IO diagnostic closure: independent source PASS on a479b540, including identical relative target arguments and absolute ownership guards. Parent/reviewer subsequently report2332313/3 PASS for the original Store regression, actual Store-payload replacement control and corrected PLATE capacity preflight. No new production IO workaround is proposed; prior intermittent1175 remains unattributed.

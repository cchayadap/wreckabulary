# Static validation metadata cache

STATUS: SOURCE COMPLETE; independent cache source review PASS against `0df3cab`; execution pending. The measurement design below was committed before production edits (`f5a914a`). Root owns all heavy execution.

The completed physical profile in `Logs/gate_test_editmode_20261023T150959Z.log` reports 3,481 reconciliation invocations across 64 sampled frames: Validate 127,330.950 ms, PhysicsApply 130,675.734 ms, Presentation 1,613.030 ms, Advance 30,770.228 ms and Capture 12,677.063 ms. These inclusive scope totals are not additive wall time. Physical had no buffer wrap; public marker retention was incomplete and must not be treated as zero CPU.

## Baseline measurement contract

Deliver the standalone benchmark before production edits so root can execute it against unchanged validation. Load the checkout's actual `Assets/_Project/Data/Config/items.json` and `rules.json`; create the actual flagship with seed `0123456789abcdef`; derive rules with the same `mode.CreateGameplayRules(book.For("Dibs"))` path; construct valid TimeRun/Solo and TeamRace/Solo matches with two seats and freeze their initial snapshots. Use real Rules source and real rules fingerprint code, not a proof course, stub or copied validation implementation. This benchmark binding identifies its local deterministic fixture and is not a public admission certificate.

Warm validation and then measure three bounded batches of 20 validations for each frozen frame. Emit each batch's elapsed time, current-thread managed bytes, validation error count, and positive 65,536-byte allocation controls before/after. Emit exact item/rule/effective-rule/geometry/mode/course/manifest/binding/snapshot hashes, seed, roster, tick and section counts. Require positive calibrated bytes, unchanged snapshot hash and zero validation errors. Timing is informational with no pass threshold. Run the same benchmark binary sources, actual data and frame identity before and after the cache; compare the repeated-batch evidence without claiming Unity throughput from .NET numbers.

## Bounded implementation contract

Use weak exact-identity course/mode keys and privately published immutable derived metadata. Cache only static route/source/placement metadata, lifecycle and internal entity-budget analysis. Every invocation still checks current binding, snapshot, roster, mode/course match, mutable catalogue/rules and all dynamic actor/economy/resource/mechanism/physical invariants. No cached successful validation, bypass overload, wire/schema/content-hash change or global strong dictionary. Prove used course/mode properties are immutable public wrappers and keep mutable Rules/ItemCatalogue values outside the cache. Tests must cover cache reuse with valid and invalid later snapshots, changed mutable rules/catalogue, exact course/mode separation and unchanged error behavior.

## Baseline retained

Root executed the actual flagship probe against unchanged validation and committed its logs/JSON at `e5ab4ad`, under `BuildNotes/online/validation-baseline-20261023`. All 120 validations passed with unchanged snapshot hashes and 65,560-byte controls. TimeRun had 94 bodies/57 items/294 resources/216 mechanisms and about 52 MB per 20 validations; the three batch times were 474.835/426.254/513.340 ms. TeamRace Solo had 186 bodies/114 items/588 resources/432 mechanisms and about 100.86 MB per 20 validations; times were 1312.239/597.708/468.516 ms. Time variability is explicit. The same unchanged probe/data must run after the cache; do not infer Unity performance from these numbers.

## Implemented narrow delta

Only `MatchStateValidation.cs` changes production behavior internally. A ConditionalWeakTable keys exact ResolvedCourse instances; each course has a second weak table keyed by exact ModeDefinition instances. The nested private value publishes Route, Sources, Placements, the internal static-only WordboundEntityBudget and WordboundResourceLifecycle after construction. No validity answer or snapshot is stored. Binding/hash/roster/play/counter checks still precede the cache lookup. All existing dynamic validators run in their existing order, with live catalogue/rules inputs; both Reconcile and PhysicsApply still invoke full validation.

Immutability proof for retained values: Route reads the public immutable Laps/checkpoint wrappers and creates RaceLap/Checkpoint objects that clone their input arrays. Public Route still allocates a separate array; the cached array is never exposed. Sources contains newly allocated tuples of immutable IDs/letters/team flags; Placements refers only to immutable ResolvedCoursePlacement values. The internal two-argument entity-budget constructor reads immutable course collections and ModeDefinition values, then uses its private dictionaries/sets only for lookups. The full geometry/items/rules budget constructor is not cached. Lifecycle likewise retains only immutable course/mode-derived consumer/lap data; each Demanded call reads the supplied current progress/resource/practice/mechanism state. No cached value reads mutable internal ResolvedCourse.Data; live course hashes remain checked each call.

The [ConditionalWeakTable contract](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2?view=netstandard-2.1) uses reference identity and permits key collection even when its associated value references that key. Thus the course closure/lifecycle does not create a strong global retention path, and old mode instances are also weak keys. Concurrent construction may compute temporary values, but GetValue publishes a complete value atomically; retained dictionaries are never mutated after publication. No static dictionary of arbitrary workshop content is introduced.

Thirteen regressions were added to `WordboundSnapshotValidationTests` (41 total): five modes reject a later missing-body snapshot after warmup and still accept valid old/new frames; three current rules mutations and two pending-recipe mutations are observed despite unchanged object references, then recover after values are restored; route/document-copy edits cannot alter cached metadata; different/equivalent mode and different/null course inputs retain binding checks; and 16 concurrent validations cannot observe partial metadata. Existing forged wire/progress/physical/budget tests remain unchanged. New tests are not yet executed.

Root-only sequence after source review:

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs core 'FullyQualifiedName~WordboundSnapshotValidationTests|FullyQualifiedName~WordboundSnapshotCodecTests|FullyQualifiedName~WordboundEntityBudgetTests|FullyQualifiedName~WordboundPhysicalEvidenceTests|FullyQualifiedName~WordboundLifecycleTests|FullyQualifiedName~WordboundPracticeTests|FullyQualifiedName~WordboundMechanismTests' --json
dotnet run --project Tools/RulesHarness/ValidationProbe/ValidationProbe.csproj --configuration Release
```

Retain stdout/exit codes and compare exact input hashes/counts with the baseline. Then rerun the same native physical/public cases with the corrected per-invocation CPU recorder and unchanged behavior/deadlines. No pass or speedup is claimed until those gates complete.

Integration warning: `8cfd475` is an excluded lane-only copy of root's current MatchStateValidation baseline. Integrate only the subsequent cache/test/report delta. The original unrelated dirty ModeDefinition is untouched and excluded.

Independent review confirmed the current root's partial budget/lifecycle remain read-only after construction, MatchProgress copies the private route array, and all dynamic validations remain live and ordered. Eager static construction could change the first error for an internally fabricated malformed ResolvedCourse; public resolver/import validation prevents that unsupported state from being published. No blocking supported-path finding was identified. This source verdict does not claim compiler, core, benchmark or native success.

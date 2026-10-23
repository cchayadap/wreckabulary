# Validation hot path and trust boundaries

STATUS: SOURCE REVIEW COMPLETE; native CPU attribution pending. Reviewed integration `c76086187c45eac5db2e77996f37cf4aedff64b3` while root runs the native profiler batch. No source optimization, compiler, native job, deployment or validation-policy change is performed by this lane.

The objective is to identify duplicate work over exact immutable inputs without weakening full validation at file, network, checkpoint, migration or public API boundaries. Source call counts are not measured costs; wait for the root profiler evidence before selecting any implementation.

## Exact repeated work

Paths below are relative to `Assets/_Project/Scripts/` in the reviewed integration checkout.

| Path | Observed sequence | Interpretation |
| --- | --- | --- |
| `Online/Gameplay/WordboundGameplayController.cs:273` and `Online/Physics/WordboundPhysicsWorld.cs:113` | Reconcile validates `next` at line279, creates/removes/configures bodies, then ApplySnapshot validates the same object again at Physics line116. | Two full state validations per normal completed host tick, plus startup/presence retries. The first protects scene mutation; the second protects the independently callable physics API. Do not simply delete either. |
| `Rules/Wordbound/AuthoritativeMatch.cs:249` | Publish captures a new sequence, calls EnsureValid, Export, and ComputeHash. | Separate publication validation; EnsureValid also checks actual prop/tile letters against the geometry catalogue at line1011, beyond MatchStateValidation. |
| `Online/Fusion/WordboundFusionSession.cs:616` | Publish is followed by ValidateSnapshot before checkpoint preparation/upload. FusionAuthority line78 imports, recomputes canonical hash and validates again. | Two full validation passes on a newly published host state, plus serialization/parsing. This occurs at checkpoint cadence, not every physics tick. |
| `Online/Fusion/WordboundFusionSession.cs:733` and `Online/Gameplay/WordboundGameplayController.cs:360` | An assembled received packet is validated/imported by the authority, then ReplicaReceived makes the controller import/validate the same JSON again. | Network boundary validation must remain. Typed reuse would require an exact immutable validated result and matching dependency contract; current string event provides no such proof. |
| `Online/Fusion/WordboundFusionSession.cs:275` and `WordboundFusionAuthority.cs:88` | Downloaded head gets ValidateSnapshot; RestoreCommitted imports again; AuthoritativeMatch.RestoreCommitted calls EnsureValid again. | Three full passes during recovery, with distinct committed mark, epoch/frontier and extra geometry checks. This is startup/recovery work, not a normal per-tick loop. |

`MatchSnapshotCodec.cs:25–58`: Export encodes the unhashed envelope for its hash, then encodes the final hashed envelope. Publish separately calls ComputeHash, constructing/encoding the unhashed envelope again. The subsequent import parses, rebuilds and hashes canonical state again. Any eventual reuse must return the hash computed from exactly the exported bytes/state, retaining Preflight limits and all format/schema/binding checks. The current eight CPU markers do not isolate publication serialization; unexplained total time is not evidence that it is the bottleneck.

## Work inside every full validation

`Rules/Wordbound/MatchStateValidation.cs` currently performs all of the following on each invocation:

- Lines29–35 construct a new route and MatchProgress, a new Economy with restored deep copies, and a new CommandJournal, then validate/restore the snapshot into each. These are dynamic validity checks; reusing mutable scratch validators would require careful reset/error/reentrancy proof.
- Line84 constructs `WordboundEntityBudget(mode, course)`. Its constructor (`WordboundEntityBudget.cs:33`) rebuilds supply/consumer/lap indexes and calls CriticalPeak at line92. CriticalPeak explores up to 16 completed-puzzle subsets at each checkpoint and each source team. This depends only on course/mode, not the changing snapshot or mutable gameplay rules. AuthoritativeMatch already retains its own budget; validation reconstructs another each time.
- Lines106,221,266 separately construct three `WordboundResourceLifecycle` objects from the same course/mode, each rebuilding supply-to-puzzle consumers and lap sets (`AuthoritativeLifecycle.cs:14`). Sources(course) is also rebuilt at lines220 and265.
- Bodies/items/tiles/teams/mechanisms are indexed or scanned independently in several helpers. They are dynamic per-snapshot data. A private per-invocation lookup context can share indexes while retaining every assertion; a long-lived successful-validation cache cannot substitute for them.
- `MatchPhysicalValidation.cs:37` reads `snapshot.Economy.Items` inside physical mechanism checks, cloning the full public item view each time. Rules code already has internal read-only use of ItemStates elsewhere; an internal indexed view could avoid these copies without exposing mutable arrays or changing the public defensive getter.

These are confirmed source operations, not measured allocation/time percentages. Validation's budget analysis may be important on large authored race content, but the profiler result must establish where to start.

## Immutability and boundary limits

`ModeDefinition.cs` exposes immutable mode fields/read-only recipes. `CourseCatalogue.cs` owns read-only dictionaries containing immutable definitions. `CourseManifest.cs:198` exposes resolved arrays/read-only entries copied from the document; CoursePoint/CourseBox are readonly structs, physical sites/fixtures are immutable wrappers, and CopyDocument returns a clone. A course/mode-derived lookup can be retained privately for that exact resolved instance. Its internal `Data` is still mutable to Rules code, so do not introduce mutation after publishing a resolved course or authorize reuse merely by a user-supplied ID/hash.

`MatchState.cs:189` freezes/sorts the snapshot's collection containers. Bodies and actors are immutable state objects. `EconomySnapshot.cs` captures private copies and its public Items/Players/Tiles getters return new defensive copies; ProgressSnapshot copies player/team state. However, Rules has internal mutable state accessors/arrays. A blanket process-wide cache that treats any MatchSnapshot reference as permanently certified would need a stronger invariant than these public APIs alone provide.

Most critically, `Catalogue.cs:88,135` exposes mutable ItemDefinition fields/lists/arrays and permits ItemCatalogue.Add; `GameRules.cs:6` exposes mutable fields and nested statistics. Controller/FusionAuthority/Physics clone rules at setup but share the item catalogue; Physics publicly exposes its cloned Rules object. Reusing a previous validity answer based only on snapshot/course/catalogue/rules reference equality could miss a changed recipe, capacity, deployment lifetime or defense rule. Reconcile also invokes scene/body callbacks between its first validation and ApplySnapshot. Do not add a trusted boolean, skip-validation overload, hash-only certificate, or broad mutable dependency cache.

Untrusted entry points retain full validation: CourseJson document/manifest imports and resolved geometry/mode validation; MatchSnapshotCodec.Import byte/depth/token/schema/canonical-hash/full-state checks; signed network roster/content binding; checkpoint packet hash and exact committed mark; migration epoch/term/frontier fences; public ApplySnapshot's host, monotonic epoch/tick, body identity/seat/mode and canonical-state checks. Source-derived indexes optimize the implementation of these checks; they do not make external input trusted.

## Smallest candidate after profiler attribution

If WB.Match.Validate plus the validation portion of PhysicsApply dominates, first retain only private derived course/mode metadata: route, source/placement/fixture maps, one lifecycle, and the static internal entity-budget result. Pass an explicit context bound to the exact resolved course and mode (and binding where appropriate); preserve the existing public Validate API as a full checked path. Validate every changing snapshot and current item/rule dependency on every invocation. Start with the budget/lifecycle construction rather than removing a validation pass or changing the snapshot wire contract.

If dynamic scans dominate afterward, build one private per-validation ledger index and share it across existing helper checks. Keep null/duplicate/missing-ledger rejection and error semantics. Do not reuse a success/failure answer across frames. Reusing exported hashes or typed import results is a separate later change with separate boundary tests, justified only by measured serialization cost.

Meaningful follow-up verification for an approved implementation: identical acceptance/rejection across all five modes and race formats; changed frame/epoch and copied-body identity; malformed/duplicate/missing actor, inventory, resource, platform, mechanism and physical evidence; mutated item definition or GameRules after context creation still changes validation outcome; another course/mode context rejected; original network/import/physics public paths still reject invalid input. Run the affected bounded `WordboundSnapshotValidationTests`, `WordboundSnapshotCodecTests`, `WordboundEntityBudgetTests`, `WordboundPhysicalEvidenceTests`, `WordboundLifecycleTests` and `WordboundPracticeTests`, plus the native 25 gameplay-plan cases and the same public/physical profiler scenarios. Do not invoke the unfiltered seed sweep. Root chooses exact filters and owns all heavy gates.

No implementation is authorized by this report. Next recoverable action: obtain the complete native profiler records, compare Validate/PhysicsApply/Advance/Presentation/BuildWorld and observed frame gaps, then agree a narrow source scope before editing.

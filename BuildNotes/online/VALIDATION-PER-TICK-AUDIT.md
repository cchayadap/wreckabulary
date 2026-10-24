# Per-tick validation architecture audit

STATUS: IMPLEMENTED, SOURCE REVIEW PENDING — no compiler/test/probe jobs run here.

Baseline inspected: integration 26c41802, including the reviewed body-budget
placement index and presentation tuple cache. Scope is the actual authoritative
capture/reconciliation/publication/import call graph, current immutable caches,
and a smallest next proposal that preserves mutable and external admission checks.
Root owns compilation, tests and measurements. Unrelated lane dirt is preserved.

## Actual validation boundaries

`AuthoritativeMatch.Advance` captures a rollback snapshot, applies its command,
physics and lifecycle rules, synchronizes and checks capacity. `Capture` freezes
state; it does not produce a full validation verdict. The successful per-tick
path is `AdvanceHost → Advance → CaptureFrame → Reconcile`.

`WordboundPhysicsWorld.ReconcileAndApplySnapshot → MatchStateValidation.ReconcileAndApply`
fully validates against controller rules, invokes the synchronous preparation
callback, then freshly validates mutable dependencies against the distinct
physics-world rules before applying. The callback may mutate catalogue/rules,
abort, stop or reenter. The second check is necessary. Public `ApplySnapshot`
continues to require full validation independently.

Publication has additional boundaries: `AuthoritativeMatch.PublishSnapshot`
captures, fully validates, exports and hashes; `WordboundFusionSession.PublishCheckpointCore`
then invokes `WordboundFusionAuthority.ValidateSnapshot`, whose bounded codec
import reconstructs and fully validates the actual serialized state before
checkpoint preparation. Local publication uses the same bridge. Received and
restored snapshots also pass codec/import validation and exact mark checks.
Restore additionally checks authoritative state and pinned placement letters.
No external admission, serialization, publication, rollback or migration check
is proposed for removal.

A cache keyed only by match/epoch/tick is unsafe: same-tick presence/result/ACK
transitions exist and mutable rules/catalogue can change independently. An exact
snapshot reference also cannot establish that its current mutable dependencies
remain valid. A trusted capture is not an existing full-validation certificate.

## Existing work, not new proposals

The current weak course/mode `StaticContent` already retains route, source,
placement, physical-site/fixture/puzzle/geometry/connection indexes, static budget
metadata and resource lifecycle. Public route/source results remain independent.
Resource/mechanism tuple uniqueness and per-call source/mechanism lookups already
exist. Immutable resource/mechanism `SortingKey` fields avoid repeated capture key
construction. Synchronize team copies and ObserveHost mechanism grouping are
already scoped to individual calls. BodyBudget now uses the existing placement
index. None stores a dynamic snapshot validation verdict.

## Measured scope and limits

Exact `PublicTeamRacePreservesWholeSoloParties` output in
`TestResults/editmode_20261024T010750Z.xml` (source `95219ddd`) failed the client
movement deadline after 43.506172 s. Its valid ready capture spans 12.718925 s,
tick 88→555, 186 bodies, 588 resources and 432 mechanisms, invalid=None/inFlight=0.

| Region | Calls | Inclusive elapsed ms |
| --- | ---: | ---: |
| HostStep | 467 | 7,767.111 |
| FullValidation | 481 | 2,260.283 |
| MutableValidation | 467 | 577.418 |
| EconomyValidation | 948 | 423.771 |
| ProgressRestore | 481 | 293.557 |
| Resources | 481 | 437.026 |
| Bodies | 481 | 662.382 |
| Mechanisms | 481 | 244.757 |
| BodyBudget | 481 | 280.109 |

Economy is nested in both full and mutable validation; other child scopes are
nested in full validation. These are elapsed observations across all owner-thread
instances, not exclusive CPU. Full includes additional publication/import
contexts. The whole-case Reconcile recorder is 556 calls/5,212.491 ms in a
different window. Do not add these totals or infer FPS. Current BodyBudget and
tuple changes are newer than this measurement. Root subsequently reported
233/233 focused core PASS; this lane ran no checks.

## Smallest next change: omit discarded economy installation

This revives the explicitly unimplemented candidate in
`PER-TICK-COST-REVIEW.md`, rather than introducing a new cache. `ValidateEconomy`
creates a temporary Economy, adds the required roster and calls `Restore`.
`Restore` first calls `ValidateReplacement`, which creates and validates another
Economy, then clears and repopulates the temporary destination dictionaries and
assigns its scalar frontiers. The destination is immediately discarded.

Add an internal validation-only entry beside public `Restore`: preserve the
same null guard and call the same complete `ValidateReplacement`, discarding only
its successful result. Switch only the temporary validation caller. This removes
the final destination clear/copy/frontier-assignment stage, not replacement
construction, item/inventory copying, audit or any validation. The constructor
itself only stores references, so caching Economy construction is not proposed.

Required invariants:

- Preserve original null exception, first-error type/message and counts→roster/
  reservations→items→tiles→conservation/ownership→held-category order.
- Every invocation reads current rules and catalogue; neither replacement nor
  verdict survives the call. Initial and post-callback rules stay distinct.
- Public Restore retains atomic replacement and exact destination behavior;
  rollback/import consumers continue using it. No callback/event is introduced.
- Validation-only success/failure leaves its destination and supplied snapshot
  unchanged; malformed duplicate keys remain rejected at the original point.
- Callback exceptions remain outside validation's exception-to-message filter;
  preparation abort and fresh mutable rejection still prevent apply.

Meaningful coverage: compare original Restore with the new entry over real
pending/cancelled/completed crafts, held/deployed/world/destroyed items and tiles;
compare malformed count, roster, reservation, item, duplicate tile, conservation
and ownership failures including competing errors. Retain source bytes/state and
destination state before/after. Exercise current catalogue/rule changes between
valid→invalid→valid calls and within atomic prepare; require rejection before
apply with the original error. No tests should merely assert a new method exists.

Root authorized this bounded implementation after the audit. Exact write scope:
`Rules/Wordbound/EconomySnapshot.cs`, `Rules/Wordbound/MatchStateValidation.cs`,
existing `Tests/Rules/WordboundEconomySnapshotTests.cs` and
`Tests/Rules/WordboundAtomicValidationTests.cs`, plus this report. Baselines will
be checked against integration before edits; any sync-only commit is excluded.

The removable copy is only an unmeasured subset of EconomyValidation's 423.771 ms.
Measure the unchanged actual-flagship ValidationProbe before/after with all
input/output hashes, counts, positive byte controls and raw elapsed samples;
then use the same native public and Windows ready scenarios. This predicts less
work, not a guaranteed useful wall-time gain or an online fix. Root owns execution.

## Remaining immutable candidate, deliberately deferred

`MatchProgress` construction repeats roster validation/copies, route-array copying
and DefinitionHash length-prefix serialization/SHA256 on every full validation.
`StaticContent` caches the route but not that combined definition. A future
internal immutable descriptor must include exact mode, ordered route and roster
IDs/team/slot; course/mode alone is insufficient. A weak exact-binding entry under
the existing course/mode cache could retain it without retaining arbitrary
matches. Public constructors accepting caller-owned enumerables must keep their
current validation/copy semantics. Build only at the original constructor point,
cache no failed construction, and create fresh mutable progress dictionaries.
All dynamic Restore checks must run every time. Input independently confirmed
this is a prior hypothesis, not an implemented cache.

That is a broader constructor/lifetime change than the economy copy removal, and
the current ProgressRestore scope combines construction with dynamic Restore.
Do not claim the whole 293.557 ms is removable. Root explicitly deferred this
cache. Reusing same-snapshot body indexes across atomic phases is also broader
than the selected change and is not bundled here.

Next recoverable action: implement the approved economy-only delta, obtain
independent Input source review, then hand exact commits/filters to root for
compilation, correctness and comparable measurement. No runtime edits or jobs
were performed during this audit.

## Approved implementation checkpoint

The four scoped files matched integration `6eed388b` byte-for-byte before edits:
EconomySnapshot `6f130cbc`, MatchStateValidation `87ad6c02`,
WordboundEconomySnapshotTests `e33b9ee2`, WordboundAtomicValidationTests `707afb7a`.
No baseline-only synchronization commit is needed. Other lane sources remain
stale/unrelated; integrate this delta, never replace complete root files.

Production is one internal six-line entry and one caller change. `Restore` and
`ValidateReplacement` are unchanged, including all null/current-dependency/error
checks and their order. Only the disposable validation Economy calls the new
entry. Required roster setup still runs first.

Eleven added cases use public runtime APIs through the existing full/atomic
validators, with public Restore as the failure and reconstructed-state oracle.
There is no new friend assembly, reflection or internal-setter dependency:

- One real authoritative BED lifecycle retains initial, pending, cancelled,
  held, deployed and consumed snapshots, compares public Restore output, and
  revalidates the old snapshots without mutating the current authority.
- Seven invalid economy variants cover counts, roster, slots, orphan reservation,
  craft timing, ownership and conservation. Exact Restore errors must precede a
  competing missing-resource error; repeated failures and later valid snapshots
  retain bytes. Public Restore's failed destination and notification count remain
  unchanged. The temporary validator destination is intentionally not exposed.
- Three atomic cases mutate enabled recipe, required letters or inventory
  capacity during preparation, add a competing later health error, require the
  exact fresh Restore error before apply, then restore dependencies and apply the
  original immutable snapshot successfully. Existing callback/abort/five-mode,
  thrown/deployment and mutable-rule tests remain relevant.

Required root verification: R0; focused core filter
`WordboundEconomySnapshotTests|WordboundAtomicValidationTests|WordboundSnapshotValidationTests`
(actual discovered case count must be read from the result); unchanged
ValidationProbe before/after with positive controls/hashes; same native/public
and Windows ready workload where scheduled. No allocation, elapsed-time or
online-acceptance improvement has been measured for this candidate.

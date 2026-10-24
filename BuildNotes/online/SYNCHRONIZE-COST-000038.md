# Synchronize: bounded team-copy proposal

STATUS: SOURCE AUDIT COMPLETE; proposal only. No runtime edit, benchmark, Unity job, cadence or guard change. The optional Advance measurement source is a separate follow-up and must run on unchanged production before any optimization is authorized.

## Exact evidence

Read only the output of `PublicTeamRacePreservesWholeSoloParties` in root `TestResults/editmode_20261024T000038Z.xml`; synthetic diagnostic cases are excluded. The ready generation2 capture is valid (`invalid=None`, `inFlight=0`), covers26.2531888s and canonical25→865, with186 bodies,588 resources and432 mechanisms at both ends.

| Existing scope | Calls | Inclusive elapsed ms | Elapsed ms/call |
| --- | ---: | ---: | ---: |
| HostStep | 840 | 17,248.050 | 20.533 |
| Synchronize | 840 | 3,022.871 | 3.599 |
| RetireResources, inside Synchronize | 840 | 325.145 | .387 |
| ObserveHost | 840 | 2,701.343 | 3.216 |
| FullValidation | 862 | 4,465.928 | 5.181 |

These are thread-owned inclusive Stopwatch observations, not CPU or FPS. Nested totals cannot be added, and3.599ms is not an estimate of removable work. Public movement failed in this case; the missing client SDK batches are separately investigated. Nothing here attributes that failure to team copies.

Inspected root HEAD `669b08fe5a626823a3aa50e7156e6f04548aab54`:

- `Assets/_Project/Scripts/Rules/Wordbound/AuthoritativeMatch.cs`, SHA256 `3130A6F5F6A934BB1CD4B6041B3B190B5B029646846D6FDDC876A89CABF1DA0E`.
- `Assets/_Project/Scripts/Rules/Wordbound/MatchProgress.cs`, SHA256 `CBE5D6909DD6B441216314C3F6D9E4A4FB5CB6124BEA8105B4EFF090C4A4C810`.
- Existing `Tools/RulesHarness/ValidationProbe/Program.cs`, SHA256 `E5385C8BE93089745FB7AD59AF20CCAB7BA30E92178216DC97B7D8BAA9D0E582`.

## One candidate and its boundary

Use a lazy dictionary of defensive `TeamProgress` copies local to the existing resource loop in `Synchronize`. Read `progress.Team(id)` once per actually queried race team, then reuse that copy for the loop's current-lap and availability predicates. Keep missing-team/null behavior and the original race/team short-circuits; do not eagerly index all teams or move a failure before the original first query.

`MatchProgress.Team` currently copies every scalar and the ordinal puzzle HashSet. `CompletedPuzzles` additionally sorts/copies that set into a public read-only list. The current loop queries a team once per resource for `CurrentLap`, again for each current-lap `Demanded`, and again for every evaluated prerequisite. For the recorded two-team/three-lap shape this means588 current-lap queries and196 current-lap demand queries before prerequisite queries. This is a source operation count, not measured savings. Reuse the existing internal `HasPuzzle` on the defensive copy instead of materializing its sorted public getter; both use ordinal string membership. Public getters/export order remain unchanged.

Keep generation/source ordering, resource recovery/reset order, live `resourceLifecycle.Demanded`, practice selection and mechanism arguments, source/prerequisite enumeration, and all short-circuit guards unchanged. Cache only the current team copy within this one invocation. Do not cache demand verdicts, `before` state, catalogue/rules, source arrays, body membership or any validation result. Do not alter `CurrentLap` used by `ScheduleSource`, `SourceCapacity`, `UpdateKeepsakes`, retirement, or the previous-snapshot `WasSourceAvailable` query.

The original `SourceAvailable(state)` is also called during constructor minting. Prefer retaining that entry and supplying a private overload for the Synchronize loop; explicitly preserve constructor behavior. A helper that changes both callers would require initializer parity evidence and a stated wider scope. No public progress API or direct mutable team exposure is needed.

## Mutation and error-order proof to retain in implementation

1. `Advance` updates progress before `Synchronize(before)`. Synchronize handles player recovery/permissions and updates mechanisms before its resource loop. RetireInactiveResources precedes the loop. The copy must be obtained at the original loop query point, after those phases.
2. Within that loop, `MintSource`, `RecoverSource` and failed `SourceCapacity` mutate economy tiles, bodies and resource entries. They do not mutate progress or execute external callbacks. `MintTiles` validates/adds letters and increments Minted; it does not invoke `ItemChanged`. Optional reservation is called with recycling=false, so it cannot enter retirement callbacks. No new yield/async/callback is introduced.
3. Dynamic practice/mechanism demand must still run for every resource at its existing position, even though team progress is stable. Previous snapshots remain separate immutable evidence; replacing them with current copies would break transition minting.
4. The resource loop keeps its original `OrderBy(Generation).ThenBy(SourceId, Ordinal).ToArray()` and state writes. Null/missing team results must not turn into a new eager dictionary error. Any exception still reaches the original Advance rollback and `inTick` finally path.
5. The dictionary dies at method exit. The next Advance, public restore, rollback/retry or constructor invocation must read fresh progress. No authority or lifetime fence changes.

Repeated immutable `course.Supplies.FirstOrDefault` is another candidate, but is deliberately deferred. A broad supply dictionary would add construction and duplicate/error-order considerations unrelated to this change.

## Meaningful verification

Use existing lifecycle/practice coverage, including completed-source retirement versus rival supply, shared consumers, real lap transition/refill, committed restore, and practice selection/rollback. Add only missing behavioral boundaries: source availability changes on the same teammate solve; a subsequent invocation observes new completed-puzzle/lap state; rival team remains unchanged; rollback then retry and committed restore preserve availability and prior snapshot bytes. Exercise both race modes and non-race/practice so no eager team lookup is introduced. Compare exact retained snapshots/export hashes, not a copied implementation of the lookup.

Existing ValidationProbe's default frozen validation and `--capture` do not execute Synchronize in their measured loops. They cannot establish this candidate's benefit. Prepare an optional `--advance` mode without changing those defaults: actual flagship seed0123456789abcdef, actual item/rule sources and effective rules, TimeRun/TeamRace Solo with two seats, bounded real Advance batches, ordinary publish/ACK outside measured batches before the120-tick guard, deterministic wall-clock inputs, full validation and canonical hashes outside timing, positive allocation controls and raw samples before assertions. Measure whole Advance allocations/elapsed, explicitly including rollback capture and other unchanged core work, without claiming native simulation or Synchronize-only CPU.

Root must execute that exact source first on unchanged production, then after a separately reviewed candidate, with identical inputs/start/end hashes and counts. Repeat the existing exact public native workload with phase capture for the inclusive Synchronize comparison, and retain behavioral outcomes. No deadlines, auth, tick rate, durable window, SDK, kit or global configuration change. No speed or FPS claim until the comparison is actually run.

## Recoverable next action

Root reviews this proposal. The online lane may prepare the optional probe source separately; no CPU job while root's native/Windows work is active. After the baseline is recorded and source scope approved, implement only the local team-copy reuse with the above regressions. Preserve existing dirty ModeDefinition and diagnostic logs.

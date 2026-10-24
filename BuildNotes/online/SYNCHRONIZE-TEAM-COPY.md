# Per-pass team copies in Synchronize

STATUS: INDEPENDENT SOURCE PASS for `70cd4c6b`; compilation/core/probe/native candidate execution NOT RUN. Root authorized this source slice after its unchanged-production Advance baseline passed at `cd4ec714`. No lane CPU jobs. Runtime edits are limited to the resource loop in AuthoritativeMatch plus a private supplied-team availability overload; focused lifecycle/practice regressions and this report accompany it.

Baseline source: AuthoritativeMatch SHA256 `3130A6F5F6A934BB1CD4B6041B3B190B5B029646846D6FDDC876A89CABF1DA0E`; lifecycle fixture `BE85490982D1AAC7237A820140CD424F3C9A71F8CD3B0F0DC9784906CCF5115E`; practice fixture `74C7152155D1E858519CE51704AA1474784495E46687176008D1A7D3DE09977C`. Baseline-only commit `7d5d35a9` supplies the previously absent exact root practice fixture and MUST NOT be integrated. Other synchronized paths were already byte-identical. Preserve unrelated ModeDefinition/ProtocolV3/logs.

Root evidence: `.harness/delivery/advance-probe-before-0029.log`, its `-source.json`, and `BuildNotes/client-view/advance-probe-before-20261024.json`: six batches,360 measured advances plus64 warmup, all valid hashes/counts and65560-byte controls. TimeRun60-step batches each15,623,448 allocated bytes and76.750/78.005/79.348ms; TeamRace31,200,584/31,200,584/31,200,744 bytes and237.306/331.591/420.266ms. These are whole idle Advance measurements, not native physics or FPS. Candidate comparison is NOT RUN.

The implementation must preserve the proposal's exact resource ordering, first team-query point, missing-team null behavior, live demand/practice/mechanisms and previous-snapshot reads. Cache only defensive copies for this invocation, after retirement. Constructor availability and other CurrentLap/SourceCapacity callers remain unchanged. No supply cache or persistent verdict. Independent review and root R0/core/probe/native comparison remain required.

## Implemented boundary

`ResourceTeam` is a local function with a lazy per-invocation dictionary. The first query still occurs where the original `CurrentLap` queried a nonnegative race team; a negative race team, if encountered, is first queried at the original availability point. A null team copy is retained as null, preserving the existing later dereference rather than substituting an eager missing-key failure. Non-race modes never instantiate the dictionary or read a team through this path. No lambda/delegate passes the cache outside the invocation.

The new private `SourceAvailable(state, team)` overload retains the original Demanded-first/PracticeRecoverySource/prerequisite short-circuit order, reading live practice and mechanisms each time. Its prerequisite membership uses `TeamProgress.HasPuzzle`, the same ordinal HashSet backing the old sorted public `CompletedPuzzles` copy. The original one-argument constructor helper remains byte-for-byte unchanged; so do SourceCapacity, ScheduleSource/CurrentLap, previous-snapshot WasSourceAvailable, retirement and all publication/validation/rollback paths. The resource sort and writes remain in the same order.

No progress mutation or external callback occurs between the first and last resource-loop team read: mint/recover touches economy tiles, resource entries and bodies; MintTiles emits no ItemChanged; SourceCapacity optional reservation uses recycle=false. The next Synchronize/restore/retry obtains fresh copies. This removes repeated defensive copies in one pass; it makes no measured timing or FPS claim.

## Behavioral regressions

- Three new cases (`TimeRun`, `TeamRace`, `BrawlRace`) explicitly gate FanSupply behind BedBridge. Real collect/craft/deploy/UsePuzzle commands establish rollback on a throwing host query, then successful same-tick mint of exactly FAN with the expected frontier/generation. TimeRun uses the other shared teammate to solve. Rival IDs/supply remain unchanged. Subsequent ticks do not remint; restoring the exact older durable checkpoint into the same authority removes the prerequisite again, and a fresh epoch command unlocks exactly one batch. Retained snapshots remain byte-identical.
- The existing full three-lap lifecycle case now uses that explicit supply prerequisite and checks each fresh lap clears completed puzzles, mints BedSupply while FanSupply stays locked, and preserves the prior snapshot bytes.
- The existing practice rollback case now retries the same command successfully, verifies retired old loose recovery, selects the next station through the other player, and checks its three FAN tiles without race puzzle progress. It preserves the retained snapshot and validates through normal publication/import.

Declared fixture counts: lifecycle13, practice16 (three added cases; two existing cases strengthened). No reflection, internal setter, relaxed assertion, fixture timeout, authority guard or production rule change is added.

## Root verification, not run here

Run normal R0, then the existing bounded core command:

```powershell
node Tools/kit/gate/game_gate.mjs core --filter 'FullyQualifiedName~WordboundAuthorityTests|FullyQualifiedName~WordboundLifecycleTests|FullyQualifiedName~WordboundPracticeTests' --json
dotnet run --project Tools/RulesHarness/ValidationProbe/ValidationProbe.csproj -c Release -- --advance .
```

Use the same probe source as the recorded baseline; compare all six mode/batch start/end hashes, clocks, epoch/counts, positive controls, raw allocated bytes and every elapsed sample. Keep actual native Synchronize/HostStep and public movement acceptance separate; root chooses the unchanged native workload after correctness passes. Existing unrelated lane dirt is excluded from all commits.

Recover_input independently source-reviewed `70cd4c6b`: PASS for lazy first-query/null/negative-team/non-race ordering, one-call lifetime, live demand/short-circuit and ordinal membership, unchanged other callers and real root assembly APIs. The three added and two strengthened fixtures retain their meaningful transition/rollback/restore assertions. They use synthetic host poses/query through existing domain fixtures; they do not establish native traversal or performance. No source changes followed that review. Exclude baseline `7d5d35a9`.

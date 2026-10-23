# Count-only canonical capacity checks

Status: SOURCE PASS — recover_assets independently reviewed the exact four-file source/test delta; compiler, core, native and player verification NOT RUN for this delta. Parent approved source/tests only; root owns all execution. Actual 191029 public comparison remains FAIL; see `NATIVE-PUBLIC-CAPTURE-COMPARISON-191029.md`. No claim that this small change resolves transfer timing or reaches 60 Hz.

Scope: add a read-only Economy item count backed directly by its existing dictionary, and replace only AuthoritativeMatch.EnsureCapacity's temporary EconomySnapshot with this count and the existing Tiles.Count. Preserve stored destroyed entries, all item states, loose-tile semantics, 512/8192 limits, exception text, call positions and transaction rollback. No cached counter, mutable reference or new state is introduced. Rollback/publication/import snapshots and validation remain unchanged.

The removed Capture constructs an EconomySnapshot by sorting and copying players, every item and every loose tile. Its constructor does not validate the economy. EnsureCapacity then used only ItemStates.Length and TileStates.Length; direct dictionary counts therefore remove those allocations without removing a validation boundary. All methods involved are synchronous and the existing authority transaction owns mutation; no callback or await is added between counts.

Three new regressions use public operations:

- CanonicalCountsRetainDestroyedItemsAtTheLimitAndSeparateCollectedLetters: creates 8192 destroyed map entries and loose letters, checks 8192/8193 independently, collects a tile, retains an immutable snapshot and restores it into another economy. Count reads emit no ItemChanged events; both ledgers remain conserved.
- CanonicalItemCountFollowsCraftCompletionAndRestoreWithoutCountingReservations: a pending craft is not an item, completion adds one, destruction retains that item entry, and restoring the pending snapshot removes the completed item while preserving the reservation.
- ReturnDuringCraftPreservesRetainedSnapshotAndCanonicalLedgerCounts: an actual authoritative Return drops the three reserved letters, preserves item/minted/spent counts and the earlier snapshot, then publishes, acknowledges and imports the final state through existing validation.

Existing failed-host-query and pressure-retirement rollback cases remain required. No reflection/internal setter or new native test framework is introduced. The boundary case uses simple economy mutations, not thousands of native simulation steps. Its raw 8193-entry economy proves counts, not an actual AuthoritativeMatch overcapacity rejection; the unchanged guard is source-reviewed and the existing budget tests remain required. These tests prove count and state behavior; they are not a timing benchmark.

Root follow-up, sequentially in the assembled integration checkout:

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs core --filter 'FullyQualifiedName~WordboundAuthorityTests|FullyQualifiedName~WordboundEconomySnapshotTests|FullyQualifiedName~WordboundEntityBudgetTests' --json
```

Expected Authority 36 and EconomySnapshot 9 cases; include the current integration EntityBudget fixture unchanged. Then rerun the same exact public TeamRace Solo native test and player workload with existing markers and deadlines. Compare inclusive Advance/Reconcile, completed tick batches and actual transfer outcome; do not infer FPS or add overlapping marker totals. The retained baseline is the 191029 run, with intervening changes recorded by the integrator. `git diff --check` passed locally; no compiler/test command was run by this lane.

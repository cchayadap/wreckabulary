# Next native online acceptance jobs

STATUS: COMPLETE — read-only planning against integration `bace5e5d`; no jobs, network operations or runtime edits. Root owns launch order after the 47 local/authoring checks and rebuilt Windows candidate. Full-mode production testing is already authorized; this checklist adds no approval gate. Historical receipts below do not certify the repaired 6000.6.4f1 Editor or current candidate.

## Next jobs, in priority order

| Job key | Expected cases | Purpose / current status | Outer gate allowance |
| --- | ---: | --- | ---: |
| `public-two` | 5 | Actual approved public content, all five modes, two authenticated clients each. TeamRace has a repeated movement failure; all five need the same current-candidate receipt. | 3600 s |
| `public-scale` | 4 | Actual public TeamRace/BrawlRace Duo (4) and Squad (8), two whole parties. Fixtures exist; no matching saved XML receipt was found. | 3600 s |
| `full-ffa` | 1 | Actual public FFA ten-KO win, nine respawns, exact confirmed terminal result/download/ACK. Existing fixture still fails its eight-minute lifetime. | 900 s |
| `behavior-controls` | 2 | Existing compact private three-lap TimeRun result and shared Freestyle physical solve/reward/station switch/no-damage behavior. Historical passes; useful regression controls, **not substitutes for full public flagship outcomes**. | 1800 s |
| `capacity-preflight` | 1 | Corrected authored content, legal budget and pinned passive SDK metadata before service allocation. Historical pass; verify current prefab/configuration before live stress. | 600 s |
| `capacity-live` | 1 | Existing signed private two-peer reclamation fixture. Run after full-FFA capacity/endurance prerequisite and preflight pass. No live receipt found. | 900 s |
| `flagship-recovery` | 2 | Existing private flagship eight-peer TeamRace composition and four-peer Brawl host departure/three-survivor restore. No matching saved XML receipt found. | 1800 s |
| recovery category below | 4 | Existing timeout-shutdown/rejoin, cancellation, definitive rejection and delayed-election cases. Distinct from a process-kill or impaired-network test. | 3600 s |

Do not launch the next group automatically after a failure. Preserve the actual failing case, operation and cleanup first; root chooses a source-backed repair or a justified comparison. A single passing retry does not erase the earlier intermittent public failures. One all-nine public run is possible with category `^ExternalPhotonPublicGameplay$` and outer allowance 7200 s; it covers exactly `public-two` plus `public-scale`, so do not count or rerun those as independent coverage without a reason.

## Exact filters and environment

Run from integration only after root releases the exclusive runtime slot. These are commands to prepare, **not commands executed by this review**. Keep current production authentication, trust, catalogue and service bindings; no regeneration/deployment or credentials are required by this checklist. The public case requires exact equality between the file, installed LaunchAssets catalogue and approved release. A mismatch is a real blocker.

```powershell
$env:WORDBOUND_EXTERNAL_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_FULL_MATCH = '1'
$env:WORDBOUND_PUBLIC_CATALOGUE = Join-Path (Get-Location) 'Assets/_Project/Resources/Wordbound/public-catalogue.json'
$env:WORDBOUND_PROFILE_NATIVE = '1'
$env:WORDBOUND_PROFILE_PHASES = '1'
$env:WORDBOUND_PROFILE_CHECKPOINTS = '1'
$prefix = 'Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.'
$nativeGate = @('test', 'editmode', '--assemblies', 'Wreckabulary.Online.NativeGameplay.Tests', '--cold', '--json')
$jobs = @{
    'public-two' = @(
        'PublicTeamRacePreservesWholeSoloParties'
        'PublicBrawlRacePreservesWholeSoloParties'
        'PublicTimeRunRequiresTwoRealHumans'
        'PublicFreeForAllAllocatesTwoIndividuals'
        'PublicFreestyleFillsTwoRealHumansWithCoopRules')
    'public-scale' = @(
        'PublicTeamRacePreservesWholeDuoParties'
        'PublicTeamRacePreservesWholeSquadParties'
        'PublicBrawlRacePreservesWholeDuoParties'
        'PublicBrawlRacePreservesWholeSquadParties')
    'full-ffa' = @('TwoAuthenticatedPublicFfaIndividualsReachScoredWinAndConfirmDurableResult')
    'behavior-controls' = @(
        'TwoAuthenticatedPeersCompleteCompactAuthoredTimeRunAndConfirmDurableResult'
        'TwoAuthenticatedPeersShareFreestyleStationRewardAndPreserveInventory')
    'capacity-preflight' = @('AuthoredCapacityPreflightUsesTheFullBudgetAndPinnedPassiveHistoryMetadata')
    'capacity-live' = @('TwoAuthenticatedPeersReclaimAndReuseTheMaximumAuthoredBudget')
    'flagship-recovery' = @(
        'EightAuthenticatedPeersComposeFlagshipTeamRaceSquad'
        'FourAuthenticatedPeersRestoreFlagshipBrawlDuoAfterHostDeparture')
}
$job = 'public-two' # Root selects one job and its outer allowance from the table.
$filter = ($jobs[$job] | ForEach-Object { $prefix + $_ }) -join ';'
node Tools/kit/gate/game_gate.mjs @nativeGate --filter $filter --timeout 3600
# Separate later job, after reviewing prior results:
# node Tools/kit/gate/game_gate.mjs @nativeGate --category '^ExternalPhotonRecoveryGameplay$' --timeout 3600
```

All live cases use `[Timeout(600000)]`; shared Run starts an **eight-minute CTS after EnterPlayMode, before coordinator preparation**. Existing limits remain prepare 120 s; each Runner/start/readiness 65 s; joint Ready+ExactBodies 25 s; remote movement 10 s; command 15 s; exact command-bearing checkpoint/ACK 25 s; replacement-host readiness 75 s. Cleanup remains per-peer stop 20 s, pending tasks 5 s and service cleanup 60 s. Outer allowances cover the whole independent batch/import/cleanup and **do not change any fixture or runtime deadline**.

Use `editmode --cold`: these Editor fixtures perform their own EnterPlayMode/ExitPlayMode, and the cold process receives the selected environment. No new environment is applied to a resident Editor. For a later instrumentation-off comparison, set all three profiling variables explicitly to `0`, keep the exact source/filter/workload, and retain it as a separate receipt. The unrelated offline puzzle environment flag is not needed for these filters; 010750's skipped offline six-family case must not be counted as a pass.

## Saved evidence that determines priority

Receipts were read from exact NUnit test-case attributes in integration `TestResults/editmode_*.xml`; absence means no receipt found in that saved set, not proof a test was never launched elsewhere.

| Existing fixture | Latest relevant saved evidence | Acceptance implication |
| --- | --- | --- |
| Public TeamRace Solo | `20261024T010750Z`: FAIL 43.506172 s, admitted client movement deadline; also failed 003910, while 003140 reached movement/command then transfer-age failure. | Public reliability still open. Do not infer network cause or native remote pose from a replica's older committed pose. |
| Public Brawl Solo | `20261023T225207Z`: PASS 58.027213 s, after earlier mixed failures/passes. | Historical bounded entry pass, current candidate unverified. |
| Public TimeRun | `20261023T154541Z`: PASS 79.022470 s. | Historical entry only, not full flagship completion. |
| Public FFA / Freestyle | `20261023T213856Z`: PASS 55.968740 / 48.756358 s. | Historical entry only. |
| All four public Duo/Squad methods; private flagship 4/8 methods | No matching saved test-case receipts. | **Fixtures exist; unverified**, not missing implementations. |
| Compact authored TimeRun finish | `20261023T202344Z`: PASS 173.549497 s. | Three real compact laps and exact durable confirmation; retain compact/private limit. |
| Shared Freestyle behavior | `20261023T215152Z`: PASS 80.000249 s; also 212947 pass. | Real shared station/reward/inventory and admitted attack-opportunity/no-damage evidence already exists. |
| Full public FFA | `20261023T233437Z`: FAIL 487.762312 s; final walk ran 5.999 s before `lifetimeCancelled=True`, not its 35 s movement limit. Six KOs/respawns and first hit of seventh cycle reached. | Ten-point outcome and terminal ACK not reached. No OOM observed and a sampled object-heap plateau are useful but **not a full-match or maximum-capacity pass**. |
| Capacity preflight / live | Preflight `20261023T233231Z`: PASS 6.061258 s after correcting unregistered BALL prop to PLATE; no live receipt found. | Preflight creates no external records. Live case remains unverified. |

Integration retains the FFA evidence in `BuildNotes/client-view/NATIVE-FFA-AUTHORING-233437-REVIEW.md`. Latest public failure is `BuildNotes/client-view/native-observe-coverage-20261024.json` plus its exact XML/log. Do not mix diagnostic lines from synthetic collector tests with a public case: use each NUnit case's own output.

## What a green selected job establishes—and what still lacks a fixture

- Public methods use actual approved content/seed `0123456789abcdef`, unique isolated `pub-*` queues, real authenticated scripted clients, signed original party membership and whole-party allocation. Shared Run requires canonical body replication, sole host authority, **host-observed remote movement**, command/replay deduplication and an exact command-bearing snapshot acknowledged by every replica. Race cases additionally check authored physical/moving content. These are separate Runners/physics scenes in **one Editor process**, not human participants or separate-machine deployment acceptance.
- The capacity fixture reserves the maximum **512-body budget** but has **366 actual initial bodies** with 228 PLATE props; it observes real generation/refill/retired IDs, exact ACKs, stable roots, recreated bodies and allocator free blocks/segments. Do not rename it “512 live bodies,” require arbitrary 60-history occupancy for static objects, or infer reclamation from total heap capacity shrinking. Passive history counts are actual samples, not 60 canonical ticks as proof of 60 received snapshots.
- **Missing behavior drivers:** full public flagship TimeRun; real TeamRace/BrawlRace multi-lap team/rival outcomes and exact terminal result; real timeout/draw outcomes. The compact TimeRun driver and Rules tests cannot fill these labels. Freestyle has no terminal race win; its shared experiment behavior is the relevant existing driver.
- **Missing mode-scale drivers:** public TimeRun 4, FFA 8 and Freestyle 8. Existing 4/8 small private proof methods and race Duo/Squad do not establish those modes at capacity. Full FFA exercises host scoring against a remote moving victim, not remote-client Attack/scoring. Race incomplete-party Fill On and a populated release matchmaking pool are also untested by these full-party isolated-queue methods.
- The later eight shipped Freestyle seed/seat startup cases are pure authority tests with a stub world, not eight native peers or full outcomes. Independent Input review knew no newer native full-mode receipt than those listed here.
- The 480-second FFA timeout and 1500-second race/TimeRun timeout cannot be reached honestly inside the current eight-minute startup-inclusive fixture lifetime. They need a separate full-duration acceptance fixture and explicit execution budget, preserving the existing short-test limits. Real completed outcomes at 4/8 players, separate-process machines, abrupt host loss and network-impairment recovery/rewind targets are additional missing acceptance routes; current graceful Stop/injected shutdown tests do not prove them.

Require exact expected case count, **zero skipped**, final NUnit success, completed service teardown with zero failures, stopped Runners/no owned scene leaks and gate exit/crash facts. `*_OBSERVED` lines explicitly precede cleanup. Record Editor path/version, source hash, actual catalogue/content/config hashes, profile flags, peer count and exact case XML/log; do not turn source parity, an older pass or a skipped case into current release acceptance.

## Source anchors / inventory correction

- `Assets/_Project/Tests/OnlineGameplay/WordboundNativePublicEntryTests.cs:14` — all nine public methods; `:32` exact catalogue/current optional `afterStarted` and iterator forwarding.
- `WordboundNativeGameplayTests.cs:85` — shared Run and lifetime; `:137` joint readiness/body/movement/ACK; `:213` migration; `:619` cleanup; all paths under the same OnlineGameplay directory.
- `WordboundNativeFullMatchTests.cs:23`, `WordboundNativeWorkshopTests.cs:25`, `WordboundNativeCapacityTests.cs:22` — existing full/behavior/capacity drivers.
- `WordboundNativeReconnectTests.cs:21` — four recovery methods. `Wreckabulary.Online.Gameplay.Tests.asmdef` declares assembly `Wreckabulary.Online.NativeGameplay.Tests`.
- Current live category counts: public **9**, general gameplay **9**, recovery **4**, workshop **3**, full-match **2** = **27**. Preflight is a separate ordinary test. Earlier `FINAL-NATIVE-MATRIX.md` / `REMAINING-MODE-ACCEPTANCE.md` inventories saying 23 total, one workshop case, no full/shared driver or no `afterStarted` callback are superseded for this source. Do not run the entire assembly or broad class merely to obtain a round count; it includes unrelated focused/optional diagnostics.

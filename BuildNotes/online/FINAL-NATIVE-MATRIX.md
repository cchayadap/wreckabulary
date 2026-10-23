# Final native multiplayer gate matrix

STATUS: SOURCE AUDIT COMPLETE; final-candidate execution pending. Read integration `8174c8d` and installed Test Framework `80f691d4262b`. No native/heavy job, network mutation or deployment. Root executes after the floor candidate, atomic chain and affected R0/core gates. Earlier reports listing five public cases or seven baseline cases have stale counts; this matrix describes the present declarations, not a passed run.

## Nine public combinations

All methods belong to `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests`, in `Assets/_Project/Tests/OnlineGameplay/WordboundNativePublicEntryTests.cs`. None is Explicit.

| Mode / format | Exact method | Authenticated Runner peers | Original parties / Fill |
| --- | --- | ---: | --- |
| TeamRace / Solo | PublicTeamRacePreservesWholeSoloParties | 2 | 2 × 1 / Off |
| TeamRace / Duo | PublicTeamRacePreservesWholeDuoParties | 4 | 2 × 2 / Off |
| TeamRace / Squad | PublicTeamRacePreservesWholeSquadParties | 8 | 2 × 4 / Off |
| BrawlRace / Solo | PublicBrawlRacePreservesWholeSoloParties | 2 | 2 × 1 / Off |
| BrawlRace / Duo | PublicBrawlRacePreservesWholeDuoParties | 4 | 2 × 2 / Off |
| BrawlRace / Squad | PublicBrawlRacePreservesWholeSquadParties | 8 | 2 × 4 / Off |
| TimeRun / Solo | PublicTimeRunRequiresTwoRealHumans | 2 | 1 × 2 cooperative party / Off |
| FreeForAll / Solo | PublicFreeForAllAllocatesTwoIndividuals | 2 | 2 × 1 / Off |
| Freestyle / Solo | PublicFreestyleFillsTwoRealHumansWithCoopRules | 2 | 2 × 1 / On |

These are independently authenticated Photon clients with separate Runners/physics scenes in one Editor process. They are automated guests, not AI participants; they are not eight separate PCs or eight manual testers. Race Solo is one person per opposing team, not a solo fallback. TimeRun/Freestyle have one cooperative team; only TeamRace/BrawlRace expose Duo/Squad.

Each public case validates exact installed item/rule bytes against the native catalogue and the catalogue assigned to LaunchAssets, release equality, fixed seed `0123456789abcdef`, a unique approved `pub-*` pool, QuickPlay, no private code, signed original party membership and exact roster. Shared Run then checks all admitted identities, isolated scenes, exact network body ledger/authority, remote movement >0.25 m, a real CancelCraft command, one receipt despite replay, and an exact committed snapshot acknowledged by every replica. Races additionally require actual physical callbacks, moving-fixture phase and complete protected fixture replication. Freestyle asserts zero timer, no enemy/hazard damage, Explore and no current result. Every public method uses `migrate:false` and no interaction scenario.

## Executable batches

Use the root integration checkout, with the exclusive Unity slot. The deployed public bindings must match the actual current catalogue; do not regenerate or redeploy merely to run this matrix. Keep the production custom-auth configuration and pinned trust. No key/token is an environment prerequisite.

```powershell
Set-Location 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration'
$env:WORDBOUND_EXTERNAL_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY = '1'
$env:WORDBOUND_PUBLIC_CATALOGUE = Join-Path (Get-Location) 'Assets/_Project/Resources/Wordbound/public-catalogue.json'
$env:WORDBOUND_PROFILE_NATIVE = '0'
$nativeGate = @('test', 'editmode', '--assemblies', 'Wreckabulary.Online.NativeGameplay.Tests', '--cold', '--json')
node Tools/kit/gate/game_gate.mjs @nativeGate --category '^ExternalPhotonPublicGameplay$' --timeout 7200
```

That single Unity invocation selects **nine cases**, including both four-peer and both eight-peer public variants. These Editor fixtures enter/exit Play Mode themselves. Do not choose `test playmode`. The public wrapper explicitly forwards Run's yields so EnterPlayMode executes; shared Run asserts Application.isPlaying before creating guests. Require actual XML 9 total / 9 passed / 0 failed / 0 skipped, not just an early certificate log.

Other existing groups can each be batched, sequentially after inspecting the prior result:

| Category | Count | Scope | Example outer allowance |
| --- | ---: | --- | ---: |
| ExternalPhotonGameplay | 9 | Private 2/4/8 proof peers; 2-peer host departure; 8-peer flagship TeamRace Squad; 4-peer flagship Brawl Duo host departure; prop interaction; two HUD interaction/defense cases | 7200 s |
| ExternalPhotonRecoveryGameplay | 4 | Real Runner timeout-shutdown/rejoin, explicit stop, expired-seat/auth rejection, delayed Photon election with unused-token credential failure | 4200 s |
| ExternalPhotonWorkshopGameplay | 1 | Two-peer exact private authored upload/download/Ready and independent reconstructed content/play | 1800 s |

```powershell
node Tools/kit/gate/game_gate.mjs @nativeGate --category '^ExternalPhotonGameplay$' --timeout 7200
node Tools/kit/gate/game_gate.mjs @nativeGate --category '^ExternalPhotonRecoveryGameplay$' --timeout 4200
node Tools/kit/gate/game_gate.mjs @nativeGate --category '^ExternalPhotonWorkshopGameplay$' --timeout 1800
```

The **exact scale/recovery names** in the first group are `FourAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands`, `EightAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands`, `EightAuthenticatedPeersComposeFlagshipTeamRaceSquad`, `AuthenticatedGameplayRestoresAfterHostDeparture`, and `FourAuthenticatedPeersRestoreFlagshipBrawlDuoAfterHostDeparture`. The first two use the small private Freestyle proof; they do not replace public Duo/Squad. The four-peer flagship departure keeps three survivors, verifies one elected host/epoch increment, the first restored moving-fixture frontier before any host advance, command deduplication, all surviving bodies and a new remote acknowledged command.

If root elects one complete native invocation instead of those four batches, the installed CLI splits semicolon lists and ORs categories. This selects **23 existing cases** and avoids separate cold imports; do not also rerun the four equivalent groups without a new reason:

```powershell
node Tools/kit/gate/game_gate.mjs @nativeGate --category '^ExternalPhotonPublicGameplay$;^ExternalPhotonGameplay$;^ExternalPhotonRecoveryGameplay$;^ExternalPhotonWorkshopGameplay$' --timeout 16200
```

No test is Explicit, so this combined category selection is valid. Sequential setup/teardown clears peers, pending tasks, local scenes and saved build settings between cases. Eight peers are concurrent only inside an eight-peer case; do not run multiple Unity batches beside each other. Gate code forwards `--category` to cold `-testCategory`; installed `CommandLineOption.SplitStringToArray` splits `;`, and `RuntimeTestRunnerFilter.AddFilters` ORs inclusions. Use cold mode for the environment above; a resident Editor does not inherit newly assigned variables.

Every selected declaration has **600,000 ms NUnit timeout**, and Run starts an **eight-minute CTS after EnterPlayMode**. Individual operation limits remain 120 s coordinator prepare, 65 s per Runner start/readiness, 25 s bodies/checkpoint acknowledgement, 10 s movement, 15 s command and 75 s replacement-host recovery. Cleanup permits up to 20 s per peer stop, 5 s pending tasks and 60 s service cleanup. Outer allowances above cover cold compilation/import plus several independent tests; they do not alter correctness or production deadlines and are not expected completion times. A too-small 3600 s envelope could kill a nine-case run before its existing per-case budgets finish. Root may select a smaller focused batch/allowance from measured startup costs. Exit 75 is blocked, not a reason to kill an unrelated Editor or retry blindly.

## Acceptance record and concrete gaps

Require each case and its teardown to pass. Cleanup returns seats, cancels queued assignments, leaves recorded parties, verifies absent membership, clears credentials/disposes transports, stops Runners and asserts no proof-owned scene objects remain. Capture the raw XML/log, exact source/build/catalogue hashes and observed peer count. `*_OBSERVED` logs explicitly say cleanup pending and cannot replace the final NUnit result. Creation that fails before a party identifier is recorded retains the known uncertain-membership limit; do not claim that unrecorded membership was checked.

- **Final execution is open.** Source coverage exists for all nine combinations; earlier partial/failed runs do not establish the new atomic/floor candidate. A 23-case class/category total must not hide skipped or missing public variants. Current native checks cover only one of the four catalogue seeds; the 36-row Worker/catalogue gates are separate evidence for the other seed identities.
- **Mode outcomes are not asserted here.** Race tests do not finish ordered puzzles/laps/final delivery, TimeRun does not assert timer success/failure, Brawl/FFA do not prove player damage/knockout/respawn/scoring/win, and Freestyle checks rule flags rather than actual rejected damage or shared practice completion/reset. Existing pure Rules and offline six-family physical tests help, but are not multiplayer end-to-end outcomes. Do not add a broad suite merely to turn the nine entry cases into a completion claim.
- **Capacity gaps remain by mode.** Duo/Squad public race tests give genuine 4/8-runner coverage. Public TimeRun is only two of four allowed players; public FFA/Freestyle are two of eight. Private proof 4/8 counts do not substitute for those modes at capacity. Fill On is tested only for two single-member Freestyle parties; incomplete race-party Fill and a populated live release pool are not covered.
- **Recovery stimulus is bounded.** Public cases do not migrate. Existing private host tests use graceful controller Stop; reconnect tests inject SDK timeout shutdown/notification and HTTP errors. They do not simulate process kill, packet blackout detection, partition or measured RTT/loss/jitter. No existing command proves a <=10 s recovery or <=2 s visible rewind target under impairment; use recorded timing facts, not operation deadlines as measurements. Authored workshop transfer does not cover host departure/retention.
- **Windows acceptance is separate.** These are same-machine Editor peers, not independent Windows processes/machines or sustained release-player performance. The QA workshop/menu capture and stripped release build/smoke remain required. Do not weaken Photon custom authentication or substitute old transport-only fixtures for these gates. Set WORDBOUND_PROFILE_NATIVE=1 only for a deliberately comparable diagnostic run; it adds observers and is not a new acceptance case.

No production edits, extra tests, account queries or gate execution were performed for this report.

## Bounded execution checkpoint — 2026-10-23T20:37Z

Public TeamSolo passes in native203305 (source714acc05): readiness11.894s, movement7.088s, authenticated command3.544s and exact committed ACK8.744s. Teardown passes. The same batch passes19 native diagnostic parity/ownership/exception cases,20/20 total. This is one observed pass after intermittent movement/ACK failures, not stable performance or completion of this nine-format matrix. Exact artifacts: ../client-view/native-phase-control-20261023.json.

Separate compact private authored TimeRun202344 passes three real laps/nine objectives, both confirmed results, independent durable download/import/ACK and cleanup. It does not close public flagship or other-mode result coverage. Exact artifacts: ../client-view/native-compact-full-match-20261023.json. The earlier source audit and coverage limits above remain historical; new full-match coverage is bounded to this named case.

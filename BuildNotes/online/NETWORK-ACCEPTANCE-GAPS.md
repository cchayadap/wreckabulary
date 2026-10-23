# Network acceptance and remaining recovery work

STATUS: READ-ONLY AUDIT COMPLETE; REQUIRED NETWORK ACCEPTANCE STILL OPEN.

2026-10-23. Reviewed integration through `fc7cae3f1304419f410b92ab88c597867b80fb57`. No native tests, service requests, account queries, compilation or deployment were run for this report. Root owns native execution and assigns any implementation below before edits.

## What the existing native tests prove

All current gameplay fixtures are in `Assets/_Project/Tests/OnlineGameplay/`, class `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests`. They create several real authenticated Photon/Fusion peers inside one Unity process, with separate Runner scenes. They do not prove separate-machine connectivity, process crash recovery, or latency/loss targets.

| Existing case(s) | Assertions and limits |
| --- | --- |
| `Two/Four/EightAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands` | Real private coordinator entry, exact body ledger, remote movement, one CancelCraft receipt despite replay, exact committed snapshot acknowledgement, host-only authority. Default content is the Freestyle proof course. These are not full flagship gameplay or public queue tests. |
| `EightAuthenticatedPeersComposeFlagshipTeamRaceSquad` | Same eight-peer checks using installed flagship content; physical callbacks run, moving fixture phase advances, complete protected platform ledger and identity agree. No puzzle/lap/final result is completed. |
| `AuthenticatedGameplayRestoresAfterHostDeparture` | Two peers; stops the host cleanly and waits for the survivor to become the new host. Preserves one committed command and bodies. No returning player. |
| `FourAuthenticatedPeersRestoreFlagshipBrawlDuoAfterHostDeparture` | Four become three after a clean host stop; exactly one replacement, new Runners, epoch +1, restored moving-fixture frontier before first advance, exact body/receipt ledger, then a fresh remote command. Allows 75 seconds for recovery; does not measure the ten-second target or two-second rewind. |
| `AuthenticatedPeersJumpCarryCraftEquipAndStrikeProps` | Host and remote jump/land, walk, carry/drop, collect BAT letters, craft/equip and damage a prop with exact durability/receipt replication. Freestyle fixture; does not test enemy damage, knockouts or scoring. |
| `TwoAuthenticatedPeersDownloadAndPlayExactAuthoredWorkshopMap` | Real private upload/download/Ready, separate reconstructed content objects, exact source/course/catalogue/rules identity and normal replicated play. `Run(2, false, authoredWorkshop: true)` does not test retention through host departure. |

The five `ExternalPhotonPublicGameplay` cases all use the exact player-assigned catalogue, seed `0123456789abcdef`, an isolated `pub-*` pool, real humans and the common movement/CancelCraft/durable-ack checks. None passes `migrate: true` or `interactions: true`.

| Public mode / players | Additional assertions | Missing mode outcome |
| --- | --- | --- |
| TeamRace Duo / 4 | Two whole Duo parties; flagship identity, physical callbacks and fixture ledger | Clean-team damage isolation during actual collisions; ordered puzzles, laps, final delivery and confirmed result |
| BrawlRace Duo / 4 | Two whole Duo parties; same flagship composition | Enemy combat/knockout/respawn, protected rival resources, race completion |
| TimeRun Solo / 2 | Whole cooperative party, Fill Off; flagship composition | Shared physical objective, final delivery, success/failure and timer result |
| FreeForAll Solo / 2 | Two independently allocated humans; exact installed content | Player combat, individual score, respawn, win/draw |
| Freestyle Solo / 2 | Fill On, zero timer, enemy/hazard damage off, Explore objective, no current result | Shared practice completion/reset and actual rejected damage; sustained absence of timer/winner |

Domain tests for results, combat, practice and reconnect deadlines exist under `Assets/_Project/Tests/Rules/`. The local six-family native puzzle fixture uses one offline TimeRun Runner. They provide useful separate evidence, not these multiplayer outcomes.

## Evidence that remains historical

`progress.md` records earlier 2/4/8-peer transport passes. `WordboundPhotonConnectionTests` only exchanges byte-exact 64 KiB payloads in private relayed rooms. It uses ordinary `AuthenticationValues` rather than the production custom-ticket path. Do not weaken current Photon authentication to rerun that older proof.

`BuildNotes/online/ADMISSION-FOLLOWUP.md` retains a failed native batch: 18/22 passed, four gameplay objectives unaccepted (`TestResults/editmode_20261020T154938Z.xml`). Its later two-peer diagnostic passed 1/1 (`editmode_20261020T161115Z.xml`); that does not clear four/eight-peer, interaction or recovery acceptance. Older `WordboundPhotonMigrationTests` test full Photon cloud carriers; the retained carrier limitation led to coordinator-backed canonical checkpoints. They are not the current durability acceptance path. No final-candidate native result was produced by this audit.

## Transport, writer ownership and reconnect are different transitions

Exact implementation locations (C# paths below are under `Assets/_Project/`):

- `Scripts/Online/Fusion/WordboundFusionSession.cs:805`: `OnHostMigration` invalidates the previous RunScope, freezes the gate and domain, shuts down the old Runner for migration, and calls `StartRunner(token, ...)`. The token chooses the Photon Host/Client role. `StartRunner:178` obtains fresh credentials; `RestoreWriter:237` claims the coordinator writer, downloads its exact committed head, restores its assigned epoch, refreshes authenticated connections and opens admission. Fresh scene/seat readiness precedes gameplay.
- `OnlineServices/src/checkpoints.ts:32`: writer lease **15,000ms**. Claim at line 200 rejects takeover while the old lease is live unless its seat returned. Renewal at line 219 extends the same term; all writes/finalization require the same live owner/term. A Photon election token does not itself grant this lease, and lease expiry does not elect a Photon host.
- `WordboundFusionSession.cs:254,476`: writer renewal scheduled every **5,000ms**; connection renewal is separately **5,000ms**. `RestoreWriter` retries busy claims every **500ms**, with a **60,000ms** ceiling. `WordboundHttpTransport.cs:23,34` allows **15 seconds** per HTTP request. Native checkpoint cadence is **500ms**; shipped `WordboundGameplayContracts.cs:53` defaults to **1,000ms**. Polling continues while gameplay is paused, so lease renewal and checkpoint upload are separate operations.
- `Tests/OnlineGameplay/WordboundNativeGameplayTests.cs:187` calls `first.Controller.Stop()`, which ultimately calls `Runner.Shutdown()`. This is graceful transport departure, not process kill, silent blackout or partition. The test does not send terminal Return before the migration assertion.
- `WordboundFusionSession.cs:821,826`: ordinary shutdown invalidates the run; ordinary disconnect clears transport readiness. Neither callback starts a reconnect loop. The session's public `Start` is one-shot. `WordboundMatchClient.cs:124` tells the user to return to the party; `ReturnToParty:133` stops gameplay and sends the terminal coordinator `return`. That forfeits the seat rather than reconnecting it. `PartyCoordinator.ResumeGuest` restores identity, and `ProcessAssignment:402` can launch a restored active assignment, but it is not an automatic recovery path for the current failed Runner.
- `SnapshotCommitTracker.cs:48` bounds provisional advancement to `ModeDefinition.ProvisionalWindowSeconds = 2` times 60 ticks. Only an exact coordinator durable receipt advances its acknowledgement. This is a 120-simulation-tick cap, not measured native crash/visual rewind evidence. Keep this invariant unchanged.

A crash just after writer renewal can require nearly 15 seconds before safe takeover. The approved ten-second acceptance clock starts at detected loss, so its result depends on overlap between transport detection and writer expiry; measure that interval before deciding the lease must change. A partitioned former host that still reaches the coordinator may renew longer; the retained checkpoint design explicitly documents that limit. The checked-in Fusion network timeout is 10 seconds, and the gameplay factories create fresh default configs rather than copying the global config's timing/conditions. No native timing result establishes the target yet.

## RTT / packet loss hooks

No application-owned latency/loss selection or RTT sampling exists in the native gameplay fixtures. Their `Configuration()` creates a new `NetworkProjectConfig` with multiple peers, 60 Hz and reliable client-to-server transfer; it never sets `NetworkConditions`. Editing the global Inspector conditions therefore does not configure these fresh test configs.

The installed Fusion 2.1.3 XML documents public `NetworkProjectConfig.NetworkConditions`, `NetworkSimulationConfiguration.Enabled`, `DelayMin/Max`, `AdditionalJitter`, `LossChanceMin/Max`, `AdditionalLoss`, and `NetworkRunner.GetPlayerRtt(PlayerRef)`. These are available candidate hooks, not implemented or verified test controls. Apply conditions to the actual Runner config, measure observed RTT per remote peer and state whether 100/150ms means added or observed RTT. Zero injected delay is not zero Internet RTT. Fusion impairment does not automatically delay/drop coordinator HTTPS; a separate bounded `IOnlineHttpTransport` wrapper is needed for that boundary.

## Existing runnable commands

Run only after root's final import/catalogue/deployment prerequisites, with the exclusive heavy slot. Stop after nonzero exit; skipped fixtures are NOT-RUN. These are EditMode tests that enter Play Mode themselves. Batch compatible cases to avoid repeated startup; adjust only runner allowance from measured import duration, not correctness deadlines.

```powershell
Set-Location 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration'
$env:WORDBOUND_EXTERNAL_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY = '1'
$env:WORDBOUND_PUBLIC_CATALOGUE = Join-Path (Get-Location) 'Assets/_Project/Resources/Wordbound/public-catalogue.json'
node Tools/kit/gate/game_gate.mjs test editmode --category ExternalPhotonPublicGameplay --cold --json --timeout 3600
node Tools/kit/gate/game_gate.mjs test editmode --category ExternalPhotonGameplay --cold --json --timeout 5400
node Tools/kit/gate/game_gate.mjs test editmode --category ExternalPhotonWorkshopGameplay --cold --json --timeout 1200
```

The categories select five public cases, seven baseline/scale/migration/prop-interaction cases, and one authored-transfer case respectively. Every declaration has a 600000ms NUnit timeout and the common fixture has an eight-minute lifetime plus bounded cleanup. A focused rerun uses `--filter 'Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.<exact method above>'` instead of `--category`. There is no existing honest command for the absent latency, hard-loss, reconnect or five-mode outcome cases.

## Smallest implementation and acceptance proposal

1. **Recover the same seat automatically.** Add one generation-fenced recovery state machine in the session: freeze inputs/domain, cancel only the failed RunScope, preserve match identity and the original absolute 120-second deadline, obtain fresh tickets and create a fresh client Runner in the same room. Serialize retries with `OnHostMigration`; disable client-created rooms. Explicit Return/Stop, expired seat and definitive identity rejection are terminal. Surface a concise reconnecting status and a cancel/return action. Reuse the controller's existing Runner-change cleanup and fresh-scene readiness; do not route recovery through terminal Return.
2. **Keep two authorities explicit.** Photon migration tokens elect the transport host. Only that elected host claims the coordinator lease and restores its exact head; no client may create a host merely because it observed an expired lease. Confirmed transport loss must stop old writer renewals and stale async responses. A partition that cannot establish exclusive ownership must freeze, not preempt a live writer.
3. **Do not lower only the lease constant.** A candidate six-second lease / one-second renewal could leave room for restoration, but requires bounded renewal request cancellation, coordinated client compatibility, and a measured shorter Fusion detection setting. The current five-second cadence and fifteen-second HTTP ceiling are incompatible with blindly shortening the lease. Old clients also need an explicit compatibility plan; prefer an optional negotiated short lease for upgraded clients or a clearly bounded rollout over silently shortening every old term. Renewal must remain independent during large checkpoint upload/download; keep term/CAS, exact acknowledgements and 120-tick provisional cap. This is a candidate to test, not a demonstrated ten-second guarantee.
4. **Add focused cases before expanding runs.** Native two-peer client disconnect/rejoin inside grace, original deadline expiry, explicit Return never reconnects, simultaneous loss/migration, stale callbacks cannot affect replacement, old host stops renewing, and service outage remains frozen. Use process-isolated QA peers for actual host termination; killing one Runner through `Shutdown` cannot prove crash behavior. Interrupt pickup, in-flight craft, lap advance and final delivery; require one item/spend/receipt/result and equal restored hashes. Record loss time, elected token time, claim time, restored head, first resumed tick and maximum lost simulation ticks; require <=10s recovery and <=120 ticks only where the measured network profile meets those criteria.
5. **Use a bounded adverse-network matrix and real outcomes.** Wire explicit test-only condition profiles and RTT evidence into the real Runner factory, plus independent HTTPS fault injection. Run 2/4/8 peers at baseline, 100ms and 150ms profiles, then a recorded nonzero loss/jitter profile. Reuse the public fixtures for the missing mode-specific outcomes in the table, and extend authored transfer with host departure/retained source verification. Preserve current public allocation and no-bot/no-solo-fallback assertions.

Suggested ownership boundary: one lane owns session/client recovery and its lifecycle tests; another owns negotiated coordinator lease/request compatibility if selected; root owns native timing/kill orchestration and five-mode acceptance. This report grants no source ownership and changes no production deadline.

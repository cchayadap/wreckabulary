# Remaining native mode acceptance

STATUS: READ-ONLY AUDIT COMPLETE — recommendations only; no fixture/runtime edits or jobs. Inspected integration `4f5e038771ee987927036ef2a01523088555b62a`. The in-flight fourteen-phase run and current Windows QA are not assigned outcomes here. This updates the earlier FULL-MATCH-ACCEPTANCE-REVIEW without erasing its unmet requirements.

## Verified boundary

Compact private TimeRun now passes `editmode_20261023T202344Z.xml`: two authenticated scripted clients, three fresh TABLE/socket objectives, three native carrier deliveries, confirmed Win on host/replica, independently downloaded/imported/acknowledged durable checkpoint and clean teardown. It is one small private authored course, not the public flagship, all modes, human traversal, eight peers or Windows acceptance.

The existing public category has **nine cases / 34 total client instances across separate runs**, one approved seed per combination. It verifies actual public allocation, exact catalogue/content, complete signed parties, Fusion bodies, remote movement, command deduplication and durable acknowledgement. It does not complete those modes. The method names containing RealHumans mean authenticated player seats; the operators are scripts. No four-seed multiplication is recommended at this stage.

All public methods below use prefix `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.`. Latest *saved XML inspected* is historical evidence, not a claim about the next source version:

| Exact method suffix | Peers / Fill | Latest saved native evidence |
| --- | --- | --- |
| PublicTimeRunRequiresTwoRealHumans | 2 / false | PASS, 154541Z; full public match still open |
| PublicTeamRacePreservesWholeSoloParties | 2 / false | PASS, 203305Z and 203605Z; intermittent prior failures preserved |
| PublicTeamRacePreservesWholeDuoParties | 4 / false | No case result found in saved XML |
| PublicTeamRacePreservesWholeSquadParties | 8 / false | No case result found in saved XML |
| PublicBrawlRacePreservesWholeSoloParties | 2 / false | Historical FAIL, 141231Z; current rerun needed |
| PublicBrawlRacePreservesWholeDuoParties | 4 / false | No case result found in saved XML |
| PublicBrawlRacePreservesWholeSquadParties | 8 / false | No case result found in saved XML |
| PublicFreeForAllAllocatesTwoIndividuals | 2 / false | Historical FAIL, 141231Z; current rerun needed |
| PublicFreestyleFillsTwoRealHumansWithCoopRules | 2 / true | Historical FAIL, 141231Z; current rerun needed |

## Smallest next execution, after timing blockers

Run the **seven missing passing combinations**, not another new startup fixture. First Brawl Solo, FFA and Freestyle (three cases/six clients across runs) establish the unaccepted mode routes cheaply. Then Team/Brawl Duo and Squad (four cases/24 clients) exercise intact larger parties and real eight-Runner load. Preserve Fill settings; do not replace eight clients with synthetic render actors. A later exact-delivery-source category run should cover all nine, including the two earlier passing combinations.

Use the existing gate from integration with `WORDBOUND_EXTERNAL_GAMEPLAY=1`, `WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY=1` and `WORDBOUND_PUBLIC_CATALOGUE` pointing to the actual assigned `Assets/_Project/Resources/Wordbound/public-catalogue.json`. Filter construction, not an executed command:

```powershell
$prefix = 'Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.'
$names = @(
  'PublicBrawlRacePreservesWholeSoloParties',
  'PublicFreeForAllAllocatesTwoIndividuals',
  'PublicFreestyleFillsTwoRealHumansWithCoopRules',
  'PublicTeamRacePreservesWholeDuoParties',
  'PublicTeamRacePreservesWholeSquadParties',
  'PublicBrawlRacePreservesWholeDuoParties',
  'PublicBrawlRacePreservesWholeSquadParties'
)
$filter = ($names | ForEach-Object { $prefix + $_ }) -join ';'
node Tools/kit/gate/game_gate.mjs test editmode --cold --filter $filter --timeout 5400 --json
```

Expected **7**, zero skipped. The outer gate allowance does not change any existing per-case ten-minute NUnit limit, eight-minute match lifetime or correctness deadline. For the final all-combination gate use `--category ExternalPhotonPublicGameplay` instead of `--filter`; expect **9**, not the stale five stated in an older network report. Root owns scheduling and failure triage.

## Smallest new behavior cases

These are proposed cases, **not implemented filters**. Four cases are the smallest useful next set spanning the remaining mode semantics; none should be credited until their actual assertions run.

| Proposed case | Minimum | New evidence beyond public entry |
| --- | --- | --- |
| Shared Freestyle practice | 2 | One peer selects a reachable authored station with PracticePuzzle; the other physically solves it. Both observe exact selection/evidence and one reward, switch station without lost inventory, share/drop/recover keepsake, attempted native player/hazard damage does not change health, no race progress/result. |
| FFA scored finish | 2 | Real collect/craft/equip/Attack and host geometry produce ten eligible knockouts across actual respawn/protection windows; no duplicate points; same confirmed individual winner and durable checkpoint. |
| TeamRace cooperative isolation and result | 4, Duo | Two intact parties; ally contributions work, rival resources/progress remain isolated, rival passage/damage follow TeamRace policy, winning team completes three real laps while loser state remains distinct; same confirmed winner on all peers. |
| BrawlRace combat and result | 4, Duo | Distinguish friendly and enemy behavior, at least one native knockout/respawn/protected recovery, preserve critical rival resources, then complete three real laps and confirmed durable winner. |

Freestyle and FFA add the largest missing behavior at only two peers. Duo is the minimum competitive race roster that proves both teammate and opponent behavior; Solo cannot establish ally cooperation/protection, and a Duo pass is not Squad acceptance. FFA leader-timeout and equal-score draw are **two additional later cases** requiring the real 480-second/28,800-tick boundary. Current eight-minute lifetime starts before allocation and cannot honestly certify that boundary; use a separately justified long-session gate, not timer/progress injection or silent deadline relaxation. Full public flagship races and the intended 18–25-minute human route likewise require their own measured session budget.

## Reuse and bounded implementation constraints

The existing `Run` owns authentic admission, real Runner/isolated scenes, baseline input/receipt/ACK and strict cleanup. Its `afterStarted` hook can host behavior without duplicating networking. `FullWalk` supplies admitted per-input movement using actual host pose with unchanged radii/physics; `Command` submits through the real controller and verifies exact success receipt; `CommittedState` requires host and replica state plus exact acknowledgements. The verified compact fixture supplies lap/result/durable assertions that must be generalized deliberately, not copied as proof for other modes.

`Interactions` already collects physical letters, waits real craft timers, equips and attacks native **props**. It does not prove player damage or knockout scoring. `Command` requires Ok, so forbidden enemy/friendly interaction cases need a small explicit expected-rejection helper retaining exact actor/sequence/hash, not a globally weakened assertion. Body selection must respect the recently verified consumed-item compaction contract.

The current authored-transfer helper explicitly permits only two private TimeRun/Freestyle peers and requires one signed party/team0. Competitive compact-course tests therefore need a bounded test-support extension for two independently created/download-verified parties and four exact signed seats; changing a mode string or weakening that roster assertion is insufficient. Alternatively, drive the existing actual approved flagship route with an adequate dedicated budget. The existing PublicEntry helper currently supplies no afterStarted driver, so attaching public behavior is another explicit small fixture change. Neither extension is authorized by this report.

Recovery remains separate: four existing `ExternalPhotonRecoveryGameplay` cases use injected timeout shutdown/election/credential failures; graceful host-stop fixtures are not process crashes. Actual loss during pickup/craft/lap/final delivery, two/four/eight peers under measured latency/loss, separate machines and ten-second recovery/two-second rewind acceptance remain open. The six-family native puzzle pass is a prerequisite, not three full flagship laps. Windows result/HUD/return, sustained performance and owner control/visual acceptance are also separate.

Next recoverable action: after root accepts the timing/performance state, run the seven exact existing public filters above; then authorize one two-peer Freestyle or FFA behavior slice with a concrete real-input plan. Preserve current compact TimeRun as the known passing full-pipeline regression rather than extending it during public failure diagnosis.

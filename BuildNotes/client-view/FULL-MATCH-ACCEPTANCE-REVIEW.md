# Full-match acceptance review

Status: COMPLETE — source/read-only coverage review, not an execution pass. Inspected root `4ebccb545dc4e439171215db7ccaf354729050ff` while root owns native run `184749Z`; that run's outcome is not assumed here. No Unity, player, compiler or network execution in this lane. This report separates fixture coverage from human play and full-match acceptance.

Scope: current root `progress.md`, native/local multiplayer fixtures and completion rules for TimeRun, TeamRace, BrawlRace, FreeForAll and Freestyle. Record exact existing methods and the smallest meaningful remaining end-to-end evidence; do not infer completion from startup or acknowledgements.

## Finding

The unchecked full-match items in `progress.md:35–36` and public-mode/player acceptance remain justified. No inspected native fixture currently completes a three-lap race, obtains a terminal FFA result, or completes multiplayer Freestyle practice. The nine public entry methods use the actual approved catalogue, real coordinator allocation and authenticated scripted Fusion clients, but stop after movement, `CancelCraft`, deduplication and snapshot acknowledgement. Their names containing “humans” refer to admitted human-player seats, not people operating the controls.

The six-family physical fixture is a useful prerequisite, not a full match: it explicitly checks `Team.Lap == 0` and `!Team.Finished`, resolves only the first six sites on lap one, and records setup repositioning. Even a pass of the current native rerun cannot close three-lap, public full-match or human traversal acceptance.

## Existing native entry points and exact scope

Paths below are relative to the root checkout. Methods are existing source, not new proposals or claims that every case passed.

| Source / exact method | Existing scope | Missing full-match evidence |
| --- | --- | --- |
| `Assets/_Project/Tests/OnlineGameplay/WordboundNativePublicEntryTests.cs`: `PublicTimeRunRequiresTwoRealHumans` | Two authenticated peers; exact public catalogue/isolated public pool; shared `Run` | Three lap transitions, real objectives/keepsake delivery, durable confirmed result |
| Same file: `PublicTeamRacePreservesWholeSoloParties`, `PublicTeamRacePreservesWholeDuoParties`, `PublicTeamRacePreservesWholeSquadParties` | Complete two-party rosters of 2/4/8 peers; same movement/command/ACK path | Rival collision/resource/damage isolation, independent team progress, actual winner |
| Same file: `PublicBrawlRacePreservesWholeSoloParties`, `PublicBrawlRacePreservesWholeDuoParties`, `PublicBrawlRacePreservesWholeSquadParties` | Same admission path with Brawl definitions | Actual enemy damage/knockout/respawn, friendly protection and protected objectives, race completion |
| Same file: `PublicFreeForAllAllocatesTwoIndividuals` | Two individually allocated peers; same command proof | Native scored knockouts, respawn/protection, ten-point finish, timeout winner/draw |
| Same file: `PublicFreestyleFillsTwoRealHumansWithCoopRules` | Two peers and Fill enabled; same command proof | Shared station selection/physical completion, shared keepsake, no player/hazard damage, persistent no-result play |
| `Assets/_Project/Tests/OnlineGameplay/WordboundNativeGameplayTests.cs`: `TwoAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands`, `FourAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands`, `EightAuthenticatedPeersComposeCanonicalBodiesAndCommitClientCommands` | Private composition/body/input/receipt/ACK proof | Objective progression or terminal result |
| Same file: `EightAuthenticatedPeersComposeFlagshipTeamRaceSquad`, `FourAuthenticatedPeersRestoreFlagshipBrawlDuoAfterHostDeparture`, `AuthenticatedGameplayRestoresAfterHostDeparture` | Flagship capacity and/or committed state restoration; migration path checks moving-fixture phase and fresh command | Migration during solved objective/lap/final delivery; complete race. Graceful host stop is not abrupt process loss |
| `Assets/_Project/Tests/OfflineGameplay/PhysicalPuzzles/WordboundNativePuzzleFlow.cs`: `SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints` | One offline TimeRun actor; first six real authored mechanisms; snapshot imports | Rest of lap one, laps two/three, multiplayer ownership, win. Explicitly not human traversal |
| `Assets/_Project/Tests/OnlineGameplay/WordboundNativeInteractionTests.cs`: `AuthenticatedPeersJumpCarryCraftEquipAndStrikeProps` | Two peers, actual movement/craft timers/prop melee in Freestyle | Enemy player combat, knockout scoring or practice station completion |
| `Assets/_Project/Tests/OfflineGameplay/WordboundNativeLocalGameplayTests.cs`: `FreestyleUsesOfflineRunnerAndExactWorkshopManifest`, `TimeRunUsesOfflineRunnerAndExactWorkshopManifest` | Offline body composition, movement/jump/landing, command and memory checkpoint | Multiplayer or mode completion |

The HUD-defense, visible-letter, exact-workshop transfer and reconnect fixtures add important independent coverage. Their current methods do not extend the shared runner into complete matches.

## Completion contract that native tests must reach

`Assets/_Project/Scripts/Rules/Wordbound/ModeDefinition.cs:54–88` fixes all race modes at three laps and 1,500 seconds. TimeRun has one team, 1–4 players; TeamRace/BrawlRace have two teams with Solo/Duo/Squad capacities. FFA uses individual teams, ten knockouts and 480 seconds. Freestyle has one shared team, no time limit, no race laps and no knockout target.

`MatchProgress.ApplyPuzzle`, `ApplyCheckpoint` and `ApplyFinish` (`Assets/_Project/Scripts/Rules/Wordbound/MatchProgress.cs:425`) require the current lap's ordered objectives and actual keepsake carrier. A lap cannot be completed by a non-carrier at the finish. Lap three creates a winner; same-tick finish candidates can draw. `WordboundPuzzleRuntime.ObserveProgress` (`Assets/_Project/Scripts/Online/Puzzles/WordboundPuzzleRuntime.cs:233`) supplies checkpoint/finish observations from the actual native body near its authored recovery anchor with support, not from an arbitrary client finish request.

`MatchProgress.ApplyKnockout` and `Timeout` require eligible attackers, enemy teams, living/unprotected victims and one credit per victim per tick. Respawn is two seconds followed by two seconds of protection. FFA timeout picks the highest score and draws on a tie. TimeRun timeout is `TimeExpired`; competitive race timeout ranks ordered objectives, then the earlier progress tick. Completion on the exact limit tick is processed before timeout.

A non-null result is insufficient delivery evidence. `AuthoritativeMatch.Acknowledge` confirms it only after a durable acknowledged tick reaches the result tick; `RestoreCommitted` also confirms a restored committed result. `WordboundMatchHud.ResultText` renders “Confirming result…” while unconfirmed. Native acceptance needs the same outcome, winners, result tick and confirmed state on the host and every client, plus exact durable snapshot/import evidence. HUD/result/return behavior still needs the real Windows player.

Mode distinction is substantive: `ModeDefinition.CanInteract/CanDamage/PlayersCollide` and `WordboundPhysicsPolicy.ShouldCollide` make TeamRace rival players noncolliding and rival team resources inaccessible; Brawl allows opposing player combat and ordinary unprotected item interaction while preserving progression resources and friendly-fire protection. Freestyle disables both enemy and hazard damage. `WordboundPracticePolicy` selects one shared authored station, preserves its actual physical evidence, and produces no race progress or result.

## Smallest useful next fixture set

These are proposed coverage cases, not implemented method names. Reuse the existing authenticated/public preparation, normal commands, runtime sensors, receipt/ACK checks and cleanup. Keep one reusable race driver rather than three independent test frameworks.

| Proposed case | Minimum meaningful peers and actions | Required evidence |
| --- | --- | --- |
| TimeRun full three-lap result | Two peers on one team; both contribute; one retains or legitimately hands over the keepsake | All ordered objectives and actual finish sensors for laps 0→1→2→3; no early finish without carrier; other teammate need not stand at finish; confirmed team-0 result on both peers |
| TeamRace full three-lap result with isolation | Duo, four peers, two actual parties; one team wins while the other makes distinct progress | Ally help works; rival player passage does not block; enemy attack causes no damage; rival protected supply/keepsake interaction denied; no borrowing puzzle progress; actual three-lap winner and unchanged loser progress |
| BrawlRace full three-lap result with combat | Duo, four peers, so teammate and enemy protections are distinguishable | Real client `Attack`/host geometry damages an opponent; at least one knockout, respawn/protection and keepsake recovery; friendly hit and enemy progression-resource interference rejected; race still reaches actual confirmed winner |
| FFA ten-point result | Two peers; collect/craft/equip through normal commands; repeat actual eligible player hits across real respawn/protection intervals | One point per valid knockout, no friendly/team sharing or duplicate credit, score reaches ten once, confirmed winner replicated |
| FFA timeout with a leader | Two peers; create unequal native scores below ten; run consecutive normal simulation ticks through 28,800 elapsed ticks | No premature result; actual 480-second simulation boundary chooses correct individual winner; result remains stable and durable |
| FFA timeout draw | Two peers; equal scores below ten, including the zero-score case if minimizing setup | Same real boundary yields both winners/Draw; no ten-point substitution; confirmed identical result |
| Multiplayer Freestyle shared practice | Two peers; one selects an authored station through normal reachable/visible command, other contributes/completes it; switch station and hand over/drop/recover shared keepsake | Both see same selected site and solved evidence; already-solved reward cannot repeat; collected value survives station change; actual attempted player/hazard damage leaves health unchanged; no race objective progress, countdown or winner; committed state survives a real reconnect/restore |

Duo is the smallest competitive race roster that can exercise both teammate and enemy behavior. These three race cases do **not** finish Solo/Squad acceptance: retain the existing nine-entry format matrix, then run at least the eight-peer full-gameplay/stability and host-loss acceptance already required in `progress.md`. Do not relabel a four-peer result as an eight-player result.

A small authored three-lap course can first prove the integrated command→sensor→progress→result→durable-ACK pipeline cheaply. Its identity must be explicit and admitted through its proper private authored-map route; it cannot stand in for the approved public flagship. Final closure of “complete full three-lap Time Run, Team Race and Brawl Race matches” on the intended game requires the actual resolved flagship/public content, every selected objective and all three laps. Setup repositioning must be recorded; scripted tick input remains automated integration evidence, not human route traversal or an 18–25 minute play session.

Do not manufacture a terminal snapshot, inject `ProgressEvent.FinishReached`/`Knockout`, edit health/score, shorten mode timers, or directly mutate transforms during the claimed traversal to turn a startup fixture into a full-match proof. A deliberately restored late-game checkpoint can separately test result recovery, but cannot count as a fresh full match. New long-running fixtures need an explicit measured budget; the current public runner starts an eight-minute cancellation lifetime **before allocation**, and the physical fixture has six minutes. Those lifetimes are not a ready-made full 480-second timeout or 18–25 minute session budget. Preserve existing tests/deadlines and failures while designing that dedicated gate.

## Existing rules coverage to retain, not duplicate as native proof

`Assets/_Project/Tests/Rules/WordboundProgressTests.cs` already contains `KeepsakeCarrierCanFinishWithoutWaitingForOtherTeammates`, `TeamsAdvanceIndependentlyAndSimultaneousFinishesDraw`, `CheckpointsAndPuzzlesCannotBeSkippedOrBorrowedFromAnotherTeam`, `CombatRejectsFriendlyFireProtectionAndDuplicateKnockouts`, `TenKnockoutsWinAndEightMinuteEqualScoresDraw`, `RaceTimeoutRanksOrderedProgressThenEarlierTick`, and `FinishOnTimeLimitTickWinsAndFreestyleNeverTimesOut`. These construct progress events directly; they prove domain rules, not native geometry, admitted input, transport or visual result presentation.

`WordboundCombatTests.CleanModesNeverDamageAPlayerEvenWhenSwingConnects` and `RivalCriticalResourcesRemainProtectedFromBrawlMelee` cover policy/authority behavior with controlled host-world inputs. `WordboundPracticeTests.CompletionAndSelectionSurviveMigrationWithoutRaceProgressOrRepeatRewards` and `SharedKeepsakeTakeDropAndRecoveryRemainCanonicalWithoutRace` cover imported canonical practice state. None substitutes for the corresponding two-/four-peer native sequence above.

## Priorities and limits

1. Finish the current native six-family and public transfer blockers before scaling to full races. Preserve their measured failures; current run outcome is outside this source review.
2. Add the shared full-race path and the three mode distinctions; use explicit terminal durable confirmation rather than another startup marker.
3. Add FFA scoring/timeout branches and two-peer shared Freestyle practice; keep public entry and native body evidence attached to the tested content identity.
4. Close Windows result/HUD/return, full-capacity/network adversity, human traversal, route duration and owner feel/visual review separately. No source audit or scripted peer test can close those user-facing judgments.

No runtime defect is asserted by this coverage review. The concrete gap is missing native end-to-end completion evidence. No production source, native test, content, protocol, timeout, art or progress file changed in this task.

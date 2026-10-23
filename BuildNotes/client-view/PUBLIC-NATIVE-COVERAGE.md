# Public native mode coverage

STATUS: SOURCE CHECKPOINT READY — `50b4abe`; native execution pending. Baseline-only `2134f97` must NOT be cherry-picked.

The existing public fixture covered five combinations and used Duo for both race modes. Four named cases now fill the missing Solo/Squad combinations without multiplying seeds or changing service, production, launcher, shared runner or roster code. All run the existing exact exported-catalogue check and public QuickPlay allocation, followed by authenticated scripted Fusion clients; these are not human play/usability acceptance.

All cases belong to `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests`, category `ExternalPhotonPublicGameplay`:

| Method | Mode / format | Authenticated clients | Party arrangement | Fill |
|---|---|---:|---|---|
| PublicTimeRunRequiresTwoRealHumans | TimeRun / Solo | 2 | one party of 2 | false |
| PublicTeamRacePreservesWholeSoloParties | TeamRace / Solo | 2 | two parties of 1 | false |
| PublicTeamRacePreservesWholeDuoParties | TeamRace / Duo | 4 | two parties of 2 | false |
| PublicTeamRacePreservesWholeSquadParties | TeamRace / Squad | 8 | two parties of 4 | false |
| PublicBrawlRacePreservesWholeSoloParties | BrawlRace / Solo | 2 | two parties of 1 | false |
| PublicBrawlRacePreservesWholeDuoParties | BrawlRace / Duo | 4 | two parties of 2 | false |
| PublicBrawlRacePreservesWholeSquadParties | BrawlRace / Squad | 8 | two parties of 4 | false |
| PublicFreeForAllAllocatesTwoIndividuals | FreeForAll / Solo | 2 | two parties of 1 | false |
| PublicFreestyleFillsTwoRealHumansWithCoopRules | Freestyle / Solo | 2 | two parties of 1 | true |

Expected result: **9 passed / 0 failed / 0 skipped**, 34 fresh authenticated clients across sequential cases, maximum eight simultaneous peers in each Squad case. Existing method names containing Humans are retained for filter compatibility; all peers are scripted clients. One fixed approved seed `0123456789abcdef` is used. Filter: `^Wreckabulary\.OnlineGameplayTests\.WordboundNativeGameplayTests\.Public` (nine current cases).

Execution uses the existing Editor-only native assembly and enters PlayMode. It requires both `WORDBOUND_EXTERNAL_GAMEPLAY=1` and `WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY=1`, approved production Photon authentication and the exact catalogue already accepted by the Worker. Optional `WORDBOUND_PUBLIC_CATALOGUE` must still match launch-assets JSON and its release. Missing opt-in produces Ignore, not acceptance. Queue pools remain disposable isolated `pub-*` values; this does not enable or validate discoverability of a populated public release pool.

Unchanged shared proof: create real guest parties, mark every member ready, use QuickPlay with no private code, verify exact approved content/settings and signed roster membership, preserve each party on one team, and require the two exact whole race parties. Every client authenticates a real Fusion Runner and owns an isolated native physics scene; complete canonical body replication, admitted movement, host-applied command receipt, duplicate-command idempotence and acknowledged durable snapshots are checked. Race cases retain actual authored fixture-motion, host-observation, environment-query and canonical physical body checks. No physical assertion is removed.

A new entry contract explicitly requires two complete race parties for the requested format (2/4/8 total clients), verifies the selected mode/format, and preserves the original Fill choices. It does not claim exhaustive partial-party Fill packing coverage. A per-mode observation log is emitted only after shared gameplay assertions finish and still says cleanup pending.

Bounds are unchanged: 600-second test timeout; eight-minute fixture lifetime; preparation 120 seconds including a 45-second FORMING poll; each Runner startup/readiness at most 65 seconds; existing body/input/receipt/commit waits remain bounded. Teardown stops each Runner within 20 seconds, drains pending tasks within five seconds, bounds coordinator cleanup to 60 seconds, restores scene settings and asserts no service/session cleanup failures or owned native object leaks. These are bounds, not measured runtimes. No compiler, Unity, network or other heavy job was launched for this source checkpoint.

# Physical craft investigation

STATUS: SOURCE INVESTIGATION COMPLETE — cause remains unconfirmed until the existing diagnostic rerun; no Unity/compiler/job or source change.

Scope: integrated six-family physical fixture at root e638853, including diagnostic delta 235cf5b. Prior native evidence is TestResults/editmode_20261023T123439Z.xml and matching gate log. Preserve the six-minute lifetime, local operation budgets, genuine crafting inputs/output predicate and native mechanism assertions.

Confirmed: the single physical case failed after 363.172282 seconds. It completed CollectLetters at canonical tick 2160, then reached CratePress's accepted TABLE craft and failed its held-output wait. The existing log has no per-phase wall timestamps or final craft/job/tick/cancellation state, so neither a product craft defect nor the precise bottleneck is established.

Confirmed lifetime scope: the six-minute CancellationTokenSource starts AFTER EnterPlayMode, but BEFORE reading config, constructing/resolving/validating all flagship content, creating the controller/runtime/physics configuration, starting the local Runner and building the world. It also includes every supply pickup, setup observation, command receipt, first-family HAMMER/TABLE craft, physical completion and checkpoint import. Prefab asset loading, PrepareScene, EnterPlayMode transition, earlier camera/detail tests and initial 81.997-second asset refresh occur before this timer and cannot consume its six-minute budget.

## What the existing evidence establishes

- The first CollectLetters family already crafted HAMMER and TABLE through the same Craft helper, destroyed the marked prop with real melee, collected its native letter bodies, deployed the TABLE, observed completion, consumed that item with UsePuzzle, and passed its checkpoint export/import. CratePress's second TABLE uses NextItemId as a lower bound, so the previously destroyed TABLE cannot accidentally satisfy it.
- BeginCraft's accepted receipt rules out missing letters, initial inability to act, an existing craft, carried map furniture or an unavailable slot at acceptance. CompleteCraft checks CanAct again and returns a Crafted/Held item owned by player index 0; the fixture predicate correctly matches this output. Later loss of CanAct remains observable in the new diagnostics and must not be ignored.
- Crafting progresses through AuthoritativeMatch.Advance at `(Tick + 1) / 60`, not elapsed wall time. Current effective recipe duration is approximately 1.2 simulation seconds for TABLE; use the logged ReadyAt/remainingTicks instead of assuming exactly 72 ticks, because the configured float duration can round upward at a tick boundary. A 20-second wall wait does not prove enough canonical ticks elapsed.
- The local session starts in GameMode.Single, asserts no cloud connection and uses an isolated real PhysicsScene. PublishCheckpoint synchronously exports, validates, acknowledges and reimports canonical state; this path is local and cannot be blamed on Photon/Cloudflare latency. The controller also requests these local checkpoints every 500 wall milliseconds. Their cost is not measured by this log.
- StartLocal links the fixture token only for startup. After it returns, the session has its own lifetime. Therefore fixture cancellation can terminate Until while session.Failure remains empty and the Runner is still running. The repaired Until now distinguishes those outcomes.
- The 600,000 ms NUnit test timeout and the gate's 3,600-second outer limit are separate from the six-minute CTS. The prior case ran 12:36:49Z–12:42:53Z, while the whole gate ran about 504.5 seconds; neither outer budget is the reported failure.
- The previous camera/detail cases do not set timeScale, targetFrameRate, captureFramerate, fixedDeltaTime or vSyncCount in their source. Project TimeManager has timeScale 1. This excludes a direct setting assignment in those selected tests, but is not a runtime proof of the physical test's timeScale; the new diagnostics record it.

The six-minute setup charge is real, but calling it incorrect would require a different agreed fixture budget contract or evidence that startup consumed most of it. There is no per-phase wall timestamp in the prior log, and no evidence identifies expensive content resolution, checkpoint serialization, host catch-up, rendering or CPU contention as the dominant cost. No elapsed-time estimate should be derived from stack-trace line counts. Canonical tick 2160 alone represents 36 simulation seconds; it is not a wall-time measurement.

## Smallest diagnostic rerun

Run only the exact case using the existing gate and the coordinator's exclusive native slot. From the integration checkout, the equivalent gate invocation is:

```powershell
$env:WORDBOUND_NATIVE_PUZZLES = '1'
& 'C:/Program Files/nodejs/node.exe' Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.OfflineGameplayTests.WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints' --cold --timeout 3600 --json
```

Use the existing job wrapper's per-job environment/restoration when scheduling it; do not persist a shell/profile setting. This assembly is Editor-only, and the UnityTest explicitly enters Play Mode itself. Selecting the PlayMode runner instead is the wrong route. Require **one executed test, zero skipped**, the six-family success marker, and successful cleanup before acceptance. Missing WORDBOUND_NATIVE_PUZZLES=1 deliberately ignores the case and is not a pass.

The existing 235cf5b diagnostic delta already records each HAMMER/TABLE/etc. craft's begin/accepted/completed fixtureSeconds and canonical tick, job ReadyAt, remainingTicks, reservations/slots/output states, and the failure's elapsed wait/start tick/end tick/cancellation/paused/readiness/timeScale. Keep all existing physical prerequisites, exact map seed, 20-second craft waits and six-minute lifetime for this rerun.

| New observation | Interpretation and next bounded action |
| --- | --- |
| lifetimeCancelled=true near 360 seconds, with waitSeconds below 20 | Aggregate test lifetime ended; do not call this a failed TABLE timer. Compare earlier craft boundary timestamps to locate the consumed phase. |
| First HAMMER begin already has large fixtureSeconds | Combined resolve/startup/world/keepsake/tool-supply setup is costly. Existing markers do not split that combined phase. If needed, add only RunDetails to existing phase logs before changing budget boundaries. |
| Large time between first-family craft completion and second TABLE begin | Charge lies in real melee/collection/deployment/checkpoint/second-supply work, not initial content construction. Preserve those prerequisites and add bounded phase timing only where needed. |
| lifetimeCancelled=false, wait≈20, tick advances less than remainingTicks | Simulation throughput or pause is the limiting condition. Inspect simulationPaused, gameplayReady, Runner flags, timeScale and commitment state; do not shorten the recipe or forge ticks. |
| Canonical ticks pass ReadyAt, CanAct=true, valid live craft remains and no held output appears | Concrete craft/completion or reconciliation defect becomes plausible. Trace the exact logged job/output state against Economy.CompleteCraft and host Advance. |
| CanAct becomes false, craft is cancelled, or output appears with another state | Investigate the actual gameplay transition/inventory state. Do not relax the Held/holder/origin/NextItemId predicate. |
| Six-family marker and one passing case | Physical mechanism proof succeeds for this exact seeded setup, including real sensors/checkpoint imports; it still does not prove human traversal, full three-lap completion, network peers or performance acceptance. |

No source changes or budget increase are justified yet. If the aggregate lifetime is confirmed as the only failure, splitting independently named family cases may improve attribution, but each must still execute legitimate prerequisites or import a separately verified exact checkpoint. Directly stamping progress, granting crafted items, skipping pickup/receipt checks, or repeating the same expensive prerequisites under a larger hidden budget would not establish the intended proof.

Source paths checked: Tests/OfflineGameplay/PhysicalPuzzles/WordboundNativePuzzleFlow.cs and WordboundNativePuzzleActions.cs; Online/Fusion/WordboundLocalSession.cs; Online/Gameplay/WordboundGameplayController.cs; Online/Puzzles/WordboundPuzzleContent.cs; Rules/Economy.cs; Rules/Wordbound/AuthoritativeMatch.cs and SnapshotCommitTracker.cs. All paths are below Assets/_Project. Diagnostic rerun remains coordinator-owned and NOT RUN by this reviewer.

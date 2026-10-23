# FAN item-contact candidate

Status: SOURCE READY; candidate-fixture and editor-setup/source-regression independent source reviews PASS. Native candidate and setup execution remain root-owned. No production material or prefab asset has been changed by this lane.

## Confirmed baseline

Root `TestResults/editmode_20261023T162601Z.xml` failed the default-contact arrival assertion in 6.584471 seconds. The matching `Logs/gate_test_editmode_20261023T162601Z.log` records three completed local-physics comparisons using the real course floor, actual item-prefab Rigidbody/BoxCollider settings, STOOL dimensions, and actual wind policy at 60 Hz:

| Case | Maximum signed travel | First arrival | End behavior |
| --- | ---: | ---: | --- |
| No wind, default contacts | 0 m | None | Sleeping at start |
| Wind, default contacts | 0.0138988495 m | None | Wind applied for all 300 steps, solver returns near-zero/backwards Z velocity |
| Wind, zero-friction control | 19.9488983 m | Tick 27 / 0.45 s | Continues coasting after leaving field and reaches a distant boundary |

The target starts 0.799999237 m forward of the authored receiver start. This confirms contact resistance in this isolated case. Zero friction is a diagnostic control, not an acceptable item response: its endpoint does not prove natural stopping. This case is at course floor Y=16; it preserves the same local construction and relative coordinates as the earlier failed full fixture at Y=0.

XML SHA-256: `C7BEC20BD394A5B25E2FD883011BBB6C4EBA4961F675FFBCCEBE0300B58B68F3`.
Log SHA-256: `C18EDB4CEB79CA0318F7A62AFC1DF4BA805BD887A5D24E4FAF321F7D0A2F807E`.

## Candidate and exact next test

`Wreckabulary.Tests.WordboundNativeWindPhysicsTests.CandidateItemContactReachesWindArrivalAndStopsWithoutObstacles` uses a private receiver-only PhysicsMaterial with static friction **0.30**, dynamic friction **0.25**, friction combine **Minimum**, bounciness **0**, bounce combine **Minimum**. All generated course surfaces retain their null/default physics material. No global physics setting, original game asset, player prefab, platform prefab, FAN strength, sensor or deadline is changed.

Checkpoint `ef17ea3` preserved the original failing default-contact test while root tested the candidate independently. The follow-up source now replaces that artificial failure with `ActualPrefabContactReachesWindArrivalAndStopsWithoutObstacles`, which reads the actual saved prefab/material, requires the validated settings and the same arrival/stopping evidence. It also runs an explicitly named historical null-contact wind control and the zero-friction control, retaining their literal settings and measurements in output. No test is skipped and no ongoing requirement demands that the old configuration fail. The original raw baseline files and hashes above remain preserved.

Three candidate runs each simulate 300 steps / 5 seconds: stationary with no wind; actual wind; no wind with an initial +Z velocity of 3 m/s. All three result records are written before their acceptance assertions. The wind case must enter the real 3D arrival volume during a wind push, leave the actual field, then receive no later wind. Both moving cases must finish below 0.05 m/s and remain below that speed for the complete last second, with less than 0.02 m displacement in that second. No velocity reset, extra damping, sleep request, force change or stop command is issued after initialization.

Each candidate step checks an expanded oriented receiver box against the local scene. Non-floor proximity fails acceptance, preventing a wall/obstacle stop from being treated as friction evidence. A full query buffer fails immediately rather than hiding contacts. The inertia case must first travel more than 0.1 m; the stationary case must remain within 0.05 m of its starting position. These are bounded control checks, not a general thrown-object tuning specification. Exit pose/velocity, last wind tick, last-second speed/drift and non-floor proximity are recorded.

Independent online-lane source review found no blocker in the preserved input/settings parity, receiver-only material, arrival/exit/stopping controls or owned-scene/material cleanup. The post-step overlap is a bounded proximity observation, not a complete collision trace. `git diff --check` passed; compiler and native execution remain root-owned and NOT RUN for this candidate.

## Bounded production implications if validated

The existing editor setup is `Assets/_Project/Editor/Online/WordboundGameplaySetup.cs`, `CreateBodyPrefabs` / `Create`. Its bounded source addition creates `Assets/_Project/Materials/Wordbound/WordboundItemContact.physicMaterial` through AssetDatabase, saves/reads back its settings, and assigns it only inside the item-prefab branch of the existing load/bake/save workflow. It then reads the saved prefab assignment back. Root must validate the candidate before invoking this setup; source integration alone creates no asset.

The native PhysicsMaterial remains directly editable in Unity's Inspector. Setup never resets an existing material's edited settings or replaces a different assigned contact material: it stops with an inspection/revalidation message. Player/platform branches are unchanged; the native prefab regression checks that their original null contact assignments remain. No handmade `.meta`, serialized asset text, shared-floor or original-game edit is involved.

Online-lane independent source review of the setup/regression delta against `ef17ea3` found no blocker: item-only assignment follows existing contract checks, save/readback preserves asset identity, custom conflicts fail without replacement, and actual-prefab runs retain the copied material while candidate/control runs are explicit. No compiler or native execution was performed by either reviewing lane.

- Held/carried items are inactive and kinematic (`WordboundPhysicsPolicy`); `WordboundNetworkBody` disables their colliders. Contact friction does not act while held.
- Deployed items stay kinematic, so their own friction cannot move them. Their surface friction can affect another dynamic body touching them.
- World and thrown items are dynamic. Lower contact friction changes sliding and settling after contact; thrown bodies retain their existing ContinuousDynamic collision mode (`WordboundBodyMotion`). The candidate's no-wind inertia check addresses stopping on this floor, not every throw, slope, stacking or impact.
- Player/floor contact properties remain unchanged, but player/item contacts and item/item contacts can change wherever `ShouldCollide` permits them. Clean/Brawl collision policy is not changed. These interactions still require the actual native gameplay route; isolated solver success is not a full puzzle, combat, multiplayer or Windows build pass.

## Narrow root-owned validation sequence

1. Run `Wreckabulary.Tests.WordboundNativeWindPhysicsTests.CandidateItemContactReachesWindArrivalAndStopsWithoutObstacles` before material application.
2. Only after it passes, invoke `Wreckabulary.EditorTools.WordboundGameplaySetup.CreateBodyPrefabs` in Unity. Inspect the generated material, item-prefab diff and readback. Repeating the setup should retain the same asset identity/settings and assignment.
3. Run both methods in `Wreckabulary.Tests.WordboundNativeWindPhysicsTests`; the actual-prefab case covers five local solver comparisons and the candidate case three. Also select `Wreckabulary.Tests.WordboundBodyMotionTests.ThrownBodyUsesContinuousDetectionAndSafeFreezeResumeTransitions`, `Wreckabulary.Tests.WordboundPhysicsPolicyTests.EconomyModesHaveConsistentCollisionAndSimulationFlags` (six cases), and `Wreckabulary.Tests.WordboundPhysicsPolicyTests.TeamRacePreventsRivalPhysicalInterferenceAndBrawlProtectsCriticalResources`. These eight existing checks protect motion/collision policy, not broad impact feel.
4. Rerun the existing `SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints` physical flow to verify actual runtime wind, receiver arming, arrival and canonical checkpoints together. Do not infer this pass from the isolated solver.

Recovery checkpoint: own branch `sethypagna/wind-contact-calibration`, based on root `7c7d3d8`; prior QA/art branches and unrelated untracked log preserved. Scope is this report, the owned native fixture and existing editor prefab setup. No Unity, compiler, Blender or test process launched by this lane.

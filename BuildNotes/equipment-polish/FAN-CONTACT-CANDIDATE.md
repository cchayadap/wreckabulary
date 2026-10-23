# FAN item-contact candidate

Status: SOURCE READY; independent source review PASS; candidate native test NOT RUN. No production material or prefab assignment changed.

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

The separate original baseline test remains intact, including its known failing default-contact assertion. Run the new exact candidate method first, not the whole class as a green gate.

Three candidate runs each simulate 300 steps / 5 seconds: stationary with no wind; actual wind; no wind with an initial +Z velocity of 3 m/s. All three result records are written before their acceptance assertions. The wind case must enter the real 3D arrival volume during a wind push, leave the actual field, then receive no later wind. Both moving cases must finish below 0.05 m/s and remain below that speed for the complete last second, with less than 0.02 m displacement in that second. No velocity reset, extra damping, sleep request, force change or stop command is issued after initialization.

Each candidate step checks an expanded oriented receiver box against the local scene. Non-floor proximity fails acceptance, preventing a wall/obstacle stop from being treated as friction evidence. A full query buffer fails immediately rather than hiding contacts. The inertia case must first travel more than 0.1 m; the stationary case must remain within 0.05 m of its starting position. These are bounded control checks, not a general thrown-object tuning specification. Exit pose/velocity, last wind tick, last-second speed/drift and non-floor proximity are recorded.

Independent online-lane source review found no blocker in the preserved input/settings parity, receiver-only material, arrival/exit/stopping controls or owned-scene/material cleanup. The post-step overlap is a bounded proximity observation, not a complete collision trace. `git diff --check` passed; compiler and native execution remain root-owned and NOT RUN for this candidate.

## Bounded production implications if validated

The existing editor setup is `Assets/_Project/Editor/Online/WordboundGameplaySetup.cs`, `CreateBodyPrefabs` / `Create`. Any later assignment must use Unity's AssetDatabase and the existing prefab workflow so the material remains Inspector-editable and Unity produces its own metadata. Limit assignment to `WordboundItemBody`; preserve original game/shared floor/player/platform assets. Root must validate the candidate before this source/asset step.

- Held/carried items are inactive and kinematic (`WordboundPhysicsPolicy`); `WordboundNetworkBody` disables their colliders. Contact friction does not act while held.
- Deployed items stay kinematic, so their own friction cannot move them. Their surface friction can affect another dynamic body touching them.
- World and thrown items are dynamic. Lower contact friction changes sliding and settling after contact; thrown bodies retain their existing ContinuousDynamic collision mode (`WordboundBodyMotion`). The candidate's no-wind inertia check addresses stopping on this floor, not every throw, slope, stacking or impact.
- Player/floor contact properties remain unchanged, but player/item contacts and item/item contacts can change wherever `ShouldCollide` permits them. Clean/Brawl collision policy is not changed. These interactions still require the actual native gameplay route; isolated solver success is not a full puzzle, combat, multiplayer or Windows build pass.

Recovery checkpoint: own branch `sethypagna/wind-contact-calibration`, based on root `7c7d3d8`; prior QA/art branches and unrelated untracked log preserved. Only this report and the owned native fixture are changed. No Unity, compiler, Blender or test process launched by this lane.

# Item contact setup and native regression review

STATUS: BOUNDED SOURCE PASS. Reviewed pending `60f7780` and its actual integration merge `4941d97` against the preceding `9107b60` floor-boundary implementation. No production files changed and no Unity, compiler, setup or solver job was launched by this reviewer.

## Preservation and scope

- `Assets/_Project/Editor/Online/WordboundGameplaySetup.cs` assigns the new Unity-created `WordboundItemContact.physicMaterial` only to the item prefab. A different assigned material, an incompatible existing asset at the path, or edited owned material settings cause an exception instead of an overwrite. The saved prefab is reloaded to verify exact shared-material identity. Creation/loading/unloading use ordinary Unity APIs.
- No new player, platform or floor material assignment is present. The existing setup still bakes all three body prefabs; its resulting serialized asset diff must be checked by the root. Setup is not a transaction: it can create the contact asset before rejecting a different item assignment, and an earlier player bake may already have completed. Neither path silently overwrites the rejected material.
- This changes the shared Wordbound item body, therefore all dynamic items using it, rather than only the FAN receiver. The isolated STOOL result does not establish every equipment collision, thrown-body, player-contact or multiplayer behavior. The material remains editable in Unity; setup intentionally refuses unvalidated edits instead of resetting them.

## Actual-prefab regression

`Assets/_Project/Scripts/Online/Puzzles/Tests/WordboundNativeWindPhysicsTests.cs` now has two native methods: `ActualPrefabContactReachesWindArrivalAndStopsWithoutObstacles` and `CandidateItemContactReachesWindArrivalAndStopsWithoutObstacles`. Together they run eight isolated solver workloads: five actual-prefab/control runs and three candidate runs, each 300 steps at the actual 60 Hz policy cadence.

- The actual-prefab path checks the persistent asset path, identity and all five contact settings. Its bare receiver copies the real prefab Rigidbody/BoxCollider serialization, verifies component ownership and native settings, then applies the actual catalogue STOOL dimensions. It deliberately does not instantiate a Fusion lifecycle.
- Candidate and actual-prefab workloads retain null course surface materials; the explicit zero-friction control alone changes those surfaces. The actual-prefab method also verifies the current player/platform prefabs retain their null materials.
- Acceptance requires arrival during an actual policy wind push, the unchanged approximately 0.800 m horizontal travel, later field exit without resumed wind, and natural rest. It separately tests an initial 3 m/s no-wind coast. No later scripted velocity reset, added damping or forced sleep is introduced.
- Non-floor overlap is checked at every step for these acceptance workloads, with a full-buffer failure rather than silent truncation. All three workloads require zero non-floor proximity and low speed/drift throughout the final second. Only owned scenes and temporary control materials are destroyed; the persistent material is not in the destruction list.

## Merge verification

The actual `4941d97` merge retains `CourseSensorBoundary.ContainsFootPoint` for start and arrival, the raw `Bounds.Contains` observation, first-horizontal/raw/shared/lower-gap observations, `WB_WIND_SENSOR` and `WB_WIND_BOUNDARY`. It also retains the contact-mode branching and actual-prefab assertions. These overlapping areas were the important merge risk; no old exact-floor predicate replaced the shared arrival rule. No additional source conflict or blocker was found.

## Evidence and remaining checks

Read root evidence `BuildNotes/online/native-fan-floor-boundary-20261023.json`, log/XML `173251Z`: candidate **1/1 PASS**, gate 172.7 s, source `8174c8d`. Wind arrival occurred at tick 43 while pushed; the foot was 0.011444091796875 mm below the authored floor, inside the shared 1 mm cap. Field exit was tick 75; final velocity, final-second maximum speed/drift and non-floor proximity were zero. This is an isolated candidate-material solver result, not an actual-prefab, Fusion or Windows acceptance result.

Next: run editor setup, inspect the generated material and prefab diff, then run the two named methods plus the affected collision/throw policy checks. Full six-family physical gameplay, public networking and Windows behavior remain separate acceptance gates. Root reported R0 after material/atomic integration PASS `173658Z`; this reviewer did not run or independently inspect that compile artifact.
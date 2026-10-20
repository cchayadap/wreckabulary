# Native six-family puzzle flow

STATUS: SOURCE VERIFIED AGAINST INSTALLED SDK; NATIVE EXECUTION PENDING

Root approved a new isolated test assembly under `Assets/_Project/Tests/OfflineGameplay/PhysicalPuzzles`. This lane owns only new fixture files and this evidence directory. Existing session, gameplay, physical policy, scenes, prefabs, item data and production settings remain outside its write scope.

The fixture uses the actual flagship TimeRun source, exact resolved manifest, real item/rule bytes, existing native body prefabs, a Single runner and `WordboundPuzzleRuntime` callbacks. It reaches the six first-destination objectives in route order: CollectLetters, PlaceObject, RedirectWind, TimedMechanism, BuildRoute and BalanceWeight.

Fixture setup may reposition the host player Rigidbody between rooms and beside supplies. Those moves are recorded explicitly and do not prove human navigation. Craft, attack, deploy, use, recovery, physical observations and checkpoint publication use normal authoritative paths. No progress event, solved state, physical evidence, damage amount or synthetic mechanism completion is supplied by the test.

Positive evidence includes the marked prop's actual destruction and released letters, wrong-socket rejection, native wind displacement, CLOCK's effect on a real canonical sorting arm, a BED-induced upward launch with movement into the arrival volume, and coverage of both weight switches. Each completion must survive canonical checkpoint import. Scene-registration changes are SessionState-backed and restored in teardown even after assertions fail.

Next action: finish source and inspect it without launching a compiler. Root owns the current build/native window and will grant the next reference-compilation slot. Native execution remains root-owned; source authoring is not an acceptance result.

Source checkpoint: the two-part fixture and explicit test asmdef are drafted. The online owner completed a bounded read-only review with no confirmed lifecycle or setup false-pass. Native-only risks to retain in diagnostics are actual collider support after the small setup height offset, reaching UsePuzzle while physical completion remains valid, and exact checkpoint freshness. The offline session currently publishes synchronously; the fixture still requires the committed tick to include each solve.

The test is `WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints`, category `LocalWordboundPhysicalPuzzles`, opt-in environment `WORDBOUND_NATIVE_PUZZLES=1`. It requires the existing root-generated Player/Item/Platform body prefabs and `WordboundPhotonProof.unity`. It uses a six-minute cancellation budget after entering Play Mode and restores the previous build-scene registration through SessionState.

An ignored local `ReferenceCheck.csproj` is prepared to compile only these new test sources against installed Unity/Fusion and integration production DLLs, with warnings as errors. It has not been run. No production source or native setting has been edited.

## Reference-verified checkpoint

The new test sources compiled as their own assembly against installed Unity 6000.6.3f1, Fusion and the current integration production DLLs: PASS, zero warnings/errors, 8.29 seconds. The initial compile found one missing `Wreckabulary.Online` namespace import in the action partial; that source-only correction is included. The test assembly SHA-256 is `65076A6E5013DF6230E23614F5F30F446D5417F07059D65B0627AC8DDAC7084C`. No production source was linked into the test compilation, so the check preserves the native assembly access boundary.

Native test filter: `Wreckabulary.OfflineGameplayTests.WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints`. Category: `LocalWordboundPhysicalPuzzles`. Required environment: `WORDBOUND_NATIVE_PUZZLES=1`.

The fixture checks six families through actual authoritative commands and the real physical evaluator, including negative wrong-socket/waiting checks, HAMMER destruction of authored furniture, released letters and craft completion, native wind displacement, canonical CLOCK fixture phase slowing, BED launch, and both weight pads. It then imports a fresh canonical checkpoint after every solve. Host-only setup moves are explicit and counted; this is not evidence of human traversal, race duration, a full three-lap finish, online migration or performance.

Compiler slot released to the online owner's one native-fixture reference build. Next recoverable action: root integrates this additive checkpoint, lets Unity create metadata, and runs the opt-in test with the existing native scene/prefabs. No Unity process was launched in this lane; runtime result is NOT RUN.

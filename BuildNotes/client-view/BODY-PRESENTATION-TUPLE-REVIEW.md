# Body presentation tuple independent review

STATUS: COMPLETE — bounded SOURCE PASS for `766f212b4b2ee33fab94f8b93303c07530262212`; compiler/native/performance execution NOT RUN by this reviewer.

Reviewed the exact GameplayBody/runtime and one native-local test delta, plus the implementation report. Root baseline `aea79369` and candidate parent have identical Git blobs: GameplayBody `b6f32512ccc42367ae59a3d20eae0cbcb969c4a4`, native-local tests `0e0bd522f5fe1473f911726d6f6264ef65d9f231`. No baseline-copy commit or unrelated ModeDefinition change belongs to this delivery. `git diff --check` passes.

## Preserved runtime boundaries

The cache is instance-local and contains only a copied raw NetworkString, exact Vector3 size, carrier and successful converted key text. Vector3.Equals avoids approximate comparison; the installed Fusion API supports the typed NetworkString equality call. The original WorldVector finite check and WordboundBodyPresentation bounds/identity/carrier constructor still execute on every changed tuple. The initialized flag and cached fields are assigned only after both succeed. An unsuccessful scalar validation cannot become a cached success.

The cache is distinct from appliedSize/appliedKey/appliedCarrier. Original collider work still runs when size differs; a collider failure does not advance appliedSize, so repeated bad capsule configuration still throws. Applied key/carrier are still updated before PresentationChanged, preserving callback exception/reentry behavior and the visibility-loop ordering. Configure still writes all networked fields and immediately calls Apply; FixedUpdateNetwork and Render retain their guards. Spawned invalidates the scalar cache without deferring any shape operation. No network layout, authority, snapshot, physics or command behavior is intentionally changed.

There is no early return for an unchanged tuple. VisualRoot lookup, hierarchy scan and renderer.enabled restoration still run after callbacks on every Apply. This deliberately preserves late nested renderer discovery, BodyActive changes and the current presentation ownership contract. World/held hierarchy replacement, wardrobe masks and eventual renderer-scan optimization remain separate work.

The successful-validation counter and friend attribute exist only under UNITY_EDITOR. The friend name exactly matches `Wreckabulary.Offline.NativeGameplay.Tests`; that existing assembly already references Gameplay and the installed Fusion dependencies. No new public player API, callback or telemetry is introduced.

## Native test review

One new direct-Run UnityTest increases the existing local fixture to 11 cases and retains `WORDBOUND_LOCAL_GAMEPLAY=1`. It creates fresh item/player prefabs in the actual isolated local Runner and registers only those clones in a second physics world. It does not rebind the controller's bodies or call SetPlayerObject. There is no yield until the second world's simulator subscriptions and all surviving clones are disposed/despawned; the original controller must then advance and remain fault-free.

The item validation count trace is coherent: initial 1; repeated Configure/FixedUpdateNetwork/Render remains 1; carrier 2; next representable float size 3; key 4; prior tuple 5; throwing callback 6 and retry still 6; same-tuple callback reentry 7. Size-only change does not fabricate a key/carrier callback. The first callback sees the updated collider before visibility restoration, and a thrown callback leaves the renderer disabled until the next successful Apply. A late nested renderer and canonical active-state changes prove the unchanged tuple cannot bypass current visibility work.

The player trace is valid 1, scalar-valid but wrong capsule size 2, repeated collider failure still 2, restored tuple 3; it distinguishes pure validation reuse from collider success. Despawned invokes world.Unregister, so the replacement item is not left colliding with the registry's prior ID. The new spawned body starts at zero validations and reaches one on Configure. Finally detaches the custom callback, disposes the secondary world and despawns owned objects even after an assertion failure.

These assertions are source-reviewed, not observed native results. The case does not inject malformed replicated bytes, demonstrate remote rollback, force reuse of the same pooled component, or measure allocations/time. Its explicit return to a prior tuple covers cache invalidation locally. Root should run R0 and the exact new case or full 11-case local fixture before claiming runtime acceptance; source-level removal of redundant validation/conversion is not a measured FPS gain or online-stall fix.

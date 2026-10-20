# Runtime gameplay composition

STATUS: SOURCE VERIFIED; NATIVE INTEGRATION PENDING

Lane: `wordbound/runtime-gameplay`, branch `sethypagna/puzzle-race`. Scope is additive `Assets/_Project/Scripts/Online/Gameplay`, its tests, and this progress directory. Root owns native scenes, prefabs, metadata, Unity execution, and existing session/physics files.

Baseline: merged integrated `c9844ca` into the lane without conflicts, preserving the separately committed geometry and physics history.

Accepted contract: controller initializes the real authoritative match through a fail-closed world query relay, exposes its Fusion authority for session construction, then attaches that session. Each runner receives local static geometry and input callbacks. Only an admitted, durable-started host creates canonical body entities and advances physics/rules. Client checkpoints update display/input frontiers only. Runner replacement destroys local composition state and rebuilds from the session's restored canonical match after admission.

Root receives explicit faults, body/visual/camera events, and host-only platform/observation hooks. Existing course documents contain no complete motion profiles or oriented puzzle sockets; unsupported puzzle predicates remain fail-closed rather than inventing acceptance.

Current action: coherent source slice implemented, independently reviewed, and verified against the installed Unity/Fusion assemblies. No Unity launch or handwritten metadata.

## Setup and public API

Attach `WordboundGameplayController` to the caller's persistent bootstrap object outside the runner's replaceable gameplay scene.

1. Construct `WordboundGameplayPrefabs(playerNetworkPrefab, itemNetworkPrefab, platformNetworkPrefab)`. Each prefab needs the existing `WordboundNetworkBody`, Rigidbody, NetworkRigidbody, root NetworkObject, and new `WordboundGameplayBody`. Player requires the feet-origin capsule; item/platform require root BoxCollider. All roots have unit scale. Set NetworkRigidbody.InterpolationTarget to a collider-free `Visual` child.
2. Call `Initialize(verifiedOnlineMatch, mode, resolvedCourse, courseCatalogue, itemCatalogue, rules, localPlayerId, prefabs, options)`. The resulting `controller.Authority` is the actual `WordboundFusionAuthority` passed into the existing session constructor.
3. Call `AttachSession(session)` before `await session.Start()`. The controller's Update calls public `Poll()`; native fixtures may call it directly. `options.GameplayScene` may name the root-provided registered scene. Only the host requests additive loading with Physics3D; restored scene information avoids duplicate loads. Clients wait for Fusion's scene replication. Every peer verifies the actual runner DeltaTime is 1/60 before content readiness.
4. Static course geometry is built once per runner identity and moved into that runner's isolated physics scene before `MarkLocalReady`. On the host, `GameplayStarted` must follow a durable initial checkpoint. Only then does the controller construct PhysicsWorld, spawn every canonical body, apply the complete snapshot, and enable local host simulation.
5. PhysicsWorld's completed-step callback invokes public `AdvanceHost(poses)`, which verifies the exact body-ID set, advances the real authority, reconciles all added/removed/inactive bodies, and applies the completed snapshot before returning. No client advances its local domain copy. Public `Frame`, `Bodies`, `PhysicsWorld`, `CourseWorld`, `IsSimulationPaused`, `FrameChanged`, and `HostAdvanced` support native probes and UI.
6. `Submit(action, target, revision, slot, value)` constructs a bounded reliable command with its canonical epoch/frontier and sends it through the authenticated session. Actor identity is never serialized in the command body. `ReadMovement` returns world-space bounded axes/jump; the controller supplies the canonical epoch and writes Fusion input on the exact requesting runner.
7. `Stop()` freezes/disposes local composition before stopping the session. `Fault` / `Faulted` report and stop unrecoverable lifecycle failures instead of leaving a silent local freeze.

## Presentation and real mechanism hooks

`WordboundGameplayBody` replicates finite bounded collider dimensions, approved visual key and carrier seat. Player dimensions stay fixed; props resolve through the pinned course AssetId and crafted items through the item catalogue. Box origins are at the feet. Metadata reaches clients through Fusion rather than depending on delayed full checkpoint arrival. The component refreshes all renderers under Visual from BodyActive, including models created after the original NetworkBody renderer cache.

`ConfigureVisual(view)` creates or selects approved models under the collider-free Visual child. It receives `view.VisualKey`, `ShapeSize`, `CarrierSeat`, `Body`, and `VisualRoot`. A separate carry visual may attach to the indicated player's hand; the inactive canonical inventory body's world collider remains disabled. `LocalPlayerChanged(bodyOrNull)` attaches/detaches the local camera without moving the physical root.

Trusted `BeforeHostPhysics(frame, bodyMap)` runs in a runner SimulationBehaviour at order 900 before Fusion's physics order 1000. It may apply real kinematic platform movement to registered Rigidbodies; the ordinary capture then persists the resulting exact canonical pose. `ObserveHost(frame, poses)` supplies bounded progress/damage/mechanism observations to the authority. `PuzzleEvaluator` is required for physical puzzle acceptance; absence remains false. This slice does not invent oriented sockets, motion profiles, team-specific gate mechanics, held models, or complete player UI absent from the authored content.

## Recovery and authorization boundaries

The session owns signed admission, coordinator presence, durable cloud acknowledgements and committed recovery. The controller does not trust arbitrary network poses or call RestoreCommitted on a replica. New runner identity cleans local physics/static objects and input/camera bindings, then rebuilds only after the session's restored canonical authority and fresh content readiness. All ledger bodies, including hidden inventory and retained destroyed items, are reconciled by stable ID; collection removes precisely the consumed tile object.

Independent review identified session-global readiness surviving replacement and domain reconnect preceding authenticated Ready. The online lane repaired both in `8cc5cb0`; that session commit is a required integration dependency. A separate client pause-state error was fixed here with focused regression cases.

Presence is polled synchronously in the pre-physics driver and again before authoritative advancement, not only in Update. If the clock crosses lease expiry within reconciliation, a missing live admission disconnects only that canonical seat and re-captures the frame; retry is bounded by the eight-seat roster. Expiry between preflight and a new body's BindHost guard despawns that incomplete object and follows the same retry. Duplicate admitted peers remain fatal. Existing body identity assignment uses the transaction's preflight peer map, while movement always rechecks the live session guard.

## Verification and next action

The final actual Unity/Fusion reference build passed with zero warnings/errors (44.39 seconds), including the presence-boundary retry and spawn-cleanup changes. All 16 focused policy cases passed (one second, zero skipped): five mode cards, real letter collection and carried furniture ledger changes, authored prop dimensions, bounded/duplicate reconciliation input, exact snapshot ownership, reliable command sequence/epoch boundaries, independent host/client pause state, and eight-seat Squad/FFA/Freestyle reconciliation. Results are locally recorded in `TestResults/runtime-gameplay/gameplay-policy.trx`.

Independent online-lane source review closed with no further blocking findings after the readiness, pause, and lease-boundary repairs. Required integration dependencies are session readiness repair `8cc5cb0` and the root's authored prop yaw correction `9f1b051`. Native Unity tests are NOT RUN in this lane.

Next recoverable action: root integrates the new Gameplay directory, imports metadata/prefabs and weaves `Wreckabulary.Online.Gameplay`. Native proof must cover real jump/carry/craft/throw/platform motion, exact all-body reconciliation on 8 peers, lease expiry before the physics driver and during spawn/admission guards, held visibility/collision on clients, camera detachment, and migration with a fresh local-world readiness round trip. Compile/policy passes do not establish those native outcomes.

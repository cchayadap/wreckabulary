# Runtime physics adapter

STATUS: SOURCE VERIFIED; NATIVE INTEGRATION PENDING

Lane: `wordbound/runtime-physics`, `sethypagna/puzzle-race`. Parent owns native Unity, prefab generation, scene setup, probes, and final integration.

Scope: additive `Assets/_Project/Scripts/Online/Physics` assembly `Wreckabulary.Online.Physics`; stable canonical body identity, state capture/restore, host-only movement from authenticated Fusion input, and runner-local physics queries. Rigidbody + NetworkRigidbody only; no NetworkTransform and no native SDK source vendoring.

Dependency action: requested online delta cherry-pick found missing preceding history; aborted cleanly. Parent authorized merging integrated `bf8a31b` to preserve both histories. Merge completed automatically without source conflicts.

Current action: source implementation complete and focused checks green. Parent released the compilation hold and authorized a separate physics commit. Parent committed the separate geometry correction as `7792121`; this physics increment does not change those paths.

## Public integration contract

- `WordboundMovementInput : INetworkInput` carries authority `Epoch`, bounded `MoveX` / `MoveZ`, and `Jump`. Actor identity comes from the spawned object's `InputAuthority`; there is no client-supplied actor ID, bespoke movement tick, or reliable-command sequence.
- `WordboundNetworkBody : NetworkBehaviour` binds through `BindHost(world, state, inputAuthority)`, applies `ApplyCanonical(state)`, and exposes `CaptureHost()`, `Rigidbody`, `OwnedColliders`, and `Canonical`. Networked metadata includes body/actor/source IDs, kind, team, protection, recovery generation, activity, kinematic flag, and economy item mode.
- `WordboundPhysicsWorld : IWordboundWorldQuery, IDisposable` takes the running server runner, pinned binding/mode/course/catalogue/rules, `Func<PlayerRef,string>` authenticated actor lookup, `Func<bool>` readiness callback, and optional trusted puzzle evaluator. The lookup should wrap `WordboundFusionSession.TryGetAdmittedActor`; absent or expired admission returns null.
- Root calls `RegisterStatic(courseWorld)`, `Register(body, state, inputAuthority)`, `Unregister(body)`, `ApplySnapshot(snapshot)`, and `CaptureHost()`. The bounded proof implementation supports at most 512 registered bodies. It never spawns or destroys caller-owned objects.
- `HostStepCompleted(IReadOnlyList<WorldBodyState>)` fires after runner physics. The host handler advances authoritative rules with these poses, reconciles canonical body spawns/despawns, and calls `ApplySnapshot` with the completed frame before returning. A missing handler freezes simulation; a handler that does not apply the resulting frame causes a permanent fail-closed freeze.

## Setup order and prefab contract

1. Start the host runner with a local isolated 3D physics scene and the approved 60 Hz tick configuration. Install/register `RunnerSimulatePhysics`. The adapter rejects the global default scene.
2. Build/move the static course world into that runner scene. Construct the physics world; readiness and admission delegates may close over the authority/session initialized afterward. Subscribe the completed-step handler before making the match ready.
3. Spawn/reconcile canonical bodies through the root adapter and register them. Active player input authority must be the currently admitted peer for the exact sorted binding seat; inactive disconnected seats may restore with `PlayerRef.None` until re-admission. Root assigns the newly admitted peer before activating that seat. Other bodies have `PlayerRef.None`. Register static colliders and apply the initial validated snapshot before opening gameplay.
4. Use a unit-scale player root located at its feet, with a root Rigidbody and CapsuleCollider centered at `(0, 0.9, 0)`, height `1.8`, radius `0.3`. The body adapter locks rotation X/Z. Put visuals in a separate collider-free interpolation child and keep the camera separate. Set `NetworkRigidbody.InterpolationTarget` to that visual child. Do not add NetworkTransform or independent child rigidbodies.
5. Feed only the authenticated host's movement through `FixedUpdateNetwork`. Missing or invalid input produces neutral movement with friction and no jump. Ground support is runner-local, near the capsule feet, and excludes rising players from the grounded jump path.
6. During recovery/readiness pauses, canonical bodies are made kinematic before physics. Exact immutable ledger values remain available until a real host simulation step; repeated equal poses/velocities are not rewritten. A new epoch may restore an earlier committed tick; the same epoch may not rewind.
7. Dispose the world before runner shutdown. The adapter unsubscribes physics callbacks and freezes owned bodies; root remains responsible for object destruction and runner lifetime.

## Ownership, visibility and query limits

All ray, sphere, and overlap queries use only the runner's isolated `PhysicsScene`; only explicitly registered static and body colliders participate in query decisions. Collision ignores are applied only between registered body collider pairs. Clean race teams cannot physically interfere; Brawl retains shared ordinary collisions while protected rival resources stay isolated.

Held/carried/crafting item bodies must be canonically inactive and kinematic. Their world colliders/renderers are disabled, while the NetworkObject remains alive for replication. Deployed bodies are active/kinematic; thrown and loose world bodies are active/dynamic. Inconsistent economy/body flags reject the entire snapshot before application. A visible hand-held visual attachment is outside this first proof slice and must not be implemented by leaving the hidden world's collider active.

Placement validates approved recipe, finite bounded dimensions, actor line of sight, overlap clearance and four supporting footprint corners. A puzzle query fails closed unless root supplies its trusted puzzle evaluator. Dynamic terrain/recipe-specific behavior and combat damage callbacks remain root authority integration responsibilities.

## Verification and recovery point

- Final actual Unity/Fusion reference compilation: PASS, zero warnings/errors, 4.26 s. Local recipe `ReferenceCheck.csproj` references installed Unity 6000.6.3f1 netstandard 2.1/UnityEngine assemblies, integration's Wreckabulary/Rules/Fusion bridge/Physics add-on assemblies, and installed Fusion precompiled assemblies. No stubs or SDK source copies were used.
- Final real NUnit pure policy run: PASS 22/22, zero skipped, 260 ms test duration, `TestResults/runtime-physics/physics-policy.trx`. The focused local recipe uses NUnit 3.14, NUnit3TestAdapter 5.0 and Microsoft.NET.Test.Sdk 17.14.1. It covers axes/epoch bounds, signed seat identity/admission, inactive seat recovery, exact ledger capture, identity/pose limits, team/protected collision decisions, and economy body activity modes.
- Final checks include item-mode consistency preflight, inactive seat restoration without an admitted peer, required owned collider, same-epoch rewind rejection, unchanged-pose write avoidance, near-feet rising-aware ground handling, neutral missing input, and bounded deployment footprint.
- Native Unity NOT RUN in this lane. Reference compilation does not prove native collision quality, multi-peer state replication, client prediction, performance, or migration behavior.

Next recoverable action: parent integrates the separate physics commit, generates metadata/prefabs, weaves `Wreckabulary.Online.Physics`, and runs native checks for input ownership/epoch, two isolated runner scenes, ramps/doors and jumps, held/deployed/thrown collisions, exact frozen capture/restore, and recovery without duplicate canonical body IDs. The ignored local reference/test recipes and their generated outputs remain available in this lane but are not vendored into the project.

# Atomic collision-pair registration

Status: SOURCE COMPLETE; independent bounded SOURCE PASS from recover_input. No compiler or tests run in this lane. Root owns all heavy execution. Scope is WordboundPhysicsWorld.cs, focused existing native physics/local atomic tests, and this report; no controller, match, lease, protocol or SDK changes.

Baseline: exact PhysicsWorld and WordboundNativeLocalGameplayTests bytes match integration e5bce9f1. Initial Register rebuilds all accumulated body pairs, producing N(N+1)(N-1)/6 pair visits for N additions; 186 bodies imply 1,072,445 visits instead of 17,205 distinct pairs. This operation count does not establish timing causation for the 192928 startup stall.

Compatibility constraint: preserve standalone Register's immediate native pair state and callback-visible permissions during atomic preparation. Public ApplyCanonical can alter an existing body's policy between registrations, even though normal restoration occurs through ApplySnapshot. Abort/throw/retry/disposal must not permit simulation with partial or stale state; no validation or exception guard may be skipped.

## Implemented contract

- Within one atomic reconciliation, a newly registered body immediately receives its native pair policy against every existing body. A private per-attempt index retains exact body and immutable canonical references plus a defensive copy of collider identities. Any existing registry/reference change causes a full refresh before Register returns. Direct ApplyCanonical is therefore conservative: even a pose-only replacement invalidates this baseline. The cache is cleared in finally and is not allocated for ordinary reconciliation without additions.
- A successful unchanged N-body initial composition performs N incremental passes and one final full pass: N(N−1) decisions including the final apply. For 186 bodies that is 34,410 decisions, compared with 1,072,445 registration-only decisions before this change. These are algorithmic operation counts, not measured milliseconds or FPS.
- Standalone Register still performs its immediate full refresh. Duplicate registration and already-bound body rejection remain. Full public ApplySnapshot validation and the atomic initial/fresh mutable validation phases are unchanged.
- An actual registry mutation during an atomic attempt sets a pending-composition freeze. An abort without mutation remains resumable. A partial abort/throw flushes dirty native pairs in finally, retains the freeze, and permits a fully validated retry. An incomplete pending registry cannot clear the freeze through public ApplySnapshot. Only successful complete application and collision-policy refresh clear it. Callback exception identity is preserved if cleanup also fails; a cleanup failure leaves the world failed/frozen.
- Existing project callbacks change native body visibility through ApplyCanonical, which changes the captured reference. This is not a general interception mechanism for external direct Physics.IgnoreCollision calls or native component disable/re-enable cycles outside the canonical API. No such supported preparation callback has been identified in the inspected source.
- Three cumulative read-only counters exist only under UNITY_EDITOR/GSK_QA: full passes, incremental passes, and pair decisions. They are not a timing profiler or shipped telemetry.

## Native regressions queued

Two new Unity cases extend WordboundNativeLocalGameplayTests from eight to ten: AtomicCollisionRegistrationKeepsSharedNativePairsAndAbortRecovery (Freestyle) and AtomicCollisionRegistrationKeepsIsolatedNativePairsAndAbortRecovery (TimeRun).

Each uses the real running local Fusion runner, authored course state, registered body prefabs and native Physics.GetIgnoreCollision. It creates a second temporary PhysicsWorld with freshly spawned clone bodies only; it never binds or unregisters the live controller's bodies and never changes SetPlayerObject. There is no yield or physics simulation while these clones exist. The temporary world is disposed and its clones despawned before yielding, then the original controller must advance without a fault.

The fixture checks callback-visible pair policy after every addition, exactly one full pass for successful unchanged initial setup, direct canonical-policy mutation forcing a full fallback, immediate standalone registration, duplicate registration, repeated removal, rejected body rebinding, nested public/atomic apply rejection, no-op abort resumability, partial abort and exact exception identity, frozen incomplete apply, successful retry, disposal, and original-world continuation. The partial-world checks assert the actual production ShouldFreeze gate; they do not advance the temporary partial world or claim packet/online acceptance.

Root follow-up, sequentially through the installed gate:

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
$env:WORDBOUND_LOCAL_GAMEPLAY = '1'
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.OfflineGameplayTests.WordboundNativeLocalGameplayTests' --cold --timeout 3600 --json
```

Root chooses any additional public comparison and profiler flags. Expected local class total is ten, with zero ignored cases when the flag is set. The 192928 run's eight local cases were ignored because the flag was absent; the later 194601 run's existing eight cases passed. Neither result executes this candidate. After native correctness, compare real initial composition, writer readiness and visible Windows progression with unchanged deadlines and content. No speed or lease-resolution claim is established by source review.

Independent review confirmed the pending-mutation fence, exact-reference fallback, callback-visible native policy, standalone behavior, abort/throw/retry cleanup and clone-only test isolation. No supported callback that directly mutates native collision state outside ApplyCanonical was found. Compilation and actual pair assertions remain NOT RUN. The unrelated ModeDefinition edit and existing untracked protocol/log evidence are excluded from this checkpoint.

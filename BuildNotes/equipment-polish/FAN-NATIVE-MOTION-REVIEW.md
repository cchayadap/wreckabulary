# FAN native motion diagnosis

STATUS: NATIVE FAILURE REVIEWED; cause isolation pending. No production change or native job run by this reviewer.

## Measured failure

Root's `TestResults/editmode_20261023T160217Z.xml` reports 0/2 selected cases passed. The physical fixture failed in 218.115201 seconds at FAN arrival; its unchanged 12-second wait observed 13.189 seconds, ticks 4320–4620. The six-minute fixture lifetime was not cancelled and gameplay was running/unpaused.

- Mechanism was **Armed**, activated at 4262, with 359 progress ticks and receiver `item:48`.
- Receiver start `(39.000, 0.020, -20.250)`; native and canonical end `(39.000, 0.001, -20.236)`. Horizontal progress was only **0.014 m**. Final velocity `(-0.006, -0.009, -0.007)`; item remained World/dynamic.
- FAN remained Deployed/kinematic at `(39, 0, -21)`, facing +Z. Field containment was true; receiver remained inside its start bounds and outside arrival.
- Arrival was X `[38.75,39.25]`, Y `[0,0.9]`, Z `[-19.45,-18.95]`. Initial travel to the near edge is **0.800 m**; remaining travel at failure is **0.786 m**.
- Raw line query reported one `WordboundItemBody(Clone)` hit at 0.534 m. The query does not print canonical collider identity. Production `ClearLine` ignores the FAN and receiver themselves, so this raw hit alone does not prove occlusion. Its distance is consistent with the receiver's near face, but that identification remains an inference.

This run refutes the floor-Y edge explanation for this failure: final foot Y is +0.001 m and the start sensor is true. It also proves actual arming, rather than a missing receiver or wrong recipe. Do not enlarge sensor bounds or extend the wait to address this evidence.

## Runtime and solver order

Reviewed `WordboundGameplayDriver`, `WordboundGameplayController`, `WordboundPuzzleRuntime`, `WordboundNetworkBody`, `WordboundPhysicsWorld`, and installed Fusion Physics Addon sources:

1. Driver order 900 calls `PrepareHostPhysics`. Any pending canonical reconciliation happens before the puzzle runtime adds wind velocity.
2. `WordboundFieldPolicy.WindVelocity` adds at most `8/60` m/s along the FAN direction per tick, retaining vertical/lateral components and capping along-wind speed at 8 m/s. The local runner explicitly selects 60 Hz, and the controller verifies its actual delta time.
3. `RunnerSimulatePhysics` order 1000 invokes `OnBeforeSimulate`, then the actual isolated physics scene. For an already dynamic receiver, `PrepareSimulation` only configures motion; it does not restore the prior canonical velocity. Player movement code does not apply to this item.
4. After simulation, body capture records the resulting pose/velocity. Host advance and reconciliation then restore that resulting canonical state. Fusion `NetworkRigidbody`'s engine restoration is at `BeforeAllTicks` when needed; its after-tick action copies to the network buffer.

No source-level velocity overwrite was found between the wind write and the solver for this dynamic body. That narrows the investigation; it is not a measured per-step proof.

## Minimum grounded next measurement

The strongest remaining hypothesis is contact/friction resisting the small per-tick velocity increment. This is not yet a confirmed cause. The item prefab has mass 5, gravity enabled, zero linear damping, angular damping 0.05, no frozen axes and no assigned physics material. `WordboundGameplayBody` makes the STOOL collider size `(0.52,0.466,0.52)`, centre `(0,0.233,0)`. The course floor is a box 0.24 m thick with its top at Y=0 and no assigned physics material. Project gravity is -9.81 Y; solver iterations are 6/1, contact offset 0.01, friction type 0, improved patch friction disabled. Null material references do not establish measured effective friction coefficients.

Root assigned an isolated native comparison to the online lane: the same STOOL body/floor/settings at 1/60 second, using the actual `WindVelocity` policy, wind versus no wind over 300 ticks. Its initial-to-arrival threshold must be 0.800 m. Record positions/velocities and distinguish the policy/solver test from the full Fusion/runtime route. If baseline wind again stalls, a bounded zero-friction/no-floor control can locate contact resistance before selecting a production material or force change. If isolated wind travels normally, inspect actual pre-wind/post-wind/pre-solver/post-solver values and blocking collider identity in the full route. Do not increase global FAN strength, alter user course data or bypass arrival evidence based only on the current endpoint.

## Related transfer result and evidence identity

TeamRace Solo also failed, in 91.246958 seconds. New diagnostics locate a transfer timeout: same mark/count/length/wire hash all true, duplicate chunk false, elapsed 17,972 ms against 10,000 ms. This is distinct from the FAN failure and is owned by the online/root lane.

- XML SHA-256: `CF0A9EF0EDB7431F1DEF6B2E407D7FA4FE9A3CD5CF341A6C09BE7DD2630D7AF7`.
- Matching `Logs/gate_test_editmode_20261023T160217Z.log` SHA-256: `9652EA38B96487ED796C59B1C472C030D59351437CFEBCC8C2E83B4289DCEF29`.
- `.harness/delivery/native-transfer-wind-diagnostics-stdout.log` records FAIL, 443.7-second gate, no crash-after-quit. Detailed diagnostic source preserves the same wait/actions and catches diagnostic errors without replacing the original failure.

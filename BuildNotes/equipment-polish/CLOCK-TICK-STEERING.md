# CLOCK fixture steering at simulation ticks

Status: SOURCE PASS after independent input-lane review. Native compilation/execution pending. Base root `a1c51af0`; isolated branch `sethypagna/clock-tick-steering`. The overlay branch and unrelated local log are preserved. No heavy job runs in this lane.

## Measured failure

Root `BuildNotes/online/native-clock-atomic-team-20261023.json` records native `181114Z`: the route starts at canonical tick 5400 and ends at 5640, with 240 advancing canonical samples but only four coroutine observations. All canonical gaps are one tick and up to 60 callbacks share one Unity frame. Waypoint zero is observed inside its unchanged 0.03 m tolerance at tick 5426 and only 0.002285 m away at tick 5614, yet the coroutine never advances it. The player oscillates across that point; later waypoints and the authored arrival are not reached.

This establishes a fixture feedback defect. It does not explain the separate one-tick Armed/Waiting changes or establish a complete six-family pass. Input lane owns the independently observed production CLOCK decision investigation. No production movement/physics tuning is included here.

## Installed SDK cadence inspection

The installed Fusion Runtime DLL SHA-256 is `D213B95E4694C167F5F6FAFFCBD75CA1E47F19A9249C91A4964F8342FAC9B560`. A bounded read-only Mono.Cecil inspection, without loading/running the Fusion simulation, establishes:

- `Simulation.Update` calls `CalculateInputTicks`, then loops `PollInput` (IL `0105`–`018a`) before the forward `StepSimulation` loop (`01b9`–`01e4`). Updating the fixture's `ReadMovement` callback alone would still queue the whole catch-up batch before observing its movement.
- `Simulation.InvokeTick` populates its current player-input collection before `ICallbacks.OnBeforeTick`, then calls `OnTick`. The public `NetworkRunner.GetRawInputForPlayer` resolves that current collection. Its implementation does not require the later private `IsInTick` flag.
- The public `NetworkInput.TrySet<T>` checks the existing input type and writes the current input's data. This avoids changing type or editing arbitrary past/future SDK buffers.

Photon describes input collection and tick-specific consumption, but its general fixed-timestep wording alone does not establish interleaved collection/physics within a catch-up frame. [Photon input documentation](https://doc.photonengine.com/fusion/v2/manual/input/player-input)

## Bounded fixture repair

Parent authorized adding one callback to the existing `UNITY_EDITOR`-only `WordboundNativeTickBatchAdapter`: implement `IBeforeTick` and forward its runner through an optional action. Existing batch counters/callbacks are unchanged. The action clears on destruction and fixture teardown, before session shutdown.

The physical fixture attaches that adapter before its actual local runner starts. It observes a real typed, epoch-valid input callback before starting the six-family sequence. During `MoveIntoArrival` only, each forward `GameMode.Single` tick evaluates the latest canonical player position, advances a reached approach waypoint with the existing 0.03 m condition, and computes the same analog direction formula. It writes that desired input through the public typed current-buffer API before the unchanged normal player motor consumes it. The final target still uses 0.08 m. No transform, Rigidbody velocity, force, target bounds, sensor, content, SDK configuration or deadline is changed for traversal.

The same wall deadline is checked within each tick as well as by the coroutine; completion, cancellation, expiry or a recorded steering failure produces neutral input. Epoch/type/local-mode/forward-tick failures fail the fixture rather than falling back to direct movement. The coroutine retains health, actual arrival and physical completion assertions; it no longer controls waypoint steering once per Unity frame.

The existing post-physics observation hook reads the public player input back and compares all movement fields with the input injected for that runner tick. The route requires a positive injection count, exact matching post-physics count and no mismatch/failure. This records the buffer available through the ordinary motor's physics step; it does not add a probe inside the motor or claim human input/network equivalence. Canonical poses, waypoint advance ticks, polling/batching cadence and final native pose remain separately recorded.

Source readiness and native rerun scope: existing `Wreckabulary.OfflineGameplayTests.WordboundNativePuzzleFlow.SixAuthoredFamiliesUseNativeSensorsAndCanonicalCheckpoints`, with `WORDBOUND_NATIVE_PUZZLES=1`. Inspect `WORDBOUND_PHYSICAL_INPUT_READY` before expensive traversal, then the per-route steering and physical decision evidence. Compilation, callback attachability, input availability and actual traversal all remain **NOT RUN** for this patch.

## Actual CLOCK decision observation

The follow-up fixture subscription depends on input lane runtime commit `f7bf3f1d`; that runtime source is not copied or edited here. Only the existing physical fixture partials and this report change. Independent input-lane source review is PASS for subscription lifetime, bounded aggregation, evaluated flags, error checks and unchanged movement/predicates/deadlines. Native execution remains NOT RUN.

For `TimedMechanism`, the fixture watches the actual sorting-arm body ID, subscribes before route traversal and removes its callback/filter in both the route's `finally` and fixture teardown. Each callback is the production `ClockAffects` decision for one eligible field at that watched fixture, before its physics movement. `NoEligibleSlowField` means the existing loop found no eligible field. Neither result is a measurement of the aggregate applied speed; canonical motion deltas remain separate evidence.

The fixture retains counts for each outcome, the first sample of each outcome, the last 24 outcome/source/fixture/blocker transitions, the final decision and an omitted-transition count. At most six first samples, 24 transitions and one final sample are retained, without formatting strings per callback. It records pre-physics completed tick, epoch, local component instance identifiers, geometric inputs, the evaluated predicate flags, actual ray hit count/capacity and blocker classification. Multiple fields can yield multiple events for one tick, so event count and distinct observed tick count are reported separately. A hit buffer at capacity is not complete ray evidence; default fields whose `Evaluated` flag is false are not predicate measurements.

The route requires a positive decision count and empty runtime/fixture observer errors. A callback disabled by the runtime after an exception cannot silently produce a successful diagnostic gate. Epoch changes and decreasing completed ticks are reported as fixture observation errors. These are additional evidence checks; all original steering, physical arrival, completion, tolerances and deadlines remain unchanged. Any native failure still needs its actual decision/geometry records before attributing Armed/Waiting changes to collision, containment or line of sight.

# Native gameplay operation diagnostics — independent source review

STATUS: PASS after bounded diagnostic exception guard; compiler/native execution NOT RUN in this lane

Reviewed root's change to `Assets/_Project/Tests/OnlineGameplay/WordboundNativeGameplayTests.cs`, from Git blob `b418ee2` to current blob `4167f881aef796e3cba6a9fc57891b4f94ea3f3d`. Scope is diagnostic safety and whether the existing movement/deadline verification is preserved. No source edits, compiler or Unity job were run here.

The two added counters observe input callback invocations and invocations with `peer.Move` enabled. The callback returns the same movement/interaction value as before. `Until` retains the supplied duration, predicate ordering, lifetime cancellation condition, health checks, one yield per loop and final completion assertion. No caller deadline, test timeout, movement policy or acceptance threshold changed. Begin/end logging and maximum yielded-iteration gap measurement add diagnostic overhead; they are not a runtime performance benchmark.

The initial draft had one concrete failure-path problem: `ObserveOperation` ran from `Until`'s `finally` and called `PhysicsWorld.CanMove`. That method reads `Single`/`SingleOrDefault` inventory/defense state, so invalid or disposing state could throw during diagnostics and replace the original failure. Root repaired this with per-peer `DescribePeer` try/catch and a whole-operation logging guard. An unavailable peer reports only its index and exception type; it does not abort other peer descriptions or replace the operation's failure. Closure was checked against the exact current blob above.

The reported fields are peer index, host flag, tick, counters, position, connection/movement/pause flags and the session's already-sanitized failure type. Review of `WordboundFusionSession` confirms `Failure` stores `error.GetType().Name`; the diagnostic catch paths also emit only exception type. No provider response, credential, URL or raw exception text was added. `Frame`, pause state and `CanMove` are read-only observations in the inspected implementation; no diagnostic state mutation was found beyond the counters.

No remaining blocking source finding was found in this bounded delta. Root must compile and run the selected native cases; this review does not explain the prior movement timeout or claim it is fixed.

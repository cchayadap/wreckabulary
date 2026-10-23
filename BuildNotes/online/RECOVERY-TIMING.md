# Native recovery timing evidence

STATUS: INSTRUMENTED, NOT RUN. This patch adds fixture-only observations through existing public events. It changes no production interface, networking setting, service lease, deadline or behavioral assertion.

The existing graceful host-departure fixtures and four recovery fixtures emit one `WORDBOUND_RECOVERY_TIMING:` JSON record for the successful recovery segment. A failure before readiness still emits the partial record when the iterator disposes; missing events remain null, with unobserved epoch/tick fields set to -1. A timing record is not proof that the entire fixture passed.

Every record identifies its exact stimulus: graceful `Controller.Stop`, an injected client `ConnectionTimeout` shutdown, or an injected disconnect notification followed one second later by graceful host stop. Credential/admission 503 injections are named separately. None of these is a packet blackout or process kill. The explicit-stop and rejection endings are behavioral tests after the timed successful rejoin and are not included in its elapsed duration.

For each survivor the record retains:

- The frame epoch/tick just before the stimulus, then the first nonempty `RecoveryStatusChanged` event's UTC Unix milliseconds and monotonic stopwatch ticks, including the frame observed at that detection.
- The first `FrameChanged` at the expected restored epoch on a replacement Runner, its timestamp and tick. Client rejoin expects the same epoch; host migration expects exactly the next epoch.
- The corresponding first `GameplayStarted` timestamp and exact monotonic elapsed ticks from detection and stimulus. The record includes stopwatch frequency and derived milliseconds so wall-clock adjustment cannot corrupt durations. Absolute stopwatch counters are decimal strings; elapsed ticks are exact integers and displayed milliseconds are invariant strings rounded to three decimal places.
- The signed difference `restored tick - old observed tick`. A negative value is observed rewind from that survivor's last sampled frame, not proof of maximum crash loss or compliance with the 120-tick provisional cap.

The group records the actual host-stop request separately, plus the time the existing `Ready` predicate succeeds for all survivors. `stimulusToAllReady` and `hostStopToAllReady` report the full observed interval to that predicate. The delayed-election case deliberately begins detection before real host stop, so both origins must remain visible.

Use the existing categories/filters in `FINAL-LIVE-CHECKLIST.md` and `NETWORK-ACCEPTANCE-GAPS.md`; new recovery cases are in `ExternalPhotonRecoveryGameplay` with `WORDBOUND_EXTERNAL_GAMEPLAY=1`. Root owns execution. Collect the raw marker from the gate's Unity log alongside its NUnit result. Compare detection-to-GameplayStarted per survivor and all-ready elapsed only after the fixture passes. Do not claim the ten-second detected-loss target, two-second rewind bound or adverse-network behavior until actual measured evidence supports the specific scenario.

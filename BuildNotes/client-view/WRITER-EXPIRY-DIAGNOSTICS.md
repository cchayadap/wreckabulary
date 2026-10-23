# Writer-expiry failure diagnostics

STATUS: Independent bounded source review PASS (recover_online). Compiler/native execution pending. No compiler, Unity or heavy job run in this lane.

Root's public batch `editmode_20261023T141231Z` had three failures reaching `WordboundFusionSession.Poll`'s writer-expiry branch. The constructed TimeoutException had no thrown stack origin, no stable diagnostic code and no timing facts. This narrow change exposes evidence through the existing single failure notification; it does not repair or relax an expiry.

`Poll` now records the most recent and maximum interval between eligible server poll calls in that RunScope, using the session's existing wall clock. It allocates no collection or log message per poll. Negative latest intervals remain visible if that clock moves backwards; maximum starts at zero. Writer acquisition, renew-attempt start, observed completion and most recent error timestamps are tracked separately for each run. A retired run can only update its own diagnostic fields.

On the existing writer-expiry branch, `FailureStage=writer-poll`, `FailureCode=writer-lease-expired` and a fixed `WordboundFusionSession.Poll` origin identify the source. Existing `FailureFacts` additionally includes:

- Latest/max poll gaps, remaining writer lease and acquisition offset, all in milliseconds.
- Renewal in-flight flag, attempt count, due offset, latest start offset and latest observed completion offset when present.
- Most recent renewal-error offset, numeric HTTP status and a closed allowlist of exception categories/service codes. Unknown categories/codes become `OtherException`/`service-error` or the existing fixed diagnostic mapping. No response text, exception message, user identity, token, claim, term or connection identifier is included.

Offsets are event time minus failure observation time; elapsed past events are negative. Completion means the async continuation was observed by this session, not raw network completion. The most recent error remains available after a later successful renewal, with its timestamp so an older error is not confused with an in-flight attempt. Absent start/completion/error facts mean that event has not happened in the run.

The existing authority condition, local writer fence, cancellation token, swallowed-renewal-error behavior, 5,000 ms renewal scheduling and service-side 15,000 ms lease are unchanged. No retry, timeout, fallback, authentication or public allocation behavior was modified. String diagnostics are built only on failure, with no added logging loop or reflection test helpers.

Owned source: `Assets/_Project/Scripts/Online/Fusion/WordboundFusionSession.cs`. Baseline-only `083206f` copied exact root `05c624b` bytes into this lane and must not be integrated. Integrate only the following diagnostics/report delta. Root owns the next assembled compile and two-case public diagnostic run; these facts do not yet establish why the writer expired.

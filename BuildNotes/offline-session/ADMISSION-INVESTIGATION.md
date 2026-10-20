# Concurrent native admission investigation

STATUS: READ-ONLY FINDINGS — ROOT DIAGNOSTIC RUN REQUIRED

Root's native run `gate_test_editmode_20261020T154938Z.log` reports a local-host `FormatException` from `WordboundFusionSession.Admit` before gameplay starts. The previously verified two-peer baseline predates this run; offline changes are not integrated and cannot explain this failure.

Confirmed source boundaries:

- `StartRunner` already checks that the local Photon UserId equals the coordinator guest before calling RestoreWriter/AdmitLocal.
- Admit verifies the start certificate and admission, then awaits `OnlineConnectionService.Authorize`. The latter validates the signed connection authorization. Following that await, the explicit lease-expired branch and `OnlineSessionGate.Admit` throw InvalidOperationException, not FormatException.
- The deployed source response remains `{ authorization, claims }`. Millisecond timestamps fit the exact integer range, and eight-seat signed roster size is below the verifier's token limit.
- `VerifyConnectionCore` rejects a signed `leaseExpiresAt` greater than local wall clock plus 15,000 ms. The service creates it from server wall clock plus 15,000 ms. `Renew` separately tolerates 5,000 ms clock offset. A server clock ahead of the client by more than response latency can therefore trigger the first check. JWT issued-at validation also has no second-level skew allowance.

The clock mismatch is a plausible boundary defect, not the established cause of this native failure. No validation was relaxed, no credential or response was printed, no source was modified for this investigation, and no compiler/native run was started.

Recommended bounded next diagnostic: capture a fixed Admit stage and static verifier error code, or the exception's method stack without its message/values. A whitelist can distinguish signature, identity, schema, token time and connection-boundary failures. Only after signature validation may a diagnostic expose numeric time deltas; never emit JWTs, secrets, arbitrary response bodies or unfiltered exception messages. Root owns this diagnostic and rerun.

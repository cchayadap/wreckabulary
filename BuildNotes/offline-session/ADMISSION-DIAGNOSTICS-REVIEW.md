# Admission diagnostic and recovery review

STATUS: COMPLETE — READ-ONLY SOURCE REVIEW; NO COMPILATION OR EXTERNAL ACTION

Scope: root's uncommitted diagnostic changes in integration FusionSession, OnlineAdmissionVerifier, the native gameplay logger and SessionLifetimeTests. The active failure cause remains unconfirmed.

Initial review found one failure-path regression: optional diagnostic extraction ran after setting Failure but before invalidating/freezing the run. A throwing virtual Exception.Message accessor could abort that path, leaving the run live while later Fail calls returned early. Root repaired it before this final review: invalidation, gameplay/gate freeze and transport pause now precede guarded diagnostic extraction. Formatting failure produces only `diagnostic-unavailable` with blank origin/facts, then reaches the existing failure notification. The new throwing-Message regression calls the original `Fail(Exception)` through reflection and checks notification, inactive run and false readiness. Its source covers the reported defect; execution belongs to root's gate.

The diagnostic data boundary otherwise looks sound. Stage labels are fixed local constants; codes map exact known messages to fixed strings; unknown messages become unclassified. Facts include only four named keys whose runtime values are Int64. The verifier populates time facts only after validating the signature, issuer/audience and claim shape. Origin uses at most four method metadata names without source paths, argument values or message text. The native logger concatenates only these fields and the existing exception type. No JWT, connection secret, provider key, HTTP body or arbitrary exception data is included.

All admission predicates in the reviewed diff are unchanged. The verifier changes only the construction of the same FormatExceptions. `Fail(Exception)` remains a single method with its original signature, so reflection callers are not made ambiguous by the differently named FailAt helper.

Disposition: no remaining confirmed defect in these diagnostic changes. This is source-review acceptance, not a successful native admission result or confirmation that clock skew caused the earlier failure.

## Current recovery paths

- Party mutations with an unknown HTTP outcome retain their original request/idempotency key and expose Retry. This does not restart a failed Fusion Runner.
- Before launch, party polling can fetch another start certificate after a transient certificate failure. A persistent behind-server clock can reject every newly issued certificate again; fresh credentials do not correct time.
- FreshJoin rotates guest authentication and fetches a new start certificate, admission and one-use Photon ticket for a new Runner. It still uses the same local wall clock. A connection ticket is requested later, close to host admission.
- A failed Session invalidates its run and cannot simply call Start again. The current user-facing recovery is confirmed Return to party, then a new reservation/match. Return is terminal for the old seat, not same-match reconnect. Return itself can be retried if the server outcome is not confirmed; the menu retains context.
- Renewal runs about every five seconds and can tolerate a transient HTTP failure only until the existing signed connection/writer deadline. It cannot extend an expired authorization locally.

No current automatic correction of coordinator/client clock offset exists. The ±5-second check in connection-state renewal is a tolerance check, not a synchronized clock implementation. JWT verification rejects future `iat`; connection verification rejects lease expiry later than local now plus 15 seconds. A behind-server clock can therefore reject a newly issued credential even when renewal's separate tolerance would permit that offset. This is a source-derived possibility, not the measured native failure. Correcting only the admission verifier would leave writer expiry, connection expiry, startup and reconnect deadlines on different time assumptions.

## Proposal if diagnostics confirm a legitimate clock offset

First distinguish future issuance or excess lease-remaining time from expiration caused by slow HTTP/scheduling. A 25-second response can exceed a 15-second ticket/lease even with perfectly synchronized clocks; that outcome must remain a failure. Do not use repeated new credentials or sleeps as proof of time correctness.

The smallest operational recovery is to correct the device clock outside active gameplay, then create a fresh session with new credentials. No device setting was read or changed in this review. The existing terminal-return/new-match path is the safe current UI route after startup failure.

For product support without dependence on device clock accuracy, add a separately reviewed coordinator-time bootstrap with pinned trust, a fresh nonce and bounded round-trip uncertainty, then inject one shared monotonic server-relative clock into party verification, connection service, Session and gameplay. Existing Func<long> clock seams already support composition, but MatchClient currently uses their default wall clocks. A signed time bootstrap should be available before the first match-certificate check; relying on connection-state renewal would create a startup dependency cycle. Freeze and restart calibration when uncertainty or a clock discontinuity makes an exclusive deadline unknowable. This proposal does not authorize widening any validity interval.

A targeted retry of an already verified, retained authorization could reuse its exact connection ID and request only while the service's idempotent authorization remains live. That is a future explicit retry contract, not the current Session behavior. It must not silently mint a different ticket under the same connection ID, resurrect a returned seat, preempt a live writer or relabel a failed initial host as a new room owner.

Required tests for any future clock/retry change: positive/negative three-second offsets; exact token, 15-second lease and 120-second reconnect boundaries; delayed/lost responses; forward/backward clock jumps; nonce replay; stale Runner/connection/writer terms; returned seats; and unchanged rejection of wrong identities, content and signatures. Time adjustment must not bypass any binding check.

## Reviewed source pins

Integration base: `deae592cf4dd10b15223ad91ef30768da3d5296f`; diagnostic changes were uncommitted when captured. SHA-256:

| File | Hash |
| --- | --- |
| `Assets/_Project/Scripts/Online/Fusion/WordboundFusionSession.cs` | `F69B7071DEB87E4DE06E7A80F20BB9F033FDDFB6E7B1FB6BC541589EC86912E3` |
| `Assets/_Project/Scripts/Rules/Wordbound/OnlineAdmissionVerifier.cs` | `4CF96C09856499A42700654C9C78D37F3B9DECA06ADAF242A9FB4317BD937254` |
| `Assets/_Project/Tests/OnlineGameplay/WordboundNativeGameplayTests.cs` | `DA3DA3605755A767A2A827CBC368F6B1053E2E570F471FB1FFECE761869977D0` |
| `BuildNotes/online/FusionCompile/SessionLifetimeTests.cs` | `AE2E6510C53416CD1E071E70F86D92EB5F8DB4342CC339187330DF56DDF789F5` |

Additional read-only recovery checks: `OnlineConnectionService`, `WordboundPartyCoordinator` and `WordboundMatchClient` at that integration state. No runtime, HTTP, device clock or account actions were performed.

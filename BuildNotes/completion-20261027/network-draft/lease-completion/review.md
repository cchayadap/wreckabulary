# Independent writer-lease completion review

STATUS: COMPLETE — independent source review only.

Read-only review of the integrated lease-completion patch and current production dependencies. No live edits, native jobs, service calls or secret reads. Starter draft remains frozen separately.

## Verdict

**No source blocker found for the unchanged native retry.** The correction addresses a real captured-context completion path while keeping expiry and authority fencing. It does not establish that writer-poll starvation is the only cause of the failed full-match startup.

Reviewed patch SHA-256: `38e33a41118d93b2be77ee4089f8938d4715c528ff8e706d82de98a7c1fe3aa2`. Independent `git apply --reverse --check --whitespace=error-all` passes against the integrated source. All four live files are text-identical to the pinned candidates after newline normalization. Raw hashes differ because the candidates use LF and integrated files use CRLF; this is verified, not ignored drift:

| Live file | SHA-256 |
|---|---|
| OnlineWriterLease.cs | `3d41f71407b6ba5917fb857f8f4208fe2d3a648a7ffc2e73bdd75f7ac80a1ba5` |
| OnlineCheckpointService.cs | `61cfb26933844618fa448bf85fd8c0208de4f811c066e6fc0e0112fbd2fe2da5` |
| WordboundFusionSession.cs | `0c1315be07076b71cd432c2c6dfbbfa531e41c3fde177462b73c18bb893de5f2` |
| WordboundWriterLeaseTests.cs | `d18b142f3a5a2cc9704b5ddca0fc2a4a4432f40c5fa6e554b38a6c81e214ab29` |

## Confirmed safety properties

- **Completion thread boundary:** only immutable writer response validation/merge and lease diagnostic bookkeeping continue after `ConfigureAwait(false)`. The cell has no Unity/Fusion object, runner, gameplay mutation, timer or recurring task. Session `Poll` still explicitly schedules one renewal; completion cannot keep a stalled/abandoned session alive indefinitely by scheduling another request.
- **Atomic liveness:** `IsLiveFor` reads owner, current expiry, cancellation and retirement under the same lock as renewal merge. It does not fetch an old DTO then compare that obsolete expiry after another thread accepts a valid renewal. `Current` returns an immutable DTO. Run identity remains checked by the main-thread session before the cell check.
- **Retirement/cancellation:** `InvalidateRun` retires the old cell before dropping active-run identity and canceling the run token. Late old-cell completions cannot mutate a replacement cell. `Merge` checks current liveness under lock, then the full authority fence. A reply arriving at or after the locally accepted expiry fails closed; it cannot revive the expired writer with a future timestamp.
- **Authority identity:** player, claim, term, epoch and base-head must match both current/request and request/response; response expiry cannot regress its request. The real service parser independently retains `SameWriter` validation. The new copy constructor can only preserve/advance expiry and preserve/open admission.
- **Open/renew ordering:** the merge uses maximum valid same-writer expiry and logical-OR admission state. An older open reply cannot shorten a newer renewal, and an older renewal cannot close admission. Existing restore/open checks remain on the current run; an open response still has to explicitly report admission open.
- **Dispatch boundary:** if retirement, cancellation or expiry occurs between `RenewalDue` and `Renew`, the method returns a completed false result without invoking transport. Duplicate explicit requests are rejected while one is in flight. Exceptions from renewal/merge are retained as diagnostic data and return false; Poll still fails when the accepted lease expires.
- **Production dependencies:** both production `WordboundMatchClient` and native gameplay fixture use the session's default `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds`, which is engine-independent. Production `WordboundHttpTransport` uses request-local messages/buffers and HttpClient, with existing `ConfigureAwait(false)` on HTTP/read awaits. Coordinator parsing and service renewal already run without the caller context. The new continuation does not introduce UnityWebRequest or off-thread SDK access. Future injected clocks must remain engine-free/thread-safe as the draft README states.
- **Failure evidence:** prior error history remains recorded; no raw exception message or credential fields were added to the session's allowlisted failure facts. No lease TTL, protocol, timeout, score, gameplay, service or acceptance threshold changed.

## Tests assessed

The 18 prepared cases meaningfully exercise this code rather than only mirror fields. The blocked-context case installs a `SynchronizationContext` whose Post deliberately never runs work, sends through the real coordinator/checkpoint parser using a pending in-memory response, and synchronously waits for the lease task. `RunContinuationsAsynchronously` plus zero Posts proves the tested completion does not rely on pumping the caller context. The test restores the original context in `finally`.

Separate cases cover already-dispatched cancellation/retirement, prior-expiry boundary rejection, due-to-dispatch invalidation without transport, both open/renew response orders, all five identity fields plus regressed expiry, liveness at the old deadline after valid renewal, wrong owner, and coalesced explicit dispatch. Negative tests retain the exact old writer object, making hidden mutation detectable. They do not emulate real Unity workload, HTTP delay or multiple machines.

## Required outcome after source gates

Root should retain the failed `165734` result, execute the focused writer/checkpoint suites, then retry the original full-public FFA gate with the same startup and gameplay assertions. Inspect writer-poll timing, real durable startup, scored outcome and cleanup separately. Repeat recovery cases afterward; passing these 18 cases alone does not close full matches, host migration, latency/loss, eight-peer performance or the original-house networking requirement.

STATUS: COMPLETE — independent source review only; root owns compiler/native outcomes.


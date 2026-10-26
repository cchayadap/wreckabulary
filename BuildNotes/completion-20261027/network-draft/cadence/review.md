# Independent checkpoint cadence review

STATUS: SOURCE REVIEW COMPLETE, revision 2 cleared for root's compile/native verification. Read-only review by story_status. Original patch `771d76f703d88b7abc0172711776bb51eb6621d17830032127667bbe663de596`; final patch `e1ccdceb7adc54e2b44f2c50c373c2f36ac84dfe87bc9e646f1ac0042d408d5e`. No live source changes, native jobs or network calls.

Confirmed delta is success-only scheduling from attempt start; failure/false completion retains completion backoff. The current-Runner completion fence remains. No blocking runtime source finding. Native/gameplay acceptance remains pending.

## Confirmed source findings

- Runtime delta has no blocking source finding. `Poll` still requires an initialized, nonstopping, fault-free, ready server/host and reconciled frame. There is at most one controller publication in flight; the session independently retains its current-run/writer/migration/publication guards.
- The successful boolean has the required meaning: `WordboundFusionSession.PublishCheckpointCore` returns true only after `CommitPublication`, which verifies matching manifest/upload, writer term and `AcknowledgeDurable`. Its recovered-HEAD path also commits before true. `WordboundLocalSession` similarly acknowledges its checkpoint before true. Failed/declined calls remain completion-backoff; success cannot enqueue a separate loop.
- Completion can change `publishing` and `nextCheckpoint` only when its captured Runner is still the controller Runner. No new off-thread SDK work, protocol/cadence/window/lifetime changes or authority validation relaxation is present.
- The existing clock is UTC milliseconds; under normally progressing time, successful starts remain separated by at least the configured interval, or completion duration if longer. False/error calls retain completion plus interval. Faster success increases request frequency compared with the previous completion-plus-interval rule; README correctly requires measured request counts/rate and does not promise unchanged service cost.
- Both candidate hashes and the existing baseline hash match the manifest. Strict `git apply --check --whitespace=error-all` passed against the live preintegration source. This reviewer did not run compilation or tests.

## Requested test correction

`Fixture` leaves `initialized` false. The final `Poll()` in `ThrowingPublicationStillFaultsAndStopsWithNoAutomaticRetry` and `StopDuringPublicationPreventsAnyLaterPollDispatch` therefore exits at `!initialized` before proving the stopping/fault guard. This does not show a runtime regression, but it weakens the claimed lifecycle proof. Requested: set the owned controller initialized immediately before those final explicit Polls and assert the relevant stopping/fault state. Do not globally enable automatic Update in the replacement-runner fixture, because its fake Session.Runner is null. Asset_intake is preparing a bounded test revision; final hash review pending.

Resolved in revision 2: both cases set and assert initialized immediately before the explicit Poll, assert stopping and their expected Fault state, and require the fake session Poll counter to remain zero. This now proves suppression happens before session polling. The runtime candidate is unchanged (`6258087f1fb8465b711b86aa6504bdb28fca6fd6ee383f3f14b85356570c88ac`); final test SHA-256 is `2e8c9f8f4f58db3af14cd598a81e8c1bd5f5cac418a9786a78d0423d6fcd6fc3`. Final patch and test hashes verified; strict apply check passed again. The original revision is retained by its author. No further source blocker identified.

## Acceptance limits

The six prepared reflection-seam tests exercise the real publication method and owned deferred session; they do not run a real server, prove complete FFA play or measure request/ACK latency. The replacement test proves stale completion does not overwrite an owned replacement reference. Root must run compilation, focused tests and the same exact public FFA acceptance without changing gameplay/writer/durability/time limits. Preserve the failed175059 evidence and compare observed rates/request totals, not just a later PASS result.

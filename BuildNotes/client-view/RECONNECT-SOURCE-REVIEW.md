# Reconnect source review

STATUS: IN PROGRESS — read-only production review; native verification NOT RUN.

Scope: online lane checkpoint `e3194fe` and its current reconnect fixtures. Review covers fresh Runner ownership, callback identity, the first-loss deadline, explicit leave cancellation, migration serialization, and the user-visible recovery notice. Production changes remain owned by the online lane. Integration and native jobs remain owned by root.

Preserved pre-existing online-lane changes: `ModeDefinition.cs`, ongoing native gameplay/reconnect tests, and untracked `BuildNotes/online` evidence. This reviewer edits only this report in the puzzle-race lane.

## Findings

Two confirmed source findings sent to the online owner for correction:

1. **Late migration token is lost during replacement preflight.** In `WordboundFusionRecovery.cs`, `RetireRecoveryRunner` clears `recoverySource` and destroys the original runner before the coordinator state / fresh credentials await. `OwnsRecoveryCallback` then rejects `OnHostMigration(originalRunner, token)` during that interval. A token arriving after the fixed initial 500 ms wait cannot supersede the ordinary client retry, despite the stated serialization contract. Keep a narrowly authorized migration-token source for this recovery generation while continuing to reject stale ordinary callbacks, and exercise this ordering through an actual callback regression.
2. **Unused host migration token is lost on transient preflight failure.** `RecoverLoop` removes `pendingMigration` before `StartRunner`, then excludes all transient catches when that token elects a host. A credential or checkpoint-Head 503/IO failure before `StartGame` therefore ends recovery even though the token was never consumed by Fusion. Distinguish preflight from actual token consumption and retain the token for a bounded preflight retry. A consumed host token may remain terminal as intended.

The new fixtures cover ordinary client timeout-shutdown/rejoin, fresh admissions, unchanged room/seat/epoch, exact restored bodies and a command ACK, stale original shutdown rejection, and explicit stop / seat-expiry / authentication rejection. The pure window tests cover first-loss bounds, monotonic narrowing, backoff and terminal cancellation. These fixtures do not yet exercise the two migration/preflight orderings above. Review remains in progress; no approval of the partial implementation is implied.

The owner accepted both findings and began the source corrections. During that correction, a further bounded issue was identified: an unconditional wait for `Runner.IsRunning` to become false at the top of every attempt also traps internally failed replacement attempts. For example, a new client connects, its admission/Head request returns 503, and `Fail` freezes the RunScope while leaving its transport running. The next loop waits for a shutdown that was never requested. Waiting for the original SDK disconnect/migration callback must be distinguished from retiring an internally failed replacement.

## Stage 2 proposal review

The proposal preserves compatibility with an optional per-term lease profile, legacy 15-second defaults, exactly seven public writer fields, strict idempotency, and no Photon Host promotion from a coordinator lease. These are coherent design constraints, not verified implementation.

One concrete integration requirement was sent to root and the online owner: same-Runner writer recovery must explicitly reset/rebind gameplay composition. Current `WordboundGameplayController` runs `ChangeRunner` only when the Runner reference changes, and `GameplayStarted` calls `BeginHost`, which returns immediately when `hostReady` is already true. A replacement RunScope or writer term on the same Runner does not itself rebuild physics, clear host readiness, or repeat peer admission. Old publication/admission work must be fenced, canonical state restored, and composition/readiness re-established before input resumes. Stage 2 source changes remain outside this review assignment until authorized and provided.

## Verification limits

No compiler, Unity, network, player, or native test was run for this review. Fixture inspection does not establish runtime recovery, host migration, or presented UI behavior.

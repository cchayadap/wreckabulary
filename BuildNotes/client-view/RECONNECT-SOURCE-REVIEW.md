# Reconnect source review

STATUS: PASS — bounded stage 1 source review; native verification NOT RUN.

Scope: online lane checkpoint `e3194fe` and its current reconnect fixtures. Review covers fresh Runner ownership, callback identity, the first-loss deadline, explicit leave cancellation, migration serialization, and the user-visible recovery notice. Production changes remain owned by the online lane. Integration and native jobs remain owned by root.

Preserved pre-existing online-lane changes: `ModeDefinition.cs`, ongoing native gameplay/reconnect tests, and untracked `BuildNotes/online` evidence. This reviewer edits only this report in the puzzle-race lane.

## Findings

Two confirmed source findings sent to the online owner for correction:

1. **Late migration token is lost during replacement preflight.** In `WordboundFusionRecovery.cs`, `RetireRecoveryRunner` clears `recoverySource` and destroys the original runner before the coordinator state / fresh credentials await. `OwnsRecoveryCallback` then rejects `OnHostMigration(originalRunner, token)` during that interval. A token arriving after the fixed initial 500 ms wait cannot supersede the ordinary client retry, despite the stated serialization contract. Keep a narrowly authorized migration-token source for this recovery generation while continuing to reject stale ordinary callbacks, and exercise this ordering through an actual callback regression.
2. **Unused host migration token is lost on transient preflight failure.** `RecoverLoop` removes `pendingMigration` before `StartRunner`, then excludes all transient catches when that token elects a host. A credential or checkpoint-Head 503/IO failure before `StartGame` therefore ends recovery even though the token was never consumed by Fusion. Distinguish preflight from actual token consumption and retain the token for a bounded preflight retry. A consumed host token may remain terminal as intended.

The new fixtures cover ordinary client timeout-shutdown/rejoin, fresh admissions, unchanged room/seat/epoch, exact restored bodies and a command ACK, stale original shutdown rejection, and explicit stop / seat-expiry / authentication rejection. The pure window tests cover first-loss bounds, monotonic narrowing, backoff and terminal cancellation. These fixtures do not yet exercise the two migration/preflight orderings above. Review remains in progress; no approval of the partial implementation is implied.

The owner accepted both findings and began the source corrections. During that correction, a further bounded issue was identified: an unconditional wait for `Runner.IsRunning` to become false at the top of every attempt also traps internally failed replacement attempts. For example, a new client connects, its admission/Head request returns 503, and `Fail` freezes the RunScope while leaving its transport running. The next loop waits for a shutdown that was never requested. Waiting for the original SDK disconnect/migration callback must be distinguished from retiring an internally failed replacement.

The revised source retains stopped token candidates, marks token consumption immediately before `StartGame`, and limits the live-transport wait to SDK disconnect/election handling. Those three triggers are closed at source level. The revised fourth native fixture delays host departure for one second after an early disconnect callback, then injects an unused-token credential 503; its initial Runner/credential assertions would fail against `e3194fe` after the original 500 ms retirement. The explicit-stop fixture now also injects one checkpoint-Head 503 after the replacement actually starts, exercising the internally failed transport path.

A final generation-order finding was corrected: the retained migration-candidate set previously had no ordering. After a newer replacement runner's election token was accepted, a first late token from an older candidate could overwrite `pendingMigration` and cancel the newer attempt. The corrected source stamps every Runner before callback registration and accepts only election generations at least as new as the last accepted generation. Rejection occurs before token mutation, cancellation or freeze. Late tokens remain eligible during ordinary retries; completed recovery clears the generation watermark.

## Bounded verdict and fixture coverage

PASS for stage 1 source through `aeadba5` (ordered implementation commits: `e3194fe`, `f549505`, `aeadba5`). All four findings above are closed at source level. The nine pure window cases include the ordered-election policy. The four native fixtures remain unrun and cover the client rejoin/terminal paths plus delayed graceful-host election and unused-token preflight failure. The post-connect admission fault is injected through the real HTTP wrapper only after the replacement Runner is running. It is not a manually queued replacement action.

The delayed-election test would fail against `e3194fe` because the original Runner is retired after 500 ms, before the one-second pre-departure assertions. With only that lifetime defect repaired, the injected first host-token credential 503 would still expose the original premature token loss. The admission503 path covers the intermediate unconditional-wait regression. The ordered-election case tests the policy directly; source inspection additionally verifies its call occurs before the real callback mutates pending state. No mutation run was performed.

Fresh runners are created only after retirement, identity/room/generation remain fixed, a retry cannot create a new room, the first-loss deadline is only narrowed by coordinator state, and cancellation propagates through the active attempt/RunScope into credential and restoration requests. Explicit stop and terminal failures disable recovery before waiting for its loop to finish. The narrow MatchClient notice opens the existing HUD and successful gameplay clears it; no separate navigation or menu tree was introduced.

## Stage 2 proposal review

The proposal preserves compatibility with an optional per-term lease profile, legacy 15-second defaults, exactly seven public writer fields, strict idempotency, and no Photon Host promotion from a coordinator lease. These are coherent design constraints, not verified implementation.

One concrete integration requirement was sent to root and the online owner: same-Runner writer recovery must explicitly reset/rebind gameplay composition. Current `WordboundGameplayController` runs `ChangeRunner` only when the Runner reference changes, and `GameplayStarted` calls `BeginHost`, which returns immediately when `hostReady` is already true. A replacement RunScope or writer term on the same Runner does not itself rebuild physics, clear host readiness, or repeat peer admission. Old publication/admission work must be fenced, canonical state restored, and composition/readiness re-established before input resumes. Stage 2 source changes remain outside this review assignment until authorized and provided.

## Verification limits

No compiler, Unity, network, player, or native test was run for this review. Fixture inspection does not establish runtime recovery, host migration, or presented UI behavior.

# Epilogue recovery fixture diagnosis

STATUS: SOURCE FIX COMPLETE — NATIVE VERIFICATION PENDING

Scope: inspect the native failure in `WordboundStoryEpilogueTests.CodecAndCommittedEpochRecoveryRetainConfirmedIdentityWithoutChangingBytes`; change only that test file if the failure is fixture-only. Preserve strict snapshot validation and all production invariants. No Unity, build, or native test execution in this lane.

Reported evidence: root's 144-case native run at 062041 passed 143 cases and failed this case at line 287 with `match-snapshot.json inventory action permission disagrees with authoritative player state`.

Next action: compare fixture construction with canonical result finalization and inventory action-state validation before deciding the repair scope.

## Confirmed diagnosis

The test creates an `AuthoritativeMatch`, captures its initial state, separately advances a `MatchProgress` to timeout, then combines expired progress with the initial economy. The initial inventories still have `CanAct == true`. This is contradictory: `MatchStateValidation.cs:330–331` requires no result whenever an inventory claims action permission. The reported failure occurs at codec import, before `RestoreCommitted` is invoked.

Production source already reconciles these states. `AuthoritativeMatch.Advance` advances progress then calls `Synchronize` (`AuthoritativeMatch.cs:235–236`); synchronization ends with `SetPermissions` (line 846), whose predicate includes `progress.Result == null` (lines 1036–1037). `RestoreCommitted` validates its input before mutation (line 306) and reapplies permissions after confirmation (line 319). This evidence supports a fixture-only repair; it is not a new native execution result.

Repair decision: restore the captured economy into a separate fixture economy, disable its inventories for the ended match, then construct the valid timeout snapshot. Preserve the original contradictory construction as an explicit negative import assertion; retain all identity, epoch, confirmation, and byte-equality assertions and add post-recovery permission checks. No production source or validator changes.

## Repair and verification

Changed only `Assets/_Project/Scripts/Online/Presentation/Tests/WordboundStoryEpilogueTests.cs`. The test now reconstructs its ended economy through the public roster/restore API and disables action permissions before serializing the provisional result. It additionally verifies that the original contradictory snapshot fails import for the permission invariant, yields no snapshot, and that correcting the fixture leaves the initial snapshot's bytes unchanged. Both committed-epoch recoveries must retain disabled permissions. Existing epilogue identity, title/cue, same-tick confirmation, epoch recovery, and confirmed codec byte equality assertions remain.

The existing XML receipt `TestResults/editmode_20261026T062041Z.xml` was read directly and confirms this exact failure at the former line 287. No result of the repaired test is claimed. Scoped `git diff --check` passed. This changes an existing test only, so case counts are unchanged.

Frozen test source SHA-256: `0C7E0C81487EB2B02AA6075615F969F64444C19F3DE8F66102DE6B3CD83FE61E`.

Next action (root-owned): include this fixture-only change in the combined R0 and native rerun. No Unity process, test, build, metadata generation, commit, or production source modification was performed in this lane.

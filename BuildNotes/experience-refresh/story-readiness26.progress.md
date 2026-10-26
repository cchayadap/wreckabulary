# Story presentation readiness source review

STATUS: COMPLETE — read-only review of root's IsPresentationReady condition against bootstrap, recovery, runner replacement and confirmed results. Only this report is writable; no runtime or test jobs.

Condition under review: initialized, not stopping/faulted, local scene ready, session GameplayReady, running controller runner, hostReady when server, existing frame and not frame.Progress.Migrating. Intended only for optional story epilogue visibility.

## Conclusion

No permanent confirmed-result-hidden path was found in this readiness condition. It is suitable as the optional epilogue's live-presentation gate, provided the epilogue selector separately requires the intended confirmed outcome and matching course/actor. The property is not a result-confirmation test or gameplay authorization test.

The inspected implementation is root's uncommitted `WordboundGameplayController.cs:71-73` on top of `40e31200ff60e7459ca2bb7241adb5d0a3b51782`; surrounding source was read live while other lanes continued. No compile/native/result run was executed by this reviewer, and the in-progress epilogue plan integration is outside this narrow lifecycle conclusion.

## Lifecycle evidence

| Stage | Expected readiness and source basis |
| --- | --- |
| Initial boot | False until local scene is prepared, session completes its initial durable checkpoint/ACK barrier, a frame exists and the host has reconciled its bodies. Session sets GameplayReady at FusionSession:457 and raises GameplayStarted; controller BeginHost requires the committed session and sets hostReady after successful reconciliation (GameplayController:211-238). Client starts only after admission, local readiness, Begin and a committed mark (FusionSession:824-827). |
| Confirmed result | May remain true. AuthoritativeMatch.IsPaused includes any Result, but IsPresentationReady intentionally does not use IsPaused or Result == null. Acknowledge confirms the result (AuthoritativeMatch:288-293), MatchesTickPresenceAndResult compares the Confirmed flag (:251-269), and controller Poll continues reconciliation/publication without requiring simulation advance (:151-156). No result branch clears hostReady, localReady or session GameplayReady. |
| Client confirmation | The result-bearing snapshot can first carry Confirmed=false because confirmation occurs after its durable acknowledgement. The host continues its checkpoint cadence while paused, so a later publication carries Confirmed=true. ReceiveReplica updates the controller frame; this is a normal propagation interval, not a permanently hidden result. LocalSession similarly emits its pre-acknowledgement serialized snapshot, then controller Poll reconciles the confirmed authority state. |
| Transport interruption | False as soon as FreezeRecovery runs: it clears GameplayReady, freezes the gate, pauses authority, invalidates the active run and clears pending admission/committed transport state (WordboundFusionRecovery:75-85). This occurs before retiring a runner or awaiting its replacement. Failure and Stop also clear GameplayReady; Fault/stopping provide additional controller guards. |
| Runner replacement | Controller ChangeRunner clears localReady, hostReady and frame (GameplayController:162-168). A new session cannot restore presentation merely by having a running runner: the local scene and checkpoint startup conditions must become ready again. GameplayStarted and ReceiveReplica synchronize the controller's runner with session.Runner before using the new state. |
| Successful recovery after a result | RestoreCommitted clears Progress.Migrating and confirms the restored result (AuthoritativeMatch:303-320; MatchProgress:295-296). Reconnect checks seat/deadline/returned status rather than rejecting Result != null (MatchProgress:272-278). New host BeginHost can reconcile a finished match because it does not require simulation to be unpaused. Consequently the property can become true again with the confirmed result intact. Failed/expired recovery appropriately leaves it false. |

## HUD refresh and limitations

`WordboundStoryView.Epilogue` queries the readiness property dynamically. `WordboundMatchHudMenu.cs:131-138` recomputes connection status and menuEpilogue on each GUI Layout; null during a transport gap is not permanently cached. The Journal tab is available when entries or an epilogue exist, and its content remains enabled when journalOpen even though simulation is paused. The epilogue is an optional journal view, so this source check does not promise that a result automatically opens that tab.

The property currently does not explicitly compare `runner` with `session.Runner`. Existing recovery ordering and the synchronous GameplayStarted/ReceiveReplica paths already fence the observed replacement paths; no concrete stale-ready race was found. Adding `ReferenceEquals(runner, session.Runner)` would state the intended invariant directly if root wants a small defensive guard, but it is not required to fix a demonstrated result-hiding bug. Likewise transport detection is not instantaneous: before Fusion reports a loss/lease failure, readiness may still reflect the last known live state. That is expected for an optional display gate.

Retain result authority in the epilogue selector: require Confirmed and the selected outcome, exact course binding, intended mode and original actor/team eligibility. Do not use this boolean to admit commands, advance physics, settle a result or weaken the existing bootstrap/recovery checks.

Focused root verification: confirmed local result with IsSimulationPaused=true and IsPresentationReady=true; same for host and replica after confirmation publication; interrupt transport while the journal is open, observe epilogue disappearance, then recover the same finished match and observe its return; ensure runner replacement cannot show a previous-runner frame. No tests were added or run by this report-only lane.

STATUS: COMPLETE — source lifecycle review. No blocking finding; all runtime behavior remains to be verified by root.

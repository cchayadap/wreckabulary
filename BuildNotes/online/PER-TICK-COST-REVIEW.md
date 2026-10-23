# Per-tick main-thread cost review

STATUS: INDEPENDENT SOURCE PASS for `4c038d45`; integrated by root as `041ce4a8`. Root started R0 then the35-case core filter. No compiler, test, Unity, player, benchmark or deployment job ran in this lane. Existing dirty ModeDefinition and historical logs remain untouched.

## Measured baseline and its limits

Root evidence is `BuildNotes/online/native-clock-atomic-team-20261023.json`, `Logs/gate_test_editmode_20261023T181114Z.log` and `TestResults/editmode_20261023T181114Z.xml`; source at launch `50520a4c`, code `305ff093`. Both native cases failed. Public TeamSolo rejected a transfer after 14766ms against the unchanged 10000ms limit, with matching mark/count/length/wire hash and no duplicate chunk. This is elapsed time observed by the receiving continuation, not a measured network RTT or proof of the transport cause.

The Team profiler window is 61.169s, main thread, all in-process instances, per-marker invocation, capacity32768 with no observed wrap:

| Marker | Calls | Inclusive CPU ms | Maximum sample ms |
| --- | ---: | ---: | ---: |
| Reconcile | 361 | 9084.061 | 1509.061 |
| Advance | 360 | 7187.259 | 128.358 |
| Capture | 1337 | 4507.558 | 38.757 |
| BeforePhysics | 360 | 1614.610 | 31.528 |
| BuildWorld | 2 | 996.795 | 593.872 |
| PhysicsApply | 361 | 591.357 | 18.870 |
| Presentation | 361 | 297.577 | 26.680 |

Scopes overlap and cannot be added. The absent Validate samples mean unavailable under the approved atomic reconciliation boundaries, not zero validation cost. PhysicsApply now covers physical application; its old pre-atomic scope included validation and is not directly comparable. Capture measures the Controller wrapper only, excluding rollback captures inside Advance.

The host recorded a maximum60-forward-tick batch of4746.743ms, maximum Unity Update gap5368.842ms, and no batch-pairing error. Client forward batch maximum was17.049ms. The public test uses two runners in one Editor process. These observations establish substantial host main-thread work and scheduling gaps; they do not establish an internet/network fault, achievable player fps or a promised speedup from this candidate.

## Confirmed source costs

`WordboundGameplayController.Poll` and `PrepareHostPhysics` each call CaptureFrame before SameFrame. SameFrame reads only epoch, progress tick, and sorted player ID/Connected/Returned values. The discarded snapshot also captures the economy and journal, freezes/sorts every body, resource and mechanism, and creates their collections. `MatchSnapshot.Freeze` sorts each section, including constructed resource/mechanism string keys. This work is unnecessary when the tick and presence are unchanged.

Initial BeginHost, post-Advance reconciliation, presence retry, rollback, durable publication and untrusted import have different needs; their full captures and validation remain. An Advance rollback snapshot is required to restore all mutations after a failed host query, so removing it is not proposed. `WordboundFusionAuthority.Presence` also captures the whole world, but its only current caller is migration restoration, not the ordinary per-tick path; it is not included.

Within Advance, Synchronize invokes RetireInactiveResources. That method scans all bodies independently for every inactive resource. A private per-call tile index could remove that repeated scan while retaining resource iteration/removal/error order, but it is a separate candidate after measurement. EnsureCapacity also copies the economy merely to inspect entity counts. Synchronize sorts resources and repeatedly queries source/body availability. Their exact individual CPU contributions are not measured by the present markers; no combined optimization is proposed here.

The Economy constructor itself only assigns catalogue/rules references. Restore performs live replacement validation and builds a replacement economy; caching mutable catalogue/rules validity is not justified. Full initial and fresh mutable atomic reconciliation checks remain intact.

## Approved implementation

Alignment-only commit `800139a2` copies AuthoritativeMatch from root `c3e53b86` and MUST NOT be integrated. Controller and existing authority tests already matched root exactly. Only the additive method, two caller substitutions, removal of unused SameFrame, tests and this report belong to the deliverable.

`AuthoritativeMatch.MatchesTickAndPresence` rejects an in-progress tick using the same guard/message as Capture, then checks the retained snapshot binding, epoch, tick, seat count, sorted seat identities, Connected and Returned. It reads each current player through existing defensive-copy `MatchProgress.Player`; there are at most8 small player copies and no full-world snapshot, mutable reference exposure or cross-call cache. It is not described as allocation-free or as a snapshot validation API.

Poll and PrepareHostPhysics now capture/reconcile only after that comparison returns false. All guards, callback ordering, stop/reentry fences, protocol, authentication, deadlines, writer lease, checkpoint schedule and physics/rules behavior are unchanged. Every full validation at reconciliation/publication/import remains in place. No snapshot is reused across simulation changes.

For supported previously reconciled frames, this preserves the old comparison exactly. Disconnect, Reconnect and Return alter its presence fields; Advance changes tick; committed restore changes epoch. A failed tick rolls back to the preceding fields. Pause, acknowledgement, sequence, wall-clock-only changes and an undone disconnect/reconnect were already outside SameFrame's comparison and remain so. Defensive binding/cardinality checks additionally reject malformed foreign frames that cannot be produced by the Controller's successful reconciliation path.

## Regression contract and required verification

The authority fixture now has35 source-counted cases (28 existing plus7 new). Five mode cases independently run the original captured-frame comparator through advance, disconnect, repeated disconnect, reconnect, return and rejected reconnect, check repeated queries and retained-frame immutability. One case covers publication/acknowledgement/pause/no advance/committed next epoch. One checks foreign binding, short/foreign roster and explicitly proves equal tick/presence does not admit an incomplete body ledger through full validation. Existing rollback and in-tick host callback tests gain assertions against the new method; no original assertion was removed.

Root must run, sequentially:

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs core 'FullyQualifiedName~WordboundAuthorityTests' --json
$env:WORDBOUND_LOCAL_GAMEPLAY = '1'
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.OfflineGameplayTests.WordboundNativeLocalGameplayTests' --cold --json --timeout 3600
```

Expected counts:35 focused core and8 existing native local cases, including FrameChanged→Stop after a real canonical Disconnect. Native public comparison requires the same exact TeamSolo workload, actual content, environment flags and opt-in native profiler/tick observer as181114Z; root owns those established invocation settings. Preserve the10000ms transfer deadline and every behavioral assertion. Compare Capture calls per advanced tick, inclusive Reconcile/Advance, maximum60-tick batch wall time, and actual public admission/gameplay outcome. A lower Capture count alone is not acceptance. Retain both raw runs and machine/workload limits; never derive zero cost from an unavailable marker or claim60Hz from this source change.

Independent recover_assets source review confirmed ordinal-ID order in Binding.Seats and ProgressSnapshot.Players, completed-tick rejection, preserved captures/validation/atomic fences and the seven new cases plus strengthened rollback/in-tick assertions. Root separately reviewed the same contract before integration. Neither source review is a compile/native/performance pass. Only whitespace/diff inspection ran in this lane; root's compiler/core results and native comparative performance are still pending at this checkpoint.

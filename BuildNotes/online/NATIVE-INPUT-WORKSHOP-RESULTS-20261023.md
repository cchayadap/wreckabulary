# Native input and workshop result review

STATUS: Initial native batch reviewed: 67/69 passed, two confirmed failures, zero skipped. Root subsequently assigned this lane the bounded four-file correction; its independent source review passes, but corrected core/native execution is NOT RUN. No compiler, Unity process or service request started here.

The completed public native five-case group is assessed separately below; its failure does not change the confirmed input/workshop results.

## Exact result

Root checkout evidence: `.harness/delivery/native-input-workshop-sources-{status.json,stdout.log,stderr.log}`, `TestResults/editmode_20261023T133902Z.xml`, and `Logs/gate_test_editmode_20261023T133902Z.log`. Wrapper 41360 ran 13:39:02–13:41:45 UTC on 2026-10-23; gate elapsed 162.6 seconds, exit 1. The expected 69 cases were discovered: input 24/24 passed, source binding 23/25 passed, existing workshop/recovery 20/20 passed. No crash-after-quit or skipped result was reported.

| Failure | Confirmed cause | Bounded correction |
| --- | --- | --- |
| `DeferredSourceChoiceIsCancelledBeforeItCanReplaceTheDraft("gameplay-block")`, source test line 192 | The test requested a fresh choice synchronously after disposing a gameplay lease. Production intentionally retains `quietUntil = Time.frameCount + 1`; the blocked choice was correctly discarded. | Replace only this parameter case with a Unity iterator. Preserve immediate post-lease suppression and cancelled queued-choice assertions, request real player-loop updates, require `Time.frameCount > quietThrough` before the fresh choice, then verify return to the same assigned history. Restore the fixture's captured quiet scalar on teardown without touching leases or controls. |
| `SameIdFlagshipHasSeparateSavesAndRetainsItsHistoryModeAndRedo`, source test line 272 | Real persistence defect: recovery schema 1 stores an entire valid course as an escaped JSON string. `WorkshopSnapshot.Recover` used the generic 24,578-character per-token limit, so a saved flagship draft could not reopen even though total file size was valid. | Permit a bounded large string only for recovery's immediate root `course` property. Decode escaped property names consistently. Retain the six-MiB recovery envelope, depth/token limits, ordinary metadata string cap and the nested course's original two-MiB/strict validation. No schema or saved-file migration. |

## Source checkpoint

Owned edits: `Scripts/Rules/Wordbound/CourseJson.cs`, `Scripts/Rules/Wordbound/WorkshopDocument.cs`, `Tests/Rules/WorkshopTests.cs`, and `Scripts/Online/Client/Tests/Input/WordboundWorkshopSourceBindingTests.cs`, all beneath `Assets/_Project/`. No product input policy, generic default string cap, storage replacement/backup policy or multiplayer path changed. The redundant unsupported `NonParallelizable` attribute removal matches root's earlier CS0246 repair, already absent during the 69-case run.

Fifteen new pure regressions cover large valid course recovery, an escaped root property, oversized metadata/nested/array/wrong-property values, inner document size/string/depth/schema/duplicate/escape rejection, and outer byte/depth/token bounds. The existing same-ID flagship test continues to check separate files, retained history/mode/Redo, source bytes/profiles and storage recovery. Source-binding count remains 25.

Independent source review by `/root/recover_input`: PASS for the exact four-file correction. It checked allowance consumption at the immediate root value, ordinary defaults, inner import, actual frame progression, immediate suppression, stale-choice discard, teardown isolation, and installed Editor/TestRunner references. Compilation/native execution remain pending; no acceptance is inferred from this source verdict.

## Root follow-up gates

After integration and when the heavy slot is free, run the changed pure boundary tests only:

```powershell
node Tools/kit/gate/game_gate.mjs core --filter 'FullyQualifiedName~Wreckabulary.Rules.Tests.WorkshopTests|FullyQualifiedName~Wreckabulary.Tests.CourseJsonTests' --json
```

Then rerun the affected source-binding fixture, expecting exactly 25 passed and zero failed/skipped:

```powershell
node Tools/kit/gate/game_gate.mjs test editmode --cold --assemblies Wreckabulary.Online.Client.Input.Tests --filter Wreckabulary.Online.Tests.WordboundWorkshopSourceBindingTests --timeout 900 --json
```

Root decides the assembled R0/native sequencing. Do not rerun unfiltered core or treat the earlier 24 input and 20 existing workshop passes as failed. If source integration changes those paths, repeat only the affected scope. Keep the original failed XML/logs as evidence.

## Public two-client batch: failed before gameplay

Read-only follow-up evidence: root `.harness/delivery/native-public-two-client-{status.json,stdout.log}`, `TestResults/editmode_20261023T134237Z.xml`, and `Logs/gate_test_editmode_20261023T134237Z.log`. Wrapper 33764 ran 13:42:37–13:48:59 UTC; gate elapsed 381.6 seconds, zero passed, five failed, zero skipped. Exact five-method/category/assembly filters and both enable flags match the reviewed public execution plan.

All five cases reached an exact public whole-party certificate, then the first Runner failed Fusion `SendJoin` with `OperationTimeout` / `Join Confirmation timeout`. They did not reach the gameplay-observed marker. This provides additional public HTTP allocation evidence only; it proves no working public native mode. `/root/recover_input` owns the SDK/fixture-lifetime comparison investigation. No timeout increase, log suppression or private-room substitution is proposed.

A separate teardown defect is directly evidenced: log lines 629 and 690 onward report `Destroy may not be called from edit mode! Use DestroyImmediate instead.` `WordboundNativeGameplayTests.Cleanup` line 523 always calls deferred `Destroy`, while the subsequent frame yield already depends on `Application.isPlaying`. The test then reports proof-owned objects remaining. Proposed narrow repair, sent to root for ownership assignment: use immediate destruction only for those same prefix-matched, valid-scene test objects when already outside Play Mode; retain the ownership filter and final leak assertion. Production Runner destruction also appears in the failure log and must be assessed with the failed start lifetime, not hidden by changing fixture expectations. No cleanup source was changed in this review.

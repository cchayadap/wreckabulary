# Creator recovery focus fixture correction

STATUS: COMPLETE — no blocking source finding in the bounded test-only correction. This is a controlled focus-handler test, not proof of physical window focus or recovery-button input. No live edits or native jobs by this reviewer.

The actual `TestResults/editmode_20261026T201133Z.xml` is **3 total, 2 passed, 1 failed, 0 skipped**, duration 48.923574 seconds. `ExistingRecoveryRequiresChoiceAndBackPreservesItsBytes` failed at the existing 30-second `CheckReturned` deadline; the exact edited Single playtest/save/return case and invalid-course safe-return case passed. All three cleanup records in the native log report zero failures. XML SHA `cb8dfe047dcd5c70948e5ca03c2557a8e86dd36a5254ac9d25f54eadd628240c`. This failure is preserved, not converted into a pass by source review.

Production `WordboundWorkshop.Update` passes `Application.isFocused` to `ProcessRecoveryChoice`. An unfocused invocation clears the queued decision and suspends input; OnApplicationFocus(false) can also clear it. The old native fixture queued Back then waited for the eventual scene transition. That structure depends on the native editor's focus lifecycle. The failed run does **not** record Application.isFocused at that decision, so focus loss is a plausible explanation, not an observed or confirmed cause. A different return failure cannot be ruled out from that timeout alone.

The correction gives the existing handler an explicit focus input, following the existing `WordboundWorkshopRecoveryTests.Choose` pattern:

- Queue the real Back decision and require `recoveryDecision == 3`.
- Call the production handler with false and require the decision is discarded, the recovery choice and workshop remain visible, the active scene stays Wordbound and the exact owned autosave text remains unchanged.
- Queue Back again and call the same production handler with true. It still runs the real decision-3 branch, `CloseRequested`, launcher `CloseWorkshop`/`BackToLobby`, Session.GoHome and the existing Hub/Create restoration checks. It does not set a scene, Visible flag, recovered history or return intent directly.
- Retain the same deadline, original local seats/bindings and unchanged-autosave assertion after return. The added log explicitly calls this scripted focus and excludes physical focus/input acceptance.

This validates a legitimate lower-level handler/lifecycle contract and adds the negative focus branch. It does not fix production focus behavior or demonstrate that Update receives true in a real window, nor does it exercise clicking the recovery panel or controller focus regain. Those remain separate Windows/user-input acceptance. The old failure must remain associated with the previous implicit-focus fixture; any new native pass must be labeled as the explicit-focus fixture.

Reviewed current native fixture SHA `48b2e00e539516f7f62395e4993416ef3f379d6b9e83f716240602d8ef2eb779`. The reviewed source calls the existing private handler through SendMessage; no fake GameMode.Single state, synthetic result, autosave overwrite, runtime guard change or deadline increase is introduced. Actual rerun outcome is pending root evidence.

# Native input fixture repair

STATUS: IN PROGRESS — source repair after native failures; no local compiler/Unity job.

Evidence: integration `TestResults/editmode_20261023T122055Z.xml` reports 11/13 InputBinding, 5/6 PartyMenuInput and 3/3 WorkshopInputSafety failures. Sampled pad state remained zero/null; the focus callback test also triggered Unity's `ShouldRunBehaviour()` assertion through EditMode `SendMessage`.

Installed Input System 1.20 source (`Library/PackageCache/com.unity.inputsystem@7a4e1a2a8194/InputSystem/Runtime/InputManager.cs`) explains the fixture mismatch: `defaultUpdateType` returns Editor outside PlayMode; the fixtures configure IgnoreFocus plus AllDeviceInputAlwaysGoesToGameView, which `ShouldExitEarlyBasedOnBackgroundBehavior` excludes from Editor event processing. `InputActionState` also ignores Editor action-change notifications, so changing focus routing alone would not test DesktopBinding's actions. The player Dynamic input path is not implicated by these failures.

Authorized repair: a test-only cloned InputSettings scope enables the installed `RUN_PLAYER_UPDATES_IN_EDIT_MODE` feature and Dynamic updates, preserving/restoring the exact original settings object. Assert the actual sampled device state and update type before existing gameplay/menu assertions. Invoke the actual private focus callback by reflection instead of asking Unity to dispatch an EditMode MonoBehaviour message. No product settings, packages, input guards or assertions are to be weakened.

`InputTestFixture` was considered but its installed source explicitly limits support to PlayMode. These EditorWindow/Layout fixtures remain EditMode; the pinned package's existing feature is applied only to the disposable test settings clone. Native acceptance remains pending the coordinator's focused rerun.

Source repair also preserves the original nonserialized feature flags on the clone and explicitly restores the player-update flag before swapping the original settings back: installed `InputManager.ApplySettings` skips all flag reapplication when the original flag collection is null. Setup failures call cleanup, and cleanup restores settings in `finally`, including when another cleanup step fails. Existing fake party-server state is reset per test so reused NUnit fixture instances cannot leak QuickPlay/ready state into a later case.

The three tracked fixture baselines match the current integration source after line-ending normalization; no baseline-only commit or old-source overwrite is needed. New helper APIs were checked against the installed package's `InputSettings`, `InputState`, `GamepadState` and `ButtonControl` source. Native expected cases remain **22** across the three affected fixtures (13 + 6 + 3); no behavioral assertion was removed and no new test case was added.

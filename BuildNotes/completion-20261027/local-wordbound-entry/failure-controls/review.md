# Independent startup-failure input review

STATUS: COMPLETE — no blocking source finding in frozen revision 2; native combined input/return acceptance remains pending.

Reviewed full replacement patch `revision2/failure-controls.patch`, SHA256 `a3a460c7297cfcd2952248bea6b5c688cb5a610bab77d990d9617997ed500ea5`. Revision 1 (`789d75d4...`) remains preserved. This is not an incremental patch to apply after revision 1.

## Confirmed behavior

- StandaloneStartupFailure requires nonempty failure text, an intact launcher, no active local client, online client or launch context, and no initialized PartyMenu. A partially added PartyMenu with null Coordinator permits the fallback; an initialized menu or game HUD retains its own input ownership.
- The existing GUI button queues a return. Update reads it once and checks that the failure text still matches. Loss of focus, typing, an existing gameplay input lease or changed surface ownership discards the queued action. No scene load or return occurs from Layout/Repaint.
- Escape is armed only after release. Controller selection/revision and focus loss suspend the existing binding and pad neutral gate. A held selecting B cannot dismiss the new surface. After a successful decision, both paths suspend so the same sampled input cannot issue another return. Controller device changes use the existing selection contract; keyboard Escape remains a distinct release-gated shortcut.
- Binding creation is lazy and restricted to the standalone failure. Normal startup does not create a second input owner. Launcher.OnDestroy disposes the owned binding before the existing teardown; temporary GUI.enabled is restored in finally. No settings, binding assets, preferences, scene or gameplay rules are mutated by the implementation.
- The corrected cue is exactly ASCII `Esc / B - Back`. Independently compared both revisions: this cue is the only code change; input implementation and six test cases remain identical.

## Verification and limits

Independently verified all three revision-2 candidate hashes and its one baseline hash. Strict application against the owned baseline passed with `--whitespace=error-all`. Existing WordboundInputBinding/WordboundPadFocusGate and the Editor-only test assembly were inspected for selection, release, disposal and actual InputSystem semantics. Producer reports full in-memory semantic compilation; this reviewer did not rerun that compiler or native tests.

The six cases cover Escape single consumption/release, held-pad selection and focus, typing plus a real input-block lease and quiet-frame release, deferred/stale/unfocused clicks, missing failure/active local client ownership, and partial versus initialized PartyMenu. The initialized-menu fixture disables Update before initialization, never authenticates and makes no service request. Test cleanup independently attempts owned object destruction/device removal, restores previous current keyboard/gamepad and typing, and disposes the existing owned InputSettings scope. No original device is removed. No scene is loaded by these tests.

Root should run `Wreckabulary.Online.Tests.WordboundLauncherFailureInputTests`, then verify the combined Update-to-BackToLobby route in a real Windows startup-failure window. Earlier missing-body native coverage proves the handler separately; neither that nor this source review proves physical keyboard/controller focus behavior in the delivered Windows build. No full gameplay, networking or visual acceptance is claimed.
## Native-caught fixture correction (root follow-up)

Native EditMode run 194030 passed 5/6 and failed TypingAndInputLeaseDiscardBackUntilReleased because the existing production input block remained true. The test originally yielded eight EditMode enumerator turns without requesting player-loop updates. KeyBindings' release quiet interval is based on `Time.frameCount + 1`; iterator turns alone do not prove those frames occurred. The failed XML remains preserved at TestResults/editmode_20261026T194030Z.xml.

Independently reviewed the root's exact test-only delta, live test SHA256 `7807a2bb229db45d2b6e588f466a80a2e3772010698e68cf74a05148c0f2fcc1`: add UnityEditor, assert the quiet interval is active immediately after disposal, request ordinary EditorApplication.QueuePlayerLoopUpdate while blocked under a five-second real-time bound, then assert actual Time.frameCount exceeds the saved quietThrough. This matches the existing PartyMenu input-test pattern. It does not write the private quietUntil field, bypass the block, alter production input logic or weaken the held-key/release assertions. No blocking source finding. The prior native failure remains real evidence; rerun of all six cases is required before acceptance. This reviewer ran no native job.

## Observed corrected native rerun

Independently read the actual 194330 gate stdout/end and XML: PASS 6/6, zero skipped, process exit 0; gate duration 31.6 seconds including 11.6-second refresh. The earlier 194030 5/6 failure remains recorded above. The combined 194544 gate also passed the three solo-entry cases plus the exact starter case, 4/4, with all four cleanup reports at failures=0. These are root-run native receipts inspected by this reviewer, not jobs run here. Combined real Windows input-to-return acceptance remains pending.

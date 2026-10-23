# Party input native fixture repair

STATUS: FIXTURE REPAIR COMPLETE; NATIVE VERIFICATION PENDING. No compiler, native job or production input change in this lane.

Root `TestResults/editmode_20261023T131522Z.xml` retains four Party failures: first Connect in FocusLoss, RealPadButtons and Retry; final post-disposal Connect in NewInputBlocker(True). All fail at the real drawn-control pending-action assertion, before relevant HTTP side effects. The shared InputSettings lifetime repair worked; this is a later input-state/lifecycle issue.

The input reviewer confirmed `InputLease.Dispose` calls `QuietInput`, setting `quietUntil = Time.frameCount + 1`. The synchronous EditMode fixture does not advance engine frames between disposal and fresh activation, so production correctly blocks that activation; the quiet state can also contaminate subsequent synchronous cases. Input lane owns bounded scope teardown restoration. This lane will advance actual Unity frames and actual EditorWindow Layout/Repaint before fresh confirmation, retaining the real rendered-control handler and existing behavioral assertions.

The trace is concrete for the final post-lease confirmation. Shared suppression is the source-supported explanation for the other first-Connect failures; the retained XML does not contain their previous frame/lease state, so this review does not claim that each first failure was independently observed with `GameplayBlocked=true`.

Baseline-only copy of root's current fixture is `8d6da87`; exclude it from integration. Deliver only the following repair delta after the input lane's coordinated helper cleanup. No shared-helper or party production source was changed here.

- [x] The five pad cases use UnityTest iterators, requesting `EditorApplication.QueuePlayerLoopUpdate` and yielding until the real `KeyBindings.GameplayBlocked` guard releases. A five-second diagnostic bound fails if capture stays active; there is no clock/state mutation to force a pass.
- [x] The gameplay-lease case additionally asserts suppression is still active immediately after Dispose, verifies held confirmation sends nothing, then requires neutral and a fresh press after the actual frame boundary.
- [x] The driver first draws Layout and Repaint, verifies the selected named button receives GUI focus and is no longer text entry, then samples neutral followed by real South. It consumes the actual drawn `PartyControl` and checks the queued handler, never directly invoking a Connect/Ready/queue callback.
- [x] Every requested draw now proves its Layout/Repaint counter advanced. Failures include frame, blocker, typing, pad selection, logical/GUI focus and draw counts. This closes the previous assertion's ambiguity between absent input and absent repaint.
- [x] Connect, mode/region/fill, create, ready, QuickPlay, retry payload/idempotency, private queue, back/leave, focus/held suppression and explicit mouse handoff assertions remain intact.
- [x] The two former bool TestCases are now separately named UnityTests (`...WithTyping`, `...WithGameplayLease`) because coroutine cases need independent entry points. Total remains six Party cases.
- [x] `git diff --check` passed. No compile or native execution in this lane.
- [ ] Root compiler and six Party native cases, including the genuine engine-frame guard and rendered-focus assertions.

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.Online.Tests.WordboundPartyMenuInputTests' --cold --json --timeout 660
```

Root may batch the class with the other coordinated input repairs. The shared helper must preserve prior external capture rather than clear a real lease. If this installed EditMode runner does not advance `Time.frameCount` after a queued player loop, the new bounded diagnostic will expose that; it must not be repaired by silently clearing production suppression within the behavior under test. No network service or actual multiplayer session is involved in this fixture.


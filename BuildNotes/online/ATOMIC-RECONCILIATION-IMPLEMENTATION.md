# Atomic reconciliation implementation

STATUS: SOURCE READY; independent bounded source PASS through `b7202ff`. Compilation, core/native execution and performance acceptance NOT-RUN for this delta. Root authorized implementation of proposal `24340dc` and remains sole heavy executor; no compile, test, benchmark, Unity or deployment jobs ran in this lane.

Baseline is root `02738cd` including resource optimization and repaired authored Freestyle case. Align only the named owned files; exclude the alignment-only commit from integration. Preserve unrelated dirty ModeDefinition and historical local evidence. Resource gates reported by root: 189/189 core PASS; unchanged probe allocation reduction 13.07% TimeRun / 11.29% Team versus indexed baseline, timing uncontrolled and not a speed claim.

Implementation contract: full initial check before side effects, current mutable checks after preparation under the separate physics rules, unchanged callback exception types, unchanged public ApplySnapshot validation, private post-check physical apply, and abandoned/reentrant/disposed operations fenced with finally cleanup. No validity receipt, public skip flag, deadline/protocol/content change or cross-call validity cache.

Next: checkpoint source, independent recover_input review, add focused negative/positive phase and native ownership/abort tests, checkpoint corrections, hand exact gate list to root. Execution remains NOT-RUN until root reports it.

## Initial source checkpoint

Alignment-only `eaa766a` is excluded from integration. Rules now exposes one synchronous ReconcileAndApply entry that performs full Validate, invokes preparation outside the validation catch, repeats the five complete mutable phases against applyRules, then calls apply. The partial validator remains private. PhysicsWorld retains full public ApplySnapshot; its new operation rejects reentry, captures the applied revision, checks host/disposal/failure/revision after preparation, and clears its operation guard in finally. Controller captures runner/world identities for each attempt and checks them after callbacks; retries run after an aborted operation returns. It clears its guard before FrameChanged and does not mark host ready or emit HostAdvanced after an aborted/stopped attempt.

Marker boundary change approved by root: WB.Match.Reconcile still covers the complete controller path (retry attempts now belong to one outer scope); Presentation remains the context/view calculation. WB.Match.PhysicsApply now covers only the private physical application/checks and excludes Rules validation. The old separate WB.Match.Validate scope is absent from this path; absence is not zero validation cost. Compare end-to-end Reconcile/native batch timing, not old/new PhysicsApply numbers as if their boundaries matched. No profiler-only callback API or Unity dependency was added to Rules.

This checkpoint has not compiled or executed. Focused tests and independent review remain pending.

## Focused regression checkpoint

Added WordboundAtomicValidationTests (25 cases): five-mode success, pre-callback invalid snapshot ordering, abort followed by invalid/valid independent calls, exact callback exception identity in both phases, distinct live rule clones, pending recipe mutations, real deployed-item lifetime/capacity mutations, thrown-flight catalogue/stat mutations, competing fresh errors and valid mutable change acceptance. The reference for fresh rejection is the complete validator on the same snapshot after the same callback mutation.

Added three native local-runner cases to the existing fixture (new class total five): actual physics body reference/pose preservation on rejection/abort/throw/reentry, fresh separate physics rules, subsequent valid apply/restoration and next host tick; disposal inside preparation; initial real presentation Stop and FormatException callbacks cannot publish the abandoned frame or revive host advancement. These use public native paths and the existing one-human offline runner, not reflection or Photon. Existing 30/10-second operations and three-minute lifetime remain unchanged. During setup MarkLocalReady can synchronously call BeginHost; post-PrepareScene and post-reconcile guards stop the outer Poll/GameStarted/BeforePhysics paths after such a callback stops the controller.

R0/core/native still NOT-RUN. Root follow-up filters: WordboundAtomicValidationTests (25 pure cases), existing WordboundSnapshotValidationTests (78), and WordboundNativeLocalGameplayTests (five native cases, WORDBOUND_LOCAL_GAMEPLAY=1). Retain existing online/private presence and host-migration coverage for real peer removal/retry; these local cases do not simulate a multi-peer disconnect or claim public network acceptance.

## Initial host reentry hardening

Self-review found that a presentation callback could invoke public Poll during initial BeginHost, while hostReady is still false. The nested BeginHost would replace the world/driver before reaching the existing Reconcile guard. BeginHost now rejects that reentry before allocating either object. A fourth added native case invokes real Poll from the first ConfigureVisual callback and requires the exact reentry fault, one callback, no abandoned FrameChanged/HostAdvanced publication, paused state and revoked gameplay readiness. The native fixture total is now six; pure count remains 25. Native rejection/abort/throw cases also assert actual rigidbody position, rotation and velocity remain unchanged, then verify successful apply/restoration positions. Execution remains NOT-RUN.

The same initial-composition lifetime also includes the first FrameChanged publication: Reconcile has correctly cleared its own guard before invoking that event, but hostReady is not set until it returns. A separate startingHost guard now spans all of BeginHost with finally cleanup, preventing a FrameChanged subscriber's Poll from replacing the initial world. A fifth added native case invokes exactly that callback, distinguishes the earlier checkpoint event (no physics world yet), and requires one publication callback, no subsequent frame/advance, the exact rejection and revoked readiness. Current native class total is seven. This does not move the event, skip validation, extend timeouts or introduce a receipt/API. Root must run all seven local cases and existing real peer retry/migration cases after source review.

Independent review confirmed one continuation gap in Poll: a synchronous session, BeginHost or FrameChanged callback could stop the controller before Poll reached checkpoint scheduling/client views. Poll now checks stopping/fault (and runner lifetime once selected) after each boundary; it does not require GameplayReady for normal replica-view updates. A sixth added native case changes real canonical presence through the public Match.Disconnect API, invokes actual Poll, and stops from the resulting FrameChanged event. It requires unchanged committed checkpoint, no later frame/advance/active local view, no checkpoint-clock read after the callback, and no fault. Null local-view cleanup remains allowed. This is a deterministic local callback stimulus, not simulated packet loss or real peer departure. Current native fixture total is eight; pure Rules count remains 25. All execution remains NOT-RUN.

## Frozen handoff and required verification

Independent recover_input review found no remaining concrete source blocker through `b7202ff`. It verified the five fresh mutable phases and error order, callback exception identity, separate rule objects, abort/reentry/host/disposal/revision guards, actual public PhysicsWorld calls and native poses, real root Practice constructor and direct test assembly references. It confirmed and re-reviewed the Poll continuation repair. This is not compilation or native acceptance.

Integrate these six commits in order after the root's frozen QA build: `71c25b4`, `ce4af4f`, `45080dd`, `524724d`, `2f12b74`, `b7202ff`. Exclude alignment-only `eaa766a`. Root already owns the resource baseline (`d881a31` and `02738cd`). The original unrelated ModeDefinition delta and historical untracked evidence are untouched. This report's later closure commit is documentation only.

From the integration root, sequentially, stopping on a failed result:

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs core 'FullyQualifiedName~WordboundAtomicValidationTests|FullyQualifiedName~WordboundSnapshotValidationTests' --json
$env:WORDBOUND_LOCAL_GAMEPLAY = '1'
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.OfflineGameplayTests.WordboundNativeLocalGameplayTests' --cold --json --timeout 3600
```

Expected focused counts are 25 atomic Rules + 78 existing snapshot cases, then eight native local cases. Inspect actual XML/log counts and failures; an absent/ignored case is not a pass. Root chooses batching/cold allowance from current editor state. Existing real peer cases `AuthenticatedGameplayRestoresAfterHostDeparture` and `FourAuthenticatedPeersRestoreFlagshipBrawlDuoAfterHostDeparture`, then the prior exact public/full-physical workload, remain necessary to exercise presence retries and final composition under transport events. This local callback suite does not replace those cases.

For performance, compare the inclusive `WB.Match.Reconcile`, whole forward-tick batches and actual player behavior against retained baseline. Do not compare changed PhysicsApply marker boundaries directly, interpret missing Validate samples as zero, or infer 60 Hz/fps success from validation removal. No Worker, catalogue, protocol, Photon configuration, material or runtime deadline change belongs to this delta.

# Atomic reconciliation implementation

STATUS: IN PROGRESS. Root authorized implementation of proposal `24340dc`. Own Rules validation, PhysicsWorld, GameplayController and focused corresponding tests. Root remains sole heavy executor; no compile, test, benchmark, Unity or deployment jobs in this lane.

Baseline is root `02738cd` including resource optimization and repaired authored Freestyle case. Align only the named owned files; exclude the alignment-only commit from integration. Preserve unrelated dirty ModeDefinition and historical local evidence. Resource gates reported by root: 189/189 core PASS; unchanged probe allocation reduction 13.07% TimeRun / 11.29% Team versus indexed baseline, timing uncontrolled and not a speed claim.

Implementation contract: full initial check before side effects, current mutable checks after preparation under the separate physics rules, unchanged callback exception types, unchanged public ApplySnapshot validation, private post-check physical apply, and abandoned/reentrant/disposed operations fenced with finally cleanup. No validity receipt, public skip flag, deadline/protocol/content change or cross-call validity cache.

Next: checkpoint source, independent recover_input review, add focused negative/positive phase and native ownership/abort tests, checkpoint corrections, hand exact gate list to root. Execution remains NOT-RUN until root reports it.

## Initial source checkpoint

Alignment-only `eaa766a` is excluded from integration. Rules now exposes one synchronous ReconcileAndApply entry that performs full Validate, invokes preparation outside the validation catch, repeats the five complete mutable phases against applyRules, then calls apply. The partial validator remains private. PhysicsWorld retains full public ApplySnapshot; its new operation rejects reentry, captures the applied revision, checks host/disposal/failure/revision after preparation, and clears its operation guard in finally. Controller captures runner/world identities for each attempt and checks them after callbacks; retries run after an aborted operation returns. It clears its guard before FrameChanged and does not mark host ready or emit HostAdvanced after an aborted/stopped attempt.

Marker boundary change approved by root: WB.Match.Reconcile still covers the complete controller path (retry attempts now belong to one outer scope); Presentation remains the context/view calculation. WB.Match.PhysicsApply now covers only the private physical application/checks and excludes Rules validation. The old separate WB.Match.Validate scope is absent from this path; absence is not zero validation cost. Compare end-to-end Reconcile/native batch timing, not old/new PhysicsApply numbers as if their boundaries matched. No profiler-only callback API or Unity dependency was added to Rules.

This checkpoint has not compiled or executed. Focused tests and independent review remain pending.

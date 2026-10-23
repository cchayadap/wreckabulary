# Resource validation lookup review

STATUS: SOURCE PASS — independent review of online checkpoint `f46e07c0b826d4ff6263aaee86b9ed1a1ef0d2f3`; no production edits or heavy jobs. Compilation, tests and performance measurement NOT RUN by this reviewer.

Scope: the online lane's `ValidateResources`-only per-call lookup proposal, initially recorded in `f12f16b`. Check exact original rejection order, valid-input equivalence, duplicate/missing resource handling, per-source loose-letter conservation and repeated-call/mutable-data isolation. The parent owns compilation, core/native execution and unchanged-probe benchmarking.

Confirmed baseline: validation restores progress/economy/journal and checks actors/practice/deployments before `ValidateResources`. Its count/key-uniqueness error precedes per-resource validation; tile dictionary construction follows that loop. Source conservation currently scans every tile body with an exact matching source, irrespective of active/owner/generation, and parses/checks its economy tile and removes its letter from the authored bag in original body order. These boundaries must survive indexing.

Design agreement sent to the producer. `MatchProgress.Restore` first requires the exact unique team roster; supported race sources address those teams. `MatchSnapshot.Freeze` rejects null entries and retains immutable body/resource objects in stable order. `ResolvedCourse` has an internal constructor, exposes read-only source/placement collections, and each public `Letters` value is a get-only string. Local indexes can preserve each call's current snapshot and need not extend the static course cache.

The parent's added no-loose-body fast path is safe if it also preserves `LetterBag.FromWord` validation semantics: `null` yields an empty bag; every character of a nonnull word must satisfy `LetterBag.IsLetter`. Invalid characters must still reach the original conversion/error at the same point. No-body sources still require current resource membership. Full bags remain necessary for sources with tile bodies so repeated-letter multiplicity is checked rather than only set membership.

Implementation and exact regression review pending. Requested cases include error precedence, over-budget multiplicity, current membership, wrong team/lap/generation/deadline, and valid→invalid→valid/repeated calls. No runtime or performance conclusion is made from design agreement.

Initial implementation inspection found no production blocker: the combined count/string-key uniqueness guard remains first; local team lookup falls back to the original `Single` on a missing key, preserving its original failure rather than introducing a dictionary exception. Every resource/lifecycle assertion remains in order. The tuple resource-key set is built only after that loop and the existing tile dictionary; exact tuple membership matches the prior three-part `Any` predicate. Ordinal source grouping includes all tile bodies in original order, with no owner/generation/activity filter. The no-body skip still checks current membership and validates characters before avoiding an unused bag. These indexes are local to the call; no dynamic result is added to the static cache.

## Final checkpoint and regression review

The final production change remains confined to `MatchStateValidation.ValidateResources`; no `AuthoritativeLifecycle`, rules, catalogue, scheduling, physics or wire-code change is included. The producer preserved the existing static cache and the unchanged actual-flagship ValidationProbe. Its two source/test baseline hashes matched the parent's files before editing; integration should take only `f46e07c`, not the lane's unrelated dirty `ModeDefinition` or historical evidence.

Nineteen added cases were inspected, for an expected `WordboundSnapshotValidationTests` total of **78**:

- Eight scope/recovery cases cover missing/duplicate records, source/team/lap/generation, an expired deadline and an inactive-lap pending deadline. Each retains the exact intended error and rechecks both the invalid snapshot and earlier valid snapshot.
- Six conservation cases cover excess repeated letters, a foreign letter, a missing economy tile and excess bodies marked inactive, owned by another team or using another recovery generation. These require the conservation error before later body ownership/activity errors, guarding against an over-filtered lookup.
- Three competing-error cases require initial count/duplicate or resource-scope errors to precede malformed tile suffix parsing. Restoring the resource list then exposes the real suffix error, so the later fault is not hypothetical.
- One positive boundary was added on reviewer request: mint exactly the current authored BedSupply batch, validate it, collect a real tile/remove only its body, then validate both the new and prior immutable snapshots repeatedly. The source is demanded by the current TimeRun checkpoint, and no internal setter/reflection is needed.
- One empty-body case retains invalid authored-letter rejection through the public resolver and revalidates current progress after an actual tick advance. Existing multi-mode, changed-state, rule/recipe and concurrent validation cases remain unchanged.

No new mutable state escapes the method and no validation success is cached. Public snapshot collections retain copied/frozen data, so later fixture changes are not silently reflected in earlier snapshots. The no-body fast path keeps invalid-character conversion at its original source iteration point. Unknown/unsourced bodies are still handled by the later body validator.

Source verdict: **PASS**, no remaining concrete blocker in this bounded delta. `git diff --check` on the producer checkpoint passed. Actual compiler boundaries, the 78-case result and allocation/timing effect are pending the parent's gates; source agreement does not establish 60 Hz or public-network reliability. Next: integrate the exact delta, run the focused rules fixture and unchanged ValidationProbe, then use native workload evidence before choosing another optimization.

## Native/core fixture feedback and bounded repair

The parent/producer reported one failure in the 189-case core selection: the new `lap` case tried to index the resource-free minimal Freestyle template. This was a fixture construction error; the earlier source review missed that template assumption. It does not establish a production validation failure or a passing test suite.

Reviewed correction `0faf11b615d63a119d3f3608273b17b6af3dfa5b`: only the test and producer report change. The `lap` branch now uses the existing actual-flagship Freestyle authority fixture, explicitly asserts non-race mode and nonempty resources, validates the original snapshot, changes only one resource's lap from 0 to 1, checks the exact intended rejection twice, and revalidates the original. Count and key uniqueness remain valid, so the per-resource non-race lap guard is the intended first error. The snapshot-copy helper appends an optional resource argument while preserving all other fields, including `Practice`; existing callers retain their behavior. The other seven cases use TimeRun with a nonempty-resource assertion. Expected fixture count remains 78.

Bounded source correction verdict: **PASS**. No production change, internal setter, reflection or weaker assertion; core rerun and performance effect remain pending the parent. Raw core execution was reported by the parent/producer rather than independently rerun by this reviewer.

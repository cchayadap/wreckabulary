# Remaining complete-validation cost

STATUS: REVIEW COMPLETE — bounded allocation candidate identified; no implementation or jobs, no promised frame-rate gain.

The 220124 visible Windows smoke passed its existing 90-update requirement, but this is not production frame-rate acceptance. Its ready window lasted 75.279 s and advanced 4,338 canonical ticks. Complete validation occupied 15.879 s across 4,482 calls; the largest measured child was resource validation (4.477 s), followed by bodies (3.508 s), progress restore (2.465 s), mechanisms (2.267 s), and body budget (1.412 s). Body-source validation is nested inside bodies and must not be added again. These are inclusive elapsed scopes on the owner thread, not CPU or GPU measurements.

Native 221108 corroborates resource validation as the largest measured complete-validation child in both successful short public race-entry cases: Brawl 832.491 ms/451 calls; Team 928.124 ms/432 calls. Their complete full-match coverage remains limited; the new full FFA case failed after real Fusion allocator exhaustion and cannot be treated as a clean performance comparison.

Scope: current integration `356fcd27`, Windows source `ecd968fd` and native source `6c1937ab`. Current immutable caches and per-call lookup optimizations remain necessary; no complete-validation skip or trusted-result cache is proposed.

| Complete-validation child | Windows 220124 ms/call | Native 221108 Brawl ms/call | Native 221108 Team ms/call |
| --- | ---: | ---: | ---: |
| Resources | 0.999 | 1.846 | 2.148 |
| Bodies | 0.783 | 1.304 | 1.658 |
| Progress restore | 0.550 | 0.590 | 0.537 |
| Mechanisms | 0.506 | 0.900 | 1.051 |
| Body budget | 0.315 | 0.386 | 0.630 |

The Windows state has 294 resources/216 mechanisms; the two public race states have 588/432. Workload, build, machine conditions and other source changes differ: this table identifies the repeated section, not a controlled speed comparison. Windows HostStep occupied 54.460 s of the 75.279 s ready window; full validation occupied about 21.1% of the window. Even removing its cost entirely would not establish the requested production FPS. The 90 Unity-update waits took roughly 71.7 s after readiness/capture, and are not a presented-frame-rate measurement.

## Smallest justified next candidate

`MatchStateValidation.ValidateResources` lines 345–346 formats `SourceId/team/lap` with invariant integer formatting for every row, then allocates a Distinct set. At line 365 it constructs a second HashSet of the same identities as tuples for current-source membership. `ValidateMechanisms` line 526 similarly formats a new key string for every mechanism. These calls reconstruct identity only; generation, deadline, demanded-state and physical validation happen separately afterward. On the unchanged Windows state, the two uniqueness projections alone visit 2,285,820 rows during the ready window. This is a derived visit/key-construction count, not measured allocated bytes or isolated time.

Recommend first a **per-call resource tuple set** `(SourceId, Team, Lap)`, retaining it for the existing later membership test. An optional separate second change can replace mechanism uniqueness with the same equality form. Keep the original resource-count short circuit before building the set, the exact error text, and all following checks in their current order. Neither set may survive the call or stand in for validation. `ResourceKey` itself remains required by authority dictionary keys and should not be globally changed.

The equality proof is bounded: these sealed records have get-only IDs/team/lap; their public constructors validate ID characters and integer domains; `MatchSnapshot.Freeze` rejects null records. `WordboundIdentity.IsId` forbids `/`, and invariant decimal team/lap encoding is unambiguous. Ordinal string equality plus exact integer equality is therefore the same identity relation as the current ordinal composite-key equality. Equality has no sort-order obligation. **Do not reuse `SortingKey`**, whose resource team -1 path deliberately preserves current-culture formatting, or change snapshot sort/export/hash behavior. Explicitly retain ordinal/case-sensitive identity in any tuple comparer.

A tuple set still allocates and hashes; this source review does not show what fraction of Resources time belongs to key formatting versus demand/letter checks. The current allocation probe must measure the candidate before claiming value.

## Required proof and remaining risks

- Preserve count-before-duplicate-before-domain-before-loose-letter first errors. Existing `PerCallResourceIndexesRetainScopeAndRecoveryRejections`, `ResourceLookupConstructionDoesNotReorderCompetingFailures`, source-generation, active-station and malformed mechanism cases remain relevant. Add focused old-key versus tuple equality parity for case/prefix/punctuation IDs, team -1, all legal team/lap combinations, repeated identities with different generation/deadline, alternate culture and competing malformed data. A later changed snapshot must be checked again; a retained earlier snapshot must remain valid/unchanged.
- Use the existing unchanged `ValidationProbe` (default validation mode, not `--capture`) on the exact same full flagship inputs for before/after byte counts and correctness. Preserve nonzero allocation calibration, zero errors, and source/input/snapshot hashes. Then run the same existing native public cases and Windows smoke with original artwork; the engine-free allocation result cannot substitute for native/player performance.
- Do not combine this with a demand cache. `WordboundResourceLifecycle.Demanded` reads current lap/finish/completed puzzles, Freestyle selection and solved mechanisms. Its public helpers use first/unique lookup semantics that must not be silently changed. The static consumer and selected-route indexes are already cached.
- A later immutable progress descriptor could avoid reconstructing `MatchProgress.DefinitionHash` and route-derived prefix sets each call (`MatchProgress` lines 151–175, 348–367, 499–523). It needs exact mode + roster identity/order + route semantics, immutable ownership and unchanged public constructor/Restore error order. This is a broader change than tuple uniqueness and only about 0.55 ms/call in the Windows measurement; no such implementation is recommended in this slice.

## Validation boundary remains intact

The actual host path is `GameplayController.AdvanceHost` → real authority advance → Capture → Reconcile → `PhysicsWorld.ReconcileAndApplySnapshot`. The atomic Rules operation performs full validation, then the supported preparation callbacks, then all five fresh mutable-dependency phases before applying. Mutable rules/catalogue, Stop/reentry, registry mutation and abort behavior are already supported and tested; a snapshot's immutable fields alone do not authorize skipping either boundary. Public `ApplySnapshot`, replica imports, recovery and checkpoint validation remain full checks. No production, SDK, physics, timing, input, content, visual default, or deadline change was made in this review.

Evidence SHA-256: Windows JSON `93E06AA63B2527CD8744CA81CBEE721DA46FF5DC43D7775E6300480DF0ABE99B`; native XML `3E33D3ADD7DF68E7115B39EC119CC5F13E272C3034BC4B4C3BD7708F0BDF21D0`; native log `570206680ADAD373F1B95E8BB4377704512881612731EAF458078EA647C25CBF`; reviewed validator `46DFE2BF05CE32EA7179FA4993E5F50E0D5A8ED9D0F10DCB62400034AD328A0C`.

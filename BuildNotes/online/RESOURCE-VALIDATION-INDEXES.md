# Per-call resource validation indexes

STATUS: IMPLEMENTED; INDEPENDENT SOURCE PASS; EXECUTION NOT-RUN. Root authorized the smallest ValidateResources-only change after independent design review. Both recover_assets and recover_input agreed with the bounded design. recover_input reviewed implementation checkpoint `f46e07c` and all 19 new cases with no concrete blocker. No AuthoritativeLifecycle edits, heavy jobs or measurements in this lane. The existing actual-flagship ValidationProbe remains unchanged.

`MatchStateValidation.ValidateResources` currently performs `Resources.Any` for every static source, scans every body for that source's loose tiles, and repeats `Progress.Teams.Single` for each resource/source. The current flagship TeamRace snapshot contains 588 resources; the repeat work is concrete, but its isolated CPU share has not been measured.

Implemented bounded change:

1. Leave the existing combined expected-count/string-key-uniqueness `Need` and its error first. A count or duplicate failure must still precede all per-resource checks.
2. After that guard, build a local team lookup from already validated Progress. Keep the resource iteration order and every source/team/lap/generation/deadline/lifecycle assertion exactly where it is; substitute only team lookup. Progress.Restore has already required the exact unique roster before this method is called.
3. After the resource loop succeeds, retain tile dictionary construction at its existing point. Build a local tuple resource-key HashSet and a local lookup of tile bodies by exact SourceId. Preserve source enumeration and each group's original body order; do not group by owner, generation or lap because the existing conservation loop deliberately includes every matching-source tile body.
4. Replace only current-source `Any` membership and per-source tile `Where` enumeration. Every Suffix parse, economy tile membership and LetterBag removal still executes in its original order. Unknown-source and unsourced bodies remain for the later body validator; no new early rejection is introduced.

All indexes die at method return. No validity result, dynamic state or mutable recipe/rule value is cached between calls. Retain concurrency and valid→invalid→valid coverage; add focused source conservation, wrong team/lap/generation, missing/duplicate resource and competing-error-order cases. Root will run bounded core and the unchanged before/after probe before selecting another change. RetireInactiveResources remains deferred.

## Empty-batch allocation guard

After the existing current-source presence assertion, sources without any matching Tile body avoid creating an unused LetterBag and its 26-entry counts array. The fast path accepts only null or entirely A–Z source strings. `LetterBag.FromWord(null)` originally returned an empty bag; every other valid string has no observable effect without body consumption. Any invalid character still reaches the original `FromWord` call at its original point, retaining its exact exception. Empty strings likewise preserve the original empty-bag result.

Resolved supply and placement letter strings are get-only immutable string values copied by the resolver. CourseValidation validates supply letters; CourseAssetDefinition validates placement resource letters; manifest import validates the document and exact resolved supply/placement data before constructing a ResolvedCourse. The defensive IsLetter check nevertheless preserves bad-letter rejection beyond those supported construction paths. This does not skip economy, catalogue, gameplay rule, body, resource deadline or lifecycle validation.

## Source checks and verification handoff

Before editing, both owned files exactly matched root: MatchStateValidation SHA-256 `9c0abde4346a2bf2ce80955ada47566258e7a3e365a7f3a7694f6dc872bad2ba`; WordboundSnapshotValidationTests `e8d7026d77368ecf4f392e9c3ddc5b7cd55bcfa63b42ea8167c0f148b8d8bd97`. Integrate only the two-file delta and this report; unrelated ModeDefinition and prior local evidence remain untouched.

Nineteen focused cases were added: eight resource scope/recovery failures, six matching-source tile conservation failures (including inactive, foreign-team and foreign-generation bodies), three competing-error ordering cases, one accepted exact batch followed by real tile collection and immutable prior-snapshot revalidation, and one empty-body/current-progress plus invalid authored letters case. Expected WordboundSnapshotValidationTests count: 78, including the prior 59 cases. Every rejection is followed by original valid-state validation; repeated calls must not hide corruption. Existing static-cache concurrency coverage remains.

Root follow-up: run R0, bounded Rules coverage including WordboundSnapshotValidationTests, then the unchanged `dotnet run --project Tools/RulesHarness/ValidationProbe/ValidationProbe.csproj --configuration Release` against the saved indexed baseline with identical hashes. Keep elapsed time informational until native gameplay/profile measurements verify the actual effect. No new 60 Hz, frame-rate, public connectivity or deadline-success claim is made by this source change.

Independent review confirmed the positive exact BedSupply batch is demanded in the current TimeRun lap; real collection/removal validates both the current and previously captured snapshots. It also checked the initial count/duplicate and per-resource/lifecycle ordering, conservation across inactive/foreign-owner/foreign-generation bodies, multiplicity and exclusively per-call lookup lifetime. Review is source-only; R0, core, benchmark and native results remain for root to record.

## First executed gate and bounded fixture repair

Root R0 passed. Root bounded core ran 189 cases: 188 PASS, one FAIL. The new non-race `lap` case failed while selecting resource zero because the minimal Freestyle proof template intentionally has no recovery resources; it never reached the intended validation assertion. This was a fixture construction error, not evidence that the production validator accepted the invalid lap. The original source review missed that empty template boundary.

The repair `0faf11b` uses the existing actual flagship Freestyle fixture for that case, explicitly asserts non-race mode and nonempty authored resources, preserves all snapshot sections including Practice, changes exactly one resource to lap one, and checks the same rejection twice followed by original immutable snapshot acceptance. The other seven scope cases retain the TimeRun proof fixture with an added nonempty assertion. Expected counts stay 78 fixture / 189 selected core. Only tests and this report change; production, timeouts and benchmark inputs remain unchanged. Independent recover_input repair source review: PASS; root rerun pending.

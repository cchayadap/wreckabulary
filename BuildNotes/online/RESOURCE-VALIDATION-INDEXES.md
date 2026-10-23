# Per-call resource validation indexes

STATUS: DESIGN REVIEW PENDING. Root authorized the smallest ValidateResources-only change after independent design review. No AuthoritativeLifecycle edits, heavy jobs or measurements in this lane. The existing actual-flagship ValidationProbe remains unchanged.

`MatchStateValidation.ValidateResources` currently performs `Resources.Any` for every static source, scans every body for that source's loose tiles, and repeats `Progress.Teams.Single` for each resource/source. The current flagship TeamRace snapshot contains 588 resources; the repeat work is concrete, but its isolated CPU share has not been measured.

Proposed bounded change:

1. Leave the existing combined expected-count/string-key-uniqueness `Need` and its error first. A count or duplicate failure must still precede all per-resource checks.
2. After that guard, build a local team lookup from already validated Progress. Keep the resource iteration order and every source/team/lap/generation/deadline/lifecycle assertion exactly where it is; substitute only team lookup. Progress.Restore has already required the exact unique roster before this method is called.
3. After the resource loop succeeds, retain tile dictionary construction at its existing point. Build a local tuple resource-key HashSet and a local lookup of tile bodies by exact SourceId. Preserve source enumeration and each group's original body order; do not group by owner, generation or lap because the existing conservation loop deliberately includes every matching-source tile body.
4. Replace only current-source `Any` membership and per-source tile `Where` enumeration. Every Suffix parse, economy tile membership and LetterBag removal still executes in its original order. Unknown-source and unsourced bodies remain for the later body validator; no new early rejection is introduced.

All indexes die at method return. No validity result, dynamic state or mutable recipe/rule value is cached between calls. Retain concurrency and valid→invalid→valid coverage; add focused source conservation, wrong team/lap/generation, missing/duplicate resource and competing-error-order cases. Root will run bounded core and the unchanged before/after probe before selecting another change. RetireInactiveResources remains deferred.

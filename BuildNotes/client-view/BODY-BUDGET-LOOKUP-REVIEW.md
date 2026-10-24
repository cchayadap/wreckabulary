# Body-budget placement lookup independent review

STATUS: IN PROGRESS — awaiting implementation checkpoint.

Scope: source-only review of Online's authorized replacement of the inner placement scan in MatchStateValidation.ValidateBodyBudget with the already constructed content.Placements lookup. No production/test edits or heavy jobs by this reviewer.

Confirmed baseline boundaries: StaticContent constructs its Ordinal placement dictionary before full validation, and the fresh mutable validation phase obtains the same immutable course/mode content. Duplicate placement keys already fail that existing construction. ResolvedCourse wraps copied placement records in a read-only collection; Id/Purpose are get-only values. WorldBodyState's public constructor rejects null SourceId, and the snapshot codec passes through that constructor; empty string remains a legal dictionary query and must preserve its existing false-match behavior. The outer All, subsequent uniqueness short-circuit, map item dictionary lookups and dynamic rules checks must stay in their original order.

Next: inspect exact runtime delta and valid/invalid/competing-error regression construction, including the full-then-fresh-mutable atomic path. Compiler/core/native/performance execution belongs to root and is NOT RUN here.

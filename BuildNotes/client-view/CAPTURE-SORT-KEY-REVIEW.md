# Immutable snapshot sorting-key proposal

STATUS: READ-ONLY SOURCE REVIEW COMPLETE; IMPLEMENTATION / TESTS / PERFORMANCE COMPARISON NOT RUN.

Reviewed integration HEAD `ecd968fdee01c892a8c0536c966e473c9bc295b7`, principally `Assets/_Project/Scripts/Rules/Wordbound/MatchState.cs` (SHA-256 `C3BD3FE5525B245A2E779EC41324815B8B413447C44199823CFA9D4871F640AA`). Root reports approximately 2.9 ms per native Capture and a representative 588 resources / 432 mechanisms. This review establishes source conservation, not an independently reproduced timing result.

## Smallest supported change

Cache the exact existing slash-delimited key once, after the existing successful constructor validation, on each immutable `MatchMechanismState` and on `MatchResourceState` only when Team >= 0. Expose the cached string internally to the two existing MatchSnapshot selectors. A resource with Team == -1 must keep evaluating `SourceId + "/" + Team + "/" + Lap` on each selector call. A readonly backing field plus internal getter is sufficient; there is no need for a global cache, lazy mutation, lock, tuple comparer, prefix comparer, invariant-format replacement or new allocation in Freeze.

Keep `Freeze` byte-for-byte: bounded Take/ToArray, null/count validation, stable `OrderBy(key, StringComparer.Ordinal)`, final ToArray and read-only wrapper. Keep all constructor and MatchStateValidation rules and their order. Do not reuse the separate invariant `MatchStateValidation.ResourceKey` helper here: legacy snapshot sorting deliberately has a different negative-team culture behavior.

## Why the keys and order can remain exact

Both classes are sealed, with get-only identity/team/lap properties. Mechanism constructors admit only Team 0–7 and Lap 0–2; resource constructors admit Team -1–7 and Lap 0–2. The keys contain only the immutable original identity, literal slashes and these numbers. Generation, recovery time, solved state and physical evidence do not enter the selector. Replacing a state still constructs a fresh key; retained snapshots never acquire mutable cache state.

The existing integer concatenation uses default `Int32.ToString`, whose G format consults the current culture. For the permitted nonnegative single digits, there is no sign, separator, exponent or rounding component. Numeric formatting uses Basic Latin digits even when NativeDigits is customized, so those exact suffixes are unchanged across construction/capture cultures. This is a contract-based argument; the installed Unity runtime still needs the focused cases below. Sources: [Int32.ToString](https://learn.microsoft.com/en-us/dotnet/api/system.int32.tostring?view=net-7.0), [NumberFormatInfo.NativeDigits](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.numberformatinfo.nativedigits?view=net-10.0).

Team -1 is different: a changed `CurrentCulture.NumberFormat.NegativeSign` can alter its key after construction. Preserve the old expression per call for this branch, including custom negative signs and resulting equal-key behavior. Do not claim universal culture-invariant ordering or hashes for negative-team snapshots; preserve the prior behavior instead.

Exact string equality with the previous selectors plus the unchanged ordinal stable sort preserves ordering, including equal-key input order. Identity prefixes make tuple sorting unsafe: valid IDs `a-` and `a` produce `a-/0/0` before `a/0/0` under the current combined key, whereas sorting ID first puts `a` first. Caching the full original key avoids that difference.

`MatchSnapshotCodec.WriteSnapshot` explicitly serializes each existing public field; it does not reflect internal fields/properties. Its source hash is `1B7D118F2669020166189DD38BC83B78B701A402DAC5D4E76034A41842510D52`. An internal key therefore adds no wire member. With identical collection order and unchanged fields, Export and ComputeHash receive identical canonical data. Keep codec/schema/protocol/content untouched.

## Required focused verification before integration acceptance

1. Compare actual snapshot Resources and Mechanisms object-reference order against the old literal selector plus StringComparer.Ordinal across every allowed nonnegative Team/Lap combination. Include reversed input, all valid punctuation, prefix IDs (`a`, `a-`, `a.`, `a:`, `a_`) and duplicate keys with distinguishable generation/phase values to prove stable ties.
2. Construct states in one culture, capture in another, and mutate a cloned current culture's number settings between captures. Include Arabic NativeDigits, custom PositiveSign/NegativeSign/separators and all 24 nonnegative Team/Lap combinations. Restore culture in finally. Exact old-selector parity is the oracle, not an assumed new invariant representation.
3. Exercise existing Team -1 resource instances across changed NegativeSign values, including a value that changes order relative to nonnegative teams and an empty sign that creates a stable tie with Team 1. Require parity with the old selector at each capture. Do not normalize away this legacy distinction.
4. Retain original snapshots across replacement resource/mechanism states; assert original object values/order/Export/ComputeHash unchanged, and changed states receive the right new order. Keep five-mode existing round-trip and malformed-state validation coverage. Existing `CultureAndInputCollectionOrderDoNotChangeCanonicalState` covers only fr-FR and is insufficient alone for the negative-sign edge.
5. Root owns compiler/core/native execution. Measure allocation/Capture under the same representative fixture and native phase configuration after correctness passes; do not infer a speedup from source alone.

The expected saving is repeated key creation for long-lived nonnegative states. Each cached state retains one extra short string/reference; replacements still allocate a new key. Sorting and snapshot array copies remain, and shared Team -1 resources keep the original key cost. No guaranteed frame-rate or full Capture improvement follows from this bounded proposal. No runtime source was changed by this review.

# ObserveHost mechanism lookup

STATUS: IN PROGRESS — source implementation; no compiler, core, native or probe jobs run.

Authorized scope is a lazy, one-ObserveHost lookup keyed by exact team/lap/puzzle
identity, with the original queried-key predicate Single failure behavior and
unchanged phase/contact/authority ordering. No before-physics, socket, SDK,
checkpoint cadence, deadline or validation change is included.

Proposal read: puzzle-race `OBSERVE-HOST-COST-REVIEW.md`, commits
`c585553c` → `5f110981`. Current root baseline inspected at
`c7b0d8a933c2235ba7cca7c195799c7964ae3b33`. The lane's two runtime partials differ
from root due to retained CLOCK/floor fixes; synchronize only those exact files
in a separately excluded baseline commit before implementation. Existing
ModeDefinition, ProtocolV3 and checkpoint-log dirt remains untouched.

## Source checkpoint

Baseline-only `1c8f1575` is excluded from integration. Only the delta after it is
deliverable. The two partials retain root's current CLOCK diagnostics, floor
boundary handling and practice behavior. No shared Rules files were synchronized.

`AuthoredMechanismLookup` is an internal value type holding one frozen snapshot
and a lazy `ILookup<(int Team, int Lap, string PuzzleId), MatchMechanismState>`.
ObserveHost creates it after the existing RequireHost, exact-pose-set and live
collider-refresh checks, then passes it by reference through authored observation
and FamilyReady. No group is allocated/enumerated until the original first
authored query; an invocation with no authored queries builds no index.

Only the original per-site query and the later CLOCK query use the lookup.
Grouping accepts duplicate keys and preserves their encounter order. The queried
group calls predicate `Single(m => true)`, retaining the original missing and
duplicate exception type/message at that query, including a valid query beside
an unqueried duplicate. Tuple string equality is ordinal, matching the original
string `==`. Immutable constructor-frozen records exclude null/mutating key
selectors. The index is local to one call: no controller frame, authority,
catalogue/rules, ownership, contact, fixture phase or validation result is cached.

The phase dictionary access still precedes CLOCK's original query. Solved/active
guards, recipe/actor selection, family/contact checks and output order are
unchanged. Legacy sockets, Evaluate and MoveAuthoredFixtures keep their original
queries. Reentry creates a separate local index; exceptional exits discard it.

Root approved an Editor-only InternalsVisibleTo attribute for the existing
`Wreckabulary.Online.Puzzles.Tests` assembly in the existing runtime source file.
The nested type remains internal in all builds; the friend declaration is absent
outside UNITY_EDITOR. No public runtime API or asmdef reference is added.

## Focused regressions

Eight added cases in existing `WordboundAuthoredPuzzleContentTests` exercise the
actual production helper. The fixture now has 22 source-counted cases (14 prior).

- Actual flagship TeamRace Solo, two teams, 72 sites and 432 mechanism records:
  all 144 original team/site queries return the exact original references in
  order; repeated queries preserve the exported frame.
- Missing and duplicate queried keys preserve predicate Single exception messages,
  allow valid queries before/after failure, and do not contaminate a fresh valid
  frame lookup. Separate competing-error cases reverse missing/duplicate query
  order and verify the same completed prefix and first failure.
- Case-distinct IDs remain distinct beside an unqueried duplicate.
- Mutation of the caller's constructor array cannot alter its frozen snapshot;
  a replacement frame's solved/motion records remain separate from the old lookup.
  That deliberately constructed replacement tests lookup identity, not acceptance
  of an invalid solved state by the full validator.
- A real AuthoritativeMatch.Advance accepts an authored moving fixture's pose and
  canonical motion together. The new fully validated snapshot yields the new
  exact mechanism reference/motion while the retained old frame stays unchanged.
  Its host evidence is supplied by the domain fixture; it is not native traversal.

Tests use the root's current public MatchSnapshot constructor, preserving Practice,
and public authority/evidence APIs; no hidden setter or reflection is used. The
lane's unrelated older Rules files are not an integration baseline.

## Construction-inclusive comparison proposal

Before claiming savings, measure the actual production helper against the original
predicate query sequence over the same validated flagship snapshot and plan. Use
the two-team initial 432-mechanism/144-query route, include construction plus first
ToLookup on every iteration, warm both paths equally, alternate order, consume
the returned records and assert exact references/unchanged full snapshot hash.
Retain every sample and report iteration/query counts, inputs and runtime.

This removes repeated scanning but adds a per-call lookup with groups/arrays, so
lower allocation is not assumed. Unity Mono previously returned ineffective zero
thread-allocation counters; do not treat that as zero allocation or reuse that
unsupported byte comparison. A root-approved allocation recorder must first pass
a positive byte control and expose its overhead/window; otherwise label bytes
unavailable and retain elapsed data. No benchmark is added or executed in this
source-only slice. A separately approved pure projection must link the actual
helper if .NET allocation evidence is desired, not benchmark a copied algorithm.

Then repeat the unchanged native public workload and actual six-family progression
with existing ObserveHost/HostStep scopes. Compare valid complete windows and
call/state counts, including failures. Inclusive ObserveHost includes fingerprint,
collider, recipe and physical-query work; 62,208 derived original predicate
evaluations are not measured cost savings, exclusive CPU or FPS evidence.

## Remaining verification

Source/static only: `git diff --check` passes. Independent source review is pending.
Compiler, native 22-case fixture, moving-family behavior and performance are NOT RUN.
Root runs normal R0 and the exact native filter
`Wreckabulary.Tests.WordboundAuthoredPuzzleContentTests` before accepting the helper,
then selects the existing physical/CLOCK and same public comparison with unchanged
deadlines/environment. No core projection includes this Unity Puzzles assembly;
an engine-free core pass alone cannot validate these new tests or friend access.

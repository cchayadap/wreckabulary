# Next bounded reconciliation/validation cost candidate

STATUS: SOURCE REVIEW PASS — bounded implementation f6b76fb5 independently reviewed by recover_input; all execution pending.

Scope: current integration validator and atomic reconciliation path. Root owns all
measurement/build/native work. Installer completion evidence is separate in
UNITY-6000.6.4-INSTALL-VERIFICATION.md; measurements during extraction are not a
clean optimization comparison.

## Exact change and source baseline

Only the map-body identity predicate in `MatchStateValidation.ValidateBodyBudget`
changes: each `course.Placements.Any` is replaced by the existing
`content.Placements.TryGetValue` followed by the same `Purpose != "Resources"`
condition. This entry is used by the initial full validation and the fresh mutable
validation after preparation. No new cache, mutable verdict reuse, validation skip,
clock, protocol, SDK or pacing change is included.

Before editing, both owned source files matched integration at
`f865c037ea27a70889097384bfc64e9784164642` byte for byte. No baseline-copy commit is
needed. Validator SHA256 was
`30C3710301E036B878D3E4C92D9780DECA55D800FBB13FB4E0AFC5A9ED18256F`;
snapshot tests SHA256 was
`1F6E242400F71748EDE2185EC78C8BBB3C3D65EB3FA51893496E2CD073C50381`.
Unrelated ModeDefinition, ProtocolV3 and checkpoint logs remain untouched.

## Preserved boundaries

- `StaticContent` already constructs the ordinal dictionary before both entry
  paths; duplicate placement IDs therefore already fail before this predicate.
- Resolved placement Id/Purpose are immutable. `WorldBodyState` rejects a null
  SourceId, including through codec import; the supported empty string remains a
  failed lookup. Ordinal dictionary equality matches the previous string `==`.
- Original map-body enumeration, earlier `items[b.Id]` access, `All` encounter and
  short-circuit order, the later distinct-source check and exact error text stay
  unchanged. All deployment, ownership, recovery, pending-craft and pool-capacity
  checks still execute in their original order with current rules.
- Inner work becomes one existing-dictionary query per visited map body instead
  of a placement scan. This is source-level operation reasoning, not a measured
  elapsed-time or allocation result.

## Meaningful regression scope

Seven added cases in the existing `WordboundSnapshotValidationTests` (123 total
source-counted cases) build real Map/World economy entries through PlaceFurniture,
using approved resolved prop identity, ownership, position and quarter-turn yaw.
These are constructed validator fixtures, not native traversal acceptance.

Four mode cases cover valid map placements, ordinal case mismatch at the earlier
source check, empty-source rejection at the budget guard, invalid import, repeated
valid/invalid validation, exact retained snapshot round-trip and successful atomic
application through distinct initial/final rules. One case covers two independent
Map items sharing one approved identity, competing earlier physical-bounds and
later narrative errors, and retained prior state. Two cases bind a Map item to an
actual approved/demanded protected Resources placement: source validation accepts
that identity, but the unchanged budget guard rejects it before prepare/apply.
Existing fresh mutable dependency tests remain required; no test bypasses the
complete validator or exposes internal setters.

## Evidence limits and next gates

Earlier root record `BuildNotes/client-view/native-public-team-copy-20261024.json`
(003910, source 0adab099) recorded 519 full BodyBudget scopes / 379.398 ms in a
16.0679429-second ready window. FullValidation was 2849.340 ms; HostStep was
9779.482 ms for 503 steps. These inclusive elapsed scopes overlap. The map predicate
is an unmeasured subset; the fresh mutable budget call is not separately timed.
This candidate cannot be presented as fixing the online failure or whole hitch.

Root should run R0 and focused core coverage for
`WordboundSnapshotValidationTests|WordboundEntityBudgetTests|WordboundAtomicValidationTests`.
Use the unchanged `Tools/RulesHarness/ValidationProbe` default workload (not capture/advance)
before/after with actual flagship inputs, fixed seed 0123456789abcdef, positive
byte controls, unchanged input/output hashes, counts, all raw elapsed and allocated
bytes. Compare the same public TeamSolo/Windows workload only after source gates;
installer load must be absent. No new microbenchmark or lane execution was added.

Compiler, core, native and performance acceptance: NOT RUN by this lane.

Independent source closure: recover_input confirmed exact original root blobs,
ordinal/non-null immutable keys and preserved All/error ordering. It traced the
seven fixtures through current root APIs, including the protected Resources Map
case reaching the intended guard and existing fresh MaxDeployed rule coverage.
No source finding remained; no compiler, test or measurement claim was made.

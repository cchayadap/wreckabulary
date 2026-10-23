# Authored capacity and reclamation fixture

STATUS: NATIVE PREFLIGHT FAILED; bounded test-only correction implemented, source review and preflight rerun pending. The prior plan is retained in MAX-BODY-CAPACITY-PLAN.md (1ed4a890 +6302cca0), including Input's conditional design review24605e0c. Root subsequently authorized the passive pinned-metadata reader and the new test-only partial-file boundary. No production, SDK, heap policy, service, existing fixture/helper, timeout or admission rule changes.

## Exact scope

The only new source is `Assets/_Project/Tests/OnlineGameplay/WordboundNativeCapacityTests.cs`, another partial of the existing WordboundNativeGameplayTests in its existing Editor test assembly. Unity creates its meta on import. Two new methods are:

- `AuthoredCapacityPreflightUsesTheFullBudgetAndPinnedPassiveHistoryMetadata`: local preflight only.
- `TwoAuthenticatedPeersReclaimAndReuseTheMaximumAuthoredBudget`: signed private authored route, ExternalPhotonWorkshopGameplay category, two scripted clients.

Preflight executes both separately and inside the existing authored factory before any guest allocation. It retains the proven TABLE circuit, adds three connected24m annex rooms with76 fixed PLATE props each, three real gated Door connection bodies,123 reserve A letters linked to the TABLE objective, and four optional BALL letters. Actual resolver, exported manifest, exact installed items/rules, full private-content factory, WordboundEntityBudget and initial AuthoritativeMatch validation must all succeed. It asserts exact reserved totals512, fixed236, pool276, critical128, actor/burst144 and optional4. The initial live ledger is **366**, comprising2 players,1 keepsake,228 props,3 connection bodies and132 letters. Two of four player reserves and all144 actor/burst reserves are unoccupied. This is full-budget admission, never512 live-body acceptance.

## Actual native sequence and evidence

The Unity method directly returns shared Run, preserving its Play Mode transition, signed upload/download/Ready/private allocation, original eight-minute lifetime and NUnit600000 timeout. After common real movement/command/ACK checks, the two peers carry the keepsake and complete two TABLE laps using existing admitted movement, collect, craft, deploy, native socket evidence and UsePuzzle. No direct transform, physics, canonical, network-object or inventory mutation is used.

Each populated state must have366 bodies, exact source letter multisets, owner/current resource-generation identity and a committed/received/acknowledged snapshot. The test observes120 ordinary remote Runner ticks, then reads actual received-history occupancy; it does not force network fields to change or claim120 ticks implies60 received histories. The fixed root IDs remain invariant throughout.

Before each UsePuzzle, a temporary host FrameChanged observer retains the last unsolved frame and first solved frame. The consecutive canonical ticks supply the actual loose source IDs and generation, including a possible600-tick refill during earlier waits. The same transition must leave minted letters unchanged, spend precisely observed loose letters plus the five-letter consumed TABLE, and remove their economy/native identities. Exact low-water state is committed and acknowledged before memory assertions. Four optional letters stay unchanged during the solved interval, then retire/recreate along with128 critical letters on the next real carrier-delivered lap. All retired IDs must be absent, new source multisets exact, fixed roots unchanged, and retained immutable snapshot bytes unchanged. The second completed lap must still have no result.

The client object's1280-byte bucket must show fewer used segments and either more free segments or more free pages after retirement, then more used segments and fewer free segments/pages after new identities populate. Statistics may lag scene membership, so a bounded25-second existing-style wait polls only the bounded statistics snapshot at most every250ms. Full per-object reflection/history enumeration occurs only at recorded boundaries. Missing statistics or mismatched pinned metadata fails the case; unavailable values never become zero/pass. Retained allocated pages need not shrink. Two cycle trajectories are logged without claiming long-run plateau or exact allocator causality.

## Passive SDK evidence boundary

The Editor-only fixture reads installed `NetworkObject.Meta` → `NetworkObjectMeta._snapshots` → `NetworkObjectHeaderSnapshotList._count`. It preflights exact field/type names and int count, and reads the pinned allocator Sizes metadata: preceding bucket128 and body bucket160 eight-byte words, index25,57 entries. Every observed gameplay body must report315 network words and the active online heap must remain2048×32KiB. It records actual min/max/total/at60 history counts, object/body counts, canonical/Runner ticks and monotonic observation time. The client must expose some received history; individual static bodies need not reach60.

No interpolation getter, NextSnapshot, reflection write, forced state update, SDK code edit or global config mutation is used. Allocated-segment high-water is not called live memory. TotalBytesFree is not used. The connection platforms are platform-kind coverage, not moving-fixture history proof. A two-client test is not eight-process memory acceptance.

## Required verification

Native232239 on the root integration candidate compiled and ran97 selected cases:96 passed, capacity preflight failed before guest/service creation. Its resolver reported asset.unknown for every BALL prop zone. The fixture and initial source review incorrectly assumed the broad item catalogue's BALL was also in the installed authored geometry. The flagship geometry explicitly registers only its twelve equipment recipes; BALL has no approved alias there (nor in the proof catalogue). This is a fixture error, not a reason to broaden runtime catalogue admission.

Root approved substituting the actual registered PLATE for the228 props. Its installed size0.54×0.073×0.54m fits the unchanged0.6×1×0.6m zone and1m grid, without overlap. The factory now explicitly asserts registered asset membership, letter identity and geometry dimensions before constructing zones, then verifies all228 resolved placements and every initial/recreated prop's real Map origin and PLATE word. The reserved body arithmetic, optional four BALL letters (ordinary valid A–Z letters), real TABLE route and all retirement/memory assertions are unchanged. The original failed XML is retained at root TestResults/editmode_20261023T232239Z.xml. Compiler/preflight rerun and live capacity acceptance remain required.

Input independently reviewed404f2604, finding no blocking algorithm, public API or lifetime issue. Review checked actual geometry/content preflight, passive metadata, consecutive solve observations, exact spend/mint and retired-ID checks, acknowledgement, immutable roots/snapshots and bounded statistics lag. Its wording correction is applied: the final observation says retiredEntityIdsReused=false, not a claim that numeric resource generations never repeat across separate lap scopes. Source review does not establish the authored resolver, runtime allocator trajectory or wall-time feasibility.

Root remains the sole executor. Run R0, then the exact local preflight first so a content/metadata error cannot create service records. After the separate full-scored FFA64MiB case succeeds, run the native case with existing private workshop flags and unchanged cleanup:

```powershell
$env:WORDBOUND_EXTERNAL_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_WORKSHOP_GAMEPLAY = '1'
node Tools/kit/gate/game_gate.mjs test editmode --assemblies Wreckabulary.Online.NativeGameplay.Tests --filter AuthoredCapacityPreflightUsesTheFullBudgetAndPinnedPassiveHistoryMetadata --cold --timeout 3600 --json
node Tools/kit/gate/game_gate.mjs test editmode --assemblies Wreckabulary.Online.NativeGameplay.Tests --filter TwoAuthenticatedPeersReclaimAndReuseTheMaximumAuthoredBudget --cold --timeout 3600 --json
```

Expected one case per exact filter, zero skipped. Outer cold allowance does not alter the eight-minute fixture or per-operation deadlines. Inspect XML, raw allocator/history observations, OOM/errors, exact content/ACK checks and cleanup before acceptance. The throughput feasibility of366 bodies/two physical cycles remains unmeasured; failure must be retained rather than increasing the heap, runtime or rule budget. The most recent full FFA64MiB run225207 ended during collection and did not establish scored-finish or plateau acceptance; collection repair1ecec6b6 is separate.

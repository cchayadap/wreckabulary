# Online body capacity and reclamation acceptance plan

STATUS: IN PROGRESS — source-only feasibility review. No fixture implemented, compiler/native job started, or SDK/configuration changed. Full FFA with the reviewed 64 MiB online policy is a prerequisite and remains root-owned. This report does not establish maximum live-body acceptance.

## Scope constraint that must remain visible

The current real authored route is `WordboundLiveWorkshop.PrepareWorkshopCore`: exactly two authenticated private peers and only Freestyle or TimeRun. It uses PartyCoordinator upload/download/Ready/private allocation and verifies the downloaded document, manifest, catalogue/rules and signed roster. Reuse this route; do not substitute a manually spawned runner or mutate canonical snapshots.

`WordboundEntityBudget.FixedCount` reserves `Mode.MaximumPlayers`, not the actual roster. TimeRun reserves four players, Freestyle eight. Therefore two actual peers leave two/six reserved body slots absent even if every dynamic pool slot could be occupied: the corresponding theoretical live-body ceilings are 510/506, before further unoccupied reserves. A claim of 512 simultaneous bodies is incompatible with the existing two-peer authored route. TeamRace Solo has two maximum players, but adding that route requires a different split-party/admission fixture and an independently proved action sequence; it is outside the smallest existing-helper change.

The unchanged installed rules reserve 10 letters, two carried slots and two deployed slots per capacity seat. The enabled longest handheld recipe is six letters and longest deployed recipe five, giving `10 + 2*(6+1) + 2*(5+1) = 36` actor/burst bodies per capacity seat. Full budget admission also reserves critical recovery, physical auxiliaries and optional admission room. Merely adding 510 props is rejected. These reserves must not be reduced to manufacture a passing capacity test.

## Smallest high-load candidate on the existing signed route

Use a new factory and one afterStarted coroutine in the existing `WordboundNativeWorkshopTests` partial, returning `Run(2, false, authoredWorkshop: true, authoredContentFactory: ..., afterStarted: ...)` directly. Keep EnterPlayMode flattening, eight-minute cancellation, NUnit 600000 ms timeout, existing coordinator/startup/body/movement/command/ACK deadlines, cleanup and exact downloaded-content assertions. No changes to `WordboundNativeFullMatchTests`, production code, SDK, global Photon config or service.

Proposed TimeRun content is an authored three-lap CLOCK circuit derived from the existing compact route and installed physical-site profile, with no reward letters and no smash/wind sites:

| Count component | Budget reservation | Actual initial body target |
| --- | ---: | ---: |
| Player seats | 4 | 2 |
| Keepsake | 1 | 1 |
| Authored Props placements | 228 | 228 |
| Authored moving fixtures, one per lap | 3 | 3 |
| Critical renewable source letters | 128 | 128 |
| Optional initial letters | 4 | 4 |
| Actor/burst headroom | 144 | 0 |
| **Total** | **512** | **366** |

The resulting exact full-budget values should be FixedBodies236, PoolCount1, PoolBodies276, CriticalRecoveryBodies128, PhysicalAuxiliaryBodies0, ActorAndBurstBodies144 and OptionalBodies4. These are source arithmetic, not a successfully resolved manifest. The factory must assert the actual returned values and reject any different result before external allocation.

The 128-letter critical source includes the required CLOCK letters plus surplus valid letters; every source stays within the existing 1–128 A–Z bound. The four optional letters are a separate unconsumed source. Geometry/placement planning must use installed catalogue assets and enough connected rooms to keep 228 props outside the working path and recovery/socket/sensor volumes. Random placement exhaustion is not a reason to relax validation. Prefer a deterministic fixed set or sparse bounded zones; CourseZones is capped256, each random zone count256. Actual CourseResolver, full authored-content construction, budget and initial snapshot validation are mandatory preflight, with exact unchanged item/rule bytes. Current public original-art/profile selection remains unchanged.

### Admitted retirement and recreation sequence

1. Complete the common authenticated startup and exact-body checks. Read actual Runner.Config on both peers: 2048 pages × 32 KiB, original tick60, reliable mode and prefab identities. Require body-kind counts and canonical/network IDs matching the resolved target, with no duplicate or missing body. The election carrier is counted separately from the366 canonical bodies.
2. Observe the populated steady phase and allocator/history evidence described below. Retain exact stable prop/platform IDs, all128 source tile IDs, and the immutable starting snapshot bytes.
3. Through normal movement and commands, collect only the five CLOCK letters, craft, deploy, trigger the actual timed fixture, traverse its sensor and submit UsePuzzle. Do not invoke Authority.Advance/Submit directly or touch transforms/Rigidbodies. Require the real accepted receipt and physical evidence, not a scripted solved flag.
4. At the source-retirement boundary, require the123 uncollected source IDs and consumed CLOCK body to disappear from both peers and the exact downloaded/ACKed checkpoint. Keep the four optional tiles and all authored root IDs unchanged. Observe allocator low-water before advancing the keepsake/checkpoint into the next lap. Root/test must first verify that this valid route exposes a stable low-water interval; if it does not, do not infer reclamation from a single mixed despawn/spawn frame.
5. Advance the real route/checkpoint. Require128 new tile IDs for the next lap's source generation, exact conserved letters, unchanged roots and restored expected body count on both peers. New IDs must not reuse retired IDs. Repeat the next lap for a second retirement/recreation cycle, then retain normal exact durable-ACK and cleanup checks. Final scored/race finish is not needed for capacity proof, but no unexpected result or fault is permitted during the measured cycles.

The expected initial366 body count is higher than the approximately178-body FFA history failure. It remains a legal stress candidate, not proof of 512 live bodies or every possible transient. Two physical CLOCK completions and two body churn cycles have unmeasured wall-time feasibility under the unchanged eight-minute lifetime. Do not extend the deadline if composition/validation cannot finish in time; preserve the result and separate capacity from throughput.

## Smaller reclamation discriminator if CLOCK route is too broad

A separate two-peer Freestyle factory can retain the existing station-selection route: a large128-letter station, a small five-letter station, zero reward, and valid fixed roots totaling88 reserved bodies (including any required fixture bodies). With288 actor/burst reserves and eight optional letters, its legal initial large-station target is218 actual bodies. Switch large → small → large twice through actual PracticePuzzle commands; old123 net bodies retire, new identities recreate, without solving/resetting a station or widening the native helper's accepted modes. A successful smaller case proves this specific allocator-reuse path only. It must not be renamed maximum-body acceptance or silently substituted for the366 case.

## Passive observations and honest history proof

Existing native `TryGetFusionStatistics` supplies object/general allocator TotalBlocks/TotalFreeBlocks and per-bucket allocated/free/used segment arrays. Record these at baseline, populated, low-water and recreated boundaries for both peers, plus actual object count, canonical tick, Runner tick, latest server tick, monotonic time, config and word-count histogram. Copy bounded arrays once per phase or at a slow fixed sampling cadence; no per-tick object enumeration or new phase/timing mutation. Require statistics availability; missing statistics is unavailable evidence, never zero memory.

The installed315-word body uses the1280-byte allocator bucket. `TotalBytesUsed` is segment-capacity high-water, not live usage, and the installed TotalBytesFree aggregation can be negative; neither establishes freeing. Meaningful reclamation requires the matching bucket's used-segment count to fall and free-segment count to rise after exact retired IDs disappear, then those free segments to be reused as new IDs populate. Allocated pages need not shrink: a retained page with reusable free segments is valid reuse. After an equivalent second cycle, require no unexplained monotonically growing live segment population. Retain raw counts and sampling/SDK-lag limits; do not demand an invented exact page decrement while other runner state is changing.

Sixty canonical ticks or60 seconds does **not** prove60 received object snapshots. `WordboundNetworkBody` has no always-changing canonical tick field; sleeping/unchanged objects may not produce60 changed updates. Do not call TryGetSnapshotsBuffers for telemetry or force dummy network writes. Moving fixtures provide real changing objects, but that alone cannot claim60-history population for every static prop/tile.

The pinned installed DLL exposes NetworkObject.Meta and NetworkObjectMeta._snapshots only internally; no public passive per-object history-count API was found in this inspection. Read-only Cecil inspection confirms the nested list's Count getter only reads its private `_count` field and returns it. If strict per-object 60-entry evidence is required, the smallest candidate is a separately reviewed, Editor-test-only passive metadata observer of the exact pinned fields/list count, preflighted before starting the run and failing closed if metadata differs. It must never call NextSnapshot, interpolation getters or mutate SDK state. That additional observer is **not implemented or approved by this report**. Without it, report only measured allocator plateau/churn and the source-proved 60-entry bound, not full history occupancy. Even with it, a lower observed count is not a 60-entry pass; a supported real action/fixture workload must populate it or the requirement stays open.

## Required scope decision and gates

1. Root completes the existing full FFA64MiB case first: actual scored finish, confirmed result/ACK, noOOM, clean teardown and memory trajectory. None is assumed here.
2. Root accepted the honest 512-budget/actual-live-count distinction and requested independent Input review before test-only implementation. Exact 512 live bodies still requires additional scope and feasibility proof; retain that acceptance item open.
3. If selected, implement only the existing Workshop partial's factory/coroutine and bounded test-only observation helper. Reuse the authenticated path unchanged; no broadening to TeamRace parties in this slice. Native source tests must prove the authored content resolves and full budget/initial state match before live allocation.
4. Root runs R0 and the exact new native method with existing external gameplay/workshop flags and profile flags. No jobs have run in this lane. Preserve failures rather than increasing memory/deadlines again.

Source basis: current integration `WordboundEntityBudget`, `ModeDefinition`, `CourseDocument`/`CourseValidation`/`CoursePhysicalSite`, `AuthoritativeLifecycle`/`AuthoritativePractice`/source minting, `WordboundLiveWorkshop`, `WordboundNativeWorkshopTests`, `WordboundNetworkBody`, and installed Fusion.Runtime metadata. Allocator mechanics and public API limits are retained in `FUSION-FFA-ALLOCATOR-REVIEW.md`; heap implementation is c9be66f4 + ca58302b. Original unrelated ModeDefinition/log work is untouched.

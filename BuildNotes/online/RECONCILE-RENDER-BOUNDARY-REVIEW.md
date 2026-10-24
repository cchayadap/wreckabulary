# Reconciliation versus render-frame work

STATUS: SOURCE REVIEW PASS — tuple-only implementation 766f212b independently reviewed by recover_input. No jobs.

Evidence is the exact PublicTeamRacePreservesWholeSoloParties output in root
TestResults/editmode_20261024T010750Z.xml, source95219ddd, with current root source
read for the physical/presentation boundaries. This case failed admitted client
movement; the audit must not claim online acceptance or a cause for that failure.

## Measured boundary

Whole-case main-thread ProfilerRecorder window was 36.777 s, with no buffer wrap:

| Marker | Count | Recorded total ms |
| --- | ---: | ---: |
| Reconcile | 556 | 5212.491 |
| Presentation (context and view dictionary only) | 556 | 284.324 |
| PhysicsApply (already validated snapshot application) | 556 | 521.893 |
| Capture | 556 | 514.187 |
| Advance | 555 | 3182.925 |

The independent Stopwatch ready window was 12.718925 s, canonical tick88→555,
467 HostSteps. It recorded FullValidation481/2260.283 ms,
MutableValidation467/577.418 ms, Prepare467/772.029 ms,
FrameChanged467/0.219 ms and HostStep467/7767.111 ms. Counts/windows differ from
the whole-case markers; scopes nest and are not additive. FullValidation includes
all invocations on the owner thread, not only Reconcile. Native test subscribers
are not the Windows HUD/artwork subscribers, so tiny native FrameChanged time
does not establish the cost of rendered client presentation.

The measured context/view work is about 5.45% of inclusive Reconcile time in this
case. Its marker does **not** cover GameplayBody.Configure/Apply, artwork callbacks,
renderer scans, or ApplyFrame. No dominant visual cost can be inferred from it.

## Why the physical step cannot be deferred

AfterSimulate captures physical poses and invokes HostStepCompleted synchronously.
Its appliedRevision fence requires the resulting canonical snapshot to be applied
before the next simulation step. Advance can change death/respawn/activity,
carrying/deployment/item mode, ownership, movement constraints, source generation,
fixtures and the body registry. ApplyValidatedSnapshot checks ownership/update
compatibility, applies those states and collision policy. Skipping that work until
Render could simulate stale geometry or permissions on the next catch-up tick.

Presentation configuration also sets authoritative replicated ShapeSize and the
native collider. Keep exact shape application synchronous, together with initial
visual construction and Configure/PresentationChanged callbacks. Current atomic
validation checks mutable catalogue/rules again after those callbacks; deferring
the callbacks would change that transaction boundary and its exceptions/reentry.
FrameChanged is also an observable per-tick event used by accepted native combat
proofs. It must not be coalesced wholesale into a rendered-frame notification.

## Narrow next proposal under review

GameplayBody.Apply is called from Configure, FixedUpdateNetwork and Render. On
every call it converts VisualKey to string, allocates/validates a new
WordboundBodyPresentation and enumerates VisualRoot.GetComponentsInChildren,
setting each renderer.enabled even if the tuple and activity are unchanged.
Its shape writes already use an applied-size guard, and artwork key/carrier
callbacks already have a change guard. WordboundGameplayPresentation.Present is
already a LateUpdate consumer; it is not currently invoked once per simulation
tick wholesale.

The smallest safe candidate is an exact raw NetworkString/Vector3/carrier tuple
cache for **successful scalar validation and string conversion only**. Read the
replicated tuple each call, validate a changed tuple before any state mutation,
and retain immediate Configure/shape/callback order. Do not mark failed tuples
valid. Do not skip Configure's authoritative field writes or collider checks on
size changes. Reset caches on pooled/spawn lifetime as required. This removes
repeat pure work without depending on frame pacing or caching mutable catalogue
validation. A live-body test must cover first apply, repeated tuple, key/carrier/
size changes, invalid change and recovery, immediate callback visibility,
callback exception/reentry, despawn/reuse and client replicated updates.

Renderer caching needs an additional ownership proof before implementation.
Presentation.Refresh can replace the world under a size/parent change without
PresentationChanged; ShowHeld can create/reparent/dispose held children under a
different carrier; Dress and visibility code can change enabled flags. A blind
cached array or last-frame flag misses those paths. Options are explicit owned
hierarchy invalidation at every replacement/reparent plus state restoration
boundaries, or initially a reusable GetComponentsInChildren list retaining every
scan/visit but removing result-array allocation. The latter is narrower but must
not be described as eliminating hierarchy traversal. Independent Input proposal
review requested before choosing the scope. The later approved checkpoint below
keeps all renderer scans and visits unchanged.

## Comparison requirements

Use real spawned bodies and the original callback/physics assertions. Exercise
multiple canonical ticks within one render frame, held-item transfer, death and
respawn, root/world replacement, appearance changes and stop/failed callback.
Keep authoritative shape/active/ownership state equal before every next simulate.
Retain the same public TeamSolo and Windows visible workloads, runtime/settings,
positive byte calibration where supported and full raw samples. Current native
markers cannot isolate GameplayBody.Apply, which runs at multiple sites; a bounded
opt-in scope is justified only if requested for attribution. No measured saving,
FPS result or online-stall fix is established by this source proposal.

## Approved implementation checkpoint

Root and Input agreed the tuple-only boundary. The implementation is limited to
`WordboundGameplayBody.cs` and one case in the existing
`WordboundNativeLocalGameplayTests.cs`. Both baseline files matched root aea79369
exactly (no baseline-copy commit). Their SHA256 values were respectively
`5405C7139DA2EC399AA46D6B899764E4509AABBCEEC3405C0EDCB172C0AE666A` and
`7BF6AC2AB325932169BEA276153975DAC09CA5EDF535D96026406733E77EF0CD`.

The cache holds a copied NetworkString, exact Vector3.Equals size, carrier, and
the resulting string from a successful scalar validation. Cache state is separate
from applied shape/key/carrier bookkeeping. A failed scalar validation cannot
mark its tuple valid; collider and callback failures retain the original retry
behavior because their checks/bookkeeping are unchanged. Spawned invalidates the
scalar cache for a fresh/reused component lifetime. No networked fields changed.
The existing hierarchy scan and renderer visit still occur on every Apply;
renderer replacement and external visibility correction therefore remain live.

Root authorized an internal successful-validation counter under UNITY_EDITOR
only, with a matching guarded friend attribute for the existing offline native
test assembly. This has no public runtime API or player telemetry. It proves
the repeated tuple takes the cached branch; it is not a byte-allocation measure.

The single new case
`Wreckabulary.OfflineGameplayTests.WordboundNativeLocalGameplayTests.PresentationTupleReusePreservesImmediateShapeCallbacksAndVisibility`
returns the existing Run iterator directly. It uses actual isolated spawned
prefabs and host field writes, plus immediate scalar/collider/callback readbacks.
Repeated Configure/FixedUpdateNetwork/Render calls retain current active state and
discover a late nested renderer. Exact next-float size, key, carrier, return to a
prior tuple, callback failure/reentry and repeat invalid capsule shape all have
behavior and counter assertions. A despawn/new spawn begins without a prior tuple
validation. Existing live controller bodies are never rebound or modified; the
secondary world and spawned bodies are disposed before any yield, followed by
observed progress of the original controller.

Remaining limits: this local case does not inject malformed replicated wire data,
prove a remote replication rollback, or force the SDK to reuse one pooled instance.
Those are source-contract/lifetime claims until separately exercised. Changed
tuples still take the original validation; no overall validation skip is implied.
The complete local fixture now has 11 cases, all requiring WORDBOUND_LOCAL_GAMEPLAY=1.
R0, native case/full local fixture and measured Windows/public comparisons remain
root-owned and NOT RUN by this lane.

Input's final source review confirmed both exact baseline Git blobs, publish-after-
successful-validation cache semantics, retained collider retry and callback
exception/reentry ordering, unconditional remaining renderer path and the native
counter trace. It checked clone ownership/unregistration and cleanup before any
yield, plus the guarded friend/counter assembly boundary. No blocking source
finding remained. Compiler, actual native behavior and measured savings remain
unverified; the review does not replace those gates.

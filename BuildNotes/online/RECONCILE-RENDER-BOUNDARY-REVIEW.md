# Reconciliation versus render-frame work

STATUS: IN PROGRESS — source-only audit and proposal. No production edit or job.

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

The measured context/view work is about5.45% of inclusive Reconcile time in this
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
review requested before choosing the scope; no such source change yet.

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

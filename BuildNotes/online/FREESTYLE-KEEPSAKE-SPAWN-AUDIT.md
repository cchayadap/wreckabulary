# Freestyle keepsake placement audit

STATUS: READ-ONLY AUDIT COMPLETE — placement defect is source-confirmed; the
reported player lift's cause remains pending native evidence. No runtime edits or
jobs. Implementation and compatibility decisions remain with root.

Root reports initial player artwork height 2.5486 at canonical tick 40 and
CollectLetter OutOfReach at tick 61 in Windows equipment run 005453. Initial
player/keepsake overlap is a source-supported hypothesis, not yet a proven cause.
Assets owns the separate native/canonical/visual observation. This audit examines
existing deterministic placement and recovery helpers, compatibility and smallest
bounded options; root selects any implementation after evidence review.

Scope: integration source inspected at `ee3b09582bbf6a034d359d2e055ca5a0332fe84c`.
Existing lane ModeDefinition/ProtocolV3 and log work is preserved.

## Confirmed paths

- `AuthoritativeMatch` adds every player at `Spawn(i)`; the Freestyle
  `InitializePractice` then adds active, dynamic `keepsake:0` at exactly
  `Spawn(0)` (`AuthoritativePractice.cs:12–16`). Both roots have the same floor Y.
  The keepsake's box is 0.45 m cubed, with centre at half-height; player geometry
  is a 0.6 m diameter, 1.8 m capsule centred 0.9 m above its root
  (`WordboundGameplayPlan`, `WordboundGameplayBody`). Their volumes overlap.
  `WordboundPhysicsPolicy.ShouldCollide` permits this same-team pair. This
  establishes invalid initial separation, not the magnitude/direction of PhysX
  resolution or the cause of the observed artwork height.
- `PracticeRecoveryPosition` returns `Spawn(0)` when no site is selected. Even a
  selected site falls back there if its module has no recovery anchor. With an
  anchor it takes the first ordinal ID in that module without clearance checks.
  `UpdateKeepsakes` uses this only when the canonical recovery generation changes;
  normal active poses are retained. The existing drop recovery delay is 10 seconds.
- Initial practice setup precedes map-prop, resource and fixture body creation.
  Checking only the constructor's current body dictionary would miss authored
  obstacles. Immutable resolved placement/fixture metadata must also be used.
- Freestyle is one shared team with up to eight players. Fixing player 0 alone
  can overlap another start, a later occupant or a player standing at the chosen
  recovery position. Race keepsakes and player spawn/respawn positions are separate
  behavior and need not change for this defect.

## Existing mechanisms and their limits

| Mechanism | Useful property | Why it is not a ready-made fix |
| --- | --- | --- |
| `CourseResolver.Shape/Fits` | Integer-mm, quarter-turn placement bounds; reserves starts, anchors, supplies and connection corridors against map props | Requires a zone/asset placement; these reservations are not a generic keepsake support/occupancy query. Moving fixtures are separate. |
| Authored recovery anchor | Existing editable, deterministic location; resolved map props avoid its radius | Anchor validation permits overlap with starts or other anchors. Location validation tests module bounds/floor elevation, not actual floor openings. Anchors can be absent. |
| `WordboundReleasePolicy.Candidates` + `PhysicsWorld.TryRelease` | Derives candidate distance from actual collider dimensions and performs native overlap/cast checks | `TryRelease` requires an admitted active player, a held/carried Item and a composed physics world. A keepsake during constructor initialization meets none of those conditions. Candidates alone are not a safety proof. |
| `PhysicsWorld.CanDeploy` | Checks overlap and support at footprint corners | Requires live actor/item deployment state; it is not an engine-free constructor API. |

`CourseGeometry` requires starts 1 m apart and within module bounds (500 mm X/Z
margin), but has no anchor/start cross-check. `WordboundCourseWorld.BuildFloor`
subtracts openings for stairs/ramps across layers; a centre inside a module's
bounding box can be unsupported. Its headroom is 2.4 m and floor thickness 0.24 m.
An algorithm that checks module bounds only is insufficient.

The flagship already supplies an anchor at each room centre, including the starting
room. The ordinary templates also have `StartSafe`. Using the starting room anchor
would remove the exact overlap on these layouts if its actual clearance/support
passes. It cannot be the complete old-course policy. Also, reserved Path volumes
and recovery anchors intentionally overlap in the flagship: reserved volumes are
authoring constraints, not automatically colliders to reject indiscriminately.

## Smallest robust option to take forward

Use a single internal, bounded, deterministic Rules-side selector for initial
Freestyle keepsake placement and the no-selected-site recovery fallback. Select a
position only at those existing creation/generation events. Do not move the player,
alter collision permissions, auto-carry the keepsake, or periodically relocate it.

1. Work in the first start's module and floor/layer initially. Consider eligible
   authored recovery anchors in a fixed order, then a bounded integer-mm search
   around the start/anchor regions. Derive candidate spacing and exclusions from
   the actual capsule and keepsake footprint plus an explicit contact margin;
   a fixed direction/offset without validation is not a fallback. Sort all ties
   deterministically; do not consume gameplay RNG or query nondeterministic physics.
2. Require the whole upright keepsake volume inside the room, with supported floor
   under its footprint. Reuse/share the runtime floor-cut geometry if practical;
   a smaller conservative alternative is to reject any candidate intersecting a
   stair/ramp opening envelope derived from the actual connection/headroom policy.
   That alternative may reject usable floor, so its no-candidate behavior must be
   explicit. Keep full Y overlap tests so unrelated floors do not block each other.
3. Reject overlap with all relevant declared start/respawn capsule volumes, not
   just occupied player 0. At recovery, additionally check active canonical player
   and other collidable body volumes in that same state, excluding the old keepsake
   itself. Use resolved props with their catalogue dimensions/yaw and the immutable
   moving-fixture swept envelopes (`From`, `To`, `SizeMm`); do not assume fixtures
   passed the ordinary map-placement reservation test. Account for connection
   platforms and future source spawn regions where applicable. This work occurs
   at rare placement events, not every tick.
4. Keep recovery selection state-driven: identical course, roster and canonical
   occupancy produce the same point. Do not silently use an overlapping point when
   no candidate exists. Report an explicit preflight failure for a new match. For
   an already-running recovery, the unavailable-placement policy needs a deliberate
   decision and regression before implementation; throwing inside Advance causes
   rollback, and an unbounded retry could halt progression. Do not invent a retry
   delay or alter the existing recovery deadline as part of a geometric fix.

This is the smallest robust *type* of correction; a two-line anchor substitution
cannot meet the general requirements. Prefer an initial implementation bounded to
the supported starting module, with a clear preflight failure on unsupported old
geometry, over claiming every old accepted course has safe free space. Current
course validation does not prove that stronger property.

The existing selected-site anchor path has the same potential overlap defect.
Reusing the same placement predicate around its chosen module is a natural later
extension, but changes selected-site recovery behavior and should be included
explicitly if root extends this initial/no-site scope. A selected site with no
anchor must not keep the unsafe Spawn(0) fallback accidentally.

## Compatibility and recovery constraints

- Preserve authored documents, catalogue bytes and resolved manifest hashes. Do
  not hand-edit current/default courses to hide the defect, or require new anchor
  fields merely to open an old workshop course. An old course can remain readable
  while a new-match preflight explicitly rejects the absence of supported space.
- Position selection is deterministic gameplay behavior. `ModeDefinition` folds
  `WordboundPracticePolicy.Hash` into Freestyle's mode hash; the authored puzzle
  plan also includes that hash for Explore. The current policy is version 1. An
  explicit versioned policy/release decision is safer than old and new executables
  claiming the same identity while producing different initial/recovery states.
  This would require coordinated acceptance/content handling; no service/hash
  change is authorized by this report. Input independently confirmed this boundary.
- Do not recompute a location on snapshot import, epoch migration, rollback or
  an ordinary tick. `RestoreState` restores the exact saved bodies/practice state;
  `RestoreCommitted` validates the durable state/hash and next epoch. Preserve
  those poses and marks, including an old snapshot's historical overlap.
- The controller constructs a new authority before restoring committed state.
  Therefore a newly added constructor preflight must be tested on the restore
  route too: rejecting a legacy course before its saved state can be imported is
  a real compatibility consequence, not fixed merely by leaving RestoreState alone.
- A new geometric contract should share or assert the collider dimensions used
  by presentation/physics. Duplicating unexplained .3/.45 constants in another
  policy would allow later shape edits to invalidate its separation guarantee.

## Minimum verification before acceptance

- Pure fixtures: original default/template and flagship; missing anchors;
  anchor equal to a start; all eight starts occupied/reserved; close boundaries;
  upper-layer stair opening; rotated prop and moving-fixture envelope; busy recovery
  point; no safe candidate; exact deterministic replay with reordered equivalent
  authoring input. Verify full snapshot validation, unchanged source/manifest
  bytes, ownership and body count.
- Canonical lifecycle: initial separation; take/drop and unchanged 600-tick
  recovery; recovery when another player occupies the preferred point; selected
  site and absent-anchor fallback; exact rollback and committed restore retain
  earlier saved poses/bytes. Never claim non-overlap for arbitrary imported poses.
- Native/local: actual capsule/keepsake collider geometry, canonical and Rigidbody
  positions at startup and recovery, no overlap after composition, player root
  remains supported through real simulation, and a real nearby letter collection
  succeeds under unchanged reach/LOS. Artwork-root height is supplementary evidence.
- Run the original Windows scenario against the same authored inputs and record
  the new native/canonical/visual observations. No CPU/FPS, network or general
  recovery acceptance follows from this source audit.

Next recoverable action: root selects policy scope and failure/compatibility
behavior after Assets' actual observation. No production patch is prepared here.

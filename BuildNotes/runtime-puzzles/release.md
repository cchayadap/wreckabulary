# Safe voluntary item release

STATUS: SOURCE VERIFIED; NATIVE PHYSICS PENDING

Baseline: `a03df26`. Root authorized bounded safe Drop/Throw release geometry in the current Rules, PhysicsWorld and Gameplay query-relay ownership. No wire, schema or command changes. Do not alter forced recovery/death/return drops in this slice.

Confirmed problem: voluntary Drop/Throw currently releases a full-sized item at the player's feet, inside the player capsule. Source assets use full root BoxColliders even while their held artwork is miniature.

Contract: an optional trusted `IWordboundReleaseQuery` receives canonical actor/item state and a throwing flag, and proposes a root position from registered host geometry. Authority calls it before changing the economy; absent/blocked geometry returns `BlockedRelease` with inventory and selection intact. No client position is accepted.

Physics policy: full actual source box and player capsule, host-facing orientation, deterministic forward/side candidates for drops and forward only for throws. Query initial and final overlap plus the swept volume using the isolated runner PhysicsScene and owned colliders. Ignore the actor only while leaving the initial holding volume; final geometry must clear the actor. Saturated or invalid queries refuse release.

Implemented the optional query, Gameplay relay, deterministic candidate policy and isolated physics clearance. Drops try forward, then +/-60 degrees; throws use only forward. The root position clears the actual full-size item footprint plus the player radius and a 0.15m gap, with a 0.015m overlap/sweep skin. Drop root height is feet +0.08m; throw root height is feet +0.75m. The returned pose uses the host's facing and preserves the existing throw speed of 8m/s. Clearance uses the released item's collision policy, including its team/protected metadata, rather than player-to-player collision policy.

No floor is required: release over an edge uses ordinary gravity/recovery. Blocked initial volumes, destination overlaps, swept obstacles or saturated query buffers refuse release. Root BoxCollider/CapsuleCollider dimensions, centers, unit scale and upright actor requirements prevent mismatches with the proof prefabs. Missing release capability refuses the command even after migration. Forced elimination/return/recovery drops retain their existing behavior.

Verification:

- Real NUnit affected authority/snapshot/combat/deployment/puzzle/field/release suite: 193/193 passed, including 13 new release cases; TRX `TestResults/runtime-puzzles/release.trx`.
- Rules netstandard build: 0 warnings/errors.
- Installed Unity 6000.6.3 / Fusion SDK reference compile after final collision-policy fix: 0 warnings/errors, 5.56s.
- `git diff --check` passed.

Native tests NOT RUN in this lane. Root filters for the new source tests: `Wreckabulary.Tests.WordboundReleaseTests` (8 cases), `Wreckabulary.Tests.WordboundReleasePolicyTests` (5 cases). Native acceptance still needs actual registered prefab releases near walls/ceilings, side fallback, rotated large furniture, another player at the destination, and isolated-runner exclusion. The rule fixture supplies a trusted query test double; its passing tests do not prove engine overlap/sweep behavior.

Next recoverable action: integrate this exact checkpoint after `a03df26`, let Unity generate new script metadata, and run the native clearance scenarios. No command wire or snapshot schema changes are needed.

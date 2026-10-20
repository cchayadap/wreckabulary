# Wordbound physics adapter review

STATUS: COMPLETE — SOURCE ONLY; NATIVE PHYSICS NOT RUN

Scope: source-only independent inspection of the online physics policy, network body and world adapter in `wordbound-puzzle-race`, requested by their implementation owner while root's native compile is active. Focus canonical capture/restore, runner-local queries, movement authority, inventory activity/collision transitions and migration of inactive seats. No Unity, compilation, production edits or provider operations.

The inspected source was subsequently frozen as `9b7486ab3eaa8ca38b56bb6a5c7608037bf8f4ba`; the owner confirmed no further Physics edits while implementing the separate composition layer. Native physics behavior and migration are not established by source inspection.

| Source | SHA-256 |
| --- | --- |
| `WordboundPhysicsWorld.cs` | `74DB2B68660E2689B82ED9689C2C19292ACFEF5B24B52C22AAEDF0D019BF9009` |
| `WordboundNetworkBody.cs` | `CD4E0FCF6B7E33098F0671033C499746B22EF9F942857F099ADCB7053346175F` |
| `WordboundPhysicsPolicy.cs` | `72986C4C336D03E38C5BCE04C9A311B2A1D83EF618D767EBDFD5B40BE8922EF6` |

No confirmed new defect found in the following bounded coverage:

| Boundary | Source coverage and practical limit |
| --- | --- |
| Canonical identity and movement | Player body suffix/source/team binds the sorted canonical seat. Active registration requires matching admitted input authority; loose objects cannot acquire player input authority. Every movement tick rechecks admission, domain activity/health/stun/craft state and current epoch. Non-finite, oversized and old-epoch axes reject; missing input uses neutral movement. No live unauthorized-input experiment was run. |
| Capture/restore | Canonical immutable values survive frozen/unsimulated capture. Actual native sampling begins after physics. Identity/kind and bounded poses are validated; registered actor identity cannot change. Full snapshot validation and per-item mode preflight precede application; same-epoch rewinds reject. The error callback freezes bodies and requires application of a resulting frame. Native engine setter exceptions and durable migration are not exercised here. |
| Inventory modes | Held/carried bodies require inactive/kinematic; deployed active/kinematic; thrown/world active/dynamic. Host restoration changes collision/render visibility before subsequent simulation. The unchanged-pose branch only skips pose/Teleport writes: it still applies activity, kinematic state and dynamic velocity, so equal-pose Held→Thrown or Held→Deployed transitions are not skipped. Client `Render` applies replicated `BodyActive`; remote visual/collision timing needs native checks. |
| Isolation and queries | Constructor requires the runner's non-default valid 3D scene; body/static registrations reject another scene/runner. Ray, sphere and overlap queries use that scene, registered colliders and mode-specific collision policy. Full result buffers fail closed. Checked actor/self/target filtering, near-feet ground support, footprint overlap and support corners, and optional trusted puzzle callback. Actual slopes/doorways/contacts are untested here. |
| Collision updates | Active/team/protection changes invalidate collision pairs; pair filtering preserves clean-race team separation and protected rival resources. Host colliders and Rigidbody detection follow canonical activity. No global layer matrix mutation is introduced. Collision quality, cost at 512 bodies and native ignore-pair restoration remain runtime concerns. |
| Inactive-seat recovery | Inactive canonical players may bind with `PlayerRef.None`; active players may not. The composition layer must assign the admitted peer before reactivation and restore absent seats as inactive. No duplicate/rejoin/native migration proof is claimed. |

Installed Fusion source was checked directly: `RunnerSimulatePhysics` has execution order 1000 and calls Before/After events around scene simulation; `NetworkRigidbody.SetupPhysicsBody` enables `Update3DPhysicsScene` when binding a Rigidbody. This refutes the initial concern that normal registered bodies would leave the simulator's default false flag unchanged. `NetworkRigidbody.Teleport` updates physics and network transform state immediately; the adapter does not combine it with NetworkTransform.

One explicit composition invariant was checked with the owner: `ApplySnapshot` rejects registered bodies missing from the snapshot but permits snapshot bodies absent from the registration map; `CaptureHost` returns the registered set only. Therefore the caller must reconcile the complete canonical ID set before applying each frame. The owner confirmed this contract and is enforcing exact IDs in the new composition layer. This report does not claim that enforcement already exists in Physics itself.

Owner evidence records actual Unity/Fusion reference compilation and focused real NUnit policy 22/22. The reviewer did not rerun those checks or launch Unity during the host compile hold. Native tests must establish body spawning/weaving, remote visibility, admission/epoch input, collision transitions, independent runner scenes, exact frozen state, current-epoch rejoin and restored cloud migration. Next recoverable action: review the composition's exact-body reconciliation and consume root's native results before asserting end-to-end gameplay.

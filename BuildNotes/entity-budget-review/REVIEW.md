# Stage 2 entity budget review

STATUS: IN PROGRESS

Read-only review requested by root while native verification holds compiler access. Scope: James's uncommitted `WordboundEntityBudget`, `AuthoritativeLifecycle`, `AuthoritativeMatch`, `MatchStateValidation` and focused tests. The review targets admission bypasses, letter conservation, critical recovery reserves, ownership accounting and snapshot consistency.

No source edits in the reviewed lane, compilation, Unity execution or service actions are authorized by this review. Findings below will identify exact inspected source and distinguish static reasoning from executed reproduction.

Reviewed draft baseline: `5ac35ec`, with the four uncommitted Stage 2 source paths frozen by James for his first focused gate. No compiler or Unity process was launched for this review.

## B1 — imported state can consume the recovery reservation

Status: source counterexample sent to owner; executable reproduction pending in the owner's focused suite.

`MatchStateValidation.ValidateBodyBudget` accepts dynamic usage up to `PoolBodies`. `AuthoritativeLifecycle.EnforceBodyBudget` instead requires noncritical, nonauxiliary usage at most `PoolBodies - CriticalRecoveryBodies - PhysicalAuxiliaryBodies` and throws if the excess cannot be recycled. These are different admission invariants.

A conserved, rehashed race snapshot can populate the gap with crafted, active, nonsleeping Thrown BED bodies owned by a valid creator/thrower. BED does not require the BALL flight ledger. If the total remains inside `PoolBodies`, the new snapshot budget check accepts it, yet the next tick cannot recycle those airborne bodies and throws when enforcing the reserved core limit. This is a static counterexample; this review has not executed the complete import.

Requested regression: construct the conserved pinned-body state inside the gross pool limit but beyond the reserved core, import it through the real codec, and require rejection. Align snapshot validation with the complete live reserved-budget invariant.

## Inspected boundaries

- Critical recovery reserve enumerates partial completion within checkpoints and uses a conservative fallback beyond four simultaneous puzzles.
- Optional source deferral keeps the generation unchanged; successful refill advances generation only after capacity admission.
- Loose retirement increments `Spent` by exactly the retired tile or locked item letters. Existing whole-tick rollback covers those changes when a later command fails.
- Stable recycling excludes held, deployed, airborne and referenced combat/puzzle evidence.
- Craft admission includes pending craft reservations and assigns held/deployed items to the current holder/deployer's team.
- An additional useful test is a Brawl Race rival pickup/redeploy/drop near both pool limits: holder/deployer charging and immutable creator ownership intentionally differ.

No additional concrete conservation failure was found in the inspected source. Runtime, native physics and persistence are outside this read-only review.

## Captured source hashes

| File | SHA-256 |
| --- | --- |
| WordboundEntityBudget.cs | 976DB9E211010492A71651BA9FFCBA8070EC4521B41190AA6BD31897CF50B3C8 |
| AuthoritativeLifecycle.cs | 21C290B178C0FDE03B2FF67A86215BA4FB75DF07F2A4EE66073AAB5023FA7FA4 |
| AuthoritativeMatch.cs | B4DFE7FAB73B0BE96B3F7C27FB058D94EE815D0F61AB72680CC673FE38069566 |
| MatchStateValidation.cs | 77121914684E2780D4F7FE57F1389FAE5B76DC99365FB82542E14B87ED4037D6 |

Next action: owner reproduces or disproves B1 and freezes the repair; re-read the narrow change and record disposition. Workshop route verification remains queued after the owner's current compiler slot.

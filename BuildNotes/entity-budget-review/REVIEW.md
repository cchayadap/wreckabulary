# Stage 2 entity budget review

STATUS: COMPLETE — B1 REPAIRED; SOURCE REVIEW AND OWNER'S FOCUSED REGRESSION

Read-only review requested by root while native verification holds compiler access. Scope: James's uncommitted `WordboundEntityBudget`, `AuthoritativeLifecycle`, `AuthoritativeMatch`, `MatchStateValidation` and focused tests. The review targets admission bypasses, letter conservation, critical recovery reserves, ownership accounting and snapshot consistency.

No source edits in the reviewed lane, compilation, Unity execution or service actions are authorized by this review. Findings below will identify exact inspected source and distinguish static reasoning from executed reproduction.

Reviewed draft baseline: `5ac35ec`, with the four uncommitted Stage 2 source paths frozen by James for his first focused gate. No compiler or Unity process was launched for this review.

## B1 — imported state can consume the recovery reservation

Status: owner confirmed the mismatch, applied a narrow repair and reported the targeted codec regression PASS.

`MatchStateValidation.ValidateBodyBudget` accepts dynamic usage up to `PoolBodies`. `AuthoritativeLifecycle.EnforceBodyBudget` instead requires noncritical, nonauxiliary usage at most `PoolBodies - CriticalRecoveryBodies - PhysicalAuxiliaryBodies` and throws if the excess cannot be recycled. These are different admission invariants.

A conserved, rehashed race snapshot can populate the gap with crafted, active, nonsleeping Thrown BED bodies owned by a valid creator/thrower. BED does not require the BALL flight ledger. If the total remains inside `PoolBodies`, the new snapshot budget check accepts it, yet the next tick cannot recycle those airborne bodies and throws when enforcing the reserved core limit. This is a static counterexample; this review has not executed the complete import.

Requested regression: construct the conserved pinned-body state inside the gross pool limit but beyond the reserved core, import it through the real codec, and require rejection. Align snapshot validation with the complete live reserved-budget invariant.

Repair reinspection: the validator now derives the same critical/auxiliary reservation from `WordboundEntityBudget`, calls the shared auxiliary-body classifier, and checks both complete core usage and pinned usage plus pending crafts. `RehashedPinnedThrowsCannotOccupyReservedRecoveryCapacity` exports the full conserved forged state, confirms it fits the gross pool, and expects the specific reserved-recovery rejection through the real codec. A subsequent live tick is also checked. James reported the corrected one-case regression PASS, plus 211 other focused cases in the preceding run and a zero-warning production Rules build. This review did not execute those checks; independent source inspection confirms that the tested guard matches B1.

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

Repair source hashes:

| File | SHA-256 |
| --- | --- |
| WordboundEntityBudget.cs | B64A78FE1DC3997D2FB0A7FFB4FF69334F0159DEE230ABEE8E3CF01AEFD97D01 |
| AuthoritativeLifecycle.cs | 4312E5163A453AC89051C93B068A58F3B30C5592E9BBA5D470DE868FB4D2D3E7 |
| MatchStateValidation.cs | 861F4FAF888B824E5AE7608169E3E1C696945F43E202A131FBD17993FA821F7E |
| WordboundEntityBudgetTests.cs | 5913F7EAC529378E7083B662EFD72678917EF2D6CE69F861012A3E8BA677D395 |

Disposition: no remaining confirmed defect in this bounded review. The owner still has separate-assembly and flagship acceptance checks queued, and root owns native integration. Next action in this lane is the already prepared route/spawn focused and reference checks after the compiler slot releases.

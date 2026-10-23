# Public FFA collection fixture review

STATUS: BOUNDED SOURCE PASS for online checkpoint `1ecec6b6`; exclude baseline-only `9674b5fe`. No source edits or native jobs were performed for this review.

Reviewed the exact `WordboundNativeFullMatchTests.cs` delta, existing `FullWalk`/`Command`/`Until`, actual `CourseTemplates.CombatArena` geometry/supplies, production `AuthoritativeMatch.Reachable` and `WordboundPhysicsWorld.HasLineOfSight`, plus the retained 225207 XML failure. No concrete API, iterator, ownership or deadline blocker was found.

The actual Start room is 20×20 m, its starts have Z=-8 m, and the first two kit rows span Z=-6.5…-5.5 m. Parking at the victim's bound Start minus0.75 m in Z therefore uses Z=-8.75 m, inside the floor and over2 m south of the rows. The new bounds and post-movement canonical/native position checks are consistent with the authored room and0.3 m player radius. Movement uses the existing admitted scripted input and friction-settling path; the later return to the recovery point is unchanged.

Each collection now queries real host canonical/native range and line of sight, activity, tile kind and mode ownership immediately before submitting. These are the actual production query/ownership semantics; no collider is ignored or moved directly. Submission remains once, with the same15-second frontier wait and exact payload-hash/Ok receipt assertions. Failure diagnostics contain numeric facts rather than credentials or identities; a diagnostic observation failure cannot replace the rejected-receipt assertion. No transport, course, combat, protection, recovery or completion rule changed.

The prior second-letter `OutOfReach` failure is retained. The reported other-player geometry is consistent with occlusion, but no rejected-time LOS ray result was recorded, so source review cannot establish that as the sole cause. This repair remains pending R0/native execution of `TwoAuthenticatedPublicFfaIndividualsReachScoredWinAndConfirmDurableResult`, including its existing strict durable terminal-result and cleanup checks. It is not a full FFA pass, human traversal proof, allocator plateau or production acceptance.

# Public FFA collection handoff

STATUS: SOURCE REVIEW PASS; native execution pending. Source-only repair of the existing full FFA fixture. No production reach/collision rules, input speeds, deadlines, SDK, heap or service changed.

## Retained failure

Root source66a0546d, native225207: TeamRace Solo and BrawlRace Solo passed; full public FFA failed75.282600 seconds into its test at the second CollectLetter receipt, Expected Ok / actual OutOfReach (`WordboundNativeInteractionTests.cs:139`). The completed run did not establish full FFA64MiB acceptance. Its short pre-combat memory samples had no OOM, but cannot establish a long-run plateau or scored finish.

At the first collection, host tick645→662, actor position was approximately(-6.632,0,-6.669) and the command succeeded. Before the second collection, FullWalk completed immediately with zero new steering samples: tick662, actor(-6.631721,0,-6.669300), target(-7.602,0,-6.493), horizontal distance0.985900m inside the fixture's1.1m approach radius. The remote actor was parked approximately(-7.175,0,-6.711), between the host and second letter. The nearest point on that logged host-target segment is approximately0.14m from the remote's centre, inside its0.30m player radius. This is geometric evidence consistent with occlusion, not an observed raycast result at the rejected receipt.

Production Reachable requires matching active body kind, ownership, distance≤3m and actual PhysicsWorld.HasLineOfSight. The query treats the other FFA player as a collider; it only excludes the acting body and the exact target body. FullWalk establishes distance and ordinary friction settling, not line of sight. The former fixture let the remote remain in the collection lanes after the generic startup movement, and it did not check the real collection precondition. No evidence implicates admission, an allocator OOM, or an incorrect production reach threshold in this particular failure.

Raw evidence remains root `TestResults/editmode_20261023T225207Z.xml` and `Logs/gate_test_editmode_20261023T225207Z.log`, specifically the two `WORDBOUND_COMPACT_WALK` and `Apply admitted CollectLetter` observations. The failed run is not relabeled as passed.

## Bounded repair

Before collecting either hammer kit, move the remote through existing FullWalk/admitted movement to a point0.75m south of its bound Start. Verify that location is inside the actual Start module, at least2m south of the authored kit rows, and the observed canonical/native player position settles within0.25m. The later existing movement back to the real recovery Start before combat remains unchanged. No teleport or state mutation is introduced.

Assets independently source-reviewed commit1ecec6b6 in report022812e7 (`BuildNotes/client-view/PUBLIC-FFA-COLLECTION-SOURCE-REVIEW.md`). It verified actual CombatArena Start geometry, clear waiting location, canonical/native reach and LOS queries, and unchanged one-submit/15-second/strict-Ok contract. No source blocker was found. Baseline9674b5fe is excluded from integration. This verdict does not establish the failed receipt's exact raycast cause or a completed native full-scored FFA.

Each fresh collection now checks actual host canonical and native distance≤3m, both actual host LOS queries, active actor/tile, tile kind and mode ownership. Then it uses the same Submit→frontier wait15s→exact payload receipt→Ok assertion. It submits once; it does not retry, replay, ignore OutOfReach, or loosen any gameplay condition. A failed precondition or non-Ok receipt records numeric positions, distances, LOS flags, activity, ownership, speeds and canonical tick without credentials or player/body identities. Failure-time diagnostics are best effort and cannot replace the original receipt assertion.

Only `Assets/_Project/Tests/OnlineGameplay/WordboundNativeFullMatchTests.cs` and this report change. Baseline-only9674b5fe copies root's existing22-line memory observations and must be excluded from integration. The test method/count remains unchanged: `TwoAuthenticatedPublicFfaIndividualsReachScoredWinAndConfirmDurableResult`. Root must run R0 and this same live public case with its existing flags, lifetime and memory observation before any acceptance claim. Capacity work remains checkpointed separately in1ed4a890 +6302cca0.

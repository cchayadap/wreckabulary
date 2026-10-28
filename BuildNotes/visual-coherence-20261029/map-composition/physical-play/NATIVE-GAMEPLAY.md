# Parcel Service native gameplay evidence

STATUS: COMPLETE FOR THESE LOCAL GAMEPLAY CHECKS — TimeRun and final Freestyle passed, with zero reported cleanup failures. This receipt covers bounded local gameplay verification, not overall game completion or visual acceptance. All native execution was performed by the root lane; this lane reviewed the actual XML and log output.

| Evidence | Actual result |
| --- | --- |
| [TimeRun case, native XML](../../../../TestResults/editmode_20261028T195312Z.xml) | `TimeRunCompletesAllThreeLapsThroughRealCraftsSocketsAndCarrierDeliveries` passed in 168.008869 seconds. The containing run is mixed 1 passed / 1 failed because its earlier Freestyle case failed; the entire run is not labelled a pass. |
| [Release regression XML](../../../../TestResults/editmode_20261028T201629Z.xml) | 11 passed / 0 failed. Includes the actual inactive-collider lifecycle, unchanged drop/throw candidate policy, body motion and thrown-contact boundaries. |
| [Final Freestyle XML](../../../../TestResults/editmode_20261028T202225Z.xml) | 1 passed / 0 failed, XML run duration 178.160915 seconds (case 178.065682 seconds). All six physical sites completed; map-origin table rejected; cleanup failures=0. |

TimeRun's successful case verifies three actual laps, six normally crafted/deployed/physically held/consumed objects, thirty collected recovery letters, twelve ordered objectives, original meshes, real collider support and a naturally confirmed Win with the keepsake carrier at each finish. Input is scripted through the admitted motor and authority commands; no position, solved or result flags are injected.

Final Freestyle verifies the same real resource/craft/deploy/use flow at all six authored sites, selected through the production nearest-unfinished-station selector. Actual completion order was SortingBench laps 3, 2, 1 (TABLE items 22, 23, 24), DispatchStand laps 3, 1 (STOOL items 25, 26), then ReturnClock lap 2 (CLOCK item 27). Every socket log reports physical=true and consumed=true. The test collected thirty real letters and required no race progress or terminal result. Its map-origin TABLE was normally carried and dropped at the socket, remained incomplete throughout a full hold, returned WrongPuzzleItem, and was restored away before the crafted solution.

The earlier [Freestyle release diagnostic](../../../../TestResults/editmode_20261028T201013Z.xml) established the disabled carried collider's missing engine attachment. The guarded production correction retains exact registered root-body ownership and all clearance sweeps. The next [Freestyle attempt](../../../../TestResults/editmode_20261028T201754Z.xml) successfully dropped the original table at its socket, held it through the rejection period, received WrongPuzzleItem, picked it up and returned it. That attempt then failed on a fixture-only supply owner assumption; its full six-station proof was incomplete. Root's correction on top of revision2 selects expectedTeam from resolved Play.TeamCount (0 for a scoped local team, -1 for shared Freestyle) and asserts every collected tile has that exact owner. No gameplay rules or completion assertions were removed.

Stable evidence hashes:

- `editmode_20261028T195312Z.xml`: `c2bd25866c6464b0612875f5f4022f0668bf5c5a0004276186f8f9f37520d4d4`
- `editmode_20261028T201629Z.xml`: `382deec75104842a5d436ab1ac1a5ee34b3e2763719ca84f2ee33854db23184e`
- `editmode_20261028T202225Z.xml`: `6f20d7cb65e3305ea9fa30d83cd5848b4ce280aa9116d721468a8b92ed9adbf5`
- Final reviewed `WordboundNativeParcelServiceTests.cs`: `ce377e10e263d07269c379817c3c096b18cdcb45955a3aca2871fff74f86e089`
- Guarded `WordboundPhysicsWorld.cs`: `9bee17321fbe558487e17faf38378bb817b25f1cba22b540b6782fc74dacdc48`

The TimeRun case preceded the carried-release correction; the later focused release and Freestyle runs exercise that changed Drop path. These are separate native checkpoints, not a claim that one final aggregate run executed every case. Root retained the earlier evidence and selected the bounded reruns.

Limits: one installed seed and saved course, local SDK simulation, scripted movement/commands. Human mouse/controller usability, broad seed coverage, additional public multiplayer modes, matchmaking, final Windows delivery, performance and owner approval of artwork are separate evidence. Earlier failed receipts remain intact.

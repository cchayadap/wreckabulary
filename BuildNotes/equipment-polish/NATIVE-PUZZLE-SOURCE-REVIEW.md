# Six-family native puzzle source review

STATUS: SOURCE AUDIT COMPLETE — bounded fixture repair prepared; native execution NOT RUN

Read-only review of root native PhysicalPuzzles flows and targeted prior failure evidence. No Unity, compiler, native run or production/test edits. Goal: identify concrete gameplay softlocks or misleading test failures before the next native run. Findings distinguish source evidence from execution still NOT RUN in this lane.

## Confirmed evidence so far

- Historical `TestResults/editmode_20261020T175216Z.xml` records the first TakeKeepsake receipt as OutOfReach (80.6 s case). It has no coordinates or LOS detail and proves no family completion.
- Historical `TestResults/editmode_20261020T184007Z.xml` records the runner's 180000 ms timeout (273.0 s case including execution/cleanup effects). The only fixture output is Fusion player-add. Its matching log includes an import-worker connection timeout; that is contextual evidence, not proof of the gameplay cause.
- Current fixture adds Timeout(600000), a six-minute post-entry cancellation budget, bounded startup/operation waits, coarse stage logs, and setup observations latched from the first completed host frame. Keepsake now waits for actual canonical range and runner-local LOS before submitting the unchanged TakeKeepsake command. These source repairs are present; their native result is not established.

## Finding 1 — fixture path runs directly into deployed CLOCK

`WordboundNativePuzzleActions.Deploy` places the player 1.5 m behind the exact socket, facing the socket. In the TimedMechanism branch, no later setup moves the actor aside. `MoveIntoArrival` continuously steers toward the target with no waypoint or jump, so X stays on the socket centreline. The target is forward of the CLOCK (radius 2.75 m / 2 = 1.375 m for the current catalogue).

The current CLOCK definition is a solid 0.48 × 0.48 × 0.122 m item. Deployment is asserted kinematic, `WordboundGameplayBody` creates its actual box with centre at half height, player capsule radius is 0.3 m, and `ShouldCollide` permits same-team player/item collisions. `WordboundNetworkBody.FixedUpdateNetwork` supplies horizontal movement and explicit-input jump only; there is no step-up path. Thus the scripted straight route intersects the CLOCK before reaching the sorting arm. This is a concrete test-path obstruction and a likely false fixture timeout, not evidence that the authored puzzle is globally unsolvable.

Recommended bounded repair: drive around the CLOCK using ordinary movement waypoints, then cross the actual sorting-arm/arrival area. Preserve all native timing, keepsake, arm-motion and observed-arrival assertions; do not teleport into the arrival, disable the collider or relax completion checks. Actual collision outcome remains NOT RUN in this review. Root was notified before any rerun.

## Bounded root-authorized repair

Root authorized only the physical test helper after receiving Finding 1. `ClockApproach` derives a side clearance from the actual CLOCK box and player capsule, moves laterally while still behind the item, passes beside it, then rejoins the centreline before the sorting arm. All waypoint X/Z extents are checked against the actual puzzle module. An explicit guard includes player radius and waypoint tolerance when checking that the rejoin is before the moving fixture's near edge. The routine uses the real item/arm transforms and existing movement input; it never writes a body pose.

For the present catalogue, CLOCK width/depth are 0.48/0.122 m and capsule radius is 0.30 m. Side offset is 0.660 m; forward rejoin is 0.441 m. The arm centre is 0.916 m ahead, with 0.100 m half-depth. After player radius and 0.030 m waypoint tolerance, 0.045 m remains between the rejoin target and the arm approach boundary. Actual first-lap room is Packing_ConveyorTurn (centre X=36, Z=12 m); socket X=39, Z=3 m; waypoint centres are (39.66,1.5), (39.66,3.441), (39,3.441). Their full horizontal capsule extents fit the 24×24 m room. This is arithmetic/source evidence, not a collision simulation.

The optional waypoints run inside the existing `MoveIntoArrival` loop and its unchanged eight-second deadline. BuildRoute keeps its original direct target path. Actual slowed-arm ticks, kinematic collider checks, keepsake carrier, observed native actor arrival, mechanism evidence, UsePuzzle receipt and canonical checkpoint import remain unchanged. No production code, puzzle condition, timeout, collider, source map or profile was changed.

## Other reviewed paths and limits

- First completed-frame setup observation is subscribed after controller initialization and before session start; Reconcile applies the canonical snapshot before FrameChanged. A requested setup cannot be satisfied by a later descent or an old epoch. Keepsake acceptance still uses real three-metre range and runner-local LOS.
- CollectLetters performs real HAMMER attacks and requires the authored prop's released letters; lifecycle compaction removes destroyed crafted entities, not the map prop needed for smash evidence. PlaceObject and BalanceWeight retain wrong-socket rejection. FAN observes host-applied force and actual receiver arrival. BED requires the upward velocity and matching mechanism evidence in the same completed frame before observed arrival. CLOCK still crosses native machinery after the detour.
- The fixture explicitly repositions the player between rooms/supplies. A passing result proves six first-lap authored families with checkpoints, not human traversal, all three laps, online recovery or performance. Existing source and template checks are not substitutes for the native run.
- No additional blocking source defect was confirmed within this bounded pass. Previous native evidence cannot identify whether keepsake range, occlusion or the old setup race caused the original failure. New diagnostics and the next native result must establish that.

Reviewed root snapshot initially fcfe325; relevant helper is from root commit3220920, SHA-2567a57eb8d2af13f4e4f044265e6f7eefa0e23a3299818cca9fe58ac2f8a421ab7. This lane lacked the helper, so local baseline commit fb9f812 contains those exact pre-edit bytes and is NOT for root integration. Integrate only the subsequent bounded delta/report commit. `git diff --check` and source/API/geometry inspection pass; compiler, Unity and physics execution remain NOT RUN here.

Independent input-lane review of commit 8378889 found no concrete API or transform-math defect for the current unit-scale, yaw-aligned fixture. It confirmed live collider clearance, the rejoin before the capsule-expanded sorting arm, room-bound checks, the unchanged single eight-second deadline and retained movement/completion/arrival evidence. This source review does not establish native movement success.

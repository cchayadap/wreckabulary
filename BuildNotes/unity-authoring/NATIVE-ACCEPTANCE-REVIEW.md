# Native acceptance fixture review

STATUS: IN PROGRESS

Read-only source review of root integration native fixtures, HUD/player-view/launcher consumers, and Online's pending all-mode public entry fixture. No production edits, compiler or Unity execution. This report distinguishes assertions present in source from runtime evidence and human acceptance.

- [~] Actual public allocator, authenticated actor binding and gameplay readiness.
- [~] HUD target selection and defensive input/focus handling.
- [~] Course asset edit, exact preview/play, return and persistence.
- [ ] Highest-value bounded native additions and exact source pins.

Findings will be appended when confirmed. Existing green logs are not extended by this source review.

## Confirmed source findings and bounded changes

1. **Remote HUD interaction was unavailable:** HUD.Target enumerated Controller.Bodies, which is populated only by host Reconcile. Replicas populate presentations instead. The existing multiplayer interaction test directly called Submit and could not detect this. Added a read-only PresentedBodies accessor restricted to valid objects of the controller's current Runner; the HUD checks canonical ID/type/activity and team policy before considering a target.
2. **Occluded objects could monopolize selection:** range/frustum checks did not establish visibility. The HUD now uses the host's actual HasLineOfSight when present. Replicas use their isolated runner PhysicsScene, only registered static surfaces and canonical native bodies, identical height offset, bounded hit buffer and ShouldCollide policy. Host authority still validates every command. This is source repair pending actual native execution.
3. **Saved authoring course entry was untested by the old launcher case:** it calls OpenWorkshop then replaces the draft with OpenFlagship, bypassing the new asset hook. Added a separate native asset-based entry/play/return case; execution remains pending.
4. **One expected value was stale:** WordboundWorkshopLaunchTests expected zero Freestyle Plan.Sites. The current unified plan includes 72 authored practice stations while resolved race laps stay empty. Corrected that assertion and explicitly retained the zero-race-laps check (13293b5, source only).

New external fixture: `HostAndRemoteHudChooseVisibleLetterAcrossActualWallOcclusion`. It uses two authenticated real runners, ordinary movement to authored supplies beside the real room wall, both candidates inside the actual camera frustum, and a nearer occluded versus farther visible letter. It invokes the HUD ReadCommands entry, checks the exact expected command payload and Ok receipt, and waits for acknowledged collected-letter effects on both peers. It explicitly exercises the replica with no authoritative PhysicsWorld. No direct Submit substitutes for the tested grab. Cameras are positioned by the fixture and UI methods invoked reflectively; this is not human input or visual acceptance.

The new partial fixture requires Online's coordinated Run(hudInteractions) seam and HudCourse selection. No compiler or Unity has run on this follow-up. Baseline import e33b602 is lane-only and must NOT be cherry-picked into integration; real deltas follow it.

New local filter: `Wreckabulary.OfflineGameplayTests.WordboundNativeAuthoredLauncherTests`. The fixture saves/reloads a distinct CourseAsset and profiles, copies item/rule TextAssets with distinct valid source bytes, and assigns it only to a cloned LaunchAssets. It calls the real launcher OpenWorkshop without replacing its draft, verifies saved source/mode/catalogue/seed, edits a fixed prop position and seed, then checks the actual local runner's exact manifest/content binding and camera/visual profile consumers. Awaited return must restore the same draft/undo history while the persisted asset remains unchanged. Cleanup awaits shutdown and deletes only the GUID-named fixture assets and its exact autosave/backup filenames. This test is source-only and requires the registered native launcher assets. It is a new child test assembly, so root must allow Unity to generate metadata.

## Additional native acceptance priorities

- Public entry draft uses real QuickPlay, signed roster verification, Photon custom authentication, actual client movement and committed command transport. It isolates the pool deliberately; this does not establish production population availability. TimeRun's native case uses a preformed two-human party; independent Fill joining is separate service evidence. Freestyle currently checks no-damage/no-timer/no-result policy, not an attempted attack or completed practice station. Online agreed to accurate test naming and explicit 600000ms timeouts.
- Add a real HUD shield case with crafted/equipped shield: refresh Hold longer than one lease, observe canonical raise transition, then spell/menu/focus loss/slot change and assert Release or bounded canonical expiry. Direct domain defense tests do not cover the HUD's dispatch and focus lifecycle.
- Preserve physical fixture labels: six-family setup may reposition bodies before actual sensor observation, and the exact checkpoint import proves persisted evidence. This is not a full human three-lap route completion.

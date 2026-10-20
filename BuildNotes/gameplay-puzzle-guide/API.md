# Guide integration

Add the component to the same owned match presentation object as the camera/controller:

```csharp
var guide = matchObject.AddComponent<WordboundPuzzleGuide>();
guide.Bind(controller, exactResolvedCourse, localPlayerId, playerView.Camera);
```

Call `guide.Clear()` on Return before destroying the match presentation object. `OnDestroy` also clears and unsubscribes. No `ConfigureVisual` callback change is needed. Do not parent the component itself to a replicated body; it owns a separate child beneath the controller's current `CourseWorld`.

The view validates exact source and manifest binding, pins the match identity, reads the local player's team, and selects only its current lap/checkpoint and unsolved prerequisites-ready profiles. Checkpoints show a muted “solve puzzle first” label until ready. After the last checkpoint, the finish ring uses that checkpoint's recovery sensor, matching `WordboundPuzzleRuntime.ObserveProgress`. Returning, disconnecting, respawning, migration, paused simulation and match completion hide the guide.

Markers:

- Cyan recipe socket ring and facing arrow use authored position, yaw and radius.
- Cream smash-source rings use resolved team-owned prop placements; authoritative missing/inactive source bodies suppress those rings.
- Mint wire volumes mark exact wind/raised/traversal arrival sensors.
- Cyan wire volume marks the loose wind receiver's authored start.
- Purple footprints mark both exact balance switch volumes.
- Orange wire extent and segment mark actual moving-arm dimensions and swept path.
- Gold checkpoint/finish ring and beacon use the actual recovery sensor radius.

All objects are renderer/text-only. The view creates its root in the loaded CourseWorld scene, inherits its transform and layer, and replaces it when that world changes. No collider, rigidbody, `NetworkObject`, trigger, gameplay flag, hidden reroll or state mutation is introduced. Shared materials and fonts are not modified. Guide-owned materials/objects are hidden immediately and destroyed on disposal.

`Fault` exposes a guide setup/binding failure without changing gameplay authority. `MarkerCount` is diagnostic. `WordboundPuzzleGuidePlan.Build(course, frame, actor)` is the production selection API; its ProgressSnapshot overload supports independent progression tests. `WordboundPuzzleGuideView` exposes renderer-only construction for actual Unity scene/cleanup tests.

Native acceptance is still required: readable URP lines and world text from the actual player camera, socket arrow orientation, target heights, both pad footprints, arm path alignment, checkpoint transitions, Return cleanup and replacement-runner scene ownership. The plan tests do not prove rendering or physical puzzle completion.

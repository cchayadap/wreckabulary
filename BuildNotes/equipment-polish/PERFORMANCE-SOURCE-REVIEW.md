# Synthetic performance scenario — independent source review

STATUS: COMPLETE — source findings closed; native execution pending

Reviewed `wordbound-puzzle-race` clean commit `e2b5860` (initial `296f60c`, then `f78e86e`) and the repair commit `d6d672c`. Scope: `WordboundPerformanceQa.cs` and `WordboundPerformanceWorkload.cs`; supporting PerfProbe, QA runner, GraphicsOptions, visual resolver and avatar-motion APIs were read only to verify behavior. No source edits, compiler, Unity process or native measurement were performed.

## Confirmed behavior

- The scenario opens the real local flagship through the existing lobby, workshop and local-client path. It requires one actual local actor and no online match client; records eight additional synthetic avatars, eight held visuals, nine total actors, zero remote peers, actual world bodies, content identity and hardware. Synthetic visuals have no colliders or rigidbodies because the production resolver rejects them. This is rendering/animation evidence, not eight-peer or active-combat acceptance.
- Every selected equipment entry must contain the detailed normal/mask maps and keywords at 512×256. Created held models must use the exact selected collection sharedMesh sequence. This proves the selected runtime collection is used; the native importer/manifest gate separately binds that collection to the reviewed DetailV2 source revision.
- Warmup uses OR in its continuation condition, requiring both at least three seconds and at least 120 frames. PerfProbe.Begin resets previous statistics only afterward. Its own Update collects the measured frames; Finish stops it before screenshots, reference-pose replay and cleanup. The frame samples therefore exclude those later operations. Frustum checks require eight synthetic avatars at the beginning and end; there is no claim of continuous occlusion visibility.
- The new steady-state RequireHealthy and SetPose paths use cached references and bounded loops, without scene searches, LINQ enumeration or per-frame renderer arrays. Setup and post-sample visibility/count checks intentionally allocate outside the sample. The small cached-string IMGUI label and existing game/PerfProbe costs remain part of the workload; this review does not establish allocation-free engine behavior.
- GraphicsOptions really stores the four keys as strings. The scenario snapshots their value and existence, restores those bytes immediately after applying the temporary Low preset, and reloads original settings during cleanup. V-sync, output dimensions/mode, camera transform/FOV and view/HUD state have explicit restore paths; destruction and application quit call the idempotent restore as well. Abrupt process termination during synchronous preference writes is outside graceful cleanup guarantees.

## Findings and source closure

1. **Measured-duration metadata includes post-sample work.** Measure computes `measured_seconds` after `probe.Finish(true, ...)` writes perf.json and after RequireLocalWorld performs scene searches. Capture the end time immediately at loop exit and report that end minus the recorded start. Frame samples themselves stop correctly. The existing QA runner may call Finish a second time; with `-qa-perf`, that can rewrite perf.json EndedUtc after cleanup while retaining the stopped sample data. Record explicit scenario sampling bounds so the sample duration stays unambiguous without editing the kit.
2. **Partial preparation can restore uncaptured view/HUD flags.** Prepare assigns view/hud before validating that both and the camera exist, but captures enabled flags afterward. A missing required component throws before capture; Restore can then set an existing component to its default false field value. Guard this restoration with a capture flag, or capture available flags before the possible throw.

Nonblocking evidence condition: the scenario's error text requires a presented release-QA Windows player, but its initial runtime guard currently rejects only Editor/batch. The execution gate/build manifest must therefore establish Windows release-QA identity; checking PerfProbe.BuildKind and platform inside the scenario would make this self-enforcing.

## Native evidence still required

Compile the final integrated source; run the presented exact release-QA Windows build with other heavy jobs stopped. Inspect checks/perf/build identity, actual displayed camera composition, source collection import, 60-second bounds, settings restoration and screenshots. A passing synthetic run does not close eight-authenticated-peer gameplay, combat/throw physics, populated-map traversal, sustained soak or human appearance acceptance.

## Repair verification — d6d672c

Both bounded findings are closed in source. `sampleEnd` is captured immediately after the sampling loop, before Finish or scene validation; duration uses `sampleEnd - sampleStart`, and checks notes retain both UTC and monotonic sample bounds. The existing kit's possible later perf.json EndedUtc rewrite therefore cannot obscure the scenario's explicit sample interval. `viewStateCaptured` is set only after the valid camera/view/HUD state is saved, before any changes; restoration of these values is conditional on that flag. The source owner also made the identity condition explicit: RuntimePlatform.WindowsPlayer, non-batch and PerfProbe.BuildKind equal to release-qa are required.

No unresolved blocking source defect was found in this bounded review. These are source checks, not native compilation or measured performance results. Root retains the execution slot and acceptance decision.

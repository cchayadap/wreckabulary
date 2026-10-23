# Authoring persistence repair

STATUS: SOURCE REPAIR COMPLETE; NATIVE VERIFICATION PENDING. No native, compiler or test job ran in this lane.

Root evidence: `TestResults/editmode_20261023T122055Z.xml`, `WordboundAuthoringPersistenceTests`: 4 passed / 3 failed. Two restored valid forms fail Apply with `yaw.invalid` and `zone.fixed`; the scene test fails its populated-untitled precondition before invoking restoration.

Confirmed source cause: `WordboundCourseDraftStore` deserializes with Newtonsoft's default collection population behavior. `CourseZone.Yaws` initializes to four quarter turns, so persisted rotations are appended rather than replaced. Recovery must preserve exact valid and invalid list values without normalization. Scope: the draft store and its persistence fixture only. In-game workshop recovery belongs to the asset lane; camera and input repairs belong to their respective owners.

The production repair sets `ObjectCreationHandling.Replace` in the existing draft serializer settings. Draft format, limits, strict member/type policy, point/box converters, atomic storage, memory fallback, recovery conflict choices and saved-course validation remain unchanged. Existing valid saved draft JSON now loads exact collections. Already persisted duplicate/invalid values are preserved for the author's correction rather than silently normalized.

The persistence fixture adds one test: `RepeatedDraftRecoveryPreservesExactValidZoneCollections`, checking full canonical document equality through three disk save/load cycles and a final valid save. The existing invalid-form test now requires exact recovery of duplicate/non-quarter-turn rotations `[90,90,13]`, as well as its extreme coordinate and reversed bounds, and still rejects Apply without altering saved data. Existing conflict, file-lock/memory fallback, corrupt-file preservation and cleanup assertions remain.

The scene fixture preserves original loaded-scene root objects in a test-only preview scene while it invokes the actual production restoration. Cleanup returns those same objects, sibling order, scene paths/load state, active selection and clean flags. The precondition still refuses dirty user scenes. The previously rejected clean populated untitled scene is supported. Partial parking failure returns roots to the original surviving scenes; the replacement flag is set before calling NewScene so a partial replacement invokes setup restoration. All original roots are returned before cleanup assertions. A preview that still holds original objects is never destroyed after a restoration failure.

Exact clean-flag restoration uses the installed Unity Editor's internal `ClearSceneDirtiness` through reflection **in the test only**, preflighted before any scene mutation. Unity exposes public APIs for preview scenes and marking a scene dirty, but its clear operation is internal in the [official EditorSceneManager reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorSceneManager.bindings.cs). The test fails before scene mutation if this API is absent. [Preview scene documentation](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/scenemanagement/editorscenemanager/newpreviewscene) confirms these scenes are editor tooling storage and need explicit closure. Production scene restoration is unchanged; the original test failed before entering that code.

An isolated baseline-only copy of the two current root files is commit **`e5f9418`**. **Do not integrate it.** Integrate only the following repair delta. Existing dirty `ModeDefinition.cs` and historical untracked evidence are untouched.

Verification performed: read the original XML and exact root source; inspect the bounded diff; `git diff --check` passed for both edited C# files. Runtime behavior and the private cleanup API remain NOT RUN. Expected follow-up is eight persistence tests, not the earlier seven. Root owns the compiler/Unity slot and should run:

```powershell
# In wordbound-integration after applying only the repair delta.
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.Tests.WordboundAuthoringPersistenceTests' --cold --json --timeout 660
```

The targeted class covers the three retained failures, the four previous passes and the new repeated collection regression. Require eight executed, zero skipped and zero failures; inspect XML and cleanup errors. Root may batch this with its other repaired editor fixtures to share startup. The production change is editor-only and does not invalidate the public course catalogue, runtime course identity, service approval or network recovery source.

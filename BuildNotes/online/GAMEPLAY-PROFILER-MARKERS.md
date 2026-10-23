# Gameplay CPU marker contract

STATUS: Source complete; independent source review PASS; native execution pending. No measured timings are claimed. This delta owns only `WordboundGameplayController.cs` and this report.

Every marker uses `Unity.Profiling.ProfilerMarker`, explicit `ProfilerCategory.Scripts`, and synchronous `Auto()` scopes. Names and boundaries are fixed for the opt-in native test recorder:

| Marker | Scope |
| --- | --- |
| `WB.Match.Reconcile` | Entire Reconcile invocation, including nested work, retries, notifications and exceptions. |
| `WB.Match.Capture` | One `Authority.Match.Capture(1)` call through the identical CaptureFrame wrapper; all six existing controller call sites use it. |
| `WB.Match.Advance` | Existing Authority.Advance call. |
| `WB.Match.Validate` | Reconcile's existing MatchStateValidation.Validate call. |
| `WB.Match.Presentation` | New frame presentation context and complete views dictionary construction. |
| `WB.Match.PhysicsApply` | Existing physics.ApplySnapshot call, including its internal validation. |
| `WB.Match.BuildWorld` | Existing WordboundCourseWorld.Build call only. |
| `WB.Match.BeforePhysics` | Existing optional BeforeHostPhysics callback invocation when authority is not paused. |

There are no additional captures or validation calls, no scope crosses await/yield, and all existing call order, guards, rule/lease/deadline behavior and exceptions remain. Reconcile is inclusive; do not add it to its child durations as elapsed wall time. Recursion and several peers can contribute multiple samples. Internal Rules captures during Advance are included in Advance, not separately intercepted by Capture. These scopes do not cover all networking, serialization, rendering or physics simulation work.

API checked against installed Unity 6000.6.3f1 `UnityEngine.CoreModule.xml` (ProfilerCategory/string constructor and Auto IDisposable scope), and the [official ProfilerMarker reference](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerMarker.html). `git diff --check` passes. Root owns assembled compile and opt-in native measurement; no heavy job was run in this lane. The input lane owns the test recorder with matching names/category and explicit sample-capacity limits.

Integrate this commit delta only after the running root test finishes. No baseline-copy commit or asset/meta edit is included; preserve root's newer surrounding Controller code and existing unrelated lane dirt.

The independent input-lane review confirmed the eight names/category, six one-for-one captures, scope disposal on return/exception and recursion, unchanged guards and ordering, and no asynchronous spans. This is a source verdict, not compile or performance acceptance.

# In-world puzzle guide

STATUS: SOURCE GREEN — NATIVE ACCEPTANCE PENDING

Owner: sethypagna/map-workshop. Scope: new Online/Presentation/WordboundPuzzleGuide*.cs, new matching presentation tests and this evidence directory. No shared HUD, launcher, runtime authority, catalogue, physics or assembly definition changes.

The existing HUD refers to marked furniture, sockets, arrival targets and floor pads, but production presentation has no markers. Implement a visual-only view of the exact resolved course and local player's authoritative progression. Each marker is sourced from canonical physical profiles or recovery/finish coordinates; the current runner's CourseWorld owns its scene and transform. No collider, rigidbody, network behaviour or authority flag is added.

Proposed composition: `guide.Bind(controller, course, localPlayerId, optionalCamera)`. The caller may call `Clear()` on Return; the guide also hides on returned/disconnected/finished local state, results, migration, faults or missing world. Runner replacement rebuilds under the new CourseWorld. A pure marker plan enables focused progression/binding tests independently of Unity.

Dependencies inspected: integration's CoursePhysicalSite/MatchProgress/MatchBinding, WordboundGameplayController.CourseWorld/FrameChanged/Faulted, existing Presentation assembly and its test assembly. Integration already contains schema2 physical profiles. No Unity or compiler process started for this slice; the long-running frozen flagship seed process remains separate.

Implemented all six family markers, ready/locked checkpoint beacon, actual finish sensor, current-team filtering, source-body inactivity filtering, match/course binding, paused-simulation/world-identity fences, and renderer-only ownership/cleanup. Labels use the caller's camera. Stable marker IDs/readiness cache prevents rebuilding unchanged marker graphics each frame. Root reviewed the pending source and requested the explicit `IsSimulationPaused` guard; it is included.

Verification, 2026-10-21:

- `dotnet test BuildNotes/gameplay-puzzle-guide/Tests.csproj --nologo --verbosity minimal -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false -p:RestoreSources=C:/Users/mrkl6/.nuget/packages --logger trx;LogFileName=guide-plan.trx --results-directory BuildNotes/gameplay-puzzle-guide/TestResults`: **11/11 PASS**, 628 ms test execution. Local package cache only. Linked integration Rules source and this lane's unchanged flagship factory; no seed category or broad suite rerun.
- `dotnet build BuildNotes/gameplay-puzzle-guide/ReferenceCheck.csproj --nologo --verbosity minimal -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false -p:RestoreSources=C:/Users/mrkl6/.nuget/packages`: **PASS, 0 warnings/0 errors**, 9.49 seconds. Unity6000.6.3f1 and installed Fusion references; includes both new test files and pause guard.
- Reference Rules DLL SHA256: `9A21754C658DC98F5EB99E717A1E7FA706503468CC4AFAD53C795539B549E12D`; Gameplay DLL: `F5BB53F8A2B485ABF812135458BA7D32465DEEBA83830B030A5D1AA2902D30F0`. Integration HEAD at evidence collection: `b296017a2c0b6d972187bf8c8392c23d10d2ae61`.
- Native scene/URP tests **NOT RUN** here. Seven native view cases authored; root owns Unity. All eleven pure plan cases can also execute in the native presentation test assembly.

Evidence: `tests.log`, `reference.log`, ignored `TestResults/guide-plan.trx`. `API.md` gives root the composition and native acceptance steps. No metas/YAML, assembly definitions, colliders, network code or shared production files changed. Next: root integrates this commit, binds both client launch paths, runs native guide tests and inspects actual camera views through representative families and Return.

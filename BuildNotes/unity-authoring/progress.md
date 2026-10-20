# Unity Inspector authoring

STATUS: IN PROGRESS

- [x] Audited existing surfaces: camera follows hard-coded ShoulderView constants; LaunchAssets exposes transport/prefabs; workshop edits courses in-game; no saved Inspector course document/form or exact preview/play entry point existed.
- [x] Authored additive CameraProfile and VisualProfile data/validation with unchanged camera defaults, render-only models/materials, canonical body fitting retained, and shared DefensePose from Sethy's presentation slice.
- [x] CourseAsset/catalogue, structured course forms, safe module transforms, exact preview and local-play editor workflow authored.
- [x] Root consumer hooks (PlayerView, VisualResolver, StartLocal/StartMatch) included in combined reference build. Separate Authoring, Editor and Tests assemblies compile with 0 warnings/errors.
- [ ] Native tests / actual editor interaction: NOT RUN. Root owns Unity.
- [ ] User acceptance: NOT RUN.

Profiles are presentation only. Course/rules/item/catalogue edits produce exact canonical course/manifest/content identities through the existing resolver, physical-content validation and entity-budget preflight. Custom local edits do not modify approved public bindings. Invalid or stale previews must fail closed.

## Source verification

Command: `dotnet build BuildNotes/unity-authoring/reference/Tests.csproj --no-restore --nologo -m:1 -p:UseSharedCompilation=false -v:minimal`.

Final result: PASS, 0 warnings/errors, 60.98 seconds. Fresh root Rules, current root runtime consumers including defense/profile hooks, separate Authoring production assembly, separate Editor assembly and separate test assembly. Earlier harness-only Photon.Realtime reference omission and one preview catch-variable name collision were corrected before the final pass. Full output retained in reference-build.log. This proves reference compilation, not execution of Unity APIs or tests.

Native filter: `Wreckabulary.Tests.WordboundAuthoringTests` (7 cases), NOT RUN. Covers source isolation/invalid import preservation, exact preview binding/staleness, item/rule input identity, module transforms, Unity serialization, camera bounds and render-only visual overrides.

Next recoverable action: root integrates and runs native tests/preview/play. Author a separate idempotent native setup helper for reusable saved profiles/course copies and actual launcher references; no further compiler until granted. Root retains screenshots and human editing acceptance.

Source-only setup follow-up: `Wreckabulary.Online.Editor.WordboundAuthoringSetup.CreateDefaults` creates or verifies the four reusable authoring assets using AssetDatabase, validates exact Freestyle/flagship content, and fills only empty LaunchAssets CameraProfile/VisualProfile/WorkshopCourse references. Existing authored assets and chosen assignments are preserved. This follow-up was written after the reference slot was released and has NOT been compiled or executed; root must include it in the next source/native gate. No handwritten Unity assets or metadata were created.

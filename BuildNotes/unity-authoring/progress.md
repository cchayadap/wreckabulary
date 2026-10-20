# Unity Inspector authoring

STATUS: IN PROGRESS

- [x] Audited existing surfaces: camera follows hard-coded ShoulderView constants; LaunchAssets exposes transport/prefabs; workshop edits courses in-game; no saved Inspector course document/form or exact preview/play entry point existed.
- [x] Authored additive CameraProfile and VisualProfile data/validation with unchanged camera defaults, render-only models/materials, canonical body fitting retained, and shared DefensePose from Sethy's presentation slice.
- [~] CourseAsset/catalogue, structured course forms, safe module transforms, exact preview and local-play editor workflow.
- [ ] Root consumer hooks (PlayerView, VisualResolver, StartLocal/StartMatch) and combined source verification.
- [ ] Native tests / actual editor interaction: NOT RUN. Root owns Unity.
- [ ] User acceptance: NOT RUN.

Profiles are presentation only. Course/rules/item/catalogue edits produce exact canonical course/manifest/content identities through the existing resolver, physical-content validation and entity-budget preflight. Custom local edits do not modify approved public bindings. Invalid or stale previews must fail closed.

Next recoverable action: finish additive Editor tools and tests, then request a bounded reference compile slot. Profile source checkpoint is intentionally uncompiled until root combines WordboundDefensePose and consumer hooks.

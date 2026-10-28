# Map correction source review

STATUS: SOURCE REVIEW COMPLETE — PRESERVATION GUARD REQUESTED

Read-only review of the current room architecture, style profile, saved correction setup and focused tests. Focus: owned meshes/materials, borrowed canonical plank resources, disposal, saved-profile edit preservation and idempotence. No native or live-source changes by this reviewer.

**P2: reject unsaved profile edits before the first correction.** `WordboundRoomStyleSetup.ApplyReviewedCorrection` validates but does not check `EditorUtility.IsDirty(profile)` before copying/saving. With unsaved Inspector changes, `SaveAssetIfDirty` also commits those unrelated edits, while the backup operation does not establish that it preserved their previous in-memory state. Reject a dirty profile before the first backup/mutation and explain that its edits need saving first. This guard can follow the existing-backup no-op, because that path makes no profile writes. The earlier menu migration already follows this preservation pattern.

No material/mesh ownership regression found. `Make` returns an explicitly assigned material without adding it to the owned list; fallback materials clone the canonical Surfaces material, and disposal releases only those clones. Plank textures remain borrowed and are not destroyed. Floor meshes are cloned, retain their canonical UVs and are released through the unchanged Dispose/OnDestroy path. Repeated disposal clears the owned lists safely.

Removing the wall baseboard changes only rendered geometry. Insets default to0 while remaining available as an explicit profile option. The saved correction changes only MaximumInsetsPerWallPiece; it leaves palette colours, assigned materials and artwork references intact. The backup lives outside Resources and an existing typed backup makes later calls a no-op, preserving later profile edits.

Recovery limitation: backup creation is also the completion marker. A failure after CopyAsset but before saving the correction leaves a backup that makes a later call return without applying the correction. Preserve/report that stage and inspect current/backup state rather than assuming a rerun completed it. This is a bounded failure-path limitation, not evidence that the current saved correction failed.

The added test checks canonical plank albedo, normal map, bump scale and UVs with no default insets. Existing tests retain collider/manifest preservation, explicit material overrides and owned-resource disposal coverage. These observations are source-only; native execution and fresh images remain root-owned.

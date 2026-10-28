# Independent map-preview pipeline review

STATUS: REVISED SOURCE REVIEW COMPLETE — NO REMAINING BLOCKER FOUND IN THE FOUR RECHECKED FIXES

Scope: five frozen Editor-only capture/settings/import files. Check actual content/seed identity, owned camera/resource cleanup, borrowed asset lifetime, dirty scene/profile guards, non-overwriting backups, import receipt/hash checks and repeated runs preserving manual edits. No live/native execution or asset generation. This report is the only write for this review.

Inspected all five initial candidate files completely and the relevant CreateSoloContent, launcher GameData binding, CourseWorld, RoomArchitecture, VisualResolver and TactileMaterials implementations. The saved WordboundLobby launcher Assets GUID matches Resources/Wordbound/LaunchAssets. Rechecked the repaired frozen revision below. Root owns integration and native checks; this review makes no native/resource-safety or visual-acceptance claim.

## Confirmed findings (initial frozen candidate)

**P2 — Import does not recheck resolved content identity.** `WordboundSoloPreviewImport.ImportReviewed` checks each supplied inputFacts file/dependency hash, but the capture's explicit roster does not cover all transitive generation code (for example WordboundClientContent or CourseResolver). Import never examines `sets`, compares each frame's manifestHash to its map set, or calls CreateSoloContent again. A change in unlisted generation logic can therefore leave older course thumbnails importable as current. Recommended bounded fix: before backup/write, validate exactly the two expected map sets and matching seed/mode, re-resolve CreateSoloContent with the current installed items/rules and captured settings, and compare SourceHash, Course.Hash, Geometry.Hash, CatalogueHash and corresponding frame.manifestHash. Sent to map_redesign and root.

**P2 — Existing texture importer dirty state is not checked.** The importer checks whether the loaded Texture2D is dirty, then rewrites TextureImporter settings. The importer is a separate Unity object; unsaved importer edits are not established as clean by the texture check. Add a dirty importer guard before any backup/write and verify the Inspector's pending import-setting path natively. The exact disk backup is useful but does not preserve unsaved in-memory edits.

Initial preservation review: stage uses the actual CreateSoloContent and installed VisualProfile; all world/prop roots are created inside an owned preview scene. Disposal attempts tactile release, visual disposal, generated world disposal, root destruction, PreviewRenderUtility cleanup and preview-scene close independently. CourseWorld/RoomArchitecture disposal is idempotent and destroys only owned meshes/material copies. No direct destruction of borrowed profile/model/material/Sprite assets was found. Capture records and compares input file/dependency hashes, selected asset memory/dirty state, scene/root state, render globals and tactile-scope count. Import uses CreateNew or non-overwriting copies for receipts/backups and updates only the two preview slots on the existing layout.

**P2 — Unsaved visual dependency edits are outside the initial dirty guard.** Top-level profiles/library objects are checked, but dirty dependent Material/Texture/Prefab assets can affect live Editor rendering without their saved bytes changing. The current input dependency hashes cover saved changes, not proof that every rendered asset's in-memory state is saved. Suggested guard: inspect persistent dependencies of recorded visual inputs and reject dirty objects/importers before capture and import. Sent to map_redesign; native verification should include an edited material Inspector and pending importer settings.

**P2 — Framing-window cleanup can skip render-global restoration.** In the initial Window.OnGUI finally block, EndPreview precedes the RenderTexture.active / GL.sRGBWrite restores. If EndPreview itself throws, both restores are skipped. A nested try/finally makes restoration unconditional. CaptureDrafts has a separate outer cleanup guard and does not share this omission. Sent to map_redesign, with the suggestion to mirror CaptureDrafts' SSAO keyword preservation in the framing window.

The four initial findings are retained above as review history. Their final source disposition is recorded below.

## Repeat/manual-edit behavior and limits

CreateOrSelectSettings selects and validates existing settings without resetting poses. Capture creates a fresh timestamp/GUID directory and uses CreateNew for outputs. Import refuses an already-started before-import directory, backs up exact existing PNG/meta/layout/meta files with non-overwriting copies and hash checks, and leaves partial-import evidence rather than silently retrying. A new reviewed import intentionally replaces the two managed preview images and Sprite slots; other layout/framing fields are preserved. Saved layout edits must be clean first. This is not an automatic rollback facility.

No native capture, importer, render fault injection, scene-dirty check or visual inspection was run by this reviewer. In particular, matching file hashes do not prove attractive framing; actual imported card screenshots remain necessary. Dirty existing scenes are recorded and checked for unchanged state, rather than rejected merely for containing unrelated unsaved scene work. The preview pipeline should not be reported as natively accepted while preservation/visual checks remain pending.

## Final frozen revision recheck

Manifest SHA-256 independently verified: `78c2d9c98b92204812b4ed57ae49ec936a85714125539658a8cb590a4fd45f96`. Independently hashed all five candidate files and confirmed every candidateSha256 entry matches.

| File | Final SHA-256 |
| --- | --- |
| WordboundSoloPreviewCapture.cs | `de956c340977534c92616d4bf93822d48d09a456c572839a17547552a420710e` |
| WordboundSoloPreviewImport.cs | `26c5c946bf5ce99f6c460a51fef4d864f4ce547c195f8dc77ba9073e1029a8a3` |
| WordboundSoloPreviewSettings.cs | `4b66c7309cde42a5b73d0a560c0f3be02f4dba666ff5cd9f0424fae151a83ec5` |
| WordboundSoloPreviewStage.cs | `9ff0bb0f7304ad068b3474762e1de24251582ec97fcab19f30a0430d26b07475` |
| WordboundSoloPreviewWindow.cs | `316eb2db7f26788704ce3dc385b22c8bf4fb433b2dd27e06069887c932c6ab8f` |

1. **Content identity fixed at source level.** Stage now obtains items/rules through the same Resources GameData as the launcher. Before any backup/write, import requires exactly two expected map sets/frames, matching saved seed/mode, re-resolves both maps through current CreateSoloContent, and compares SourceHash, manifest/Course.Hash, Geometry.Hash, CatalogueHash and frame.manifestHash. Missing/duplicate map identities reject instead of selecting a substitute.
2. **Destination importer guard added.** Both the existing Texture2D and its separate AssetImporter must be clean before backup or managed PNG/settings replacement.
3. **Dependency guard added.** Capture checks relevant AssetDatabase dependencies before creating output and again during final preservation checks; import checks them before writing. The bounded roster rejects dirty persistent main assets/importers and a dirty current Prefab Stage when its asset belongs to those dependencies. This addresses the reviewed material/texture/prefab-main-asset gap. Actual Inspector pending-state behaviour still needs native verification.
4. **Framing-window restoration fixed.** EndPreview now sits in an inner try with an outer finally restoring RenderTexture.active, GL.sRGBWrite and the original SSAO keyword. An EndPreview exception can no longer skip those restoration statements. Native exception/fault behaviour is not proven by this source review.

The additional SourceFingerprint hashes sorted, normalized, distinct C# paths and their bytes from eight relevant source roots. Capture compares it before/after and records sourcePreserved; import requires that flag and an exact current fingerprint match before writes. The existing checkout has 204 covered source files, 209 including these five proposed files, within the explicit 512-file bound. The fingerprint therefore covers added/deleted/changed files in those roots, rather than just the earlier hand-picked files. It is a bounded source roster, not a claim to cover every package or external renderer dependency.

Independently read `source-check.json`: four semantic compilation records (Rules75, runtime178, Client23, Authoring.Editor19 source files) have zero errors. This compiler invocation was not rerun by this reviewer. No further broad review was performed during the final recheck. **Revised-source verdict: no remaining blocker found in the four requested fixes or their source-fingerprint guard.** Root still needs the actual capture/import preservation results, imported card appearance and repeat/dirty-state checks before native acceptance.

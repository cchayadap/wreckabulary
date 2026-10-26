# Corrected shop portrait rebuild

STATUS: SOURCE REVIEW PASS — independent source review, C#9 syntax/hash checks and strict patch checks passed. Native render/import and visual acceptance remain root-owned. No live Assets edit, Unity render, import or publication was performed by this agent.

Final patch SHA-256: `2c3c855dd172d275e0526cde4a3d4f8078d26dbf165da6c06cc782547dc9a80b`. Candidate SHA-256: `fdb8e3e0aad9ad5e03d32f87346419357a9287ed3a4865a526cf7c6908b3f7b0`. Independent review: `BuildNotes/restart-20261026/guard-review.progress.md`.

One existing editor file changes. The draft requires the separately reviewed Avatar follow-up's read-only `VerifyExistingProfile(true)`; a disabled, absent or stale correction fails before creating portrait output. It rebuilds the existing six bundle IDs only, through the production `ModelVisual.Spawn` route. No portrait-specific mesh/material fix is inserted.

## Preserved visual setup

Original Idle at time 0, bundle wardrobe, materials/property-block tinting, yaw160, visible bounds, orthographic-size equation, near0.01/far30, camera5 units back, background(0.97,0.94,0.85,0), HDR off, 256×320, key1.15 at(35,35,0), fill0.65 at(340,218,0), ambient0.45. Constructor light colors remain unchanged. One render per portrait, synchronous shader compilation during the isolated capture, then restored.

The [official Unity 6000.6 PreviewRenderUtility source](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.6/Editor/Mono/Inspector/PreviewRenderUtility.cs) and prior actual portrait-control receipts show BeginStaticPreview enables the preview lights without resetting custom intensity, rotation, color or ambient. The original pre-Begin setters are retained. This draft records before-Begin, after-Begin and after-Render light states and fails on changed numerical lighting (enabled-state transitions are observed, not misclassified as absent lighting). Branch source inspection is not a binary-exact decompilation of this installed patch editor.

## Evidence and bounded writes

- Unique output: `BuildNotes/developer-workspace/shop-portraits-corrected/<UTC>-<unique id>/`. Existing evidence is refused, never overwritten.
- `Previous/` stores byte-verified copies of all six original PNGs and their metadata before any capture. `Rendered/` stores all six new PNGs before publication.
- Original FBX/meta, saved correction/profile/meta, materials/textures, Idle, game data, renderer meshes, fonts, source library and render settings are hashed or inspected in memory before/after. Source memory dirty flags, active scene/root IDs/dirty state, lighting globals, active render target, async-shader flag and SSAO keyword are checked after cleanup. No global SaveAssets.
- Each portrait proves the exact original→saved corrected head reference and strict identity of every other mesh; records the actual shape weights, materials, camera/bounds, pixel format, pixel variation, hash and cleanup. The profile is required to be enabled. The historical raw-original controls stay unchanged.
- Only after all six staged renders, cleanup and source/global proofs pass does publication begin. Each existing target is checked against its preflight hash/GUID before replacement. Publication is sequential, **not atomic**. If import fails halfway, backups, all staged images and a failure receipt are retained; no automatic rollback or retry overwrites potentially concurrent work.
- Each target texture retains its GUID. Its metadata intentionally receives the old importer settings (Default, transparency enabled, no mipmaps, max512) plus NPOT=None, then only that importer saves/reimports. Existing courier metadata has NPOT=1 and a legacy top-level max2048 field alongside DefaultTexturePlatform max512; the receipt captures the actual importer state. NPOT changes mean target-meta bytes need not remain identical. The receipt records full before/after target importer metadata and hashes. Source metadata remains protected separately. Native readback must confirm imported dimensions256×320.

The capture output is an actual original-material Lit preview. Pixel variation is only a blank-image guard, not proof of visual quality, skinning motion, real gameplay, camera usability or transparent background support. Prior EndStaticPreview results used RGB24/sRGB; alpha0 in the chosen background does not by itself establish an alpha-bearing output.

## Root execution and acceptance

1. Finish native avatar source/skin/blendshape tests and the wrapper/validation follow-up tests. Strict-check and apply this one-file patch only after independent source review. Let root run the required R0/core gates.
2. Use the existing official gate entry `exec Wreckabulary.EditorTools.ShopPreviewImporter.Rebuild --json` with root's approved cold/warm flags. This is the minimal native six-portrait route; it does not open/save game scenes or invoke a workspace reset.
3. Require `REBUILT_NOT_VISUALLY_APPROVED`, six installed PNG hash matches, six unchanged GUIDs, six256×320 imported textures, exact saved head references, zero owned objects/open preview scenes, source/global preservation and no capture errors. Verify old backup hashes against the receipt.
4. Open all six Previous and Rendered images side by side. Check cream face/brown hair, costume hue, black accessories, eye/brow definition, silhouette, clipping, original background and no magenta/error shader. Compare courier against raw A/corrected H controls without claiming they are exact material/normal-identical images (production correction also rebuilds tangents and shape surface deltas).
5. Keep final lobby/shop native/player screenshots and later Windows smoke evidence separate from this offline portrait rebuild. Human style acceptance remains pending.

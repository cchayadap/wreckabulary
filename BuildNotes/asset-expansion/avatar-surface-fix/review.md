# Avatar surface correction independent review

STATUS: SOURCE REVIEW REOPENED — root subsequently identified the actual-player wrapper mismatch below. The prior no-blocker verdict for the eleven-file draft is superseded pending a reviewed follow-up. Native compile/generation/tests and visual acceptance remain NOT-RUN by this reviewer. No live source edits.

Final review target: [avatar-surface-fix.patch](avatar-surface-fix.patch), SHA-256 `3df3e6f1b606bcbb3287ce50922ea1d547151a03ee471f979588fd1ea2f4f69d`, with all eleven candidate file hashes verified against [manifest.json](manifest.json). The initial nine-file draft was `2646d783fe9563738601af3468de23d5153d3cbeb520ddce99d51b6fee1fed20`.

Scope: original source and rig preservation, base and blendshape surface generation, reference-bound runtime use, persistent asset lifetime, editor preservation, diagnostic baselines and meaningful native tests. Compilation, actual mesh generation, saved-asset readback, visual acceptance and player verification remain unproven until the integrator runs the official gates.

## Follow-up blocker found by root

`WordboundOriginalEquipmentView.Artwork` returns the outer **Wordbound artwork player** object. `Observe` passes it to `MatchesOnlyBoundHeadReplacement`, but that helper searches `instance.transform.Find(rendererPath)`, with `rendererPath` relative to the inner original avatar. The actual resolver hierarchy is **Wordbound artwork player → Shape fit → Authored orientation → Avatar → SK_Head**. Therefore a correctly corrected actual Windows player fails the helper even though its collected mesh list has the intended single replacement.

The initial independent review missed this caller shape: its inspected regression used a raw avatar root, so it could not expose this mismatch. Root requested a bounded fix and a regression exercising an actual `WordboundVisualResolver`-created wrapper and the same full mesh traversal as the QA caller. The guard/importer follow-up review is recorded in `BuildNotes/restart-20261026/guard-review.progress.md`; no live fix was made by this reviewer.

## Confirmed source findings

- **Source binding:** `AvatarSurfaceCorrectionSetup.ValidateSource` binds the reviewed FBX path, FBX bytes, importer metadata, full mesh fingerprint, head name, vertex/submesh counts and the observed face-normal inversion. This is a bounded correction recipe for the original `SK_Head`, not a generic mesh operation.
- **Protected values:** `AvatarSurfaceMeshData.Fingerprint(mesh, false)` retains geometry, mesh/submesh bounds, indices/topology, non-surface vertex descriptors, all eight UV channels, colors, bind poses, every bone weight/index, and blendshape names/order/frame weights/position deltas. Normals, tangents and their shape deltas are the explicit permitted differences. The full serializer remains compatible with the capture-04 source fingerprint. Descriptors of non-surface attributes remain in the comparison, so an unexpected position/UV format conversion should fail validation.
- **Surface generation:** the generator clones the original once, rebuilds base normals/tangents, then calculates each original expression frame from unchanged base positions plus its original position deltas. Frame normal/tangent deltas are differences from the corrected base. It rejects a changed tangent handedness, which a three-component blendshape tangent delta cannot encode. Temporary meshes are destroyed in `finally`; the saved result is shared, never cloned per frame.
- **Runtime binding and loading:** `AvatarSurfaceProfile.TryApply` requires the exact source-avatar reference and exact original shared-mesh reference at the recorded renderer path. It does not replace custom meshes or modify the original object. Reapplying to an already corrected instance is a no-op. `Load()` retries when the cached value is null/destroyed, so a prior missing resource is not permanently cached. Validation checks source binding and shape layout without reading mesh vertex buffers in a player build.
- **Instance preservation:** assignment keeps the renderer's blendshape weights and local bounds; it does not write materials, property blocks, bones, transforms, animation clips, ModelLibrary entries or fonts. The generator writes only its new mesh/profile paths and refuses occupied conflicting paths. Existing valid saved assets are verified without being regenerated.
- **Integration:** `ModelLibrary.Spawn` covers ModelVisual/Lobby/Wordbound fresh spawns; it destroys its newly instantiated copy if correction throws. `PlayerAppearance.Initialize` applies after validating its selected Animator and before indexing expression shapes, covering saved `authoredAvatar`. `PlayerAuthoring.InstantiateAvatar` covers new editor instances and records only the renderer override. Existing `UpgradePlayer` and preview no-op paths remain unchanged; existing saved edit-mode visuals require the explicit selected-instance command.
- **Baseline integrity:** the portrait diagnostic now instantiates the raw ModelLibrary source and explicitly retains former skin/collider setup. Its A case cannot silently become the corrected default. Original-art capture restores the exact original head on its owned instance and records that exclusion; its original mesh-identity assertion remains strict.
- **Actual-player QA:** the final draft adjusts `WordboundOriginalEquipmentView` for exactly one reference-bound head replacement, while preserving original identity for every other mesh. A second altered mesh is rejected by a focused test. Evidence reports `original_mesh_identity=false` when the correction is present and records both mesh-name lists and the source fingerprint. This does not mislabel corrected geometry as original shared-mesh identity.
- **Failure ownership:** final changes destroy a newly created authoring model when correction fails, and remove the newly created legacy runtime wrapper if its spawn fails. Existing authored instances are not destroyed. These are bounded cleanup changes; no broader authoring rebuild is introduced.

## Review verification

- Roslyn CSharp9 syntax parsing: **PASS**, all eleven final candidate C# files, zero syntax diagnostics, including a pass with `UNITY_EDITOR` and `GSK_QA` symbols enabled so the QA body is parsed. This does not establish Unity semantic compilation.
- `git apply --check --whitespace=error`: **PASS** against the working checkout at review time.
- Final patch SHA-256 and all eleven per-file hashes: **PASS**, match the frozen manifest.
- Existing preservation tests were inspected: `PlayerAuthoringTests.ReapplyingUpgradePreservesArtistTransformsMeshesAndMaterials` remains strict; the draft does not weaken original FBX/animation binding tests.
- No live source edits, Unity runs, Blender work, generated assets or commits were made by this reviewer.

## Test coverage follow-up resolved in the final draft

The final draft prepares **10 EditMode cases plus one PlayMode case**, including 27 Blink/BrowRelax/animation combinations. These meaningfully compare deformed positions, normals, saved asset bytes, material/property-block state, skinning references and shared-mesh identity. No native pass is claimed here.

The producer added the requested coverage:

1. A PlayMode test loads the actual player prefab, puts its authored head on the original shared mesh, calls player setup/appearance initialization, and verifies the saved corrected mesh is used without replacing its avatar hierarchy or artist offset. Repeated initialization remains idempotent.
2. A new editor-preview test exercises `PlayerAuthoring.CreatePreview`, verifies the saved correction on a new preview, then confirms an existing preview's custom mesh and transform are preserved.
3. Repeat setup now snapshots active/loaded scene identity, roots and dirty state plus the current saved font files and metadata in both project font directories. It also retains the original source/derived/profile/material/library byte and clip checks. Both font directories exist in the reviewed checkout.
4. The QA identity-allowance test proves that a second unrelated changed shared mesh is rejected.

## Native and visual limits

- The recipe is deliberately strict and may reject the source or an expression at native execution. Do not relax its fingerprint, protected-data comparison, tangent-handedness or surface checks merely to obtain a passing run.
- Recomputed frame normals are correct at each authored frame; combinations and intermediate weights remain an interpolation, hence the explicit 27-combination native checks. They do not establish every pose, outfit, lighting direction or continuous animation transition.
- Base face normals following geometric winding do not by themselves prove the final normal-map appearance, eye/eyelash shading, UV seams or visual quality. Review matched front/side/rear and animation frames with the original materials after generation and import/readback.
- The profile has lightweight runtime structural validation. The stronger source/corrected mesh-content checks run in editor setup and native verification; runtime loading alone is not a mesh-content certificate.
- The optional selected-instance action intentionally changes and dirties that selected scene/prefab-stage renderer. Automatic setup and spawn correction are separate from that explicit editing action.

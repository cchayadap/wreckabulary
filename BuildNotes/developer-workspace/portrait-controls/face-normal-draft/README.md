# Capture04 — owned head normal recalculation discriminator

STATUS: DRAFT COMPLETE; independent source review found no concrete ownership blocker. Final candidate hash was independently verified and parsed with Roslyn C#9: zero syntax errors. No live Assets edit, semantic compilation, Unity capture or production fix was performed by this lane.

## Why this comparison

Capture02's original, frontal-key and rear-key images were inspected. The rear key brightens the face while the hoodie darkens. Capture03's BrowRelax-zero image changes eyebrow expression; the face remains dark. In capture03 diagnostics, SK_Head submesh0 has exactly the same normal statistics in A and G, while the eyes/brow submesh changes. Both meshes have finite, nonzero baked normals. These facts weaken a face-wide BrowRelax delta explanation. They do not prove imported normals are inverted or that mesh winding is correct.

Capture02's rendered head and hoodie slots all record `_Cull=2`, rendering layer mask `1`, `BlendProbes` and `receiveShadows=true` in A/D/F. There is no reported head/body difference in those fields. Back-face culling and a visible face make a simplistic inward-sphere explanation insufficient without further evidence. Per-renderer light selection, skinning, imported normals, actual fragment normals and the geometry itself remain distinct possibilities.

## Prepared files

- `baseline/ShopPortraitControlsDiagnostics.cs`: exact current live capture03 source, SHA-256 `fdb75e67fd1dfdd3135f0093bd40508fa0e803d46f0f7fbf91bf7439c4a8facc`.
- `candidate/ShopPortraitControlsDiagnostics.cs`: capture04 candidate, SHA-256 `28be4af2ed60b79572819ac6874e1494752722c8f8474fcc7826d91ddffebe07`.
- `face-normal.patch`: one-file patch targeting only `Assets/_Project/Editor/Art/ShopPortraitControlsDiagnostics.cs`, SHA-256 `c22104938af627e1fc42e4b1a107bda8b36ccc0968e9cc73fc2ff9ee3042ee6c`.
- `manifest.json`: source/patch identities, preparation HEAD, strict patch-check result and hashes of preserved capture02/03 outputs.

Patch check: `git apply --check --whitespace=error-all` PASS against the current baseline. 187 added / 8 removed lines; no intentional trailing whitespace. This is not semantic compilation. Do not apply against a changed live source without review/rebase.

Independent reviewer `/root/story_status` inspected the clone ownership, cleanup, preserved data, telemetry weighting and caveats. The reviewer verified the final candidate hash above and reported a fresh zero-error Roslyn C#9 parse after the descriptor/determinant additions. No concrete source blocker was found; actual Unity semantics and native capture remain root-owned and untested.

## Controlled changes and preservation

The new refused-overwrite destination is `BuildNotes/developer-workspace/portrait-controls/capture-04`. Cases are A-original-lit and H-head-recalculate-normals. Both use original Lit materials, original sampled Idle at time0, courier outfit, holder yaw160, same camera/framing, key/fill/ambient and image encoding. The source animation and blendshape weights are preserved.

Only H's instance SK_Head receives an owned `Object.Instantiate(sourceMesh)` mesh copy. The source remains a persistent saved mesh and the renderer must be an instance. The copy is registered in owned cleanup immediately after creation; RecalculateNormals is called only on that copy. No mesh vertices, indices, winding, tangent data, UVs, skinning data, bindposes, blendshape frames, source FBX, importer or material are intentionally altered. No automatic normal-map or tangent recalculation is performed.

A fingerprint before/after normal recalculation must preserve positions, bounds, submesh descriptors/indices, tangents, colors, all UV channels, all per-vertex bone counts and bone weights, bindposes, every blendshape name/frame weight and its position/normal/tangent deltas. Vertex-attribute semantic/format/dimension/stream descriptors are also included, so implicit position-format conversion causes the control to fail. Source/control attribute descriptions are exposed in telemetry. Sampled blendshape weights are explicitly reapplied after assigning the copy, then compared exactly. Bones, root bone, shared materials and renderer world transform are checked. This is a controlled base-normal experiment; it does not silently clear animation data.

Both A/H still render with SkinnedMeshRenderer. Each case owns separate BakeMesh outputs solely for telemetry. A should own two baked meshes; H should own the modified head copy plus two baked meshes. They are all destroyed through the existing cleanup path, including exceptions. After cleanup, each original head/hoodie mesh's full semantic fingerprint (including normals) and dirty state must match its initial values. Existing source-file/meta/material/global proofs remain active. Zero remaining owned objects and a closed preview scene are still required.

The mesh fingerprints cover semantic values and vertex-attribute descriptors, not every byte of Unity's internal buffers or serialized importer representation. RecalculateNormals can convert a vertex position buffer to Float32; the candidate rejects that descriptor change as a confound, even if values match. No source data is rewritten.

## Telemetry interpretation

Each baked head and hoodie submesh records:

- Existing whole-mesh and unique-index normal direction statistics, plus the renderer world determinant. The expected finite positive determinant is required; a mirrored or invalid transform fails this bounded control instead of silently changing winding interpretation.
- Triangle counts, finite-position failures and zero-area triangles.
- Corner normal count, nonfinite/zero normals and agreement with `cross(worldV1-worldV0, worldV2-worldV0)`: aligned above +0.05, opposed below -0.05, side otherwise. The mean dot is weighted by triangle area/3 for each valid corner.
- Area-weighted geometric-normal dot with the normalized radial direction from that submesh's world AABB center to the triangle centroid. Positive/negative/side triangle counts and zero radial vectors are reported separately.
- Signed volume contribution `sum(dot(worldV0,cross(worldV1,worldV2))/6)`, accumulated in double precision, and world surface area. Invalid/degenerate triangles are counted and excluded.

For the sphere-like face submesh0, consistently negative radial orientation and signed volume may identify inward winding even if vertex normals agree with it. But an open surface does not define an enclosed volume; translation affects an open mesh's signed sum. Eyes, hair, accessories, non-star-shaped meshes, holes and overlapping shells can invalidate a simple radial sign interpretation. The statistics cover full submeshes, not just visible pixels, and are not shader light-contribution measurements. A dot sign records the stated cross-product convention; do not relabel it outward without checking actual geometry.

H recalculates base normals **before** skeletal deformation and active blendshape deltas. Existing blendshape normal deltas remain exactly as sampled. Therefore a lit change can implicate the base-normal path, but it does not prove the recalculated normals are artistically correct or compatible with all expressions. No change does not establish that normals are correct: inward winding, retained blendshape deltas, shading participation or another rendering cause may remain. RecalculateNormals cannot repair reversed triangle winding. It may create UV-seam shading discontinuities and leave tangent-basis inconsistency, which is why this is a diagnostic only.

## Root execution / review

After root source review, use the established official gate entry for `Wreckabulary.EditorTools.ShopPortraitControlsDiagnostics.Run`; no direct Unity launch and no new output overwrite. Require:

- Exactly A/H with identical sampled blendshape weights, unchanged camera/lights/materials and hoodie control.
- H `ownedNormalControlApplied=true`, matching non-normal fingerprints, finite nonzero recalculated normals and recorded changed-normal count (zero is a valid negative observation).
- Every source mesh memory/dirty proof, source-file/meta/material proof and global proof unchanged.
- A ownedMeshes2, H ownedMeshes3, zero remaining owned objects, no cleanup errors, closed preview scenes.
- Inspect actual images and all face0 metrics together before deciding a next repair experiment. Preserve any failed/partial capture04; never call this an accepted face or runtime fix.

Primary API references checked during preparation: Unity's [Mesh.isReadable](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/mesh/isreadable) permits Editor access outside the game/rendering loop; [GetAllBoneWeights](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/getallboneweights) returns an Allocator.None view, used immediately without disposing or holding it across mutation; [RecalculateNormals](https://docs.unity.cn/2023.2/Documentation/ScriptReference/Mesh.RecalculateNormals.html) describes triangle/shared-vertex reconstruction, UV-seam and tangent limitations. Native behavior of this candidate still requires the pinned editor run.

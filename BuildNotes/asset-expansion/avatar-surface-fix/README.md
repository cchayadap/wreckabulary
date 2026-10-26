# Saved correction for the original avatar's surface directions

STATUS: SOURCE DRAFT; independent source review complete with no blocking finding. All eleven candidate files passed Roslyn C#9 syntax parsing and strict patch application check. No live Assets files, original FBX, materials, imported animations or Unity settings were changed by this lane. No Unity/Blender job was launched here. Apply/generation/native tests and final appearance acceptance remain root-owned.

## What the completed diagnostic establishes

I inspected both capture04 PNGs and its `diagnostics.json`. The original face is dark gray; the instance with recalculated head normals has a cream face and dark hair while the hoodie remains visually unchanged.

Face submesh0 in A has 4,992 valid outward triangles and 14,976/14,976 corner normals opposing their geometric direction, with area-weighted dot `-0.9984567766`. H has the same geometry/area/volume, 14,976 aligned corners and mean dot `+0.9984583817`. All 6,630 head normals changed. Both cases' original mesh fingerprints, source files/metas/materials and global state remained unchanged according to that receipt. This strongly isolates a head surface-normal defect for this sampled pose; it does not validate a production correction across expressions and motion.

One global sign flip is not established for every head part: A's eye/brow submesh has 1,336 aligned, 6,088 opposed and 112 side corner samples. H corrects that part too. The draft therefore regenerates a consistent geometric surface basis rather than negating all normals indiscriminately.

## Chosen implementation

One saved derived Mesh asset holds corrected base normals/tangents and reconstructed normal/tangent deltas for every original blendshape frame. Geometry, triangle winding, UVs, skinning weights, bindposes, shape names/frame weights/position deltas and source materials are preserved. The original FBX and ModelLibrary entry remain authoritative and unchanged.

Proposed saved assets, created only by Unity's AssetDatabase:

- `Assets/_Project/Art/Corrections/Avatar/SK_Head_Surface.asset`
- `Assets/_Project/Resources/Art/AvatarSurfaceCorrection.asset`

The profile exposes enabled state, original avatar/head references, corrected mesh, exact renderer path and source fingerprint in the Inspector. A new avatar swaps only the matching head's `sharedMesh` reference to this saved mesh. Repeated spawns share the same asset. There is no runtime mesh cloning, normal recalculation, per-frame update or new skeleton. Applying again is a no-op; custom head meshes and unrelated source prefabs are left alone. Sampled expression weights and renderer bounds are retained during assignment.

The Editor generator:

1. Verifies the exact source FBX/meta SHA-256 and the captured original head's complete semantic/attribute fingerprint. It also rechecks that face0 still exhibits the reviewed inversion.
2. Copies the mesh in memory, retains every original frame's vertex delta, and reconstructs the base normal/tangent basis from triangles and UV0.
3. For each original blendshape frame, recomputes the basis at `basePositions + originalDeltaPositions`. Stores normal and tangent-XYZ differences relative to the corrected base, retaining frame names/weights and vertex deltas.
4. Fails if any generated basis is nonfinite/zero or a frame changes tangent handedness. Unity's three-component blendshape tangent delta cannot safely encode a change in tangent W. A failure is evidence to review; it must not be weakened into a silent approximation.
5. Requires the protected geometry/skin/UV/shape-position fingerprint to match and face normals to align with the outward geometry. Creates only missing assets. A conflicting path, changed source or invalid existing authored profile fails without overwriting it.

Re-running setup validates existing assets without resetting profile preferences or rewriting the saved mesh. If first creation stops after saving the derived mesh but before saving the profile, the next run can verify/reuse that exact mesh and finish the missing profile. Temporary meshes are destroyed on failure or after use. Original mesh memory/dirty state and source file/meta identities are checked again before return.

The recipe changes derived normals and tangents, including their blendshape deltas; these are intentional changes. It does not claim byte-identical internal vertex buffers. The protected comparison covers remaining semantic values and non-surface vertex-attribute descriptors. Reconstructed smoothing may reveal UV seams; the native render review must judge them. Intermediate or combined blendshape normals are interpolated by Unity; reconstructing stored endpoints is not proof of every expression, so the test matrix exercises combined weights and skinned poses.

## Spawn and authoring routes

- `ModelLibrary.Spawn`: corrects the owned copy after instantiation, only for `Avatar/Avatar`; destroys that copy if validation throws. This covers ModelVisual, lobby leader/guests, shop portraits and Wordbound player visuals. ModelLibrary.Find, Entries and source meshes stay original.
- `PlayerAppearance.Initialize`: applies the same reference-bound correction after the selected avatar's Animator validates and before the face/mesh scan. Existing authored player avatars therefore receive the correction at runtime without rebuilding their hierarchy or changing their source prefab.
- `PlayerAuthoring.InstantiateAvatar`: new authored prefab instances receive the saved reference, with the renderer modification recorded. Existing authoring upgrade early returns remain intact to preserve artist changes.
- Existing saved EditorOnly previews/scenes are not rebuilt automatically. Select their actual avatar root and use `Wreckabulary > Art > Apply Avatar Surface Correction to Selected Instance`, then save the scene/prefab. This action uses Undo, rejects persistent asset roots and requires the exact bound original mesh. It does not modify the imported FBX.

Raw diagnostic baselines remain raw: `ShopPortraitControlsDiagnostics` explicitly instantiates the original model and applies the same standard material selection without the production surface hook. `WordboundOriginalArtworkCapture` explicitly restores the original head on its owned baseline instance before its existing strict original-mesh identity assertion, and labels that bypass in its receipt. Equipment/source identity assertions are not weakened. A future production-corrected render must be labeled separately from these original-art controls.

The actual Windows equipment smoke view keeps the corrected player. It accepts either exact original mesh identity or, only for the player/avatar key, exactly one reference-bound original-head-to-saved-corrected-head replacement with every other mesh reference/order unchanged. Its facts truthfully report `original_mesh_identity=false` and `bound_head_surface_correction=true` for the corrected avatar, alongside original and actual mesh names and the source fingerprint. It does not restore wrong normals to satisfy QA. A targeted test rejects a second changed mesh. `WordboundPerformanceWorkload.ObserveArtwork` was inspected: its only call checks the eight equipment items, never the actors, so that existing strict equipment identity assertion remains unchanged.

## Prepared package

`candidate/` contains eleven C# files in their eventual project-relative locations. `baseline/` contains the exact current versions of the six modified files. Five files are new: runtime profile, Editor generator, mesh-data fingerprint helper, focused Editor tests and a PlayMode integration test. `avatar-surface-fix.patch` combines these changes; `manifest.json` records per-file hashes, source HEAD, capture04 receipt hash and the strict patch-check result. No `.meta` or serialized asset YAML was authored by hand.

Current patch SHA-256: `3df3e6f1b606bcbb3287ce50922ea1d547151a03ee471f979588fd1ea2f4f69d`. Strict `git apply --check --whitespace=error-all` passes against the saved current baselines. A first patch serialization used Windows CRLF and was rejected as trailing whitespace; the patch was regenerated with LF and passed. This was a packaging failure, not a source/native test failure; no live source was applied.

Root execution after review and baseline recheck:

1. Apply the patch, let Unity create metadata, and run the repository's required cheaper checks before a native rung.
2. Through the official gate, invoke `Wreckabulary.EditorTools.AvatarSurfaceCorrectionSetup.CreateOrVerify`.
3. Run focused EditMode filter `AvatarSurfaceCorrectionTests` and PlayMode filter `AvatarSurfaceRuntimeTests`.
4. Run setup again and preserve source/output hashes, then capture an actual corrected shop/lobby preview through the normal production spawn route. Check face, hair, eyelids, brows, normal-mapped material detail and UV seams under the same lighting as the original.
5. Test Play Mode and the Windows build for avatar expressions, movement/attack and shared saved references. Existing original-art diagnostic images intentionally remain uncorrected controls.

Ten prepared Editor cases cover: saved geometry/skin/shape-position preservation; fresh repeated spawns sharing one saved mesh; authored pose/weights/materials/bounds/property blocks; unrelated/custom/source assets unchanged; exact-head identity allowance rejecting a second changed mesh; new Editor previews receiving the saved correction while existing preview overrides remain unchanged; repeat setup preserving source/authored/font bytes, clip identities and loaded/active scene roots/dirty state; and three motion clips (`Idle`, `Walk_InPlace`, `Swing_OneHand`) each with all nine combinations of Blink/BrowRelax at 0/50/100. The latter compare original/corrected baked positions and verify corrected normal direction across 27 pose/expression combinations. One PlayMode case exercises the actual `PlayerAppearance.Initialize` path on an existing authored player and checks reused hierarchy, artist offset, corrected shared mesh and idempotence. They are tests to run, not passed results. They do not replace clips of continuous transitions, animation blending or GPU/player verification.

## Acceptance boundary

- [x] Actual capture04 images and source/normal telemetry reviewed.
- [x] Reference-bound saved-mesh recipe and minimal spawn/authoring hooks drafted.
- [x] Raw original diagnostic baseline semantics preserved in the draft.
- [x] Strict patch application check passed.
- [x] Independent source review and C# parsing (`review.md`); eleven files, zero syntax errors, no blocking source finding.
- [ ] Native generation, ten Editor cases and one PlayMode case.
- [ ] Repeat-generation/source/output preservation verified in Unity.
- [ ] Corrected render comparison, expression/animation motion and Windows build acceptance.

No appearance or production-readiness claim is made from this source draft alone.

# Original course surface UV review

STATUS: SOURCE PASS — final Assets commit **`dbaf65c2`**. No compiler, Unity or player run by this reviewer.

## Scope

Review the Assets lane change to `WordboundCourseWorld` and its existing native Editor fixture. Physical geometry, world transforms, colliders, original shared materials, course content and collision callbacks must remain unchanged. Check the original `Art.Surfaces.Box` / `Tile` conventions and disposal paths, including direct parent destruction and construction failure.

## Confirmed finding in the first source checkpoint

The initial delta used `Art.Surfaces.Box(size, tile, position)` for each default floor/wall. That API keeps meshes in a static dictionary keyed by dimensions, tile and origin, with `HideFlags.DontUnloadUnusedAsset`. Edited/randomized course positions and cutout sizes therefore add persistent native meshes across world disposal. Same-key rebuild reuse does not establish bounded lifetime: the proposed test explicitly kept those meshes alive and did not vary the course.

Reported to Assets and root before integration. Requested bounded world-owned UV meshes and disposal/direct-destruction/failure coverage, without populating the original global cache for every course position. Original shared materials must remain borrowed.

## Checks already traced

- Floors/walls use resolved course coordinates and identity course rotation; parent translation/yaw is applied by `SetCoursePose`. Metre-scale UVs should follow those course coordinates so adjacent split pieces align.
- Connectors use a local box rotated along the actual endpoint vector. Local wood grain should measure the real slope length and width without changing endpoint geometry.
- Original `Surfaces.Box` uses 24 face vertices, outward face normals, unit-cube bounds and recalculated tangents. The normal-mapped original material requires valid tangents after changing UVs.
- `WordboundCourseWorld.Dispose` alone is insufficient for owned mesh cleanup because existing tests and callers can destroy its parent directly. Failed construction also needs cleanup before the partial world disappears.

## Corrected implementation and closure

Root authorized the additional `Art/Surfaces.cs` path. Existing `Box` keeps its cached behavior; new `CreateBox` shares the same mesh builder with cache lookup/insertion disabled. Vertex positions, outward face normals, triangle indices, UV calculations, bounds and tangent generation are unchanged. Only the new owned path uses `DontSave` instead of the original cache policy; explicit destruction provides its lifetime. The read-only `MeshCount` does not change cache behavior.

`WordboundCourseWorld` records each new mesh before assigning it or invoking the existing collision callback. `Dispose` releases those meshes before destroying the root. `OnDestroy` also releases them for direct parent/world destruction, and clearing the owned list makes both paths idempotent. Construction exceptions already call `Dispose`, covering partially generated inactive worlds. Player destruction remains deferred according to Unity's `Destroy`; actual deferred-player cleanup still needs runtime verification. Borrowed original meshes, custom-material primitive meshes and shared materials are never entered into the owned list.

Default floors/walls use the original Planks/Plaster tile sizes and course-space origin. Default wood connectors use the original local-origin/grain convention along actual slope length. Supplied materials keep the prior primitive mesh path. The diff does not change transform positions/rotations/scales, native box colliders, tags, collision callbacks, course data or material selection.

The **6 new cases / 17 total CourseWorld cases** meaningfully cover:

- Actual unequal cutout floor/wall metre UVs and shared seams under a translated/rotated parent; the old stock 0–1 cube UVs fail these expectations.
- Reversed horizontal Stairs and Ramp slopes, full length/width tiling, unchanged native box dimensions and exact top-surface endpoints.
- Custom/default comparisons of world transform, scale, collider geometry, mesh bounds, manifest and callback count; borrowed materials/custom stock meshes remain valid across rebuilds.
- Exact cached/uncached builder vertex, normal, tangent, UV and index parity.
- Three changed course sizes/origins without global mesh-cache growth; actual Editor native-mesh destruction on Dispose, direct parent destruction, and failure after the third configured collider.

The existing four `SurfacesTests` continue to cover the original cache and outward winding/tangent contract. No concrete source blocker remains. Targeted native filters are `Wreckabulary.Tests.WordboundCourseWorldTests` (17) and `Wreckabulary.Tests.SurfacesTests` (4). These source checks do not establish rendered texture quality, player memory behavior or Windows acceptance; those require root's native/player gates.

## Native lifetime failure and bounded Editor repair

Directly read `TestResults/editmode_20261023T210954Z.xml` on source `752a8193`: 24 total / 23 passed / 1 failed / 0 skipped. `ChangingCoursesReleaseUvMeshesOnDisposeParentDestructionAndConstructionFailure` failed at line 410 because at least one owned mesh remained. The old assertion did not label the iteration, so this artifact alone does not identify whether it was explicit Dispose or parent deletion. The other UV/surface cases and all three camera cases passed. Do not generalize the earlier source verdict into runtime lifecycle acceptance.

Reviewed Assets repair **`2a623b4b`**, bounded SOURCE PASS: add `ExecuteAlways` to `WordboundCourseWorld` so its existing `OnDestroy` cleanup is eligible in Editor/Prefab mode. The installed Unity 6000.6.3f1 CoreModule XML documents that callback context. This component has no Awake, OnEnable, Update or other periodic Unity message; the change adds no edit-time construction loop. Owned-only, idempotent cleanup and geometry/material behavior remain unchanged. Strict leak assertions remain and now identify revision, explicit Dispose versus direct parent destruction, and retained mesh count; failed-construction diagnostics are also labelled. The attribute is a source-backed repair for the lifecycle context, but only the native rerun can establish that it closes the observed failure. Deferred player destruction and rendered acceptance remain separate.

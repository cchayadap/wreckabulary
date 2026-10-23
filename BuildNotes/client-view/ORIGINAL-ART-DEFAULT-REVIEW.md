# Original artwork default review

STATUS: PASS — bounded independent source review. Native execution and rendered acceptance remain pending. No production edits or native jobs.

Scope: recover_online's forthcoming Base Artwork dropdown on WordboundVisualProfile, resolver default/override compatibility, assigned Presentation asset, and native original mesh/bounds/grip coverage. Root owns rendered art and native gates.

Current integration evidence: LaunchAssets.asset references the assigned Presentation.asset visual profile; that profile currently has DefensePose and an empty Overrides list. WordboundVisualResolver creates overrides first, otherwise resolves canonical model keys through the optional Wordbound equipment collection or original ModelLibrary. ModelVisual's original branch retains the existing skin/tactile material behavior. The gameplay physics bodies are separately authored; a visual choice must not change canonical collision geometry, gameplay rules or signed content bindings.

Review checks: Original Wreckabulary is serialized default for assigned profiles; explicit Wordbound Equipment selection remains available; null-profile legacy behavior remains unchanged; authored overrides retain precedence; inspector labels/enum values/validation are clear and stable; native assertions compare actual original mesh identity and vertex bounds/grip alignment rather than names alone. Native/render acceptance is not inferred from source.

## Reviewed implementation

Reviewed the stable recover_online delta, now committed as `f163361`, after baseline-only `0b8df7c` (do not integrate that baseline). Files: `WordboundVisualProfile.cs`, `WordboundVisualResolver.cs`, `Tests/WordboundPresentationTests.cs`, and `docs/wordbound/UNITY-EDITING-GUIDE.md`. Source review found no concrete blocking defect.

- `OriginalWreckabulary = 0` is the field initializer and the missing-field value for existing profiles. `WordboundEquipment = 1` is an ordinary serialized Unity enum option. Validation rejects unknown values. The Inspector regression checks the serialized enum and displayed labels.
- Collection selection retains the caller's existing enable flag and additionally requires the explicit equipment option when a profile is supplied. A null profile retains the original flag-only choice. `Create` still tries per-item overrides before either library; artwork selection does not change item statistics or physics bodies.
- In integration checkout `abaa6b2`, `Resources/Wordbound/LaunchAssets.asset` references `Data/Wordbound/Authoring/Presentation.asset` (GUID `8513f12a73657d54393cf5b4d5749239`). The existing profile omits the new field and has no overrides. This supports the actual launch default, rather than only a new temporary profile. The prepared asset is absent in the online worktree; execute native tests in the assembled integration project.
- Original models continue through existing skin/material handling. The guide explicitly identifies the original ModelLibrary models, distinguishes both optional generated revisions, and says to recreate the playtest after changing the captured source choice. It does not promise live rebuilding or unchanged imported material bytes.

## Native coverage reviewed, not executed

The exact filter is `Wreckabulary.Tests.WordboundPresentationTests` in Editor-only assembly `Wreckabulary.Online.Presentation.Tests`. Expected count: **48 cases**, zero skipped.

| Cases | Assertions inspected |
| ---: | --- |
| 12 | Actual assigned default profile; exact original shared mesh references; mesh vertices fitted/grounded to canonical dimensions; grip contact before and after rotated hand placement; held scale/two-hand policy; no 3D or 2D physics components. |
| 12 | Explicit optional collection profile; exact collection meshes, real vertex bounds and retained collection materials. |
| 4 | Actual mesh selection under caller enable/disable and null-profile compatibility. |
| 1 | New profile default, serialized Inspector enum labels and invalid-value rejection. |
| 8 | Canonical/custom overrides with/without authored grip under both artwork choices; own bounds/materials/grips retain priority. |
| 11 | Existing held artwork, non-item, wardrobe and missing-mapping coverage retained. |

The added tests use types already supplied by the test assembly's direct references. `SameMeshes` compares mesh asset identities and multiplicity; `VertexBounds` reads mesh vertices in the target coordinate space. Original fallback grip calculation is equivalent to production for these item models because their authored-orientation node is identity. The hand is translated and rotated before placement, so contact checks cover transform handling rather than a coincident origin.

No compiler, Unity, renderer or Windows test was run in this review. Root must obtain the 48-case native result and inspect the default originals in the actual game camera, plus an explicit optional collection and an override. Source correctness alone is not artwork approval or multiplayer acceptance.

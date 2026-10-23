# Windows render material retention review

Status: bounded independent SOURCE PASS for the current generated material pair; rebuilt Windows appearance and shader retention NOT RUN by this lane. Reviewed root source `efb7131` and its Unity-generated Resources material files. No runtime, root or asset edits were made.

## Reviewed behavior

- `Scripts/Art/WordboundRenderMaterials.cs` loads the opaque or transparent Resources template and returns `new Material(template)` with `DontSave`. Callers receive a clone; they do not recolor or modify a shared asset.
- `Editor/Online/WordboundRenderAssetsSetup.cs` creates two separate URP Unlit materials under `Assets/_Project/Resources/Wordbound/Render`. Both reference shader GUID `650dd9526735d5b46b79224bc6e94025`. The installed URP Unlit shader declares `_SURFACE_TYPE_TRANSPARENT` as a local shader feature. The material pair provides explicit build dependencies and both keyword states instead of relying on a runtime-only shader-name lookup.
- The generated `Surface.mat` is opaque, queue 2000, blend 1/0, alpha blend 1/0, ZWrite 1, with no transparent keyword. `TransparentSurface.mat` has the transparent keyword, queue 3000, blend 5/10 (SrcAlpha/OneMinusSrcAlpha), alpha blend 1/10 and ZWrite 0. These are observed serialized values, including URP's generated alpha settings.
- `Scripts/Workshop/WordboundWorkshopViewport.cs` still copies the same source texture and color onto its five owned clones. Its transparent ghost path retains the existing blend, depth-write and queue properties. `Dispose` destroys all owned materials, the render texture and preview stage; `WordboundWorkshopInteraction.OnDestroy` invokes it.
- `Scripts/Online/Presentation/WordboundPuzzleGuide.cs` uses an opaque clone, retaining white base color and per-line property blocks. `WordboundPuzzleGuideView.Dispose` releases the clone and guide objects; the controller invokes it during clear, disable, destruction and failed/changed world binding. Font material usage is unchanged.
- The workshop's separate `Surfaces.Get` source material cache is unchanged. `Resources/GameAssets.asset` has a real `tintBase` reference, so this reviewed configuration does not rely on its Lit `Shader.Find` fallback.

Material SHA-256 at inspection:

| File | SHA-256 |
| --- | --- |
| `Surface.mat` | `A32E232A966BAF71FB842B692F60C69ABBFA2636AE91611E305298D6EFEDD769` |
| `TransparentSurface.mat` | `F11E56F18353BB9697492A5311448E8D501E79A2D7A3E3FDD955859BC3E97B1B` |

## Remaining limit

The setup verifier checks shader, required texture/color properties, surface value and transparent keyword. It does not reject an existing material with conflicting blend factors, ZWrite, queue or RenderType tag. The currently generated pair has the correct values, so this is a bounded verification gap for a later edited template, not an observed defect in these artifacts. Root was informed; preserve edited assets rather than silently overwriting them if the contract is tightened.

Rebuild and execute the Windows workshop scenario and an actual guide-bearing course. Inspect opaque floors, translucent adjacent layers, texture/color continuity and guide lines; check player logs for missing material/shader errors. Source references alone do not prove the built variant survived stripping or that the rendered result is acceptable. No Unity, compiler, player, Blender or other heavy job was started for this review.

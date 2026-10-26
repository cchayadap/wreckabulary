# Portrait controls

Root copied both reviewed source files into `Assets/_Project/Editor/Art/` with matching SHA-256 hashes on 2026-10-26. Compilation and execution are pending. The copies below preserve the reviewed draft. These diagnostics do not change production portrait generation, source materials, import settings, saved lighting or runtime shaders.

## Root-owned execution

After the running native work has completed, root can review and copy `ShopPortraitControlsDiagnostics.cs` and `PortraitPureAlbedoDiagnostic.shader` into `Assets/_Project/Editor/Art/`, allowing Unity to generate metadata normally. No project/assembly changes are required: the existing `Wreckabulary.Editor` assembly supplies the same dependencies as the original probe. Run root's source check, then the existing gate's cold exec:

```text
exec Wreckabulary.EditorTools.ShopPortraitControlsDiagnostics.Run --cold
```

The method refuses any existing `BuildNotes/developer-workspace/portrait-controls/capture-01` path. It creates five new PNGs and one `diagnostics.json`; each individual file uses FileMode.CreateNew. A partial failure remains as evidence and prevents an accidental same-path rerun. A subsequent experiment needs a separately reviewed new output constant. This draft has not created that capture folder.

## Fixed inputs and isolated changes

All cases use the original `Avatar/Avatar`, the courier outfit, sampled Idle at time zero, holder yaw 160°, the original 256×320 framing recipe, and A's bounds/camera for the remaining cases. Each case owns a fresh PreviewRenderUtility and temporary avatar instance. Wardrobe renderer/per-material property blocks remain assigned throughout the material swaps.

| Output | Change from A |
| --- | --- |
| A-original-lit.png | Existing original URP Lit materials and original preview lights. |
| B-no-normal-map.png | Owned clones disable the main normal keyword/map and set detail normal map/strength to flat/zero. Detail-albedo keywords remain unchanged. Unexpected detail-normal property-block overrides fail closed. |
| C-urp-unlit.png | Owned URP Unlit materials copy source base map/color, UV transforms, culling, blending and cutout state; source property blocks remain. |
| D-frontal-key.png | Original materials; only the primary preview light rotation changes to the camera rotation. Light color/intensity, fill and ambient remain unchanged. |
| E-pure-albedo.png | Owned diagnostic materials use the same base map/color/UV and property blocks. The small diagnostic shader samples albedo directly, with no normal input, BRDF, AO or fog code. |

C is deliberately separate from E: the installed URP Unlit forward pass still multiplies screen-space AO and applies fog. The project PC renderer has an enabled SSAO feature, but this source review does not prove that the preview camera executes it. Pre/post-render SSAO keyword observations are context, not proof that a pass ran. E bypasses those operations in its material pass; camera/render-target/PNG transfer and any renderer-level processing remain part of the capture path.

## Evidence and cleanup

Telemetry records source and rendered material shader/keywords/properties, texture asset paths/GUIDs/graphics formats/import settings, both property-block levels, albedo override scope and effective base color/map/UV transform. Property-block texture entries use neutral scale/offset placeholders because those properties are separate vectors; inspect the recorded `_BaseMap_ST`/other vector entries and `effectiveBaseMapST` for actual UV overrides.

Each image records SHA256, captured Texture2D format/graphicsFormat/isDataSRGB, project color space, framing, camera transform and full preview light colors/intensities/rotations. The source texture's runtime isDataSRGB and importer sRGB flag are recorded separately. These are inputs for investigating transfer/encoding; they do not prove a PNG transfer fault merely because C or E differs.

Cleanup independently attempts destruction of the returned image, preview scene/model, fallback holder and every owned material, then restores RenderTexture.active. No mesh or source-texture clone is created. Failed cleanup is retained and fails the probe. No SaveAssets, ImportAsset, SaveAndReimport, CreateAsset, PlayerPrefs write or RenderSettings assignment occurs.

Before/after proofs compare tracked source file and metadata hashes; tracked original materials also compare serialized in-memory state and dirty flags. The active scene identity/dirty state and selected global lighting/fog values are compared without repairing them. This is bounded preservation evidence, not a claim to fingerprint all Unity/editor state. Original textures/meshes/materials are never placed on the owned destruction list.

## Current evidence and hypotheses

- Original `ivory_vinyl.mat` is URP Lit with white base color, metallic/smoothness zero, normal map enabled and strength one. The albedo importer uses sRGB; the normal importer is linear NormalMap with no green-channel flip. The FBX explicitly remaps ivory_vinyl to this material and imports normals/calculates Mikk tangents.
- Existing A and B PNGs are byte-identical. Existing C differs; the sampled face pixel (128,100) is RGB105/101/92 in all three. That is a bounded image observation, not an artistic approval or a whole-face statistic.
- Existing diagnostics report 6,630 finite, nonzero, approximately unit head normals/tangents in all three cases. This excludes missing/zero arrays; it does not establish correct directions or tangent-space interpretation.
- `PlayerAppearance.Dress` contains no head-tint branch and assigns empty per-slot blocks for the head. It does not clear a renderer-level block. The original probe did not record those blocks, so the new telemetry checks actual inputs instead of inferring them solely from the saved material.
- B can distinguish sensitivity to normal-map processing. D can distinguish sensitivity to key-light direction. C versus E can separate built-in Unlit effects from the pure material sample, while retaining the shared render-target/encoding path. No outcome alone establishes a historical root cause.

API source check: Unity documents `MaterialPropertyBlock.HasProperty` for both property names and IDs: [Unity 6 API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MaterialPropertyBlock.HasProperty.html). Installed source was used for the actual URP Unlit/AO/fog behavior. Compilation, shader import, captures and visual acceptance remain NOT-RUN.

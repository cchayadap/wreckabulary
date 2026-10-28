# Workshop depth / cutaway independent source review

STATUS: COMPLETE — no blocking defect found in the exact three-file source change. Native and actual image verdicts remain separate.

Scope: frozen three-file manifest `dc684301474e86a588b12af0c3556272962c6ca244fc98f3a0b45c7ae8bb44fe`, patch `9a62bbef3b822d43c41f9767e378931327521d76bf3fce9324fd5bc44df33d7a`. Read-only production review; this report is the only write. Root owns integration, native tests and actual image comparison. Review checks material clone/borrowed ownership, unchanged transparent ghosts and isolation, wall-fixture omission without authored-data mutation, and meaningful coverage.

The manifest and patch hashes match the requested values. All three baseline hashes and all three candidate hashes match the manifest. At final inspection, all three live files match the candidate hashes; root confirmed its exact integration had occurred. The five-assembly source-check receipt contains zero errors. I did not rerun compilation or native processes.

## Findings

No blocking source defect was found in the bounded change. The following conclusions are based on the exact patch and surrounding implementation, not a rendered result:

- **Owned opaque clones:** `WordboundWorkshopViewport.cs:83–99` now clones the canonical `Surfaces.Get` material for opaque surfaces, then changes only the clone's color/name/hide flags and same albedo reference. `Surfaces.cs:40–65` supplies the original Lit shader, normal-map reference/keyword and surface parameters. The source material is never written or destroyed. Every generated clone joins `ownedMaterials`.
- **Borrowed authored material stays borrowed:** viewport `FloorFor` at lines 166–175 still returns an explicitly assigned `FloorMaterial` directly without adding it to either owned collection. Per-layer generated variants are removed from `ownedMaterials` and destroyed on source sync at lines 120–121; the remaining clones are released by `Dispose` at lines 376–387. No shared texture or cached mesh is destroyed by this patch.
- **Ghost behavior remains unchanged:** alpha-below-one materials still use `WordboundRenderMaterials.Create(true, ...)` with the same blending, transparent keyword, queue and disabled depth writing. The adjacent-floor choice still uses `ghost`. New `Box` renderer flags at lines 228–230 keep `_Surface`-transparent ghosts from casting/receiving shadows. The changed opaque flags allow shading/shadows but do not guarantee visible shadows: the two existing preview lights still use `LightShadows.None`.
- **Isolation and picking are preserved by the diff:** camera culling mask, layer 31, stage offset, scoped begin/end-camera light callbacks and callback teardown are unchanged. Geometry dimensions, UV construction, canonical documents, fixed-zone mapping, camera position and picking paths are unchanged. No RenderSettings or global light mutation is introduced.
- **Cutaway omission preserves authoring:** the one removed call at the module loop stops wall-only fixture instances from appearing without their walls. The provider is still read and validated. Its list/rows, prefabs, material references and course data are not rewritten or cleared. Runtime and thumbnail composition are untouched by this three-file patch, and the full-world fixture test is retained.
- **The adjusted tests cover the intended contract:** the material test checks distinct clone ownership, original shader/keywords and every texture reference/scale/offset, source immutability, borrowed-material survival and generated-clone release. The cutaway test checks fixture absence while a floor still renders, source switching/hiding, source prefab/material survival and unchanged authored row data. The unchanged runtime test still checks fixture presence with the same canonical hash/collider shapes and cleanup. They do not establish final lighting quality or inspectability.

## Limits and next evidence

The `_Surface` transparency check is appropriate for the project's URP materials; this review does not promise shadow classification for arbitrary third-party transparent shaders without that property. No such new material is introduced here.

Root's targeted native gate `212943` was running at review closure. It must independently establish the 13 targeted cases. The next unchanged Windows screenshots must show whether the original furniture/floor details are clearer, adjacent floors remain unobtrusive, selection/handles remain readable, cutaway fixtures no longer float and full gameplay fixtures stay attached. The current 211157 Workshop screenshots remain the dated before evidence. The separate PreviewStage fill/soft-shadow work is outside this manifest and outside this source verdict. No production, image or audit evidence was changed by this review.

## Exact candidate identities

| File | SHA256 |
|---|---|
| `WordboundWorkshopViewport.cs` | `e07ee006f1e7e52559227733014d0a793293d68ecd1eba8b9ef5c81b48e3c210` |
| `WordboundModuleDressingTests.cs` | `18a301a8b2312322d4ba5d6ddf5f705ab69f616d370885e677df5a3bd42b7746` |
| `WordboundCourseMaterialTests.cs` | `e099a7211f8d8697d93b0b4f06202fab83545f8b4f5ffb83c47ccaf6d0f201e6` |

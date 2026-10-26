# Portrait preview light-path review

STATUS: COMPLETE — read-only source/receipt investigation after parent reports native portrait-controls capture01 PASS at 082029 (23.6s). No native job or live Assets edit occurred here.

Question: beforeRender telemetry records disabled PreviewRenderUtility lights, but Render may temporarily enable them. Trace the actual utility and installed URP preview renderer before assigning cause; recommend one discriminating capture or bounded material-preserving fix.

## Confirmed facts

**The pre-Begin light setters are not overwritten by the inspected utility.** Official UnityCsReference branch **6000.6** creates the two lights disabled. BeginStaticPreview calls InitPreview, which configures the render target/viewport and enables lights. Render enables them again before Camera.Render and sets temporary ambient lighting. Neither method resets their rotation, intensity or color. EndStaticPreview's FinishFrame disables them. Our telemetry at ShopPortraitControlsDiagnostics.cs:261 precedes BeginStaticPreview at265, so enabled=false is expected at that point, not proof of absent rendered lights. The production ShopPreviewImporter.cs:54–60 setter order is therefore not itself the bug. Reference: [official 6000.6 PreviewRenderUtility source](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.6/Editor/Mono/Inspector/PreviewRenderUtility.cs) (InitPreview298+, FinishFrame376+, Render504+). This is branch source inspection, not a binary-exact decompilation of the installed patch editor.

Installed **URP17.6.0** (`Library/PackageCache/com.unity.render-pipelines.universal@b0678dfc9e21`) emits preview geometry then culls normally (UniversalRenderPipeline.cs:979–1026). CreateLightData/GetMainLightIndex selects the visible sun or brightest visible directional when main-light mode is PerPixel (2232–2255,2392–2437). UniversalRenderer.SetupCullingParameters (632–675) has no blanket preview-camera direct-light suppression. ForwardLights.cs:711–725 uploads main-light direction/color. PC_RPAsset has main-light mode1 (PerPixel); PC_Renderer mode2 is Forward+. These inspected paths support preview directional lighting; they do not establish this head's actual per-object/per-pixel contribution. Render-time SSAO/lighting state cannot be proved from before/after keywords alone.

Actual capture01 diagnostics report Unity6000.6.4f1, URP PC_RPAsset, Linear color space, D3D11 Intel Arc, all outputs R8G8B8_SRGB/isDataSRGB=true, all34 source proofs unchanged, global state unchanged and zero remaining owned objects in all5 cases. I visually inspected D and E and sampled original PNGs read-only; the face difference is confirmed. Head slot0 uses original ivory_vinyl Lit, white base color, metallic0/smoothness0, empty renderer/slot property blocks. Removing normal maps barely changes the sampled face.

| Capture / sampled RGB | Face128,100 | Face118,110 | Face140,110 | Hoodie128,180 |
| --- | --- | --- | --- | --- |
| A original Lit | 105/101/92 | 108/102/93 | 106/101/92 | 93/141/112 |
| B no normal map | 106/101/92 | 108/102/93 | 106/101/92 | 93/141/112 |
| C URP Unlit | 237/226/201 | 238/226/202 | 238/227/202 | 115/179/139 |
| D frontal key | 105/101/92 | 108/102/93 | 106/101/92 | 105/160/126 |
| E pure albedo | 243/231/206 | 243/231/206 | 243/231/206 | 117/180/140 |

D changes the hoodie while its three face samples equal A exactly. Therefore the changed key direction has an image effect; it was not globally neutralized by BeginStaticPreview. This focuses the uncertainty on the head's lighting response. Misdirected/imported/deformed normals are a hypothesis, as is per-renderer light participation; no cause is proved. The importer stores imported-normal settings (Avatar.fbx.meta:648–655), but source settings and finite unit-length normals do not prove the rendered normal direction. No normal recalculation/importer repair is justified yet.

## One bounded next capture

**F-back-key:** repeat D with original Lit materials, identical idle sample, pose/yaw, camera/framing, texture output, key intensity/color, fill and ambient. Change only the primary light direction:

```csharp
preview.lights[0].transform.rotation = camera.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
```

This is the opposite of D's frontal primary light; keep its other settings identical. Retain the existing source/global/owned-object preservation assertions and a unique refused-overwrite output. Add observational light snapshots immediately after BeginStaticPreview and immediately after Render, before EndStaticPreview; record rotation/intensity/color/enabled and camera type. Do not call a new render, mutate scene/global lighting or add a card/shader/material/mesh for this control. Avoid explicit enabled=true as a claimed repair; the utility already does that.

- If F brightens the face relative to D while the source material remains identical, it supports a head surface responding to the opposite light hemisphere. Next inspect rendered head normals/skinning/import orientation; do not adopt back-lighting as the production fix or recalculate artist normals blindly.
- If F remains dark, the specific opposite-facing-normal hypothesis is not supported by this control. Preserve that result and investigate per-renderer light selection/layers and actual shader inputs before proposing an asset/material change.

The existing albedo controls show the source can produce a cream face through this capture/output path. They do not establish an acceptable Lit portrait or justify replacing production materials with Unlit. This recommendation is one discriminating direction-only capture, not an executed fix or art acceptance.

## Head/body follow-up review

STATUS: IN PROGRESS — append-only review while root owns the core suite and later capture02 execution. Inspect all five capture01 images, bounded material telemetry and available original avatar/hierarchy data for renderer-specific lighting, transforms or skinning differences. No source edit, native/GPU job or new capture is performed here; the applied rear-key control remains root-owned.

STATUS: COMPLETE — all five capture01 PNGs were opened with `view_image`; bounded head-slot JSON, original face/hair material YAML, current spawn/dress code and the earlier pose-probe hierarchy/mesh receipt were inspected. The earlier observations and failure history above remain unchanged.

Confirmed image/material distinctions:

- A/B/D show a dark grey face and blue-grey hair; C/E show a cream face and dark brown hair. D visibly changes the hoodie without a comparable face change. B actually clears `_NORMALMAP` on all three head slots; its near-identical face therefore weakens a normal-texture-only explanation. It does not remove mesh or blendshape normals.
- `Avatar/SK_Head` holds face, eyes and hair as three material slots. The face uses original `Assets/_Project/Materials/Library/ivory_vinyl.mat`: white base color, metallic0, smoothness0, no metallic/specular/occlusion mask, and empty whole-renderer and per-slot property blocks. Hair uses original `hair.mat`, metallic0/smoothness0.38 and a dark brown base color approximately (0.190,0.138,0.106); it references the neutral orange fabric base texture and orange fabric normal texture. A hair reflection/specular hypothesis must not be transferred to the rough face without evidence. No material repair is justified by these values.
- Current `ModelLibrary.Spawn` instantiates the imported model directly. `PlayerAppearance.Dress` changes wardrobe visibility and eligible slot tints; capture01 confirms no hidden head tint. The diagnostic does not use the legacy `Player.prefab` primitive head, so that prefab's nonunit scales are irrelevant to this capture.

The earlier `BuildNotes/experience-refresh/portrait-probe-20261026/diagnostics.json` gives useful actual hierarchy evidence, separate from capture01: A-sample, B-playable and C-baked all have unit local scales along Avatar → Avatar_01 → root → pelvis → spine → chest → neck → head. Their world 3×3 determinants are positive, approximately 1.00000008–1.00000030. `SK_Head` and `SK_Hoodie` have identical world matrices (determinant1.00000018) and the same root bone. This weakens gross mirrored/nonunit object or ancestor-transform explanations. It does not validate mesh bindposes, every weighted vertex or final shader-space normals.

The concrete head-only deformation difference is `Blink=0`, **`BrowRelax=100`** in all three earlier pose methods; the hoodie has no blendshapes. Head source normals are finite, nonzero and approximately unit length, but that does not establish their direction at visible pixels. Negative tangent W values occur on both head and hoodie and are not by themselves invalid handedness. The FBX importer stores imported-normal mode0, blendshape-normal mode1, `legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes=1` and `calculateBlendshapeNormalsDeltaFromImportedNormals=1`. These settings and the fully applied blendshape make the head's normal-delta path worth checking; they are not proof of defective normals. A baked pose retains the deformation, so the earlier three-method agreement does not exclude it.

Discriminators, in order:

1. Read the already queued capture02 A/D/F result first. Compare actual head/hoodie rendering-layer masks, probe usage and receive-shadows fields, plus post-Begin/post-Render light state. Capture01 did not record those renderer fields. A difference may identify a participation seam; equal fields alone do not prove identical shader light inputs.
2. If the opposite key brightens the face, examine the rendered/baked head normal orientation relative to that light. Do not adopt rear lighting as a production fix. If it stays dark, preserve that negative result and do not call it proof that all preview lighting is absent.
3. Only if uncertainty remains after that result, one instance-only `BrowRelax=0` control with identical original Lit materials, pose, camera and lights can separate the blendshape-delta hypothesis from the unchanged base normal/light path. Record the sampled weights and owned-object cleanup; do not edit the source animation, FBX importer or materials. No such control was written or executed here.

The shared output path already yields cream pixels under both albedo controls, and the hoodie responds to the frontal key. That weakens a blanket PNG transfer or globally disabled-light explanation, while leaving Lit shading, head normals and per-renderer participation unresolved. No cause, visual acceptance or production fix is claimed.

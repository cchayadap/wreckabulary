# Optional brow/normal portrait control

STATUS: DRAFT COMPLETE — source draft outside Assets only. Root will first inspect capture02 and decides whether this control is warranted. No compilation, native/GPU capture or live-source edit was performed here.

Scope: current original-Lit baseline versus a fresh matched preview instance with only SK_Head's BrowRelax weight set to zero after Idle sampling and outfit application. Fresh refused-overwrite capture03; preserve original materials, mesh references, camera, lights and framing. Record every blendshape name/weight before/after plus telemetry from owned temporary baked head/hoodie meshes. Never recalculate normals or assign the telemetry meshes to renderers.

## Prepared source

`baseline/ShopPortraitControlsDiagnostics.cs` is the exact live diagnostic at preparation, including root's capture02 changes and explicit Light rendering-layer-mask conversion. `candidate/` contains the optional single-file change; `brow-normal.patch` targets only `Assets/_Project/Editor/Art/ShopPortraitControlsDiagnostics.cs`. `manifest.json` records HEAD, source/patch hashes and the six preserved capture01 files. Capture02 and capture03 were absent at this check; root still owns their execution.

- Baseline SHA256: `2da5c14a19b50570e3348ef6053b562c518c95bb3354f766a5e6d0f27ac1cef9`
- Candidate SHA256: `fdb75e67fd1dfdd3135f0093bd40508fa0e803d46f0f7fbf91bf7439c4a8facc`
- Patch SHA256: `74e8393b5a43d495e27029d426d070d3650ab031e8f81e3946cbf7f55ab4ca81`

Patch is 84 added/3 removed lines. Read-only `git apply --check --whitespace=error-all` passes against that live baseline; candidate trailing whitespace count is zero. Independent read-only review by `/root/story26` found no concrete blocker in cleanup, comparison isolation or normal-coordinate semantics at the candidate hash above. This is not compilation or native validation. Do not apply if the live source hash has changed without reviewing/rebasing the draft.

## Exact comparison and telemetry

The selected cases are `A-original-lit` and `G-brow-relax-zero`, under new refused-overwrite `capture-03`. Both use the baseline angled key, fill, ambient, original Lit materials, sampled Idle, courier outfit, yaw and baseline-derived framing. G changes only the owned SK_Head renderer's exact `BrowRelax` weight to zero; Blink and other blendshape weights are retained. It fails explicitly if BrowRelax is missing or already zero, avoiding a misleading no-op comparison. The previous B/C/D/E/F branches and shader prerequisites remain unused to keep this change bounded.

Each case records both SK_Head and SK_Hoodie with source mesh path/name, world matrix, every blendshape name and before/after weight, vertex count and normal summaries. Two temporary `BakeMesh(..., false)` meshes are owned only for telemetry. Rendering continues through the original SkinnedMeshRenderers and original shared mesh/material references. Each temporary mesh is registered before baking, destroyed in the existing cleanup path even after an exception, and included in owned/remaining object counts.

Normal summaries use the inverse-transpose renderer matrix on baked local normals. They report invalid/near-zero counts, transformed lengths, mean normalized world direction, and counts facing toward/away/sideways relative to the orthographic camera and primary directional light. Toward/away thresholds are dot products above +0.05/below -0.05; sideways includes both boundaries. The axes are `-camera.forward` and `-key.forward`, using the configured pre-Begin transforms; the existing post-Begin/post-Render light snapshots remain separate. These are unweighted baked vertex-normal counts before normal-map shading over the entire mesh, with additional per-submesh summaries using distinct vertex indices. Head submesh0 isolates the face from eyes/hair; counts are neither area-weighted nor restricted to visible pixels and do not model shadowing, BRDF, normal textures, light selection or actual fragment shader inputs.

## Root-owned acceptance if capture02 leaves this warranted

Apply only after reviewing capture02 and source hash, then use the existing gate entry `exec Wreckabulary.EditorTools.ShopPortraitControlsDiagnostics.Run --cold` with root's established flags. Require two cases; A unchanged sampled weights; G only BrowRelax zero; source/global preservation; two owned telemetry meshes per case and zero remaining owned objects; closed preview scenes. Compare the two face regions, head submesh0 normal distribution and unchanged hoodie as a control. A face/normal change supports the blendshape-normal path for further diagnosis, while no change weakens that specific hypothesis. Neither result warrants automatic normal recalculation or source-asset changes. Preserve any partial capture03 and choose a new output for a later retry.

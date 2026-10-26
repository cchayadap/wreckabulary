# PackingClub build-b geometry source review

STATUS: COMPLETE — independent read-only review of ee5dcd3f against 1a74d885. Root owns the Blender job. This lane runs no generation, export, import, Unity or performance job and edits only this report.

Scope: inspect the changed BAT/SHIELD construction, original-reference bounds and grip contracts, nondegeneracy/material/budget checks, and report concrete blockers before visual polish. Preserve all prior build evidence and unrelated worktree dirt.

## Result

No concrete source blocker found in the generator delta. This is source readiness for root's build/round-trip job, not a native PASS or approval of the new appearance. Compared only the generator/spec changes in `ee5dcd3f` against `1a74d885`; unrelated branch changes were excluded.

### Original-reference evidence

- Re-read the original files: BAT SHA-256 `f79c6a1465e6b1627953dc4baab9ef7a83b0cabd57b5c3ba8dbee11fc027ab15`; SHIELD `c52b3c7c23f5cdd664bba168e11b1c803bfed57aee9cdd6a4b049f0fb58f5552`. Both match the generator's fixed references. Inspected new script SHA-256: `e5a1ed9e7bad6309e316918f2ea513ff11eee44acc89d37bad097f5fc3aaeb8d`.
- Independently decoded the original SHIELD GLB POSITION floats (read-only, no Blender). The board's unique x/y pairs after the parent's -0.03 y offset are `(0,.57),(.23,.50),(.22,.18),(0,0),(-.22,.18),(-.23,.50)`. These are exactly the six corners used by new `shield_outline` at `packing_club.py:245`. Rounding the corners and normalizing the resulting outline to .46 by .57 deliberately changes the silhouette locally while retaining its envelope.
- Original GLB nodes retain the two cancelling half-turns. BAT local Grip_R is approximately `(0,.17,0)`; SHIELD local `(0,.32,-.11)` plus parent `(0,-.03,0)` composes to `(0,.29,-.11)`, subject to float precision. The generator still obtains the complete imported matrix after the dependency graph update, assigns it to each new socket, and checks all 16 matrix components at 1e-5.
- BAT source POSITION bounds are approximately x/z +/- .086 and height 0..1.018. New barrel maximum radius .086, crown top 1.018 and knob base 0 preserve these. The fuller knob, cuffs, wrap, laminate strips and star remain inside that envelope by their declared dimensions.
- SHIELD original front/back game-z extrema are approximately +.063 and -.12464504 (depth .18764504), with x +/- .23 and world y 0.. .57. New star front reaches the measured original minimum Blender-y exactly; the unchanged strap thickness is computed from original maximum Blender-y minus .110, preserving the back extreme. Board/binding depth remains between these extremes. The original rounded gameplay size .1877 is not substituted for the measured render envelope.

### Analytic budget and construction review

Counts below derive from the authored faces (quad = two triangles; cap n-gon = n-2), not executed Blender output.

| Model | LOD0 expected / cap | LOD1 expected / cap |
| --- | --- | --- |
| BAT | 2,308 / 3,000 | 1,172 / 1,200 |
| SHIELD | 968 / 1,200 | 472 / 600 |

BAT LOD1 has only 28 triangles remaining. The counts include both cuffs, both laminate strips and the new fan-capped star. SHIELD counts include the closed fabric binding, three-ring star and unchanged strap. Do not add detail to BAT LOD1 without recounting and retaining the native verifier.

No zero-sized section or invalid arithmetic was identified: crown ends in a positive .007 radius; cuff heights/radii differ; laminate/star heights fall within bat_section's interpolation domain and their x coordinates remain inside the positive surface-power base; shield normalization spans nonzero extents; the outline is simple/convex before corner rounding; binding cross-section has nonzero area; strap thickness is positive at the measured original depth. These observations support plausibility but do not replace native triangulation/nondegeneracy checks.

`mesh_part` still assigns one of the same two materials, palette indices 0..4, a UV layer, recalculated normals and positive signed orientation. New part calls use only valid palette indices; Wrap/Surface are both used in each item/LOD. No new texture, material, physics or network component is added by the delta.

### Verification retained and remaining limits

- `inspect` at line305 still enforces measured world bounds within 1e-5, complete grip matrix within 1e-5, positive triangle count within the hard budget, exact assigned two-material identities, valid face material indices and UV presence. It welds a temporary BMesh for manifold/volume inspection and rejects nonmanifold edges, nonpositive volume and loop triangles below 1e-12 area.
- `load_manifest` at603 and `verify` at644 retain exactly four item/LOD records and all eight distinct canonical GLB/FBX exports, hash checks for script/copied script/originals/blend/palette, one combined export mesh, no rig/camera/light, exact triangle count after reimport, and connected palette-image verification for both assigned materials. The new geometry delta does not weaken these checks.
- A manifold positive-volume component test does not establish that separate authored pieces never intersect, that their combined mesh is one watertight solid, or that every surface normal/silhouette looks correct after native export/import. Thin laminate/star contact and smooth shading are the most useful next visual checks: no detached strips, z-fighting, hidden details or unexpectedly sharp LOD transitions. These are pending inspections, not defects established by this review.
- Root should retain the exact new-output build-b manifest and all eight export receipts before matched views. Runtime draw cost, LOD switching, Unity material import, held contact/attack readability and online appearance remain separate gates. No source/native/runtime file outside this report was written by this reviewer.

STATUS: COMPLETE — source review only, no blocking finding. Root's Blender/native results may supersede the analytic expectations; they were neither run nor claimed here.

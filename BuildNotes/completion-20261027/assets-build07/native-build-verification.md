# FAN build-07 native build verification

STATUS: BUILD EVIDENCE VERIFIED — read-only receipt/source/export verification passes. Root ran Blender 5.2.2 LTS at 2026-10-26 19:38:01 UTC; this review launches no native or rendering jobs and does not edit models. Visual/Unity/physics acceptance remains pending.

Rehashed all **69 recorded input files**, the native blend and all four exact export files against the actual receipt. All match; native source preservation is PASS and changed_inputs is empty. Receipt SHA `8640eff57172db5c80ae913b864d7c51f6a2efa15e774d0876dabb846df6ec8b`. Detailed evidence is in the adjacent JSON, outside the frozen source manifest.

| Export | Triangles / meshes | Grip translation error | Grip basis error | Bounds error |
| --- | ---: | ---: | ---: | ---: |
| LOD0 GLB | 15,032 / 58 | 0 m | 1.74846e-7 | 0 m |
| LOD0 FBX | 15,032 / 58 | 6.68789e-8 m | 1.62921e-7 | 7.45058e-8 m |
| LOD1 GLB | 8,775 / 14 | 0 m | 1.74846e-7 | 0 m |
| LOD1 FBX | 8,775 / 14 | 6.68789e-8 m | 1.62921e-7 | 7.45058e-8 m |

All four native reimports found the required controls and rigid mesh parentage and passed the existing 1e-6 full grip-matrix tolerance. The blend retained the exact original matrix and parent-preservation proof. GLB export rounds away its tiny residual original rotation; FBX introduces a tiny additional basis/translation conversion error. These are measured differences within the existing exchange tolerance, not an exact-matrix claim for exports. The hand contact is restored to the original 0.41049999 m height; held scale remains 0.4385.

Both GLB binary payloads were also independently decoded read-only for mesh/index counts, actual position buffers, full node matrices, grip and world envelope. This confirms the stated counts/bounds, six named materials, zero skins and zero animation clips. FBX readback is the root-produced native reimport evidence, independently hashed here; this review does not claim another FBX import.

LOD1 is **58.3755%** of LOD0 against the unchanged 65% cap. All eight requested detail parts remain protected, with 14 groups against the 20 cap. Its neutral envelope matches exactly (0 error against 1e-5 m), preserving source bounds corresponding to approximately 0.522 × 0.821 × 0.250 m in Unity.

Clearance reports a 0.00600001 m conservative blade gap (required 0.003 m), 0.000999998 m hub gap (required 0.0005 m), and zero surface overlaps at all eleven tilt samples from -10° to +10°. Minimum sampled vertex/surface gap is 0.003 m against the unchanged 0.001 m threshold. These are source geometry checks with intentional bearing mates excluded. They do not certify colliders, all continuous combined rotor/tilt poses, gameplay physics or runtime animation.

The candidate remains original-derived constant PBR colors with painted material inlays, not a texture-painted shop skin. Matched renders, Unity import/material/LOD validation, actual held contact/orientation and runtime motion acceptance remain separate gates. Nothing has been promoted to the original default equipment.

# Post-restart portrait comparison

STATUS: DIAGNOSTIC PASS; imported-normal defect confirmed, saved correction pending. 2026-10-26.

The owner restarted Windows at 09:52 UTC. Unchanged-source gate `101437` subsequently completed capture02 in 240.8 seconds with exit0, no reported error, 34 source files unchanged, global state unchanged, closed preview scenes and zero remaining owned objects. This establishes recovery for this launch, not a proven cause of the earlier Data Store failure. The exact sanitized receipt is [reboot-recovery-20261026.json](../reboot-recovery-20261026.json).

Root viewed all three capture02 images: [original light](capture-02/A-original-lit.png), [frontal key](capture-02/D-frontal-key.png), [rear key](capture-02/F-back-key.png). The rear key makes the face visibly cream/light while the hoodie becomes dark. The front key leaves the face dark while lighting the hoodie. This supports investigating surface orientation and mesh shading; simply raising scene exposure would brighten already-lit clothing without resolving the different response.

The reviewed brow control was then applied to the diagnostic only. R0 `101914` passes; native gate `102001` passes in 102.9 seconds with exit0. Both capture03 previews close and destroy their owned objects; all tracked sources and global state remain unchanged. Root viewed [original](capture-03/A-original-lit.png) and [BrowRelax zero](capture-03/G-brow-relax-zero.png). The expression changes, but the dark face remains. Face submesh0's 2,663 distinct baked vertex normals have identical reported distributions in both controls. This weakens BrowRelax as the explanation for the face defect; it does not justify removing the character's expressions.

All measured baked normals are finite and nonzero. Whole-mesh directional counts include both sides of the head and cannot prove whether the visible face is correctly oriented. The next bounded diagnostic compares winding/geometric normals and an instance-only recalculated-normal copy. Original FBX, materials, animations and character settings must remain unchanged. No runtime repair, final portrait set or current Windows acceptance is claimed.

Evidence: [capture02 telemetry](capture-02/diagnostics.json), [capture03 telemetry](capture-03/diagnostics.json), gate logs `Logs/gate_exec_Run_20261026T101437Z.log` and `Logs/gate_exec_Run_20261026T102001Z.log`. Private native logs remain in ignored Logs; only sanitized receipts and relevant asset telemetry are shared here.

## Capture04: normal direction confirmed

R0 `103403` and native `103446` pass; the native diagnostic completes in 59.1 seconds with exit0. Root inspected [original](capture-04/A-original-lit.png) and [instance-only recalculation](capture-04/H-head-recalculate-normals.png). The corrected face is cream and lit, while the hoodie remains the same. Hair shading changes too; it needs separate visual/expression review.

The original face's 4,992 triangles all point radially outward, while all 14,976 sampled vertex-normal corners oppose geometric winding (area-weighted dot −0.99846). In the corrected instance every sampled face corner agrees (+0.99846), with unchanged geometry/winding. Hoodie measurements are unchanged. This identifies a normal-direction defect in the imported head, rather than an inward face surface or global exposure problem. Eyes/hair also show substantial disagreement before correction, but their more complex shape requires individual review.

All 34 source-file proofs, source-mesh semantic fingerprints, global state and authored data remain unchanged. H owns one head copy and two telemetry meshes; all are destroyed, with zero remaining owned objects and closed preview scenes. Positions, indices, vertex attribute formats, skinning/bindposes, tangents, UVs, colors and complete blendshape data are preserved by the diagnostic. [Full telemetry](capture-04/diagnostics.json).

This is not yet a production repair: recalculating base normals leaves existing blendshape-normal deltas/tangents, and only one sampled Idle pose was compared. The saved correction must preserve the rig and source, handle expression normals consistently and test actual poses before replacing shop portraits or player presentation.

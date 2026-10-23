# Detailed equipment recovery

STATUS: IN PROGRESS

- [x] Verified real lane checkout at `f295956`; preserved the sole untracked historical `BuildNotes/flagship-content/focused-first-attempt.log`.
- [x] Verified original editable blend matches `e99fc364cfc1f1ddad09ba17d4e58f115bc229675a6def224b2236567f9f1f60`. Original revision remains unchanged.
- [x] Confirmed DetailV2 had never been generated; no existing output is being replaced.
- [x] Inspected original BAT, SOFA, FAN and TABLE three-quarter renders. Their silhouettes are readable, but material differentiation is weak.
- [x] Prepare export-only mesh grouping by parent pivot and shared material, independent reimport measurements and delivery counts. Python AST, Node syntax and `git diff --check` pass. Execution remains NOT RUN.
- [x] Generate all twelve DetailV2 models and editable source: build PASS, 49.62 seconds. Independently reimport all 24 FBX/GLB exports: 24/24 PASS, 26.20 seconds.
- [ ] Render and inspect BAT, SOFA and FAN before completing the remaining views.
- [ ] Verify texture channels, dimensions, budgets, source/provenance bindings and all delivery hashes.
- [ ] Root performs Unity import, native material/presentation tests and runtime performance measurements.

The original twelve assets have 193 meshes in total despite sharing only two materials. Detailed geometry would increase that count, particularly CLOCK's minute marks. The approved optimization merges delivery meshes only within the same parent pivot and material. It does not merge distinct FAN rotor or CLOCK hand pivots, simplify geometry, alter dimensions, or change grips. The editable `.blend` retains separated components. Fewer exported mesh renderers is a structural measurement; frame-time improvement is not yet measured.

Root completed the recovery snapshot, assembled R0 and released the exclusive Blender slot. No paid provider is involved. Native Unity and owner visual acceptance remain NOT RUN.

The grouping uses temporary cloned mesh data; all original components and empty transforms remain in the editable source. Before export it checks the vertex-position multiset within 0.01 mm. Initial build failed at SOFA because sorting near-identical floating-point coordinates reordered corresponding vertices. This failed output is preserved separately; no original art was replaced. The repaired comparison performs one-to-one spatial matching at the unchanged tolerance. Maximum measured grouping drift is 0.000246 mm.

Actual generated counts: 273 editable meshes retained; 27 exported meshes/material draws across twelve models versus 193 meshes in the original revision. FAN retains its rotor pivot and CLOCK retains both hand pivots; their exports contain three and four meshes respectively. Maximum triangle count is 4,908 (SOFA), within the unchanged 6,000-per-item cap.

The independent FBX/GLB reimports passed bounds, triangle/material/mesh counts, UVs, texture caps, and every empty's full world transform including hand and rotor pivots. Evidence: `assets-build-retry.log`, `assets-reimport.log`, and the DetailV2 `manifest.json` / `export-verification.json`. Texture-channel and final preview/delivery checks remain pending.

Next: inspect the first BAT/SOFA/FAN renders, complete the remaining views and run final delivery verification.

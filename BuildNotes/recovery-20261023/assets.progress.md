# Detailed equipment recovery

STATUS: IN PROGRESS

- [x] Verified real lane checkout at `f295956`; preserved the sole untracked historical `BuildNotes/flagship-content/focused-first-attempt.log`.
- [x] Verified original editable blend matches `e99fc364cfc1f1ddad09ba17d4e58f115bc229675a6def224b2236567f9f1f60`. Original revision remains unchanged.
- [x] Confirmed DetailV2 had never been generated; no existing output is being replaced.
- [x] Inspected original BAT, SOFA, FAN and TABLE three-quarter renders. Their silhouettes are readable, but material differentiation is weak.
- [x] Prepare export-only mesh grouping by parent pivot and shared material, independent reimport measurements and delivery counts. Python AST, Node syntax and `git diff --check` pass. Execution remains NOT RUN.
- [ ] Generate DetailV2 in its new directory, then independently reimport all 24 FBX/GLB exports.
- [ ] Render and inspect BAT, SOFA and FAN before completing the remaining views.
- [ ] Verify texture channels, dimensions, budgets, source/provenance bindings and all delivery hashes.
- [ ] Root performs Unity import, native material/presentation tests and runtime performance measurements.

The original twelve assets have 193 meshes in total despite sharing only two materials. Detailed geometry would increase that count, particularly CLOCK's minute marks. The approved optimization merges delivery meshes only within the same parent pivot and material. It does not merge distinct FAN rotor or CLOCK hand pivots, simplify geometry, alter dimensions, or change grips. The editable `.blend` retains separated components. Fewer exported mesh renderers is a structural measurement; frame-time improvement is not yet measured.

Root completed the recovery snapshot and released source/evidence writes. Online has the first heavy-process slot; Blender waits for root's explicit slot release. No paid provider is involved. Native Unity and owner visual acceptance remain NOT RUN.

The grouping uses temporary cloned mesh data; all original components and empty transforms remain in the editable source. Before export it checks the vertex-position multiset within 0.01 mm. The independent FBX/GLB reimport records bounds, triangle/material/mesh counts, UVs, texture caps, and every empty's full world transform including the hand and rotor pivots. The Node delivery check ties these measurements to export and source hashes. These are prepared checks, not results.

Next: run Blender after the heavy slot is available.

# Detailed equipment recovery

STATUS: ASSET GENERATION AND REVIEW COMPLETE; NATIVE UNITY AND OWNER ACCEPTANCE PENDING

- [x] Verified real lane checkout at `f295956`; preserved the sole untracked historical `BuildNotes/flagship-content/focused-first-attempt.log`.
- [x] Verified original editable blend matches `e99fc364cfc1f1ddad09ba17d4e58f115bc229675a6def224b2236567f9f1f60`. Original revision remains unchanged.
- [x] Confirmed DetailV2 had never been generated; no existing output is being replaced.
- [x] Inspected original BAT, SOFA, FAN and TABLE three-quarter renders. Their silhouettes are readable, but material differentiation is weak.
- [x] Prepare export-only mesh grouping by parent pivot and shared material, independent reimport measurements and delivery counts. Python AST, Node syntax and `git diff --check` pass.
- [x] Generate all twelve DetailV2 models and editable source: build PASS, 49.62 seconds. Independently reimport all 24 FBX/GLB exports: 24/24 PASS, 26.20 seconds.
- [x] Render and inspect BAT, SOFA and FAN before completing the remaining views. Side inspection found a floating FAN motor inherited from the first model; added an axle and angled support within the unchanged dimensions.
- [x] Rebuild the final source in 48.72 seconds and independently reimport all 24 exports again: 24/24 PASS, 29.05 seconds. Preserve the earlier revision separately.
- [x] Render and inspect repaired FAN: four views PASS, 155.97 seconds at two CPU threads. Root independently confirmed the support repair in the side view.
- [x] Render the remaining eleven items from the final frozen source at six CPU threads: 44 views PASS, 647.54 seconds. This changes rendering only; the geometry-build hash is preserved separately.
- [x] Inspect all twelve final three-quarter and side views, plus FAN front (25 views). Record actual image hashes and observations in `visual-review.json`.
- [x] Verify texture channels, dimensions, budgets, source/provenance bindings and all delivery hashes: PASS. Final report covers twelve concepts/models, 24 exports, 36 previews and twelve transparent icons.
- [x] Verify exact archived geometry/render/base builder receipts and both render batches against the same frozen editable source.
- [x] Create `BuildNotes/equipment-polish/ASSET-REVIEW.html` with the actual 36 views, source/export links and explicit native/owner acceptance status.
- [ ] Root performs Unity import, native material/presentation tests and runtime performance measurements.

The original twelve assets have 174 meshes in total despite sharing only two materials. Detailed geometry would increase that count, particularly CLOCK's minute marks. The approved optimization merges delivery meshes only within the same parent pivot and material. It does not merge distinct FAN rotor or CLOCK hand pivots, simplify geometry, alter dimensions, or change grips. The editable `.blend` retains separated components. Fewer exported mesh renderers is a structural measurement; frame-time improvement is not yet measured.

Root completed the recovery snapshot, assembled R0 and released the exclusive Blender slot. That slot is now released after final rendering. No paid provider is involved. Native Unity and owner visual acceptance remain NOT RUN in this asset lane.

The grouping uses temporary cloned mesh data; all original components and empty transforms remain in the editable source. Before export it checks the vertex-position multiset within 0.01 mm. Initial build failed at SOFA because sorting near-identical floating-point coordinates reordered corresponding vertices. This failed output is preserved separately; no original art was replaced. The repaired comparison performs one-to-one spatial matching at the unchanged tolerance. Maximum measured grouping drift is 0.000246 mm.

Final generated counts after the FAN support repair: 275 editable meshes retained; 27 exported meshes/material draws across twelve models versus 174 meshes in the original revision. FAN retains its rotor pivot and CLOCK retains both hand pivots; their exports contain three and four meshes respectively. Maximum triangle count is 4,908 (SOFA), within the unchanged 6,000-per-item cap.

The independent FBX/GLB reimports passed bounds, triangle/material/mesh counts, UVs, texture caps, and every empty's full world transform including hand and rotor pivots. Final evidence: `assets-build-support.log`, `assets-reimport-support.log`, `assets-preview-support.log`, `assets-preview-final.log`, `assets-delivery-final.log`, and DetailV2's manifest/export/delivery/source-provenance/visual-review reports.

Correction: earlier notes misstated the original mesh total as 193. Final machine verification sums the twelve original manifest entries to 174. The correct reduction to 27 delivery meshes is 84.5%; no geometry or export result changed. All current asset documents use the corrected baseline.

Exact final geometry builder SHA-256: `e0ea5155727af8d9c4db015e8159373ef00f4b5387e3c110c05f2b8a61cea8b0`; archived in the final source folder. Editable blend: `fefb1c851d674357aabc3f9e33a0983a6f1813476180b9447877a9b867bf0f25`. The renderer-only revision records its own hash and settings without rewriting the geometry-build receipt.

The first partial build and full pre-support revision remain under `.harness/recovery/failed-detail-build-1` and `.harness/recovery/detail-before-fan-support`; neither original work nor caches were deleted. DetailV2's local Git attributes preserve exact source/provenance bytes across Windows checkouts.

Next: root imports the final revision, executes native material/presentation checks, measures game performance and validates the Windows build. Unity and owner appearance acceptance remain pending. The assets are detailed stylized artwork; native appearance, photorealism and runtime speed are not claimed by the Blender checks.

## No-code guide and packaging follow-up

- [x] Verify exact current Unity authoring menus and runtime workshop controls at root b4a2cca; prepare docs/wordbound/UNITY-EDITING-GUIDE.md. Inspector course/profile workflow, workshop dragging/randomness/routes, saving, private hosting and real limitations are documented from source.
- [x] Review source-to-player art packaging in BuildNotes/equipment-polish/PACKAGING-SOURCE-REVIEW.md. Explicit runtime allowlist and existing grouped/shared-map budgets confirmed; no additional safe deletion demonstrated. No source/caches removed and no native job run.
- [ ] Root captures final Unity editing/runtime appearance and inspects actual Windows packed assets.

## Native six-family source review

- [x] Read targeted keepsake and timeout failure XML/logs and current native fixture/runtime source.
- [x] Identify straight CLOCK approach obstruction and prepare root-authorized test-only movement waypoints inside the unchanged eight-second deadline; verify actual geometry/API references without native execution.
- [x] Independent input-lane source review of 8378889 confirms current fixture geometry and unchanged physics evidence; no concrete defect found.
- [ ] Root compiles and runs the repaired six-family fixture; runtime outcome remains NOT RUN in this lane.

## Authored-workshop recovery and usability review

- [x] Review current plan/progress/guide, generated default references, workshop entry and packaging source without native jobs or deletion.
- [x] Independently confirm authored entry can overwrite an existing draft before recovery; implement root-authorized explicit recovery choice and archive-before-new-course protection.
- [x] Prepare 11 focused native Editor regressions and update the guide/report; source/diff checks pass.
- [ ] Root integrates only the repair delta, reviews it, compiles and runs recovery + existing workshop/saved-authoring checks; implementation/native UI acceptance remains pending.

## Owner visual rejection and original-game comparison

- [x] Record DetailV2 appearance as owner-rejected; preserve both Wordbound revisions and original game assets.
- [x] Inspect the original game showcase and all twelve original item icons against the twelve DetailV2 renders; confirm original SOFA/FAN model and SOFA/FAN/BED icon bytes match untouched main.
- [x] Save ORIGINAL-GAME-VISUAL-REVIEW.md with exact references, observed regressions and a small original-based SOFA/FAN pilot brief.
- [ ] Root schedules approved pilot production and matched Unity/source review. No new assets/rendering or full-set redesign has started.
- [~] Pivot-basis source investigation is preserved in 430da89; actual native matrices remain pending. Camera help clarification is 8404b04. Broader template binding work is paused before implementation at root's instruction.

# 2026-10-23 original-game pilot preparation

- [x] Preserved owner rejection of DetailV2 and established original game SOFA/FAN/HAMMER references (1833479).
- [x] Prepared isolated SOFA-first Blender script and exact root-owned staged commands in `BuildNotes/equipment-polish/ORIGINAL-BASED-PILOT.md`; original geometry/materials are the base, no runtime replacement.
- [~] Original-based SOFA: source-only pilot, native build and matched renders pending serial slot. FAN/HAMMER are bounded queued specifications, not generated assets.
- [ ] Owner appearance acceptance and later Unity/material/export/performance validation.
- [x] Closed pivot-basis investigation after directly reading root native matrices and the 6/6 detail pass in `editmode_20261023T125443Z.xml`; root fixed only fixture basis conversion in 5e9ad10. DetailV2 appearance remains owner-rejected.
- [x] Independent input-lane source review found a hash-only render-resume comparison risk; fixed current-setting checks and gallery pair consistency. Root reports first build passed and an uninterrupted three-quarter pair is rendering; native output/visual inspection pending in this lane.

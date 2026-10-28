# Independent map review

STATUS: REVIEW COMPLETE — SOURCE FINDINGS RESOLVED. Frozen candidate rechecked against manifest SHA256 a08137dcd6df555ee76fde84efd569f957ba87def98c1c0b8e358fb305d9e2f8. Native rendering/performance/collision execution not performed by this reviewer.

Reviewer: menu_redesign, independent from map author. Scope: initial eight candidate sources and the added official variant test under `BuildNotes/redesign-20261028/map/candidate`, their live baseline dependencies, source/probe evidence and integration instructions. No candidate/live sources, assets, native processes or public content changed by this reviewer.

## Findings requiring action

### Resolved P2 — architecture meshes omitted tangent data required by their materials

Initial `WordboundRoomArchitecture.Build` sets vertices, normals, UVs, triangles and bounds but does not generate tangents. `Make` clones `Surfaces.Get(Plaster, colour)`; that factory assigns a normal map and enables `_NORMALMAP`. Existing `Surfaces.BuildBox` explicitly calculates tangents. Consequently the new replacement walls/frames can lose or misorient normal-map lighting even though normals/winding are valid. The default materials are affected, not just an optional custom material.

Requested fix: calculate tangents after setting geometry, then assert count matches vertices and values are finite/nonzero. Verified corrected in frozen source: Build now calls RecalculateTangents; contract tests assert matching count and finite values. Native material/lighting inspection remains necessary. Initial candidate architecture hash in manifest: `f5dfc22260b0b4e4e3348f386d50b29b4de5a243c66dc5a3bb018c093abf7260`.

### Resolved contract clarification — arrival variant intentionally clones current local edits

`WordboundArrivalVariant.Create` checks the flagship course ID, presence of the entry module, no existing entry zones and three furniture catalogue IDs. It does not compare the entire source document or catalogue to canonical hashes. An edited course retaining the flagship ID passes these checks. That is deterministic and correctly hashed as authored content, so this is not an online authority bypass. However, the error text's claim of an unchanged Lost Address source is stronger than the guard.

Choose and test the intended contract: preserve the current editable source and explicitly call this a clone-with-furnished-entry, or require the canonical source/catalogue hashes if the preset must be byte-reproducible from an official base. Never reset existing author edits to satisfy the guard. Verified corrected contract: wording now says Lost Address course copy. New LocalAuthoringEditsAreClonedAndExistingEntryFurnitureIsNeverOverwritten test proves edits are preserved, source is untouched and already-furnished/variant inputs are rejected.

## Correctness and collision assessment

- World integration occurs after canonical Construct; existing surfaces, collider transforms, openings, anchors and collision callbacks are retained. There is no added collider, rigidbody or light in the presentation component.
- Wall renderer replacement uses a 12 mm recess inside the original 200 mm wall. Frames, panels, sills, mullions and artwork stay at or behind the original interior face. Floor material separation does not alter vertices or collider geometry. Static source inspection found no planned obstruction across a route opening.
- The new tests compare collider identity/shape/transform counts under a translated and rotated parent, ensure every architecture vertex lies inside an original solid wall, check winding, original UVs, custom material precedence and owned-resource disposal. These are appropriate meaningful contracts; native execution was pending at review time.
- All authoritative furniture in the arrival variant is represented as real fixed `Props` zones. A renewable protected HAMMER supply uses the existing supply schema. There are no decorative fake furniture copies that could diverge from break/carry/letter authority.
- Fixed zones use distinct generated names, original asset IDs and module-relative positions. The source document is cloned before edits. The creator saves a new asset path and separate course ID; an existing variant is selected and validated rather than overwritten.

## Deterministic content boundary

The presentation pass does not alter CourseDocument or ResolvedCourse/hash, so its palettes/mesh dressing are local visuals only. The furniture variant does alter course data and correctly receives its own ID and source hash. Existing course creation validates source/seed, catalogue and exact manifest; authored private sharing binds that identity and requires installed item/rule compatibility. The setup leaves launcher/public catalogue assignments unchanged.

The author's `probe-result.json` records PASS for 8 mode/format configurations and 24 seeds each (192 resolutions), repeated manifest hashes, canonical round trips, original factory immutability and six furniture break/conservation checks. This is useful engine-free evidence, not proof of actual visual/physics performance. Official gate tests are being added by the author before final freeze.

No source path in the candidate silently changes the public flagship factory, its version or public approved-content registry. The optional furnished course must remain outside public matchmaking until normal explicit catalogue/version validation and admission occur.

## Performance assessment and measurement needed

The pass runs at world construction, with no per-frame Update work. Geometry batches are capped below 60,000 vertices, compatible with default 16-bit indices. Decorated wall pieces and insets are bounded. Runtime-owned meshes/materials are released on destruction; borrowed textures and assigned materials are not destroyed.

There are real tradeoffs that need measured acceptance:

- Floor surfaces change from one material slot to three, increasing per-floor draw submissions. Walls gain combined meshes while their old renderer components and original meshes remain resident.
- Batches group by material across the course, not by room. This lowers draw calls but gives coarser culling and shadow bounds, potentially submitting architecture from several distant rooms whenever one part is visible.
- The author also addressed retained CPU builders: batches and completedBatches are cleared after successful upload in the frozen source, reducing duplicate geometry memory.

Measure CPU construction time, frame/GPU time, rendered triangle/draw counts and peak memory at the starting room and the largest authored map. Test repeated build/dispose to distinguish reusable Surfaces cache growth from owned leaks. No performance improvement is claimed from batching alone.

## Original art and visual-design boundaries

No original model/icon/texture files are overwritten. Furniture refers to the existing TABLE/SOFA/STOOL catalogue items. Optional wall art reuses the existing generated townhouse illustration without changing its file. Custom per-build surface materials take precedence over the architecture pass. The resource profile is saved and editable in the Unity Inspector; changes take effect after rebuild/re-entry to Play.

Remaining visual risks are substantial enough to require screenshots and owner review:

- Layer identity is mainly colour/material with shared rounded-panel geometry. This improves room surfaces but does not by itself supply distinct room silhouettes, major landmarks or a comprehensive new map.
- Window-like panels remain solid walls. Framing/sills/mullions should make this legible; check players do not read them as exits.
- The architecture pass does not furnish the sparse default arrival room. Furniture exists only in the separate local Parcel Hall variant. Ordinary Play/public launch still uses its existing map assignment unless root deliberately selects/releases the variant. Do not report the furnished entry as delivered in normal gameplay just because the asset exists.
- Close and distant views must confirm vivid palette values, readable original item silhouettes, no generic empty-room impression, no striping, no normal-map artifacts, no seams/z-fighting, and no overwhelming repeated windows. Test all four destination palettes and a low/maximum lighting setting.

## Recommended integration order

1. Require the tangent fix and its source tests; re-run the author's semantic check and refresh manifest hashes.
2. Integrate presentation sources/profile only after matching both live baseline hashes. Run official native architecture/collision/preview tests, including custom material override and disposal.
3. Capture ordinary gameplay to assess the surfaces separately from the optional furniture variant. Record that distinction in progress.
4. Create/select the local variant, run its official content tests, then real Windows entry navigation, breaking, letter collection, crafting and return-route checks with multiple players/team formats.
5. Keep production/public readiness unchecked until visual acceptance and performance/online evidence are complete. No public registry mutation is implied by this review.

## Frozen-candidate recheck

Verified source after author fixes: tangent generation at WordboundRoomArchitecture.cs:63; CPU-builder cleanup at 73; matching finite tangent assertions at WordboundRoomArchitectureTests.cs:61. Authoring contract wording and the new official variant test match preservation of current local edits. `official-rules-result.json` reports 12 passed/0 failed through the unchanged repository RulesHarness in a draft-only project; this reviewer read the result and tests, did not rerun them. Final `source-check.json` reports zero semantic errors across all six assemblies. No unresolved blocking source defect found. Remaining native, performance, visual and release/admission checks above remain required.

# Workshop cutaway and material depth

STATUS: SOURCE DRAFT — root integration/native comparison pending.

The actual Windows run `docs/qa/runs/20261028T211423Z-wordbound_parcel_workshop` shows wall sconces, shelves and signs floating above cutaway floors. The viewport omits full walls, so wall-only dressing must also be omitted there. Full gameplay and thumbnail worlds retain the same saved fixtures. The course/profile/prefab data are not modified or copied.

The second defect is technical: generated opaque floors/edges use the Unlit overlay template, losing the original Lit surface shader and normal/detail maps. The candidate clones the original Surfaces material for opaque geometry, tints only that owned clone and preserves texture references/scales/offsets/keywords. The translucent adjacent-floor path is unchanged. Explicit author-assigned FloorMaterial remains borrowed. Surface meshes retain the same geometry, UVs, layer31 and picking bounds.

The two preview lights, camera, pipeline and RenderSettings.sun are unchanged. Opaque generated geometry can cast/receive shadows; transparent ghosts cannot. This does not assert that the present preview main light produces shadows. Do not alter global lights merely to force that result. Root should use the same Windows sorting-close shot to assess material shading and wall-fixture absence before deciding on any further lighting work.

Integration: compare all three production SHA256 values to `manifest.json` baselines; apply `workshop-depth.patch` or copy only matching candidate files. `git apply --check` passed against the current source. No native or production mutation was performed in this lane.

Focused native filter: `Wreckabulary.Tests.WordboundModuleDressingTests;Wreckabulary.Tests.WordboundCourseMaterialTests` (existing nine plus four cases). The updated cutaway case proves no unsupported wall fixtures are rendered while the floor remains, source switching/hiding leaves no clones and row/source/material values survive. The unchanged full-world case proves fixtures still render with identical canonical colliders/hash. The extended material case checks original shader, keyword, every texture reference/scale/offset, borrowed-source immutability and owned floor-clone cleanup when changing source.

Then repeat the exact Parcel Workshop smoke and open all nine frames, especially `parcel_workshop_sorting_close.png`, saved/returned cutaway, and full playtest. Check original prop readability, selection/ghost visibility, no floating wall fixtures in cutaway, attached fixtures in gameplay and no material/leak/console regression. This is a bounded visual correction, not completion of overall map art.

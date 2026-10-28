# Integration handoff: route woodwork

STATUS: SOURCE CHECK PASS; independent source review PASS. Manifest SHA256 e52bc388a9a4c0f25bf7d5cc5c18cce78b0f15d2d004e1e2b47250d9d04cb3a8. Exactly three existing sources are substituted, listed with before/candidate hashes in manifest.json. No live file was edited by this lane.

1. Compare each current live SHA against manifest beforeSha256. Resolve any divergence with this lane; do not overwrite newer parent work.
2. Copy the three candidate `.cs` files to their matching paths, preserving existing `.meta` files. No source art, imported model, material asset, saved course, catalogue or profile YAML change is needed.
3. Unity loads existing `Assets/_Project/Resources/Wordbound/RoomArchitecture.asset` with additive defaults: `FrameRouteOpenings=true`, `DoorwayFrameWidth=0.24`, `MaximumOpeningBands=512`, `Woodwork=(0.55,0.36,0.20,1)`, no WoodworkMaterial. Verify these after domain reload and keep every pre-existing field/palette/material reference. If persisting the additive fields through Unity, first keep the exact profile/meta backup; do not call a Defaults/reset helper. The Inspector is the no-code editing path.
4. Run focused `Wreckabulary.Tests.WordboundRoomArchitectureTests` (7 tests). Roslyn checks are source checks only. Existing tests check canonical collider/manifest/route preservation, all generated vertices within wall collision (including 0.4 m maximum surrounds), tangents/winding and cleanup. New tests check ramp texture/normal-map/UV retention, borrowed-material identity/survival, band budget, disable toggle, legacy omitted fields and invalid bounds.
5. Capture actual fixed-seed Lost Address and Parcel Hall before/after with the accepted preview tool; its source fingerprint will require a fresh receipt after this source change. Inspect wood grain and tint on long ramps, actual doorway scale, header seams, grazing light and no route occlusion. Run actual Workshop playtest and inspect the same shared renderer there.

## Exact visual change

The current RoomArchitecture renderer assigns the gold plaster Frame material to connection surfaces. The candidate assigns original textured `Surfaces.Kind.Wood` with one owned woodwork material, or the owner's explicit material unchanged. Mesh and UV data are unchanged. The default inherited texture orientation is preserved, not newly authored.

The candidate adds only narrow jamb and header geometry around apertures derived from the same connection dimensions, plane crossing, floor thickness and slope allowance as CourseWorld.BuildWalls. Each band is intersected with a real solid wall rectangle and recessed 4–12 mm behind its collider face. No sill, door, signage or closed aperture is added. Surrounds are local architectural features, not perimeter stripes. Their vertices are covered by the existing geometry containment test. Duplicate identical bands on a wall piece are suppressed; the global budget caps emitted band boxes.

Explicit caller wall/floor/connector material overrides remain respected. All pre-existing palette edits remain effective for their previous wall/floor/ceiling/inset roles; route woodwork is now a separate semantic role. Dispose still releases owned runtime materials/meshes, with no new cached resource or material asset.

## Before references and limits

- Actual game before: `docs/qa/runs/20261028T172443Z-wordbound_solo/solo_parcelhall_freestyle_world.png`.
- Original architecture/material reference directly viewed: `docs/reviews/evidence/unity-environment-2026-10-09/14-terrace-interior.png`.
- The 26 original asset candidates remain inventoried in `../map-previews/ASSET-CANDIDATES.md`. No imported FBX is added or distorted for this change: furniture needs canonical placement/break-resource rules, and protruding sconces cannot meet the no-new-collision scope.

This does not fix sparse room composition or make every destination finished. Native visual quality, default profile reload and user acceptance remain open. The new controlled material and opening detail must be compared in real captures before being called an improvement in delivery notes.


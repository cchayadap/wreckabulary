# Independent environment polish review

STATUS: COMPLETE — BOUNDED SOURCE REVIEW; NATIVE AND CURRENT VISUAL REVIEW PENDING

No blocking source defect found in the frozen three-file candidate. This is not a visual acceptance or production-readiness claim. Only this report was written; no live source, assets, native process or prior evidence was changed.

## Exact reviewed draft

Manifest SHA-256: `e52bc388a9a4c0f25bf7d5cc5c18cce78b0f15d2d004e1e2b47250d9d04cb3a8`.

All three candidate hashes and current live before hashes matched the manifest when checked.

| Candidate | SHA-256 |
| --- | --- |
| WordboundRoomArchitecture.cs | `b2ce21eabc433aec48e7e8f71f81990409ce4d61120e42d933fb941283eebe35` |
| WordboundRoomStyleProfile.cs | `ae0bfc2fa3177194707c5daeb2af3a9f07862981050209029d96680010793aa1` |
| WordboundRoomArchitectureTests.cs | `0e83ba1b4a961c529974f3430c5eb27049be5fcbca9f357739b9c3abf2b84ea5` |

The supplied six-assembly Roslyn receipt contains zero errors. This review read that receipt; it did not run Unity or independently execute the tests.

## Confirmed source properties

- Architecture lines 158–208 reconstruct the same connection aperture extents used by canonical `WordboundCourseWorld.BuildWalls`, including sloped-connection clearance. Each requested jamb/header rectangle is intersected with an existing wall piece before emission. Depth is 4–12 mm behind the canonical wall face (lines 206 and 228–232), within the existing 200 mm wall solid. No collider, route, course geometry, manifest or resource catalogue is changed.
- The new count cap stops emission deterministically, and zero disables new bands. The flag can disable them independently of the connector finish. Exact duplicate clipped rectangles within one wall piece are suppressed. Maximum width is bounded to 0.4 m; the preserved-geometry test now stresses that maximum under a translated and rotated parent.
- Connector changes affect the material only. Existing mesh and UVs remain unchanged. Explicit connector material overrides still bypass this path. The fallback derives from the existing Wood surface and therefore retains its texture and normal inputs instead of using the former flat frame finish.
- `Woodwork()` uses the existing material ownership path. A supplied material is borrowed unchanged and is not added to the owned list. A fallback material is cloned, registered and released through `Dispose`; generated meshes use the existing owned-mesh cleanup. The new test checks borrowed material survival and unchanged colour, and existing lifetime tests remain present.
- Profile changes are additive Inspector controls with finite and bounded validation. Existing palette, inset, artwork and override fields remain intact. Default insets remain zero; no setup or asset migration is included in this draft.
- Tests meaningfully compare manifests, collider transforms/counts and collision callbacks; check decorative vertices stay within canonical wall solids; preserve connector textures and exact UV arrays; and exercise optional bands, the cap, borrowed materials and invalid authoring values. They are not merely screenshots or mirrored implementation assertions.

## Remaining checks and limits

1. Run the native architecture tests and capture both maps with identical seed, framing and lighting before/after. Source containment does not establish visual quality, absence of a visible seam, or actual traversal comfort.
2. Inspect a real existing saved profile after native reload. The added `JsonUtility.FromJsonOverwrite` assertion proves missing fields retain defaults on a newly created instance; it is not a full saved Unity asset reload/migration test. Confirm new values become true / 0.24 / 512, and previously edited palette/material values remain unchanged.
3. The geometry fixture covers its flagship generated course, including the maximum band width. It does not claim exhaustive coverage of every possible authored course or connection overlap. The clipping algorithm itself is bounded to each existing solid.
4. The change is restrained route woodwork, not a comprehensive environment-art replacement. Large wall areas, prop placement density and lighting still require direct review against the owner's request.

## Images actually opened

Opened the complete unchanged images at original size:

- `Docs/qa/runs/20261028T172443Z-wordbound_solo/solo_parcelhall_freestyle_world.png` (1280×720): broad plain teal walls, an unframed rectangular route opening, wood floor and a flat gold-coloured overhead connector are visible. This supports the specific targets of this patch, but says nothing about its unseen result.
- `Docs/reviews/evidence/unity-environment-2026-10-09/14-terrace-interior.png` (1600×900): original house interior has two-tone wall treatment, trim, framed artwork, a rug and more varied prop silhouettes. It is a style reference from another map/mode and camera, not a matched before/after comparison.

No current candidate render was available or claimed to have been reviewed.

## Preview capture guard adjustment

Verified current live `WordboundSoloPreviewCapture.cs` guard: only a main asset of type `Shader` whose path begins `Packages/` skips the dirty-main-asset rejection. Every importer remains checked, and all other dirty main assets, including Mesh, AnimationClip and controllers, remain rejected. Dependency/source hashes and prefab-stage dirty checks are retained. This is a suitably narrow exception for the reported automatically dirtied package shader; it does not exempt user materials, textures, meshes or scripts. Native capture still must complete and its exact dependency/source receipts must match before import.

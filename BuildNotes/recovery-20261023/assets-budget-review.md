# Wordbound DetailV2 — measured delivery and review

STATUS: ASSET DELIVERY VERIFIED; UNITY AND OWNER ACCEPTANCE PENDING

The detailed revision keeps rounded Wreckabulary household silhouettes and the existing warm wood, cream, sage, terracotta and brass palette. This is detailed stylized artwork. Its 512-pixel material budget and softened geometry are visible; photorealism and final owner style acceptance are not claimed.

## Source and delivery

| Item | Triangles | Editable meshes | Delivery meshes / material draws |
| --- | ---: | ---: | ---: |
| BAT | 2,724 | 5 | 2 |
| BROOM | 2,556 | 35 | 2 |
| HAMMER | 2,852 | 9 | 2 |
| SPEAR | 2,608 | 11 | 2 |
| SHIELD | 1,964 | 17 | 2 |
| PLATE | 2,508 | 4 | 2 |
| TABLE | 2,628 | 23 | 2 |
| SOFA | 4,908 | 34 | 2 |
| BED | 3,520 | 28 | 2 |
| STOOL | 2,136 | 15 | 2 |
| FAN | 4,444 | 27 | 3 |
| CLOCK | 3,424 | 67 | 4 |
| Total | 36,272 | 275 | 27 |

The original twelve delivery models used 174 meshes. Grouping only by shared material and parent pivot reduces the new delivery count to 27, an 84.5% reduction against the original revision. This is a structural renderer/material-draw measurement. Actual Unity draw calls, instancing, memory use and frame time remain separate native measurements. All 275 components remain separate in the editable Blender source. FAN rotor and CLOCK hand pivots remain separate in both formats.

Each asset uses two shared materials. The largest model uses 4,908 of the 6,000-triangle allowance. Four shared 512 × 256 PNG sources provide sRGB color, linear tangent-space normal (+Y), linear grayscale roughness, and a Unity mask with metallic in red and smoothness in alpha. Unity uses color, normal and mask; glTF uses roughness. PNG source size is not a claim about runtime texture memory or compression.

## Independent export verification

The final support-corrected revision passed all 24 FBX/GLB reimports. The checks measure geometry bounds, triangles, meshes, material draws, UVs, texture dimensions, every empty's full world matrix, and `Grip_R`. Maximum bounds difference is `1.67638063430786e-7` metres. Maximum pivot matrix element difference is `1.78813934326172e-7`. Grouping's largest vertex drift is `2.45756240246919e-7` metres, below the `1e-5` metre tolerance.

The first build stopped at SOFA because rounded coordinate sorting mismatched equivalent vertices. The comparison was repaired using one-to-one spatial matching at the same tolerance. Initial partial output remains at `.harness/recovery/failed-detail-build-1`. The first full revision and its twelve previews remain at `.harness/recovery/detail-before-fan-support`.

Side inspection identified a disconnected FAN motor. The final revision adds an angled support neck and brass axle inside the original bounds. The final FAN has 4,444 triangles, 27 editable components and three delivery meshes. Root independently reviewed the repaired side view and confirmed the earlier gap was resolved.

## Timings and receipts

Final geometry build: 48.7178 seconds. Final 24-format reimport pass: 29.0542 seconds. Four repaired FAN views: 155.9699 seconds at two CPU threads. Remaining eleven assets / 44 images: 647.5394 seconds at six CPU threads with the same Cycles CPU renderer, 24 samples, 640-pixel previews and 512-pixel icons. Both render commands exited zero.

BAT's side view took 24.844 seconds during the two-thread pass and 11.359 seconds during the six-thread pass; its three-quarter view took 23.406 and 11.250 seconds respectively. These are observed log intervals on this PC, with normal background activity, rather than a controlled runtime performance benchmark.

The geometry builder is `e0ea5155727af8d9c4db015e8159373ef00f4b5387e3c110c05f2b8a61cea8b0`. The later render-only builder is `f2a868cdc76947ccc8b2fe022875e75013f5e0a76a72d9b85b65055e3a4deec3`. The editable source is `fefb1c851d674357aabc3f9e33a0983a6f1813476180b9447877a9b867bf0f25`. Exact geometry, render and base builder bytes are archived beside the `.blend` with local Git newline preservation. Geometry and materials were not regenerated for the thread change.

Final file/channel verification passed for twelve concepts/models, 24 exports, 36 previews and twelve transparent icons. FBX exports total 999,520 bytes; GLB exports total 3,874,764 bytes. The machine-verified original mesh total is 174; earlier notes stating 193 were corrected before delivery. Repeated original concept files are identical LFS objects, and source/previews remain outside Unity's Assets folder.

Agent visual inspection covers all twelve three-quarter and side views plus FAN front: 25 final images bound by hash in `visual-review.json`. Root independently reviewed final FAN side, SHIELD three-quarter, TABLE side, BED three-quarter and CLOCK side; earlier BAT/SOFA/FAN three-quarter views informed the direction. No blocking construction issue remains in these inspected views. The local gallery uses those actual finished images and has no external scripts or dependencies.

## Outstanding integration gates

- [x] Final 48 images and file/channel/provenance verification.
- [x] Inspect final previews for every model and record the specific views.
- [x] Local review gallery with actual image/source/export links.
- [ ] Root imports via `Wreckabulary.EditorTools.WordboundEquipmentImporter.BuildDetailed`.
- [ ] Root runs native `Wreckabulary.EditorTests.WordboundEquipmentDetailTests`, collection/presentation checks and actual game-camera review.
- [ ] Root measures runtime performance in the assembled game and tests the Windows build.
- [ ] Owner accepts the style and gameplay appearance.

No paid service was called. Original concept bytes and source art remain unchanged. No caches or unrelated work were deleted.

# Corrected holding views — 143859

STATUS: PASS for this bounded diagnostic image review. Agent-reviewed improvement; owner visual acceptance and broader animation quality remain pending.

All 18 actual PNGs in `captures/20261026T143859915Z-8a9a18e0` were opened and independently reviewed: BAT/HAMMER × early windup/late windup/active × front/side/rear. No floating-grip or obvious head/body penetration blocker is visible at these sampled poses. This is sufficient to proceed with the Windows candidate without speculative runtime pose changes.

## Actual visual findings

- Rendered arm, mitten, torso and satchel now advance with the sampled weapon pose. BAT active front/side and HAMMER late-windup side no longer show the floating handles from the rejected 142356 run. The stale-preview problem is visibly resolved, not merely hidden by a passing setting flag.
- BAT early windup holds the weapon outward; late windup brings it nearer the cheek in frontal projection. The side view shows the handle connected to the raised mitten and the barrel directed forward/upward. No visible cut-through or broken surface boundary establishes a head intersection. Some front/rear projections overlap the head silhouette; that alone is not penetration.
- HAMMER early and late windups show the head and short shaft connected to the mitten. Late side provides a clearer forward silhouette than the frontal cheek overlap. The active side shows the hammer ahead of the body with the swinging arm following it, rather than the previous stationary arm.
- Both active rear views largely hide the held item behind the avatar. This is consistent with the front/side position and ordinary occlusion; it limits attack readability from that direction. It is not a reason to move the weapon through the hand or alter combat timing.
- The original mitten shape remains stylized and has no independently articulated fingers visible. Contact is plausible for this design; a realistic finger-wrap claim is unsupported. These stills do not establish swing weight, continuous smoothness, hit feel or readability at the normal gameplay-camera distance.

## Evidence

All 18 PNG hashes match the receipt. Receipt SHA-256: `94ae88ed45232fb11cf340bb971cecf88f33da7634d9013bb2e31e34fc3e1d02`.

Every image reports all geometry inside the unchanged frame limits and per-render skin matrix refresh enabled. Maximum recorded grip distance is `1.4901161193847656e-08 m`; grip angle is `0°`. The compensated/full-transform versus scaled/rigid-transform bake check has maximum world-vertex disagreement `6.224691446732322e-07 m`, below the unchanged 0.1 mm limit. The early head center Y is 1.3635 m and height 0.7799 m, consistent with the rendered fitted avatar instead of the rejected doubled measurement.

All 322 file records and 220 memory records passed preservation. Scene/global state and owned resource cleanup passed. Tactile scope count returned 0→0; generated materials and textures finished at zero. These are diagnostic integrity checks, not polygon-intersection or collision-clearance tests.

The poses use the real cosmetic motion graph at controlled synthetic canonical ages, with the saved presentation profile and original palette/production material setup. They do not replay exact Windows interpolated frames, exercise networking/human input, or cover every intermediate tick, equipment family, skin or camera. The earlier Windows camera's hidden windup remains a readability limitation. No broader production-ready or owner-accepted animation claim follows.

## Images reviewed

- `bat-early-windup-front.png`
- `bat-early-windup-side.png`
- `bat-early-windup-rear.png`
- `bat-late-windup-front.png`
- `bat-late-windup-side.png`
- `bat-late-windup-rear.png`
- `bat-active-front.png`
- `bat-active-side.png`
- `bat-active-rear.png`
- `hammer-early-windup-front.png`
- `hammer-early-windup-side.png`
- `hammer-early-windup-rear.png`
- `hammer-late-windup-front.png`
- `hammer-late-windup-side.png`
- `hammer-late-windup-rear.png`
- `hammer-active-front.png`
- `hammer-active-side.png`
- `hammer-active-rear.png`

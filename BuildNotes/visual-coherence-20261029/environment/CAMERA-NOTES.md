# Actual preview framing follow-up

STATUS: inspected actual preliminary images; camera suggestions require native comparison. No preview source/settings writes from this lane; root owns the five preview files.

Directly inspected both full PNGs in `../map-previews/captures/20261028T182236Z-739e35b6/`. Receipt creation failed in that run; these are diagnostic images, not importable approved outputs. Their large empty foreground/ceiling and distant furniture do not communicate Wreckabulary. Both maps share the empty-looking entry shape, so identical wide framing also hides the actual Parcel Hall difference.

Suggested starting poses, in metres relative to the named module:

| Map | Module | Camera | Target | FOV | Truthful subject |
|---|---|---|---|---|---|
| Lost Address | Packing_LabelBench | (0, 2.2, -10) | (7, 1, -5) | 50 | Actual marked TABLE props near the first smash/craft station, with route context. |
| Parcel Hall | Packing_ParcelGate | (-1, 2.2, -5) | (-5.5, 0.9, -9) | 50 | Actual sorting table/stool cluster and practice supply area, instead of the floating ramp. |

These are unrendered starting poses. Root should compare them, retain useful architecture context and reject clipped props or a misleading isolated furniture glamour shot. The wide room remains sparse outside any crop; no thumbnail crop resolves world composition.

## Existing faithful static gameplay path

`WordboundLauncher.CreateSoloContent` remains the source. `WordboundLocalSession.CreateBinding` can make an in-memory Solo binding without starting Fusion. Instantiate `AuthoritativeMatch(binding, content.Mode, content.Course, content.Geometry, content.Items, content.Rules, query)` and capture the untouched initial snapshot via `Capture(1)`. A small query implementation that rejects all physical interaction is sufficient for the initial constructor/capture, as already demonstrated by `WordboundWorkshopLaunchTests.NoPhysicalCompletion`; do not advance or mark puzzles solved.

`WordboundPresentationContext(frame, course, geometry, items).Presentation(body)` provides exact canonical size and visual key for each active body. The existing `WordboundVisualResolver` can render these in the same owned preview scene using each body's canonical position/quaternion. Render the active non-player roster once instead of additionally rendering raw placements, to avoid duplicates. This includes real item props, keepsake, letter tiles and moving-fixture platforms where the initial canonical state makes them available. It requires no new gameplay controller, player view, physics runner or network connection. Include snapshot/body roster facts in a refreshed receipt if root adopts it.

Limits: supplies are demand-gated; rendering every raw course supply would be false. Initial letter tiles are minted at the same source position before physics, so static initial snapshots may show overlapping tiles. Do not manually spread or spell them for a prettier image. Initial moving fixtures are phase-zero snapshots, not motion proof. A `WordboundPuzzleGuideView` can display `WordboundPuzzleGuidePlan.Build(course, frame, localActor)` exactly, but the initial Freestyle actor remains at spawn and has not selected a later station. Guide markers must not be fabricated or activated merely to sell a thumbnail. The current guide is text/rings and will not itself supply richer map architecture.

Root may choose the bounded camera-only change first. Faithful static bodies are feasible but are a separate parent-owned preview modification requiring source/identity/cleanup re-review and actual image acceptance.

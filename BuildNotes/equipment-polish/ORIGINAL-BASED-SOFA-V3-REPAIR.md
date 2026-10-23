# Original-based SOFA V3 seam repair

STATUS: SOURCE PREPARED — V3 build/render NOT RUN; V2 detailed appearance not accepted

Root and this lane directly inspected the V2 original/candidate side and detail pairs. The side silhouette stays faithful and no detached seam is visible there. The closer detail reveals a kink near the back cushion's upper-left corner and an awkward angular lower-left run. The original uninterrupted surface is cleaner at those locations. The first three-quarter contact improvement therefore does not constitute a full V2 visual pass. Both V1 and V2 remain preserved, with no runtime replacement.

The V2 curve followed a cross section of the actual triangulated cushion. Its finite contact probes passed, but those measurements did not establish smooth shape. V3 uses the same 256 measured section hits to fit a smooth rounded rectangle in the cushion's local section plane. Its circular corners join straight edges tangentially. The emitted polyline has 512 points, limits adjacent segment turning to 1.5 degrees, and checks 2,048 actual-surface distances per welt along the emitted line. The original 1.3 mm tube radius and 0.975 mm absolute contact limit remain. The centreline offset is reduced to 30% of its radius to keep the smooth fit in contact with the underlying low-poly surface. A bad fit fails; the code does not move the cushions, loosen the contact limit or enlarge the seam to hide it.

This is a bounded seam-path experiment, not an appearance claim. Original cushion vertices/faces/UVs/normals, all remaining sofa geometry, palette and material settings remain unchanged. The source receipt records fitted corner radius, maximum fit residual, maximum line turning and actual contact range. Finite probes do not prove every possible bevel surface point or visual quality; close rendered inspection remains necessary.

The V2 side studio images also have a gray strip at the bottom because the low orthographic camera allows lower ray origins beneath the floor plane. This is a preview staging artifact visible in both variants, not a geometry regression. The current repair deliberately leaves the frozen comparison camera setup untouched. A later presentation-only rerender can move the same side camera farther along its view ray while preserving direction/framing.

## Root-owned staged commands

Integrate the V3 source checkpoint after any active renderer exits. Root alone schedules the heavy slot. Build into a new destination:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_original_pilot.py' -- --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotV3' --stage build
```

Render the failing close-up first. Inspect the upper-left corner and lower-left run before spending time on other views:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Tools/AssetPipeline/build_wordbound_original_pilot.py' -- --source-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration' --output-root 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/ArtSource/Collections/WordboundPvP/OriginalBasedPilotV3' --stage render --views detail --size 960 --samples 64 --threads 6
```

If actual close-up quality improves, continue three-quarter/side/clay, then scale/front/rear; preserve all failed revisions and their receipts. FAN/HAMMER remain separately scoped original-based pilots. No whole-set rollout or owner appearance acceptance is implied.

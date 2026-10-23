# Original FAN / HAMMER pilot source review

STATUS: PASS — bounded source review only. No Blender, renderer, Unity or native jobs; no asset/source edits. Build/render/appearance acceptance remains pending.

Scope: recover_assets' `Tools/AssetPipeline/build_wordbound_tool_pilots.py` at final checkpoint `f874829` and its existing `build_wordbound_original_pilot.py` helper. Parent authorized this review after the original-artwork default review. The owner confirmed no further source change after the closure below.

Verified source behavior:

- Build reads hash-checked original GLBs and refuses an existing destination. The destination must be a new item-specific revision directly below the intended ArtSource collection folder. It writes separate source copies/Blender output; no original runtime file is modified.
- Component copies bake the source world transform into independent mesh data while preserving source UVs and normals. Candidate material copies do not modify the imported baseline materials.
- The retained HAMMER source accessor extents, adjusted for its -0.02 m vertical origin, put the steel head at z=.445–.635 m. The eye cutter spans .41–.67 m; the tenon spans .505–.6345 m and overlaps the original handle ending at .54 m. The wedge ends at .6348 m. These coordinates fit the claimed construction, subject to actual Boolean/topology results.
- The held root transform `socket * scale * translation(-gripPosition)` maps the captured original grip position to the avatar socket. Both baseline and candidate use the same root rule and source scale. This is a static original-avatar reference, explicitly not Unity hand-fit acceptance.
- Render resumes compare blend/script/helper hashes, Blender version, exact camera matrix/scale, resolution, samples, threads and view. Image hashes are checked. Gallery pairs reject differing render metadata. No mismatched-pair source defect found.

## Finding and closure

The source component copier retains split GLB vertices. The owner confirmed from raw GLB data that the head has 342 vertices but 218 unique positions, with 240 boundary edges before positional welding and none after. A disconnected operand can make a Boolean unreliable, and the aggregate min/max guard could not prove that the eye was cut because the end caps preserve the outer bounds. This was a source validation gap, not an observed Blender failure.

The revised 385-line script closes that gap with candidate-head-only welding at 0.0000001 m, closed-manifold checks before and after Boolean, unchanged head-specific bounds, five vertical rays inside the eye that must miss steel, and two outside-eye rays that must hit retained steel. The section rays would reject the original uncut head. Coordinates are valid in object space because `make_part` bakes source world geometry into an identity-transform candidate. Probes run before the tenon/wedge are added, so they inspect steel alone. Original source/caps remain untouched. No further blocking source finding.

The held pose now takes the matching imported NLA strip's action slot and rejects a missing active slot. This follows the installed Blender 5.2 glTF importer's `animation_utils.py` restore path, which assigns both action and slot. It removes reliance on action assignment alone; actual posing still requires render inspection.

The actual silhouette, join quality, materials, normals, pose and readability still need the planned front/side/rear/detail/clay/scale/held renders. No aesthetic approval is inferred from these source checks.

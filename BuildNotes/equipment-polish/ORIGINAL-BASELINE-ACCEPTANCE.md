# Original Wreckabulary baseline acceptance review

STATUS: REVIEW COMPLETE; original-art direction retained, player art acceptance incomplete. The owner confirms that the original Wreckabulary artwork is the stronger baseline. This read-only review compares actual original source images and preserved model/material references with the integration Windows captures from `20261023T204132Z-visible-wordbound_workshop_flow`. It owns only this report. No assets, runtime/source, profiles, kit, captures or unrelated dirty files are changed; no Unity, Blender, compiler or other heavy job is run.

The accepted direction remains original artwork by default. DetailV2 is owner-rejected; both Wordbound revisions and optional original-based experiments remain preserved and unapproved. Technical geometry/presentation passes are not owner appearance acceptance. The current Windows scenario timed out, so a ready screenshot cannot establish delivery or full equipment coverage.

## Reference and current evidence

Directly viewed the existing `Web/public/art/supplied-showcase.png`, original `Assets/_Project/Resources/UI/Items/{SOFA,FAN,HAMMER,BAT}.png`, and actual 204132 `wordbound_local_ready.png` and `wordbound_workshop.png`. The showcase renders the real original GLB library with a 1440×960 orthographic camera, three large Cycles area lights and AgX (`Tools/AssetPipeline/render_supplied.py`). The player is a 1280×720 perspective Unity capture, in different lighting, pose and surroundings. This is a direction/identity comparison, not an equivalent-camera raster regression test.

The original reference is clean and rounded: a pale sage two-seat sofa, light wood with restrained grain, a slender powder-blue fan, steel/pale-wood hammer, simple pale bat and small readable character. The native ready view retains the actual rounded avatar and wardrobe components. Red clothing, a dark satchel/boots and pale cuffs in this view are not proof of an original-material regression: the showcase uses a different default outfit and front pose. The screenshot contains no useful close view of SOFA/FAN/HAMMER/BAT; their live materials and silhouettes cannot be signed off from it.

Reviewed original-default/archive reports, gallery and root source at `4f5e038771ee987927036ef2a01523088555b62a`. The 204132 image itself belongs to QA build `20261023T203920Z`, source `d8eb1804da0b79b4253bfc44614aa5383b3941ae`, not the later source HEAD. Native original presentation 48/48 evidence remains scoped as described in `ORIGINAL-GAME-VISUAL-REVIEW.md`. Gallery defaults and archive labels are consistent: original ModelLibrary first, DetailV2 rejected, SOFA V4 without convincing improvement, FAN/HAMMER pilots unapproved.

## Concrete findings and limits

| Area | Actual finding | Evidence / decision |
| --- | --- | --- |
| Floor/wall material scale | The ready view has broad horizontal floor streaks and very enlarged wall texture. Source confirms Wordbound stretches one stock cube UV square over each independently sized surface. This loses the established original room's metre-based surface scale. | `WordboundRuntime/WordboundCourseWorld.cs: Box` creates a primitive, scales it and assigns material without the original UV mesh. Original `Game/RoomBuilder.cs: Surface` calls `Surfaces.Box(scale, Surfaces.Tile(kind), ...)`; workshop viewport does likewise. This is the strongest bounded correction. |
| Original item geometry/scale | No evidence here supports another replacement or a broad scale correction. All twelve item definitions match their original manifest dimensions except tiny published rounding: BROOM height +0.000027 m, SHIELD depth +0.000055 m, STOOL height +0.000015 m. | Compared `Data/Config/items.json` with `Web/public/art/manifest.json`. Resolver fits original mesh bounds to those sizes, grounds them, and preserves avatar proportions. HeldScale is intentionally separate. Native grip tests are useful technical evidence, but held silhouettes/animation still need actual views. |
| Item materials | Original selection does not mean untouched GLB material bytes. Existing runtime skin mapping and tactile variants still apply. No visible item-material defect can be established from this avatar-only ready view. | `Art/ModelVisual.cs` uses `MaterialLibrary.ApplySkin` and `TactileMaterials.Apply`; the latter limits detail to eligible existing material families. `Online/Presentation/WordboundVisualResolver.cs` selects original library via the assigned profile; `Presentation.asset` has no overrides. Preserve these paths until actual material comparisons justify a change. |
| Lighting | The ready room has harder diagonal light/shadow separation and warmer, darker floor than the soft studio showcase. This is visible, but the scene/camera/light difference prevents attributing it to wrong material values. | Original showcase uses area lighting/AgX; Unity has existing environment/GraphicsOptions lighting. Do not replace palettes or add noise to compensate. Keep lighting changes separate from the UV correction. |
| Camera/render configuration | Source risk, not measured cause: `WordboundPlayerView.Initialize` creates its camera after scene startup without calling existing `GraphicsOptions.ApplyTo`. That function enables HDR/MSAA/postprocessing and configured smoothing. Synthetic `WordboundPerformanceWorkload` explicitly calls it. | Scene-loaded `GraphicsOptions` application to Camera.main does not prove this later camera receives the same settings. Read back the actual player camera and quality settings before changing or blaming exposure/AA; source alone does not establish final native camera flags. |
| Framing/composition | The rear avatar occupies about half the ready image height. Empty floor/wall and overhead ramp dominate; checkpoint and narrative text remain clear to its right. This capture gives little sense of the original household furniture, and does not demonstrate the next playable route from multiple angles. | This is a visible composition limitation, not proof of incorrect physical scale or an obstruction bug. Current editable `PlayerCamera.asset` is FOV 55°, distance 3.3 m, pivot 1 m, lift 0.1 m, starting pitch 0.16 rad. Defer camera tuning until actual obstruction distance and a second route-facing view are recorded. |
| Workshop/UI | Current labels are separated and cyan geometry stays inside the viewport in the inspected image. The surface has recognizable board scale here, in contrast to the gameplay slab. | No new workshop overlap/spill defect observed. This remains one static floor/view, not mouse/controller usability acceptance. Compact HUD, screen-facing clue and checkpoint caption are readable; no missing shader or black-frame regression. |

All script paths in the table are below `Assets/_Project/Scripts/`. The material-scale finding is not a claim that the original furniture needs higher-resolution textures. `Art/Surfaces.cs` already contains the intended textures and metre-based UV method. Its Planks tile is 4×1.2 m and contains two board lengths/four rows: approximately 2×0.3 m per board. With stock 0..1 UVs over a 24×24 m slab, those same pattern divisions become approximately 12×6 m before any surface split. `Tint_Base.mat` uses unit texture scale. This explains the oversized pattern without changing the source texture or colour.

## Small next improvement: restore original surface UV scale

Implement only in `Assets/_Project/Scripts/WordboundRuntime/WordboundCourseWorld.cs`: use existing `Art.Surfaces.Box` / `Surfaces.Tile` for the default floor/plaster/wood surfaces, as the original room builder already does. Preserve each primitive's dimensions, world transform, surface tag, collider, collision callback, material identity and the resolved course/hash. Preserve supplied custom-material behavior; do not infer a texture scale for an arbitrary author material. Choose UV origin/orientation consistently across split floor/wall pieces so cutouts do not reset the visible pattern. Do not alter original model files, skins, artwork profile, camera or global lighting in this change.

Meaningful acceptance before integration:

- Native mesh assertions for actual course-generated surfaces: metre-based UV differences on two unequal floor sizes and split pieces; consistent seams across one cutout; wall/connector axes and slopes; exact mesh bounds, transforms and unchanged BoxCollider geometry. Exercise custom-material input explicitly.
- Reuse existing cached mesh/material facilities, with no new per-frame work or full-screen texture generation. Verify shared identities and repeated build/dispose behavior under the existing cache contract.
- Root-owned matched ready capture with the same build settings, seed, avatar/camera pose and 1280×720 frame: board scale should read as small original-style timber, with no stretched stripes, missing faces, dark normals, seam discontinuities or text/readability regression. Inspect a floor opening and sloped connector as well as the starting room. Camera and lighting stay controlled.
- Then a small original-only native arrangement or route-facing capture containing SOFA, FAN, HAMMER and BAT plus avatar at actual dimensions, followed by one held view. Confirm pale sage, powder-blue, steel/wood separation, rounded silhouettes, grounding and grips against original icons/showcase. These missing views remain acceptance work; this report does not imply they exist.

No-code review remains through the assigned visual profile's **Base Artwork → Original Wreckabulary**, copied course/profile assets and **Validate and preview exact seed → Play this exact preview locally**. Camera composition is editable under **View and follow**; Starting angle requires a recreated playtest/profile switch. None of those controls was changed here.

## Artifact identity and remaining scope

204132 full flow remains FAIL at 81/90 required Unity update waits; exact save/Return/history/Undo passed. See `BuildNotes/client-view/QA-PHASE-CAPTURE-REVIEW.md` for final log/check hashes and source/build binding. No FPS, production-quality, multiplayer or owner screenshot approval is inferred.

| Inspected image | SHA-256 |
| --- | --- |
| Original showcase | `F7EBEEFC2D74034F36EB73591BE413057E4C1A3A923FD94F1A967B120F7567E4` |
| Original SOFA icon | `BC569F1C62CE4D2E7DA14898F4F24EF6CDE2AB8499FAC9356C3E9962E77F0D63` |
| Original FAN icon | `6B87D913A5BB1EEB555E244C3B5CE3738CF61A5622E85F750D674AB70AA39AFF` |
| Original HAMMER icon | `5D4EA135115DD1336B209F3BABBFBF94723E5BD88AF5880D03358E7939A82505` |
| Original BAT icon | `86407243778293AEAEBF762F2CBA13965B3F074D19797E81F5E3309E241D4191` |
| 204132 ready PNG | `70E5BF1E75EBD9DD5B2052FE1379EFC7365B67B75B0F7EF4737A02A4FDB81057` |
| 204132 workshop PNG | `AD72EBA2592686855CB8E79D0156CA3C76E17E10B78BE01D0CAEF85D3D7BE8D0` |

Parent subsequently authorized the bounded surface-UV implementation after this report checkpoint. That will be a separate source/test commit and independent review; implementation and native/visual acceptance are not completed by this audit.

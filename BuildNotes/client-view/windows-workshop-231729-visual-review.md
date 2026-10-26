# Workshop Windows visual review — 231729

STATUS: HISTORICAL CANDIDATE — NOT PROMOTED; NEW RANGE CONTROLS READABLE, EXISTING HEADER CLIPPING REMAINS

All **8 actual 1280 × 720 PNGs** from `20261026T232018Z-wordbound_workshop_flow` were opened individually. Build `20261026T231729Z` identifies source `fe7f1738f7223b52393eb841c61a01867c53e396`, Unity 6000.6.4f1. The actual checks record **5/5 PASS**, no assertion failures and zero console errors/exceptions/warnings. Both recorded process exits are 0. This review ran no native jobs and changed no source images.

## New controls

The height view at scroll **748** visibly includes world X/Z bounds, **Height min / max = 16 m**, **0° / 90° / 180° / 270°**, all four floor choices (Packing, Kitchen, Attic, Rooftop), the disabled **Attic · room** choice, **Spawns stay in this room**, and **Apply / Cancel**. Text fits and the selected/required states are distinguishable. No blocker was found in these new controls at this sampled size.

The second view at scroll **986** keeps all floor choices, the room-only note, Apply/Cancel, Redraw bounds and the zone-selection actions readable. Rotation is partially clipped by the top scroll edge; the first image fully covers it. This is a deliberate second scroll position, not evidence that rotation disappeared.

## Existing routes and limitations

- Online entry retains the vivid sunroom and readable Workshop/Back actions; no network connection was attempted by this scenario.
- Normal and returned Workshop PNGs are **byte-identical**. The selected-module toolbar exposes an existing issue: **Group / Ungroup / To this floor wrap and clip in the short three-button row**. This remains a concrete layout correction, not an accepted part of the whole editor.
- Both playtest frames show the original red avatar, world camera, timer, checkpoint and LMB/Q/ESC hints. The room remains sparsely dressed and checkpoint text is light against the wall; this is not final-art acceptance.
- The equipment menu preserves original thumbnails, readable missing-letter guidance, disabled Craft and Return to workshop. Its scroll viewport partially shows the last item row while keeping the detail/return controls fixed.
- The unchanged **trial version** label is visible at bottom right in all eight originals. Its cause is unconfirmed. It remains a release limitation; no image was edited, cropped or relabelled.

## Receipt evidence and acceptance bounds

All range view/history/snapshot/revision/Undo–Redo/primary-byte/manifest preservation probes are true; cleanup errors are empty. Readiness took **3.373392 s**, canonical progress reached **179 ticks**, and the exact manifest/draft checks passed. Return records same history/draft, destroyed client/camera and visible editor; the final exact Undo/Redo step passed.

This establishes actual layout visibility and the existing automated route for one selected Attic zone. The fixture uses the real selection handler and temporary view fields, not physical pointer/controller input. It does not establish applying changed values, every resolution/font, cross-floor spawning, full course completion, story endings or public multiplayer. The build's retained provenance reports dirty=true; this visual review is not a separate full source-preservation audit. Image hashes and selected raw receipt values are in the adjacent JSON.

## Unaltered actual views

![Height, rotation and floor controls](C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Docs/qa/runs/20261026T232018Z-wordbound_workshop_flow/wordbound_workshop_range_height.png)

![Floor permissions and placement actions](C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Docs/qa/runs/20261026T232018Z-wordbound_workshop_flow/wordbound_workshop_range_floors.png)

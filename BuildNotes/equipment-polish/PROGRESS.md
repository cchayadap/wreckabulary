# Equipment detail and editable presentation

- [x] Inspected all twelve actual Wordbound three-quarter renders and the legacy Arcade/Winter shield native screens.
- [x] Confirmed current editable Blender source, two-material pipeline, 512px texture cap, 6000-triangle cap and immutable catalogue dimensions/grips.
- [x] Audited current Unity usability: collection entries expose model/icon/size/grip/heldScale but imported FBX roots are not independent authoring prefabs; the dedicated importer overwrites material values from constants; default held placement follows the hand without a defensive pose. James owns the new persistent visual/camera profiles and authoring workflow.
- [~] Added persistent WordboundDefensePose type and narrow GameplayPresentation hook. Profile field is James-owned; resolver Profile exposure is root-owned. Compilation and native tests NOT RUN for this new slice.
- [ ] Implement separate Blender detail revision and bounded material-atlas importer support. Root reserved WordboundEquipmentImporter.cs for this lane.
- [ ] Blender build, independent export validation, actual previews and source comparison after root grants a heavy-process slot. No render or model batch has run in this slice.
- [ ] Native material/prefab/pose validation and owner acceptance remain pending.

Visual direction: preserve the rounded household-object silhouettes, sage/cream/terracotta identity and readable game scale. Improve material differentiation with directional wood grain, restrained textile weave, glazed ceramic and painted-metal surface response, plus selective seams/joints/fasteners. All twelve current models have useful component separation, but their shared smooth palette finish makes these materials hard to distinguish. This is an agent inspection, not human style approval.

Defensive pose source uses actor-local rotations around the authored grip. Defaults keep the grip exactly at the hand; PLATE rotates its horizontal dish 90 degrees to face forward. Persistent offsets are bounded artist hand-placement corrections. Pose applies only to the exact selected Held item named by the canonical defense lease; release/expiry/migration/fault/unavailable world resets it immediately. Render blending is visual only. Existing carried furniture and keepsake paths are preserved.

Integration: `c235a46` imports the already-integrated selected-equipment presentation baseline and must not be cherry-picked into root. Apply the later pose delta over the root's current GameplayPresentation plus James's VisualProfile and root's VisualResolver.Profile hook. Root owns native Unity execution and all generated metadata.

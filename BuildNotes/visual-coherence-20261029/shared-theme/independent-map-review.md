# Independent shared-theme source review

STATUS: COMPLETE — no blocking defect found in this bounded read-only skim. Native migration, repeat-byte preservation and actual visual acceptance remain pending. This is not a full shared-theme/runtime test review.

Files inspected: LobbySharedThemeSetup.cs, LobbyInterfaceTheme.cs, LobbyInterfaceFrame.cs, and the relevant WordboundUiTheme/shared references plus current saved-frame creation paths.

- Fresh apply checks Play mode, affected dirty asset/components and open Prefab Mode before writes. All seven frames are role-planned before backup/migration. Exact seven-prefab plus online-theme bytes and metadata are backed up and hashed; editable archive copies are separate.
- Repeated apply requires COMPLETE.txt, verifies the closed16-file backup checksum roster and saved adapters/theme references, then returns without overwriting designer edits. Incomplete migration remains a deliberate manual-recovery case rather than an automatic retry.
- Palette validation rejects nonfinite/out-of-range RGB and nonopaque alpha; role lookup rejects undefined enum values. Frame validation rejects null/duplicate/external graphic targets, RawImage artwork targets and mismatched gradient ownership.
- PlanBindings deliberately changes local Graphic colours and named shape backgrounds; actual shop/equipment artwork uses RawImage and is not included by the Image traversal. The saved Shop Art object is a painted background container, not the borrowed portrait image. No source texture, sprite, mesh or material mutation was found in this migration.
- Semantic role assignment preserves object/layout references and TMP font/material references. Online receives a saved shared theme reference and Roboto TTF references. Original artwork objects are not replaced.

Limits: role classification is based on the known seven saved frames; unknown opaque Image names intentionally stop the migration. Native preflight must confirm every current frame still classifies. The new dark/blue palette is a candidate requiring actual owner-facing screen review, not proof of improved world/style coherence. A later designer-added Image sprite is not covered by this known-frame inspection; retain explicit role review when extending layouts.

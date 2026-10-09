# Player authoring migration — 2026-10-09

STATUS: NATIVE TESTS PASSED; final saved-scene image review belongs to the lead

## Confirmed mismatch

- Saved `Player.prefab` contains 18 legacy primitive objects, no PlayerAppearance and no SkinnedMeshRenderer. PlayerController.Setup previously added the imported avatar only at runtime.
- Saved Hub and LivingRoom contain no player or imported avatar instances. Their cameras retain the old FOV 40 / near clip .3 and obsolete CameraRig `follow` field; current close perspective is configured only at runtime.
- PrototypeBuilder.BuildAll still overwrites the player prefab and scenes with primitive content. The lead owns its guard and the saved scene migration.

## Delivered source

- `PlayerAuthoring.UpgradePrefab()` loads the existing player prefab and adds a nested imported Avatar plus serialized PlayerAppearance.AuthoredAvatar. It hides legacy geometry while retaining the gameplay roots, collider and grip proxy references. Once the authored avatar exists, rerunning does not save or replace the prefab.
- The first migration writes separate CharacterDefaults material assets for the deterministic unsaved hoodie/satchel outfit. Existing materials are reused without repainting them, and the source FBX/material library remains independent.
- `PlayerAppearance.Initialize` reuses the authored hierarchy. Legacy geometry cleanup runs only on the fallback path, so artist accessories outside the imported skeleton remain visible. Runtime wardrobe tinting recognizes the persistent default material variants. Animation graph, grip proxies, facial morphs and saved player wardrobe behavior remain intact.
- `PlayerAuthoring.CreatePreview(parent)` produces an editable visual-only EditorOnly-tagged avatar with a sampled idle pose. It has no player controller or Rigidbody; repeat calls preserve the existing child. Unity strips the root from builds. `EditorScenePreview` now disables the parent during Play at execution order -1000; SceneWorkspace attaches the marker.
- `PlayerAuthoring.FramePreview(camera, avatar)` explicitly sets the close camera lens and framing; it never runs automatically over an artist camera.

No runtime player Setup, physics initialization or player preferences are used by authoring helpers. No Unity process or scene/prefab asset write was performed by this lane; the lead invoked the helpers through native Unity.

## Verification

- PASS: scoped diff whitespace check.
- PASS: all three PlayerAuthoringTests verify baked avatar/hidden old geometry, idempotent preservation of artist overrides, and visual-only build-excluded previews.
- PASS: all four PlayerAppearanceTests verify authored avatar reuse, artist wrapper offset/accessory preservation, actual moving legs and item grip alignment, imported morphs, and preview deactivation. All five CameraRigTests also remain green.
- Native evidence: `Logs/authoring-editmode.xml`, 2026-10-08 16:46:03–16:46:06 UTC: **194 passed, 0 failed, 7 skipped**. Includes all four saved-scene authoring cases.
- Native evidence: `Logs/authoring-playmode.xml`, 2026-10-08 16:46:29–16:48:39 UTC: **135 passed, 0 failed, 4 skipped**.

## Scoped scene workflow hardening assigned by lead

- SceneWorkspace uses the persistent SceneAsset label `CurrentAuthoringV1`, written only after a successful scene save. Renaming/removing a preview does not retrigger default lighting/camera changes. Pre-label previews are found through their EditorScenePreview component and reused even when renamed.
- The static arena preview stages at `(-6.2,.15,7)`, yaw 90, keeping its full 3.15 m camera boom inside the west wall. Hub stages at `(0,0,-3.8)` facing forward through the invisible front wall.
- SceneAuthoringTests inspect isolated preview scenes and close them without replacing the user's open scenes.
- AuthoringCapture refuses dirty or meaningfully unsaved scene work, restores camera position/rotation/FOV/target and shader compilation settings in finally blocks, and restores the original scene setup. It writes no inspected scene changes.

Next recoverable action: lead reviews saved-camera and editor-overview images from AuthoringCapture, then completes the integrated workspace delivery. This report does not claim a final image pass without viewing those artifacts.

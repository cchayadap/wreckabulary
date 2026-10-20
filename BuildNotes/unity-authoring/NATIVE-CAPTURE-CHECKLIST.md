# Native authoring review and capture

STATUS: PENDING. No screenshots or human acceptance are recorded yet. Capture the actual Unity editor; do not generate mock evidence.

1. From a clean saved editor setup, run `Wreckabulary.Online.Editor.WordboundAuthoringSetup.CreateDefaults`. Capture the Project assets and LaunchAssets Inspector with CameraProfile, VisualProfile and WorkshopCourse assigned. Run again and confirm chosen assets are preserved.
2. Open a separate editable course copy. Capture module/layer/physical-site/random-zone forms and the Scene module handle. Move a module, apply, save, unload/reload the asset, and show its owned spawn/site positions persist. Verify Undo, invalid-data feedback and the explicit pending-form warning.
3. Capture the exact preview, including seed and source/manifest/content identity. Close/reopen it and verify there are no leftover generated world objects in normal scenes. Change a saved course field and confirm stale preview play is blocked until regenerated.
4. Capture CameraProfile and VisualProfile Inspectors, a render-only prefab with Grip_R, and assigned material slots. Enter local play, edit a valid camera value, then an invalid value; observe valid change and last-valid fallback. Confirm imported artwork bounds/grip and raised SHIELD/PLATE pose with canonical body colliders unchanged.
5. Play the exact preview locally. Record actual runner start, movement and return. Check previous scene order/active state after success and controlled startup failure. Include saved + empty untitled additive scenes; save any populated untitled scene before this test.
6. Open the ordinary launcher Workshop. Confirm the saved course's title, seed, mode, item/rule bytes and profile choices are used. Edit and return from local play; document that workshop autosave is a separate draft and JSON export is the transfer route back to the asset Inspector.

Run `Wreckabulary.Tests.WordboundAuthoringTests` and `Wreckabulary.Tests.WordboundAuthoringPersistenceTests` from a clean scene. Record exact source revision, native test XML/log, screenshots and limitations. The persistence fixture constructs the actual static preview and validates play setup; it does not start a runner or certify all six puzzle families.

# Household front and rear review

STATUS: EDITOR IMPORT VERIFIED; FINAL ART AND PHYSICS ACCEPTANCE OPEN.

Root inspected all four capture-01 images and all eight capture-02 images on 2026-10-26. The second set uses matching framing for front/rear pairs of the gallery, kitchen, attic and rooftop/parcel groups. Native capture `113334` passed in 65.3 seconds. All eight previews closed with zero remaining owned objects; the receipt records unchanged input files, material state and editor globals. The separate 275-file setup preservation comparison also passes after the intentional capture-code change, with all 102 generated files unchanged.

The new front views visibly expose the oven door and shelves behind its glass, fridge handles, cabinet fronts and drawer pulls. The earlier rear views hid these features; they remain useful controls. Both sides keep the source silhouettes and colors. No missing or magenta material is visible in these images. This does not establish every underside, joint or normal-map condition.

The set is still basic supplier furniture. Beige wood and pale metal dominate, with broad uniform surfaces and little painted identity or surface detail. Door separation and small handles are weak at gallery distance. The stove offers more readable construction than the cabinets. The plant provides a useful turquoise accent, but its pointed leaves need silhouette and collision review. The supplied parcel has visible flap geometry despite its source name; its static box collider is only an envelope, not tested breakable packaging.

The captures deliberately have no floor, motion or physics simulation and disable MSAA. Their floating arrangement and jagged silhouette edges are diagnostic presentation limits. They are not gameplay shots or proof that the final game should look this way.

Before map use:

- Develop distinct painted palettes and selective trim for each destination, alongside the original Wreckabulary assets. Keep supplier sources unchanged and edit saved materials or explicit variants.
- Review bevels, door/drawer gaps, underside construction and readable handles at game-camera distance. More geometry alone is not an improvement.
- Author colliders for the seven currently unconfigured props. Review parcel contact, door/drawer clearance, tipping, mass distribution and any break behavior separately.
- Test actual room placement, navigation, letter/puzzle contrast, shadows, repeated-object cost and LOD behavior in the Windows player.

The scene is saved at `Assets/_Project/Scenes/Authoring/HouseholdStudio.unity`; it is an authoring review scene, not an enabled player map. Use Workspace → Scenes to open it and Workspace → Design for its prefab folder and initial sizing settings. Existing prefab edits remain authoritative on repeat setup.

Evidence: `capture-01/receipt.json`, `capture-02/receipt.json`, `../household-native-20261026.json`, and `../household-preservation-2026-10-26T11-38-59-963Z.json`. Each capture receipt distinguishes ordinary on-disk inputs from expected absent metadata; do not report all proof records as existing files. Detailed hero equipment, animation and James's visual acceptance remain unfinished.

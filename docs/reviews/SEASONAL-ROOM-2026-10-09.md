# Editable Pinwheel winter corner

STATUS: NATIVE VERIFIED; INCLUDED IN WINDOWS PACKAGE 3fe398b

Baseline: a09ed63. Added a native tree, three wrapped gifts, a wall-mounted wreath and brass garland to the saved Pinwheel living room. This is authored map decoration; previewing a lobby theme does not change the map.

## Editing

Open `Assets/_Project/Resources/Worlds/PinwheelHouse.prefab`, then `Geometry / Storey 0 / Winter reading corner`. Each object, position and saved material remains editable. `Wreckabulary > Authoring > Add Pinwheel Winter Corner` fills a missing collection only. Repeating it preserves an existing collection, including renamed, moved, hidden and individually deleted artist elements. Remove its `SeasonalRoomDressing` collection root deliberately only when you want a fresh corner.

The collection uses six renderers and 5,088 triangles. Five meshes and all materials reuse the verified Winter collection; the brass garland is a separately saved mesh. It adds no lights, colliders, rigidbodies, smashable furniture or letter ownership. Wall decoration participates in its room's existing camera cutaway; floor decoration remains visible from above. No frame-rate improvement is claimed.

## Verification

- Six EditMode tests passed in 12.17 seconds. They check unchanged original transforms, meshes, material references, physics and furniture; persistent references after prefab reopen; preservation of artist changes on repeat; real saved wall-face placement; and rollback on an occupied corner.
- Authoring command completed with `PINWHEEL_WINTER_READY`. The production prefab diff is additive: 681 inserted lines and no removed lines.
- First native journey passed actual character movement through both nearby doorways, furniture breaking into letter tiles, and two furniture resets. Its later couch setup incorrectly joined after bots filled the match; corrected the fixture to join local seats before starting. This was a fixture issue, not a gameplay-capacity change.
- The final native run passed **7/7 tests in 17.33 seconds**, including all six camera-cutaway cases and the seasonal journey. Seven actual screenshots show solo TPP at 16:9/21:9, upward wall detail, post-reset decoration, two/four local seats and active Moving Day. The four-seat match includes active foam shields. Moving Day Retry preserves the same corner while resetting gameplay furniture. Evidence: `Logs/seasonal-room-native-verified.xml` and `.log`.
- A fresh editor process reran the production authoring command and reported `PINWHEEL_WINTER_PRESERVED`. The prefab SHA-256 was unchanged: `569e4abc1db1c81a624965e972940e976494161568072e9b8e0446db23b230ae`.

Source and failed first-capture evidence remain in `Logs/checkpoints`; final native artifacts are under `docs/reviews/evidence/unity-seasonal-room-2026-10-09`. Native visual review is by the implementing agent for this batch. Windows package 3fe398b includes this corner: build succeeded with zero errors and packaged startup passed. See LOBBY-PARTY-2026-10-09.md and its verification.json for hashes and scope. Lunar icon/model/audio integration remains separate and incomplete; the ongoing asset-expansion goal stays active.

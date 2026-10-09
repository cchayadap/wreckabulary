# Lobby navigation and unobscured themes

STATUS: COMPLETE

Baseline: 23a089b. User requested one coin/shop control, a colored leaderboard icon next to Career, mode selection integrated with Play, no visible CHANGE labels, and removal of the large cream washes over the theme artwork.

Implemented: top-bar wallet is a focusable Shop button with a live-sized coin balance; the gold leaderboard trophy follows Career; centered Play includes the current mode; Home keeps only an unfilled house selector and GO. Removed top/right/bottom screen scrims and redundant cart glyphs. Existing page close/focus, controller page cycling and queue selection remain.

The first native pass exposed insufficient contrast on busy artwork and a blank mode label: the old 24-pixel rectangle was shorter than the font's line span. The final pass uses cached per-font SDF outlines and small icon outlines, leaving all backgrounds exposed, and gives the subtitle 34 pixels. Regression assertions inspect real glyph geometry, including the longest supported mode.

Unity 6000.6.3f1 verification: **49/49 PlayMode tests passed** in 75.03 seconds, including the complete LobbyTests fixture, two navigation regressions and the explicit native screenshot journey. Evidence: `Logs/lobby-navigation-verified.xml` and `Logs/lobby-navigation-verified.log`. Tested pointer hit targets, submit/controller focus return, live currency sizing, mode refresh, queue restrictions, tutorial/workshop routing and existing lobby flows.

Twelve native screenshots: `docs/reviews/evidence/unity-lobby-navigation-2026-10-09`. All four themes were captured at 1600x900 and 2100x900; further frames show Shop, leaderboard, Play and a changed mode. Root and an independent read-only reviewer inspected the final frames and found no remaining contrast, alignment or subtitle blocker. The rejected first pass is preserved in `Logs/checkpoints/lobby-navigation-first-capture`.

Windows package rebuilt from a2b379b: **Succeeded, zero errors, 233,814,383 bytes**. The hidden-window startup check stayed responsive for **69.26 seconds with zero logged errors**, then the test-owned player was closed. This is startup coverage; interaction and rendering coverage are the native tests above. Launcher, gameplay assembly and resource hashes are in `verification.json`. The local launcher remains `Builds/Windows/Wreckabulary.exe`.

Build-generated settings/material serialization changes were inspected, preserved in `Logs/checkpoints/lobby-navigation-build-side-effects`, and restored to their authored versions. Seasonal map and Lunar source work remain separate and are not part of this UI checkpoint. The broader asset-expansion goal remains active.

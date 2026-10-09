# Lobby compact panels and party presentation — 2026-10-09

STATUS: COMPLETE

## Acceptance

- Five compact painted areas match the latest marked screenshot: system controls, text navigation, wallet/party, theme badge, start dock.
- LOADOUT, PLAY, CAREER, LEADERBOARDS use text only. Shop remains the live coin balance.
- START is above smaller mode and map selectors; both selectors remain actionable. No CHANGE label or broad artwork scrims.
- Existing local lobby seats show one to four animated avatars. Solo/side-page framing, saved seat outfits, theme selection, emotes and game launch retain their behavior.
- Native Unity captures at 1600×900 and 2100×900, functional navigation checks, then a Windows package including the previously verified seasonal room.

## Implementation / scope

Local cream panels preserve visible theme artwork and use dark ink with teal/gold accents. Guest avatars reuse the gameplay wardrobe resolver; up to three cached models/animation graphs and contact shadows join the existing host. Hidden guests stop their graphs. Winter display props move to the edges when a group is visible and return to their authored solo positions on side pages or when guests leave.

This is local couch-party presentation. Online/LAN matchmaking remains unavailable as stated in the existing lobby. Images in evidence are native Unity captures, not generated mockups. This batch reuses the existing GPT theme artwork and native assets.

## Verification log

- 7216ebf seasonal room baseline committed/pushed before this change; it is included in this combined Windows package.
- Initial native run: Logs/lobby-party-native.xml and .log; 56/56 passed.
- Visual review: accepted after the bounded prop-position correction, root self-review. No independent review claimed.
- Packaged Windows build: succeeded with zero errors, 233,836,879 bytes; responsive startup for 111.80 seconds with 0 logged errors.

## Later batch

Lunar source remains preserved outside Assets. This completed lobby request does not mark the broader asset collection work complete.

Initial verification passed: **56/56** native PlayMode checks in 90.5509928 seconds (complete LobbyTests, navigation, party capture and PlayerAppearanceTests). The focused follow-up passed **9/9** in 27.8186039 seconds (navigation including real idle-bone motion/stopped hidden graphs, capture, WinterCollectionTests). Initial evidence is preserved under Logs/checkpoints/lobby-party-first-capture and lobby-party-second-capture.

Visual iteration: the first four-player winter frame hid the wreath behind P4. The second moved both edge props outward; review caught gifts at the left edge. Final composition keeps the tree and wreath outside the lineup but brings gifts inward, with a projected renderer-bounds check preventing clipped props. The capture journey now equips Winter before visiting Shop so restoring a temporary preview cannot silently change the requested theme.

The projected-bounds check caught the winter tree within 0.00449 viewport units of the left edge (required inset 0.005). Its position and the wreath were brought slightly inward. The failed composition run is retained in Logs/checkpoints/lobby-party-bounds-check; the test threshold was preserved.

Final native composition/decor/emote run: **5/5 passed** in 24.2086247 seconds (Logs/lobby-party-verified.xml/.log). Sixteen final screenshots and the completed party-journey.json cover all four themes, one/four players at 16:9, four players at 21:9, two/three Winter players, page changes, guest reuse, release/take-camera and restored solo framing. Native projected bounds verify separate avatar silhouettes and safe prop edges. Root inspected the updated Winter frames and the other theme lineups.

Windows was built from **3fe398b**, including seasonal room **7216ebf**, and the test-owned player was closed after the startup check. The launcher is `Builds/Windows/Wreckabulary.exe`; gameplay assembly/resources hashes are in `verification.json`. Twenty reviewed build-generated settings/material paths were preserved in `Logs/checkpoints/lobby-party-build-side-effects` and restored. This check proves packaged startup; rendering and interaction evidence comes from the native Unity tests and screenshots above.

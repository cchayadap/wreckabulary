# Lobby image review — 26 October 2026

STATUS: COMPLETE — static-image review only. Reviewer: `story_status`. No live edits, native jobs or interaction tests.

Inspected all 19 PNGs in [the 11:54 capture folder](../developer-workspace/lobby-capture-2026-10-26T11-54-47-483Z): Home, Practice, Loadout, Recipes, Career, Shop Looks, Shop Colours, Leaderboards, Quit, four Settings pages, two-player Party and five selected-map Home states. The images are 1600 by 900. Root reports capture run `115448` passed; this report evaluates the visible output, not that gate's internals.

No new blocking layout failure is visible in this set. Primary navigation, Start/Go, close controls, selected tabs, item prices and the Quit/Stay choices are legible and contained. Home has a clear central avatar and a compact action area; Loadout keeps the character visible beside its controls. This limited result does not establish smaller-window layout, focus order, controller use, hit targets, purchase behavior or online functionality.

## Concrete findings

| Priority | Evidence | Observed issue | Required follow-up |
| --- | --- | --- | --- |
| Before final visual acceptance; already tracked | [Shop Looks](../developer-workspace/lobby-capture-2026-10-26T11-54-47-483Z/lobby_5_shop.png) | All six look thumbnails have a narrow cream rectangular background inside a wider mint image area. The portraits also appear darker than the large live avatar. The rectangular matte is conspicuous and breaks the card's background treatment. | Finish the existing native alpha/lighting work, then recapture this actual Shop view. Inspect all six silhouettes, skin/accessory colours and edges on the mint card background; accepting isolated PNGs is insufficient. |
| Before claiming online scope complete; does not prevent a labelled QA build | [Party](../developer-workspace/lobby-capture-2026-10-26T11-54-47-483Z/lobby_8_party.png), [Leaderboards](../developer-workspace/lobby-capture-2026-10-26T11-54-47-483Z/lobby_6_trophy.png), [Home](../developer-workspace/lobby-capture-2026-10-26T11-54-47-483Z/lobby_1_home.png) | Party says friends appear “once online or LAN play is built”; Leaderboards says online boards “arrive with online play”. Home simultaneously labels Wordbound “Online parties”. The screens do not make the local-party/Wordbound distinction explicit in those messages. | Replace future-tense placeholders with concise, accurate scope labels and a clear Wordbound route where appropriate. Do not promise online rankings unless implemented. These images do not prove a networking defect. |
| Minor consistency polish | [Shop Looks](../developer-workspace/lobby-capture-2026-10-26T11-54-47-483Z/lobby_5_shop.png), [Shop Colours](../developer-workspace/lobby-capture-2026-10-26T11-54-47-483Z/lobby_5b_shop_spotlight.png), [Loadout](../developer-workspace/lobby-capture-2026-10-26T11-54-47-483Z/lobby_3_loadout.png) | Shop prices use a small black circular currency glyph; the wallet and Loadout prices use the gold W coin. The same currency has visibly different symbols. | Reuse the gold coin icon at a readable small size when this UI is next changed. No build-blocking failure is established. |

## Scope limits that must remain visible

- Recipes shows a partial next card row; Controls shows a partially visible Aim row at the bottom of its list. These appear to be scroll-window boundaries. A still image cannot establish whether the remaining rows can be reached, so this review does not classify them as broken scrolling or hidden controls. Verify scrolling and keyboard/controller focus in the Windows run.
- The five selected-map images are Home states with changed map labels and toasts. They do not show the maps themselves or prove their geometry, scale, navigation or playability.
- The shown Play page is Practice. Matchmaking and Workshop tabs are present, but their contents and public-queue states are not shown in this capture set.
- No further concrete clipping, overlap or unreadable primary-action defect was observed in the reviewed states. The known portrait issue remains the only clear image-compositing defect identified here.

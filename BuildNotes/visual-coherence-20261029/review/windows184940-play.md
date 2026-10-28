# Windows 184940 Play review

STATUS: COMPLETE — 11/11 ASSIGNED ACTUAL PNGS OPENED

Reviewed full, unchanged 1280×720 Windows captures from `Docs/qa/runs/20261028T185132Z-lobby_experience`, build `20261028T184940Z`. Root supplied source identity `c4258b18`; checks.json independently confirms the build/run/scenario. This is a visual review, not owner acceptance or a production-readiness claim. No native process, live source, image, crop or overlay was changed. This report is the only write.

## Findings

1. **Map imagery remains unfinished.** All three Solo captures still show the BOX and TABLE fallback illustrations for Lost Address and Parcel Hall. They identify props, not the actual maps. They do not demonstrate room layout, environment detail or the difference between the two courses. This remains a material gap against the owner's request for imagery that fits the playable map. The new dark surface does not resolve it. Create Wordbound likewise uses TABLE as its generic editor illustration.
2. **Large unused space remains.** Normal Local/Solo states have approximately 170–185 pixels of empty vertical space between their choice area and fixed footer. Create Wordbound has over 300 pixels below its single compact card, plus the empty remaining row. Escape has the same one-card/empty-row situation. The compact single-card footprint is preserved correctly; this review does not recommend stretching it back across the row or inventing extra choices. A later real-content/layout decision is still needed if the owner wants a fuller option area.
3. **Floor-plan labels are small.** GROUND/UPSTAIRS and GROUND/1ST/2ND are visible without clipping in the original-size Dibs/Duos/Build captures, but the three-floor labels are tiny. The floor and room count below each card remains readable. Inspect smaller displays and viewing distance before calling this fully accessible; no scale/font measurement or accessibility certification is claimed.
4. **Online/Home still use the illustrated sunroom.** The Online form now shares the dark neutral surfaces, restrained typography and amber primary action with the rest of the interface. The full illustrated background remains visually different from a playable-room capture. Its accuracy as map preview is not established by these screenshots.

## Confirmed visual behavior

- Selected mode and selected house/map are independently marked with a cyan border and circular check. The labels and bottom context agree in Dibs, Duos, Escape, Time Run, Freestyle, Learn and Build. The active top category uses a cyan underline; selection is not conveyed only by colour.
- All nine Local/Solo/Create captures have no character obstructing the options. On return Home, the character is visible again. The original item art remains vivid against the neutral card surface and has no observed tint artefact or clipping.
- Local/Online/Create labels and Close are visible in every assigned Play capture. No decorative stripe/ray background is present. Thin category/selection lines are functional dividers, not background stripes.
- The two-seat Solo capture shows Local2/4, a visibly disabled Play action, and the direct reason: “Solo only. Leave extra local seats to start.” No false multiplayer promise is introduced. The red reason remains readable; the muted button is visibly unavailable.
- Build a home shows Pinwheel/Garden available and City Flat/Terrace/Walk-up visibly disabled. Selected Pinwheel and the fixed action remain intact. The screenshots alone do not establish the reason for disabled house types or tooltip discoverability.
- The fixed Play/Open action and footer text remain within the panel in all nine states. I found no truncated mode/map title, overflowing subtitle, footer overlap, missing glyph, pink texture or obvious rendering corruption in this roster.
- Compared with the three opened before images, the new dark neutral surfaces and bright cyan selection edge make the active choice easier to distinguish from the previous multiple coloured fills. The amber filled action is more prominent than the previous text/underline action. The Online typography now appears consistent with the lobby instead of the earlier heavy rounded style. This is reviewer judgement; the owner has not accepted the aesthetic.

## Exact new images opened

1. `lobby_local_dibs.png` — Dibs and Pinwheel selected; five house plans visible.
2. `lobby_local_duos.png` — Duos and Pinwheel selected; footer identifies one human plus bots.
3. `lobby_local_escape.png` — Escape and Random house selected; compact card and “New each run” visible.
4. `lobby_solo_timerun.png` — Time Run and Lost Address selected; BOX/TABLE map fallbacks remain.
5. `lobby_solo_freestyle.png` — Freestyle and Lost Address selected; both map titles intact.
6. `lobby_solo_two_seats.png` — two local seats; Solo action disabled with reason.
7. `lobby_create_learn.png` — Learn selected; full-width tutorial note remains a separate note, not a map thumbnail.
8. `lobby_create_build.png` — Build selected; available/disabled house cards distinct.
9. `lobby_create_wordbound.png` — compact editor card; Open fixed at footer; large blank area remains.
10. `lobby_online_ready.png` — guest name/Connect/Workshop/Back form, before connection.
11. `lobby_home_returned.png` — avatar restored and local party1/4; home shortcuts visible.

Additional unchanged before images opened from `Docs/qa/runs/20261028T172356Z-lobby_experience`: `lobby_local_dibs.png`, `lobby_solo_timerun.png`, `lobby_online_ready.png`. No other current screenshots are claimed reviewed by this lane; Customize/Shop/Settings belong to the menu reviewer.

## Test evidence and limits

checks.json SHA-256: `b09cd8b16559a18311fa78145b041a07c273c756bacc15a6310ab7e0a75f1e89`. It records PASS for all five scenario steps, 24 total captures and zero errors, exceptions or warnings. These screenshots show menu states and an Online entry form. They do not prove connection, matchmaking, cross-machine gameplay, all resolutions, controller navigation in motion, map-quality acceptance or complete production readiness. All three Solo shots select Lost Address; a visibly selected Parcel Hall state is outside this capture roster.

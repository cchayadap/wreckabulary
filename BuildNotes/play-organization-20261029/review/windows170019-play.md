# Windows170019 Play review

STATUS: BOUNDED VISUAL REVIEW COMPLETE — 11 NEW IMAGES OPENED

Actual build `20261028T170019Z`; run `Docs/qa/runs/20261028T170223Z-lobby_experience`. Independently read `checks.json`: PASS, all5 steps PASS,24 declared captures. Opened all11 requested Play/return images directly, plus3 exact preferred232801 references. No live edits/native execution. Original screenshots remain unchanged, including any trial labels in their files. No owner approval or production claim.

## Findings

No confirmed text clipping, overlapping essential controls or wrong-selection defect in the11 inspected new images. Roboto labels are legible at1280×720. Mode and map selections use borders plus checks; the two-seat solo state has a disabled Play action and a concise reason. All9 Play-family views omit the character, while Home restores it.

The requested aesthetic is **not yet fully demonstrated**:

1. **P2 — Sparse states still leave large unused surfaces.** The normal House/Solo screens leave roughly170px between the final cards and footer in these720px captures. `lobby_create_wordbound.png` leaves about300px after its only card. Its single TABLE illustration occupies a small part of an almost screen-wide green card. Escape's Random house and Learn's room panel have the same stretched-single-choice problem. This conflicts with the owner's request for less empty space and a fuller composition. Preserve fixed actions and generous targets, but compose sparse states deliberately rather than stretching one tile and retaining a large blank region below it.
2. **P2 — Solo map cards still show object identifiers, not maps.** Lost Address uses BOX; Parcel Hall uses TABLE. Labels and selection are clear, but players cannot compare the actual environments. This limitation was explicitly disclosed in the draft and remains visible in the actual build. Actual map previews or recognizable authored course views are needed before claiming the map presentation is finished.
3. **P3 — Online entry is visually disconnected.** `lobby_online_ready.png` uses a large bold form with Guest name, Connect, Workshop and Back, while the new Play page uses Roboto card navigation. Nothing is clipped, but the transition still feels like a separate interface. This screenshot shows entry readiness only; it does not prove successful online matchmaking.
4. **P3 — Remove redundant microcopy when revisiting Create.** Learn repeats `Learn` as both title and detail, and Build a home repeats `Build`. The repeated words add little to the already-clear artwork/labels. This is minor compared with the composition and map-preview gaps.

## Exact new-image coverage

All files below belong to `Docs/qa/runs/20261028T170223Z-lobby_experience`.

| Image opened | Observation |
| --- | --- |
| `lobby_local_dibs.png` | Short illustrated mode band above larger map cards. Dibs and Pinwheel selected; local bots stated in footer. No avatar or stripes. |
| `lobby_local_duos.png` | Duos selection is clear; footer says one human and a bot versus2 bots. Map selection remains Pinwheel. |
| `lobby_local_escape.png` | Escape/Random house selections clear; New each run is readable. Single random card is oversized and generic in appearance. |
| `lobby_solo_timerun.png` | Time Run and Lost Address selected independently. Map names fit. Object art does not depict either course. |
| `lobby_solo_freestyle.png` | Freestyle selection changes while Lost Address remains selected. Text and main action fit. |
| `lobby_solo_two_seats.png` | Local2/4, disabled Play and red solo-only reason agree. No warning box or overlap. |
| `lobby_create_learn.png` | Learn selected; separate tutorial-room panel and footer. Duplicate Learn wording and stretched sparse panel. |
| `lobby_create_build.png` | Build a home selected; supported Pinwheel/Garden choices are distinct from disabled other houses. All map names fit. |
| `lobby_create_wordbound.png` | Wordbound maps card and Open action are clear; local one-player scope remains accurate. Largest empty lower region. |
| `lobby_online_ready.png` | Entry form has legible fields/actions and original lobby background. Older form style differs from new Play. Connection itself is outside this capture. |
| `lobby_home_returned.png` | Same original-style avatar and warm lobby background return. Navigation, local-play dock and party-leave notification fit. |

## Preferred232801 comparison actually opened

From `Docs/qa/runs/20261026T233250Z-lobby_experience`: `lobby_local_dibs.png`, `lobby_solo_timerun.png`, `lobby_create_wordbound.png`.

The new House view restores the earlier colourful-card hierarchy while removing its rays, shortens names and reduces outer gutters. Maps are more prominent than the shortened mode band. Compared with the earlier compact Solo/Create layouts, the new full-width layout satisfies the no-character direction but exposes considerably more empty space in sparse states. The new map choices are an additional capability; the preferred baseline has no equivalent two-map selector to compare directly. These observations compare against232801, not the rejected155755 revision.

## Limits / next action

- Exactly11 of24 new lobby images were opened in this pass; Customize/Finishes/Equipment/Shop/Settings are outside this review. Do not imply all24 were visually reviewed here.
- Stills do not prove focus movement, short-window wrapping, track clicks, live matchmaking, full matches, animation or editor persistence. Keep the passing scenario scope and earlier native evidence separate.
- No gallery was generated yet. Wait for root's exact completed Solo/Workshop run IDs for this build, then run the prepared strict gallery generator. All captures remain unchanged.

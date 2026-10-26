# Windows build 202047 lobby visual review

STATUS: COMPLETE — all 20 actual Windows PNGs independently opened. Readable current UI, with one confirmed compact-layout improvement required before calling the Solo/Create presentation finished. No live source edits or native jobs by reviewer.

## Exact evidence and limits

- [Machine-readable review receipt](windows-202047-lobby-visual-review.json) pins all 20 image hashes/dimensions, build metadata and checks; receipt SHA-256 `5328a213cecccc7458f3e059cf7a16995bed1f13e542928458a97255fee14608`.
- Windows build `20261026T202047Z`, source `c44a39a0e1b81b524c691f2a1f5dda5de31e3d6a`, Unity `6000.6.4f1`, release-QA/non-development. Build gate `workspace-windowsbuild-2026-10-26T20-20-46-855Z` PASS, 202 seconds. Build metadata: [build.json](../../Builds/qa-20261026T202047Z/build.json).
- Actual regular Windows run [20261026T202433Z-lobby_experience](../../Docs/qa/runs/20261026T202433Z-lobby_experience/checks.json): PASS 5/5 steps (`local_choices`, `create_choices`, `customize_and_shop`, `settings_navigation`, `open_wordbound`), console errors/exceptions/warnings 0/0/0. All 20 captures are 1280×720.
- These are real player screenshots, but the scenario drives UI programmatically. They do not prove human keyboard/controller usability, another window size, a successful online authentication or match. `lobby_online_ready.png` shows the guest front door, not a ready multiplayer party. Separate Solo gameplay gates must be reported independently.

## Confirmed issue: oversized Solo/Maps panels

`lobby_solo_timerun.png`, `lobby_solo_freestyle.png`, `lobby_solo_two_seats.png` and `lobby_create_wordbound.png` reuse the large house-grid panel. Their one or two cards end around screen y=354, while the footer begins around y=626: about 272 pixels of empty cream area. The panel covers almost all of the Home backdrop despite having very little content. Text is readable and the footer is present, but this is visibly at odds with the requested minimal, immersive presentation.

The source explains the space rather than suggesting missing controls: `PlayPage.Build` stretches `Panel` over the full page, while the compact families render one 260-unit card row. The panel needs 130 header + 28 bottom + 104 footer reservation + 44 family strip + 8 gap + 260 cards = 574 logical units. A separate bounded proposal will size only those two families to their content and keep the original house/tutorial grids unchanged. This review does not claim that an unrendered correction is visually accepted.

## Other observed views

| Captures opened | Observations |
| --- | --- |
| `lobby_solo_timerun`, `lobby_solo_freestyle` | Original CLOCK/BOX illustrations, selected-state border/check, one objective, 1-player label and GO are clear. Local / Online / Create and House games / Wordbound solo remain distinct. |
| `lobby_solo_two_seats` | Local 2/4, amber one-player restriction and disabled GO communicate the actual guard. No apparent clipping. |
| `lobby_create_wordbound` | Original TABLE image, Build & playtest, Local editor / one-player playtest summary and OPEN distinguish the map creator. |
| `lobby_local_dibs`, `lobby_local_duos`, `lobby_local_escape` | Five original modes and map choices fit the full panel. Dibs and Duos label their actual bots; Escape shows Random house. Selected objectives appear once in the footer. Preserve this full grid. |
| `lobby_create_learn`, `lobby_create_build` | Home/tutorial choices and supported workshop houses are distinguishable; unavailable houses are visibly disabled. |
| `lobby_customize` | Original avatar is large and readable beside clothing/accessory controls; visible hands, face and clothing have no obvious separation or panel overlap in this still. |
| `lobby_shop` | Six outfit portraits have clean alpha on mint backgrounds; no cream rectangular mattes or obvious halo/stretching. Titles and Equip/currency controls remain legible. |
| `lobby_settings_gameplay`, `lobby_settings_graphics`, `lobby_settings_audio`, `lobby_settings_profile` | Selected tabs and rows are clear; no observed text clipping. Fixed panel whitespace is present but is outside the bounded Solo/Maps correction. |
| `lobby_settings_controls`, `lobby_settings_controls_bottom` | Key/controller labels and scroll endpoints are readable. Reset footer remains outside the scrolling content. Cropped rows at the scroll edge are expected viewport behavior. |
| `lobby_settings_help` | Optional help is denser than normal gameplay, with readable cards/key guide and a visible Got it action. |
| `lobby_home_returned` | Vivid Home backdrop, avatar, LOCAL PLAY and ONLINE PLAY are visible. Two temporary join/leave toasts do not overlap navigation. |
| `lobby_online_ready` | Compact guest entry with name, Connect, Workshop and Back is legible against Home; no authenticated/public-match claim. |

No other concrete blocking visual failure was found in these 20 frames. Human style acceptance, native compact-panel recapture, small-window layout and physical input remain separate checks.

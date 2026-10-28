# Held Windows233448 lobby review

STATUS: COMPLETE —24/24 ACTUAL PNGS OPENED; CANDIDATE HELD, NOT DELIVERED

Build233448/source042f3753; actual lobby run `Docs/qa/runs/20261028T233800Z-lobby_experience`. Root reported scenario pass, but this build is held for a separate Workshop RoomRoutes horizontal-clipping repair. This review covers original lobby PNGs only; no source, image or native changes. A replacement build will require its own delivery identity.

No additional blocking lobby clipping, overlap, missing art or character-visibility regression was found in these24 unchanged full images. This does not clear the separately reported Workshop defect or establish owner art acceptance.

## Exact image coverage

All filenames below were opened at original full dimensions using view_image. PNG headers independently show24 images at1280×720.

| Area | Exact opened filenames | Findings |
| --- | --- | --- |
| Shop | lobby_shop.png | Meaningful Sunny-side Club default preview, selected card, Owned/Preview footer and EQUIP are visible. All six looks/actions fit. No empty selection prompt. This Windows scenario captures Looks only; Gear/Winter evidence comes from the separate native14-frame capture, not this PNG. |
| Customize | lobby_customize.png; lobby_customize_finishes.png; lobby_customize_equipment.png | Character controls, finish purchase labels and DONE fit. Original item artwork is visible in Equipment, with the selected BALL detail separate from the scroll viewport. A partial next row at the viewport edge remains a scrolling state, not overlap with the selected detail. |
| Settings | lobby_settings_audio.png; lobby_settings_gameplay.png; lobby_settings_graphics.png; lobby_settings_profile.png; lobby_settings_help.png | Readable rows, selected values and actions remain within panels. Graphics has a visible scrollbar; Help's instructions/Play action fit. No avatar or feet visible in any Settings image. |
| Controls | lobby_settings_controls.png; lobby_settings_controls_bottom.png; lobby_settings_wordbound_controls_mid.png; lobby_settings_wordbound_controls_bottom.png | House/Wordbound labels, keyboard/controller columns and bottom Reset defaults remain separated. Scrolled top rows are clipped by the intended viewport; no horizontal spill or lower action overlap observed. |
| Solo | lobby_solo_timerun.png; lobby_solo_freestyle.png; lobby_solo_two_seats.png | Three actual map previews, names, selection outlines and PLAY fit. Two seats produce a clear Solo-only message and disabled PLAY. No Play avatar. |
| House games | lobby_local_dibs.png; lobby_local_duos.png; lobby_local_escape.png | Five-mode strip, five house cards or single Random house card fit. Bot/team summaries are visible. Dibs/Duos/Escape selected states remain clear. |
| Create | lobby_create_build.png; lobby_create_learn.png; lobby_create_wordbound.png | Home/tutorial versus Wordbound maps is distinct. Build's unavailable houses are visibly disabled; Learn and Wordbound Open actions fit. No Play avatar. |
| Online | lobby_online_ready.png | Unconnected Guest name/Connect/Workshop/Back panel is readable and complete. This is not evidence of connected public matchmaking. |
| Returned Home | lobby_home_returned.png | Original red character returns after temporary Shop previews; navigation, local party state and current activity card fit. |

## Receipt and remaining limits

`checks.json` SHA-256 `45dae829be79b835e87583122aa9578d82a420f45519418eca47b075ac21bb98` records exact build`20261028T233448Z`, run`20261028T233800Z-lobby_experience`, five PASS steps and zero console errors/exceptions/warnings. Its commit/tree fields are null; source042f3753 is the root-provided build identity, not independently derived from this scenario receipt. Root owns the complete build/source audit.

Known design limits remain: sparse Solo environments contrast with the detailed illustrated lobby; Escape/Create retain large empty areas; item-finish swatches are generic cues; selected/equipped states still rely partly on labels. Secondary text/accessibility at smaller windows is outside these1280×720 images. The Equipment list has little explicit scrolling guidance, despite a visible partial next row. These are not newly introduced blockers from this build.

The build remains **held and not delivered** because of the separately observed Workshop RoomRoutes defect. This report must not be reused as the replacement build's screenshot roster. No Windows Gear skin/Winter check, full game completion, physical-input usability, performance acceptance, live online mode acceptance or owner approval is claimed.

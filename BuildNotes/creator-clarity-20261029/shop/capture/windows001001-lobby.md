# Final Windows001001 lobby review

STATUS: COMPLETE — 24/24 actual full PNGs reviewed; technical visual checkpoint, not owner acceptance.

Actual Windows run `Docs/qa/runs/20261029T001342Z-lobby_experience`, build `20261029T001001Z`; parent-provided source `184f1e85`, locally resolved to `184f1e85eababb90010e32f8e9e1b22aa4637f47`. The completed receipt records five PASS steps and zero console errors/exceptions/warnings. This is a fresh 24-image review, comparing the prior `235946` review while preserving all earlier reports. No production edits, native launches or image modifications are part of this review.

Confirmed first 12 originals: Shop, all three Customize views, and eight Settings views (Audio, Gameplay, Graphics, Profile, House controls top/bottom, Wordbound controls middle/bottom). No new fit defect. Shop selected footer and all six Looks cards fit. Equipment's partial next row remains contained in its scroll viewport. Both bottom Controls states show complete last action, notes and Reset; no Settings avatar or feet appear. Help and the remaining Play/Create/Home/Online images are still pending at this checkpoint.

Final checkpoint: the remaining 12 originals are now opened, bringing coverage to 24/24. No additional blocking visual regression found relative to `20261028T235946Z-lobby_experience`.

## Receipt and image provenance

`checks.json` records Windows player, Unity `6000.6.4f1`, run interval `2026-10-29T00:13:45Z`–`2026-10-29T00:13:59Z`. Receipt SHA256: `71f8f5538527b78e51f063acef1fba4388e647e71033934cc7fcf3f49d8f45ec`. Its commit/tree fields are null; source-to-build identity remains the parent delivery audit's responsibility. All 24 PNG headers were independently read as 1280×720. Each full original was opened directly with `view_image`; no edited images or previous-build screenshots substituted for fresh coverage.

## Final findings

- Shop: meaningful Sunny-side Club preview, selected card, category tabs, six Looks cards, Owned/Preview footer and EQUIP all fit. This Windows roster covers Looks only; it does not replace the separate seven-category compact/native review or prove the Gear/Winter states in this build.
- Customize: original item artwork, colour/accessory controls, finish cards and DONE remain cleanly separated. Equipment's partially visible next row is inside the intended scrolling area, with BALL detail below and no overlap. A clearer scroll cue remains worthwhile.
- Settings: all nine captures have no avatar/feet. Row labels and values fit; top, middle and bottom control states show bounded scrolling with a visible scrollbar and stable footer. Graphics' partially exposed lower row stays inside its viewport. House and Wordbound legends are visibly distinct; Help fits its three instructions and Play action. No horizontal spill or missing final action is visible.
- Play/Create: all nine captures have no avatar and retain clear cyan selection marks. Three Solo previews and labels fit; two-seat Solo shows a disabled PLAY and a concise reason. Five House cards fit. Learn, Build a home and the separate Wordbound map entry retain visible bottom actions. No striped UI backgrounds appear.
- Home returns the avatar; Online fits the guest-name/Connect/Workshop/Back menu. The Online image is unconnected and proves no queue, room or multiplayer session.

The new settled captures do not show a scrolling/layout regression. They are static evidence, not a demonstration of physical wheel/controller behavior or the absence of transient frames during every navigation path.

## Unresolved visual and acceptance limits

The vivid, detailed cozy lobby still differs from the sparse stylized Solo map interiors. Their actual thumbnails are truthful but reveal broad empty walls/floors and limited composition. Large unused dark areas remain on House/Create/Escape; the single Wordbound Create card is especially sparse. Small secondary type and subdued disabled labels still warrant physical-use assessment on smaller screens. These design concerns remain open despite successful technical checks.

No full playthrough, all-mode public matchmaking, performance across hardware, physical-input acceptance, owner art acceptance or production-ready claim follows from this review. Career/Leaderboards and unpictured Shop categories are outside its roster. Root's separate strict build/flow/preservation audits remain authoritative for delivery provenance.

## Exact 24 opened originals

| Group | PNGs |
|---|---|
| Shop / Customize (4) | `lobby_shop.png`, `lobby_customize.png`, `lobby_customize_equipment.png`, `lobby_customize_finishes.png` |
| Settings (9) | `lobby_settings_profile.png`, `lobby_settings_audio.png`, `lobby_settings_gameplay.png`, `lobby_settings_graphics.png`, `lobby_settings_controls.png`, `lobby_settings_controls_bottom.png`, `lobby_settings_wordbound_controls_mid.png`, `lobby_settings_wordbound_controls_bottom.png`, `lobby_settings_help.png` |
| Solo (3) | `lobby_solo_timerun.png`, `lobby_solo_freestyle.png`, `lobby_solo_two_seats.png` |
| House (3) | `lobby_local_dibs.png`, `lobby_local_duos.png`, `lobby_local_escape.png` |
| Create (3) | `lobby_create_build.png`, `lobby_create_learn.png`, `lobby_create_wordbound.png` |
| Home / Online (2) | `lobby_home_returned.png`, `lobby_online_ready.png` |

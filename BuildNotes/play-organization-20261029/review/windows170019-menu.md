# Windows build 170019 menu visual review

STATUS: COMPLETE — 13/13 requested full PNGs opened; no owner acceptance or full gameplay claim.

Source run: `docs/qa/runs/20261028T170223Z-lobby_experience`. Scope: every Customize, Settings and Shop image in this run. Images are opened directly at full size, without editing/cropping. Check text fit, selected states, pending-display visibility if present, shop preview, warm panels and no decorative stripes. Native execution and final approval are root/owner responsibilities.
## Opened so far: 4/13 requested images

- `lobby_customize.png`
- `lobby_customize_finishes.png`
- `lobby_customize_equipment.png`
- `lobby_shop.png`

Confirmed: Customize now shows clothing, all colour-part chips, one active palette, three accessory cards and all three finish cards together. Warm ivory and gold selection borders are visible; selected Hoodie, hood, Tomato swatch, Satchel/Letter badge and Classic finish are distinct. Labels/prices fit; Done and Close are fully visible. This restores the earlier compact organization without reverting Roboto.

Equipment shows a three-column illustrated grid, readable letter tiles and a separate selected BALL detail. The next row is partially visible at the scroll viewport boundary; it does not overlap the detail or footer. Shop shows six clearly separated character previews with fully visible names and EQUIP/prices. Its screenshot is an unselected Looks page (`Select to preview`), so it does not demonstrate an active try-on/purchase or other shop tabs.

No decorative striped panel background or confirmed text clipping in these four frames. Small muted helper labels are quieter/thinner than 232801; that is a legibility/taste concern to judge on the actual target display, not a demonstrated clipping defect. No images were edited.
## Completed coverage: 13/13 requested full images

Additional images opened directly:

- `lobby_settings_profile.png`
- `lobby_settings_audio.png`
- `lobby_settings_gameplay.png`
- `lobby_settings_graphics.png`
- `lobby_settings_controls.png`
- `lobby_settings_controls_bottom.png`
- `lobby_settings_wordbound_controls_mid.png`
- `lobby_settings_wordbound_controls_bottom.png`
- `lobby_settings_help.png`

Together with the four names above, this is every filename matching Customize/Settings/Shop in the run. These are original 1280×720 PNGs opened with original detail; no edits, crops or synthesized comparisons were made.

## Confirmed findings

- **No confirmed text-clipping or overlapping-action defect in the 13 frames.** Profile name and Customize action fit. Audio and Gameplay have one clear setting per filled row; selected On/Off and Classic states are indicated by teal underlines. Volume arrows and 100% remain fully visible. Graphics fits Borderless/Full screen/Windowed, all smoothing choices through MSAA 8x, and selected High/None/Off states without truncated words. The lowest following Graphics row is outside the scroll viewport, not painted over another action.
- House/Wordbound control rows retain distinct keyboard and controller columns. Longer hints (`LT / L2 blocks`, `R3 cycles gear`) fit. Partial first/last rows at scrolled viewport edges are normal scrolling clips; reset and saved-state footer stay outside that viewport and visible. No accidental header or footer overlap is visible.
- Help remains explicitly scoped to House games, includes the Wordbound control direction, and shows all three illustrated steps plus View controls and Play. Nothing important is offscreen in that frame.
- Warm panel surfaces and compact accessory/finish cards recover the preferred earlier identity; no decorative stripe backgrounds are visible. Roboto is visibly active and readable at this resolution.

## Taste/legibility considerations, not proven defects

Muted helper text and inactive labels are noticeably lighter/thinner than the earlier 232801 typography. They remain readable in these captures, but small-screen/contrast acceptance needs the actual display and supported scale checks. Short Settings tabs expose the avatar's lower body beneath the panel; that is a composition preference, not control clipping. No new visual redesign is proposed from this review alone.

## Exact limits

None of the 13 images shows a pending display-change countdown or Keep/Revert confirmation. The confirmation layout cannot be visually approved from this run; retain root's separate native coverage. Shop only shows Looks browsing, with no active try-on, selected purchase, other categories or transaction state. These images do not establish controller interaction, mouse hit targets, small viewport/max-font behavior, gameplay completion, multiplayer, performance, or owner acceptance. Build/smoke and preservation receipts remain separate root-owned evidence.



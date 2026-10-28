# Windows 215158 original Solo / Workshop visual review

STATUS: COMPLETE — 21/21 assigned PNGs directly opened, unchanged, at original size.

Scope: build `20261028T215158Z`, root-provided source `4ee1d37a4e64751e093d136dd52a6d99be7aa53f`; `Docs/qa/runs/20261028T215536Z-wordbound_solo` (13 PNGs) and `Docs/qa/runs/20261028T215600Z-wordbound_workshop_flow` (8 PNGs). Both actual checks files identify this build and report PASS. Their commit/tree fields are null, so source identity belongs to root's separate build provenance, not these checks files alone. Root reports border-only selection targeted native `214922` passed 13/13.

Opened coverage: 21/21. All PNG headers report 1280 × 720. Solo checks report 4/4 steps PASS; Workshop checks report 5/5 steps PASS. This review writes only this report, with no production, native or audit/image mutation. Separate Parcel Service scenario images are outside this assignment. Prior 211157/213901 evidence is retained unchanged. The earlier completed visual comparison is `review/windows211157-world.md`; the retained overbright mint-room image is documented in `workshop-depth/selection-review.md`.

## Observed improvements and remaining issues

- **Map choice layout remains intact.** Time Run/Lost Address, Freestyle/Lost Address and Freestyle/Parcel Hall selected/returned cues fit. All three actual previews use their card width; titles, check marks and fixed Play button fit without an avatar or decorative stripes. Compared with the previously opened 211157 images, thumbnail shading is stronger and contact/room shadows are more apparent. Lost Address is darker, but its furniture and doorway remain discernible. The original two rooms still look very similar in identity.
- **The original Workshop no longer floods the selected area with mint.** In `wordbound_workshop.png` and its returned view, the floor remains pale wood; a fine cyan boundary and the X/Z/R/+ handles indicate Book Spring. This is a useful reduction in selection dominance. The thin outline is faint against the bright pale floor at this whole-Attic framing, and most objects are still only tiny marks. This capture therefore does not establish that room/object detail is now good enough for close editing. Keep whole-floor framing for navigation and assess the existing frame-selection control at an ordinary working distance. Root's separate Parcel Sorting close-up is outside this image assignment and cannot substitute for original-course evidence.
- **Workshop control density remains unresolved.** The right panel still combines placement controls, object catalogue, range/floor controls and puzzle fields. The range views expose `Props_Attic_BookSpring`, `BalanceWeight`, `Recipe ID`, `BED` and `Puzzle ID`. Height min/max controls remain tightly packed; the strong yellow scroll thumb attracts more attention than the small selection. Use short display labels and contextual/advanced groups while preserving exact values and existing functions. This is a remaining design concern, not a newly observed failed action.
- **The original gameplay rooms remain sparse.** Lost Address's broad teal walls, repeated wood floor and overhead block-like route structure are largely undecorated; Parcel Hall adds tables/stools/sofa but retains that same shell. The rich sunroom lobby establishes a much more detailed expectation. The newer Parcel Service assets do not finish the art of these separate original sources.
- **Original-world framing remains large.** The avatar is about 380 px high in the 720 px rear-facing world images. Doorway, checkpoint and nearby props remain visible around it, so these stills do not prove a blocking camera defect. Movement, aiming and near-wall camera usability remain a separate play check; the Parcel Service spawn repair is not being judged here.
- **Equipment is consistent but still needs visual polish.** The recipe list, BAT preview, Resume and correct return action fit. Bright yellow scroll thumbs dominate the list, and disabled Craft text is very faint. `Missing ABT` requires more decoding than distinct letter symbols. The partly visible TABLE row is the ordinary scroll-window boundary, not proof of inaccessible content.

No new clipped fixed action, missing preview, incorrect visible selection/return state or decorative stripe was found in these 21 images. These observations do not approve the unresolved aesthetics or the full product.

## Exact opened roster

Solo files are in `Docs/qa/runs/20261028T215536Z-wordbound_solo`.

| # | PNG directly opened | Observed state |
|---|---|---|
| 1 | `solo_initial_lobby.png` | Vivid sunroom, centered avatar, readable navigation and local-play summary. |
| 2 | `solo_timerun_selected.png` | Time Run + Lost Address selected; three shaded actual previews and fixed Play fit. |
| 3 | `solo_timerun_world.png` | Three-line HUD, visible yellow checkpoint/ring and concise control hints; bare room/large avatar remain. |
| 4 | `solo_timerun_menu.png` | Equipment, BAT preview, Resume and Return to lobby visible; Craft disabled. |
| 5 | `solo_timerun_returned.png` | Time Run + Lost Address remain visibly selected after return. |
| 6 | `solo_freestyle_selected.png` | Freestyle + Lost Address selected; short description and footer fit. |
| 7 | `solo_freestyle_world.png` | Freestyle HUD and LMB/Q/Esc prompts visible; same original room. |
| 8 | `solo_freestyle_menu.png` | Equipment layout consistent; Return to lobby fits. |
| 9 | `solo_freestyle_returned.png` | Freestyle + Lost Address selection restored visibly. |
| 10 | `solo_parcelhall_freestyle_selected.png` | Freestyle + Parcel Hall selected independently; Parcel Service remains the third distinct choice. |
| 11 | `solo_parcelhall_freestyle_world.png` | Additional stool/table/sofa props and E Pick up STOOL prompt visible. |
| 12 | `solo_parcelhall_freestyle_menu.png` | Equipment fits above furnished scene; correct Return to lobby action. |
| 13 | `solo_parcelhall_freestyle_returned.png` | Freestyle + Parcel Hall visibly selected on return. |

Workshop files are in `Docs/qa/runs/20261028T215600Z-wordbound_workshop_flow`.

| # | PNG directly opened | Observed state |
|---|---|---|
| 14 | `wordbound_party.png` | Guest name, Connect, Workshop and Back fit; explicit Human players label. This disconnected view is not online-match evidence. |
| 15 | `wordbound_workshop.png` | Attic/Book Spring selected, pale wood retained with fine outline, handles and icon/object controls visible. Whole-floor objects remain tiny. |
| 16 | `wordbound_workshop_range_height.png` | Range/rotation/floor controls, Apply/Cancel and Redraw bounds visible. Top clipped content belongs to the scroll viewport. Fixed footer fits. |
| 17 | `wordbound_workshop_range_floors.png` | Floor and puzzle controls visible; internal IDs/dense text remain. Fixed footer fits. |
| 18 | `wordbound_local_ready.png` | Original Time Run at 25:00, checkpoint and prompts visible. |
| 19 | `wordbound_local_world.png` | Time Run at 24:58 with readable HUD/controls. Still frame alone does not prove movement or route completion. |
| 20 | `wordbound_equipment_menu.png` | Equipment explicitly shows Return to workshop; no clipped return label. |
| 21 | `wordbound_returned_workshop.png` | Attic/Book Spring presentation restored. Byte-identical to `wordbound_workshop.png`, SHA256 `cceb273131cc9ca0672da4e1a881825458b074c1bfe507a99f1dbd5e57bec93a`. History/undo behavior comes from the separate automated assertions. |

## Controls and evidence limits

Visible gameplay prompts remain LMB Smash furniture, Q Craft, Esc Menu or E Pick up STOOL. Time Run's yellow objective ring and text remain readable. Equipment uses one row per recipe and distinguishes Return to lobby from Return to workshop. Workshop keeps Undo/Redo, Preview, Playtest and Host online fixed in the footer while inspectors scroll. The screenshots show these controls; no physical input was performed in this review.

This is visual review of captured mouse/keyboard states at 1280 × 720. It does not verify controller/focus navigation, hover tooltips, actual drag/scroll interaction, movement/camera feel, network matches, full course completion, restart persistence or other aspect ratios. The rear-facing stills do not establish held-item poses, facial emotions or animation quality. No owner approval or production-readiness claim is implied. The assigned 21 images and all earlier evidence remain untouched.

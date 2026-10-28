# Windows 211157 original Solo / Workshop visual review

STATUS: COMPLETE — 21/21 assigned original PNGs directly opened, unchanged, at original size.

Exact scope: build `20261028T211157Z`, source `66687eb7784118cc9628eb344c93741738bd04e7`; original Solo run `20261028T211651Z` (13 PNGs) and original Workshop run `20261028T211737Z` (8 PNGs). All 21 PNG headers report 1280 × 720; their on-disk hashes were read. No image was resized, cropped, annotated or rewritten. Root reports strict 45 + 18 audits PASS and 4,306 authored files preserved. The two actual checks files report Solo 4/4 steps PASS and Workshop 5/5 PASS. This independent visual inspection does not replace those bounded audits or constitute owner approval. New Parcel Service scenario world images are outside this assignment.

Opened coverage: 21/21. No source, native or image changes. This checkpoint is intermediate; later Workshop depth/cutaway work cannot be credited to these images.

## Solo observations

- Map preview images fill the available card width; all three titles and selected outlines/check marks fit. Time Run/Lost Address, Freestyle/Lost Address and Freestyle/Parcel Hall selections remain visible on return. No avatar or stripes appear on the Play page.
- Lost Address remains visually sparse in both world captures: broad flat teal walls, long repeated floorboards and almost no furnishings in the initial view. Parcel Hall adds visible tables, stools and sofa but shares the same large bare surfaces. These captures do not meet the owner's request for detailed environmental identity.
- All three world captures put the avatar at roughly 380 pixels high in a 720-pixel image. The visible route is not fully blocked, but the character occupies a large part of the center. This is separate from the new Parcel Service camera repair, whose world images are outside this assignment.
- The Equipment menus are legible and consistent. Their bright yellow scroll thumb dominates the list visually; the disabled Craft label is very low contrast. The partially shown TABLE row is the scrolling list boundary, not evidence of inaccessible content.

## Prioritized remaining findings

1. **Workshop visual acceptance remains open: depth and object detail are difficult to judge.** In `wordbound_workshop.png`, both range captures and `wordbound_returned_workshop.png`, the Attic reads as one pale, almost flat sheet. Furniture and puzzle objects become tiny marks. The aqua selected zone and X/Z/R handles are visible, but actual object shape, wall relationships and vertical clearance are hard to assess. Preserve whole-floor framing for navigation, then verify bounded lighting/cutaway improvements and the existing frame-selection control at an ordinary editing distance. These images support the owner's plain-map criticism.
2. **Workshop still exposes too much technical structure at once.** The right inspector combines selection tools, placement types, object catalogue, coordinate ranges, floor constraints and puzzle fields in a long scroll. `wordbound_workshop_range_floors.png` exposes `Props_Attic_BookSpring`, `BalanceWeight`, `Recipe ID`, `BED` and `Puzzle ID`. `wordbound_workshop_range_height.png` crowds Height min/max labels against their inputs. Keep exact values/IDs available in advanced editing; give ordinary selections short readable labels and show controls for the current operation. The yellow scroll thumbs compete with the map selection for attention.
3. **Original gameplay rooms remain much plainer than the lobby.** The rich sunroom of `solo_initial_lobby.png` establishes more detail than the original teal rooms deliver. Parcel Hall adds recognizable furniture but retains broad bare walls/floors. Add coherent authored room identity and readable navigation cues for these separate sources; the newer Parcel Service dressing is not a fix for Lost Address or Parcel Hall.
4. **Original-world camera framing deserves its own playability check.** The roughly 380 px tall avatar in a 720 px frame occupies much of the center. Doorway, checkpoint and nearby furniture remain visible around it, so these stills do not establish a blocking camera failure. Compare normal movement, aiming and near-wall behavior before changing the camera. This is separate from Parcel Service's corrected initial spawn.
5. **Equipment is readable but visually unbalanced.** Its yellow thumb is much brighter than the selection cue and disabled Craft text is exceptionally faint. Preserve the unavailable state while making the label readable and reducing scrollbar emphasis. `Missing ABT` is compact but less natural than distinct needed-letter symbols. No new Equipment functionality failure is established here.

No clipped fixed action, wrong selected-map indication, missing preview image, avatar on the Play page or decorative stripe was observed within this scope. The remaining aesthetics are not approved.

## Exact opened roster

Solo files are in `Docs/qa/runs/20261028T211651Z-wordbound_solo`.

| # | PNG opened directly | Observed state |
|---|---|---|
| 1 | `solo_initial_lobby.png` | Central avatar, vivid sunroom, legible navigation and local-play summary. Detail mismatch with original gameplay rooms. |
| 2 | `solo_timerun_selected.png` | Time Run + Lost Address selected. Three actual previews use card width; titles, check marks and fixed Play fit. |
| 3 | `solo_timerun_world.png` | Three-line HUD, visible checkpoint ring/text, sparse original room and large central avatar. |
| 4 | `solo_timerun_menu.png` | Equipment list, BAT preview, Resume and Return to lobby visible; Craft disabled. |
| 5 | `solo_timerun_returned.png` | Time Run + Lost Address selection visible on return; fixed action fits. |
| 6 | `solo_freestyle_selected.png` | Freestyle + Lost Address selected; short description and footer fit. |
| 7 | `solo_freestyle_world.png` | Two-line HUD and LMB/Q/Esc prompts visible; bare original room. |
| 8 | `solo_freestyle_menu.png` | Equipment consistent with Time Run; Return to lobby visible. |
| 9 | `solo_freestyle_returned.png` | Freestyle + Lost Address selection visible on return. |
| 10 | `solo_parcelhall_freestyle_selected.png` | Freestyle + Parcel Hall selected independently; Parcel Service remains a distinct third map. |
| 11 | `solo_parcelhall_freestyle_world.png` | Added tables/stools/sofa visible; E Pick up STOOL prompt fits. Same original shell/camera framing. |
| 12 | `solo_parcelhall_freestyle_menu.png` | Equipment fits over furnished scene; Return to lobby visible. |
| 13 | `solo_parcelhall_freestyle_returned.png` | Freestyle + Parcel Hall selection visible on return. |

Workshop files are in `Docs/qa/runs/20261028T211737Z-wordbound_workshop_flow`.

| # | PNG opened directly | Observed state |
|---|---|---|
| 14 | `wordbound_party.png` | Guest-name input, Connect, Workshop and Back fit. Human-player wording explicit; disconnected screen is not matchmaking evidence. |
| 15 | `wordbound_workshop.png` | Attic and Book Spring selected; icons/object tiles visible; nearly flat whole-floor preview. Fixed Undo/Redo/Preview/Playtest/Host online footer. |
| 16 | `wordbound_workshop_range_height.png` | Ranges/rotation/floors with Apply/Cancel and Redraw bounds visible. Partly clipped top content is at the scroll-window edge. Footer remains separate. |
| 17 | `wordbound_workshop_range_floors.png` | Floor constraints and puzzle fields; technical IDs and dense labels. Footer remains fixed. |
| 18 | `wordbound_local_ready.png` | Time Run at 25:00; checkpoint and prompts visible in original room. |
| 19 | `wordbound_local_world.png` | Time Run at 24:58, same room/controls; still image does not prove movement or full progression. |
| 20 | `wordbound_equipment_menu.png` | Equipment explicitly offers Return to workshop; label fits. |
| 21 | `wordbound_returned_workshop.png` | Attic/Book Spring selection and editor presentation restored. Byte-identical to `wordbound_workshop.png`: SHA256 `c7bf25c1d46bad57c9b0b06d13dd8bd240a2f7f2338d921d43652a406349e1a2`. Undo/history behavior belongs to the separate automated assertions. |

## Controls and limits

Visible prompts are concise: LMB Smash furniture, Q Craft, Esc Menu, or E Pick up STOOL when nearby. The Time Run objective has a distinct yellow ring/text. Equipment uses one row per recipe with the correct return destination. Map cards keep independent mode/map cues, use their width and leave a short gap before the footer rather than the previous large unused region.

Only these captured mouse/keyboard states at 1280 × 720 were visually reviewed. No controller navigation, physical input, dynamic tooltip, focus traversal, scroll/drag interaction, camera movement, network match, full route, restart persistence or other aspect ratio was exercised here. Rear-facing stills cannot establish facial expression, held-item quality or animation quality. No owner approval or production-ready claim is implied. No production sources, assets, native processes or audit evidence were modified.

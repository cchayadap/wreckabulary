# Windows lobby visual review — 162634

STATUS: COMPLETE — agent-reviewed improvement; one minor card-label defect; owner acceptance pending

Independently inspected all16 actual1280×720 PNGs in `Docs/qa/runs/20261026T162634Z-lobby_experience`. The inspected checks.json reports PASS5/5, zero assertion failures, zero console errors/exceptions/warnings, build20261026T162356Z, Unity6000.6.4f1, non-development player. Parent identifies sourcef328c814. Every image filename, dimension and SHA256 is recorded in `windows-162634-visual-evidence.json`. No live edits or native jobs were performed here.

| Images inspected | Visual finding |
| --- | --- |
| local_dibs, local_duos, local_escape | Local/Online/Create choices are distinct. Mode and house rows retain vivid original item/floor-plan art. Gold border plus check mark identifies selection; objective and participant summary appear once beside Go. Dibs explicitly says you+3bots; Duos says you and a bot vs2bots; Escape says1player. Escape's sole house card has the defect below. |
| create_learn, create_build | Learn/Build cards are easy to distinguish; tutorial room and house workshop use the original book/hammer. Solo describes the current one-person selection; the images do not establish tutorial's maximum capacity. Workshop's unavailable houses are dimmed, while supported choices and selection remain clear. |
| home_returned | Vivid sunroom, cream-faced red-hoodie original avatar and readable LOCAL PLAY/ONLINE PLAY survive the navigation round trip. Selected mode/map remain visible. The two small prior-selection notices are still visible in this particular frame; no overlap occurs. |
| customize, shop | Large original avatar remains unobscured. Clothing/swatches/accessories/gear and Done are readable. Shop's six corrected portraits have distinct colors, genuine clean backgrounds and no earlier cream rectangles. Titles, price/Equip and category tabs fit. These screens establish no new purchase/equip interaction. |
| settings_profile, settings_audio, settings_gameplay | Six-tab hierarchy and selected underline are clear. Short panels hide unused scrollbar tracks; rows and actions fit without overlap. |
| settings_graphics | All nine rows fit, including Defaults. Choice labels remain separated after the24px scrollbar reservation; the unnecessary scrollbar is hidden. Fine helper text is smaller than primary labels, so this is not a measured accessibility acceptance. |
| settings_controls, settings_controls_bottom | The teal track now clearly indicates more content. Top and bottom captures show the handle at the corresponding ends. Bottom exposes Pick up, Spell, Drop, both hands, Bag/map, Pause and final input note; the fixed saved/reset footer stays separate. This resolves the observed scroll-discovery defect for this Windows layout. |
| settings_help | Three illustrated steps, key table and primary action all fit. This is the densest text page, confined to optional Help. |
| online_ready | Actual existing Wordbound guest frontend is visible with Guest name, Connect, Workshop and Back. It is plainly a different, simpler visual shell; no claim that Online received the lobby's art redesign. |

One minor defect remains: `lobby_local_escape.png` shows `rand...` in the otherwise empty gold random-house card. Footer RANDOM makes the route understandable, so this does not block navigation testing, but it is unfinished visual polish. Source inspection points to `PlayPage.RandomHouseCard`: its centered text uses Size(-1,44) without filling the poster body; bound the label to the card width and display the complete Random label. No new texture is necessary. Parent was notified before this report was finalized.

No additional clipping/overlap blocker was found in the16 images. Original artwork is preserved visibly; no claim of expanded art content or owner approval follows from this review. The actual receipts confirm focused rendered frames and preference preservation for each route. The Online step explicitly made no Connect/queue/room/match request, so this run proves the route and visible frontend, not authentication or public multiplayer operation. Settings traversal used actual UI Submit callbacks and programmatic scrollbar linkage; physical input navigation, mouse dragging, display switching, alternate aspect ratios and full-match play remain separate evidence.

- [x] All16 images visually inspected.
- [x] Checks.json5/5PASS and bounded rendering/preservation scope verified.
- [x] Complete file hash/dimension manifest saved.
- [x] Random-card truncation reported promptly.

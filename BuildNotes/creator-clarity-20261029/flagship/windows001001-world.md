# Windows 001001 — final original Solo and Parcel Service image review

STATUS: COMPLETE — all22 final full images inspected. No new blocking visual regression found in this bounded final-build comparison; full map-art, route and online acceptance remain open.

Parent-reported source `184f1e85`; both completed receipts independently identify build `20261029T001001Z`. Opened all13 original Solo images from `Docs/qa/runs/20261029T001401Z-wordbound_solo` and all9 Parcel Service images from `Docs/qa/runs/20261029T001424Z-wordbound_parcel_service` with view_image at original1280×720. No crops, contact sheets, pixel edits, production changes or native jobs. Exact untouched PNG hashes are below and in the companion evidence JSON. The parent owns executable/source binding through its strict delivery audit.

## Final image findings

Compared directly with the previously reviewed235645 presentation, the final images retain the same visible composition, UI organization and known limitations. Minor timer/letter-tile differences reflect the different captured moments; no new art improvement is claimed from the passive measurement/QA-settling change.

| Actual frames reviewed | Confirmed final observation |
| --- | --- |
| Both initial lobby frames | Whole avatar, top navigation and Local Play panel visible without overlap. The detailed furnished lobby image still creates a higher visual-detail expectation than the playable maps. |
| All five selected frames and all five returned frames | Correct mode/map cyan selection and checks; all three truthful thumbnails, map names, descriptions and Play button fit. Returning preserves the shown selected mode/map. No thumbnail stretching or panel overflow seen. |
| Lost Address Time Run world | Whole avatar and floor contact visible; onward doorway and yellow checkpoint/keepsake marker readable. Original timber floor retained. Large plain teal walls, open floor and overhead ramp still dominate; the sparse arrival is not artistically finished. |
| Lost Address Freestyle world | Same unobstructed camera and empty-room composition. No nearby practice guide at arrival; tiny keepsake and generic Smash furniture hint offer weak first-action guidance. This known UX limitation remains. |
| Parcel Hall Freestyle world | Nearby stool interaction prompt, several pale tables/stools and green sofa visible. More real furniture than Lost Address, but repeated perimeter furniture in a very large hall still lacks destination identity. |
| Parcel Service Time Run world | Whole avatar, red/oak desks, drawers/handles, shelf, wall sign/sconces, wood floor and clear contact/directional shadows retained. Yellow checkpoint/keepsake marker and Take keepsake prompt readable; no extreme head closeup. |
| Parcel Service Freestyle world | Cyan practice marker/readable label, retained real furniture detail and shadows. Starting camera faces the working wall, leaving onward doorway outside the frame. Large plain blue wall areas and initial route orientation remain art/UX work. |
| All five equipment-menu frames | Contained two-column panel, search/recipe rows, BAT detail, disabled Craft/missing letters, Resume and Return to lobby all visible. TABLE is partly clipped at the scroll viewport boundary, with scrollbar present; no whole-panel overflow. Empty-slot/disabled text is subdued and the gold scrollbar is visually prominent. |

Parcel Service letter tiles appear partly airborne/stacked at the right edge of near-start world/menu captures and change between frames. These are actual gameplay letter objects; the still images do not establish their settled behavior or a persistent floating-fixture problem. No new missing/pink materials or gross furniture embedding was observed in the sampled views.

There is no repeated decorative stripe background in these frames. Timber seams/grain, light bands and functional cyan selection underlines remain. The menus show no owned letters or selected physical puzzle, so BAT is still the default here; these screenshots do not test the contextual HAMMER-before-TABLE suggestion at the marked source. Separate gameplay/read-model evidence is required for that interaction.

## Completed receipt evidence and limits

- Original Solo completed00:14:23Z:4/4 steps PASS,13 PNGs,0 console errors/exceptions/warnings. Checks SHA256 `17c4da03d7f97939142678d294b732b6deac8fd38113e81f70f34a5e4f597e43`.
- Parcel Service completed00:14:37Z:3/3 steps PASS,9 PNGs,0 console errors/exceptions/warnings. Checks SHA256 `2696f06aad8e5d8dfbf166d02ba1868d781f0fcccc49ebd4963bd22dd683a2c8`.
- Actual scope is local launch/render/menu/return through production handlers. It does not prove physical controller/keyboard input, all camera angles, held-item/facial animation, complete races, bots, teams, public matchmaking, reconnect or multiplayer stability.
- No Workshop images are in this22-image set. The final Room/routes width and cutaway results are separate reviews. No inherited pixel-review claim is made for them here.
- The original native first-leg231532 proof is still only three rooms/two puzzles. The full34-room/four-layer/three-lap route remains open. Its remaining traversal and richer destination art are not resolved by these smoke passes.
- `NEXT-ART-IMPLEMENTATION.md` remains a proposed full-map original-kitchen pilot, not implemented in this build. Continue actual coherent environment composition and concise opening guidance; adding repeated furniture or changing only thumbnails would not settle the sparse-map complaint.

## Exact final reviewed image roster

All files were opened directly, unmodified. SHA256 hashes identify the exact reviewed bytes.

### 20261029T001401Z-wordbound_solo

| File | SHA256 |
| --- | --- |
| solo_initial_lobby.png | `2c719302b961758bada52d60a987aa7a7846779bf50b727812f504bd584ed2d2` |
| solo_timerun_selected.png | `537e86d8554a9c1205c8aadcf14bb2778ab86d8ae97888a3e6ee7be142713a79` |
| solo_timerun_world.png | `37d5e527ec440488a9cdc443b0e4e45f1f8115b9ea52b06f856884068cc2bb97` |
| solo_timerun_menu.png | `b962c2974e2da151f8a52f6a5f2cade62cbd84e456359b93cc1a1fd2cafc0a11` |
| solo_timerun_returned.png | `840c58f57c84eafb0abb4aed809faae0b1274b21856a871683fc69d63591d2e1` |
| solo_freestyle_selected.png | `123db0a707e6c53f6042fa5ba2e1913ab0676172bdf269ab91567011c4d3b37c` |
| solo_freestyle_world.png | `1856eeabc35c94a9f3c3aad409e82afc1c4238e7f88d8266736aec1520c83195` |
| solo_freestyle_menu.png | `ec2c411360378db12cf0eaa167d120a58398f7a29fc17eb93a30eab0667bff4b` |
| solo_freestyle_returned.png | `65f374df46c07efa94ccb48b19a2602cd91d436b191b71f6e9ce61e8a69e389c` |
| solo_parcelhall_freestyle_selected.png | `fc81365327ef960c1b295604ac1d93f9fb576b80b95a5d41826bddb87b4b03a9` |
| solo_parcelhall_freestyle_world.png | `a25c7f59a2a4725cd59fa48f181d7bb441ddf89ccc06d04b08895f373bb1ca7b` |
| solo_parcelhall_freestyle_menu.png | `753787daa6c0ddc3bc482c2cff3985f39a6f27f73949a32380556a99cae648e8` |
| solo_parcelhall_freestyle_returned.png | `3aef2719bbde8541957427e91bc6175ddd28943c0025d788ea1467f4d334fc58` |

### 20261029T001424Z-wordbound_parcel_service

| File | SHA256 |
| --- | --- |
| parcel_service_initial_lobby.png | `a35d80837b8956cf29ca1d2766180f2134cb64aed0c76008970c7876281315be` |
| solo_parcelservice_timerun_selected.png | `928eae8d78298997ce372e2b80045b084bce683449b83083213c9a1d419ef772` |
| solo_parcelservice_timerun_world.png | `19fce2d8e3cd4c6c243b14cbc3f142213efdab28c828bf9913792b63dc39b0b3` |
| solo_parcelservice_timerun_menu.png | `1723b5e23a3a81628f1315c08f247a382ee649b302e104b6cf8e1bd794f120a6` |
| solo_parcelservice_timerun_returned.png | `619b8a1825f9d60734dc2b3e835b55476007888e4bd8a076b33089c21772ba52` |
| solo_parcelservice_freestyle_selected.png | `7e5ee941c45cc152c36deb887dfc90f394a20789973b2672550f96ae8901be97` |
| solo_parcelservice_freestyle_world.png | `1bb2a09c7c6931222e8ed0de270bcacc94d6d130331365f28b2c6ec3339a4a4e` |
| solo_parcelservice_freestyle_menu.png | `221e2e4917d552d54ce2337bc7f01332da77836a0be45ea582ee43e033a3c6b4` |
| solo_parcelservice_freestyle_returned.png | `f7a86ea868cf268a04ced21b5d993ed9e1dc6ce5abb3a356f4ae1ac172195920` |

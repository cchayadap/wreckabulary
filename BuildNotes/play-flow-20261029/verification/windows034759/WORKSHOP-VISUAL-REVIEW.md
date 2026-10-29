# Windows Workshop visual review

STATUS: COMPLETE READ-ONLY IMAGE REVIEW; OWNER VISUAL ACCEPTANCE PENDING

Build: `20261029T034759Z`, source `07d814ec`. Read-only original screenshot review. Root runs the smoke scenarios serially and will supply verified run paths. Scope: Workshop flow 8 PNGs, Parcel workshop 9 PNGs, Parcel inspector 11 PNGs (28 total). No production files, Unity/player processes or Git changes by this lane.

- [x] Opened all 8 original PNGs in verified Workshop flow `20261029T035131Z`.
- [x] Opened all 9 original PNGs in verified Parcel workshop flow `20261029T035207Z`.
- [x] Opened all 11 original PNGs in verified Parcel inspector flow `20261029T035224Z`.
- [x] Inspected all 28 original 1280×720 PNGs individually using view_image with original detail. Exact hashes are recorded below and in WORKSHOP-PNG-ROSTER.json.

Visual review does not grant owner style approval or prove full creator acceptance. Existing map density/detail concerns remain open unless actual captures resolve them.

Confirmed from original images: the neutral dark panels, readable primary actions, cyan active tab/room outline and gold scrollbars render without blocking overlap. The original prop icons remain recognizable. The assigned Attic default zoom shows mostly pale floor and tiny props; Parcel close zoom reveals desks, shelves and packages but its floor and spacing remain repetitive. These images do not resolve the owner's detail/density concern. The yellow checkpoint text overlaps desk/letter clutter in the Parcel playtest. A scroll viewport naturally shows a partial next object row; this is not evidence of unreachable content. No physical pointer or handle interaction, animation quality or online Host success is claimed.

No blocking panel collision or absent primary action was observed in this 28-image sample. This is a bounded visual inspection, not full creator usability or production readiness approval. All three run checks.json files report PASS and build_id 20261029T034759Z. The build manifest records nondevelopment Windows QA, Unity 6000.6.4f1 and source 07d814ec1993f7b444bc150091f699206ea7901c. Root owns strict smoke/preservation audits and their final verdict.

## Findings still open

1. Default assigned Attic framing is dominated by pale repeated flooring and very small props. Even though the room/floor controls are visible, this view is poor for judging assets and discovering spatial editing. Parcel's close view makes object silhouettes clearer, but its broad floor areas, few repeating package/desk/shelf silhouettes and low wall cutaways still leave the owner's map-detail/density request unresolved. The warm, detailed lobby artwork has markedly more visual richness than these playable/editor map views.
2. The editor is clearer than a text-only catalogue because object and recipe cards show original rendered prop icons; Clock also has an explicit check and cyan selected frame. It still presents many field labels, text buttons and outlined controls. This evidence supports visible controls; it does not establish that the owner's minimal-text, image-led creator goal is complete.
3. The R/X/Z/+ spatial controls are visible as a compact dark cluster in the map. These stills do not prove drag targets, snapping, axis direction, hit areas or pointer/controller operation. In the wide assigned view, the control cluster is considerably more conspicuous than its tiny target geometry.
4. Parcel's yellow CHECKPOINT / Bring keepsake text sits across desks and nearby letter/prop clutter. The words remain readable here, but spatial occlusion and scene integration still need review from moving camera angles. The character is seen mostly from behind and unarmed: holding corrections, facial emotion, animation and impact audio are not covered.
5. The equipment menu shows recognizable original prop silhouettes, selected BAT, a clear missing-letter explanation and a disabled Craft action. Pale BAT preview, list density and generic large empty panel areas remain visual polish topics; this does not verify new skins or detailed equipment. The partial next list/card row at scroll boundaries is normal clipping inside scroll viewports, not evidence of inaccessible content.
6. Workshop header still displays Time Run while the public mode selector uses Time Trial. It is readable, but the visible name is inconsistent. Host online and Share buttons being visible does not demonstrate publishing, public matchmaking or multiplayer editor support.

## Individual original-image observations

Paths below are relative to Docs/qa/runs. Each row was opened separately; repeated states from different runs were not treated as the same image.

| Run / PNG | Observation |
| --- | --- |
| 035131 / wordbound_party | Full neutral matchmaking panel; five modes and Find players focus visible. Small actual Lost Address preview is sparse; warm lobby artwork vivid. No Connect attempt proven. |
| 035131 / wordbound_workshop | Objects tab active; illustrated picker visible; fixed top/bottom actions readable. Attic floor dominates and props are tiny; compact spatial handles visible. |
| 035131 / wordbound_workshop_range_height | X/Z min/max, height16/16, yaw buttons and rotation/floors section readable; right inspector scroll contains lower controls. No panel collision. |
| 035131 / wordbound_workshop_range_floors | Allowed floors, current Attic room scope, Apply/Cancel and Redraw bounds visible. Advanced IDs/checks collapsed. View-only screenshot is not an Apply/drag test. |
| 035131 / wordbound_local_ready | Red original character/backpack and warm wood visible; compact HUD and bottom hints readable. Empty teal room and low scene detail remain. |
| 035131 / wordbound_local_world | Same playable world after timer advances; no layout collision. A still does not establish movement or animation quality. |
| 035131 / wordbound_equipment_menu | Selected BAT recipe and missing ABT readable; Craft disabled; icon list and Return to workshop visible. Partial next row stays within scroll viewport. |
| 035131 / wordbound_returned_workshop | Returned editor matches prior Attic framing and Objects tab; top/bottom controls intact. Visual similarity alone is not save/history proof. |
| 035207 / parcel_workshop_assigned | Assigned Lost Address title and Attic selection visible; no-room-selected guidance clear. Pale sparse overview remains. |
| 035207 / parcel_workshop_saved | Parcel three-room overview with cyan Sorting outline, original package/shelf/desk props and icon picker. Controls unobscured. |
| 035207 / parcel_workshop_sorting_close | Close Sorting view makes object silhouettes clear and room outline visible. Map viewport intentionally crops adjacent rooms; handles visible; repeated flooring/sparse spacing remain. |
| 035207 / parcel_workshop_playtest_world | Exact Parcel world appears with blue walls, red desks, shelves, letter props and original red character; hints readable. Checkpoint text overlays furniture. |
| 035207 / parcel_workshop_playtest_menu | Equipment menu unobscured in Parcel world; disabled Craft and Return to workshop visible. No equipped-item pose shown. |
| 035207 / parcel_workshop_returned | Close Sorting framing and selected room restored visually; illustrated picker and primary actions intact. |
| 035207 / parcel_workshop_assigned_returned | Assigned Attic view returned; title and retention message fit. Large sparse pale floor remains. |
| 035207 / parcel_workshop_parcel_returned | Wide Parcel view returned; visible retention message, cyan Sorting boundary and all major panel actions fit. |
| 035207 / parcel_workshop_lobby | Lobby return shows original red character and vivid sunroom; top navigation and Local/Online card fully visible. No held-item/facial-action sequence. |
| 035224 / parcel_workshop_assigned | Independently opened inspector run's assigned overview; readable title/floors/room guidance with same sparse Attic concern. |
| 035224 / parcel_workshop_saved | Inspector run's Parcel overview and original prop catalogue visible; cyan room boundary and Objects tab distinguish current context. |
| 035224 / parcel_workshop_sorting_close | Close room framing preserves sidebars/actions and shows original prop silhouettes and spatial handles. |
| 035224 / parcel_workshop_new_puzzle | Puzzles tab active; new-puzzle/type controls, Find recipe and illustrated recipe cards visible. Clock has cyan frame/check; no overlapping labels. Lower actions require scrolling. |
| 035224 / parcel_workshop_room_routes | Room tab, icon toolbar, Move to floor and Routes/checkpoints controls visible; Safe/Stops/Routes/Laps/Spawns and Sorting safe return readable. More controls continue below viewport. |
| 035224 / parcel_workshop_playtest_world | Independently opened Parcel playtest; same readable hints/HUD and original red character; checkpoint/furniture text layering remains. |
| 035224 / parcel_workshop_playtest_menu | Independently opened recipe menu; selected BAT, missing ABT, disabled Craft and return action visible. |
| 035224 / parcel_workshop_returned | Close Sorting context restored visually after playtest; Objects tab and original icon catalogue intact. |
| 035224 / parcel_workshop_assigned_returned | Assigned title, Attic selection and retention message fit. Same wide pale overview concern. |
| 035224 / parcel_workshop_parcel_returned | Wide Parcel context and retention message fit; cyan room outline and primary actions visible. |
| 035224 / parcel_workshop_lobby | Independently opened final lobby return; vivid background, neutral navigation and original character fully rendered without blocking overlap. |

Next: owner inspects the candidate build; root retains actual online acceptance, creator physical input, dense map/art polish, holding/emotion/motion and audio as separate pending work. No source changes are recommended solely to make these smoke screenshots appear passed.

## Exact original PNG SHA256 roster

| Run / original PNG | SHA256 |
| --- | --- |
| 20261029T035131Z-wordbound_workshop_flow / wordbound_equipment_menu.png | 8f18d0a926af1b515fe0cb6f4f7689111bf5cd4843ad5b3cdb654972e25a2c85 |
| 20261029T035131Z-wordbound_workshop_flow / wordbound_local_ready.png | c9cc32451a8fdbc5f519e61ea431e8b57f6068e388229c6ec77a231ed3547376 |
| 20261029T035131Z-wordbound_workshop_flow / wordbound_local_world.png | b157dc4f0a43461ccda1dd7bc2a3ded57ec78c26b9e99c68a1354cd5347eef0d |
| 20261029T035131Z-wordbound_workshop_flow / wordbound_party.png | 0b11cc8e765dc7ff191dcd9cba4525469765bb87864f42b4adc965e30e07c5f0 |
| 20261029T035131Z-wordbound_workshop_flow / wordbound_returned_workshop.png | 7a6348b3ec2036bc35ac0c9548e775f225ff435d7f4124dafcadcc238c8abe0c |
| 20261029T035131Z-wordbound_workshop_flow / wordbound_workshop_range_floors.png | 698e8c1b54e3e511821f863c5523f68c9abad5069d63e8eab71711e129c97dc3 |
| 20261029T035131Z-wordbound_workshop_flow / wordbound_workshop_range_height.png | b1de081aa29e53fdd19ccc3ff72c9016e826ab419d39341887f0454586fd94f6 |
| 20261029T035131Z-wordbound_workshop_flow / wordbound_workshop.png | 7a6348b3ec2036bc35ac0c9548e775f225ff435d7f4124dafcadcc238c8abe0c |
| 20261029T035207Z-wordbound_parcel_workshop / parcel_workshop_assigned_returned.png | 48446f13a368834f4eadf9282cea5ca13aaee1567f61db089eef2b9a5b36e7e3 |
| 20261029T035207Z-wordbound_parcel_workshop / parcel_workshop_assigned.png | dc2da5743839cd597935b8eb3670393b57f0bb00121df43b2c1603d8c0babb12 |
| 20261029T035207Z-wordbound_parcel_workshop / parcel_workshop_lobby.png | 868056cccc31e1e76ea78d8aa34b11840071fdbb1ab7fc4b6199861b69ad3e57 |
| 20261029T035207Z-wordbound_parcel_workshop / parcel_workshop_parcel_returned.png | d28c062584498f66a70bfe8eb5470d9db5f79bc794c4a621c02ff0bf15626c29 |
| 20261029T035207Z-wordbound_parcel_workshop / parcel_workshop_playtest_menu.png | cff475c8009632c0f2b556eb3caa40f0f1653ea03806ef738626fe43d65daa3d |
| 20261029T035207Z-wordbound_parcel_workshop / parcel_workshop_playtest_world.png | d59b92f44c53ca88e6cd923a91546cb74fec26043dcaf7dd86e38f8362064f0e |
| 20261029T035207Z-wordbound_parcel_workshop / parcel_workshop_returned.png | a4c580a4c384e6377cb5f84ec77cb55aa89231198be1a35397212a67ebf6dca4 |
| 20261029T035207Z-wordbound_parcel_workshop / parcel_workshop_saved.png | aec440b3f6b3d0c596b6db1db7e9db4acba3cb6c04de508af4ce7b044eada094 |
| 20261029T035207Z-wordbound_parcel_workshop / parcel_workshop_sorting_close.png | da15ef1a5e8e72ffe2c83eb1f494a99bd7eb2160bb761b22be444e5c27d983f7 |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_assigned_returned.png | 48446f13a368834f4eadf9282cea5ca13aaee1567f61db089eef2b9a5b36e7e3 |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_assigned.png | dc2da5743839cd597935b8eb3670393b57f0bb00121df43b2c1603d8c0babb12 |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_lobby.png | fccb237c1358e241989a5f9b2a035b1ff25710c3515605f88c9071b458ae16e7 |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_new_puzzle.png | d810905af2d7a4dfda86c1e2d62e2551a48205f7ec69ae1e0e5b2ec784cef382 |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_parcel_returned.png | 70ff6f106aaf8470c54ce9617166bbeabd4f7d1c0bab0a2c4bbf98b6f9b86401 |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_playtest_menu.png | 7fd422a10fd29f87d6b9541820225a81fed2f9ec0a68ec88ef508fa210025b8d |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_playtest_world.png | f97df0849b176865c01c769ff817ffbc8711227b199bebad66c0a41fd0e70cc3 |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_returned.png | b33e07eadfa766647a51fa3c0cc4de5307b989f830c8689e004af1630b7c9104 |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_room_routes.png | b89463c65a3cdc76af7fdd48d7905b242db04b459126c45770d275a7fb8f5957 |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_saved.png | 08f50f31093f0809d9643ae458067037b9b3d918e2b6072587cbab15f969ae9a |
| 20261029T035224Z-wordbound_parcel_workshop_inspector / parcel_workshop_sorting_close.png | 8285fc748539a550c8db64f5b103f0ab4c20307e7f7c78903a21d225df8e3066 |

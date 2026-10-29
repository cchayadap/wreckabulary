# Windows001001 Workshop review

STATUS: COMPLETE for all 28 actual-image reviews. No production/native actions. Earlier failed strict-audit outputs and prior visual reviews remain unchanged.

Build `20261029T001001Z`, source `184f1e85eababb90010e32f8e9e1b22aa4637f47`, bound from `Builds/qa-20261029T001001Z/build.json` (`ok: true`, release-qa). Directly opened all 28 distinct unchanged full PNGs at original detail: original Workshop001200 (8), Parcel Workshop001439 (9), inspector001453 (11). Every file is 1280 × 720; similar-looking images in different runs were each opened. No owner acceptance or production readiness claim.

Checkpoint: directly opened all 8 original Workshop001200 plus all 11 inspector001453 full PNGs (19/28). Original range targets are visible; no horizontal overflow. Room Safe controls and all five navigation choices fit; XYZ continue below the vertical fold. New puzzle CLOCK is visibly selected, original recipe images clear; second row/Add below fold. Return restores Objects and the matching original/Parcel context. Sparse rooms, distant overview props and low-contrast disabled Craft remain visual limitations. Parcel Workshop001439 nine-image review follows.

## Final findings

1. **Range capture now matches its stated proof.** Original range-height shows all X/Z/height pairs and rotation; range-floors shows all four floor choices, Apply/Cancel, Redraw bounds, the placement row, New placement and Advanced IDs without horizontal clipping. The fixed Objects/Puzzles/Room tabs and footer remain visible. The target labels are visible in the full PNGs, not inferred solely from equality of numeric probes.
2. **Room width correction remains visible in the final build.** Inspector `parcel_workshop_room_routes.png` fits all five navigation choices, the room icon row, Move to floor, Module, Recovery anchor and Add recovery. There is no horizontal scrollbar. X begins at the vertical viewport's bottom; remaining coordinate rows are below the fold. This is normal vertical scrolling, not the old horizontal overflow. Only Safe is open in this Windows shot; other route panels and large-text/narrow-width combinations rely on separate native layout evidence.
3. **Puzzles use original image choices and a readable selected cue.** The CLOCK tile is selected with border/check; HAMMER is fully visible, and the STOOL/TABLE row starts below it. Lower tile labels and Add are below the vertical fold, so the top screenshot does not visually verify those controls. No noncraftable furniture is offered among the visible recipe choices. Internal source IDs are absent from the default visible puzzle fields.
4. **Edit and return context remains coherent.** Original Workshop returns to Book Spring/Attic and Objects. Both Parcel scenarios return to selected Sorting and Objects after playtest; the temporary Puzzles/Room capture context does not leak into the return view. Switching to Assigned shows The Lost Address, Attic and no selected room; switching back shows the Parcel title, Packing floor and selected Sorting. The footer explicitly reports retained edits/Undo. Exact history and autosave preservation is evidence from probes, not an inference from these labels.
5. **The close Parcel preview retains original furniture and wood.** Sorting shows brown planks, cream parcels, red-legged desks and shelves, with only the mint perimeter indicating selection. No full-floor mint wash or floating cutaway fixture is present. The overview remains sparse/repetitive; the original Attic overview has tiny props and faint room boundaries. More environmental identity is still needed to satisfy the owner's quality request.
6. **Gameplay and lobby still differ in visual finish.** The Parcel initial view has dark blue walls, two sconces, a red sign and original furniture. The original map is much barer: teal walls, wood floor, beam, doorway and distant keepsake. Initial avatars occupy about half the frame height; the earlier extreme close-camera failure is not visible in these shots, but no whole-route camera claim follows. The lush Sunroom lobby artwork remains substantially richer than the playable rooms. The generic Smash furniture prompt in the original initial view has no nearby visible furniture, leaving an onboarding mismatch.
7. **Equipment remains usable-looking but needs contrast polish.** BAT preview/details, search, Resume and Return to workshop fit. Disabled Craft is difficult to read, and small list icons plus the partially visible bottom item make the list less clear than the Workshop image tiles. The partially visible TABLE row is at an ordinary vertical scroll boundary. Static captures do not prove physical controller scrolling, focus movement or crafting eligibility.

No new blocking default-scale layout defect was observed in these 28 final PNGs. The findings above are not owner aesthetic acceptance.

## Read receipt facts

All three checks files name build `20261029T001001Z` and report PASS: original Workshop 5 steps/8 PNGs, Parcel Workshop 4 steps/9 PNGs, inspector 5 steps/11 PNGs.

Original range probes retain the first attempt and distinguish the actual second request:

| Target | Original anchor request | Measured reachable request | Final actual | Label offset | Viewport height |
| --- | ---: | ---: | ---: | ---: | ---: |
| Height | 324 | 324 | 324 | 0 | 421 |
| Floors | 639 | 604.8801 | 604.8801 | 34.11987 | 421 |

`workshop_ranges_preserved` is true. Both Parcel runs report the same Assigned and Parcel history objects, exact Undo/Redo, separate autosaves and source/preferences/device preservation. Inspector additionally reports fields/history/seed/autosaves/sources preserved and an empty cleanup-error list. These are read scenario facts; this review did not operate input devices or rerun the scenarios.

Read root's saved final audit receipts under `BuildNotes/creator-clarity-20261029/windows001001/`: `base-45.json` has `BOUNDED_LOBBY_SOLO_WORKSHOP_WINDOWS_PASS`, `parcel-18.json` has `BOUNDED_PARCEL_SOLO_WORKSHOP_WINDOWS_PASS`, and `inspector-11.json` has `BOUNDED_WORKSHOP_INSPECTOR_WINDOWS_PASS`; `preservation.json` is PASS with no changed files. This reviewer did not independently execute those verifiers. The earlier235645/000027 failed receipt is not replaced or relabeled.

## Limits

This review covers default 1280 × 720 Windows views only. It does not establish every lower inspector field, every route tab, physical drag/controller input, all UI scales, online matches, complete routes/endings, animations or production readiness. Full camera motion and asset collision quality need their own play evidence. The unchanged screenshots are retained; there were no crops, overlays, image edits or generated replacements.

## Exact opened roster

The following SHA-256 values and dimensions were read from each existing file after direct viewing.

### 20261029T001200Z-wordbound_workshop_flow

| File | Dimensions | SHA-256 |
| --- | --- | --- |
| wordbound_equipment_menu.png | 1280 × 720 | `dab6a4a10299e89406463fb3a422bf5287c05a4de6dccd6b9c19258ae006d4c1` |
| wordbound_local_ready.png | 1280 × 720 | `bb4f7041c2d5a624965d6df4eceb393b9c59a83a90bd242ee3c31031803dd762` |
| wordbound_local_world.png | 1280 × 720 | `604ee2d7f1efa722b2f1812acf4b201585462aaac67954aa6ed3da0821831df5` |
| wordbound_party.png | 1280 × 720 | `beb142f7beb13432373851d33b31d4815cf98732c4e644ae9d05ea9f435e3f10` |
| wordbound_returned_workshop.png | 1280 × 720 | `c1a73b4a1326d41031e0a4a8ffd4d226a03cb7b0e1e06e996e0bada1fb36ac44` |
| wordbound_workshop_range_floors.png | 1280 × 720 | `9782b1a200e26fea9c6110ef43a5c363392547c3c416c7890cce6a1efd90a74e` |
| wordbound_workshop_range_height.png | 1280 × 720 | `3e64025b3ffed1ff60ae1ded7afd3a7fc5c671dfb2eafd670490edb6f91b2a54` |
| wordbound_workshop.png | 1280 × 720 | `c1a73b4a1326d41031e0a4a8ffd4d226a03cb7b0e1e06e996e0bada1fb36ac44` |

checks.json SHA-256: `ad01dc4dc2ac5759e863605e8389f53b13c3b571929b998effdabe6f735d5553`.

### 20261029T001439Z-wordbound_parcel_workshop

| File | Dimensions | SHA-256 |
| --- | --- | --- |
| parcel_workshop_assigned_returned.png | 1280 × 720 | `48446f13a368834f4eadf9282cea5ca13aaee1567f61db089eef2b9a5b36e7e3` |
| parcel_workshop_assigned.png | 1280 × 720 | `dc2da5743839cd597935b8eb3670393b57f0bb00121df43b2c1603d8c0babb12` |
| parcel_workshop_lobby.png | 1280 × 720 | `bcdf460971aa0d9e625ea6083171eb6adf3cd70f93588ff56cdbfea62f454bfc` |
| parcel_workshop_parcel_returned.png | 1280 × 720 | `ea8d57f608283122da2f199ad45d047c35710df2f89f9afc785ffe2be6abf2fd` |
| parcel_workshop_playtest_menu.png | 1280 × 720 | `9870ec82cd5901466051dfb775e4dea5f262e519f4e36eaba069c3cc3b9ac176` |
| parcel_workshop_playtest_world.png | 1280 × 720 | `7e74c2dcb5bdbd5584a26431343925438b18ca19770adb2c29a28e1b5ee29c44` |
| parcel_workshop_returned.png | 1280 × 720 | `07afeaa9201ea82ab4973f6cc8b971fc587fe791bf01e3b93db59fc1f87e4935` |
| parcel_workshop_saved.png | 1280 × 720 | `5994d123c2ef58e42fd6b3f4e10d0831b319b458ab2f5914538fa90a7d85022b` |
| parcel_workshop_sorting_close.png | 1280 × 720 | `e5b4fe812d2f24a74548a932d0b6ae034e1f5dc472763364c39bbc46ebe8f178` |

checks.json SHA-256: `a76ef16d598d5918023eb1b6cf4bdda3ffbc82ce7ab90a413a4cbd1e1be0b52c`.

### 20261029T001453Z-wordbound_parcel_workshop_inspector

| File | Dimensions | SHA-256 |
| --- | --- | --- |
| parcel_workshop_assigned_returned.png | 1280 × 720 | `48446f13a368834f4eadf9282cea5ca13aaee1567f61db089eef2b9a5b36e7e3` |
| parcel_workshop_assigned.png | 1280 × 720 | `dc2da5743839cd597935b8eb3670393b57f0bb00121df43b2c1603d8c0babb12` |
| parcel_workshop_lobby.png | 1280 × 720 | `7a6321625a8a474f41d692ca99e3b0c2cc753d5b57446dd9d74d1ffdabfd121a` |
| parcel_workshop_new_puzzle.png | 1280 × 720 | `d142d0c13b042728db3932fcbcf0eeef7781e90553890c58179e6c32e10001aa` |
| parcel_workshop_parcel_returned.png | 1280 × 720 | `3d8a30c22707552f69f0919a91329c0c5b0c2be604a55255855f43e37759ab1c` |
| parcel_workshop_playtest_menu.png | 1280 × 720 | `44e278700c3da241e430f16c01896d409a27c818a18c3d60fcab77c738753447` |
| parcel_workshop_playtest_world.png | 1280 × 720 | `a82b0d5de43f0cc846b6f65841a6f91760ec5484aed163858a19101b5464cd7a` |
| parcel_workshop_returned.png | 1280 × 720 | `33582e3b7c7e0b57a5294908d6571dc6c837a803d41c6a04cab3c67cd5354ecc` |
| parcel_workshop_room_routes.png | 1280 × 720 | `dc7b84583e95a2912f882c364766f9e1361a004e3e41f938b7afee326f527497` |
| parcel_workshop_saved.png | 1280 × 720 | `24eb740e96029bac666e8c1cbbe9bfbaee7e11794e9e33ca958ba92a383396b9` |
| parcel_workshop_sorting_close.png | 1280 × 720 | `37631161169d49ce5f7d80111cfcb0a89c4136c69f050cd6e186d249407e6e13` |

checks.json SHA-256: `9df657625080266c96681217f4c169a278ee40b88f470e1a2033ebbb5cd39118`.

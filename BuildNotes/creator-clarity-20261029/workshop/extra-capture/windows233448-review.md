# Windows 233448 Workshop review

STATUS: COMPLETE FOR ALL11 INSPECTOR IMAGES — BLOCKING ROOM WIDTH DEFECT. Original/Parcel Workshop runs remain pending; no combined28-image review claim.

Build20261028T233448Z, source042f375365652336ec53fa4170e0ba34553022ce (root supplied). Inspector run Docs/qa/runs/20261028T233658Z-wordbound_parcel_workshop_inspector. Root reports5steps/11images PASS; exact checks inspected below. Visual acceptance remains independent of smoke status. Original Workshop8 and Parcel Workshop9 run IDs pending.

Opened-image observations will be appended as confirmed. No owner acceptance or full-production claim.


Confirmed first5/11 original1280×720 images opened: parcel_workshop_assigned, parcel_workshop_saved, parcel_workshop_sorting_close, parcel_workshop_new_puzzle, parcel_workshop_room_routes.

Blocking finding: Room/routes horizontally scrolls beyond the300px inspector. Laps/Spawns, right edges of Module/recovery buttons and XYZ fields are clipped. This is a real layout failure despite the five-step smoke PASS. Root stopped completion flow and requested a bounded draft fix with actual width tests.

New puzzle: selected CLOCK art/label is clear, HAMMER visible, stool/table next row partially visible behind intentional vertical scroll. No noncraftable BOX/CHEST/CRATE/DESK/SHELF recipe shown. Add action is below fold, so this initial top capture cannot establish its fit. Fixed Objects/Puzzles/Room tabs and footer fit. Sorting close retains authored wood and original props, thin mint border (no floor-wide fill) and no floating wall fixtures. Props remain largely pale timber/cream; map composition still sparse. Assigned original full Attic frame is distant, with tiny furniture and faint room divisions. Remaining6 inspector images and later scenarios not yet opened.


## Completed 11-image review

All11 exact full PNGs below were opened directly with original detail; every image is1280×720. The remaining6 views confirm actual world, equipment menu, both saved-source returns and main lobby. The checks report exact build233448,5/5 PASS, clean inspector restoration and source/preferences/device preservation. Embedded commit is null; source042f375365652336ec53fa4170e0ba34553022ce is the root-supplied build identity, not an embedded screenshot claim. The strict delivery verifier has not been represented as passing this rejected layout candidate.

| Opened file | SHA256 |
|---|---|
| parcel_workshop_assigned.png | `01f9f266b81e232587807c0ec90584beee628649d8614656f7de5da35eca236a` |
| parcel_workshop_saved.png | `310e203ac448a732fb7d16f751c8606d850db6cdaf5f0dd419418f4454f46c6b` |
| parcel_workshop_sorting_close.png | `05b86b494bee597758a2c02840cffdb710e235f74b004ec75951d95e05804fd0` |
| parcel_workshop_new_puzzle.png | `c25b728ce653958dfa3e2f99e1476df4af77d6d7af877fc95a700c3eff53f361` |
| parcel_workshop_room_routes.png | `14ac5ea361b8720ac1d6748de9deaf0d9468d38a9b881307bf5d992e43c97d21` |
| parcel_workshop_playtest_world.png | `5070fbdaaf6b29494a034eda9efbb9e96e172ec70654145958e93376e3212da8` |
| parcel_workshop_playtest_menu.png | `04a7ec6892f076434770b6da971116f8e51749840e586d6ccc0d8f2f97d48567` |
| parcel_workshop_returned.png | `1a201451c465464417db58189989d899011a492d35f36302ec609f8181b85726` |
| parcel_workshop_assigned_returned.png | `d68108ef52d89ed43d4561761787ddebd64e5d12b4f717564386ec4ec4369708` |
| parcel_workshop_parcel_returned.png | `a0bf1f13aaf3f2533d55951e03a2e95f112f429baf0f4f9c9c168885a4d6939d` |
| parcel_workshop_lobby.png | `88b0e784c5342d13255113526098e56800cc1d5be255ed627138298b3f42975b` |

Blocking: Room/routes horizontal overflow clips actual navigation and edit controls, as described above. A source-only correction with real IMGUI239/300px ×15/22px tests is frozen in room-width-fix; that later candidate is not in these images. Root must rerun native/Windows and inspect fresh pixels before closing this defect.

Other findings and limits:

- Fixed inspector tabs, contextual selection and source return titles are visibly coherent. The returned Parcel draft comes back to Objects rather than leaving the temporary Puzzles/Room capture state active. Assigned and Parcel source switches show their distinct titles/floors and retained-history status. Exact Undo/autosave invariants come from probes, not these appearances.
- Normal object catalogue shows readable original Box/Chest/Clock/Crate imagery, with lower tiles intentionally clipped by a visible vertical scrollbar. New puzzle has CLOCK selected and only craftable choices visible; the lower tile labels and Add action are below this top capture. Their full fit/reachability is unproven, not silently accepted.
- Sorting close has proper brown planks and original furniture shape/detail with a mint perimeter rather than full mint floor. No floating cutaway fixtures seen. Much furniture is still pale cream/timber, repeated across a sparse rectangular room layout. The original Assigned Attic overview is too distant to assess individual props and has faint divisions. These remain visual-quality limitations.
- Actual playtest has readable compact HUD and Take keepsake prompt. The character occupies about half the image height; the keepsake and nearby station remain visible. This single starting angle does not prove all camera collisions or navigation views. Wall sign/sconces add identity; large wall areas remain plain.
- Equipment menu now separates the scrollable named recipe list from selected BAT detail and fixed return action. Text/actions fit horizontally. Disabled Craft text has very low contrast, and small recipe-list icons have limited detail; bottom TABLE row is partly visible behind the intentional scroll boundary. The missing-letter line is readable. Full controller/scroll discovery is not established by this one image.
- Main lobby return shows the avatar, header/navigation and local1/4 state correctly. Its richly illustrated Sunroom background still suggests a denser, more varied world than the actual room capture; no claim of stylistic coherence or owner acceptance.

This is an actual-image review of the11 inspector-flavor frames only. Original Workshop8 and ordinary Parcel Workshop9 were not supplied/opened for this build because root paused completion after the blocker. No full route, all authoring actions, accessibility extremes, physical controller use, network matchmaking, performance or production-ready claim follows from these captures.

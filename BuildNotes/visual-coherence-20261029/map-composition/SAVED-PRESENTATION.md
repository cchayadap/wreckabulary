# Saved Parcel Service presentation

STATUS: IMPLEMENTED; FINAL WINDOWS AND OWNER VISUAL REVIEW PENDING.

The separate local Parcel Service course now has eight original-art wall fixtures in three editable prefab groups, a clear rear-camera start and real map-card imagery. Gameplay and full previews show the saved dressing; the wall-free Workshop cutaway omits it so decorations do not float. Static decoration borrows original meshes/materials; it adds no usable items, physics bodies, scripts or puzzle progress. Existing courses have no dressing assignments.

Native checks: rules204708 passed20/20; combined native204900 passed92/92 (dressing, persistence, original material bindings, Workshop/input and HUD theme). Saved spawn correction205132 changed only Start0 z from-2.6m to0m and retained the exact old course/meta in spawn-camera/before-assets. This avoids the original south-wall camera squeeze. Its final normal Windows camera is still awaiting review.

Original source faces were inspected in all six images205502. Saved fixture creation205804 and no-write repeat210047 pass. The preservation receipt in parcel-dressing-setup/preservation.json records4299 unchanged prior authored files and the intended ParcelServiceVisuals profile change. Three new prefab groups are outside that pre-creation baseline.

Matched static captures205623 and205930 retain the same seed, map, source, geometry and camera. Root opened Parcel Service, Lost Address and Parcel Hall after capture. Parcel's original red desks, oak shelving, brass fixtures and blue walls are coherent, but the rooms still lack environmental density and strong landmarks. Lost Address and Parcel Hall remain particularly sparse. These are truthful previews, not approved final art.

Import210504 passed exact receipt/input/image checks. All three PNGs and saved LobbyPlayLayout references were imported from205930, with original files and metadata backed up in that capture's before-import folder. Parcel's existing GUID is retained. Cards use the already tested editable480px minimum; gameplay HUD overlays use neutral panel/ink roles instead of mustard action colour.

Evidence: Logs/exec/20261028T205132Z.json, Logs/exec/20261028T205804Z.json, Logs/exec/20261028T210047Z.json, Logs/exec/20261028T210504Z.json, TestResults/editmode_20261028T204900Z.xml, map-previews/captures/20261028T205930Z-70e5e4cc/import-receipt.json. Full native physical rerun210548 and fresh Windows build remain pending at this checkpoint. No public multiplayer, production readiness or owner design acceptance is implied.

Later verification: full physical run210548 passes both Time Run and Freestyle; Windows211157 passes all five scenarios and the strict63-image audit, with4306 authored files preserved. Its actual images exposed floating decoration in the cutaway. The bounded correction hides only wall decoration and restores owned original Lit floor materials, retaining texture/normal/UV detail. Native212943 passes13/13, including clone cleanup and source preservation. The final map-preview comparison keeps original0.7 fill with soft key shadows; lower0.3/0.5 attempts remain separate and were not imported. A fresh Windows review follows this correction.

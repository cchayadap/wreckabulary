# Local player view

STATUS: IN PROGRESS

Root owns the new `Assets/_Project/Scripts/Online/Client` assembly and eventual frontend composition. Existing lobby, Escape and local player controls remain outside this slice.

The player view is being prepared under ignored `.harness/wordbound` while native Unity jobs run. It adapts the existing remappable desktop input or an injected input binding, queues one jump edge for Fusion input, follows the interpolated local body, and performs camera obstruction queries only in that runner's physics scene. Menus and focus loss clear pending movement without changing global time scale.

`WordboundPlayerView` and its new Client assembly are now integrated. R0 passes in 29.1 seconds (`Logs/gate_check_all_20261020T140439Z.log`); the subsequent native presentation test run compiles the new assembly and passes 23/23 presentation cases (`TestResults/editmode_20261020T140526Z.xml`). These cases do not exercise player input or the camera. The view has not yet been wired into a playable launcher.

Combat rules are integrated as `5951865`. The HUD now sends authenticated equip, attack, pickup, letter collection, crafting, drop, throw, deploy and keepsake commands; it shows canonical inventory, progress and rejection messages. The presentation adapter displays only the canonical selected Held item while retaining distinct carried furniture and keepsake handling.

R0 passes (`Logs/gate_check_all_20261020T141129Z.log`), affected core tests pass 174/174 (`TestResults/core_20261020T141140Z.trx`, 38.9 seconds), and the actual Unity compile passes in 138 seconds (`Logs/gate_compile_20261020T141314Z.log`).

This is source/native compilation evidence, not input or UI acceptance. The launcher is not yet wired. The online owner's next wire checkpoint must permit Equip/Attack before live use. Native held-item selection, camera collisions, controls, controller menu focus and full HUD usability remain unverified. Next: integrate the reviewed coordinator/session/wire cutover, compose a live match and verify these behaviors.

The reviewed checkpoint session and Equip/Attack wire changes are now integrated (`2087906`), with the separately deployed checkpoint service. `WordboundMatchClient` composes authenticated session, scene, camera, selected-item presentation, HUD and optional exact-rules-bound physical puzzles. It handles confirmed Return to party separately from stopping the local Runner. HUD faults open the menu and provide a return path; blocked release has a readable explanation. The lobby launcher still needs to call this composition.

Merged R0 passes in 60.1 seconds (`Logs/gate_check_all_20261020T144012Z.log`) and all 228 Wordbound core cases pass in 30.3 seconds (`TestResults/core_20261020T144107Z.trx`). The first subsequent native run failed before tests because the newly integrated native fixture duplicated an existing test assembly name (`Logs/gate_test_editmode_20261020T144219Z.log`, 40.5 seconds). Native assembly repair and actual client verification are next. The explicit Photon.Realtime reference was added afterward and awaits the next check.

The repaired native run compiled the complete client composition and explicit references, then passed 42 of 43 selected tests (`TestResults/editmode_20261020T144759Z.xml`, 248.2 seconds). The remaining failure is the real two-peer session's initial scene setup. This is not playable client acceptance. The scene contract is being repaired before repeating that case; the party launcher remains in its separate lane.

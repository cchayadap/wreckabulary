# Local player view

STATUS: IN PROGRESS

Root owns the new `Assets/_Project/Scripts/Online/Client` assembly and eventual frontend composition. Existing lobby, Escape and local player controls remain outside this slice.

The player view is being prepared under ignored `.harness/wordbound` while native Unity jobs run. It adapts the existing remappable desktop input or an injected input binding, queues one jump edge for Fusion input, follows the interpolated local body, and performs camera obstruction queries only in that runner's physics scene. Menus and focus loss clear pending movement without changing global time scale.

`WordboundPlayerView` and its new Client assembly are now integrated. R0 passes in 29.1 seconds (`Logs/gate_check_all_20261020T140439Z.log`); the subsequent native presentation test run compiles the new assembly and passes 23/23 presentation cases (`TestResults/editmode_20261020T140526Z.xml`). These cases do not exercise player input or the camera. The view has not yet been wired into a playable launcher.

Combat rules are integrated as `5951865`. The HUD now sends authenticated equip, attack, pickup, letter collection, crafting, drop, throw, deploy and keepsake commands; it shows canonical inventory, progress and rejection messages. The presentation adapter displays only the canonical selected Held item while retaining distinct carried furniture and keepsake handling.

R0 passes (`Logs/gate_check_all_20261020T141129Z.log`), affected core tests pass 174/174 (`TestResults/core_20261020T141140Z.trx`, 38.9 seconds), and the actual Unity compile passes in 138 seconds (`Logs/gate_compile_20261020T141314Z.log`).

This is source/native compilation evidence, not input or UI acceptance. The launcher is not yet wired. The online owner's next wire checkpoint must permit Equip/Attack before live use. Native held-item selection, camera collisions, controls, controller menu focus and full HUD usability remain unverified. Next: integrate the reviewed coordinator/session/wire cutover, compose a live match and verify these behaviors.

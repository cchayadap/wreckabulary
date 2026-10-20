# Local player view

STATUS: IN PROGRESS

Root owns the new `Assets/_Project/Scripts/Online/Client` assembly and eventual frontend composition. Existing lobby, Escape and local player controls remain outside this slice.

The player view is being prepared under ignored `.harness/wordbound` while native Unity jobs run. It adapts the existing remappable desktop input or an injected input binding, queues one jump edge for Fusion input, follows the interpolated local body, and performs camera obstruction queries only in that runner's physics scene. Menus and focus loss clear pending movement without changing global time scale.

`WordboundPlayerView` and its new Client assembly are now integrated. R0 passes in 29.1 seconds (`Logs/gate_check_all_20261020T140439Z.log`); the subsequent native presentation test run compiles the new assembly and passes 23/23 presentation cases (`TestResults/editmode_20261020T140526Z.xml`). These cases do not exercise player input or the camera. The view has not yet been wired into a playable launcher.

The HUD draft remains under ignored `.harness/wordbound/WordboundMatchHud.cs`; it depends on the combat lane's selected-slot/attack API. Next: integrate that rules checkpoint, add and compile the HUD, independently review command/input behavior, then compose a live match and verify controls/camera in the native player.

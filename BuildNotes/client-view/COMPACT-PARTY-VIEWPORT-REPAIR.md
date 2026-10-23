# Compact party card — explicit drawing viewport

STATUS: SOURCE PASS after Input's independent bounded review; native and Windows verification NOT RUN for this repair.

Native `TestResults/editmode_20261023T234950Z.xml` records 37/39 PASS, no skips. All nine theme cases, twenty phase cases and eight of nine party cases passed. The 420-pixel party case correctly failed its strict drawn-width assertion: expected 396 pixels, actual 620. The 700 case passed. The unrelated TeamSolo admitted movement deadline failed separately. Gate reported no crash; this change does not address networking.

The fixture's EditorWindow width does not set Unity's global `Screen.width` in this native context. The previous private draw method therefore used the larger screen even when invoked by the narrow EditorWindow. The test must render the actual narrow geometry, not remove its assertion or treat the requested window width as proof.

The same private `DrawPartyGui` now receives its drawing viewport explicitly. Production `OnGUI` passes `new Vector2(Screen.width, Screen.height)` exactly where it previously called the method, preserving runtime sizing. The fixture passes its real `EditorWindow.position.size`. No public test override, stored viewport, alternate control renderer or setting mutation is introduced. All existing connected/controller tests use this same actual window route. The measured card algorithm, Layout/Repaint boundaries, scrolling, action names, input and deferred dispatch are unchanged.

The 420/700 cases retain assertions that the actual native window matches the request, actual card width matches that window's viewport, measured content fits the shorter card, and Repaint cannot mutate the applied rectangle. Required native filter: `Wreckabulary.Online.Tests.WordboundPartyMenuInputTests` (9 cases); adjacent `WordboundUiThemeTests` (9) remains useful. A fresh Windows disconnected/connected card capture is still needed for visual acceptance. No heavy jobs were run in this lane.

Independent review verified all repository callers, unchanged production Screen values, and that MenuRect is the only viewport consumer changed. Both edited source files matched root byte-for-byte before this delta; no baseline-only synchronization commit is required. The matching native log ends with the normal failed-test exit and shutdown; this remains preserved failure evidence, not a repaired native result.

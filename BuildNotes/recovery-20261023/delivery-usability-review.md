# Delivery and usability source review

STATUS: SOURCE AUDIT COMPLETE — BOUNDED FIXES ASSIGNED; NATIVE PENDING

Reviewed target: integration HEAD `467c87999d288aba7d93fe7848e271bfe12c8c6b` plus current working changes, 2026-10-23. Write scope is this report only. No root files, native jobs or external services changed.

Scope: launcher, party/gameplay menus, controller routes, course-authoring guide and saved scene/profile references. Findings require an exact source path and trigger; source evidence is not player/UI acceptance.

- [x] Trace actual player entry, mode/party/workshop/return routes and controller boundaries.
- [x] Compare no-code authoring guide to editor/profile/menu implementation and serialized references.
- [x] Record bounded verified findings, source pins and exact next actions.

| Finding | Exact trigger and evidence | Small fix / owner |
| --- | --- | --- |
| Party screen has no pad-only path | Enter Wordbound from the main lobby using its EventSystem controls. `Online/Client/WordboundPartyMenu.cs:74,151` only polls and draws IMGUI controls; no gamepad focus/confirm/back handler exists. The gameplay input/menu binding starts after a match. Connect, settings, ready, Quick Play, private-room actions and Back therefore need a pointer. | Add stable enabled-control focus and deferred actions with neutral gating to the existing party screen. Root assigned this bounded fix to input lane after this audit. |
| Workshop can edit while unfocused | With a selected module, `Workshop/WordboundWorkshop.cs:110` calls `ControllerInput` while visible; `WordboundWorkshopInteraction.cs:347` reads current pad and invokes move/rotate/undo without focus, typing, device-transition or neutral gates. `ProjectSettings/ProjectSettings.asset:90` enables background execution. When background gamepad events continue, the active draft can change behind another app. No native background event behavior is claimed. | Cancel pending gestures and suppress input on focus/typing/unplug transitions; require neutral before resuming. Root assigned this bounded fix to input lane. |
| Guide's record-copy route does not exist | `BuildNotes/unity-authoring/EDITING-GUIDE.md:19` says to configure puzzles by copying an existing station record. `Online/Authoring/Editor/WordboundCourseFields.cs:54–65` offers only Add (blank default record) and Remove; there is no copy/duplicate action or clipboard path. | Add a bounded duplicate-selected-record action with unique ID, or document the actual manual Add-and-fill workflow. No code/JSON is needed for fields, but the current copy instruction is not actionable. Root authoring scope. |
| Guide omits the new private workshop route | Guide line 27 describes multiplayer admission only in terms of approved content. Current `WordboundLauncherWorkshopOnline.cs:18`, `WordboundWorkshopOnlineContent.cs:12` and `Workshop/WordboundWorkshopOnline.cs:14` expose a validated authored bundle → private party route. | Document Connect → leave current party → Workshop → validate selected mode → host online → ready/private room, and distinguish public approval from exact private bundle validation. Root guide scope. |

Delivery prerequisites observed, not treated as source defects: the existing `Resources/Wordbound/LaunchAssets.asset` has ApprovedPublicCourses unassigned, MapPoolVersion `1`, and no serialized camera/visual/workshop references yet. No generated `Data/Wordbound/Authoring/*.asset` files exist at inspection. `WordboundDeliverySetup.PrepareFromEnvironment` plus `WordboundAuthoringSetup.CreateDefaults` are the implemented preparation paths; root must run and validate them before the Windows build. A build from this exact asset state has no approved Quick Play map choices and uses runtime defaults for optional profiles.

Positive source checks: Hub and both Wordbound scenes are enabled in Build Settings; the actual lobby scene points to the existing LaunchAssets GUID and camera. All inspected launch script, trust, network prefab and snapshot GUIDs resolve to existing metadata. Main lobby exposes a Wordbound button. Gameplay inventory/recipes/journal/return have the new controller route. Camera and model/material profiles are CreateAssetMenu assets with runtime validation and Inspector fields. Course forms cover current typed fields without JSON input; local editor play explicitly permits TimeRun/Freestyle. Scene restoration retains recovery state after failure. No missing serialized scene/prefab target was found in this bounded set.

Source pins (SHA-256, integration working copies):

- PartyMenu: `0205E4C97C4AF35C5C6A00399392954D1E79579FEB0F169CA9ACD9E34E16DA3A`.
- Workshop: `2B8B7FAC94FA9623D1BBFE3AA1388452B92F5F93022C6D8130FF996C67DEB136`.
- WorkshopInteraction: `AAE580DF14BC269309DA71AE51572D9BB89DB6B40A0F6FF2A45E6877AE687130`.
- CourseFields: `771A08CF9DC10F0092DCBB48F65A813F1E568FFA8AE0FE5B9A25EBF8690B2807`.
- EDITING-GUIDE: `B56E7DEA4D231F7DBD56A26F5D59A2DAF0444F7EB98E2E1E2FE43B1972507128`.
- LaunchAssets: `FAE847CC77DFA155FF01C4CD6DAE52CC39440C476241E109B744F22090AE1F88`.

Next action: input lane takes an exact private snapshot of current root PartyMenu/Workshop sources, implements only the assigned controller fixes and returns a delta against that snapshot. Root retains guide/asset preparation, menu caching/Fill semantics and all compiler/native jobs. Native UI navigation, screen sizes, actual background focus behavior and saved editor asset use remain NOT-RUN in this audit.

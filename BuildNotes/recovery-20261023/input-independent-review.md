# Independent input review

STATUS: IN PROGRESS

Inspected `wordbound-puzzle-race` commit `6c83d5d7d401ea79f3d5a5a3b09aed36df08f407` read-only at the input owner's request. Scope: `WordboundInputBinding`, `WordboundPlayerView`, `WordboundMatchHud`, menu/focus helpers, original bindings and the 18 new input/menu/cache cases. No Unity or compiler ran in this review.

## Confirmed source finding

`WordboundMatchHudMenu.MenuButton` consumes pending gamepad confirmation on `EventType.Repaint` (line 92). The journal/equipment handlers immediately change `journalOpen` at lines 110–112, and the same GUI pass then follows the newly selected branch at line 117. Clear-search at line 134 likewise changes the recipe controls drawn during the current Repaint. That can request a different GUILayout tree from the preceding Layout, rather than applying the action between GUI passes. The focus-model tests do not exercise that native GUI ordering.

Sent to the input owner: defer topology-changing actions until Update/next Layout or safely terminate the active GUI pass; exercise a real Layout→Repaint branch transition before declaring the controller menu verified. This report does not claim a native reproduction.

## Other inspected paths

The gameplay release latch suppresses held attack/defense/drop/craft/interact and slot actions across menu/device/focus suspension while retaining responsive camera/movement. Device loss explicitly opens the menu and emits a released command frame. Focus loss resets menu selection/pending confirmation through `MenuRequested`. Empty equipment slots are disabled, and recipe caching is refreshed only on changed letter contents. No second confirmed gameplay defect was established in these bounded source paths.

Native hotplug, focus changes, IMGUI behavior and controller feel remain outside this source review's evidence. Await the owner's repair and root's tests before closing the finding.

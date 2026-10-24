# Wreckabulary implementation plan

The approved plan is [wordbound/PLAN.md](wordbound/PLAN.md); current evidence and next action are in [progress.md](../progress.md) and [wordbound/STATUS.md](wordbound/STATUS.md). The written plot, endings, player behaviour and rating design are in [STORY-AND-PLAYER-EXPERIENCE.md](wordbound/STORY-AND-PLAYER-EXPERIENCE.md); new narrative systems remain implementation tasks.

| Milestone | Required outcome | Current status |
| --- | --- | --- |
| M0 | Preserve work, reconcile histories, verify regression baseline | In progress; no formal closure |
| M1 | Eight-player online interaction and host recovery proof | SDK/authentication and bounded transport verified; final large-party gameplay and recovery targets open |
| M2 | Complete three-lap race loop | Compact two-client Time Run completed; full-course races and all-mode outcomes open |
| M3 | Large layered world and deterministic generator | Content/generation implemented and seed cases verified; complete physical traversal and session duration open |
| M4 | Workshop, templates and twelve assets | Exact-draft Windows playtest/return verified; original artwork restored; complete controls, held equipment and human review open |
| M5 | All modes and public matchmaking | Catalogue deployed and bounded two-client cases recorded; latest public movement still fails |
| M6 | Verified Windows release candidate | Review build005254 exists; official smoke, performance and final release acceptance open; isolated editor patch under test |

These are implementation and evidence states, not formal milestone closure. The current order is to resolve Windows stalls and the Freestyle spawn issue, verify the isolated editor patch, then finish full-match, larger-party, recovery and final release checks. Preserve each failure and outstanding acceptance item in progress.md and STATUS.

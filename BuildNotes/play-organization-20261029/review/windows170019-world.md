# Windows 170019 — Solo and Workshop visual review

STATUS: REVIEW COMPLETE — no newly observed blocking layout defect in these captures. World art and preview limitations remain OPEN; this is not final art acceptance.

Build: 20261028T170019Z. Directly opened all 21 unmodified 1280 × 720 PNGs below using view_image, not only receipts/thumbnails. No native invocation or live edits by reviewer.

## Automated evidence, kept separate

- Solo 20261028T170426Z-wordbound_solo: completed PASS, 4/4 stages, 13 captures.
- Workshop 20261028T170534Z-wordbound_workshop_flow: completed PASS, 5/5 stages, 8 captures; ended 2026-10-28T17:05:52Z before its images were reviewed.
- These smokes do not establish physical-input play, completed campaigns, networking, team play, or visual acceptance.

## Actual observations

- **Lobby and chooser:** the warm illustrated sunroom, readable slim lobby type and clean navigation survive in the actual Windows player. Local / Online / Create and House games / Wordbound solo are visibly separated. Time Run, Freestyle and the two maps have legible labels and clear teal selection borders/checkmarks. All three return screenshots preserve the mode/map selection. No overlapping page titles, broken glyphs or obscured Play action are visible at 1280 × 720.
- **Solo preview limitation stays open:** Lost Address is represented by a parcel, Parcel Hall by a table. These identify choices but reveal neither layout nor destination atmosphere. The lower half of the chooser remains largely empty. A real map view or authored thumbnail is still needed; do not call these preview cards complete.
- **World/map identity stays open:** the wood floor is textured again and there are no decorative background stripes or repeated fake blue-window panels. The red avatar and its dark satchel read clearly. Lost Address's start is still a vast teal box with a plain aperture and a large exposed yellow ramp; furniture is dwarfed by the room and the lobby's richer art sets a quality expectation this world does not meet. Parcel Hall visibly adds tables, stools and sofas and leaves a clear central route, but the sparse clusters do not yet establish a convincing parcel hall or distinct destination architecture. These screenshots cover the entry, not the four later destinations.
- **Guidance:** Time Run shows a clearly separated HUD and yellow checkpoint ring/instruction; Freestyle removes the timer/checkpoint emphasis. Parcel Hall's contextual pickup prompt visibly changes to STOOL. The Freestyle entry offers little environmental guidance beyond interaction prompts. Screenshots cannot prove held-item alignment, motion, facial emotion, collision behaviour or puzzle completion.
- **Equipment:** the search/list/detail split is readable with a large item image, short description, missing-letter message and disabled Craft action. Resume remains visible at the top. Solo uses Return to lobby; Workshop uses Return to workshop. One partially visible bottom list row is the scroll viewport boundary, not evidence of a missing item. This panel retains the heavier gameplay font and plain white styling; the new Roboto lobby typography is not applied across this entire interface.
- **Workshop:** name, mode, seed lock, editor actions, top-down draft, floor and playtest controls are readable. X/Z min/max, world height, rotation/floor choices and Apply/Cancel are visible in the focused range captures without text colliding. The return image restores the same visible draft/selection. The seed remains 0314159265358979 in all editor views.
- **Workshop polish stays open:** the screen still presents many equally bold controls, internal names such as Attic_BookSpring and Props_Attic_BookSpring, and tightly packed numeric controls. Selection states for the range rotation choices are not obvious from the captured appearance. The white selection handles cover part of the selected floor area. Clearer human labels, stronger active states and progressive disclosure would better meet the requested minimal, approachable creator experience. These are design follow-ups, not evidence of a newly failing range operation.

## Exact reviewed files

All Solo files are under docs/qa/runs/20261028T170426Z-wordbound_solo/:

1. solo_initial_lobby.png
2. solo_timerun_selected.png
3. solo_timerun_world.png
4. solo_timerun_menu.png
5. solo_timerun_returned.png
6. solo_freestyle_selected.png
7. solo_freestyle_world.png
8. solo_freestyle_menu.png
9. solo_freestyle_returned.png
10. solo_parcelhall_freestyle_selected.png
11. solo_parcelhall_freestyle_world.png
12. solo_parcelhall_freestyle_menu.png
13. solo_parcelhall_freestyle_returned.png

All Workshop files are under docs/qa/runs/20261028T170534Z-wordbound_workshop_flow/:

1. wordbound_party.png
2. wordbound_workshop.png
3. wordbound_workshop_range_height.png
4. wordbound_workshop_range_floors.png
5. wordbound_local_ready.png
6. wordbound_local_world.png
7. wordbound_equipment_menu.png
8. wordbound_returned_workshop.png

The result supports a reviewable Windows delivery with clearly stated unfinished world/creator polish. It does not support declaring the comprehensive production-ready art/gameplay plan complete or owner-approved.

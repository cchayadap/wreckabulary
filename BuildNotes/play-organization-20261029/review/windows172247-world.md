# Windows 172247 — Solo and Workshop visual review

STATUS: TECHNICAL CHECKPOINT REVIEW COMPLETE. All 21 full 1280×720 images directly opened. No newly observed blocking layout regression; NOT visual acceptance. The owner explicitly rejected current style/map coherence after these runs.

Build: 20261028T172247Z, root identifies exact source74ffae03. Solo172443 completed PASS4/4. Replacement Workshop172801 completed PASS5/5, focused=true for party and playtest. The earlier Workshop172606 remains excluded because the strict audit found focus=false; its two already-inspected party/editor frames are retained as superseded evidence, not substituted into the final roster.

## Findings

- Solo mode/map cards, titles, Play action and selection marks remain legible. Time Run, Lost Address Freestyle and Parcel Hall Freestyle return to the same visible choices. No broken glyph, overlapping title or obscured action appeared in this set.
- The equipment list/search/detail split is still readable; disabled Craft has a missing-letter explanation. Solo shows Return to lobby and the Workshop playtest shows Return to workshop. The scroll viewport deliberately clips the next row at its lower edge. The panel retains a heavy gameplay font/plain styling rather than the thinner lobby type.
- The replacement Workshop ranges show X/Z/height, rotations, floors and Apply/Cancel with no new collision. The selected draft, seed0314159265358979 and visible module persist across return. White handles, raw names such as Attic_BookSpring/Props_Attic_BookSpring, equally bold controls and subtle selection states still make the editor busy and less approachable.
- Map quality is materially unfinished: the lobby sunroom is rich and coherent, while Lost Address is an oversized near-empty teal room with a bare doorway/yellow ramp. Parcel Hall has more original tables/stools/sofas and a clear center route but is still sparse and lacks a strong parcel-hall identity. Wood floor texture survives; there are no decorative background stripes or repeated fake blue window boards. This does not establish a detailed, cohesive map.
- Lost Address/Parcel Hall chooser art is still a box/table identifier, not a map view. No claim that the real-preview audit has been implemented. The single-card correction is not directly demonstrated by this two-map Solo/world set; its intended Create/other single-card page is covered separately by root's lobby capture review.
- Current screenshots do not test held-item alignment, facial animation, movement, real input, late destinations, puzzle completion, online matching, bots or teams. Automated PASS and visible readability do not mean art, gameplay or the full production plan is complete.

Owner correction is retained as the next design priority: the current images/styles do not fit the actual map/objective; colours feel old/plain, and Workshop objects/maps need more detail and cleaner design. Next work must improve actual world/presentation coherence rather than merely relabel this checkpoint as finished.

## Exact final roster — 21 images

Solo directory: docs/qa/runs/20261028T172443Z-wordbound_solo/

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

Replacement Workshop directory: docs/qa/runs/20261028T172801Z-wordbound_workshop_flow/

1. wordbound_party.png
2. wordbound_workshop.png
3. wordbound_workshop_range_height.png
4. wordbound_workshop_range_floors.png
5. wordbound_local_ready.png
6. wordbound_local_world.png
7. wordbound_equipment_menu.png
8. wordbound_returned_workshop.png

No source/live asset/native changes were made by this review.

# Lost Address: current story and asset expansion

STATUS: SOURCE/EVIDENCE REVIEW COMPLETE — 2026-10-26. Owner: story_status. This is a design and evidence report, not a new playable-map delivery or human playtest result.

Inspected checkout: `wordbound-integration`, branch `sethypagna/wordbound-integration`, HEAD `0327e4739c5bfa74c70169f727172eaa70518ca9`. This lane writes this report and, under root's expanded assignment, reconciles stale status in `docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md`. No runtime/source changes, native jobs, imports or service writes. Root owns post-restart verification.

## The story in plain English

**The Lost Address** is a household adventure about finding where home belongs. A parcel bell rings, but the delivery labels are wrong. The Movers keep sending kitchen things to the attic and other belongings out of place. The roommates carry a keepsake through the house, breaking furniture into letters and spelling useful equipment to get past the machinery.

The first journey goes up through **Packing Yard → Kitchen Works → Attic Stacks → Rooftop Garden**. Each place adds a clue: the labels are mixed up, and the rooftop windmill is repeating a missing address. The second journey follows those instructions back down. The team discovers a broken sorting pointer; the Movers follow bad instructions rather than acting as a newly established villain. The final journey goes up again after the Packing repair route, putting the address back in order and delivering the keepsake home.

This is a warm mechanical mystery: break, understand and rebuild. The source supports the route and clue sequence. The brass bell, teapot chimney, toy-house archive and windmill are authored landmark concepts; final distinctive artwork, readable movement and repair transitions are not established by this audit. No new Mover cutscene, named hero biography or enemy encounter is claimed.

## Implemented versus proposed

| Area | Inspected state | Remaining distinction |
| --- | --- | --- |
| Course | Four destinations/layers, 34 room modules, three laps, ascending/descending/ascending route, six puzzle families and 72 puzzle sites. Packing has a possible repair detour on lap two and mandatory repair route on lap three. | Authored scale/determinism do not prove comfortable traversal, interesting repetition or a finished visual environment. |
| Chapters | `Find the address`, `Follow the wrong delivery`, `Put home back together`. Fourteen canonical clue IDs; selected-route filtering determines which appear. | Chapter text does not prove physical repair animation. |
| Minimal story UI | At most the latest earned cue in the local team's current chapter; anchored to its reached place, hidden beyond 20 m, suppressed for migration/results/unavailable players. No duplicate automatic story banner in the current view. Full earned clues remain in the optional journal. | Actual camera legibility and comprehension without reading remain unobserved. |
| Unity editing | Saved `Assets/_Project/Data/Wordbound/Authoring/LostAddressStory.asset`, wired through `LaunchAssets.asset`. Copy validation, chapter/clue/epilogue previews and editable text need no code changes. | The saved profile currently has chapters/notices but no serialized epilogue rows; runtime defaults work. **Add missing epilogue copy** in the Inspector exposes default rows, then save. This lane did not perform that asset write. Preview cannot change triggers or award progress. |
| Endings | Six result-aware epilogue IDs implemented against confirmed course/mode/roster/team/result. | Six outcome presentations, not six separately branching campaigns. |
| Optional restoration | Four extra tasks and the richer `A place for everything` ending remain proposed. | No new task state, recovery contract or richer ending is claimed. |

The previous story document, `STATUS.md`, rubric observations and parts of `progress.md` predate the current implementation. Claims that no ending resolver/editable story profile exists are superseded. Root authorized this lane to reconcile the story document only; it does not own other shared trackers.

| Epilogue ID | Player title | Actual trigger |
| --- | --- | --- |
| `TimeRunSuccess` | Home, at last | Valid confirmed cooperative final delivery and earned final clue. |
| `TimeRunExpired` | Delivery postponed | Valid confirmed Time Run timeout without a finishing team. |
| `RaceDelivery` | Home, at last | Own team completes the route and is among confirmed winners; same-tick delivery draws are not described as exclusive wins. |
| `RaceProgressWin` | Furthest along | Race times out; own unfinished team is sole confirmed progress winner. |
| `RaceProgressDraw` | Level progress | Race times out; own unfinished team shares confirmed best progress/timing. |
| `RaceDefeat` | Delivery unfinished | Another team wins; no opponent's unreached clue is exposed. |

FFA has its knockout result, not a campaign ending. Freestyle has no campaign win/timeout epilogue. Disconnects, Return and accessibility support are not moral failures. Public matchmaking remains unranked; no official age classification or predicted store rating exists.

Opened [native XML](../../TestResults/editmode_20261026T081750Z.xml): **144/144 passed, zero failed/skipped**, including **22/22 StoryEpilogue cases**. XML execution is 41.19 s; the [gate receipt](../developer-workspace/continuation-20261026.json) records 84.7 s end-to-end. This suite covers presentation/story/profile/readability/persistence/menu/cleanup, not an uncoached human campaign completion. These changes are newer than the tested Windows review build; root must establish a current built-player result separately.

## Proposed expansion: The Lost Address — House in Motion

This working name describes visual/spatial enrichment of the four existing places, **not a sixth mode or implemented expansion**. Preserve the original plush characters and painted household identity. Add stronger silhouettes, saturated painted surfaces and believable hinges, seams, fasteners and supports. Detail should explain construction and movement rather than bury the player in geometry.

The concurrent intake lane confirmed **140 Kenney + 53 KayKit designs**, both with supplied CC0 licenses. All **193 inspected glTF payloads have zero skins and zero animation clips**. Kenney is 12–992 triangles/design; KayKit is 44–1,018; intake found no normal maps. Scale varies substantially. These are useful static sources, not finished richly textured/rigged game assets. Exact evidence belongs in [local intake](local-intake.md) and [Unity intake](unity-intake.md); this lane did not independently parse all files.

| Place and purpose | Confirmed candidate source names | Proposed treatment and acceptance |
| --- | --- | --- |
| Packing Yard: first craft and departure | Kenney `table`, `doorway`, `stairs`/`stairsOpen`; KayKit `cabinet_medium_decorated` | Warm painted wood/brass; prominent parcel dock. Separate first breakable from clutter. Check clear paths/spawns, readable target and door swing/pivot/collider agreement. |
| Kitchen Works: controllable sorting | Kenney `ceilingFan`, `table`; KayKit `table_medium_long`, `shelf_B_large_decorated` | Vivid enamel/timber with visible motor, shaft and guards. Keep decorative fan distinguishable from playable FAN. Check clearance/support and avoid false interaction cues. |
| Attic Stacks: evidence and interpretation | Kenney `bookcaseClosedDoors`; KayKit `book_set`, `lamp_standing`, `cabinet_medium_decorated` | Ink blue, amber and wood; cabinets frame the house diagram. Check normal-camera readability, dark character separation and door collision; repeat downstairs label shapes. |
| Rooftop Garden: final destination | Kenney `stairsCorner`, `loungeSofa`; KayKit `couch`, `armchair_pillows` | Domestic terraces and vivid upholstery around the windmill. Keep destination visible on approach. Check edges, recovery and keepsake route; treat soft fabric and rigid frame motion separately. |

Kenney candidates are under sibling `Wreckabulary/assets/Models/GLTF format/*.glb`; KayKit under `Wreckabulary/assets/KayKit_Furniture_Bits_1.0_FREE/Assets/gltf/*.gltf`. Sources remain intact. `doorway` has a separate door, `bookcaseClosedDoors` separate left/right doors and `chairDesk` a separate chair. These are articulation starting points, not authored animation or verified hinge pivots.

### Larger maps without longer chores

The current 72 sites leave **20.8 seconds each** within 25 minutes, before travel/mistakes. Enrich room character and sightlines first; compare a full run before lengthening mandatory routes. Keep new exploration pockets optional and nearby until observed pacing shows headroom. An untimed Freestyle variant can support longer exploration without silently altering a competitive course.

Retain recognizable destination anchors. Vary approaches, prop grouping, palette accents and optional detours in separate deterministic streams. Decoration cannot alter required supplies/checkpoints/spawn safety. Gameplay geometry or requirement changes need a new content identity; all peers need the same resolved manifest. A larger room should offer a useful decision, cooperation or discovery, not just walking.

### Rig, material and physics review

Furniture usually needs rigid hierarchies, not a humanoid skeleton: hinges, drawers, shafts and folding mechanisms must have credible pivots/stops. Character skins must preserve skeleton mapping/weights/bounds and test shoulders, wrists, hips, knees, footwear and accessories through actual animations. Painted skins cannot conceal team/interaction identifiers.

For every pilot family, compare original/candidate with identical camera/light/scale/background: close-up, gameplay distance, held, placed, thrown and active. Inspect motion/contact/weight as well as stills. Check units, center of mass, grip, collider clearance, UVs/normals, seams, silhouette and material response. Review 720p/1080p in dark and bright places. Record triangle/material/texture/draw-call/memory costs and LOD transitions. Concept images, validated meshes and tested online items are different milestones.

## Low-text introduction and player review

Introduce one visible obstruction with its marked source and matching receiver. Let the player complete **break → letters → spell → use**, then confirm success through a physical response and short contextual cue. Repeat distinctive label shapes to reveal the sorting mistake; colour reinforces rather than carries meaning. Apply a familiar tool in a different context. Show repair only after authoritative progress, without moving occupied terrain or exposing rival progress. End with Return available and an optional journal epilogue, not mandatory reading.

The specific label art/mechanical staging above is proposed, even though the underlying loop and cue/result contracts exist. Use the eight-dimension [experience rubric](../../docs/wordbound/GAMEPLAY-EXPERIENCE-RUBRIC.md); keep **N/O** for unobserved human experience instead of turning automated tests into enjoyment/immersion scores.

| Perspective | Anticipated issue | Evidence to collect |
| --- | --- | --- |
| New solo player | Smashes decoration; hoards letters; confuses equip/place | Proposed target: 4/5 identify next objective within 60 s and finish first loop within 5 min uncoached. |
| Expert/returning player | Skips prose and repeats remembered recipes | No forced pause; demonstrate an actual new decision rather than identical repeated crafting. |
| Silent public pair | Duplicates crafting or cannot locate carrier | Proposed target: 4/5 unfamiliar pairs complete first objective without voice/developer help; record handover failures/stalls. |
| Mixed skill | Expert advances while beginner waits | Meaningful parallel preparation; record idle periods over 60 s caused by exclusive tasks. |
| Explorer | Inspects props and misses timer pressure | Clear timed-mode contract before starting; Freestyle practice; no new compulsory chores. |
| Low-text/access needs | Tiny labels, colour-only cues, rapid effects | Shape/location/action cues, muted-audio/reduced-motion checks, keyboard/controller and both resolutions; no mandatory timed text. |
| Disruptive player | Withholds keepsake or changes shared station repeatedly | These remain open gameplay risks. Asset polish does not fix them; require authority/recovery checks and understandable remedies. |

Observe at least five first/repeat participants across two seeds before claiming 18–25-minute pacing. Separate travel, puzzle, queue/load and recovery time; report failures/aborts and assistance. Ask for an unprompted retelling before explaining the mystery. Map feedback should separate **clarity, fairness, enjoyment and reliability**, tied to content/seed. Do not invent store ratings, player skill scores or age-classification decisions.

## Next recoverable actions

- [x] Reconcile current source, saved profile and 22 passing epilogue tests; identify stale documentation.
- [x] Connect supplied candidates to the four places without claiming import, animation or style acceptance.
- [ ] Root completes post-reboot native diagnostics and current Windows/online verification.
- [ ] Add/save missing epilogue rows through Unity if desired, preserving authored copy.
- [ ] Normalize and compare a small material/articulation pilot in isolated staging before broad import.
- [ ] Stage one complete readable first-room loop and rooftop landmark; test visuals/motion/physics/performance against originals.
- [ ] Obtain James's style review and full-course observations before enlarging mandatory routes or adding restoration tasks.

## Sources opened

[Layout/clues](../../Assets/_Project/Scripts/Rules/Wordbound/WordboundFlagshipLayout.cs), [content](../../Assets/_Project/Scripts/Rules/Wordbound/WordboundFlagshipContent.cs), [story plan/ending resolver](../../Assets/_Project/Scripts/Online/Presentation/WordboundStoryPlan.cs), [profile defaults](../../Assets/_Project/Scripts/Online/Presentation/WordboundStoryProfile.cs), [contextual view](../../Assets/_Project/Scripts/Online/Presentation/WordboundStoryView.cs), [journal UI](../../Assets/_Project/Scripts/Online/Client/WordboundMatchHudMenu.cs), [Inspector](../../Assets/_Project/Scripts/Online/Authoring/Editor/WordboundStoryProfileEditor.cs), [setup](../../Assets/_Project/Editor/Online/WordboundStorySetup.cs), [saved profile](../../Assets/_Project/Data/Wordbound/Authoring/LostAddressStory.asset), [launch assignment](../../Assets/_Project/Resources/Wordbound/LaunchAssets.asset), [progress](../../progress.md), [story design](../../docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md), and native evidence above. No new tests were run by this lane.

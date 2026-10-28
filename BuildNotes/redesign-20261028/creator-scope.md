# Player creator scope audit

STATUS: COMPLETE — READ-ONLY SOURCE AUDIT, 2026-10-28. Native/editor/build execution was not performed by this lane.

Scope: existing player-facing creation in `wordbound-integration`, including the Wordbound course workshop and the separate original-game Creative Workshop. This audit identifies implemented controls and remaining product scope; it does not claim these features passed the current Windows acceptance run.

- [x] Trace creator UI, bounded course schema, validation, storage and online binding.
- [x] Map rules, teams, objectives, scoring, items, randomness and save/share controls.
- [x] Recommend one concrete next improvement using the existing schema.
- [ ] Implement and validate that improvement in a subsequent owned lane.

## What players can create today

| Area | Exposed controls and current behavior | Boundary / missing scope |
| --- | --- | --- |
| Starting content | Open assigned course, Flagship draft, Race Circuit, Combat Arena or Freestyle World. Draft sessions retain edits and Undo history when switching. Rename the course. | Templates and module geometry come from an approved catalogue. There is no arbitrary mesh/model upload or in-game room shape sculpting. |
| Map arrangement | Place catalogue modules; click/drag and marquee selection; additive selection; move, quarter-turn rotate, duplicate, delete, group/ungroup; grid snaps; advanced X/Z position; connect two modules. Orbit, pan, zoom, frame selection/map and top view. | Connection creation chooses Walk/Bridge/Stairs based on positions. No full player-facing inspector for connection type, one-way policy, width or all connection endpoints. The catalogue room dimensions remain fixed. |
| Floors | Add floor at a numeric height, select, hide/show, lock/unlock, move modules to active floor. | Floor visibility/locking is editor state. A zone belongs to one room; selecting allowed floors does not make objects roam across rooms. No automatic random floor layout builder is exposed. |
| Objects / furniture | Search approved object IDs with icons. Choose Fixed, Zone or weighted Pool. Place/reposition fixed objects; draw/redraw rectangular bounds; edit pool members and weights. Fixed placements can be selected and moved/rotated. | Available objects depend on source catalogue: proof templates expose BED/FAN/STOOL/PLATE; Flagship exposes its 13 approved equipment entries. No player-authored item stats, behaviors, models or paint/skin controls in the Wordbound course schema. |
| Random placement | Min/max count, spawn chance in 0.1% steps, spacing, world X/Z/height bounds, allowed quarter-turn rotations and floors. Pool weight is 1–10000. Counts are bounded 0–256; nonzero guaranteed minimum requires 100% chance. | These controls randomize prop selection/placement within authored rooms; modules themselves are not reassembled by a procedural layout grammar. Validation may reject impossible counts/clearance. |
| Random routes | Add/edit sections, entry/exit modules and weighted alternatives; order connections and checkpoints; reverse or delete routes; one alternative chosen per section/lap from seed. | This selects among authored route alternatives. It is not arbitrary runtime branching logic or player-scripted events. |
| Seeds | Sixteen hexadecimal digits; Set seed; lock/unlock; Reroll when unlocked. Preview and playtest use the exact current seed. | Seed control is separate from course JSON export: course-only file sharing does not preserve the currently chosen seed. |
| Existing modes | Select a supported Time Run, Team Race, Brawl Race, Free-for-All or Freestyle. Team/Brawl Race expose Solo/Duo/Squad formats. Freestyle can be enabled on the current document as an undoable edit. | No mode-rule builder. The UI does not expose arbitrary enable/disable mode editing beyond adding Freestyle. Switching preview mode does not redefine gameplay rules. |
| Teams / player counts | Mode and format determine capacity. Spawn editor exposes Start/Respawn, module, position and quarter-turn yaw, with required anchor counts. Runtime mode allocates teams/seats. | Custom team count, asymmetric teams, team-specific spawn binding, friendly fire or ownership policy are not creator controls. Every puzzle must retain a one-player solution; validator rejects MinimumPlayers other than 1. |
| Objectives | Six approved puzzle families: CollectLetters, PlaceObject, BuildRoute, RedirectWind, BalanceWeight, TimedMechanism. Add puzzle + protected renewable recipe letters; link prerequisite puzzle; link completion to an existing gate connection. | New objective creation still asks for raw Recipe ID, Puzzle ID, prerequisite ID and connection ID. No complete existing-puzzle editing/removal flow or visible unlink control here. Reward letters, consumption policy and supply conditions exist in schema but are not general creator UI controls. |
| Physical puzzle behavior | Per-lap physical controls, checkpoint/supply references, draggable marked controls, socket position/yaw/radius, yaw tolerance, hold time, arrival box where applicable. Family-dependent source props, tool/receiver supplies, floor pads, receiver bounds, moving fixture start/end/size/period/phase and active window. | Physical controls require an existing ordered checkpoint on the selected lap, a compatible approved recipe and recovery supplies. Adding a logical puzzle alone can still fail Playtest until these links are completed. No general scripting, custom physics parameters or arbitrary trigger logic. |
| Progression / recovery | Add/edit safe anchors and available-after puzzles; add checkpoints with required puzzle order and arrival connection; order route sections in laps; choose lap start/finish and approved story event. Delete operations protect/remove references as defined. | Races require exactly the three supported laps. Arbitrary lap count is not available. Custom endings, dialogue, story text or a narrative graph are not available in the player workshop. Lap story is an approved-event picker only. |
| Scoring / victory | Runtime mode applies its existing objective: deliver keepsake, knockouts or explore. | Time Run/Team Race/Brawl Race use 3 laps and 1500-second limit; Free-for-All uses 10 knockouts/480 seconds; Freestyle has no time limit. Target score, timer, win condition, rounds and scoring formula are fixed ModeDefinition data, not saved creator settings. |
| Preview / play | Preview validates selected mode/team size and seed; highlights validation issues. Local one-human playtest is available for Time Run/Freestyle. Private online hosting validates the current resolved map and physical controls. | Local team/competitive simulation with custom bot roster is not exposed here. This audit does not prove a current network session or current build. |
| Save / recovery | Autosave after revision settles; save on close/disable; atomic write with previous backup; recover autosave; scoped template/assigned drafts; 64 Undo snapshots with an 8 MiB history budget. | No in-game personal map-library browser with thumbnails, named copies, revision history or publish workflow was found in this workshop. Rename changes title, not course ID. |
| File sharing | Share JSON panel, Copy/Paste, validated Import, Save file. File is `.course.json`, bounded to 2 MiB. | Export/import covers course document only; chosen seed, editor selection, groups, locked/hidden floors are autosave state, not carried by the file. Import creates default workshop seed `0000000000000001`. |
| Online sharing | Host online opens private party using exact document+resolved manifest+seed+mode/format; content hash/geometry/item/rules identities validated; joiner must obtain matching content before Ready. | Requires the installed approved geometry/item/rule sources; custom item/rule files are rejected. Authored maps are not in public Quick Play unless explicitly in approved registry. No public creator browser, ratings, moderation/reporting, versioned publishing or discoverability flow found. |
| Cosmetics / separate original workshop | Original Creative Workshop lets players furnish an existing house with approved objects, type object words, place/auto-place, move/nudge/rotate/remove, select Classic/Candy/Arcade finish, Undo/Redo, save, JSON import/export and peaceful Tour. | This is a separate Home schema and furnishing/tour flow, limited to 64 props. It does not create Wordbound race routes, objectives, online rules or new skins. Current Wordbound schema has no per-prop skin/material identifier; only approved ThemeId. |

## Evidence map

All references are source evidence, not test results.

- `Assets/_Project/Scripts/Workshop/WordboundWorkshop.cs:212`: header/palette, inspectors, typed puzzle links, Preview/Playtest/Host online, seed controls and Share JSON.
- `Assets/_Project/Scripts/Workshop/WordboundWorkshop.cs:124`: revision refresh, autosave scheduling, close/disable saves and validation.
- `Assets/_Project/Scripts/Workshop/WordboundWorkshopModes.cs:84` and `Assets/_Project/Scripts/Rules/Wordbound/WorkshopModes.cs:26`: supported mode/format choices and sole enable-Freestyle edit.
- `Assets/_Project/Scripts/Workshop/WordboundWorkshopPlacementPanel.cs:18`, `Assets/_Project/Scripts/Workshop/WordboundWorkshopPlacement.cs:36`, `Assets/_Project/Scripts/Workshop/WordboundWorkshopInteraction.cs:500`: floors, placement settings/constraints and icon object picker.
- `Assets/_Project/Scripts/Workshop/WordboundWorkshopRoutes.cs:24`: safe anchors, checkpoints, weighted routes, laps and spawn editor.
- `Assets/_Project/Scripts/Workshop/WordboundWorkshopSites.cs:47` and `Assets/_Project/Scripts/Rules/Wordbound/WorkshopPhysicalSites.cs:51`: physical editing and creation requirements/defaults.
- `Assets/_Project/Scripts/Rules/Wordbound/CourseDocument.cs:39`: supported schema fields; limits at 168. Schema has no custom match-rule/scoring/skin/script payload.
- `Assets/_Project/Scripts/Rules/Wordbound/CourseValidation.cs:96`: single-player-solvable puzzle requirement.
- `Assets/_Project/Scripts/Rules/Wordbound/ModeDefinition.cs:81`: authoritative fixed mode/team/time/score definitions.
- `Assets/_Project/Scripts/Rules/Wordbound/WorkshopStorage.cs:60` and `Assets/_Project/Scripts/Rules/Wordbound/WorkshopDocument.cs:8`: course-only sharing versus richer recovery state and default seed.
- `Assets/_Project/Scripts/Online/Client/WordboundWorkshopOnlineContent.cs:9`: exact draft/seed/mode validation before private online creation.
- `Assets/_Project/Scripts/Online/Client/WordboundPartyCoordinator.cs:222` and 272: installed content requirement and rejection of public Quick Play for unapproved authored course.
- `Assets/_Project/Scripts/Online/Client/Tests/WordboundWorkshopPartyTests.cs:77`: existing source test coverage for private transfer, content binding and public exclusion; these tests were not run by this audit.
- `Assets/_Project/Scripts/UI/CreativeWorkshop.cs:139` and `Assets/_Project/Scripts/Rules/HomeDesign.cs:97`: separate house furniture/finishes/tour system and bounds.

## Recommended next creator improvement: guided Add challenge

**Player outcome:** choose a challenge and an item, choose an existing checkpoint/lap, then place its controls. A new creator can produce a playable challenge without typing internal IDs or discovering three separate inspectors.

**Bounded scope:** an objective composer over the existing CoursePuzzle, CourseSupply, CourseCheckpoint and CoursePhysicalSite data. No schema, networking, scoring or public-registry extension is necessary. All generated IDs remain internal. Reuse existing commands and the physical-site factory; commit all linked objects in one `WorkshopHistory.Execute` transaction so failure leaves no half-created objective and one Undo removes the complete addition.

Suggested simple flow:

1. Select a room and choose `Add challenge`.
2. Show the six family choices with small example imagery and a short label. Present only compatible approved recipe choices using original icons. Compatibility must use existing physical validation/behavior, not just an asset-name list.
3. Pick an existing checkpoint on a lap that traverses this room. Show the lap number and checkpoint location, not an editable ID. If none exists, show one concise `Add a route stop first` action that opens the existing route editor. Do not silently alter route topology.
4. Confirm Add. Create puzzle + protected renewable letter supply, add the checkpoint reference, and create the physical site(s) for the selected valid lap(s). The chosen checkpoint must be respected explicitly; the factory currently chooses the first matching checkpoint, so the composer must bind/validate the requested one rather than depend on enumeration order.
5. Select and frame the new socket. Keep `Move`, `Test`, and `More` as immediate actions; advanced numeric fields remain available in the existing inspector.

**Why this comes first:** the map editor already exposes many systems, but creation still has a gap between a logical puzzle and a playable one. Closing that gap directly serves the owner's easy select/drag/no-code request and produces real gameplay variety with current runtime behavior. Adding custom victory rules now would require a new versioned rule identity and online admission work rather than just UI.

**Acceptance:** all six supported families; recipe filtering; correct chosen checkpoint/lap; invalid room/locked floor/absent route/unsupported recipe rejected with no partial mutation; all references resolve; Preview/Playtest no longer fails because controls were forgotten; one Undo/Redo round trip; course export/import preserves generated challenge; private bundle preserves exact map; keyboard/mouse/controller focus; 854x480 with maximum text; new creator can complete a short challenge without typing an ID. Then a real Windows playtest must prove each family progresses the intended checkpoint and does not softlock after item loss.

## Plan implications

Keep `player-created maps` as implemented-but-under-acceptance, and `player-created gameplay` as partial: authored puzzle courses within fixed modes. Public creator distribution, arbitrary rules/scoring, cosmetic authoring and narrative endings remain separate unchecked scope. Preserve these distinctions in the master progress report instead of calling the entire creator requirement complete.


## Determinism and compatibility of the recommendation

The guided composer changes existing document data, so the current course JSON hash, seed resolution, physical-site validation and private bundle identity already cover its result. It must generate stable unique IDs, validate references and selected-lap membership, preserve recovery guarantees, and resolve the same draft/seed identically on every peer. Reusing the schema does not remove the need to reject a challenge that cannot physically fit or cannot be completed with its approved equipment. Changes to recipe compatibility must be shared with the runtime validator, not enforced only in the visual picker.

A configurable mode-rule schema is a larger subsequent feature. Current ModeDefinition hashes fixed victory, capacity, timing, damage, ownership and recipe data; mode/format determine roster and course requirements, and authored content transfer requires identical installed item/rule files. Exposing a score/timer/team slider without extending that contract would display settings that the authoritative runtime cannot honor. A correct implementation needs a bounded versioned rules object, strict import validation, content and protocol identity updates, deterministic state/result logic, roster and bot constraints, private sharing compatibility, and a deliberate public admission policy. Public matchmaking for official modes must remain distinct from publishing arbitrary user rules. The composer is therefore the next practical gameplay-creation improvement; custom rules remain explicitly unfinished.

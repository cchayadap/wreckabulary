# Wreckabulary: Wordbound — development goal and implementation plan

## 1. Goal and verified starting point

**Build a substantial, replayable Wreckabulary expansion where players smash furniture, collect letters, spell equipment, and use it to solve physical puzzles across changing routes and destinations.**

The complete delivery includes:

- Five distinct play styles, with clear objectives.
- A large, multi-layer map with three evolving laps, recognizable destinations, and a cozy mystery.
- Online private sessions, public matchmaking, Solo/Duo/Squad formats, and automatic host recovery.
- An accessible in-game workshop for maps, puzzles, layers, and controlled randomness.
- Twelve equipment/furniture models developed through generated image concepts and editable Blender sources.
- Separate development branches that preserve existing work.

**Implementation authorized on 2026-10-20. James and SethyPagna are the same owner: use James in team-facing documentation and SethyPagna for Git identity. This document records the approved scope; STATUS.md records actual completion and evidence.**

### What the repositories actually contain

| Repository/branch | Verified position | Planning consequence |
|---|---|---|
| `cchayadap/main` — `4d5c162` | Older team baseline. | Do not start this expansion directly from it. |
| `cchayadap/james-v1` — `0140d96` | 68 commits ahead of upstream main; contains multi-storey houses and the Creative Workshop. | Reuse its current systems. The local extracted ZIP is obsolete. |
| `cchayadap/ICY` — `fb94c9a` | One commit beyond James, adding Escape and wardrobe colour fixes. | Preserve ICY’s ownership and integrate its changes deliberately. |
| `SethyPagna/main` — `f7c3c90` | Contains four additional interaction/control commits; history diverges from James through merge commits. | Use this maintained checkout’s committed revision as the development starting point. |
| `feature/unity-project` | Behind upstream main. | Reference only. |

The existing workshop supports furnishing, selection, movement, rotation, undo/redo, and JSON sharing. It does **not** yet author complete courses, puzzle logic, random zones, or arbitrary layers. Existing houses already support storeys and stairs. Online gameplay is new work. See the [branch comparison](https://github.com/cchayadap/wreckabulary/compare/james-v1...SethyPagna:main), [workshop implementation](https://github.com/cchayadap/wreckabulary/blob/0140d96d8219cb98bb73e2f9a469c82eb90ac79d/Assets/_Project/Scripts/UI/CreativeWorkshop.cs), and [ICY changes](https://github.com/cchayadap/wreckabulary/compare/james-v1...ICY).

## 2. Work directions and safe integration

### Preserve existing responsibilities

| Direction | Owner | Boundary |
|---|---|---|
| 1. Lobby, character, UI design | Existing assigned person | Retains presentation ownership; receives small integration requests. |
| 1.A Character colour fixes | ICY | Preserve boots, glasses, satchel, and black colour support. |
| 2.A PvP equipment/furniture/weapons | James | Owns equipment design; new asset production supports this direction. |
| 3.A Escape | ICY | Word doors, keys, guide, randomized house/start/item placement. |
| 3.B | Reserved | Do not assign overlapping Escape work without a separate brief. |
| 4.A Existing puzzle direction | James | Preserve its current scope. |
| **4.B Wordbound mode suite** | **James (SethyPagna)** | New race, traversal, narrative, and mode implementation. |
| **5.A Workshop and map generation** | New implementation lane | Shared authoring foundation; separate from James’s puzzle work. |
| **6.A Online services** | New implementation lane | Networking, parties, matchmaking, reconnect, migration. |

These are work packages, not an assumption that additional people have already joined the team. One person may deliver multiple packages sequentially.

### Branch and worktree arrangement

1. Create `sethypagna/wordbound-integration` from the verified `SethyPagna/main` commit in a **new worktree**.
2. Merge the reviewed `ICY` revision there; it already includes James’s history. Resolve shared input, HUD, rules, and lobby changes while retaining Sethy’s later controls.
3. Establish a green baseline before branching feature work.
4. Use separate worktrees for:
   - `sethypagna/map-core`
   - `sethypagna/puzzle-race`
   - `sethypagna/map-workshop`
   - `sethypagna/online`
   - `sethypagna/pvp-assets`
5. Develop on the Sethy fork. Use a separate upstream `sethypagna/wordbound-integration` branch for team review and integration; keep existing `main`, `james-v1`, and `ICY` branches outside routine feature writes.
6. One integrator owns shared catalogs, package manifests, scene registration, player integration, and lobby hooks. Feature lanes own their new components and assets.

Preserve the current checkout’s modified font and unfinished Lunar work. Do not rebuild existing authored scenes or reuse the extracted James folder as a development checkout.

**Identity:** Git and GitHub are already configured as `SethyPagna`. Retain that identity for new work, preserve other contributors’ existing authorship, and avoid assistant coauthor trailers. Use concise titles such as:

- `Add puzzle race checkpoints`
- `Add map random zones`
- `Support party fill matchmaking`
- `Add workshop drag controls`

Record each lane’s base revision, owned scope, verification evidence, and next action. Commit after verified steps.

## 3. Gameplay, progression, and mode rules

### Wreckabulary’s defining loop

**Explore → smash → collect letters → spell a useful object → solve or disrupt → reach the next destination.**

Progress must come from interacting with words and objects. Reuse BED/STOOL traversal, FAN wind, CLOCK slowing, TABLE/SOFA placement, and equipment handling before adding new powers.

### Five explicit mode cards

| Mode | Players | Objective and result | Combat |
|---|---:|---|---|
| **Time Run** | 1–4 cooperating | Deliver the shared keepsake through three laps before the 25-minute timer expires. | Environmental hazards; friendly damage off. |
| **Team Race** | Two teams; Solo/Duo/Squad | First team to deliver its keepsake after three valid laps wins. | Rival teams cannot damage, block, steal from, or displace each other’s puzzle objects. |
| **Brawl Race** | Two teams; Solo/Duo/Squad | Same race objective, with equipment-based disruption on a shared course. | Enemy combat enabled; friendly damage off. |
| **Free-for-All** | 2–8 individuals | Combat contest: first to 10 knockouts, or highest score after eight minutes. Equal scores at timeout produce a draw. | Individual combat with respawns. |
| **Freestyle** | 1–8 | Explore, experiment, practice puzzles, and play with objects. No winner or timer. | Damage off. |

Free-for-All is a combat mode, distinct from an individual racing variant. Under the owner's 2026-10-21 update, Freestyle supports public and private human sessions; it does not open the map editor during online play. Public cooperative sessions require at least two humans and never silently substitute AI or solo play.

Public race matches have a 25-minute cap. At timeout, rank teams by completed ordered objectives, then by the earliest authoritative tick at which that progress was achieved; an exact tie is a draw.

### Legal laps and recovery

- Each lap has an ordered checkpoint sequence and required puzzle completions.
- A team shares one keepsake and puzzle progress. Teammates can split tasks.
- A lap completes when the keepsake carrier delivers it after all required checkpoints. Everyone need not stand at the finish.
- Passing a checkpoint without completing its puzzle cannot advance progress.
- Keepsakes cannot be destroyed or permanently stolen. An inaccessible or abandoned keepsake returns to its last valid checkpoint after ten seconds.
- Falling or being knocked out returns a player to the last safe checkpoint. Recovery must not duplicate inventory or refund already completed actions.
- Required resources have deterministic recovery supplies so waste, theft, falls, and disconnects cannot make completion impossible.
- Brawl Race cannot erase completed checkpoints. Start with a one-second maximum control-disabling hit and two seconds of protection after recovery; tune through playtesting.

### Large-map content target

Build **one complete flagship map**, with:

- Four destinations: **Packing Yard, Kitchen Works, Attic Stacks, Rooftop Garden**.
- Four traversable height layers, visible landmarks, shortcuts, and return routes.
- Approximately 600–900 metres of meaningful route per lap as an initial greybox target.
- At least 24 reusable course modules and six puzzle families.
- A three-lap session target of roughly 18–25 minutes, verified through playtesting.

Six initial puzzle families:

1. Smash selected furniture to obtain the required letter combination.
2. Spell and place an object into a physical socket.
3. Build a traversal route using furniture and spring objects.
4. Redirect wind to move objects or operate a mechanism.
5. Balance weights or hold switches using objects.
6. Coordinate timed mechanisms while transporting the keepsake.

Solo must always have a valid solution. Additional teammates provide coordination options; they must not be mandatory for a Solo-format puzzle.

### Cozy mystery across laps

- **Lap 1 — Find the address:** learn the destinations and discover that room labels are misplaced.
- **Lap 2 — Follow the wrong delivery:** routes change and reveal that the Movers are responding to a malfunctioning toy-house sorting system.
- **Lap 3 — Put home back together:** restore the final address and deliver the keepsake to the rooftop mechanism.

Use environmental clues, animations, short optional dialogue, and visible changes. Competitive play must not stop for mandatory cutscenes.

Keep landmarks recognizable across laps. Generate all route variants before the match; use different branches, puzzle states, supplies, and story events without rebuilding occupied terrain. Teams on different laps must retain independent progress.

## 4. Map generation and player workshop

### Extend the existing systems

Retain current multi-storey geometry and compatible home saves. Add a separate versioned course document rather than changing the meaning of schema-1 furnishing saves.

The new map contract contains:

| Element | Authoring capability |
|---|---|
| Modules and layers | Place rooms, platforms, stairs, ramps, bridges, start/finish areas, and destination markers. |
| Connections | Link valid entrances, exits, and vertical transitions. |
| Puzzle graph | Connect triggers, conditions, objectives, and actions using preset nodes. |
| Spawn zones | Draw a volume and choose eligible objects, count range, probability, spacing, rotation, and height/layer limits. |
| Route variants | Specify fixed sections and eligible alternatives for each lap. |
| Safety constraints | Reserve spawn space, paths, checkpoints, required letters, and recovery locations. |
| Presentation | Choose approved themes, lighting presets, sound zones, landmarks, and narrative events. |

### Randomness rules

- Use a versioned deterministic generator with separate random streams for routes, props, resources, and decoration.
- The host generates once and distributes the **resolved map manifest**, not just a seed.
- Equivalent clean-race teams receive equal route difficulty and resource opportunities.
- Validate geometry, connections, resource dependencies, and recovery paths before admitting players.
- Retry invalid candidates within a bounded attempt limit. Shipped maps may use a validated fallback; workshop drafts display the failing constraint.
- Never silently relax a creator’s required constraints.
- Validate mixed door/stair routes explicitly before reusing ICY’s path-reconstruction logic.
- Clean Race isolates puzzle state, resources, movable puzzle props, and collisions between teams. Brawl Race shares combat interactions while protecting mandatory progression resources.

### Workshop interaction design

Use the existing workshop as the foundation, with four visible areas:

- **Left:** searchable object/module palette with thumbnails.
- **Centre:** editable world.
- **Right:** compact properties for the current selection.
- **Bottom:** layer selector, undo/redo, preview, and playtest controls.

Primary actions:

- Select, drag, move, rotate, duplicate, delete, multi-select, and group.
- Grid snapping and surface snapping; numeric entry in an expandable advanced panel.
- Layer visibility, locking, and ghosted adjacent floors.
- Directly drawn random-placement volumes with resize handles.
- A simple randomness selector: **Fixed / Within zone / Choose from pool**.
- Preset puzzle links, such as **Place object → Open gate**.
- Seed lock, reroll preview, and visible validation markers.
- Playtest the exact draft and seed, then return with editing history intact.
- Autosave, recovery, and JSON import/export.

Use short labels and tooltips rather than paragraphs. Icons still need accessible names and controller focus states.

Ship three starting templates: **Race Circuit, Combat Arena, and Freestyle World**. Workshop editing is offline; exported maps can be played online in private/custom sessions. Public Quick Play initially uses the tested built-in map pool.

Map files contain approved asset IDs and bounded data—not scripts, arbitrary downloads, or embedded executable content.

## 5. Online architecture and intuitive party logic

### Player-facing flow

**Mode → Solo/Duo/Squad where applicable → Fill switch → Play.**

Keep party portraits, invite/code buttons, readiness, and one prominent Play/Cancel button visible. Use a separate **Private** action for code sessions.

| Format | Team capacity | Maximum players |
|---|---:|---:|
| Solo race | 1 | 2 |
| Duo race | 2 | 4 |
| Squad race | 4 | 8 |
| Time Run | One team of 4 | 4 |

**Fill On:** retain the party together and add consenting human teammates up to capacity.

**Fill Off:** retain only the current party; unused teammate slots remain empty. No AI substitution or silent change of format.

Further rules:

- Never split a party between teams.
- An oversized party cannot queue for a smaller format.
- Objective difficulty remains fixed within the selected format. Fill Off means choosing an undersized team, shown by the occupied/empty portraits.
- Changing roster, format, or Fill cancels the current queue before requeueing.
- No new random teammates after the match begins; reconnecting members reclaim their slots.
- Private sessions default to Fill Off. Turning it on explicitly allows random teammates while the session remains code-based.
- FFA uses individual matchmaking. Public Freestyle uses one cooperative group, with whole-party Fill and a two-human minimum; its private sessions may still be solo.
- When population is insufficient, show elapsed queue time and Cancel. Do not silently add bots or change the rules.

### Technical choice

Use **Photon Fusion Host Mode with the matching Physics Addon**, plus a small separate Cloudflare service for parties and matchmaking allocation.

Fusion provides the relevant physics and host-migration foundation, but migration requires restoring a stored snapshot. It is not an automatic guarantee of uninterrupted play. Verify SDK compatibility with the existing Unity `6000.6.3f1` editor before adopting the package; do not silently change the engine version. See [Fusion SDK requirements](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download), [physics support](https://doc.photonengine.com/fusion/v2/addons/physics-addon-2.1), and [host migration](https://doc.photonengine.com/fusion/v2/manual/advanced/host-migration).

Initial package candidates are Fusion `2.1.3` and Physics Addon `2.1.2`, subject to the compatibility gate.

The separate service uses authenticated guest identities, party membership, queue tickets, and transactional team reservations. Keep it separate from the current website and score API. Durable Objects provide the transactional storage needed to allocate whole parties without oversubscribing a team. [Cloudflare storage documentation](https://developers.cloudflare.com/durable-objects/api/sqlite-storage-api/)

### Required interfaces and authority boundaries

| Contract | Responsibility |
|---|---|
| `CourseDocument` | Editable geometry, layers, constraints, puzzle links, and supported modes. |
| `ResolvedCourse` | Exact placements and three-lap routes, with seed, versions, and content hash. |
| `ModeDefinition` | Team capacity, objective, scoring, combat policy, timer, and permitted recipes. |
| `PartyTicket` / `AdmissionGrant` | Locked party revision, Fill policy, assigned team, expiry, and authenticated player identity. |
| `MatchSnapshot` | Players, objects, inventories, puzzle/lap state, keepsakes, timers, narrative flags, and migration sequence. |

The simulation host owns pickups, crafting, inventories, damage, puzzle completion, and results. Clients send input requests. Existing local inventory mutations must be adapted into one authoritative path for the new modes.

Eight-player support requires distinct spawns, player IDs, roster handling, nameplates, appearance replication, and per-client cameras. It is more than changing the existing four-player limit.

### Host recovery

Accepted experience: **automatic continuation of the same match after a brief visible pause**.

- Capture consistent snapshots periodically and at important objectives.
- Include stable entity IDs, carried-object relationships, pending transactions, physics state, and platform timing.
- Freeze the race clock during migration.
- Restore one complete state; never combine newer inventories with older world objects.
- Target recovery within ten seconds of detecting host loss under the acceptance network conditions.
- Target no more than two seconds of provisional-state rewind; verify snapshot freshness rather than assuming the interval guarantees it.
- Do not confirm final results until the containing snapshot is acknowledged.
- Allow disconnected players a 120-second reconnect window.
- If automatic recovery fails, provide an explicit recovery screen and the last valid checkpoint; never silently restart or award results twice.

Initial public play is unranked. Browser parity, mixed couch-plus-online parties, online collaborative editing, and persistent global rankings are outside this release.

## 6. Equipment, furniture, and image-to-model production

Use **generated image concepts → editable Blender meshes → Unity validation**.

First batch:

| Role | Assets | Design purpose |
|---|---|---|
| Melee | BAT, BROOM, HAMMER, SPEAR | Distinct reach, weight, attack silhouettes, and readable wind-ups. |
| Defense | SHIELD, PLATE | Clear block direction, coverage, and impact feedback. |
| Obstacles | TABLE, SOFA | Recognizable cover, throwing silhouettes, and placement boundaries. |
| Traversal | BED, STOOL | Obvious spring/step affordances. |
| Control | FAN, CLOCK | Readable direction and affected area. |

Develop ROPE and MAGNET as later concepts; do not present currently disabled entries as working mechanics.

For every asset:

1. Produce silhouette alternatives and front/side/three-quarter concept views.
2. Match the rounded toy-house style and preserve its word identity.
3. Build and retain the editable Blender source.
4. Export the Unity mesh with correct scale, floor origin, and grip marker.
5. Supply materials, icon, previews, dimensions, provenance, and validation records.
6. Verify held, placed, thrown, used, and destroyed states in gameplay.

Use existing import conventions: metres, Unity-compatible orientation, `Grip_R`, and gameplay-owned colliders. Start with 2–6k triangles, up to two material slots, and the project’s current 512-pixel texture cap. Treat these as initial budgets to validate visually. The owner's 2026-10-21 update requires meticulous, believable construction and material detail, inspected close-up and in gameplay while preserving editable sources and measured performance.

Art production stages its own sources. The integrator updates shared catalogs and import tests. Cosmetic variants retain identical gameplay dimensions and statistics.

## 7. Delivery milestones and acceptance

| Milestone | Deliverable | Exit condition |
|---|---|---|
| **M0 — Safe baseline** | Isolated integration branch, reconciled histories, ownership map, regression baseline. | Existing modes and saves work; protected working copies remain unchanged. |
| **M1 — Online proof** | Small course with jumping, carrying, crafting, a moving platform, parties, and migration. | One host plus seven clients pass interaction and forced-host-loss tests on the selected SDK/editor. |
| **M2 — Complete race loop** | Shared keepsake, ordered checkpoints, three laps, recovery, Time Run and Team Race. | Full playable matches without duplication, skipped objectives, or permanent softlocks. |
| **M3 — Large world and generator** | Four destinations, four layers, route variants, puzzle families, story beats. | Full-map playthrough and seed/resource validation pass. |
| **M4 — Workshop and assets** | Complete authoring flow, three templates, twelve integrated assets. | A new creator can build, randomize, export, reload, and play a course. |
| **M5 — Mode suite and public play** | Brawl Race, FFA, Freestyle, public queues, final party UI. | Each mode follows its own rules; Fill and party admission pass concurrency tests. |
| **M6 — Release candidate** | Packaged Windows build, evidence, documentation, upstream review. | Regression, performance, multiplayer, and usability acceptance pass. |

Concept art can proceed alongside early technical work. Full map production follows the networking proof.

### Required verification

- **Generation:** 1,000 seeds per shipped course configuration; reproducible manifests, valid stairs/layers, resource availability, no circular requirements, and a valid route for every supported format.
- **Race rules:** teams on different laps, repeated checkpoint triggers, lost keepsakes, inventory overflow, falls, intentional resource waste, and simultaneous finishes.
- **Matchmaking:** concurrent requests for the last slots, Fill Off undersized parties, oversized parties, expired grants, cancellation, and failed admission of one party member.
- **Networking:** 2/4/8 clients at 0/100/150 ms RTT with packet-loss testing; simultaneous pickups, crafting, grabbing, platform boarding, reconnect, and repeated host migration.
- **Migration:** force host loss during pickup, craft completion, lap transition, and final delivery; verify no duplicated items, objectives, or rewards.
- **Workshop:** drag/undo/redo, layer changes, invalid ranges, autosave recovery, import/export round trips, old home saves, and playtest-return fidelity.
- **Regression:** existing lobby, wardrobe colours, Escape, controls, local multiplayer, and furnishing saves.
- **Usability:** at least four of five new testers create and play a simple randomized course within ten minutes without developer help.
- **Performance:** profile the populated large map with eight players and active equipment. Use the inspected Core Ultra 7/Intel Arc PC as an initial reference, targeting 60 fps at 1080p Low; record actual results before claiming the target is met.

The project is complete when the packaged build delivers the mode suite, large evolving map, online party flow and recovery, usable workshop, and validated asset batch—not merely when those systems exist in source.

### Unity authoring and production update — 2026-10-21

Ordinary camera, equipment/presentation and course-design changes must be possible through saved Unity assets, Inspector fields and editor tools without editing code. Provide validated camera profiles; editable visual prefabs/materials/grips; course controls for modules, layers, spawns, puzzle parameters and random ranges; scene preview/handles and exact playtest/export actions. Preserve canonical gameplay dimensions, document validation and multiplayer content identity. Deliver an illustrated editing guide and verify edit/save/reload/playtest behavior. Track the complete remaining production acceptance in the repository-root `progress.md` using `[ ]`, `[~]` and `[x]`.

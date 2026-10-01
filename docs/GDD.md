# Wreckabulary: Game Design

**Wreck the room, build the word!**

Plush roommates turn a warm house into a workshop and a battlefield. A sofa breaks into S, O, F and A; those tiles become ingredients for something useful. The fun comes from deciding what to wreck, which letters to keep, and when to build, throw, protect or help.

This document describes the current production design and separates implemented systems from future creative scope. [The production plan](PLAN.md) sets delivery priorities; [the progress record](progress/WRECKABULARY.md) records actual validation.

## Pillars

1. **Exact wordcraft.** Every enabled recipe has a fixed letter cost, including repeated letters. The same object identity survives pickup, use, swapping, throwing and deployment.
2. **A house worth playing in.** Recognizable furniture, looping routes and a garden create both tactical choices and a playful home atmosphere.
3. **Readable physical comedy.** Soft silhouettes, wooden letter tiles and clear action feedback keep the player, loot and danger legible through the mess.
4. **Cooperation and rivalry.** The same collection and handling skills support a brawl, a furnishing task and an escape.

## Shared rules

The canonical values live in `Assets/_Project/Data/Config/rules.json` and `items.json`.

| Rule | Current design |
| --- | --- |
| Health | 100 HP; letters are separate loot |
| Loose bag | 18 letters, including the letters reserved by an active craft |
| Gear | Two carried slots and two deployed items per player |
| Starting bag | Empty under the standard rules |
| Crafting | Reserve the exact letters immediately; channel for 0.6 seconds + 0.12 seconds per letter at 40% move speed |
| Interruption | Cancel, dodge, stagger or knockout refunds the craft reservation; an invalid completion also refunds it |
| Elimination | Spill all loose/reserved letters and release carried gear |
| Reusable gear | Breaking returns its recipe once |
| Consumables | BOMB after arming, FOAM after use and SOAP after placement spend their letters; expiry returns none |
| Cosmetics | Appearance changes do not alter costs, damage, collision, timings or movement |

For example, a bag containing TABLE plus D can craft BLADE by spending B, L, A, D and E; the surplus T remains in the bag. No recipe invents or substitutes letters.

Punches, weapons, blasts and thrown impacts damage objects. Furniture should not break merely because it topples, and deliveries should not destroy each other on landing. Damage uses simple gameplay collision shapes rather than detailed moving mesh colliders.

Melee uses authored windup, active and recovery windows, one hit per target per swing, reach and facing arcs. PLATE blocks within its authored front arc, slows movement while raised, and wears down from blocked damage. Dodging has a cooldown and a brief invulnerability window; a landed stagger prevents actions without allowing repeated stun lock.

## Twelve enabled recipes

| Recipe | Role |
| --- | --- |
| BALL | Recoverable throw: 12 damage, fast travel and modest shove |
| BAT | Broad melee swing: 14 damage and strong knockback |
| BED | Reusable deployed jump pad |
| BLADE | Narrower, heavier melee hit: 24 damage |
| BOMB | Single-use 2.5-second fuse; 3-metre blast with damage falling from 45 at the centre to 15 at the edge |
| FOAM | Single-use 35-point protection bubble for up to 10 seconds |
| LAMP | Long, narrow melee thrust; also furniture when required by a Moving Day checklist |
| MAT | Directional deployed speed strip; boost continues briefly after leaving |
| PLATE | Durable frontal shield |
| SOAP | Single-use slippery patch with an 8-second lifetime |
| SOFA | Durable deployed cover |
| TABLE | Heavy melee swing or deployed cover |

The catalogue also contains disabled entries and the art pack contains additional household objects. Those objects can furnish a map or provide letters; they are not additional live recipes. Shared JSON does not make the Unity and JavaScript behavior identical automatically, so parity needs tests in both editions.

## The house and maps

The hub offers mode selection, map selection and cosmetic choices. Unity local roommates join through the front door; AI seats are created for a match and do not become persistent hub roommates.

**Pinwheel House** occupies a 20 × 20 metre footprint. An 8 × 8 metre playroom connects to Bedroom, Kitchen, Study and LivingRoom; eight doorways provide paths through the middle and around neighbouring rooms. A raised balcony and stairs provide vertical variation in the Unity blockout.

**Garden Courtyard** occupies a 32 × 32 metre footprint around a 12 × 12 metre garden. Four indoor rooms connect through the garden and around its perimeter. Wider doorways support carrying furniture. Both maps define spawn positions, furnishing checklists, keepsakes and extraction in shared JSON.

The large house is a connected route network, not a distant overview of tiny players. Camera follow, cutaway walls, open door clearance, reachable letters and safe spawn areas are acceptance requirements. Geometry sizes alone do not prove useful route times or phone readability; test the actual routes at the gameplay camera. Unity and browser camera/physics implementations need separate checks.

## Modes and complete local loops

**Dibs.** Everyone is hostile. The last roommate standing wins a round; first to three wins takes the match. Solo starts fill AI seats. The Movers warn before clearing outer rooms and damaging anyone who stays; the central playroom/garden remains available. An unresolved round at the time limit is a draw, not a highest-HP victory. Results lead to the next round, replay or home.

**Duos.** Two teams fight under friendly-fire rules with downed players and revival. Hold interact near a downed teammate for the authored revive duration. A team that has no possible rescuer is eliminated; simultaneous elimination can draw. Solo play supplies an AI teammate and opponents. Team state, timeouts and results must agree across editions.

**Moving Day.** Cooperate to furnish the selected map before time expires. Deliveries contain the exact checklist words. All twelve enabled recipes remain available. Only the map's explicit checklist IDs build plain furniture rather than filling a gear slot; other words craft their regular tools, so SOAP and MAT can still be used creatively. A settled item in its target room completes that objective and locks in place. LAMP remains an allowed checklist item despite being a utility in the combat catalogue. Cancelled tool crafts refund their exact letters. Supplies lost or spent on consumables can be replaced so the checklist cannot become unwinnable. Completion awards one to three stars based on time remaining; failure offers retry. Each current map supplies four placement objectives.

**Moving Out.** Carry marked, protected keepsakes to the van and release them inside its 2.4-metre extraction radius. Keepsakes require handling rather than destruction. Clear-out pressure continues as the team rescues them. Victory requires every keepsake and every remaining survivor at the van, with survivors alive rather than downed. Loss, retry and home are explicit states.

**Tutorial.** Unity guides movement, smashing a BAT delivery, collecting, crafting, attacking, throwing and knockout. The browser provides an open practice space with a dummy and progress prompts. This difference is intentional and must be presented accurately.

Unity's local input supports keyboard/mouse, gamepads and shared-keyboard seats. The browser currently has one human with AI housemates and keyboard/mouse or touch input. Neither edition currently provides online matchmaking or networked play.

## Characters, skins and feedback

Use the supplied modular avatar and its authored rig, wardrobe meshes and clips. Wardrobe selections hide unused modules, recolour approved material regions, and preserve head morphs. Item skins are Classic, Candy and Arcade. Held gear uses its holder's choice at miniature scale; a thrown crafted object keeps that look until it rests, then returns to full-size Classic in the room. Pickup changes appearance without changing recipe or durability.

Wood, ceramic, cloth and rubber should read as distinct surfaces under a restrained cream, sage and terracotta environment palette. Player accents, visible glyphs, shield states and danger shapes carry information in addition to colour. Impact, craft, shield, fuse and result cues should be brief and audible without concealing loose letters.

Touch controls need reachable movement/aim and action buttons, visible recipe costs, a clear craft channel/cancel path, and usable navigation while a typewriter freezes its user. Safe-area and portrait/landscape layout checks are separate from real-device responsiveness and performance.

## Critical art constraints

The [supplied-asset audit](art/SUPPLIED_ASSET_AUDIT.md) evaluates the actual meshes and materials. Use its evidence rather than the old primitive-only review or promotional illustrations.

The original avatar totals 63,110 triangles across 11 skinned modules, 22 bones and 17 clips. The default outfit uses 31,696 triangles, six skinned renderers and nine material slots. The accepted reduced browser export lowers its default outfit to 17,660 triangles while retaining the head and both morph targets. Interchange, exact animation data and visual comparison checks pass. Complete scene profiling and Unity deformation checks remain required before mobile readiness can be claimed.

Preserve the authored FBX root transforms, grip positions, material assignments and exact word-to-letter burst origins. Use simple physical proxies; do not enable the imported visual colliders alongside the gameplay collider. Test object and letter silhouettes at the actual camera, including the full loose-tile load. Concept images and raster button illustrations do not establish 3D topology, skinning or FPS.

## Future creative home mode

**Home Sweet Home** is planned. Its first useful slice is a local room editor with a curated prop palette, snap/rotate/place/remove controls, undo, and versioned save/load. A room must retain reachable doors, spawn clearance and recipe accounting when used as a playable arena. A larger connected house and garden should remain useful for exploration and cooperative play; combat layouts can bound the same themes for readability.

New recipes, extra floors, pets and custom competitive rules follow only after the existing four modes, touch navigation, art acceptance and performance targets pass. This planned scope is not a description of current playable features.

## Validation boundary

The exact Unity editor is **6000.6.3f1**. The cloud's Unity Personal activation currently fails with exit 198, so compilation against its official assemblies does not demonstrate import, EditMode/PlayMode execution, screenshots or successful builds. Browser mechanics and Chromium interaction tests supply separate evidence. Representative phones and PC builds still require direct testing before a platform or performance release claim.

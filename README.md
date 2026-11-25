# Wreckabulary

**Wreck the room, build the word.**

A toy-like third-person party game for 1–4 players. Smash furniture and it breaks into the letters of its name (a TABLE drops T, A, B, L, E). Spell a word to build it: a BAT swings, a SHIELD blocks, a BED springs. Then use it to win.

## How to play

1. Break furniture; pick up its letters.
2. Spell a word (Q, type, Enter) or tap a ready card in the recipe book (hold Tab).
3. Use what you built: swing, throw, block, place.

Harder words make better items. A plain word like BAT is an honest hit; harder words carry effects:

| Effect | How it lands | Examples |
|---|---|---|
| None | — | BAT, PAN, POT, BALL |
| Heavy | every hit pushes further | LAMP, BROOM, BARREL, BOOK |
| Daze | by chance, or on every 3rd hit | SPEAR, TOASTER, PILLOW, RACK, VASE, BOOK |
| Bleed | over time; repeated cuts deepen | ARROW, BLADE |
| Crush | every 3rd hit lands extra hard | HAMMER |
| Guard / Swift | constant while held | MIRROR, SHIELD / PLATE |

Bare hands barely hurt. Every recipe card shows its effects and how its damage and reach compare.

A BOW fires further and harder the longer you hold attack; a meter under the crosshair shows the draw. Place a CHEST and interact with it to bank your letters (or take them back with an empty bag); only your team can open it, and it spills everything if an enemy grabs or breaks it.

## Modes

| Family | Modes | Players |
|---|---|---|
| Learn | Tutorial (selected until you finish it) | 1–4 on one PC |
| Battle | Dibs! (last one standing), Duos (2v2 with revives), Free-for-All (respawns; first to 10 knockouts), War (two teams of up to 4 share 12 lives; hold the middle room for more) | 1–4 on one PC, bots fill seats (War plays 4v4) |
| Co-op | Moving Out (escape with the keepsakes), Moving Day (finish the checklist), Escape | 1–4 on one PC |
| Word games | Shopping List (everyone builds the same items in a new random order each round), Riddle Rush (solve the riddle, spell the answer first) | 1–4 on one PC, bots fill seats |
| Boss Fight | Spell the three parts of the Calculator's name to break its shield, then knock it out (Pinwheel House) | 1 on one PC |
| Courses | Time Trial, Freeplay on Parcel Depot, The Lost Address or Parcel Hall | 1 on one PC |
| Online courses | Time Trial, Team Relay, Brawl Relay (teams of 1, 2 or 4), Free-for-All (first to 10 knockouts), Freeplay | Matchmaking or a private party |

The ⓘ beside the selected mode, and **How to play** in the pause menu, show a picture guide for every mode.

Five houses (Pinwheel, Garden Courtyard, City Flat, Terrace, Walk-up) with gardens, yards and roofs, or **Random**; each map card shows a picture of the real map. The camera frames every player on the couch. **Parcel Depot** is a two-level warehouse whose laps take different routes.

Course races carry a keepsake through three laps of checkpoints; each checkpoint is a word puzzle. **The Lost Address** is the story course: four floors, three chapters (find the address, the wrong delivery, restore the home), clues earned at checkpoints and an ending for each result.

Create: **Map editor** (build a course in four steps: Build, Fill, Rules, Test & Share; save, open and share maps, or play one online with friends) and **Home builder** (furnish a house, then play it in any Battle or Word game mode).

## Online

House party modes are offline only. Course modes go online through Photon Fusion (host and clients):

1. Each player signs in as a guest; the coordinator service matches parties and signs the roster.
2. The first player on the roster hosts; everyone joins with a one-use Photon ticket the coordinator issued.
3. The host referees the match and saves checkpoints to the coordinator; if the host leaves, another player takes over from the last checkpoint.

Every player must have identical item and rule files or the match does not start. Shared maps are checked for size, format and fingerprints before loading. House items never reach online play. Sign-in and two-player sessions have passed live; a full public match has not been verified end to end.

## Controls

| Action | Keyboard and mouse |
|---|---|
| Move / look | WASD / mouse |
| Look around the room | Hold Left Ctrl and move the mouse, or drag the eye button |
| Attack, throw, place, use; hold with a shield to block | Left click |
| Aim | Hold right click |
| Pick up, interact / revive | E / hold E |
| Spell | Q |
| Bag, map, recipe book | Hold Tab |
| Switch hand / drop | 1, 2 / hold R |
| Jump / dodge | Space / Left Shift |
| Menu | Esc |

Every binding is in Settings → Controls. Gamepads (hold L3 and move the right stick to look around), keyboard halves and touch work too. On touch screens, Settings → Gameplay → Touch layout moves, resizes and fades every button: start from Classic or Claw, keep three layouts of your own and share them as codes.

Settings → Gameplay turns damage numbers, effect words, furniture names (off by default, always on in the Tutorial), control tips and screen shake on or off.

## Unity project

Unity **6000.6.4f1** (URP). Run `git lfs install && git lfs pull`, open the folder in Unity Hub, choose **Wreckabulary → Set Up Art and Data**, then open `Assets/_Project/Scenes/Hub.unity` and press Play.

| Scene | Purpose |
|---|---|
| `Hub` | Lobby: play, create, shop, settings |
| `LivingRoom` | House party matches |
| `MovingDay` | Moving Day |
| `Tutorial` | Guided first match |
| `WordboundLobby` / `WordboundGameplay` | Courses: party, matchmaking, play |
| `Authoring/`, `Testing/` | Editing and test scenes |

| Folder (`Assets/_Project/`) | Holds |
|---|---|
| `Data/Config` | Rules, items, houses (JSON) |
| `Scripts` | Game code by area: Core, Game, Player, Letters, Rules, UI, Online, Workshop |
| `Prefabs`, `Worlds`, `Resources` | Characters, items, houses |
| `Art`, `Audio`, `Materials`, `Shaders`, `UI` | Assets |
| `Editor` | Setup and authoring tools |
| `Tests` | Rules, EditMode and PlayMode tests |

Checks run through `node Tools/kit/gate/game_gate.mjs` (`compile`, `core`, `test editmode|playmode`, `build`, `smoke`).

A browser prototype lives in `Web` (`npm ci && npm run build`).

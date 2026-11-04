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
| Heavy | every hit pushes further | LAMP, BROOM, BARREL |
| Daze | by chance, or on every 3rd hit | SPEAR, TOASTER, PILLOW, RACK |
| Bleed | over time; repeated cuts deepen | ARROW, BLADE |
| Crush | every 3rd hit lands extra hard | HAMMER |
| Guard / Swift | constant while held | MIRROR, SHIELD / PLATE |

Bare hands barely hurt. The recipe book says what each item does when you point at it.

## Modes

| Family | Modes | Players |
|---|---|---|
| Learn | Tutorial (selected until you finish it) | 1–4 on one PC |
| House party | Dibs! (last one standing), Duos (2v2), Moving Out (escape with the keepsakes), Moving Day (finish the checklist), Escape | 1–4 on one PC, bots fill seats |
| Courses | Time Trial, Freeplay on Parcel Depot, The Lost Address or Parcel Hall | 1 on one PC |
| Online courses | Time Trial, Team Relay, Brawl Relay (teams of 1, 2 or 4), Free-for-All (first to 10 knockouts), Freeplay | Matchmaking or a private party |

Five houses (Pinwheel, Garden Courtyard, City Flat, Terrace, Walk-up) with gardens, yards and roofs; the camera frames every player on the couch. **Parcel Depot** is a two-level warehouse whose laps take different routes.

Course races carry a keepsake through three laps of checkpoints; each checkpoint is a word puzzle. **The Lost Address** is the story course: four floors, three chapters (find the address, the wrong delivery, restore the home), clues earned at checkpoints and an ending for each result.

Create: **Map editor** (build a course in four steps: Build, Fill, Rules, Test & Share; save, open and share maps, or play one online with friends) and **Home builder** (furnish a house, then play it in Dibs! or Duos).

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
| Attack, throw, place, use; hold with a shield to block | Left click |
| Aim | Hold right click |
| Pick up, interact / revive | E / hold E |
| Spell | Q |
| Bag, map, recipe book | Hold Tab |
| Switch hand / drop | 1, 2 / hold R |
| Jump / dodge | Space / Left Shift |
| Menu | Esc |

Every binding is in Settings → Controls. Gamepads, keyboard halves and touch work too.

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

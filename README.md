# Wreckabulary

**Wreck the room, build the word!**

Wreckabulary is a toy-like third-person party game for 1–4 players. Plush roommates smash household furniture, and every piece breaks into the wooden letters of its name: a TABLE drops T, A, B, L and E. Collect letters in a small bag, spell a word to build gear (a BAT swings, a PLATE blocks, a BED springs), then use it to wreck the house, fight each other, or work together before the Movers pack the house away.

**Core loop:** explore → break → collect letters → spell and build → equip → fight or cooperate → survive.

## What's in the game

- **Modes:** Dibs! (free-for-all, first to three rounds), Duos (2v2 with downed and revive), Moving Out (co-op escape with keepsakes), Moving Day (co-op furnishing checklist), a guided tutorial, and the Creative Workshop for furnishing a house.
- **Houses:** Pinwheel House, Garden Courtyard, City Flat, the two-storey Terrace House and the three-storey Walk-up Apartments.
- **Rules:** 100 HP, a 10-letter bag, 2 carried and 2 deployed items, and 12 recipes: BALL, BAT, BED, BLADE, BOMB, FOAM, LAMP, MAT, PLATE, SOAP, SOFA and TABLE.
- **Play:** local play for up to four (keyboard and mouse, gamepads, two keyboard halves), with AI roommates filling empty seats.
- **Lobby:** a PC lobby with loadout, career, cosmetic shop, leaderboard and graphics settings. Cosmetics never change a number.

## Editions

| Edition | Folder | Status |
|---|---|---|
| Unity 6 (main edition, PC first) | repository root | Active development |
| Browser edition (Three.js) | `Web` | Playable prototype for one player with AI housemates |

Both editions read the same JSON rules, recipes, maps and wardrobe from `Assets/_Project/Data/Config`.

## Run the Unity edition

1. Install **Unity 6000.6.3f1** (URP) through Unity Hub.
2. Fetch the art:
   ```sh
   git lfs install
   git lfs pull
   ```
3. Add the checkout to Unity Hub and open it.
4. Choose **Wreckabulary → Set Up Art and Data**.
5. Open `Assets/_Project/Scenes/Hub.unity` and press Play.

In the lobby, PLAY picks a queue, mode and house, and GO starts the match. Couch players join the party with Start on a controller or `.` on the keyboard's right half.

## Run the browser edition

Needs Node.js 22.12 or newer.

```sh
cd Web
npm ci
npm test
npm run build
npm run preview -- --port 4173
```

Then open `http://localhost:4173`.

## Controls (keyboard and mouse)

| Action | Key |
|---|---|
| Move / look | WASD / mouse |
| Attack, throw, place, use; hold to block with a PLATE | Left click |
| Aim (closer shoulder view) | Hold right click |
| Pick up / revive | E / hold E |
| Spell | Q, type the word, Enter. Tab, right click or Esc closes. |
| Bag, map and recipe book | Hold Tab |
| Choose a hand / drop | 1, 2 / hold R |
| Jump / dodge | Space / Left Shift |
| Pause | Esc |

Every key can be changed in Settings → Controls, along with mouse sensitivity and invert Y. Gamepads, keyboard halves and touch screens are supported too.

## Project report

[PROJECT_REPORT.md](PROJECT_REPORT.md) is the master plan: design, rules, modes, maps, controls, art, architecture, tools, assets and credits, roadmap and open decisions.

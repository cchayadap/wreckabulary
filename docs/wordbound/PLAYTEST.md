# Wordbound playtest guide

Windows delivery is being verified. The build path will be recorded here after the actual player passes its launch checks.

## Try the map locally

1. Open **Wordbound** from the main lobby, then **Workshop**.
2. Start with **Flagship draft**. Choose **Time Run** for puzzle progression or **Freestyle** for exploration.
3. Use **Preview** to resolve the current map and seed. **Playtest** launches that exact preview.
4. Press **Esc** during play to open the equipment menu. Choose **Return to workshop** to continue editing the same draft.
5. Undo and redo history is retained across the playtest.

The local workshop playtest works without signing in to Photon.

## Default keyboard and mouse controls

| Action | Control |
| --- | --- |
| Move / look | WASD / mouse |
| Jump | Space |
| Collect a letter, pick up an object or use a placed puzzle item | E |
| Open the spelling menu | Q |
| Attack / smash furniture | Left mouse |
| Throw selected equipment | Hold right mouse and press left mouse |
| Select equipment | 1 / 2, or its slot in the equipment menu |
| Cycle occupied equipment slots and fists | Tab |
| Raise a selected shield or plate | Hold left mouse; face the incoming attack |
| Put down an item | Hold R, or use the menu |
| Open or close the equipment menu | Esc |
| Place selected equipment | **Place selected item** in the menu |

These are the default bindings. The original lobby's keyboard settings can change them. Wordbound does not currently dispatch a dodge action.

## Controller controls

| Action | Control |
| --- | --- |
| Move / look | Left stick / right stick |
| Jump | A |
| Interact / collect / pick up | RT |
| Spell equipment | Y |
| Attack / hold raised shield or plate | X / hold X |
| Throw selected equipment | Hold RB and press X |
| Block | Hold LT |
| Drop equipment | Hold LB |
| Cycle occupied equipment slots and fists | Right stick click |
| Open or close equipment menu | Start / Menu |
| Menu selection / confirm / back | D-pad or left stick / A / B |
| Scroll menu | Right stick |

The party screen also uses D-pad/left stick, A and B. The default guest name works with a controller; editing names or typing invite text still uses a keyboard. **Paste invite** and **Paste room invite** can use an already copied invite. After focus, device or menu changes, release held controls before the next action.

## Play with friends

1. Open **Wordbound**, enter a guest name or keep the default, and choose **Connect**.
2. Choose the same mode, region and map settings, then **Create party**. Copy the party invite for friends, or use **Join party** with an invite you received.
3. Each player chooses **Ready**. The leader chooses **Quick Play** for an approved public map, or **Create private room** and **Start private match** for an invited session.
4. To use an edited map, leave the current party, open **Workshop**, choose a supported mode/team size and select **Host online**. This validates and shares the exact draft and resolved map with a new private party. Invite friends, ready up and create/start the private room there.
5. Use the gameplay menu's **Return to party** before leaving or changing the party. An uncertain request exposes **Retry same action**, which reuses the pending request rather than creating another room.

Private edited maps do not become approved public maps. Public matchmaking activation and the new controller routes still need the final assembled native tests and Windows build acceptance; this guide is not evidence those checks passed.

Follow the current objective and its world markers. Craft the named object from collected letters, place it at the marked controls, then use it. Carry the keepsake through the ordered checkpoints and lap finish.

## Edit a map

- Select a module, then drag it. Shift adds to the selection. The inspector also has numeric position, rotation, duplicate, grouping and floor controls.
- Right-drag orbits, middle-drag pans, the wheel zooms, and F frames the selection.
- Choose fixed placement or a random zone to set its position or bounds, allowed objects, counts, probability and spacing.
- Open **Routes & checkpoints** for safe return anchors, ordered checkpoints, weighted route branches, laps and spawn anchors.
- Select a puzzle's physical controls to move sockets, arrival regions, receivers, pads and fixtures.
- Use validation messages to locate missing links or invalid placements. Incomplete drafts can be edited and saved; they must validate before playtesting.
- **Share** exports the course JSON. A locked seed preserves the same generated preview.

Private online sessions are being verified separately. Public matchmaking is not enabled yet; its approved map assignment and service activation remain part of delivery preparation.

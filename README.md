# Wreckabulary

**Wreck the room, build the word!**

Wreckabulary is a local house brawler and word-crafting game. Smash household objects into their exact letters, collect a useful word, and build gear to fight or help your roommates. The production goal is a complete, playful home game with readable controls, objectives, results and replay.

The project contains a Unity edition and a standalone HTML/Three.js edition. Unity supports local roommates and solo matches with AI seats; the browser edition currently runs one human with AI housemates. Both use the same checked-in rule, recipe, wardrobe and map JSON. Their runtime implementations are separate.

## Current game

- **100 HP**, an **18-letter bag**, **two carried gear slots**, and **two deployed items per player**. Letters are loot; taking damage does not normally remove them. Elimination spills the bag.
- **12 enabled recipes:** BALL, BAT, BED, BLADE, BOMB, FOAM, LAMP, MAT, PLATE, SOAP, SOFA and TABLE. Other supplied models can furnish rooms without becoming enabled recipes.
- **Two connected maps:** Pinwheel House and Garden Courtyard. Four game modes plus a tutorial are selectable.
- Supplied furniture, letter meshes, an animated modular avatar, cosmetic wardrobe and Classic/Candy/Arcade item skins are integrated in source. Touch controls, keyboard/mouse controls, and Unity couch/gamepad bindings share the gameplay commands.

| Mode | Objective |
| --- | --- |
| Dibs | Last roommate standing wins the round; first to three round wins takes the match. Solo starts add AI opponents. |
| Duos | Two teams compete; hold interact beside a downed teammate to revive them. AI fills local seats. |
| Moving Day | Place the map's checklist furniture before time runs out; all twelve recipes also remain available for creative tool use. |
| Moving Out | Carry marked keepsakes to the van, then gather every surviving roommate there alive before the house clears out. |
| Tutorial | Guided Unity exercises and a browser practice space for smashing, collecting, crafting and handling gear. |

The creative room editor, custom room saves and online multiplayer are future work. The hub currently provides exploration, mode/map selection and cosmetic choices.

## Run the browser edition

Install Node.js 22.12 or newer and run from the checkout:

```sh
cd Web
npm ci
npm test
npm run build
npm run preview -- --port 4173
```

Open `http://localhost:4173`. For development, use `npm run dev` instead of preview. Build and dev synchronize the five canonical JSON files from `Assets/_Project/Data/Config` into `Web/public/data`; edit the canonical files rather than the copies. `Web/dist` is the production web output and must be served over HTTP. Dependencies and game art are bundled locally.

With that server still running, open another terminal in `Web`:

```sh
npm run test:browser
```

This runs real Chromium interactions and writes screenshots/report data to ignored `Web/playwright-results`. Set `CHROMIUM_PATH` if Chromium is not `/usr/bin/chromium`, or `WRECKABULARY_URL` for another server address. Mechanics tests, browser interactions and real-device performance are different checks.

## Open the Unity edition

Use **Unity 6000.6.3f1**, exactly as pinned in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt), with the URP project and matching platform modules.

```sh
git lfs install
git lfs pull
git lfs fsck
```

Add this checkout to Unity Hub and activate Unity Personal on the machine running the editor. After import, choose **Wreckabulary → Set Up Art and Data** to refresh generated material/model libraries, animation references and map data. Open `Assets/_Project/Scenes/Hub.unity`, press Play, join through the front-door UI or a join button, choose a map and mode, then start. The typewriter offers the same mode choices. Scene names such as `LivingRoom` select a runtime map; they are not separate content maps.

[Cloud setup instructions](Tools/CloudSetup/README.md) provide the pinned .NET installer and Unity verification script. With .NET 9 installed, the rules suite runs without Unity:

```sh
dotnet run --project Tools/RulesHarness
```

With an activated editor:

```sh
export UNITY_EDITOR_PATH=/path/to/6000.6.3f1/Editor/Unity
bash Tools/CloudSetup/verify-unity.sh
bash Tools/CloudSetup/verify-unity.sh --build
```

The verification script prepares data/art libraries and runs EditMode and PlayMode tests; `--build` also attempts Linux PC and Unity Web builds. Other build menu entries cover Windows and Android when their modules are installed. Run generation/build steps sequentially. **Wreckabulary → Rebuild Prototype** overwrites generated scenes and prefabs; preserve manual scene edits before using it.

## Controls

| Action | Unity desktop | Browser desktop |
| --- | --- | --- |
| Move / aim | WASD / mouse | WASD or arrows / mouse; movement follows the camera |
| Attack / use held gear | Left mouse button | Left mouse button or J |
| Block with PLATE | Hold right mouse button | Hold right mouse button or K |
| Interact / revive | E; hold beside a downed teammate | E; hold beside a downed teammate |
| Spell | Hold Q, choose with W/S or wheel, release to start crafting | Q or C opens recipes; select a card to start crafting |
| Swap gear | Tab | Tab, or 1/2 for a specific slot |
| Place a tool | F | F |
| Drop | Hold R briefly | R |
| Throw what's held | E; BALL/BOMB also throw when used | G |
| Jump / dodge | Space / Left Shift | Space / Shift |
| Start / return | Enter / Esc or HOME | On-screen start / Esc pauses; HOME returns |

Unity also supports gamepads and two keyboard halves; [ControlHints.cs](Assets/_Project/Scripts/Input/ControlHints.cs) and the on-screen hints show their bindings. On touch, use the movement/aim controls and labelled action buttons. Browser touch attacks use nearby-target auto-aim. Phone layout checks do not establish a tested Android or iOS build.

## Art and verification limits

The source pack includes **40 items, 26 letter tiles, 21 house modules, 10 VFX meshes and one modular avatar**. See the [critical supplied-asset audit](docs/art/SUPPLIED_ASSET_AUDIT.md), [contact sheet](docs/art/SUPPLIED_ITEMS_CONTACT_SHEET.png), and [asset pipeline](Tools/AssetPipeline/README.md).

The avatar contains 63,110 triangles across all wardrobe modules; its default outfit uses 31,696 triangles. The accepted reduced browser export uses 17,660 default-outfit triangles and passes interchange/animation checks. Unity deformation checks and device profiling remain open; those counts are not FPS results. Model import rotations, grip points, exact-letter destruction and simple gameplay colliders must survive visual changes. Generated concept images and UI raster art are separate from the supplied 3D meshes.

The cloud editor currently exits with **code 198 because Unity Personal is not activated there**. Source compilation against official Unity assemblies and engine-free rules tests do not establish Unity import, PlayMode behavior, captures or successful platform builds. Check [the progress record](docs/progress/WRECKABULARY.md) for the latest actual test counts, browser evidence and outstanding checks.

## Project and documents

| Location | Purpose |
| --- | --- |
| `Assets/_Project/Data/Config` | Canonical rules, enabled recipes, maps and wardrobe |
| `Assets/_Project/Scripts/Rules` | Engine-free health, inventory/economy, room and match contracts |
| `Assets/_Project/Scripts` | Unity movement, combat, modes, imported visuals, controls and UI |
| `Assets/_Project/Art/Imported` | Supplied art converted to Unity FBX/textures |
| `Assets/_Project/Tests` | Rules and Unity regression suites |
| `Web` | Standalone HTML edition, mechanics tests and Chromium checks |
| `Tools/AssetPipeline`, `Tools/CloudSetup` | Repeatable art conversion and environment verification |

- [Production plan](docs/PLAN.md) and [progress](docs/progress/WRECKABULARY.md)
- [Game design](docs/GDD.md) and [roadmap](docs/ROADMAP.md)
- [Team workflow](docs/CONTRIBUTING.md), [asset provenance](docs/ASSETS.md), and [contributions](docs/CONTRIBUTIONS.md)

Live recipes come from enabled entries in `items.json`; adding a CSV row does not enable a recipe. The legacy CSV parser remains available for explicit fixtures. Keep JSON data, rules tests and both runtime implementations consistent when changing the game.

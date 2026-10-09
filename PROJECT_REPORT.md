# Wreckabulary: Project Report

**Wreck the room, build the word!**

Wreckabulary is a toy-like third-person party game for 1–4 players. Plush roommates smash household furniture, and each piece breaks into the wooden letters of its name: a TABLE drops T, A, B, L and E. Players collect letters in a 10-letter bag and press Q to fuse them into gear: a BAT swings, a PLATE blocks, a BED springs. They use the gear to fight, cooperate or escape across houses that grow from small flats into many-floor apartments and famous buildings. It is built in Unity 6 for PC first, with a browser prototype and mobile planned. The camera is centred behind the player, there is one main click, and the UI and lobby are bright and readable.

## 1. Core rules

| Rule | Value |
|---|---|
| Health | 100 HP; healing comes only from items |
| Bag | 10 letters; matches start empty |
| Gear | 2 carried, 2 deployed per player |
| Fusing | Letters are reserved at once; it takes 0.6 s + 0.12 s per letter, at 40% speed |
| Refunds | Interrupting or cancelling refunds the letters; broken reusable gear returns its letters |
| Elimination | Spills the whole bag |
| Cosmetics | Looks only, never stats |

**Recipes (12):** BALL, BAT, BED, BLADE, BOMB, FOAM, LAMP, MAT, PLATE, SOAP, SOFA, TABLE. Next up: VACUUM and ROPE.

## 2. Controls

| Action | Key |
|---|---|
| Move | WASD |
| Look | Mouse |
| Attack, throw, place, use, block (whatever you hold) | **Left click** |
| Aim (over-the-shoulder zoom, precise throws and placement) | **Right click** (hold) |
| Fuse letters | **Q** → type the word → Enter |
| Close the fuse box | **Tab** or **right click** (or Esc) |
| Bag, map and recipes | Hold Tab |
| Pick up / revive | E / hold E |
| Switch hand / drop | 1, 2 / hold R |
| Jump / dodge | Space / Shift |
| Pause | Esc |

- **Settings → Controls:** click a key, press a new one; clashes swap. Mouse sensitivity, invert Y and reset, saved on this PC. On-screen prompts always show your current keys.
- **Gamepad:** picks words from a list instead of typing.
- **Touch:** stick, auto-aim and action buttons.

## 3. Camera and cursor

- **Position:** third person, centred behind the back, with the whole body in view above the letter tray. The pivot is 1 m up, 3.3 m back, with a 64° field of view.
- **Spawns:** you start a short step clear of walls, facing the open room, so the camera has space.
- **Aim:** hold right click to move to a closer shoulder view with a narrower field of view.
- **Cursor:** locked and hidden in a match, with a centre crosshair. It is freed for menus, the bag and the fuse box.
- **Walls:** the camera pulls in and rises over your head at walls, blocking furniture fades, and upper floors hide when you are below them.
- **Couch play:** split screen (recommended).

## 4. UI and HUD layout

| Place | Content |
|---|---|
| Top left | Brand tile, which also pauses |
| Top right | Minimap, players alive, timer, room or objective |
| Bottom left | Big HP number, two hand slots |
| Bottom centre | 5 × 2 letter tray |
| Centre | Crosshair |
| Overlays | Fuse box, Tab bag (letters / map / recipe book), toasts, round countdown, pause, help, result card |

**Style:** warm cream panels and dark glass, Lilita One and Nunito fonts, large icons at 512 px, and sharp 2× sprites.

## 5. Lobby

- **Stage:** you stand in the selected house with a rotatable character and no bots.
- **Top bar:**
  - home, settings and quit;
  - leaderboard and shop;
  - **LOADOUT · PLAY · CAREER**;
  - coins and party.
- **Behaviour:** every page toggles open and closed, and closes with X, Esc or a click outside.
- **PLAY:** queue (Practice / Matchmaking / Workshop), mode cards, and house cards with floor plans.
- **LOADOUT:** outfit, colours, extras, gear skin and recipe book.
- **CAREER:** level, stats and recent matches.
- **Settings:** General, Graphics (up to 4K), Controls and How to play.
- **Match dock:** chosen mode and house, with a big GO.

## 6. Game modes

| Mode | Idea |
|---|---|
| Dibs! | Free-for-all; last standing; first to 3 rounds |
| Duos | 2v2 with downed and revive |
| Moving Out | Co-op: carry keepsakes to the van before the house closes |
| Moving Day | Co-op: fuse and place checklist furniture against the clock |
| Tutorial | Guided basics |
| Creative Workshop | Furnish and save your own house |
| *More* | Open; the team adds modes here |

## 7. Maps

| Map | Type | Status |
|---|---|---|
| Pinwheel House | 1 floor, 5 rooms | Built |
| Garden Courtyard | Rooms around a garden | Built |
| City Flat | 6-room flat | Built |
| Terrace House | 2 floors | Built |
| Walk-up Apartments | 3 floors, café and garage | Built |
| Apartment tower | Many floors, lifts and stairwells | Planned |
| Famous buildings | Landmark-inspired, original designs (castle, palace, museum, skyscraper and so on) | Planned |

**Every map:**
- 4 spawns;
- rooms close in order;
- clear doorways;
- bots that can use the stairs.

## 8. Art and audio

- **Look:** warm toy box, plush bodies, wooden letters, a cream, sage and terracotta palette.
- **Quality:** MSAA, ACES tone mapping, a sun-and-fill light rig, 2K textures, and render scale for 4K.
- **To do:**
  - richer walls, sky and decor;
  - UI sounds and music;
  - layered animation.

## 9. Tech

- **Engine:** Unity 6000.6.3f1 with URP and the Input System.
- **Rules:** an engine-free C# rules layer, tested in a .NET harness.
- **Data:** shared JSON for rules, items and maps.
- **Web:** a Three.js prototype.
- **Tests:** EditMode 252, PlayMode 233, rules 225, web 101 plus 29 browser flows, all passing.
- **Run:**
  - **Unity:** open `Assets/_Project/Scenes/Hub.unity`.
  - **Rules:** `dotnet run --project Tools/RulesHarness`.
  - **Web:** `cd Web && npm ci && npm run dev`.
- **Art:** Blender scripts in `Tools/AssetPipeline` (build, then verify).

## 10. Roadmap

| Step | Status |
|---|---|
| Rules, art pipeline, modes, 5 houses, lobby, HUD, match cards | Done |
| Fuse box closes with Tab or right click | Done |
| Left click does everything; right click aims | Done |
| Centred camera framing | Done |
| Key rebinding in Settings (keyboard and mouse) | Done |
| Gamepad and keyboard-half rebinding | Planned |
| Map and image quality (walls, sky, decor, icons, UI sound) | Next |
| Leg motion, layered animation, world feedback | Planned |
| Apartment tower and famous-building maps | Planned |
| Windows build at 60 fps | Planned |
| Android, LAN, then online play | Later |

## 11. Open decisions

- Split-screen couch camera.
- Web speed and scale in Unity: 4.5 m/s and ×1.23.
- Upright letter tiles.
- Online leaderboard storage.
- Merging back into the team repository.

## 12. Team workflow

- **Branches:** `main` is always playable; work on `feature/*` and `fix/*` branches and merge by pull request.
- **Commits:** short and prefixed by area.
- **Unity:**
  - one person per scene;
  - commit `.meta` files;
  - large files go through Git LFS.

| Member | Main areas | Share (%) |
|---|---|---|
| | | |
| | | |
| | | |

## 13. Assets and credits

| Asset | Source | Licence |
|---|---|---|
| 3D art pack (40 items, 26 letters, 21 house modules, 10 VFX, avatar) | Supplied to the project | Project asset |
| Lilita One / Nunito | Google Fonts | SIL OFL |
| Liberation Sans | Red Hat (TextMeshPro) | SIL OFL |
| Three.js, Vite, DejaVu | Open source | MIT / Bitstream Vera |
| URP template | Unity Technologies | Unity Companion License |
| Action icons | Generated with an AI image tool for this project | Project asset |
| Item thumbnails, textures, sound cues | Rendered or generated by the project | Project asset |

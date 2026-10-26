# Wordbound controls and editing

Windows review build **20261026T221541Z**, source **c4810d6a**. The lobby and both Solo launch/menu/return walkthroughs pass, with 31 actual images reviewed. Saved Play/Settings frames, the two control legends and compact character framing are included. Physical controller testing and full multiplayer matches remain open. [Try it](../../BuildNotes/client-view/TRY-WORDBOUND.md).

## Start playing or building

- **Play → Local → Wordbound solo → GO:** Time Run or Freestyle, one player. Leave extra local seats before starting.
- **Play → Create → Wordbound maps → OPEN:** opens the local map editor. Its playtest uses the first player's controls; other local seats are retained for your return, but do not join that playtest.
- **Online:** a separate entry. The original house modes are still local; selecting them does not create a public Wordbound match.

The keyboard/mouse or controller selected at launch stays attached to that Solo session or local map playtest. Another controller's activity does not take over. The game borrows the original controls and returns them intact when you leave.

## Default controls

The on-screen hints follow saved keyboard/mouse remaps. Controller letters below use Xbox names; the displayed label can vary by device.

| Action | Keyboard / mouse | Controller |
| --- | --- | --- |
| Move / look | WASD / mouse | Left stick / right stick |
| Jump | Space | A / south button |
| Pick up, use or practise | E | RT |
| Open crafting | Q | Y / north button |
| Smash / attack | Left mouse | X / west button |
| Throw equipped item | Hold right mouse, then left mouse | Hold RB, then X |
| Block with a shield equipped | Hold left mouse | Hold LT |
| Drop | Hold R | Hold LB |
| Select equipment 1 / 2 | 1 / 2 | Use the equipment cycle |
| Change equipped item | Tab | Press right stick |
| Open or resume menu | Esc | Start / Menu |

Crafting is a two-step choice: select an item, then choose **Craft**. Missing letters appear in the selected item's detail. In menus, use the pointer or controller D-pad/left stick; A selects, B goes back, and the right stick scrolls. Help and Journal return to the previous menu before B resumes the game. **Menu → Help** keeps full controls optional; story and clues belong in the Journal when available.

**Settings → Controls → House games / Wordbound** selects the relevant legend. Its heading remains visible while scrolling, showing the current context. Both legends edit the same saved keyboard/mouse bindings, mouse sensitivity and inverted mouse Y; switching the legend does not choose a game mode. These changes apply to the mouse-player seat and appear in Wordbound's hints. Controller mappings currently stay fixed. Cancel a rebind to keep the previous key; Reset Defaults changes the saved bindings.

## Leaving and recovering

- Losing window focus, typing or rebinding suppresses gameplay actions. Release held action buttons before resuming.
- If the selected controller becomes unavailable, the menu opens and gameplay input stops. It does not silently switch to another controller or keyboard. Reconnect the original controller and release its buttons; if it is not recognized as the same device, return to the lobby and launch again with the intended controls.
- **Return to lobby** ends Solo. A local map playtest returns to the same editor history. **Back to lobby** from that editor saves the draft; reopening can recover the draft, but closing the scene does not preserve Undo history.
- A stopped or failed local session offers a safe return. These local flows do not imply online host recovery, matchmaking or a completed full-match acceptance test.

## What you can edit in Unity

Open **Wreckabulary → Open Current Workspace → Design**. Stop Play before saving edits.

- Home, Play, Settings, Shop and Loadout have saved frame prefabs. Use **Design → Edit Home dock & turn hint / Edit Play panel / Edit Settings panel / Edit Shop panel / Edit Loadout panel & recipe shelf**, save outside Play, then use **Scenes → Play Lobby / Play choices / Play Settings / Play Shop / Play Loadout**. Stop and reopen the asset to check persistence; previews use the real player save.
- **Edit Play panel / Play layout assignments** controls its frame regions and `Compact Maximum`; **Edit Settings panel / Settings layout assignments** controls tabs/content regions and display confirmation space. The profiles are `Assets/_Project/Resources/UI/LobbyPlayLayout.asset` and `Assets/_Project/Resources/UI/LobbySettingsLayout.asset`; prefabs are `Assets/_Project/Prefabs/UI/Play/PlayPage.prefab` and `Assets/_Project/Prefabs/UI/Settings/SettingsPage.prefab`. See the [lobby editing guide](LOBBY-EDITING-GUIDE.md) for exact fields and safe hierarchy rules.
- Compact Solo/Maps framing follows the fitted panel width; Home and the full House chooser restore centre. It uses the saved lobby camera settings without changing avatar scale.
- Lobby framing and assigned gameplay Camera/Visual/Ui Theme profiles are editable. `Assets/_Project/Data/Wordbound/Authoring/Interface.asset` styles Wordbound menus/HUD; lobby fonts and prefab colours have their own saved settings.
- **Inner Play cards, Settings tabs/option/rebinding rows, top navigation, online party layout, HUD placement and inner pickers remain generated.** Their surrounding saved frames are editable; dragging temporary generated children during Play does not save a layout.
- **Scenes → Edit Flagship course** opens the saved course. Workshop autosaves are separate from that Project asset. Keep map rules, objectives and physical puzzle setup consistent when editing; decorative changes alone do not create playable interactions.

Native checks: **27/27 Editor** and **31/31 Play** cases pass with no skips. Missing-only setup/repeat checks pass; the recorded **4,208 authored files** are preserved. These native checks cover source integration and projected avatar bounds; the separate Windows review covers its 31 actual images. Physical controller acceptance remains open.

Source basis: `LobbyMenu.OpenWordboundSolo/OpenWordboundWorkshop`, `WordboundLauncherSolo`, `WordboundLauncher` local playtest route, `WordboundInputBinding`, `WordboundPlayerView`, `WordboundMatchHud/Menu`, `DesktopBinding`, `ControlsPanel`, and the current lobby editing guide.

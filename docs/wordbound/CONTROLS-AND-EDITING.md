# Wordbound controls and editing

Windows review build `20261026T210349Z`, source `0ba2309a`. Lobby and both Solo launch/menu/return checks pass. Physical controller testing and full multiplayer matches remain open. Settings currently shows house-game controller guidance; use the Wordbound controls below or its in-game Help.

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
| Change equipped item | Tab | Press right stick |
| Open or resume menu | Esc | Start / Menu |

Crafting is a two-step choice: select an item, then choose **Craft**. Missing letters appear in the selected item's detail. In menus, use the pointer or controller D-pad/left stick; A selects, B goes back, and the right stick scrolls. Help and Journal return to the previous menu before B resumes the game. **Menu → Help** keeps full controls optional; story and clues belong in the Journal when available.

**Settings → Controls** saves keyboard/mouse bindings, mouse sensitivity and inverted mouse Y. These changes apply to the mouse-player seat and appear in Wordbound's hints. Controller mappings currently stay fixed. Cancel a rebind to keep the previous key; Reset Defaults changes the saved bindings.

## Leaving and recovering

- Losing window focus, typing or rebinding suppresses gameplay actions. Release held action buttons before resuming.
- If the selected controller becomes unavailable, the menu opens and gameplay input stops. It does not silently switch to another controller or keyboard. Reconnect the original controller and release its buttons; if it is not recognized as the same device, return to the lobby and launch again with the intended controls.
- **Return to lobby** ends Solo. A local map playtest returns to the same editor history. **Back to lobby** from that editor saves the draft; reopening can recover the draft, but closing the scene does not preserve Undo history.
- A stopped or failed local session offers a safe return. These local flows do not imply online host recovery, matchmaking or a completed full-match acceptance test.

## What you can edit in Unity

Open **Wreckabulary → Open Current Workspace → Design**. Stop Play before saving edits.

- Home, Shop and Loadout have saved panel/card prefabs. Use **Edit Home dock & turn hint**, **Edit Shop panel / offer card**, or **Edit Loadout panel & recipe shelf**; try them through **Scenes → Play Lobby / Play Shop / Play Loadout**. Those previews use the real player save.
- Lobby framing and assigned gameplay Camera/Visual/Ui Theme profiles are editable. `Assets/_Project/Data/Wordbound/Authoring/Interface.asset` styles Wordbound menus/HUD; lobby fonts and prefab colours have their own saved settings.
- **Play cards, Settings, top navigation, online party layout, HUD placement and inner pickers are still generated.** Dragging their temporary Play-mode hierarchy does not save a layout. Changing those structures still requires source changes.
- **Scenes → Edit Flagship course** opens the saved course. Workshop autosaves are separate from that Project asset. Keep map rules, objectives and physical puzzle setup consistent when editing; decorative changes alone do not create playable interactions.

Source basis: `LobbyMenu.OpenWordboundSolo/OpenWordboundWorkshop`, `WordboundLauncherSolo`, `WordboundLauncher` local playtest route, `WordboundInputBinding`, `WordboundPlayerView`, `WordboundMatchHud/Menu`, `DesktopBinding`, `ControlsPanel`, and the current lobby editing guide.

# Lobby and play experience refinement

STATUS: IN PROGRESS. Owner: James / SethyPagna. Baseline source fd5bb147; tested Windows 20261026T145311Z. This work continues the existing production goal and preserves its unfinished work.

## Intent

Keep the vivid sunroom and original character as the visual focus. Make the next action obvious with short labels, clear selection, consistent Back behavior and honest participant information. Ordinary saved lobby layouts and profiles remain editable in Unity. Avoid advertising modes, bots, matchmaking or accessibility options the runtime cannot actually deliver.

## Current implementation slice

- [x] Confirm integration checkout, branch and existing drafts; save recovery snapshot 15-16-26-475Z (564 verified files).
- [x] Read official navigation, input and matchmaking references; record their application separately from our design choices.
- [x] Extract the unchanged Play and Settings page classes for separate ownership; R0 151628 and core 152023 (41/41) pass.
- [x] Capture the unchanged lobby, Play, customization and settings as a visual baseline. Native 152213 passes 2/2 with unique capture folder; original sunroom and character preview remain the visual basis.
- [~] Replace the misleading house-mode Matchmaking destination with a direct route to real Wordbound online play. Group local play, online play and creation/learning; show actual solo/local/bot/team composition.
- [~] Keep Play cards visual and concise, with one selected objective and participants summary instead of repeated paragraphs.
- [x] Save Home's Local Play / Online Play labels through Unity; native 155841 passes 12/12, migration 160028 changes exactly three text fields, repeat 160158 makes no save and preserves all 276 authored outputs. Shop/Customize/local-count navigation and narrow-width reflow still await full visual acceptance.
- [~] Group real settings into meaningful categories; preserve rebind cancellation and display rollback, and repair content overlap/scroll/focus behavior.
- [~] Review customization with actual captures; preserve its useful full-character preview, use the explicit Customize destination and shorten its completion action to Done. Saved appearance/recipe layouts remain intact.
- [ ] Run focused behavior/layout/persistence checks, inspect changed screenshots at useful sizes, create an exact-source Windows build and verify the affected routes.
- [ ] Update progress.md, editing instructions and recovery checkpoint with evidence and remaining work.

## Behavior boundaries

Original Dibs and Duos fill empty seats with bots. MovingOut, MovingDay and Escape use human-only crews. Local party means players on this PC. These five house modes still lack their online bridge. Wordbound's five modes retain their real public/private party paths; no local launch or bot substitution may be called public matchmaking. Team format, fill and privacy are separate concepts.

This slice improves choosing and understanding gameplay; it does not claim to finish the original-mode online bridge, full multiplayer match acceptance or richer maps/skins. Those remain in progress.md.

## Research applied

- [Xbox XAG 112: navigation](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/112): predictable focus order, consistent return routes and layouts that remain usable when scaled. We use these principles for page grouping and focus restoration; full accessibility compliance is not claimed.
- [Epic: mode settings](https://dev.epicgames.com/documentation/en-us/fortnite/mode-settings-in-fortnite-creative): player structure, public/private access and team fill are distinct settings. Our inference is to avoid conflating Wreckabulary's local party, AI filling and public matchmaking.
- [Rocket League: Play menu organization](https://www.rocketleague.com/news/first-look-play-menu-changes-coming-to-rocket-league): an official example of grouping play destinations and maintaining useful routes between them. We retain Wreckabulary artwork and terminology rather than copy that game's visual assets.
- [The Last of Us Part II: accessibility](https://www.playstation.com/en-ie/games/the-last-of-us-part-ii/accessibility/): a reference for discoverable control customization and separately adjustable options. It is inspiration for organization, not evidence that those features are implemented here.

Root owns shared integration, saved asset changes and serialized Unity/player jobs. Modes, settings and visual/reference lanes draft in separate BuildNotes subfolders. No original checkout, ICY/Lunar work, Tools/kit, networking secrets or unrelated heavy jobs are changed.

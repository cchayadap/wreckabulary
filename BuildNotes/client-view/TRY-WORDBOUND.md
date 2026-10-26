# Windows review build

Open [Wreckabulary.exe](../../Builds/qa-20261026T145311Z/Wreckabulary.exe). Keep the complete build folder together. This is a tested QA-enabled review build, not the finished production release.

**Changed:** corrected BAT/HAMMER grip orientation; calmer idle and stronger hurt faces; crafting, carrying and confirmed-win reactions; compact gameplay HUD; original-colour item pictures with one selected recipe detail.

1. **Home → Shop / Loadout** — inspect the original character and saved appearance. Purchases use earned coins and the real local save.
2. **Wordbound → Workshop → Preview → Playtest** — edit and try a course offline. **E** picks up, **Q** opens crafting, left mouse smashes. **Esc → Return to workshop** returns to editing. Help contains the full current bindings.
3. **Wordbound → Connect → Create party → Ready → Quick Play** — search for compatible human players. Friends can use a party invite. Public queues need other people and do not silently substitute AI.

Build **20261026T145311Z**, source **be648835**, Unity **6000.6.4f1**: Home **3/3**, Workshop **5/5**, equipment/action **6/6**. All three Windows runs exit normally with no reported console errors, exceptions or warnings. The actual HUD Craft route receives exactly one accepted command. [Build, run and artifact evidence](windows-character-ui-20261026.json).

## Unity editing

Open this isolated project using **C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe**, then **Wreckabulary → Open Current Workspace**. Saved Home, Shop, Loadout, cameras and studios are available. **Design → Facial reactions & blending** selects the saved expression profile. [Editing guide](../../docs/wordbound/LOBBY-EDITING-GUIDE.md).

## Still open

All-equipment animation/feel, additional emotion cues, live remote transitions, richer maps/skins, full online matches and release acceptance remain unfinished. The five Wordbound modes have bounded online evidence; the five original house modes remain local. [Progress and remaining work](../../progress.md).

**The Lost Address:** follow a misrouted keepsake through Packing Yard, Kitchen Works, Attic Stacks and Rooftop Garden, spelling tools to repair the route home. Three chapters, 14 clues, optional journal and six result-aware epilogues are implemented. Optional restoration tasks remain planned. [Story design](../../docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md).

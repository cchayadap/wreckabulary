# Windows review build

Open [Wreckabulary.exe](../../Builds/qa-20261026T125941Z/Wreckabulary.exe). Keep the complete build folder together. This is the tested **before-correction baseline**: James's requested holding, expressions and UI corrections are in progress. It is not a production release.

1. **Home → Shop / Loadout** — browse the original character and saved appearance. Purchases use earned coins and the real local save.
2. **Wordbound → Workshop → Preview → Playtest** — edit and try a course offline. **E** picks up, **Q** crafts, left mouse smashes. **Esc → Return to workshop** returns to editing.
3. **Wordbound → Connect → Create party → Ready → Quick Play** — search for compatible human players. Friends can use a party invite. Public queues require other people; they do not silently substitute AI.

Build `20261026T125941Z`, source `78ac4514`, Unity **6000.6.4f1**: Home **3/3**, Workshop **5/5**, original equipment and BAT action **6/6**. All three Windows runs exit normally with no reported console errors, exceptions or warnings. Eight real BAT phases have verified upright orientation. [Exact build and smoke evidence](windows-reviewed-baseline-20261026.json).

## What remains

- Held-item poses, action/facial reactions and excessive gameplay panels need correction. These functional passes do not approve the visuals.
- The five **Wordbound** modes have online routes. Fresh TimeRun4 and Freestyle8 capacity tests also pass, including cleanup; full matches, recovery and separate Windows clients remain unverified. The five **original house modes remain local** and still need their online bridge. [Online scope](../restart-20261026/all-modes-online-scope.md).
- Richer maps, painted skins, full animation coverage, reliable performance, human usability and stripped release packaging remain unfinished. An earlier workshop timeout is retained in [stage-one evidence](windows-current-stage1-20261026.json).

## Story and Unity editing

**The Lost Address:** follow a misrouted keepsake through Packing Yard, Kitchen Works, Attic Stacks and Rooftop Garden. Smash furniture for letters and spell tools to repair the route home. Three chapters, 14 clues, optional journal and six result-aware epilogues are implemented. Optional restoration tasks remain planned. [Story and experience](../../docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md).

Open this isolated Unity project using `C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe`, then **Wreckabulary → Open Current Workspace**. Saved Home, Shop, Loadout, camera and studio assets are available for Inspector/Prefab editing. Do not open a second Editor while an automated check owns the project. [No-code workspace guide](../../docs/UNITY-WORKSPACE.md), [lobby editing](../../docs/wordbound/LOBBY-EDITING-GUIDE.md), [current progress](../../progress.md).

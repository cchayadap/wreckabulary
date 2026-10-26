# Try Wreckabulary

Open [Wreckabulary.exe](../../Builds/qa-20261026T232801Z/Wreckabulary.exe). Keep the complete build folder together.

- **Play → Local → House games:** original modes/maps, with the actual human/bot setup shown. Dibs/Duos support local bots; the other house modes use humans.
- **Play → Local → Wordbound solo → GO:** Time Run for the delivery circuit, or Freestyle to explore and craft. Use one local player; the game menu returns to the chooser.
- **Play → Create → Wordbound maps → OPEN:** edit, playtest and return to the same draft. Select an existing random zone to adjust height, quarter-turn rotations and floor permissions; changes wait for **Apply**. **Home & tutorial** retains the original tools.
- **Play → Online:** Wordbound public/private human parties. Full online-match acceptance and original-house networking remain unfinished.
- **Customize / Shop / Settings:** preview original artwork and adjust real options. **Controls → House games / Wordbound** shows the correct shortcuts; its heading stays visible while scrolling. Keyboard remaps are shared and saved; controller mappings stay fixed.

Build **20261026T232801Z**, source **ec4b127b23c9d084aa281f2248a362d92b313399**, Unity **6000.6.4f1**. This build passes **13 Windows stages** across lobby, Solo and workshop; all **39 screenshots** were reviewed. All runs exit normally, and all **4,208** recorded authored files are preserved. Earlier saved-frame Editor 27/27 and Play 31/31 checks pass; new workshop Editor checks pass 35/35. [Lobby/Solo evidence](windows-lobby-solo-232801.json) · [Workshop evidence](windows-workshop-232801-verification.md).

For Unity editing, open **Wreckabulary → Open Current Workspace → Design**. Home, Play, Settings, Shop and Loadout have saved frames; use the matching **Scenes** preview after saving outside Play. Inner cards/settings rows and some other layouts remain generated. [Editing guide](../../docs/wordbound/LOBBY-EDITING-GUIDE.md) · [Controls](../../docs/wordbound/CONTROLS-AND-EDITING.md).

This is a review build. A small **“trial version”** label is visible; its origin is unconfirmed. Starting rooms still need richer art and stronger checkpoint contrast. The workshop log records four unused postprocessing-shader diagnostics despite zero scenario console counters. Physical input, complete public-mode coverage, performance, final artwork and stripped production release remain open. [Progress](../../progress.md).

[Current visual review](windows-232801-lobby-solo-visual-review.md) · [Workshop screenshots](WORKSHOP-REVIEW-232801.md) · [Earlier lobby before/after viewer](UI-REVIEW-221541.html).

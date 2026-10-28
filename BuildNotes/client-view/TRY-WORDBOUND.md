# Try Wreckabulary

Open [Wreckabulary.exe](../../Builds/qa-20261028T155755Z/Wreckabulary.exe). Keep the complete build folder together.

This is the last tested checkpoint. James prefers the earlier menu appearance; the full-width Play organization and Roboto typography revision is in progress and is not in this executable yet.

- **Play → Local → House games:** original modes and houses. Dibs/Duos support local bots; the other house modes use humans.
- **Play → Local → Wordbound solo:** choose Time Run or Freestyle, then Lost Address or Parcel Hall, and **Play**. Parcel Hall is a separate version of the arrival layout with 18 usable furniture pieces. Use one local player.
- **Play → Create → Wordbound maps → Open:** edit, validate, playtest and return to the same draft. Existing zone controls edit placement ranges, rotations and floor permissions.
- **Share → Map + settings:** keeps the exact seed, mode and team format. Import with the matching game content/authoring source. **Draft only** keeps the older course-only format. Undo restores map/seed, while mode stays a session choice.
- **Customize / Shop / Settings:** cleaner rows, original artwork and real options. Settings has separate House and Wordbound control legends.
- **Play → Online:** Wordbound public/private human parties. Complete online acceptance and original-house networking remain unfinished.

Build **20261028T155755Z**, source **75b02aff8e7b27e4e1c45e9444c7225a5d4841dd**, Unity **6000.6.4f1**. Windows lobby **5/5**, Solo **4/4**, Workshop **5/5** pass with normal exit. These are scripted checks, not complete matches or physical-controller acceptance. The gallery contains **45 actual screenshots**. Creator sharing passes **35 rules tests** and **48 Unity tests**; its actual Share-panel input/layout and restart persistence still need review. The authored-file comparison preserves **4,228** files.

[Before/current gallery](../redesign-20261028/review/REVIEW.html) · [Full progress](../../progress.md) · [Editing guide](../../docs/wordbound/LOBBY-EDITING-GUIDE.md).

For Unity editing, open **Wreckabulary → Open Current Workspace → Design**. Home, Play, Settings, Shop and Loadout have saved frames; use the matching Scenes preview after saving outside Play. Room appearance and the Parcel Hall course copy have Inspector assets. Inner cards/settings rows and some layouts remain generated; editing the course copy does not replace the packaged/public course automatically.

This is a tested review build, not the finished production release. Room scale, furnishing/landmarks, additional maps/skins/items, creator scoring/team/victory rules, full multiplayer/recovery, physical input and performance remain open. Equipment catalogue needs a visible scrollbar and stronger small-preview contrast. A small “trial version” label remains visible; its origin is unconfirmed. No removal was attempted. Each raw player log retains four unsupported/stripped depth-of-field/Panini diagnostics; those passes do not execute. Review this and the release backlog before shipping.

# Try Wreckabulary

Open [Wreckabulary.exe](../../Builds/qa-20261028T215158Z/Wreckabulary.exe). Keep the complete build folder together.

**Current review build: 20261028T215158Z.** It includes real map previews, the editable Parcel Service course, original-art wall detail, corrected starting camera, neutral HUD and a clearer Workshop cutaway. This is a tested review build; final art and the full production plan remain unfinished.

- **Play → Local → Wordbound solo:** choose Time Run or Freestyle, select **Parcel Service**, then Play. Lost Address and Parcel Hall remain available. Use one local player.
- **Play → Create → Wordbound maps → Open:** expand **Templates** on the left, then choose **Parcel Service**. Its draft, autosave and Undo history are separate from Assigned course. Select a room/object, use the frame-selection icon, drag/edit, then **Playtest**. Return through the game menu to keep editing.
- **Workshop:** object thumbnails and compact tool icons remain visible. The selected room has a thin border; its real floor material stays visible. Wall decoration is hidden with the walls in the editing cutaway and appears in full preview/Playtest.
- **Share → Map + settings:** includes seed, mode and team format. Custom courses require compatible content and use private sessions; they are not automatically public-matchmaking maps.
- **House games / Customize / Shop / Settings:** existing routes remain available. Dibs and Duos have local bot filling. Original House modes still need their online bridge; Wordbound online acceptance is also unfinished.

In Wordbound, **E** interacts, **Q** opens crafting and **Esc** opens the game menu. Use Settings → Controls for the separate House and Wordbound bindings.

[View actual screenshots and the map comparison](../visual-coherence-20261029/review/windows215158/REVIEW.html) · [Full progress](../../progress.md) · [Edit Parcel Service in Unity](../visual-coherence-20261029/UNITY-EDITING.md) · [Lobby editing guide](../../docs/wordbound/LOBBY-EDITING-GUIDE.md)

## Verified scope

Source **4ee1d37a4e64751e093d136dd52a6d99be7aa53f**, Unity **6000.6.4f1**. All five Windows scenarios pass and exit normally: lobby 5/5, original Solo 4/4, original Workshop 5/5, Parcel Solo 3/3 and Parcel Workshop 4/4. Exact build/source evidence audits verify **63 game screenshots**; **4,306 authored files** remain unchanged after the runs. Native local Parcel tests complete all three Time Run laps and all six Freestyle stations; focused rendering/material checks pass 13/13 after the selection correction. These are scripted checks, not physical-controller or separate-PC multiplayer acceptance.

The review page contains the original game screenshots and two matched static map images. File references and inline JavaScript syntax were checked. Automated browser rendering was unavailable because the browser tool rejects local file URLs; no workaround was attempted.

## Still open

The old maps remain sparse; Parcel Service needs stronger room landmarks and first-action guidance. Workshop still exposes technical fields in advanced editing, some props look pale, and Shop/Loadout composition needs further work. More maps, detailed equipment/skins, motion/emotions, story/creator rules, full public multiplayer/recovery, original House networking, performance and physical-input acceptance remain in progress.md. This is not the production release.

The previously observed trial label and unsupported depth-of-field/Panini diagnostics remain release-review items. No watermark removal was attempted.

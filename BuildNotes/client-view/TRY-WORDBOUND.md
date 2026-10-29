# Try Wreckabulary

Open [Wreckabulary.exe](../../Builds/qa-20261029T034759Z/Wreckabulary.exe). Keep the complete build folder together.

**Current review build: 20261029T034759Z.** Play now has centered **Practice (Offline) / Matchmaking / Create** choices, direct modes below them and consistent compatible-map cards. Roboto, neutral panels and original artwork remain. Final art and the full production plan are unfinished.

- **Play → Practice (Offline):** choose Dibs!, Duos, Moving Out, Moving Day or Escape. For a Wordbound course, choose **Time Trial** or **Freeplay**, select Lost Address, Parcel Hall or Parcel Service, then Play. These local Wordbound entries use one player. Escape keeps its randomized house.
- **Play → Matchmaking:** opens the existing separate Wordbound online lobby. Choose a mode, public map and human teammates, then connect. A lone co-op party with teammate filling off gets an explanation before Find match. This build's menu checks do not verify live Photon matches or original House networking.
- **Play → Create:** choose Home builder, Map editor or Tutorial. For Wordbound, choose **Map editor → Open**, expand **Templates** and select **Parcel Service**. Its draft, autosave and Undo history are separate from Assigned course. Select a room/object, frame it, drag/edit, then **Playtest**. Return through the game menu to keep editing.
- **Workshop:** use **Objects**, **Puzzles** or **Room** on the right. Choose a recipe by picture, use named room links, and expand **Advanced IDs** only when needed. Scroll vertically for lower settings. The selected room keeps its real floor material and a thin border. Wall decoration appears in full preview/Playtest.
- **Share → Map + settings:** includes seed, mode and team format. Custom courses require compatible content and use private sessions; they are not automatically public-matchmaking maps.
- **House games / Customize / Shop / Settings:** existing routes remain available. Dibs and Duos have local bot filling. Original House modes still need their online bridge; Wordbound online acceptance is also unfinished.

In Wordbound, **E** interacts, **Q** opens crafting and **Esc** opens the game menu. Use Settings → Controls for the separate House and Wordbound bindings.

[View actual screenshots](../play-flow-20261029/windows034759/REVIEW.html) · [Full progress](../../progress.md) · [Edit Play in Unity](../play-flow-20261029/UNITY-EDITING.md) · [Saved scenes and testing](../../docs/wordbound/SCENES-AND-TESTING.md) · [Edit Parcel Service](../visual-coherence-20261029/UNITY-EDITING.md)

## Verified scope

Source **07d814ec1993f7b444bc150091f699206ea7901c**, Unity **6000.6.4f1**. All **seven Windows scenarios** pass and exit normally: lobby5/5, original Solo4/4, original Workshop5/5, Parcel Solo3/3, Parcel Workshop4/4, inspector5/5 and Play organization1/1. Strict build/source audits verify **79 game screenshots**. All **4,326 saved authored files** remain unchanged against the post-preview baseline. Native menu checks pass **76/76 Play** and **35/35 online/settings** tests.

Earlier native Parcel tests complete three Time Run laps and six Freestyle stations. Isolated Kitchen appliance handling passes3/3; its separate saved Unity entry is available for further development. These Windows screenshots do not prove the complete flagship route, held-item animation, physical-controller input or separate-PC multiplayer.

The review page contains unchanged final game screenshots. File references, dimensions, hashes and inline JavaScript syntax were checked. Automated browser rendering was unavailable because the browser tool rejects local file URLs; no workaround was attempted.

## Still open

Maps remain sparse and some props pale; stronger landmarks, first-action guidance and richer artwork are still required. Shop/Loadout composition and Workshop thumbnails need more work. **BELT and matching weapon/material impact sounds remain planned, not finished items.** More maps/skins, grips and motion/emotions, story/creator rules, public multiplayer/recovery, original House networking, performance and human acceptance remain in progress.md. This is a tested review build, not the production release.

The previously observed trial label and unsupported depth-of-field/Panini diagnostics remain release-review items. No watermark removal was attempted.

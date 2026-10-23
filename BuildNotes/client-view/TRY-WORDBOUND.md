# Try the current Windows review build

Open `Builds/qa-20261023T232641Z/Wreckabulary.exe` from the integration project. Keep its whole folder together, including `Wreckabulary_Data` and the DLLs. This is the tested review build; final production acceptance is still in progress.

## Try the workshop

1. Open **Wordbound**, then **Workshop**. You do not need to connect for a local playtest.
2. Choose **Time Run** or **Freestyle**, edit the course, then select **Preview** and **Playtest**.
3. In the game, use **E** to pick up, **Q** to craft, **left mouse** to smash and **Esc** for the menu. Use **Return to workshop** to continue editing.

The automated Windows run verified the exact draft, playtest, return and Undo/Redo. Original Wreckabulary models are the default. The menu uses the original cream/teal colours and Nunito font.

## Try online with another person

Choose a guest name and **Connect**. Select the same mode, region and public map settings as the other player. Select **Create party**, mark yourself **Ready**, then use **Quick Play**. Friends can use **Copy party invite** and **Join party**; a party leader queues the ready party. Public matchmaking needs other compatible human players and can remain in the queue when none are available.

All multiplayer modes have public catalogue entries. Full matches, Duo/Squad gameplay and recovery are still being verified; this build is not a claim that every online case has passed. The latest results and remaining work are in [progress.md](../../progress.md).

## Edit in Unity

Use the isolated `wordbound-integration` project and its current Unity version, 6000.6.3f1. Follow the [Unity editing guide](../../docs/wordbound/UNITY-EDITING-GUIDE.md) for course handles, random placement ranges, camera profiles, visual overrides and the interface theme. Ordinary design changes use these Inspector controls.

## Known limits of this build

- The measured playtest still has noticeable update stalls. The 60 FPS target is not met.
- The initial party card is taller than its content; a compact layout is being prepared. Starting-room composition and held/combat views still need review.
- An earlier Unity Inspector draft-save failure remains unexplained. The latest exact save and recovery tests pass; failed writes retain in-memory recovery.
- Separate Windows clients, full mode/party coverage, human usability and the final stripped release are unfinished.

Build source: `8c02f106c5e6beb9a72fc35f06d22264a94e504e`. Windows run: `20261023T232850Z-visible-wordbound_workshop_flow`, **5/5 PASS**, normal exit and no reported console errors, exceptions or warnings. [Evidence and screenshots](windows-original-theme-polish-20261023.json).

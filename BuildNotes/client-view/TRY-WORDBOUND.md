# Try the current Windows review build

Open `Builds/qa-20261024T005254Z/Wreckabulary.exe` from the integration project. Keep its whole folder together, including `Wreckabulary_Data` and the DLLs. This is the tested review build; final production acceptance is still in progress.

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
- The initial party card now fits its content. Starting-room composition, camera framing and held/combat views still need review.
- Public Team Race Solo tests have failed remote movement and snapshot transfer deadlines. The latest native run fails movement; an earlier run reaches a host command, then rejects an overdue snapshot. Long client frames are confirmed; reliable online play across all modes is not yet verified.
- The separate Freestyle equipment inspection exposed an airborne starting character and rejected collection. Held-item inspection remains unfinished while the starting collision is investigated.
- An earlier Unity Inspector draft-save failure remains unexplained. The latest exact save and recovery tests pass; failed writes retain in-memory recovery.
- Separate Windows clients, full mode/party coverage, human usability and the final stripped release are unfinished.

Build source: `c7b0d8a933c2235ba7cca7c195799c7964ae3b33`. Windows run: `20261024T005757Z-visible-wordbound_workshop_flow`, **5/5 PASS**, normal exit and no reported console errors, exceptions or warnings. [Evidence and screenshots](windows-synchronize-workshop-20261024.json).

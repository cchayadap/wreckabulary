# Try the current Windows review build

Open [Wreckabulary.exe](../../Builds/qa-20261024T034544Z/Wreckabulary.exe). Keep its whole folder together, including `Wreckabulary_Data` and the DLLs. This tested review build uses Unity 6000.6.4f1; final production acceptance remains in progress.

## Try the workshop

1. Open **Wordbound**, then **Workshop**. You do not need to connect for a local playtest.
2. Choose **Time Run** or **Freestyle**, edit the course, then select **Preview** and **Playtest**.
3. In the game, use **E** to pick up, **Q** to craft, **left mouse** to smash and **Esc** for the menu. Use **Return to workshop** to continue editing.

The official Windows test passed all five checks: exact draft, playtest, return and Undo/Redo, with no reported console errors, exceptions or warnings. A separate five-check equipment run passed real collection, crafting and equipping of BAT/HAMMER, then returned to the unchanged draft. Original Wreckabulary models are the default; the menu uses the original cream/teal colours and Nunito font.

## Try online with another person

Choose a guest name and **Connect**. Select the same mode, region and public map settings as the other player. Select **Create party**, mark yourself **Ready**, then use **Quick Play**. Friends can use **Copy party invite** and **Join party**; a party leader queues the ready party. Public matchmaking needs other compatible human players and can remain in the queue when none are available.

All multiplayer modes have public catalogue entries. Full matches, Duo/Squad gameplay and recovery are still being verified; this build is not a claim that every online case has passed. The latest results and remaining work are in [progress.md](../../progress.md).

## Edit in Unity

Use the isolated `wordbound-integration` project with the verified editor at `C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe`. The installation, package resolution, native checks and this Windows build pass. Follow the [Unity editing guide](../../docs/wordbound/UNITY-EDITING-GUIDE.md) for course handles, random placement ranges, camera profiles, visual overrides and the interface theme. Ordinary design changes use these Inspector controls. Avoid opening a second Editor on this project while an automated Unity job is running.

## Known limits of this build

- The measured playtest still has substantial update stalls. The 60 FPS acceptance target remains open; the test does not measure presented frame rate.
- Starting rooms still need richer visual composition. The held BAT is partly hidden by the head in the inspection view; full grip, animation, combat and camera comfort remain under review.
- Current native two-client public checks pass for all five modes, but earlier intermittent movement/transfer failures remain unresolved as a reliability requirement. These are separate clients in one Editor process. Full party sizes, separate Windows clients and complete match outcomes are not yet verified.
- Initial Freestyle overlap is repaired: native floor/clearance and actual Windows grounded collection pass. Recovery/drop paths remain separate unfinished work.
- Windows Inspector draft publication is repaired; all 36 persistence cases pass, including locked/read-only files and recovery after memory loss. A new-process Windows-player repeat-save recovery check remains open.
- Separate Windows clients, full mode/party coverage, human usability and the final stripped release are unfinished.

Build source: `9e013dc9e766a616ae1d45ed037cbbd863dbf89e`. Build `20261024T034544Z` passes its gate in 237.3 seconds. Official workshop run `035028`: **5/5 PASS**, 60.7 seconds. Equipment run `035159`: **5/5 PASS**, 13 successful commands and a normal exit. [Official evidence](official-short-editor-pass-20261024.json), [equipment evidence](windows-original-equipment-pass-20261024.json), [visual review gallery](../equipment-polish/ASSET-REVIEW.html).

The [plot and player-experience design](../../docs/wordbound/STORY-AND-PLAYER-EXPERIENCE.md) covers anticipated play, endings and rating principles. New epilogues and optional restoration tasks are specified but not implemented.

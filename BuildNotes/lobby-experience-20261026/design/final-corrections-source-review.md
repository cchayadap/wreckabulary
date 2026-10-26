# Final lobby corrections — source review

STATUS: SOURCE REVIEW COMPLETE — no blocking source finding

Read-only review of the five root-owned changes after Windows162634 exposed a truncated Random card and stale map-change notices. No live source edits or native jobs by this reviewer.

| Inspected file | SHA256 |
| --- | --- |
| Assets/_Project/Scripts/UI/Lobby/LobbyMenu.cs | ee98c8b5a0cbda02fa0aa6fc4aac8188e59fb9c36029f26b3ec24ba63f0bddf8 |
| Assets/_Project/Scripts/UI/Lobby/PlayPage.cs | d50b7e3f711a308e07772de358d07dfe5d221c22700139234cb81c47e160556b |
| Assets/_Project/Scripts/WordboundQa/LobbyExperienceQa.cs | b51b90e825efc9dc6fd8bf9743ddab4aa8836eecdeda43531a5eb6dc87c335b7 |
| Assets/_Project/Tests/LobbyPlayChoiceTests.cs | 93b8817af12cc9ee0563dc9343442114e0f6af5ba1308d5c002c49e05f7bb08e |
| Assets/_Project/Tests/LobbyTests.cs | c53056a5ae625aab4594229f74cbb8c96f413ddeae559ed24cedd4c48ef7646d |

- RandomHouseCard retains the same button identity, selected state and callback. It now uses the existing Home icon and the shared Words/TagLine layout. Words stretches to the poster width with14-unit side padding, so the title receives the layout group's real width instead of the old default100-unit RectTransform width. Random house is a full label; New each run is a concise explanatory tag. No texture/font/material asset is introduced.
- At the authored244-unit poster height, the top icon occupies20–130 from the top; the bottom text block is36+6+30 units high with14 bottom padding, starting around158. That leaves a nominal28-unit gap before render/layout effects. Final screenshot comparison remains necessary; this arithmetic is not a visual pass.
- LobbyMenu.Choose removes only the two map-state Post calls. It still assigns the random map, selects concrete Session maps, changes Stage.SetMap, refreshes the bar and refreshes Home. Mode/queue policy, generation, scene loading, authority and all unrelated notices remain unchanged. Current card, footer and Home details already represent the selected map; a fresh run can no longer accumulate those contradictory prior-selection notices.
- LobbyPlayChoiceTests now asserts the exact Random house label, updates all TMP meshes, rejects truncation/overflow, and rejects the two obsolete map-notice patterns. Existing bot policy, joined-player summary and focus assertions are retained.
- Windows QA extends ControlFits to the actual Map random button; this checks its screen-safe bounds and every child TMP label. It does not change route timing or weaken the existing checks.
- Existing LobbyTests replaces a toast assertion with current selected-house Describe text, while retaining map selection, backdrop replacement/framing and match-start assertions.
- git diff --check passes for all five files. No native compile/test result is claimed here; parent is running those gates.

Next acceptance gate: inspect new actual Windows Escape and returned Home images beside preserved162634 evidence. Verify complete title/tag, icon/text clearance, and no stale map notifications. Preserve the earlier images and review as the evidence that motivated the correction. This slice adds no Online design or network coverage.

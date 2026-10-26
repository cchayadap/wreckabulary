# Settings scrollbar source review

STATUS: SOURCE REVIEW COMPLETE — no blocking source finding

Read-only review of the root-owned three-file delta, addressing the weak scroll discovery observed in all12 prior Settings captures. No live source edits or native jobs by this reviewer.

Frozen inspected SHA256 values:

| File | SHA256 |
| --- | --- |
| Assets/_Project/Scripts/UI/Lobby/SettingsPage.cs | f9678be5ca1b9d4c3f79094252f3b67f0b1ccf80ce78f75a15878c2aca0ac033 |
| Assets/_Project/Tests/LobbySettingsNavigationTests.cs | 1dc111e189d2834c7ea5769768a1388caef032f1ab79881098775a91ea636408 |
| Assets/_Project/Scripts/WordboundQa/LobbyExperienceQa.cs | 8c0399e0a8afcb41079fa0500c9cd556436f3c9cd6c459ed34c0d49363f48d89 |

Confirmed:

- AddScrollbar changes only Settings-created lists. The current list roots have zero horizontal offsets; reserving24px leaves an8px gap before the16px sibling track. No text is added. Profile/Audio/Gameplay/Help can hide their tracks when content fits.
- The track fills the list's parent, not the entire page. Controls' dedicated list parent excludes its title and fixed footer; other tabs use the content holder that already excludes the92px pending-display strip. Therefore the new track does not span the footer or confirmation controls.
- AutoHide is used, not AutoHideAndExpandViewport: no automatic viewport resizing competes with the explicit24px reservation. Installed uGUI ScrollRect.cs confirms the verticalScrollbar property wires its value-change event to SetVerticalNormalizedPosition and updates visibility from actual scrolling need. BottomToTop agrees with normalized0 at bottom.
- Navigation.None keeps the mouse scrollbar out of the established button/tab focus order. Existing LobbyScrollFollow still scrolls selected content buttons into view. Refresh destroys the owned UI tree and recreates the track; no static resource, persistent asset or extra subscription owner was introduced.
- Existing tab scroll recording in Closed and restoration after Canvas.ForceUpdateCanvases are retained. The changed Graphics test now checks a visible bar, sets its value to0, verifies bottom, then exercises scrolled setting refresh, focus and tab-return restoration. Its existing owned page fixture is1600×720 and settings are restored in teardown.
- Windows QA checks the Controls scrollbar is active, traverses it to0, waits, checks actual normalized position and saves one additional bottom image. Existing preference restoration and input-lease/close checks remain. This changes expected evidence from15 to16 images without changing user settings.
- git diff --check passed for the three files. No native compilation/execution was performed here; root owns those gates.

Remaining validation: inspect the slightly narrower Graphics/Controls rows and scrollbar in the new Windows PNGs, especially final binding reachability. The QA path programmatically changes the actual Scrollbar value; it proves event/linkage and rendering only when run, not a physical mouse drag or controller navigation sequence. Track hide/show for every short/long tab and pending-display state still requires native visual observation. No broad accessibility compliance or minimum pointer-target claim is made for the16px track.

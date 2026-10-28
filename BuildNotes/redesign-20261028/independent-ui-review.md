# Independent UI integration review

STATUS: IN PROGRESS

Scope: read-only source review of the current combined Play, Settings, Controls, Loadout, shared UI primitives and saved-menu migration. Root owns every native process and gate143423. The reviewer authored Play/Settings/Controls, so those checks are self-review; the root-owned Loadout/shared/migration checks provide an independent implementation perspective. Native screenshots have not yet been supplied for this revision.

Review criteria: essential actions stay reachable, short-width labels remain legible, keyboard/controller focus remains visible, colour and equipment selections preserve saved state, original assets remain recoverable, and Unity-authored frames/font assets remain editable.

Confirmed findings will be appended below with file/line evidence and practical fixes. Source inspection alone is not visual approval or proof of runtime behavior.

## Findings

1. **P2 — A focused choice can be clipped after a window resize.** `Assets/_Project/Scripts/UI/Lobby/LobbyScrollFollow.cs:21` returns whenever the selected object is unchanged. It does not react to the viewport becoming shorter. With the new Play scroll region, select a lower mode or map while everything fits, then shorten the page: content gains overflow, but the selected object receives no reveal call. The same shared behavior applies to Loadout. Preserve focus and call `Show` when the viewport dimensions change, or track viewport/content bounds in the follow component. Add a case that selects a map at full height, shrinks the page without changing selection, and checks that selection is visible. This is a source-confirmed missing resize trigger; exact rendered clipping still needs the native check.

2. **P3 — The Loadout scrollbar track cannot be clicked.** `Assets/_Project/Scripts/UI/Lobby/LobbyPages.cs:363` sets the track image's `raycastTarget` to false. Only the draggable handle remains a hit target, so clicking the visible track outside the handle does not page the list. The new long single-column locker makes this affordance useful. Allow raycasts on the track while leaving decorative lines noninteractive, then check dragging and track clicks in the player. Wheel and controller selection reveal remain available.

Update after root approval and completion of gate144044: finding1 has a live source repair in LobbyScrollFollow plus a focused unchanged-selection resize/manual-scroll test. The repair checks viewport/content size, not scroll position, so mouse scrolling is not constantly recentered. Root reports finding2 repaired in its owned source. Both still require native validation; the repaired shared follow component is now this reviewer's implementation and no longer independent source evidence.

## Checks with no source defect found

- Palette rows retain original wardrobe data, owned-colour filtering, try-on preview and committed outfit updates. Selecting a different garment or colour still refreshes and reselects its stable object name. Hood dependencies and matched colours are untouched.
- Character/Equipment tabs have a 240-unit authored strip with 18-point maximum and roughly 14-point minimum autosizing. The labels should fit their existing cells; actual readability remains a screenshot check.
- The 180×164 recipe tiles have 96-unit art and a 154-unit letter budget. Current configuration offers at most three choices in a clothing row; no added giant unbounded option row was found.
- The migration assigns the saved body font and its matching material before save; the runtime typography layer still clones persistent source fonts/materials rather than changing authored assets during play.
- Independently compared all six archived source files against root's pre-migration SHA-256 inventory: Play, Settings, Loadout, RecipeTile, Shop and Interface are exact matches to their previous bytes. All six original source .meta files are unchanged, preserving their existing asset identities. Evidence: independent-archive-check.json.
- Header, close, fixed Done/Play and display confirmation roles retain their callbacks and authored references. No purchase, ownership, map identity or multiplayer rule was changed in the reviewed diff.

## Observed gate evidence

Read only: `.harness/delivery/workspace-minimalsetup-2026-10-28T14-34-23-838Z-stdout.txt` reports R3 PASS for `LobbyMinimalDesignSetup.Apply`, exit 0, no errors or failures, and the preserved archive at `Assets/_Project/UI/DesignArchive/BeforeMinimal20261028`. Its end receipt is 2026-10-28T14:36:01.761Z. This confirms setup execution and saved artifacts only; it is not UI acceptance. Root's home/Play native test gate began at14:37:33Z and is pending in this review.

Subsequent root gate143734 returned96/102; gate144421 returned100/102 after bounded repairs. The latest completed run passed the unchanged-focus resize and independent manual-scroll assertions. Two heading-height failures remained and have source repairs pending rerun. A later authorized Solo map chooser adds an explicit Parcel Hall alternative while retaining Lost Address as default; that source and root launcher integration also need the next native run. No fresh Windows screenshots have yet been inspected for final visual judgment.

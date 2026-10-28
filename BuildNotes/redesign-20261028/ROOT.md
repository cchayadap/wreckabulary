# Wreckabulary design recovery

STATUS: IN PROGRESS

Recovered 2026-10-28 after usage interruption. Checkout wordbound-integration, branch sethypagna/wordbound-integration, HEAD2862c9c1. The only tracked source edit at recovery is PlayPage.cs; prior map and menu lanes saved findings but no completed implementation. No Unity/player/Blender process was found. Current delivered Windows232801 remains historical behavior evidence; the owner rejects its visual design.

Direction: no decorative stripes/sunbursts, restrained neutral surfaces with original vivid art, clean one-per-row choices, meaningful preview and selection, concise text. The actual map entry has no resource furniture and uses giant plain collider-box surfaces. Improve real architecture and content without adding misleading walk-through props or silently changing published canonical map identities.

Ownership: play_settings_redesign owns PlayPage/SettingsPage/ControlsPanel; menu_redesign owns WordboundMatchHudMenu/Recipes and the relevant GUI layout fixture; map_redesign drafts separate source under its BuildNotes folder. Root owns shared LobbyKit/LobbyPages, saved frame setup, integration and all native gates. Keep unrelated heavy jobs running. No Tools/kit, SDK, YAML or metadata edits by hand.

Plan remains comprehensive: all five Wordbound modes and original multiplayer house modes; human public/private parties/recovery; more maps/items/skins/story; usable map and gameplay authoring; saved Unity editing; tested Windows and final performance/human acceptance. No production completion claim.

Next: finish and review source changes, R0 and relevant core, serialized native behavior/layout and saved-asset setup, then actual Windows captures and refinement. Preserve older failures and current untracked drafts before committing verified steps.

## Verified recovery and first native iteration

- Recovery snapshots at 14:15, 14:32 and 14:51 UTC preserve all dirty/untracked files; the last contains 1,922 verified files. These live in ignored `.harness/recovery`.
- R0 **143029** and cosmetics/inventory core **142559** pass (41/41).
- Saved menu setup **143423** passes. `saved-menu-preservation.json` verifies exactly six intended original assets changed, all six prior byte sequences were archived, and no other previously fingerprinted authored file changed. Independent archive review confirms original metadata unchanged.
- Lobby runtime **143734** fails 6/102: three clipped text slots, obsolete uppercase/ray expectations and a too-strict geometry equality. Preserve the XML and raw log.
- Equipment/input/GUI runtime **144044** passes 58/58, including large text and small viewport Layout/Repaint/deferred actions.
- Lobby runtime **144421** passes 100/102; the remaining House/Help heading heights are fixed in source, pending rerun. Scroll resize/focus fixes and earlier text corrections pass their checks.
- `post-first-native-preservation.json` verifies all 4,220 post-setup authored files unchanged by these native tests.
- Map source integration verified frozen manifest `a08137dcd6df555ee76fde84efd569f957ba87def98c1c0b8e358fb305d9e2f8`; independent tangent/cleanup findings resolved. R0 **145230** passes; official variant/flagship core is running. No map native/render acceptance yet.
- An explicit Solo selector now preserves Lost Address as default and offers the separate furnished Parcel Hall identity. Root added real launcher construction, native tests and a third Windows scenario step; source/native validation remains pending.

Creator audit `creator-scope.md` identifies substantial current map/puzzle controls, missing custom mode-rule authoring, private-only custom-map hosting, and file export losing the selected seed. These gaps are retained in progress.md; map editing is not presented as finished gameplay authoring.

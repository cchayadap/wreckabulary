# Optional actual-course previews

STATUS: FROZEN DRAFT — SEMANTIC SOURCE CHECK PASS; NATIVE/VISUAL CHECK NOT RUN

Three-file integration manifest: `manifest.json`; exact diff: `solo-previews.patch`; original and proposed source: `baseline/` and `candidate/`. No live Assets, scenes, prefabs, meta files or colours were changed.

| Target | Change |
| --- | --- |
| `Assets/_Project/Scripts/UI/Lobby/LobbyPlayLayout.cs` | Optional public `Sprite LostAddressPreview` and `Sprite ParcelHallPreview` fields. Inspector tooltips identify the map and explain empty-field fallback. Validation accepts absent previews. |
| `Assets/_Project/Scripts/UI/Lobby/PlayPage.cs` | Resolve the matching field for each actual solo map ID. Draw `Image` named `Map preview` above the unchanged short title/detail; preserve aspect, use white tint and disable raycasts. Put image before the selected mark in sibling order. Keep BOX/TABLE through the existing ItemImage path when the matching preview is absent. |
| `Assets/_Project/Tests/LobbyPlayChoiceTests.cs` | One focused test uses distinct wide/square sprites, checks both map assignments and independent fallback, wide/narrow fit, title separation, selection-mark order, Submit/focus and original House/binding preservation. Restores shared profile fields and destroys only its temporary sprites/texture. |

Contract agreed with map_redesign: namespace `Wreckabulary`; resource key `UI/LobbyPlayLayout`; active asset `Assets/_Project/Resources/UI/LobbyPlayLayout.asset`. Their separate editor capture/import draft will write `Assets/_Project/UI/Previews/Wordbound/LostAddress.png` and `ParcelHall.png`, capture actual `CreateSoloContent`, and expose editable framing at `Assets/_Project/Data/Wordbound/Authoring/Previews/SoloMapPreviews.asset`. This draft neither creates nor assigns those assets. Root integrates the source contract before their setup references the new fields.

The runtime borrows Sprite references and does not create a camera, RenderTexture, material or per-card texture. It does not dispose borrowed assets or write the profile. Existing IDs, handlers, selected mode/map state, saved frame and input behaviour stay in place. No new explanatory text appears in the player menu.

`check-source.ps1` compiled the candidate substitutions against the current Unity response files: Wreckabulary 178 sources / 302 references and Wreckabulary.Tests 69 sources / 292 references, zero errors. See `source-check.json`. This does not substitute for running the new test and inspecting actual imported course captures after root integration.

Recommended native check: run the focused existing fixture with both saved previews assigned; repeat with one and then both references absent. Review 1280×720 and narrow-layout captures for recognizable course content, clear title/check, no pointer obstruction and unchanged map selection. Aspect preservation intentionally permits unused space around a thumbnail on wide cards; this source contract alone does not finish the card composition, map art, colour direction or Workshop redesign.

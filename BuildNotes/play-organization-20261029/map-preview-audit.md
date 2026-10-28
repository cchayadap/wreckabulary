# Solo map preview audit

STATUS: COMPLETE. Read-only next-step preparation. No art generation, native job, source/asset edits or current-build acceptance claim.

## Smallest useful change

Replace only the two Solo map-card item icons with saved landscape previews of the actual course entrance. Keep the existing names, selection borders/checkmarks, navigation, return state and launch handlers. Use a fixed recorded sample seed; actual Solo starts choose a fresh random seed. A thumbnail is a representative map view, not a promise of the next match's exact furniture arrangement.

The bounded implementation is two Sprite references in the existing LobbyPlayLayout asset, a small SoloMapCard renderer change, and an Editor-only two-image capture/import tool with saved camera composition. No runtime camera, RenderTexture, new art service, public map change or online catalogue change is needed.

## Confirmed reusable content

| Purpose | Exact existing path / API | Finding |
|---|---|---|
| Lost Address editable course | Assets/_Project/Data/Wordbound/Authoring/FlagshipCourse.asset | Saved TimeRun/Solo, seed 0000000000000001, references saved camera and original presentation. |
| Parcel Hall editable course | Assets/_Project/Data/Wordbound/Authoring/Variants/ParcelHallArrivalV2.asset | Current saved variant; preserve earlier ParcelHallArrival.asset. |
| Shared presentation | Assets/_Project/Data/Wordbound/Authoring/Presentation.asset | Both courses reference this visual profile. |
| Runtime room style | Assets/_Project/Resources/Wordbound/RoomArchitecture.asset | Use its current wood-floor/no-default-panels configuration. |
| Existing saved UI configuration | Assets/_Project/Resources/UI/LobbyPlayLayout.asset | Already loaded by PlayPage.Build; natural place for the two editable Sprite assignments. |
| Existing saved frame | Assets/_Project/Prefabs/UI/Play/PlayPage.prefab | Keep frame, typography, spacing and layout references intact. |
| Card replacement point | Assets/_Project/Scripts/UI/Lobby/PlayPage.cs, SoloChoices and SoloMapCard | BOX and TABLE are passed at lines 146–147; SoloMapCard currently uses LobbyKit.ItemImage with a 184-square image. |
| Stable selection keys | Assets/_Project/Scripts/Core/WordboundSoloEntry.cs | LostAddressMap="LostAddress", ParcelHallMap="ParcelHall". Bind preview by these keys. |
| Native world/props | WordboundCourseWorld.Build + WordboundVisualResolver.Create | Already used by existing map review captures; render real geometry and original item artwork. |
| Owned capture lifecycle | Assets/_Project/Scripts/Online/Authoring/Editor/WordboundRoomArchitectureCapture.cs | Actual course copies, owned preview scene, PreviewRenderUtility, camera/lights, property snapshots, exact receipts, visual/world disposal and render-state restoration exist. Reuse this pattern; do not run its whole fourteen-frame baseline comparison for a two-card refresh. |
| Current no-code course preview | Assets/_Project/Scripts/Online/Authoring/Editor/WordboundCourseAssetEditor.cs + WordboundCoursePreview.cs | Inspector already offers Apply form edits and save asset, Validate and preview exact seed, drag/orbit/zoom, and Play this exact preview locally. It does not currently save/export a thumbnail camera composition. |

The existing architecture capture menu is Tools > Wordbound > Presentation > Capture room architecture comparison. Its camera poses, seed 0123456789abcdef and 1280×720 output are hard-coded, so it is useful reusable implementation, not yet an editable thumbnail workflow.

## Essential source-parity constraint

Normal Solo does **not** load the editable course assets above. Assets/_Project/Scripts/Online/Client/WordboundLauncherSolo.cs calls CreateSoloContent(items, rules, mode, seed, map): Lost Address comes from installed flagship content; Parcel Hall is created from the current WordboundArrivalVariant factory. StartSolo chooses a new random seed each launch.

Therefore the thumbnail capture should use CreateSoloContent with a documented sample seed and render its IWordboundPuzzleContent.Course / Geometry / Items, matching the installed Solo maps. Alternatively, resolving the saved course is acceptable only after verifying its source/catalogue/manifest identity against CreateSoloContent at that same seed/mode. Stop with a clear mismatch instead of advertising an edited authoring draft as the installed map. Editing a saved course currently affects its Inspector playtest; it does not silently replace the shipped Solo map. Making custom drafts selectable in Solo is a separate future feature.

## Proposed saved Unity editing path

- Add LostAddressPreview and ParcelHallPreview Sprite fields to Assets/_Project/Scripts/UI/Lobby/LobbyPlayLayout.cs. Put new imported PNGs under Assets/_Project/UI/Previews/Wordbound/LostAddress.png and ParcelHall.png. Preserve the current layout asset GUID and exact pre-edit bytes/meta.
- Add a small Editor-only authoring settings asset at Assets/_Project/Data/Wordbound/Authoring/Previews/SoloMapPreviews.asset, containing map key, sample seed/mode, source parity metadata, module anchor, camera pose/FOV and output Sprite. Put its type/tool in the existing Wreckabulary.Online.Authoring.Editor assembly, which already references Client, Puzzles, Authoring and Presentation. Do not put Online course dependencies into the core lobby runtime assembly.
- Provide a saved preview rig prefab at Assets/_Project/Editor/Previews/WordboundSoloPreviewRig.prefab: Camera plus the two preview lights, no gameplay components. An Editor command opens a disposable generated preview from the selected installed map and rig. James moves the camera in Scene View, saves the rig/settings, then presses Capture selected map. This adds the currently missing no-code saved framing path. Keep generated world objects transient; do not copy them into a gameplay scene.
- Capture two truthful entrance views using the same lens/lighting, with enough lateral framing to show Parcel Hall's furnished station clusters and the clear route. Do not add fantasy architecture absent from the playable world. Produce 960×540 opaque, sRGB, single-sprite PNGs (no mipmaps, clamp, bilinear); make the importer settings explicit and inspect them after import.
- In SoloMapCard, show the assigned Sprite above the existing text region, preserving aspect ratio; keep the checkmark last/on top and image raycastTarget=false. Reuse the current available art height so the card need not be taller. Keep the previous item icon as a missing-preview fallback, with an Editor diagnostic rather than runtime loading/generation.
- Stage new files and a receipt outside Assets first, compare source/seed/style/camera hashes, back up only existing preview files/meta/layout, then import and assign via Unity APIs. Do not rewrite metadata or serialize Unity assets by hand. Refresh is an explicit editor action, not an automatic change on every selection/build.

## Existing frames that can inform framing

BuildNotes/redesign-20261028/map-capture/captures/20261028T154341Z-23c34185 contains packing-entry-styled.png / packing-return-styled.png and arrival-entry-styled.png / arrival-return-styled.png. Its receipt has no capture errors and confirms FlagshipCourse plus ParcelHallArrivalV2 (57 vs 75 prop visuals). Its status is CAPTURED_NOT_VISUALLY_APPROVED; these are historical static Editor evidence, not approved ready-to-ship previews. Do not blindly import the older 151024 set, which predates the floor/panel correction. The Windows 170019 Solo world screenshots are additional framing references but include HUD/avatar and are not clean menu artwork.

## Bounded verification and honest limits

Native follow-up should verify source parity at the chosen sample seed, correct map-key-to-Sprite mapping, persistent imported references, crop/aspect at compact and desktop card sizes, selection marker visibility, no input interception, and no mutation of original course/profile/public content. Repeated capture with unchanged inputs should preserve source assets and leave no preview worlds, materials, meshes, cameras or render state behind. Review the actual menu in a Windows build after import.

This replaces misleading item-only cards with real views, but does not solve the sparse/generic room architecture recorded in windows170019-world.md. Current static rendering omits players, active puzzles and some gameplay-only presentation. The full multi-floor map cannot be accurately communicated by one entrance shot; a later optional floor-plan/in-game preview is separate scope. No claim that the current comprehensive map/design plan is finished follows from this proposal.

# Actual map-preview authoring handoff

STATUS: DRAFT. Source compilation passed before final review; no native rendering/import has run and no live assets were changed by this lane.

## Contract and exact scope

Five new Editor-only scripts under Assets/_Project/Scripts/Online/Authoring/Editor. Integrate only the closed manifest files after the Play lane's Sprite contract: Wreckabulary.LobbyPlayLayout.LostAddressPreview and ParcelHallPreview, saved resource Assets/_Project/Resources/UI/LobbyPlayLayout.asset.

The capture uses WordboundLauncher.CreateSoloContent for LostAddress/ParcelHall, a fixed recorded seed and saved mode; it does not substitute an edited course asset. Runtime selection and public maps remain unchanged. It renders canonical world geometry and existing visual-resolver props only. No invented decoration, generated mesh, player or HUD is added.

## Root native sequence

1. Integrate five new scripts plus the independently owned Play Sprite consumer. Unity creates metadata. Compile through the normal project gate.
2. Execute Wreckabulary.Online.Editor.WordboundSoloPreviewCapture.CreateOrSelectSettings. It creates only Assets/_Project/Data/Wordbound/Authoring/Previews/SoloMapPreviews.asset when absent and preserves an existing settings asset.
3. Inspector controls: sample seed/mode and per-map module-relative camera position/target/FOV. Frame Lost Address / Frame Parcel Hall opens an actual-world preview. Drag to orbit, scroll to zoom, Save camera to settings, then close the window. Position/Target can also be edited directly without code. This stored camera composition replaces the audit's more expensive separate rig-prefab proposal; it does not change the saved gameplay camera.
4. Save settings, then Capture two draft previews (WordboundSoloPreviewCapture.CaptureDrafts). Fresh outputs go under BuildNotes/visual-coherence-20261029/map-previews/captures/<timestamp-id>. It produces exactly LostAddress.png and ParcelHall.png at960×540 plus receipt.json, including actual course/source/geometry/catalogue hashes, pose/lights, PNG hashes, inputs, file/memory/scene/render-state/tactile preservation. Status deliberately remains CAPTURED_NOT_VISUALLY_APPROVED.
5. Review both full images and the receipt. Reject poor framing or a mismatch rather than hiding it with unrelated art. Capture itself does not touch UI PNGs or the saved layout.
6. To import the chosen capture, invoke WordboundSoloPreviewImport.ImportReviewed after setting WORDBOUND_SOLO_PREVIEW_RECEIPT to its exact receipt.json (or choose it from the Editor menu dialog). It validates the completed two-frame receipt, preservation flags, all recorded input/dependency hashes, both PNG hashes and bounded paths; altered source requires recapture.
7. Before import, exact prior PNG/meta and Play layout/meta bytes are backed up to that capture's before-import folder with hashes. It writes/imports only Assets/_Project/UI/Previews/Wordbound/LostAddress.png and ParcelHall.png as opaque single Sprites with sRGB, no mipmaps, clamp/bilinear/full-rect/uncompressed settings, and assigns the two fields via Unity API. Existing target GUIDs/meta are retained. Import receipt status is IMPORTED_REQUIRES_MENU_REVIEW.
8. Run a fresh Windows lobby/Solo chooser review at compact and desktop sizes. Verify aspect, correct map binding, checkmark visibility and selection/return behavior. Preserve authored assets before/after native tests. A static sample preview is not the next random match's exact placement.

Partial capture/import failures retain their output and backups. An import folder is single-use; a failed partial import requires deliberate review/recovery before rerunning. No automatic broad deletion/rollback or published-map update is attempted.

## Known limits

The framing tool is intentionally small: orbit/zoom plus Inspector target/FOV, no pan gizmo. Capture requires an active graphics device. Static previews omit active puzzles, letter supplies, players and dynamic game lighting. The actual room remains sparse; a truthful preview cannot make it a finished map. The owner has rejected current world/style coherence, so native technical success is not visual approval.

The frame window and capture reuse the existing PreviewRenderUtility/world/visual ownership model; root must verify actual cleanup and input preservation natively. This draft was source-checked only. Other scene edits, paid work or external jobs are outside this slice.

## Final review corrections

- Inputs come from the same saved GameData resource as the launcher, plus its saved launch visual profile.
- Before any backup/write, import re-resolves both installed maps and compares seed/mode/source/geometry/catalogue/manifest hashes, including each image's recorded manifest.
- A deterministic fingerprint covers sorted C# source path+hash entries under Art, Core, Rules, WordboundRuntime, Online/Presentation, Online/Puzzles, Online/Client and Online/Authoring/Editor (maximum512 files). Capture checks it before/after; import requires the same fingerprint. This intentionally forces recapture after relevant source changes, even if payload manifests happen to remain identical.
- Capture/import reject dirty persistent dependencies and importers discovered from the bounded asset dependency roster (maximum4096 paths), including an unsaved dependent Prefab Mode scene. Existing managed PNG TextureImporters are checked separately before replacement.
- Framing-window render cleanup uses nested finally blocks so a failing EndPreview cannot bypass RenderTexture/sRGB/SSAO restoration.

Independent Play lane review found these bounded issues; all are addressed in the frozen source. Root native tests and image inspection remain required. The current PNGs and map styling are not accepted merely because compilation succeeds.

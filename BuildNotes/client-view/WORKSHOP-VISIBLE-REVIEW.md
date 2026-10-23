# Visible Windows workshop review

STATUS: read-only review complete. Two visible production layout defects confirmed; the save failure has a strong QA path-length explanation that still needs a bounded runtime comparison. No source changes, launches, tests or UI operations were performed. Root/input own the native crash investigation. No successful scenario or release acceptance is inferred.

## Evidence opened

Root checkout: `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration`.

- Actual `docs/qa/runs/20261023T175916Z-visible-wordbound_workshop_flow/wordbound_workshop.png`, SHA256 `662fcc1e6b9a3aedcd3e8c648e88b94be42fdeb04f4c8db1aed76e2b5f2af9ab`, 1280×720: workshop panels and a small map render, labels overlap densely over the map, cyan line fragments extend across the left panel/header, and the bottom status begins `Autosave: Could not find a part of the path` followed by the long QA directory. This is a rendered image, not a blank capture.
- The same run directory contains party/workshop PNGs and the created draft/template directories, but no final `checks.json` and no autosave file. The player log initializes D3D11/Intel Arc, reports stripped post-processing shader warnings, adds a Fusion player, then records a native crash. Root reports exit `-1073741819`. The screenshot precedes this crash; this review does not attribute the crash to the save or overlay defects.
- Source read: `WordboundWorkshop{,Interaction,Viewport,Sites,Modes,Sources}.cs`, `Rules/Wordbound/WorkshopStorage.cs`, `WordboundLauncher.cs`, `WordboundWorkshopQa.cs`, and existing `Tests/Editor/WordboundWorkshopTests.cs`.

## 1. Dense labels are a normal-user layout defect

`Workshop/WordboundWorkshopInteraction.cs:83` draws every visible active-floor module ID into a fixed 150×20 rectangle and every active-floor zone ID into a fixed 160×22 rectangle. There is no selected/hovered priority, projected size threshold or overlap rejection. The shown named Attic modules and Props zones share the same small central screen area, making most labels unreadable.

`WordboundWorkshopViewport.Frame` also frames every course module when called without a selection, including other floors, whereas `Sync` limits geometry to active/adjacent visible floors and `WorldView` labels only the active floor. This can make the initial editable region unnecessarily small. The QA scenario uses the production `OpenFlagship`, `ResumeEditing` and `Preview` paths; it does not inject these labels or a special zoom. Normal users opening this content can encounter the same presentation.

Smallest correction to consider: frame the visible editing context; prioritize selected/hovered module and zone labels and suppress overlapping secondary labels at overview scale while preserving full IDs in inspection/tooltips. Keep all geometry, identifiers and picking behavior intact. Acceptance must include an actual 1280×720 flagship capture plus selection/hover, floor switch and zoomed editing, not only the existing projection/picking tests.

## 2. Cyan line leakage is a normal-user drawing defect

The screenshot shows mint/cyan strokes over the palette and header outside the viewport. `WorldView` draws every active-layer zone boundary using `DrawLine`; default state has no selected physical site, so the unconditional zone overlay is the relevant path. These strokes use the production zone color `(0.4,1,0.8)`.

Although the callsite surrounds overlays with `GUI.BeginGroup(viewport)`, `WordboundWorkshop.cs:468` draws each line by multiplying `GUI.matrix` with a translation/rotation and drawing a texture. It does not clip line segments geometrically to the local viewport or reject behind-camera endpoints. `Viewport.Project` returns a large sentinel coordinate for points behind the camera. The screenshot proves the existing drawing path does not contain all strokes; the exact Unity transformed-clipping mechanism has not been reproduced independently here.

Smallest correction to consider: enforce viewport-local segment clipping and safe projection before drawing, and preserve/restore GUI state in a finally boundary; use a drawing path whose clipping remains valid under rotation. Do not fix this by hiding all editing overlays. Check diagonal/near-edge/behind-camera lines and selection handles while orbiting/panning, plus screenshot pixels outside the viewport. Existing viewport tests validate projection, picking and disposal, not rendered clip containment.

## 3. This autosave failure is probably QA path depth, not teardown

`WordboundWorkshopQa.EditWorkshop:127` places storage under the already long `context.OutDir`, appending `workshop-draft-<32-hex GUID>`. Production `OpenFlagship` adds `templates/Flagship`. `WorkshopStorage.AtomicWrite:83` then appends `<course ID>.autosave.json.<32-hex GUID>.tmp` before moving/replacing the primary file.

Observed paths/lengths for this run:

| Component | Characters |
|---|---:|
| Existing scoped `.../workshop-draft-2a44eef79e2747e59dd3cbea9cd6cff1/templates/Flagship` directory | 197 |
| Intended `wordbound-lost-address.autosave.json` primary | 234 |
| Constructed atomic temporary filename | 271 |

Both scoped directories still exist and are empty. No QA scenario/launcher deletion of this folder exists in the inspected route. The error is already drawn in the screenshot before playtest/crash, so post-run teardown cannot explain this occurrence. Windows APIs can retain a 260-character path limit unless the necessary long-path support is active. The 271-character temporary path is therefore a strong inference, not yet a measured Unity/Mono long-path diagnosis. No registry or executable manifest was changed. [Microsoft path-limit documentation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation?tabs=powershell)

Normal production storage defaults to `Application.persistentDataPath/wordbound-workshop`. The project company/product settings are `Wreckabulary Team/Wreckabulary`; the corresponding conventional LocalLow path for this account would produce a 174-character temporary filename for this course. This is a computed comparison, not a runtime save acceptance test. Custom/deep storage paths could still expose the same storage limit; do not conclude all normal-user saves pass.

Smallest diagnostic/QA correction: use a unique short QA-only temporary root, retain the existing `ConfigureQaWorkshopStorage` separation from owner storage, and record its exact path. Wait for the real workshop autosave, then read the saved recovery and compare its complete draft/seed to the edited snapshot before proceeding to playtest. This both tests the hypothesis and prevents another edit-step PASS while persistence failed. Do not change global long-path settings, remove atomic replacement/backup behavior, write into the owner's workshop directory or suppress the error to produce a clean screenshot.

## 4. Save-error presentation and QA assertions need bounded improvement

`WordboundWorkshop.Update:132` stores the complete exception message in `status`; the footer draws it in one 23-pixel-high full-width label. Long paths run offscreen, as shown. Tooltip text occupies almost the same footer coordinates (`OnGUI:191`), so hovering can overlap a persistent status. A concise visible save-failure message with accessible details is appropriate even if this trigger is QA-specific; preserve a clear indication that the draft has not saved.

The QA edit step currently captures after `WaitFrames(3)` and only validates in-memory preview. It has no save/recovery or workshop-pixel/readability assertion. The prior hidden run could therefore PASS edit/return/Undo despite black images, and this visible run could reach capture despite failed persistence. Strengthen only these missing evidence boundaries rather than adding broad UI tests. The supplied image remains pending owner visual acceptance.

## Follow-up ownership and verification

No production fix is included in this report. Coordinate exclusive ownership before editing workshop/UI or QA files. Keep crash diagnosis separate. After a bounded correction, root should run the existing workshop projection/source/Undo/recovery gates, the targeted clipping/label cases, then a real visible Windows workshop save/recover/play/return capture using the same build identity. Supplemental results remain separate from official R4 and all multiplayer acceptance.

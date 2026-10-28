# Visual coherence, real map imagery and Workshop clarity

STATUS: IN PROGRESS. Owner rejected the current appearance on 29 October; technical passes do not approve it.

## Owner correction

The images do not communicate the actual game objective or map. Current colours feel old, odd and plain, including Local. Workshop has too much text, exposes internal object names, and its objects/maps lack detail and clarity. Preserve original Wreckabulary identity, no stripes, no Play avatar, minimal text, editable Unity assets and all prior gameplay/online work.

## Working sequence

- [x] Preserve Windows172247 as a technical checkpoint: source74ffae03; lobby172356 and Solo172443 pass. Workshop172606 passed scenario checks but failed the strict focus requirement; retry172801 passes unchanged requirements. Final strict45-image audit and4268-file preservation pass. This is not an approved visual direction.
- [~] Diagnose muddy/flat palette and inconsistent visual treatments across Play, Shop, Settings and Online. Prepare coherent neutral/vivid-art and playful-original-art directions; the optional owner preference is pending. Do not repaint pages independently.
- [~] Replace Solo BOX/TABLE identifiers with truthful, reusable map previews captured from the same factory content that launches. Record sample seed, map identity, camera, source hashes and exact imported images. Add saved editable Unity framing and Sprite assignments; no runtime preview camera.
- [ ] Recompose Play around actual places and mode intent, with consistent card hierarchy and useful sparse-state composition. Card width alone does not solve the empty page. Keep Local/Online/Create routing, bots/party eligibility, selected state and fixed start action clear.
- [~] Make Workshop icon-led: compact tool actions, real object thumbnails, friendly names, one contextual inspector, clear selection, and optional advanced settings/help. Keep essential short labels/tooltips and controller focus. Preserve drag/move/rotate, exact seed/ranges, Undo/Redo, validation and playtest/return.
- [ ] Improve actual destination/Workshop presentation using original compatible assets, coherent lighting/material scale, purposeful object clusters and identifiable landmarks. Real map preview images must not advertise architecture absent from the playable map. Keep route clearance, puzzle/resource feasibility and physics behavior measurable.
- [ ] Make Shop show the actual equipped/previewed item or skin in a consistent presentation, with clear category, ownership, price and action states. Preserve original meshes/materials and existing outfit/commerce behavior.
- [ ] Compare real screenshots at desktop and compact sizes, review interactions and target contrast, then save/reopen Unity authoring assets and build/test Windows. Preserve failed evidence. The owner alone accepts visual style and feel.

## Ownership and contracts

Root owns integration, Workshop source, native Unity/player jobs, saved assets, progress and checkpoints. Menu lane owns draft colour/composition diagnosis. Play lane owns draft LobbyPlayLayout/PlayPage preview consumers. Map lane owns draft Editor preview capture/import and asset recommendations. All delegates write outside Assets until reviewed; no paid generation or unrelated jobs.

Preview contract: nullable `LostAddressPreview` / `ParcelHallPreview` Sprite fields on `LobbyPlayLayout`; PNG outputs under `Assets/_Project/UI/Previews/Wordbound/`; saved framing/settings under `Assets/_Project/Data/Wordbound/Authoring/Previews/`. Match `WordboundLauncher.CreateSoloContent` for each sample map/seed. Original item art remains a missing-preview fallback only.

## References and practical rules

[Rocket League's official Play-menu design](https://www.rocketleague.com/news/first-look-play-menu-changes-coming-to-rocket-league) is a historical reference for consolidated choices and distinct offline/private routes. CS2 remains the owner's structural reference, not an art template.

[Xbox contrast guidance](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102) supports checking text and controls against their actual backgrounds. [Additional visual cues](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/103) supports pairing recognizable icons with short labels/tooltips where meaning is ambiguous. Less text must not make controls harder to identify.

The full production goal remains in ../../progress.md, including complete multiplayer/recovery, original-house networking, richer content/story, creator rules and production performance. This visual correction does not replace that backlog.

## Current integration checkpoint

- Source13540817 preserves the first Workshop thumbnails/tool icons and optional help; focused input checks28/28 and icon registration1/1 passed before palette integration. Actual new screen review remains pending.
- Shared neutral theme integrated with saved palette and seven frame bindings. The first migration181422 completed but exposed illegal resource loading from serialized component defaults; do not treat its gate PASS as a clean result. Root moved loading to safe startup points and added a cold-thread regression. Native shared-theme checks181725 pass21/21. Migration repetition and visual review remain pending.
- Exact migration comparison covers4268 prior authored files: only eight intended saved UI assets changed; all16 original asset/meta backup bytes match and existing metadata is retained (shared-theme/migration-preservation.json).
- Root corrected fixed illustration ink, selected/check contrast, account badge ink, fallback font outlines, and XP fill/label separation. Final palette/Shop/Play screenshots are still pending.
- Five actual-map preview Editor scripts integrated. Create saved camera settings, capture both maps, inspect actual PNGs, then import a chosen receipt. No generated preview has been claimed yet.
- Additional draft work targets restored original timber ramp material/door frames and actual Workshop object rendering. These are not yet integrated.

## Saved implementation and new Windows candidate

- 45d873f9 saves the shared palette, safe startup loading, exact UI backup/archive and real map camera/capture tools. Theme21/21; menu/navigation/Shop/Loadout97/97; migration repeat181843 clean.
- a80934f5 saves original timber ramp treatment and doorway frames. Native27/27 and scoped core12/12; profile additions load without rewriting old saved values. First precision assertion failure retained.
- c4258b18 saves Workshop original/profile-aware meshes, canonical fit, contextual labels, smaller handles, resolved fixed-object drag and assigned-course startup repairs. Native87/87; first85/87 precision result retained.
- Windows184940 builds from c4258b18; lobby185132 passes5/5, Solo185415 passes4/4 and Workshop185618 passes5/5. Strict audit windows184940-audit.json verifies the exact source/build and45 captures. Preservation verifies4288 prior authored files. Actual Shop/Local/Online images opened by root show dark surfaces, cyan selection, amber action and unchanged original pictures; this is not owner visual acceptance.
- Real map captures182356,183615 and closer183917 are retained. Door/ramp comparison is visible, but both close thumbnails still show sparse generic rooms. No mappreviewPNG was imported or markedvisuallyapproved. A separate local ParcelService mapcomposition is being planned from original legitimate props.
- Current prebuild preservationbaseline: .harness/delivery/ui-authored-before-2026-10-28T18-49-39-867Z.json,4288 files. Latest verified recovery snapshot: .harness/recovery/developer-workspace-2026-10-28T18-31-01-650Z,2313 files.

## Continuing from the Windows review

- Independent reviews opened all24 lobby images: review/windows184940-menu.md and review/windows184940-play.md. Settings hides the avatar's head while leaving feet visible; root is correcting visibility. Play still uses BOX/TABLE placeholders and has sparse lower space. Shop has unused space and the detailed illustrated background differs from the simple game artwork. These remain visual concerns, not test failures.
- Root adds visible Workshop Frame selection/Frame floor icon buttons using existing camera actions, replacing persistent drag-help copy. The actual whole-floor view makes original props tiny; a visible focus action helps inspect them without changing map geometry. Native verification is in progress.
- Root reviewed map-composition/PLAN.md and authorized bounded draft batches A/B. Parcel Service is a separate local three-room course using original real props, seeded parcel storage, existing TimeRun/Freestyle rules and an editable saved CourseAsset. Existing maps and public contracts stay unchanged. Original LAMP is a table lamp, so floor LAMP placements were omitted; revised roster21props/90optional letters must be proven by tests. Drafts are not integrated or claimed playable yet.
- BatchA owner map_redesign: independent content/setup/tests; BatchB owner play_settings_redesign: explicit local destination and separate Workshop source. Both write outside Assets; root integrates and runs all native jobs. Menu reviewer is inspecting all21 current Solo/Workshop images.

## Parcel Service integration — recoverable state

- BatchA manifest281fa54a integrated five new files. Original table lamps were omitted; the inbound PLATE socket was corrected to deployable CLOCK before integration.21props/90optional letters,18fixed plus3seeded BOX placements. Gate R0192237, rules19230019/19 (new course plus existing Arrival variant), native19242119/19 (rules/authored/persistence), saved-course creation192659 all PASS. The saved Unity asset is Resources/Wordbound/LocalCourses/ParcelServiceV1.asset. Preservation after creation verifies all4288 pre-existing authored files unchanged; new files are outside that old baseline's scope.
- BatchB manifest409c590a integrated11files with explicit third Solo selection, saved-course launch, separate Workshop source/history/storage and safe launch binding. Root reviewed source/tests/setup; native Workshop/source tests193156 are running. Launch assignment has not yet been saved. Entry method: WordboundParcelServiceLaunchSetup.BindInstalledCourse.
- Preview manifest5ae51db3 integrated5scripts. Independent review repaired profile fallback, exact input roster and mandatory dependency hashes. A one-map ParcelService capture/import can preserve the old rejected map previews. No new PNG imported yet. CaptureParcelServiceDraft is the new entry point; source and saved assets must be final before import.
- Independent world review is complete21/21: review/windows184940-world.md. Distant Workshop props, technical IDs, sparse maps and low disabled-Craft contrast remain visible concerns. Root's6-frame native lobby backdrop comparison191136 confirms simply switching to the current real3Droom would be too sparse; no default background switch adopted.
- A review found co-located Freestyle sites can be hidden behind a completed nearest site. Menu agent drafts a nearest-unfinished selection fix/regression under map-composition/practice-selection. Map agent drafts actual local native TimeRun/Freestyle physical play tests under map-composition/physical-play (no completed-state injection). Play agent drafts separate Windows parcel scenario under map-composition/windows-qa; original4-step Solo smoke remains intact.
- Current HEADc488ecee saves the backdrop diagnostic;7126ef0a saves Settings visibility/Workshop focus controls. Current A/B/preview production edits are not committed yet. Root owns all native jobs and final saved assets. The last fully audited Windows build remains184940/c4258b18 and predates these new changes.

## Recovery 2026-10-28T19:49:48.831261+00:00

BatchB fixture repaired to use actual Parcel content; targeted19343135/35 PASS. Launch binding193605 saved through Unity with backup. Practice2files, physical1file and WindowsSoloQA2files integrated exactly per integration-1942.json. R0194614,core19463719/19,guide19474114/14 PASS. Native full physical194837 not yet verified. First actual Parcel capture193745 opened; still pale and blank, NOT imported or visually accepted. Agents review bounded course-specific room/material treatment and draft standalone Workshop QA. Root owns native and final integration. Snapshot194916 verifies2531 unfinished files before this note update.

- Native194837 failed0/2 before gameplay because nested EnterPlayMode did not transition. Preserved XML/log. Root reviewed and integrated test-only revision1 manifest0f63a04d: direct root transition, explicit playing assertion, independent source-preservation cleanup. Gameplay checks unchanged. Play chooser/presentation19511317/17 PASS. Standalone Workshop QA source reviewed+integrated, R0195105PASS; Windows not run.

- Checkpoint743fa016 commits Parcel Service source/saved assets and current QA. Native195312231.3sec finishes1/2: TimeRun actual3laps/6crafts/30letters/12objectives confirmedWin; Freestyle map-originTABLEDrop returnsBlockedRelease. Bothcleanup0failures. No physics assertion weakened. Root compact seed foldout toolbar integrated with R0195839PASS; nativevisual checks pending. Course-specific room/material binding+guarded setup drafts in progress, none saved yet.

## Recovery 2026-10-28T20:11:30.615702+00:00

Materialbinding/setup integrated4f00468c/1298495b; native20015952/52PASS. Firstsetup200411failedcompiledURPLitdirty beforewrites; narrowexactshaderexception+importerdirtychecksrootrepair; setup200521/repeat200848PASS. Saved5editableartassets,4289/4290priorauthored unchanged intentionalParcelVisualrefonly. Beforecapture200337/after200615 bothopened; betterfinishseparation butblankwallsstill. Toolbarrootdraft normalized CRCRLF toCRLF; existingtestfixdaac6b6b integrated, native20080821/21PASS. Physicaldiagnosticrevision2 manifest277f1e3c integrated; targetedFreestyle201013running. No newWindowsbuildyet; lastaudited184940 stillpredates allParcelwork.

## Recovery 2026-10-28T20:26Z

Freestyle202225 PASS1/1 through six actual stations,30letters/6crafts and wrong-origin table rejection, cleanup0. Earlier201754 failure was fixture-only shared supply owner: expectedTeam now follows TeamCount and each tile owner is asserted. Strict carried collider repair20162911/11 remains intact. Doorway framing201325/capture201408 and exact preview import201528 PASS; actual PNG now assigned to ParcelService in saved LobbyPlayLayout. No owner art acceptance; map remains sparse. Root saving bounded source checkpoint then release-qa build and five Windows scenarios. No existing public course/admission change.

Windows202715/bccb2fb8 passes all5 scenarios: lobby2028555/5,solo2029454/4,Workshop2030415/5,ParcelSolo2032063/3,ParcelWorkshop2033234/4. Strict45+18 audits PASS;4300authored files preserved. ActualrootParcel review finds blocking spawncamera head occlusion; fix in draft. Realmappreview card too small; source4filefit63212261 integrated, R0203906PASS andnative20394818/18PASS. Optional wallfixtures12filedraft awaitingindependentreview, notintegrated. No newreadybuildclaimuntilcamerafix/newimages.

Recovery20:57Z: committed82ae843c mapcardfit18/18. Integratedspawn3422270e/dressing1d1a24c7 +neutralHUD, officialcore20470820/20 andnative20490092/92PASS. Guardedspawn205132PASS exactStart0only backup. Setup8713c66e helperindependentlyreviewed; inspect205440PASS all6facesrootopened. Beforefixtures all3preview205611PASS output205623-fcb77310. Nativecreatefixturesnowrunning fromexactinspectedfolder; no newWindowsaftercamera yet. Snapshot204819 preserves2751files; latestpreservationbaseline2057344300filesbeforefixtureassignment. Viewer beingupdatedtosamecamera2-mapcomparison.

## 21:08 UTC saved presentation checkpoint

All-three actual-map preview import210504 PASS with exact backups. Fixture creation205804 and read-only repeat210047 PASS. Root opened current205930 Parcel/LostAddress/ParcelHall PNGs: improved original materials and wall fixtures; sparse environments remain unresolved. Full physical rerun210548 active. Recovery snapshot .harness/recovery/developer-workspace-2026-10-28T21-06-14-309Z verifies2809 unfinished files. Next: commit bounded source/assets, fresh Windows build and63-frame audit/review, update TRY/progress.

## 21:24 UTC review-led correction

Build211157/source66687 passes all five scenarios and strict45+18 image audits;4306 authored files unchanged. Full physical210548 passes2/2. Root opened all3 Parcel gameplay frames: camera now whole-body, neutral HUD and proper world shadows. Root and map reviewer confirmed new P2 floating wall fixtures in Workshop cutaway, because supporting walls are hidden. Map lane drafts viewport-only Lit surfaces and omission of wall-only decoration; gameplay/thumbnail fixtures remain. Menu lane preview-depth manifest e916f4ed is integrated (two lighting lines), R0212258PASS; native all-three capture212335 active. No new preview imported yet. Candidates must be compared before selection; current source is66687 plus that one Editor file. All earlier source/build artifacts retained. progress.md reconciled with full backlog and exact earlier file backup.

## 21:46 UTC selection correction

Build213901/source29e5da9a compiled and ParcelWorkshop214134 passed4/4. Root opened the actual close cutaway and playtest: floating fixtures removed, original normal-lit floors/edges visible, gameplay unchanged. However selectedroom floor glows mint under lighting, masking actual wood. This is retained as an intermediate render candidate, not a final63-image delivery. Root keeps the real floor material and uses the existing thin perimeter edges for an owned Unlit selection cue. Existing material lifecycle test now exercises an actually selected room and checks its explicit/derived floor remains intact. R0214521PASS; focusednative rerun follows. Other scenarios for213901 intentionally not run before fixing this visual defect.

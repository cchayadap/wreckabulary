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

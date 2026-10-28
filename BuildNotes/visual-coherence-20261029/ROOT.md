# Visual coherence, real map imagery and Workshop clarity

STATUS: IN PROGRESS. Owner rejected the current appearance on 29 October; technical passes do not approve it.

## Owner correction

The images do not communicate the actual game objective or map. Current colours feel old, odd and plain, including Local. Workshop has too much text, exposes internal object names, and its objects/maps lack detail and clarity. Preserve original Wreckabulary identity, no stripes, no Play avatar, minimal text, editable Unity assets and all prior gameplay/online work.

## Working sequence

- [x] Preserve Windows172247 as a technical checkpoint: source74ffae03; lobby172356 and Solo172443 pass. Workshop172606 passed scenario checks but failed the strict focus requirement; retry172801 passes unchanged requirements. Final strict45-image audit and4268-file preservation pass. This is not an approved visual direction.
- [~] Diagnose muddy/flat palette and inconsistent visual treatments across Play, Shop, Settings and Online. Prepare coherent neutral/vivid-art and playful-original-art directions; the optional owner preference is pending. Do not repaint pages independently.
- [~] Replace Solo BOX/TABLE identifiers with truthful, reusable map previews captured from the same factory content that launches. Record sample seed, map identity, camera, source hashes and exact imported images. Add saved editable Unity framing and Sprite assignments; no runtime preview camera.
- [ ] Recompose Play around actual places and mode intent, with consistent card hierarchy and useful sparse-state composition. Card width alone does not solve the empty page. Keep Local/Online/Create routing, bots/party eligibility, selected state and fixed start action clear.
- [ ] Make Workshop icon-led: compact tool actions, real object thumbnails, friendly names, one contextual inspector, clear selection, and optional advanced settings/help. Keep essential short labels/tooltips and controller focus. Preserve drag/move/rotate, exact seed/ranges, Undo/Redo, validation and playtest/return.
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

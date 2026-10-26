# Windows 232801 lobby and solo — independent visual review

STATUS: BOUNDED VISUAL REVIEW PASS — no new blocking visual regression; existing art/release limitations remain.

Reviewer: story_status. Independently opened **all 22 lobby and 9 solo PNGs** at original resolution from Windows build `20261026T232801Z`. All are 1280×720. Also reopened eight matched 221541 images for direct comparison: solo chooser, Wordbound maps, Shop, Customize, House/Wordbound scrolled legends, Time Run world and Freestyle equipment menu. All 31 image hashes, dimensions and comparison-file hashes are in the companion JSON; unviewed prior images are explicitly marked hash-only. No native jobs, image alterations or live source edits.

Read the actual checks for lobby `20261026T233250Z-lobby_experience` (**5/5 PASS, 22 PNGs**) and solo `20261026T233412Z-wordbound_solo` (**3/3 PASS, 9 PNGs**). Both name build 232801 and report zero errors, exceptions and warnings. Solo terminal/stdout evidence is hashed in the JSON. This is a visual review of those bounded runs; root's delivery verifier owns exact-source provenance and authored-file preservation.

## Observed result

- **Character framing:** the full red-hood avatar stays clear beside the compact Wordbound Solo and Wordbound Maps panels. Both solo returns restore the selected mode and unobstructed avatar. The two-seat guard clearly disables GO and displays its reason.
- **Controls:** HOUSE CONTROLS and WORDBOUND CONTROLS remain visible above the scrolled rows. Wordbound shows Aim RB/R1, Equipment 1/2, R3 cycle and Start/Options menu; House retains View/Share pause. Text and keycaps fit the visible rows, and the reset footer stays separate. Partial rows at viewport edges are ordinary scrolling, not text spilling outside the panel.
- **Shop and Customize:** original character proportions, colored outfit previews, swatches and gear thumbnails remain consistent with the prior accepted views. No cream portrait rectangles, obvious halo/stretching, unreadable item names or blocked Done/equip actions were observed.
- **Other lobby views:** house modes/maps, both Create families, selected summaries and GO/OPEN actions remain readable. Profile, Audio, Gameplay, Graphics and Help show no visible overlapping controls. The online guest view clearly states Wordbound human players, with Connect, Workshop and Back; it does not show a completed connection or match.
- **Solo gameplay/menu:** the original avatar, compact status panel and short prompts remain readable. Time Run's checkpoint cue is visible; Freestyle omits the Time Run counter/cue. Both equipment menus retain the colored original-item grid, one selected-item missing-letter line, Resume and Return to lobby. These observations match the reopened prior world/menu images; no new visual blocker was found.

## Remaining limits

The bottom-right **trial version** label remains visible, as in the comparison images. Its cause is unknown; no licensing attribution or removal is claimed.

The shown starting rooms remain sparsely dressed, with large pale walls and floor areas. Some legacy House Create and Settings pages retain large unused cream panel areas. These are existing polish limits, not evidence that the user's detailed-map and immersive final-art goals are complete.

This review covers static 1280×720 captures. It does not certify other sizes, maximum text scale, continuous animation/rig quality, physical controller/mouse input, purchases, completed rebinding, complete solo runs or live multiplayer. It is suitable for the current tested review build, not a whole-project production-readiness claim.

# Independent online shell visual review

STATUS: COMPLETE — readable improvement, with further refinement required before claiming a minimalist final UI. Independently opened all16 actual GameView PNGs from `run-20261026T183622634Z-e5feb9`, including both widths and maximum font. Scope is synthetic UI-state rendering; no real network, physical input or match acceptance. Reviewer: story_status. No live edits or native jobs.

## Observed strengths

- Guest00/01: a short, readable panel with a strong Connect action. The selected sunroom background keeps the lobby's visual identity visible. No text overlap or clipping is apparent at either size.
- Ready07/08: the dark Find match action and Unready control are visible at both sizes. Optional Private rooms are clearly separated from the primary public action. At maximum font15, the full Find match label still fits near the bottom; roster and composition remain readable.
- Search11/12: Finding players and Cancel search / reservation are immediately visible without scrolling. Copy invite and Leave party are separate actions. No clipped text is apparent.
- Join05/06 and private09/10: displayed fields, Paste/Join controls and custom-room consent are readable. Disabled custom search looks distinct from enabled actions. The narrow private frame is explicitly a bottom-scrolled view, not proof that all controls fit simultaneously.
- Failure13/14: at the captured positions the explanatory error, uncertain-result sentence and Retry same action are readable, with other actions visibly disabled. The message preserves the reason to retry the same transaction.

## Concrete remaining issues

1. **Create's primary action is below the initial480p fold.** Frame03 shows settings down to a partially visible Layout1; Create party and Back are absent. Frame04 proves they appear after scrolling, not that they are initially discoverable. Keep mode/composition visible and place the detailed region/team/fill/map settings behind an explicit Match settings section, with a concise current-settings summary and Create party visible by default. Preserve every setting and existing safe seed/default behavior.
2. **Short authenticated screens retain a large empty cream panel.** In wide Join05, controls end around y430 while the panel continues to y696. Ready07 and Search11 similarly leave substantial blank area. This obscures the room without serving the short content. A later bounded content-height treatment can reuse the guest panel's measured-height approach, retaining scrolling for longer views. This is not necessary to mix into the compact Create correction.
3. **Failure onset remains unproven.** Frame14 was deliberately scrolled to the bottom. It proves readable recovery at that endpoint only. At normal top scroll the disabled party controls precede the newly appended error/Retry, and the wide frame places the error around y435; that suggests poor initial visibility at480p. Capture the actual failure transition without forcing scroll before accepting automatic recovery discoverability. If confirmed, reveal the error/retry region or move the short recovery block above disabled options; retain the full uncertainty semantics.
4. **The visual vocabulary is still a form with many outlined buttons.** Create02 and private09 remain dense when expanded, and numeric other-player labels such as Player100000 feel technical. Folding optional settings is the appropriate small next step; do not call this final immersive/minimalist styling. Human display names require a separate supported data contract, not invented labels that imply real account names.
5. **Navigation at maximum font is only partially shown.** Frame15 includes Find match but excludes Back and lower options; frame08 likewise hides Workshop/Back below the fold. The scrollbar is visible. These images establish legibility, not successful keyboard/gamepad scrolling, Back focus restoration or physical navigation.

## Copy and scope

Shorten the queued/no-assignment action to Cancel search. Keep reservation/return wording appropriate to an assigned transaction and preserve the retry warning. Party2/4 and up to8 humans are both accurate but refer to different capacities; future concise wording should make party versus match capacity explicit if players find this confusing.

All16 images were directly opened:00–15, covering guest, Create/top-bottom, Join, ready, private bottom, search, failure bottom and maxfont ready. The receipt reports PASS, exact1280×720/854×480 sizes and no cleanup errors. This review does not turn registered-control metadata or that PASS into claims about real networking, physical input, human style approval or complete game readiness. Required follow-up is a compact Create capture and unforced failure-onset capture, plus the existing native input tests.

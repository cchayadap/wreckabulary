# Play organization and typography

STATUS: IN PROGRESS — pending native layout, Windows and owner visual review.

The owner prefers the earlier build232801 visual identity. Preserve its colourful artwork, readable cards and warm surfaces while simplifying organization. Do not restore decorative stripes or sunbursts. Play should use the page for choices, with no character, smaller margins and fewer repeated labels. Font changes are allowed; Roboto is the first concrete candidate.

## Intended result

- Full-width Play with Local / Online / Create, a compact mode-card strip, larger map choices and one fixed primary action. Keep the current routing, party, device, map and creator behavior.
- Keep character presentation in Home and Customize. Hiding the Play character must retain the same outfit, party, bindings and avatar and restore them on return.
- Clear Regular / Medium / Bold typography with readable sizes, minimal decoration and exact original fonts preserved. Fonts, materials and saved menu frames remain editable in Unity.
- Restore the earlier warm visual identity in Settings and Customize with organized rows and tighter spacing. Do not introduce more navigation levels.
- Keep a tested Windows checkpoint and a new comparison gallery; preserve rejected and earlier evidence separately. Automated tests are behavior evidence, never owner visual approval.

## Reference decisions

CS2 is a structural reference: a compact mode strip above central map choices and a fixed start action. The [CS2 menu example](https://blast.tv/article/how-to-surf-in-cs2) was inspected as a visual reference. It is not a Wreckabulary style template.

The [official Rocket League Play-menu design article](https://www.rocketleague.com/news/first-look-play-menu-changes-coming-to-rocket-league?lang=en), published in November 2023, explains consolidation of play choices and separate offline/private routes. It is historical design evidence, not a claim about the current 2026 menu.

Roboto source, exact version, licence and hashes are recorded by the typography lane. No generated or approximate font substitutes are acceptable for that attribution.

## Evidence and integration

- [x] Official source checks162413 pass; core162833 passes41/41.
- [x] Native162848 passes4/4: Play hides characters and shadows while retaining party, bindings and outfit; Home/Customize restore the same avatar. Existing camera runtime checks also pass.
- [x] Hash-verified four-file Play draft integrated from `play/manifest.json`; root reduced Play outer gutter from26 to12 units with6 units below the top navigation. Other pages keep their existing outer geometry.
- [x] Full-width Play characterization and navigation gate163111 passes32/32; source checkpoint `c45e4fac` preserves the tested step. Independent lifecycle review found no blocking defect and records the untested active-preview/emote transition separately.
- [~] Roboto source and saved asset migration being prepared separately.
- [~] Earlier-look Settings/Customize organization draft being prepared separately.
- [ ] Native font persistence, layout, focus, small-window fit and saved-asset validation.
- [ ] New Windows build and actual lobby/Solo/Workshop captures, image review and exact preservation receipt.

The full multiplayer, story, map, item, skin, creator and production backlog remains in ../../progress.md. This bounded UI iteration does not replace it.

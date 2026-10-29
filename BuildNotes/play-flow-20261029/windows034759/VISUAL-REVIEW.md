# Windows review build034759

STATUS: AGENT-VERIFIED BOUNDED REVIEW; OWNER VISUAL/FEEL ACCEPTANCE PENDING

Build `20261029T034759Z` uses source `07d814ec1993f7b444bc150091f699206ea7901c` and Unity 6000.6.4f1. All seven Windows scenarios pass with normal exits. Unchanged strict audits cover 45 lobby/Solo/Workshop, 18 Parcel, 11 Inspector and five new Play organization screenshots. Post-run preservation verifies 4,326 saved authored files unchanged against the post-preview baseline.

Every one of the 79 original 1280×720 PNGs was opened individually. Root reviewed the five new Play frames; three independent agents reviewed 24 Lobby, 22 map and 28 Workshop images. `visual-review-coverage.json` reconciles their exact original-image hashes with the strict audited gallery. This evidence does not establish physical-controller input, motion, full routes, online matches or owner style approval.

## Observed result

Centered categories and direct modes are readable; Time Trial and Freeplay share the same three actual course cards. Create shows one selected tool. The Play action stays clear, with no Play avatar or stripe decoration. The compact online guest form renders with its actual course image. Native 76 Play and 35 online/settings checks cover short-window focus, fixed controls, retry and preflight behavior.

Root also directly opened Audio, Graphics and Help screenshots to challenge a suspected missing Close icon. Reinspection confirmed the white X is visible, and the reviewer withdrew that false positive. No product change was needed.

## Remaining work

- Maps are still sparse, with broad plain surfaces and repeated furniture. The lobby artwork sets a richer expectation than the playable/editor maps.
- House previews are schematic floor plans; Map editor still uses a table thumbnail. Single-card views and Shop/creator panels have excess empty space. These are organized current assets, not completed detailed map art.
- Escape's footer says “random”; Workshop still says Time Run while the selector says Time Trial. Finish display-copy consistency without changing canonical IDs.
- Focused return can scroll the Map section label partly away; the focused card, mode row and Play action remain visible. Lower partial rows inside a scroll viewport are expected.
- Workshop retains substantial field/control text. Attic's wide view makes props tiny; checkpoint text crosses furniture in Parcel. Inspect spatial handles, readability and occlusion in actual motion with players.
- These images do not show a held/crafted attack, facial action, remote character or audible impact. BELT, impact sounds, richer meshes/skins and holding/emotion/animation acceptance remain unfinished.
- Public/private real-client multiplayer, original House networking, full-course recovery, creator usability and measured performance remain separate production gates.

[Lobby review](LOBBY-VISUAL-REVIEW.md) · [Map review](MAPS-VISUAL-REVIEW.md) · [Workshop review](../verification/windows034759/WORKSHOP-VISUAL-REVIEW.md) · [Actual screenshots](REVIEW.html) · [Try guide](../../client-view/TRY-WORDBOUND.md)

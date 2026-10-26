# Final Windows recipe grid and HUD review

STATUS: BOUNDED AGENT VISUAL CHECK PASS at 1280×720; real HUD Craft receipt checks pass. No further UI source change is recommended from this evidence.

Reviewer `story_status`, 2026-10-26. Read-only inspection of actual Windows build `20261026T145311Z`: Workshop run `145731` and equipment run `145634`, compared with Workshop `144717`. No native job, live edit or simulated physical-input acceptance by this lane.

## Actual pictures

Opened `144717/wordbound_equipment_menu.png`, final `145731/wordbound_equipment_menu.png` and final `145731/wordbound_local_world.png` from `docs/qa/runs/*-wordbound_workshop_flow`.

The full-color original pictures are visibly better than the earlier translucent unavailable pictures. The red BALL, blue FAN and green SOFA are clearer; wooden furniture edges and the HAMMER head separate more readily from the cream cards. Item identities and aspect ratios remain consistent. The BAT's light wood is still naturally pale, but the image is recognizable beside its short word. No generated generic replacement is visible in the compared grid.

The four-column grid is substantially easier to scan than the earlier full-width text rows. Each item has one short name. The selected BAT uses a visible border/background and bullet, while `BAT · Need ABT` appears once in the fixed detail strip. Craft and Return remain visible below the scroll region. Resume, Equipment, Help and Search are legible. The lower row is partly clipped by the scroll viewport, with a visible scrollbar indicating more content; this does not overlap the fixed detail/return controls. This frame does not show every scrolled item.

The world image retains the compact three-line status and small Smash/Craft/Menu prompts. The character, floor and checkpoint remain unobstructed by a full-width panel. No visible clipped words or overlapping controls were found in these inspected 1280×720 states.

## Actual Windows checks

Workshop `145731` reports PASS 5/5 and equipment `145634` reports PASS 6/6, both on build `145311`, with zero reported console errors, exceptions and warnings.

The equipment BAT probes now distinguish every guard condition. Selection-only/no-submit passes. For both temporary letter/readiness injections, the command sequence remains 3→3, the menu stays open, canonical MatchSnapshot reference/content and letter values are preserved, and the HUD inventory/readiness are restored. Lost-focus cancellation passes. These are explicitly synchronous QA fault injections into the presentation reference/readiness flag, not observed network migration or real player letter-loss events.

Positive HUD Craft reports **one received command and one accepted BeginCraft**, then resumes. The retained command-receipt file records BAT sequence 4, epoch 0, `Ok`, matching payload. The scenario then passes actual craft completion, held inventory/equipment, eight-phase BAT motion, HAMMER and return checks. Its later HAMMER BeginCraft is sequence 13; the one-command assertion applies to the BAT HUD action, not the entire scenario. The previously failed `144544` run remains preserved separately.

## Limits

This is agent inspection, not James's style approval. The final Windows screenshots are 1280×720 and do not prove physical mouse/controller/keyboard navigation, a small physical window, maximum-font player behavior, Help/Journal states, all long error messages or online recovery. The Editor layout/input cases remain separate evidence. The scripted real HUD action/authoritative receipt proves the command route, not a human button press. Whole-game production, full matches and five original house modes online remain unfinished.

## Evidence hashes

| File | SHA-256 |
| --- | --- |
| `145731` checks.json | `320893e9cda6450744bbb50f8507a82d302c7be0d5faa4a05e2b307b60003e96` |
| Final equipment menu PNG | `a169b95e00c71b4d0368b6be5dc822a50ea8686d7e7198d62c43a2956b69b5a7` |
| Final world PNG | `106cdd621c685877149a6a996f7cd4efe84f12d3886bdd42643ad4f94a439f0d` |
| `145634` checks.json | `1b79a133ec84b339481278d100d0d0600b53f70ff90e742279df6c3047e0356d` |

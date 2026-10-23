# Gameplay HUD readability implementation

Status: SOURCE COMPLETE; independent bounded SOURCE PASS through `0d4bf128`. Isolated `sethypagna/hud-readability` branch from integration `5427493ff5ce8ca0e8971543078e64504a0d4d06`. Parent authorized the three visual corrections plus read-only active binding hints and focused regressions. No heavy execution, generated metadata, camera/art/physics/content changes. Actual Windows visual acceptance remains pending; root owns integration after its count-only comparison.

Latest execution: root native batch `200052` passed all **53 selected UI/input/menu/guide/story cases**, including all thirteen additions. The full batch was **63/65 passed, 2 online failures, 0 skipped**; it is not an overall green result. Actual Windows capture/readability acceptance remains pending. Source-only statements below are the earlier checkpoint history.

## Source checkpoint

- Active hints read the exact `WordboundPlayerView` binding. The automatic binding reads its existing private desktop actions or its already selected pad, including current overrides and localized physical-key fallback. Hints do not poll commands, select a device, create an input map, change bindings, or write preferences. An unavailable selected pad yields no guessed hint until normal input handling changes devices. Raw legacy desktop/pad and unknown bindings retain generic action text because their consumed commands differ from Wordbound's automatic binding.
- The HUD measures cached wrapped rows using its existing label and box styles. Empty status/footer space shrinks to content, while health, timer, objective, host/fault/result status, refusal text and nearby interaction prompts remain. Keyboard/pad labels reflect the consumed map; gear actions appear only when relevant. Panel contents refresh at Layout and reuse content/measurement on Repaint. Existing deferred menus and all command handlers remain unchanged. The existing target query occurs once during preparation, with no new scans, raycasts or materials.
- Checkpoint and finish captions use two lines and a lower anchor; their decorative post ends at 0.7 m below the 1 m caption anchor. Authored sensor positions, ring geometry and rules are untouched.
- Story notices retain their exact words, anchors, current-lap and 20 m visibility rules, journal and banner behavior. Text wraps at 36 columns, uses a modestly larger character size, faces parallel to the camera plane and scales within 0.8–2.0. No camera setting or original artwork changes.

## Focused verification queued for root

Thirteen new native cases are source-complete; none has been executed in this lane:

| Class | New cases | Evidence intended |
| --- | ---: | --- |
| `WordboundInputBindingTests` | 4 | Actual private-map override consumption; no hint-driven input/device mutation; unplug transition remains owned by normal input; localized key fallback; forwarding of known/unknown bindings. |
| `WordboundPuzzleGuideViewTests` | 2 | Actual checkpoint/finish plan caption and post positions under a transformed course; projected anchor separation at three camera poses; no physics components. |
| `WordboundStoryReadabilityTests` | 4 | Actual notice-facing helper at near/mid/far/outside distances; unchanged words and anchor; bounded scale and screen-horizontal baseline. |
| `WordboundHudLayoutTests` | 3 | Actual private panel renderer in EditorWindow Layout/Repaint; wrapped row containment and separation at 1280×720, 1920×1080 and 854×480; shrinking two-row footer. |

Run the four whole classes alongside existing `WordboundMenuGuiTests`, `WordboundPuzzleGuidePlanTests` and `WordboundStoryPlanTests`. This preserves related input/menu and story/progress behavior coverage. Root owns R0 and native execution; no metadata is handwritten here.

These tests cover input identity and layout/projection mechanics. They do not establish glyph readability, occlusion, visual taste, actual Windows rendering, controller usability or owner approval. After compilation/native cases pass, inspect an actual Windows local-ready frame, checkpoint at multiple angles, near/far notice, equipped gear/shield, rebound keyboard controls and pad-switched hints. Keep all current original-art/profile settings.

`git diff --check` passed (line-ending normalization warnings only). Compile/native/player/visual checks: NOT RUN in this lane.

The independent review identified a real compatibility gap in the initial explicit support for raw legacy bindings: their Menu/Cycle/Aim behavior differs from Wordbound. Removed that mapping instead of inventing another legacy contract. The forwarding regression now checks every hint for automatic, raw desktop, raw pad and unknown bindings; only the automatic Wordbound binding supplies specific controls. Online independently closed that finding and reviewed all thirteen new cases and production deltas: bounded SOURCE PASS, no remaining concrete source blocker found. It confirmed Layout/Repaint row consistency, restored GUI state, unchanged command/menu routes and display-only checkpoint/story changes. This is not native or owner visual acceptance.

Source integration order: `966afe07` (active hints), `bc8a0d4b` (HUD/captions/story and report), `0d4bf128` (legacy mapping closure), then this report-only closure. Unrelated lane log remains preserved and unstaged.

## First assembled compile correction

Root R0 `195747` failed before native execution: the new keyboard cases called `WordboundNativeInputScope.Sample(Keyboard, KeyboardState)` but the shared fixture had only its gamepad overload. The bounded follow-up adds the keyboard overload: require the owned device active, queue the supplied real state event and use the same `Pump()` that asserts a Dynamic input update. No direct action triggering or new input update mode is used; the existing test then checks the actual resulting action edges and commands. The shared gamepad path and fixture restore behavior remain unchanged. This closes a source API omission, not a native pass; root rerun is required.

Input independently source-reviewed the `4b8c43b0` repair against the installed Input System API: SOURCE PASS. No compilation/native execution in that review.

## Native evidence: 200052

Independently parsed root `TestResults/editmode_20261023T200052Z.xml` and read its matching `Logs/gate_test_editmode_20261023T200052Z.log`. XML ran 65 cases from 20:01:51–20:03:51 UTC, duration 119.7424084 seconds. The initial R0 `195747` keyboard-overload failure remains recorded above; this later native result verifies execution after its repair.

| Executed class | Passed / total |
| --- | ---: |
| `WordboundHudLayoutTests` | 3 / 3 |
| `WordboundInputBindingTests` | 19 / 19 |
| `WordboundMenuGuiTests` | 2 / 2 |
| `WordboundPuzzleGuidePlanTests` | 11 / 11 |
| `WordboundPuzzleGuideViewTests` | 9 / 9 |
| `WordboundStoryPlanTests` | 5 / 5 |
| `WordboundStoryReadabilityTests` | 4 / 4 |
| **UI/input/menu/guide/story total** | **53 / 53** |

The same batch passed all ten selected local atomic/collision/gameplay cases. Its two separate online cases failed: public TeamRace Solo at admitted client movement (41.298785 seconds), and compact private TimeRun at carrying the keepsake clear of the workbench (60.961645 seconds). They do not invalidate the individually passed UI cases, but neither online completion nor the whole batch is accepted.

No UI case contains a warning/failure output; only the final story case contains the test runner's results-save message. Targeted log inspection found no invalid GUILayout/control-group, missing glyph or missing shader message for this slice. The log is not warning-free: it includes existing obsolete object-search API warnings in workshop/performance QA, a deferred Fusion import-dependency warning, licensing messages and a Unity telemetry hostname resolution error. No new HUD/input/story warning was identified.

XML SHA-256: `C7A64D817657354F10B56EDB0613F6DB4BF5F64370455B3918550047BC97969D`.
Log SHA-256: `CA45D9EFCFB37432C61045845F64FB59E1197476A5ED95D4A8631657A0297D9A`.

These are actual native input/layout/projection regression passes. They do not establish Windows glyph readability, world-label occlusion, real controller usability or owner appearance approval. The next root-owned Windows capture remains necessary; no further source changes or heavy jobs were performed for this review.

## Actual Windows ready-frame comparison: 200812

Independently opened both actual PNGs with the image-view tool: `docs/qa/runs/20261023T193821Z-visible-wordbound_workshop_flow/wordbound_local_ready.png` and `docs/qa/runs/20261023T200812Z-visible-wordbound_workshop_flow/wordbound_local_ready.png` in root integration. Both are 1280×720 ready views of the same Packing Yard start, rear original avatar and nearby checkpoint. Parent identifies the latter as QA build `200608`; this review assesses these images, not the build process or final scenario result.

- The new top-left HUD ends immediately below health/stamina rather than carrying a large empty lower area. The bottom panel shrinks to two readable rows. Both remain within the frame with consistent margins; no visible text/box clipping or overlap in this view.
- The actual visible footer reads `Letters: —` and **[E] Pick up · [Q] Craft · [LMB] Smash · [ESC] Menu**. These are the labels observed in this PNG; Space/Ctrl/M are not shown. This is not a rebound-key or pad-switch capture.
- Packing Yard and its unchanged clue now form three larger, screen-horizontal lines. The earlier notice was smaller and visibly tilted in perspective. The new text is readable at this captured position without covering the avatar or checkpoint caption.
- The checkpoint caption is now two lines (`CHECKPOINT` / `Bring keepsake`) above the short post, with a visible gap. The earlier long post crossed the single-line caption. The ring and keepsake remain recognizable; no new geometry defect is visible here.
- The original rounded red avatar, black satchel/boots, pale cuffs, camera framing, doorway and room materials appear unchanged. The world is still visually sparse, and the avatar still occupies a substantial part of the frame; this bounded UI change does not claim an environment/camera/art redesign.

This single ready view supports the intended static readability improvement. It does not prove motion readability, every camera angle, far/near occlusion, dense notices, equipment/shield hints, different resolutions, controller/rebinding behavior, final frame rate, completed workshop flow, multiplayer outcome or owner style approval. Those remain separate evidence. No art, runtime or QA changes were made during this review.

Before PNG SHA-256: `278EEAC129394112410EC209CBC437B5047DB9C88E723501E8D4EDAE67E182AC`.
After PNG SHA-256: `7DBF75226BD5E481F752BED68A48541468CD64037C260C5B5BF18292088EB28A` (621,080 bytes; last written 20:09:19 UTC on 2026-10-23).

### Final visible-flow result

Subsequently read the completed `200812` checks: **FAIL**, build `20261023T200608Z`, Unity 6000.6.3f1, non-development player. Open party and edit/autosave passed; the saved primary was exactly 183,314 bytes, no backup was used and no temporary file remained. Return-to-workshop passed with the same history/draft, destroyed gameplay client/camera and visible editor. Undo/redo also passed. Recorded console counts were zero errors, exceptions and warnings.

`exact_playtest` still failed its unchanged 90-second deadline while waiting for ninety post-ready Unity update frames. Readiness took 3.619521 seconds (frame 14, canonical tick 35); at Return entry, elapsed time was 90.743923 seconds, only **78/90** post-ready frames had been counted, current frame 93 and canonical tick 4740. The client, controller, frame, world and camera remained active; focus and time scale were normal, with no pause or gameplay/guide/presentation fault reported, 93 visuals and one guide marker. Later world/equipment captures were not reached.

The checks' performance object is empty. These update counts are not presented-frame/FPS evidence and do not establish the cause of the timeout. The static readability improvement is supported by its retained image, but the full Windows flow and performance acceptance remain open. The report's commit/tree fields are null, so source identity must be bound through the separate root build receipt rather than inferred from this checks file.

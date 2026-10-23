# Gameplay HUD readability implementation

Status: IN PROGRESS. Isolated `sethypagna/hud-readability` branch from integration `5427493ff5ce8ca0e8971543078e64504a0d4d06`. Parent authorized the three visual corrections plus read-only active binding hints and focused regressions. No heavy execution, generated metadata, camera/art/physics/content changes. Actual Windows visual acceptance remains pending, and this source will be integrated only after the root's count-only comparison.

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

`git diff --check` passed (line-ending normalization warnings only). Independent source review requested from the online lane; pending. Compile/native/player/visual checks: NOT RUN.

The independent review identified a real compatibility gap in the initial explicit support for raw legacy bindings: their Menu/Cycle/Aim behavior differs from Wordbound. Removed that mapping instead of inventing another legacy contract. The forwarding regression now checks every hint for automatic, raw desktop, raw pad and unknown bindings; only the automatic Wordbound binding supplies specific controls. Independent closure pending.

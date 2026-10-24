# Original artwork in Windows — independent review

STATUS: COMPLETE — all three actual 035159 images personally inspected. Functional scenario PASS; owner style approval and complete visual acceptance remain open. No model, runtime, camera, kit or image edits, generation or execution by this lane.

Scope: actual original four map props and the crafted/equipped BAT and HAMMER in the Windows release-QA player, using the explicitly labelled QA inspection camera. Functional results and visual observations are separate; this is not owner style approval.

## Exact evidence

- Build `20261024T034544Z`, executable source `9e013dc9e766a616ae1d45ed037cbbd863dbf89e`, non-development release-QA, Unity `6000.6.4f1`. Its `build.json` records PASS and 236.3 build seconds; the parent reports 237.3 seconds for the surrounding gate.
- Run `20261024T035159Z-visible-wordbound_original_equipment`: all five steps pass (`equipment_open`, `equipment_world`, `equipment_bat`, `equipment_hammer`, `equipment_return`), final scenario time 13.892215 seconds. Console errors/exceptions/warnings are 0/0/0. This is the supplemental normal-window scenario, separate from the official smoke.
- `equipment-command-receipts.json` contains exactly 13 matching-payload Ok receipts: nine `CollectLetter`, two `BeginCraft`, two `Equip`. The four world props are authored map placements; the held items are actually crafted and equipped. This is not a human-input or remote-peer result.
- Same build's official run `20261024T035028Z-wordbound_workshop_flow` independently records 5/5 PASS and console 0/0/0. Its images were not re-reviewed in this three-image task.
- At the pre-world boundary, actual native and visual player positions are `(0, 0, -1.2)`, keepsake native position `(0, 0, -5)`, with both grounded predicates true against `Floor:Room:0`, floor Y=0, and no player–keepsake penetration. The settle window records 30 distinct observation frames, ticks 19–61. This corroborates the visible grounding in this run; it does not prove every intervening contact or recovery/drop case.

Read directly: the run's `checks.json`, 13-receipt file and pre-world physics JSON; exact build manifest; root records `windows-original-equipment-pass-20261024.json` and `official-short-editor-pass-20261024.json`. The brief capture scenario is not sustained-performance acceptance.

## Direct image findings

| Actual image | Visible strengths | Remaining limits |
| --- | --- | --- |
| `wordbound_original_world.png` | The original red-hooded avatar appears grounded. Pale timber BAT, blue-gray/wood HAMMER, powder-blue FAN and sage SOFA remain recognizable, with consistent original soft forms and warm floor boards. The earlier apparent airborne character is absent in this image. | The room is sparse, with large empty wall/floor areas. The sofa is mainly seen from behind, the small tools occupy few pixels, and the avatar overlaps some space around the bat. This is an inspection arrangement, not finished level staging. |
| `wordbound_original_held_bat.png` | Actual original avatar materials, seam edges, hair, black mittens and boots read clearly. The crafted bat visibly reaches the raised mitten. | Its upper section is hidden behind the head/hood, preventing a full silhouette or contact judgement. A separate large map BAT crops at the lower edge; the placed hammer at left also competes with the subject. |
| `wordbound_original_held_hammer.png` | The original small blue-gray metal head and warm end trim are recognizable next to the raised mitten; the actual equipped visual differs from the preceding BAT capture. | The handle/contact is mostly occluded. The same foreground BAT remains cropped. This single pose cannot approve grip, collision-free animation or ordinary game-camera readability. |

All three images are 1280 × 720, personally inspected without alteration. The top QA banner correctly names the inspection camera and source, but its pale text on translucent dark fill has weak contrast. No missing/pink shader is visible. Visible grounding and model identity are supported here; socket/bounds checks alone do not approve a grip. No new art replacement is justified by these captures.

Small next visual step, if selected: adjust only the QA close-up framing or viewing angle to exclude the foreground map BAT and expose the held BAT/HAMMER silhouette around the mitten; retain original models, authored scale, actual command route and animation. Separately, normal level staging and ordinary-camera equipment readability remain owner-facing work. No such edit was made here.

## Image hashes and gallery preservation

Images live under `docs/qa/runs/20261024T035159Z-visible-wordbound_original_equipment/` in integration:

| File | SHA-256 |
| --- | --- |
| `wordbound_original_world.png` | `07F246C042AFAE29C4C5C0C9955522590EB3B09E82E030B2FE619686274B90C7` |
| `wordbound_original_held_bat.png` | `92686E45FA0022291E74C13AE184DF34636D2D6AE8F17A6499DC60B06A6F8E1F` |
| `wordbound_original_held_hammer.png` | `69A57F97D1590BC1E97AFCC5250BBC1F9A4ACEF9E3E87DF7E3236FDD8BE92163` |

`ASSET-REVIEW.html` now leads with these real Windows views and links actual checks/receipts. Earlier ordinary-camera screenshots, neutral Editor captures, original references/icons and all experimental versions remain. DetailV2 stays owner-rejected in its collapsed archive. The initial airborne Windows capture and the earlier Editor held-capture failure remain linked as historical evidence.

Baseline-only **`6f96f32e` must be excluded from integration**: it copies root gallery blob `1ef693a6181dd3652929aa1e908c8ac641dd227c` exactly before this scoped update. Only the later gallery/report delta should be cherry-picked. No generated files or images are copied into this lane.

Source checks: `git diff --check` clean; all seven distinct new artifact links resolve in integration; HTML IDs are unique and the rejected archive retains a closed `details` element. The revised HTML itself was not browser-rendered in this task.

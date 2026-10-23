# Retirement optimization: independent fixture and player review

Status: REVIEW COMPLETE. Consumed-body fixture repair received bounded SOURCE PASS; actual visible run `202045` remains FAIL at the unchanged playtest deadline. Parent owns build/execution and raw artifacts. This lane owns only this report and read-only independent review. Original Wreckabulary artwork remains the baseline; no visual redesign, runtime/kit edits or heavy jobs were performed here. Unrelated `BuildNotes/flagship-content/focused-first-attempt.log` is preserved.

Source correctness, native execution, visible readability, full-flow completion and performance acceptance are recorded separately below. The fixture correction itself has not been natively rerun as part of this review.

## Consumed TABLE assertion: independent source review

Bounded SOURCE PASS for input-lane `b8e5df98`, following `73b6883b`. Changed only the assertion immediately after the existing successful `UsePuzzle` receipt. No movement, action, wait, deadline, course, physics or production behavior is changed.

Directly inspected root `TestResults/editmode_20261023T201239Z.xml`: compact case failed after 74.411977 seconds with `Sequence contains no matching element` in `WordboundNativeInteractionTests.Pose:149`, called from `CompleteCompactTimeRun:189`. The trace records real native socket completion at tick 1806 and successful ordinary UsePuzzle at tick 1807. The failing selector tried to retrieve the consumed body's pose; no checkpoint/lap completion is established by this run. XML SHA-256: `0D9B53B9AD7AA5F92A9D73E1727908462E6F8F19E8F05175DF3801E6B00149E9`.

`AuthoritativeLifecycle.CompactDestroyedEntities` removes a gone crafted item's body when `ReferencedBodies` does not retain it. A solved mechanism retains its item ledger via ItemEntityId, but only attack hits, thrown-flight items/hits, physical auxiliaries and unsolved mechanism item references retain the body. The repair asserts the exact solved mechanism ID and gone item ledger, explicitly excludes those live references, then requires absence from both the host canonical body list and `host.Controller.Bodies`.

This is the correct host publication boundary: the existing Command helper observes the successful receipt in Controller.Frame; `WordboundGameplayController.Reconcile` unregisters/removes/despawns the planned body and completes snapshot application before assigning/publishing that frame. No premature replica assertion is added. Host dictionary absence is reconciliation membership evidence, not an independent proof of Unity object destruction completing. Later per-lap replica ACK and terminal durable-result checks remain intact. Native rerun is still required.

## Actual visible capture: 202045

Parent reports QA build `20261023T201839Z` completed in 107.4 seconds from source `449f292e`; the run is `20261023T202045Z-visible-wordbound_workshop_flow` under root `docs/qa/runs`. Directly opened its actual local-ready, workshop and party PNGs. Final checks were not yet present at the initial capture inspection, so these observations establish static appearance only.

- Local-ready retains the original rounded red avatar, black satchel/boots, room geometry/materials and camera composition. The compact HUD remains contained and readable, with actual [E], [Q], [LMB] and [ESC] labels. Packing Yard's clue faces the screen; the two-line checkpoint caption stays clear of its short post. No new text clipping, overlap or missing-material appearance was observed. This view does not exercise object retirement or establish frame rate.
- Workshop retains eight separated room labels and cyan geometry contained within its central map viewport. Its visible state says Ready for preview; left controls include Assigned course and Flagship draft, and bottom preview/playtest/history controls are legible. Its PNG is byte-identical to the prior corrected workshop capture. No hover/drag/zoom/input action or autosave behavior is inferred from the image.
- Party is nonblank and legible with the existing guest-name, Connect, Workshop and Back controls. Its large empty lower panel is unchanged in style; no redesign or owner appearance approval is claimed.

SHA-256 receipts:

| Actual PNG | SHA-256 |
| --- | --- |
| `wordbound_local_ready.png` | `DD7766B2E2AA1B1A1C7D5C5795E4A326E8EBE34376B9D7F5F52AC0266F650DED` |
| `wordbound_workshop.png` | `AD72EBA2592686855CB8E79D0156CA3C76E17E10B78BE01D0CAEF85D3D7BE8D0` |
| `wordbound_party.png` | `6B8C44D1F1415F4BBE2CF8639E5FF9A4264F02BA11DAE10056D87FCD1AE7103D` |

## Completed player outcome and identity

Directly read `Builds/qa-20261023T201839Z/build.json`: `ok=true`, release-qa, non-development, Unity 6000.6.3f1, profile `Win-QA.asset`, recorded source HEAD `449f292e68e815b40e03d26dbbaf7a7deab2496a`, tree receipt `0aefa05a2d789c1ff1adcd273afd43f09650a66a41c9a96587b633015c83be86`, build duration 106.3 seconds. The parent wrapper duration was 107.4 seconds. This is a QA player, not an accepted production release.

The status file binds that build's executable to the normal visible 1280×720 launch, scenario `wordbound_workshop_flow`, seed 1, PID 34304, final exit code **1** at 20:22:58 UTC. It explicitly says diagnostic-only and not an official gate replacement. `checks.json` independently identifies the same build/run but has null commit/tree fields, so source identity comes from the build receipt rather than those null fields.

Final checks (20:21:16–20:22:57 UTC): **FAIL**. Open party, edit workshop, return and undo/redo passed. Actual autosave was exactly 183,314 bytes with no backup use, unchanged draft and zero temporary files. Return retained the same history/draft, destroyed gameplay client/camera and showed the editor. The returned workshop PNG is byte-identical to the workshop image above. Recorded console totals are **0 errors, 0 exceptions, 0 warnings**; the player log ends with normal subsystem shutdown after the QA failure, not a recorded crash.

The failed step is still `exact_playtest`, `timed out after 90 s`, during `wait_90_frames`. Readiness took **5.6706 s** at frame 14/tick 49. At Return entry after **90.738307 s**, only **75/90** required post-ready Unity update frames had been counted (current frame 90, canonical tick 4609). The client/controller/world/camera stayed active, time scale 1 and focus true, with no reported pause or gameplay/guide/presentation fault, 93 visuals and one marker. Subsequent world/equipment captures were not reached.

The player log's 15-update samples advance canonical ticks by 900, consistent with the observed 60 simulation ticks per update during this interval. That is scheduling evidence, not a cause attribution or measured display FPS. `perf` remains empty. Compared with the preceding 78/90 run, this run does not close throughput acceptance or demonstrate an optimization benefit; the three-update difference is not a controlled benchmark. Static appearance is preserved, but full-flow/performance, public/full-match outcomes and owner approval remain open.

| Evidence | SHA-256 |
| --- | --- |
| `Builds/qa-20261023T201839Z/build.json` | `2A9B6A87A05D25AA5AE99157820FE244D46CCAC0633D558A5316F02EF038D626` |
| `Logs/gate_build_release-qa_20261023T201839Z.log` | `E4E6434CBA8AF04D14D3C96257910D29A33EFCDC205BD71A7147FEC5FFA6F522` |
| `docs/qa/runs/20261023T202045Z-visible-wordbound_workshop_flow/checks.json` | `53DDF0FF538C219555A83720590B39CF25772A2A54AEB9052930CBDA1F55CEE7` |
| `Logs/player/20261023T202045Z-visible-wordbound_workshop_flow.log` | `79E161778DFA1E0D8C9A810ABC0A364CF74FEABCD9997063B8E7A2507F521A81` |
| `.harness/delivery/20261023T202045Z-visible-wordbound_workshop_flow-status.json` | `BFAD853E577C7E694041304ED7AE33A9D1C0E272A24B4428E9D8B65B6D3DAE1B` |

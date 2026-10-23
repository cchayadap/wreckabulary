# Actual Windows 232850 visual and cadence review

STATUS: REVIEW COMPLETE — direct inspection of all six images and completed raw results. Source stayed frozen; no player, Unity or profiling job was launched in this lane. Functional smoke passes; production performance and owner visual approval remain open.

Build manifest `Builds/qa-20261023T232641Z/build.json` identifies successful non-development release-QA, Unity 6000.6.3f1, source `8c02f106c5e6beb9a72fc35f06d22264a94e504e`, tree `c05e8cee638fc595c326f34bfb81366fbf6c303c75784728bd64706a1f87f501`. The manifest's build duration is 112.5 s; the parent reported 113.4 s gate duration. Captures are the actual 1280×720 images under `docs/qa/runs/20261023T232850Z-visible-wordbound_workshop_flow`.

Independently read `TestResults/editmode_20261023T232239Z.xml`: 97 total, 96 passed, one failed, zero skipped. All nine theme cases and seven party input cases pass, including Search at both font sizes, assigned custom party theme/cleanup and real deferred pad/retry/focus routes. The separate capacity preflight failure keeps the overall native run failed; these UI passes do not close that unrelated result.

## Directly inspected images

- Party: original Nunito headings and strong dark text now share the cream/teal theme with the equipment/workshop UI. Guest name, Connect, Workshop and Back are complete and legible. No gray-gradient remnants or control overlap are visible here. The tall card still has substantial unused space below its initial controls; this is an unfinished composition choice, not a functional defect proven by this capture. Authenticated settings, selected/disabled/fault/retry states and controller focus are not pictured.
- Workshop: `Time Run` is human-readable in the header and mode button. `Preview ready · exact draft and seed` is now fully legible inside the opaque footer. The palette's two-line module button fits. Seed/grid controls and room labels are separated, and cyan outlines remain inside the visible map viewport. The header has tight spacing around its mode label, but no observed clipping. This single unchanged view does not prove every long/custom label, window size, drag or modal state.

- Equipment: Search is now complete on one line beside its field; no overlapping/wrapped letter is visible. The opaque panel, dark labels, disabled recipes, teal scroll bar and Return control remain legible. Removing the extra search line also exposes the complete STOOL row in this viewport. The screenshot does not demonstrate scrolling, a focused controller state, typed filtering or command activation.
- Local ready and local world: original red avatar, black boots/bag, cream cuffs and measured wood-plank scale remain consistent with 230748. The facing story and checkpoint caption are legible, and ordinary E/Q/LMB/ESC bindings remain in the bottom HUD. The later world image shows the timer advancing. The large rear avatar and sparsely furnished first room still look unfinished; no new geometry, grip/material replacement or camera regression is established by this pair.
- Returned workshop: the same edited draft title/seed, viewport labels, readable status and controls remain visible. No new clipping or cyan spill was observed. Return/history/Undo acceptance comes from the checks below, not image similarity alone.

These are bounded visual corrections within the original identity. They are not owner approval, a full art review, proof of every no-code editing interaction, or production-quality sign-off. No fresh appearance-blocking defect was identified in these six views. The tall initial-party card's empty area and tight header-mode spacing are candidates for a later measured composition pass, not reasons to alter networking or controls here.

## Completed functional evidence

The normal-window status records finished/exit 0 at 23:29:44 UTC, diagnosticOnly true and officialGateReplacement false. Actual checks record **five passed steps**, no assertions, zero console errors/exceptions/warnings: open Wordbound, edit workshop, exact playtest, return, and preserved Undo/Redo. Readiness is 4.472 s from playtest request; the 90 required waits finish at 11.926 s and playtest returns at 12.450 s. The checks' `t_s` values are scenario-relative (exact playtest 22.24799), so they are not substituted for this playtest interval. The separate failed capacity native test reports unapproved zone asset IDs and is not closed by this player smoke.

## Actual opt-in cadence and settings

The frame observer completed its intended screenshot-excluding 90-wait interval: **7.240 s, 90 Update samples, Unity frame 14→104, rendered counter +90, canonical tick 39→518 (+479)**. Mean update gap is 80.411 ms; maximum is 696.499 ms with 46 canonical advances. The first nine reported worst samples are early in the window and commonly carry 13–46 ticks. Later worst gaps also include 148.759/151.590 ms with only one tick, so catch-up work does not explain every individual gap from these samples alone. Missing/unpaired snapshots, epoch changes, tick regressions, duplicate Updates and unfocused samples are all zero. The final unsampled tail is 2.605 ms. This is about 12.43 ordinary update samples/second over the observed interval, not presented FPS.

The assigned gameplay camera records **89 begin and 89 end callbacks on 89 distinct frames**, all 89 paired in the same frame, zero unmatched/cross-frame/in-flight events. Last camera frame is 103; the observer closes after Update 104, before that frame's later render stage, which is consistent with the intentionally unchanged stop boundary. The measured callback wall interval totals 249.891 ms, maximum 6.895 ms. It excludes GPU completion/presentation and can overlap CPU scheduling. The difference from 90 Update samples is not evidence of a dropped camera render.

Beginning and ending settings agree: quality index 0 named PC, v-sync 0, target FPS -1, render interval 1, display refresh 120 Hz, windowed 1280×720, focus true, run-in-background true, timeScale 1, frame timing unavailable/disabled. GraphicsOptions selects unlimited cap, scale 1, MSAA4 and High shadows; actual URP scale 1/MSAA4, shadow distance 50 m/four cascades/2048 map. Camera is active at 1280×720 with no target texture, display 0, culling mask -1, occlusion/HDR/MSAA/post-processing enabled, FOV 55°, near .1/far 1800, perspective. `cameraAA=None` is the URP post-process antialiasing selection; it does not negate the separately recorded MSAA4 pipeline setting.

The wider ready phase includes screenshots and menu work: 7.979185 s, 502 HostStep attempts / 4,783.849 ms inclusive elapsed; FullValidation 520 / 1,443.188 ms. Do not subtract or sum nested scopes against the narrower frame window. The observed settings show no intentional one-frame-per-second cap or render suppression at either boundary. Sparse updates with multi-tick advances, synchronous Fusion scheduling and substantial HostStep time support game/simulation catch-up as a material contributor. Camera callbacks do occur regularly relative to Unity frames and their measured spans are much shorter than the worst update gaps. This still does not isolate exclusive CPU, GC, native physics, render-thread/present waits or GPU cost. No setting/tick/deadline tuning is justified solely by this result.

## Artifact binding

| Artifact | SHA256 |
| --- | --- |
| Native 232239 XML | `D58DB46E043D0AF49B17D25FE041BF94C2F9B47CDC93673CD87908A0B504F8B0` |
| Build 232641 manifest | `21E90D19580D75E35F67444AFF44F6B81C745DD8A94CF6AEE83C4A8B605C9850` |
| 232850 player log | `29D19B5D3D1C1F5B2556BC50CC0A28122E81DDEE63AB96D29219184BF8624674` |
| 232850 checks | `82E97992A4925158454F4541FE8EF1C5D69C693DA2C390B8497A8A3EFFC6E410` |
| Party PNG | `6193F1345A9D19C558833AF8339C00299FD604E127D951B47E1CE1E391785761` |
| Workshop PNG | `AA2F2274159EAF749F669F62A672C653A26EF4AAA4EFB60A9C1EC495C8F6A0F2` |
| Local ready PNG | `451337E672B8D293CE24832239ED20107B8A5C0F744D27D4DD9B045B72A13FB1` |
| Local world PNG | `E6FC54F533ABF07ACB46EC0288EDC2B56D4CB3D8FF0E9957041B3DE65C041CD0` |
| Equipment menu PNG | `8DE8C1AB5780F32D36A3F6FE329749E37DA39A9ADF10FAADC7F535F6005D73E0` |
| Returned workshop PNG | `F34381A2767094562C812FD7A5D1A04E5B0F252D74A94968BA85C4375DA21E8A` |

## Authorized small composition follow-up — source only

The initial disconnected party card now measures its actual non-stretch GUILayout content group during Repaint and applies the resulting bounded height only on the next Layout. A changed theme generation or width discards the old measurement. The first measurement can use the full-height card; connected and active-match paths retain the full scrollable rectangle. Required errors/retry/help controls remain in the same content method and order, and a tall/narrow result is capped to available height with scrolling preserved. No new action, focus name, network operation, wait, camera/art setting or scene change is introduced.

Guest help now says `Connect to find matches or join friends.` Workshop mode help becomes `Preview your selected mode and team size.` The status says `Preview ready · current map and seed`, and the workshop header uses the theme's existing gap after its heading and around the mode label. These changes affect player-facing wording/spacing only; mode IDs, rules and exact saved map contracts are unchanged. Independent review corrected the first proposed wording because the separate Solo/Duo/Squad toolbar can change team capacity within the same mode.

Two focused cases draw the actual disconnected party at 700/420 px requested window widths, verify measured content fits a shorter card, and mutate the cached measurement inside a real Repaint to verify the card rectangle does not change until Layout. Existing actual connected-party/controller cases remain required. Input's bounded source review found no layout/control blocker: measurement and compact grouping freeze on Layout, width/theme invalidation and scrolling remain, and action/control ordering is preserved. These source changes have not been compiled, run natively or captured in a player yet; the six images above remain the preceding baseline. Required native filters: `Wreckabulary.Online.Tests.WordboundPartyMenuInputTests` (9 cases) and `Wreckabulary.Online.Tests.WordboundUiThemeTests` (9 cases), followed by actual disconnected/connected party and workshop captures.

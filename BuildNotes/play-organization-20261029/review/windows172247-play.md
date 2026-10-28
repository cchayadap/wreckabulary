# Windows172247 Play review

STATUS: BOUNDED VISUAL REVIEW COMPLETE — 11 NEW IMAGES OPENED

Inspected all 11 requested Play/Online/Home images from Windows build `20261028T172247Z`, run `Docs/qa/runs/20261028T172356Z-lobby_experience`, using `view_image(detail: original)`. All 11 PNG headers independently confirm 1280×720. Also reopened the two corrected states from build170019 for direct comparison. Checkout was `sethypagna/wordbound-integration`, HEAD `74ffae0359766d04d1632728f5da0a7f72457887`, with no tracked dirty files at review start.

No live source edits, native execution or prior evidence changes. This report is the only write made for this review. Original images, including any provenance or trial labels, remain unchanged. This is independent screenshot inspection, not owner approval or a production-readiness verdict.

## Result

The two intended corrections are visible. Escape's Random house and Create's Wordbound maps cards now use the compact, left-aligned House-card footprint instead of stretching across the entire row. The editor caption reads `Build & playtest` and fits. Their selected check/border, queue/family headings and fixed Play/Open actions remain visible and consistent with the prior build. The corrected card dimensions do not move those actions.

No confirmed clipping, overlapping essential controls or conflicting selected states in the 11 new captures. The nine Play-family captures omit the avatar; Home restores it. Local/Online/Create and Close are visible. Roboto labels remain readable. No decorative stripes are visible on these Play surfaces. Static images support these visible states, but do not independently prove activation, keyboard/controller traversal or gameplay routing.

## Remaining findings

1. **P2 — Sparse composition remains.** Ordinary House and Solo screens still have roughly 170–185px of empty vertical space between the cards and footer. Create Wordbound leaves over 300px below its card, and the single-choice Escape/Wordbound states now also expose the unused horizontal area to the right. The uniform card footprint fixes the oversized card, not the owner's broader request for a fuller composition. Learn's separate full-width room note remains unchanged and should not be described as fixed by the grid correction.
2. **P2 — Solo map artwork still identifies objects rather than environments.** Lost Address shows BOX; Parcel Hall shows TABLE. Both names and selection states fit, but the images do not help compare the actual courses. The Random house uses a generic home icon. These are presentation limits, not evidence that the underlying maps changed.
3. **P3 — Online entry remains a different visual system.** Its bold Guest name/Connect/Workshop/Back form contrasts with the Roboto card navigation. It fits in its panel, but the transition is visually inconsistent. This capture does not demonstrate a successful connection or matchmaking.
4. **P3 — Minor repeated Create labels remain.** Learn repeats `Learn`, and Build a home repeats `Build` below its title. They add little information. The Wordbound editor caption was shortened successfully without implying online or multiplayer playtests.

## Exact image coverage

All 11 new files are under `Docs/qa/runs/20261028T172356Z-lobby_experience`.

| File opened at original size | Observation |
| --- | --- |
| `lobby_local_dibs.png` | Dibs and Pinwheel selected; five mode and five map cards fit; footer states You + 3 bots. |
| `lobby_local_duos.png` | Duos and Pinwheel selected; footer states You and a bot vs 2 bots; no conflicting check. |
| `lobby_local_escape.png` | Escape and compact Random house selected; New each run fits; fixed Play remains. |
| `lobby_solo_timerun.png` | Time Run and Lost Address selected independently; both map names fit. |
| `lobby_solo_freestyle.png` | Freestyle selected; Lost Address remains selected; action and short summary fit. |
| `lobby_solo_two_seats.png` | Local 2/4, disabled Play and red solo-only reason agree; no warning box or overlap. |
| `lobby_create_learn.png` | Learn selected; tutorial-room note remains full width; footer clearly says Solo. |
| `lobby_create_build.png` | Build a home and Pinwheel selected; Garden available, other three maps visibly disabled. |
| `lobby_create_wordbound.png` | Compact editor card selected; Build & playtest fits; fixed Open and one-player playtest note remain. |
| `lobby_online_ready.png` | Guest form and Connect/Workshop/Back fit; legacy visual style persists; connection not attempted by this scenario. |
| `lobby_home_returned.png` | Original red-hood character restored; local 1/4 and Creative workshop / Pinwheel House context visible; short Player 2 left toast. |

Reopened direct comparisons: `lobby_local_escape.png` and `lobby_create_wordbound.png` in `Docs/qa/runs/20261028T170223Z-lobby_experience`. Both prior states used full-width single cards. Prior images/reports were not modified. Other 170019 states were assessed in the earlier report; they were not reopened during this pass. The preferred232801 baseline was not reopened in this bounded correction review.

## Automated evidence and limits

Independently read the exact run's `checks.json`: all five steps PASS, zero assertion failures, 24 declared captures; console reports zero errors, exceptions and warnings. Checks SHA-256: `bec8a0c9c3821189420726308adc6ad9cc7115f741b38b5e46612d843a5deed0`. Local probes report preserved original selection/bindings and the two-seat solo guard; Create probes explicitly cover selection only, without launching creators. Online probes explicitly exclude Connect, queue, room and match requests. These scope limits remain in force despite the scenario PASS.

This pass visually covers 11/24 current lobby images, plus two earlier comparison images. Customize, Shop and Settings are outside this report. It does not verify other resolutions, motion, controller input, screen-reader use or real online play. Root reports the dedicated width/selection/footer Unity test passed; that native result was not rerun by this reviewer. Next useful visual work is actual course thumbnails and deliberate sparse-state composition, followed by the Online styling pass. No new implementation blocker was identified for the bounded card-width correction.

# Green online UI commit draft

STATUS: REVIEWABLE DRAFT. No staging or commit performed by this lane.

Suggested title: **Keep online creation and recovery actions visible in compact layouts**

Create shows the selected mode and human composition with its primary action visible at 854×480; advanced region/fill/map options live under Match settings. Expanded forms immediately retain their full scroll area. An uncertain request shows a compact recovery surface with Retry, clearing the prior scroll position. Search labels distinguish canceling a search from canceling an assigned reservation. Saved theme/background controls and coordinator rules stay intact.

Verified evidence:

- Native EditMode 192038: 53/53 passed, zero skipped; 39 cases cover menu focus/GUI/party input/theme, with 14 existing checkpoint/lifecycle cases in the same gate. The earlier expanded-layout run 191259 failed 5/53 and remains recorded in green-ui-receipt-draft.json; it is not relabeled as a pass.
- Native capture 193149: 1/1 test passed, producing 21 actual GameView images with simulated responses. Receipt PASS, zero cleanup errors. All 21 PNG hashes and 16 source/theme/font hashes independently match. Runtime menu source matches the captured versions.
- All 21 original images independently opened. Compact Create, large-type primary actions, Cancel search and scrolled-form-to-Retry are visible. REVIEW.html offers local state/size selectors without changing image bytes or loading external resources.

`green-ui-staging-paths.txt` is an exact proposed allowlist: five changed UI/runtime/input/capture source files, the final 21 images and receipt, the independent visual report, local review page and this small evidence draft. It intentionally excludes Launcher/failure-input files, WordboundQa, FFA diagnostics, root progress, other asset work, earlier prototype candidates and unrelated changes. Those remain untouched and available for their own checkpoints. No new .meta is needed for these five already-existing source files.

Before committing, root should confirm the allowlist still matches the intended diff and hashes in the receipt. Do not use a recursive add of Assets or BuildNotes. TestResults/Logs remain local ignored evidence; the compact receipt records their exact hashes/counts and the retained failure.

Limits: these are synthetic UI states, not public networking, Windows delivery, physical-input or full-match acceptance. Advanced/private forms still have many outlined controls; lower secondary navigation may require scrolling at large type. Final human visual approval and broader multiplayer/production goals remain open.

# Windows workshop range audit

STATUS: COMPLETE — BOUNDED FLOW PASS; VISUAL HOLD, NOT PROMOTED.

Independently read the raw build/smoke receipts, five-step checks, player log, saved draft, packed-file report and committed scenario source. This audit ran no native job or service call and changed no live source or previous evidence. The [machine-readable receipt](windows-workshop-ranges-20261027.json) records hashes, all 227 current packed-file hashes, eight capture hashes, source comparisons and acceptance limits.

| Evidence | Verified result |
| --- | --- |
| Windows build | `20261026T231729Z`, release-qa, Unity 6000.6.4f1, PASS in 155.9 s; wrapper exit 0, no signal |
| Reported build revision | `fe7f1738f7223b52393eb841c61a01867c53e396` |
| Workshop smoke | `20261026T232018Z-wordbound_workshop_flow`, explicitly selects that build, seed 1, 90-second gate budget; PASS in 62.2 s |
| Player | Exit 0, not timed out; exact run-specific log identifies the same build directory and normal shutdown |
| Scenario | All five expected steps PASS, no assertion failures, eight actual PNGs |
| Capture containers | All eight paths are inside the exact run folder, PNG headers and 1280 × 720 dimensions match; no missing/extra PNG |
| Packed output | All 227 recorded files exist with matching recorded sizes: 262,458,029 bytes; required Hub, Wordbound lobby, GameData and scenario source appear in packed-asset contents |

The five steps are `open_wordbound`, `edit_workshop`, `exact_playtest`, `return_to_workshop`, and `undo_redo_preserved`. `checks.json` itself has null commit/tree fields, so this audit links it through its exact build/run IDs, gate arguments, build metadata, Unity CLI provenance and player log rather than inventing a test-level source hash.

The range capture inspected `Props_Attic_BookSpring` in its owning Attic room: height 16,000–16,000 mm, four yaw choices, and Attic permitted. Actual scroll positions were 748 and 986. All eight recorded booleans are true: seven individual checks for view fields, history, snapshot, revision, Undo/Redo, primary bytes and manifest, plus their combined `preserved` result. Cleanup errors are empty. The committed source uses the real selection handler, temporarily sets view fields, and restores/checks them in `finally`; it never applies edited ranges. This is view/preservation evidence, not physical-pointer or changed-range Apply coverage.

The primary autosave was mandatory, not optional: the scenario awaited its exact edited text, recovered it through `WorkshopStorage`, required no backup and zero atomic temporary files, and copied its actual bytes into the run. The copied evidence is **183,332 bytes**, SHA-256 `05005f79fa73dcf827222a68a8844d9d428346a8a8661b275aea18403352d5ed`. Its parsed draft has the expected title, locked seed `0314159265358979`, Attic selection and layer. At audit time the exact named temporary primary still existed, was byte-identical to this evidence, and had zero matching temporary files. The private temporary selector remains in the original checks, not this report.

Playtest used manifest `146a289906d1cf3840a9985ce0a38e4ae968889b9e1cb1b60808333ed519e77f`, and exact manifest/draft assertions passed. Readiness was recorded after 3.373392 s at tick 27. After 90 rendered updates, the completed observation was 5.724458 s at tick 179, with 93 visuals, one marker, active camera and no gameplay/guide/presentation fault. It was focused and unpaused. Return awaited actual local shutdown and then verified the same history/draft, destroyed client/camera and visible editor. Undo restored the exact initial recovery JSON; Redo restored the edited recovery JSON. These are scripted local-entry/return contracts, not full course completion.

Scenario console counters are **0 errors / 0 exceptions / 0 warnings**. The full raw player log also contains four early unsupported/stripped postprocessing-shader diagnostics covering three shaders: Gaussian depth of field twice, Bokeh depth of field, and Panini projection. The log says their postprocessing passes will not execute. The zero scenario counters therefore must not be described as a completely diagnostic-free player log. QA PASS, input-system shutdown, backend cleanup and `CodeReloadManager destroyed` are present.

Source and binary provenance remain bounded. Unity CLI provenance reports the revision above with **dirty=true**. `build.tree` is a hash of path/size/mtime metadata, not a complete content snapshot. The packed report records paths, roles and sizes; this auditor calculated content hashes **now**. No historical per-run executable hash was present in these artifacts. Current hashes do not independently prove the exact bytes at the earlier launch. Ignored/untracked build inputs are not certified by the Git revision. The two current files `WordboundWorkshop.cs` and `WordboundWorkshopQa.cs` now differ from the build revision because root has started the subsequent clipping correction; their current hashes are separately recorded, and this audit reviewed the committed `fe7f1738` scenario contract.

[Independent review of all eight images](windows-workshop-231729-visual-review.md) found the new range controls readable but the existing Group / Ungroup / To this floor row clipped in normal and returned Workshop views. Those two PNGs are byte-identical. It also retains sparse room dressing, light checkpoint text, and an unexplained visible trial-version overlay as limitations. That visual report and its JSON are hash-pinned in this receipt; this auditor did not substitute container checks for pixel review.

**This build is retained as passing flow evidence, not promoted.** Root is preparing the toolbar correction and a fresh build. The earlier review build `221541` remains the current tested delivery until combined acceptance is ready. Root separately owns verification of all 4,208 authored files against `.harness/delivery/ui-authored-before-2026-10-26T23-17-28-898Z.json`; this report does not claim that comparison. It also does not establish physical controls, other resolutions/fonts, full puzzle/story completion, public multiplayer, final art or production readiness.

Raw gate prefixes: `.harness/delivery/workspace-windowsbuild-2026-10-26T23-17-29-017Z` and `.harness/delivery/workspace-workshop-2026-10-26T23-20-18-141Z`. Exact run evidence: `Docs/qa/runs/20261026T232018Z-wordbound_workshop_flow/checks.json`; player log: `Logs/player/20261026T232018Z-wordbound_workshop_flow.log`.

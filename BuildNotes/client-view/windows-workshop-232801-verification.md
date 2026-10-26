# Corrected Windows workshop verification — 232801

STATUS: FLOW AUDIT COMPLETE — PASS; SEPARATE PIXEL REVIEW PENDING.

The corrected Windows build and its new workshop smoke pass the bounded evidence audit below. This review ran no native job or service call and changed no live source or historical evidence. [The JSON receipt](windows-workshop-232801-verification.json) pins the raw inputs, current hashes of all 227 packed files, eight actual images and relevant source files.

| Evidence | Independently verified |
| --- | --- |
| Build | `20261026T232801Z`, release-qa, Unity 6000.6.4f1; gate PASS in 139 s, wrapper exit 0 |
| Reported revision | `ec4b127b23c9d084aa281f2248a362d92b313399` |
| Workshop | `20261026T233042Z-wordbound_workshop_flow`; explicit build232801, seed 1, unchanged 90-second gate budget; PASS in 55.6 s |
| Process/console | Normal exit 0, no timeout; scenario counters 0 errors / 0 exceptions / 0 warnings |
| Steps/captures | Five expected steps PASS without assertion failures; all eight exact PNG paths exist at 1280 × 720, with no extra/missing run PNGs |
| Packed output | All 227 recorded files exist with matching recorded sizes, totaling 262,458,541 bytes |
| Source readback | Current tracked tree clean; scenario and three Workshop source files normalize exactly to the saved build revision |

The correction is limited to two source files since the prior build: Group/Ungroup retain a two-button row, and **Move to floor** gets a separate row while retaining the existing `ChangeLayer` callback. The scenario advances its two capture scrolls by exactly one row, from 22/29 to 23/30. No deadline, gameplay requirement or preservation assertion changed. This source review does not itself establish that the layout looks correct; the separate image review does that.

The range views recorded actual scroll positions **782** and **1020** for the existing `Props_Attic_BookSpring` zone. Its base height is 16,000–16,000 mm, yaw choices are 0/90/180/270 degrees, and permitted floor is Attic. All seven individual checks—view fields, history, snapshot, revision, Undo/Redo, primary bytes and manifest—and the eighth combined preservation result are true. Cleanup errors are empty. These are real selection-handler and view-restoration checks; the scenario does not click Apply or physically operate a pointer/controller.

The mandatory autosave copy is **183,332 bytes**, SHA-256 `05005f79fa73dcf827222a68a8844d9d428346a8a8661b275aea18403352d5ed`. The scenario awaited its exact edited primary, recovered it through `WorkshopStorage`, required no backup and zero temporary atomic files, then copied those actual bytes. The saved draft contains the expected title, locked seed `0314159265358979`, Attic layer and selected owner. This auditor also read the exact retained primary: it matches the copied evidence byte-for-byte and has zero corresponding temporary files. The private temporary selector remains only in the original checks.

Playtest retained exact manifest `146a289906d1cf3840a9985ce0a38e4ae968889b9e1cb1b60808333ed519e77f` and unchanged draft. Readiness occurred after **3.624263 s at tick 21**; after 90 rendered updates the completed observation was **5.367928 s at tick 131**, with 93 visuals, one marker, active camera, focused/unpaused state and no gameplay/guide/presentation fault. Return awaited actual local shutdown, then verified the same history and draft, destroyed client/camera and visible editor. Undo restored exact initial recovery JSON and Redo restored exact edited recovery JSON. These checks validate the scripted local entry/return route, not full puzzle completion.

The exact run-specific player log identifies the new build, records QA PASS and normal input/backend shutdown. Despite zero scenario console counters, it still contains **four early unsupported/stripped postprocessing-shader diagnostics**: Gaussian depth of field twice, Bokeh depth of field and Panini projection. The log says those passes will not execute. This is not described as a completely diagnostic-free log.

Provenance limits are explicit. `checks.json` has null commit/tree; its exact build/run IDs, gate arguments, build metadata, Unity CLI provenance and player log supply the link. Unity CLI reports the build revision with **dirty=true**, even though current tracked source is clean and the four inspected source files match that revision. No historical clean whole-tree claim follows. `build.tree` uses path/size/mtime metadata, not all input content. The packed report provides paths/roles/sizes; the SHA-256 values in this audit are **current readbacks**, not historical hashes captured at player launch. Ignored and untracked inputs are not certified by the Git revision.

The prior [231729 audit](windows-workshop-ranges-20261027.md) and its JSON were rehashed and remain unchanged. The corrected build does not replace the current reviewed delivery solely from this workshop result. Root separately owns combined lobby/Solo verification and all **4,208 authored files** against `.harness/delivery/ui-authored-before-2026-10-26T23-28-01-405Z.json`; this audit does not claim that comparison. Physical control testing, changed-range Apply in Windows, other resolutions/fonts, full story/course completion, public multiplayer, final art and production readiness remain outside this evidence.

Raw gate prefixes: `.harness/delivery/workspace-windowsbuild-2026-10-26T23-28-01-680Z` and `.harness/delivery/workspace-workshop-2026-10-26T23-30-41-526Z`. Exact run evidence: `Docs/qa/runs/20261026T233042Z-wordbound_workshop_flow/checks.json`; player log: `Logs/player/20261026T233042Z-wordbound_workshop_flow.log`.

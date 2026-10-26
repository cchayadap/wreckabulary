# Windows lobby experience scenario

STATUS: SOURCE INTEGRATED; first native compilation found a missing namespace, corrected by root. Windows scenario remains pending. No native process, build, service call or player run performed by this lane.

New project-owned scenario: **`lobby_experience`**. Root can run its normal Windows smoke gate with the requested 90-second process timeout after compiling the exact candidate. This draft changes no Packages or Tools/kit code and does not create a separate test runner.

Current `lobby-experience-qa.patch` is `adfc26bceb0cf888b18e04cc8ad78df180f3412c1c39dd1c2beb8d63830e09c5`, including root's one-line namespace correction. The separate `qa-ui-references.patch` remains `2d0ff5686175df192b349e3fa51b6339bf68ce887e8ca78ff114037eb388bde1`. Both are already integrated; strict reverse checks pass against the exact current candidates. The reference patch adds existing `Unity.TextMeshPro` and `UnityEngine.UI` references to the project QA assembly; it changes no assembly constraints. Unity generates new C# metadata. `manifest.json` pins baseline and candidate hashes. The first revision is preserved in `review-v1`.

Requires integrated Play choices, Settings revision 2 (visible Customize action, stable `Open loadout` name), and root's updated Home/navigation source. Strict apply checks passed before integration. `syntax.json` parses the actual code with both `UNITY_EDITOR` and `GSK_QA` preprocessor symbols active using C# 9: zero syntax errors. Semantic compilation and native behavior remain pending.

Native attempt `154725` caught an API-resolution error missed by syntax-only review: `HomeDesigner` is in `Wreckabulary.Rules`. Root added `using Wreckabulary.Rules;` in the live scenario. The owned candidate now mirrors that correction (SHA256 `a2ddfae274affdf1b05007ac7702659bfd64cc4eafaecdda29ab17ecc0ad8351`). Root's R0 `154903` passed afterward. Preserve `Logs/gate_test_editmode_20261026T154725Z.log` and `Logs/gate_check_all_20261026T154903Z.log`; syntax checks never established semantic API correctness, and a Windows run is still needed.

## Five actual UI steps

1. **local_choices**: real top PLAY Submit; Local/Online/Create labels; exactly five original house modes; submit every mode, inspect selected mode/map and live bot summary; no GO/countdown. Screenshots: Dibs, Duos, Escape.
2. **create_choices**: Create submits to Tutorial/Creative workshop only; tutorial has its own room, workshop keeps supported house, neither launches. Two screenshots.
3. **customize_and_shop**: Settings Profile's visible Customize button submits to the real Loadout, then visible top Shop opens Shop. Two screenshots. No purchase, cosmetic change or unlock.
4. **settings_navigation**: submit all six tabs; selected tab retains EventSystem focus; Controls owns its gameplay input lease and leaving releases it. Close-page button returns to Home and focuses the real Settings opener. Six category screenshots plus returned Home.
5. **open_wordbound**: real Play Online button loads WordboundLobby. Initialized coordinator, launcher callback and rendered guest menu required. Existing `WordboundPartyFrameObservation` proves splash ended, at least two menu repaints and assigned-camera frames before/after the final screenshot. The exact `open_wordbound` step name is required by that existing observer. Guest remains unauthenticated, party null, match inactive. One screenshot.

Expected total: **15 screenshots**, produced by the ordinary player capture path. Each PNG must exist, decode, match actual Screen dimensions and have varied RGB pixels. Capture probes include actual resolution, focus state and rendered frame number. The local UI checks top navigation within Screen.safeArea and disjoint button rectangles; selected mode/footer and critical button text must fit. No blanket overflow assertion is made against scroll contents. Root's two-row navigation is accepted by rectangle geometry, not hard-coded to one row.

## Preservation and honest limits

Only navigation and runtime local mode/map selection are intentional. Known profile/outfit, TurnHint, graphics, audio, input override, sensitivity/inversion, combat feedback and theme-motion preferences are snapshotted with their source-defined types after the lobby is ready. Every step checks equality in finally; an unexpected mutation is restored to its original type/value/existence and fails the step. This is not a complete registry snapshot. No resolution, setting, binding or purchase callback is invoked.

The scenario requires a regular WindowsPlayer window; it refuses batch/Editor execution. UI Submit callbacks exercise the application’s actual button actions but do not establish physical keyboard/controller behavior. It does not connect a guest, send a queue/room request, accept a custom map or launch any online match. No full-match, recovery, capacity, human visual acceptance or five-original-house-online claim follows from a pass.

The root should inspect the PNGs after the gate, preserve any failed run, and run the existing relevant regression suites. No timeout relaxation or existing assertion removal is included.

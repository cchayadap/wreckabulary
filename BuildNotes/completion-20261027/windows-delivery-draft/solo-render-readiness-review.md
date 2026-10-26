# Solo render readiness correction review

STATUS: IN PROGRESS — source-only review of root's WordboundSoloQa correction against source commit c44a39a0e1b81b524c691f2a1f5dda5de31e3d6a. No live edits, native jobs or real delivery-verifier execution by this reviewer. The earlier Windows Freestyle failure remains separate evidence.

STATUS: COMPLETE — no blocking source finding in this bounded QA-only correction. This is source clearance for a fresh root-owned Windows run, not a new passing run or visual acceptance.

Reviewed live `Assets/_Project/Scripts/WordboundQa/WordboundSoloQa.cs` SHA-256 `d96ae55351bc78f35336f4f067f75eda6f2082b2baaaab5bcac94da71f4a85a3` against commit `c44a39a0e1b81b524c691f2a1f5dda5de31e3d6a`. The diff changes only readiness and its diagnostic note. Launch, actual Single runner/content binding, camera callbacks, return, nested iterator cleanup, preferences/source preservation and all deadlines remain unchanged.

Confirmed contracts:

- The new expected count uses the actual resolved HUD course, current canonical `MatchSnapshot`, and the single canonical player. The preceding fixture assertions require exactly one player and installed content/binding. `WordboundPuzzleGuidePlan.Build(MatchSnapshot)` rejects mismatched source/manifest hashes and a player outside the bound roster; it does not return a synthetic expected constant.
- Freestyle delegates to `WordboundPracticeGuidePlan.Build`. It selects the nearest station within 20 metres for guidance; with no selected station and no nearby station, its honest plan is empty. Its nearest/selected markers are mode-specific and do not invent race checkpoints. The production guide calls the same plan on `FrameChanged`.
- Zero cannot pass solely because `MarkerCount` defaults to zero: the fixture additionally requires an enabled/active component, an actually created `WordboundPuzzleGuideView`, an empty fault, and equality to the current plan. Production `Refresh` disposes that view when the controller/world is unavailable, paused, disabled or faulted, and validates the world manifest before creating it.
- TimeRun retains a strictly positive expected marker count. Both modes retain at least three completed renders from the assigned camera, strictly more than five canonical ticks, and the original 12-second readiness bound. The subsequent enabled camera/no target texture/fault checks remain.
- The nested `finally` records the last evaluated plan count, observed rendered count, camera count, tick delta and fault even when the wait fails. The existing flattened iterator guard disposes nested iterators, so this diagnostic survives an ordinary bounded wait failure. It is not a guarantee after abrupt process termination or a stalled frame.

Evidence boundary: this asserts marker-count agreement and a live presentation object; it does not independently compare every marker identity/position or prove visible pixels. The assigned-camera captures and human visual review remain necessary. Calling the production planner is appropriate for this integration-readiness check; separate guide-plan tests cover course/roster rejection and mode semantics. No tests or native jobs were executed by this reviewer.

Prior failure remains preserved and unresolved as to its exact conjunct: `Docs/qa/runs/20261026T202643Z-wordbound_solo/checks.json` (SHA-256 `b5709366ff1b8090d688f17b5770e9560a5be4ac69721c2fb7fb48fbaac127cd`) belongs to build `20261026T202047Z`; selection and TimeRun passed, Freestyle failed with `timed out after 12 s waiting for fresh assigned camera frames, simulation and guide`. That receipt does not separately record camera/ticks/marker values for the failed condition. Source proves that the old unconditional positive-marker requirement was too broad for Freestyle, but does not prove it caused this particular failure.

Read-only supporting hashes:

| Source | SHA-256 |
|---|---|
| WordboundPuzzleGuidePlan.cs | d2de4e0a919079104e3b6bcbb7297fcc047b36441e2b8e6ff6cfdd699a00f832 |
| WordboundPracticeGuidePlan.cs | 58adb611638c3e667c33cd99b83f293b1aa6604af1686b515af41e5e7792f7fd |
| WordboundPuzzleGuide.cs | 002ccb02774cfe5822fc3932676495770f6272701e6a8859d35125f05f64ad82 |

Next acceptance: root rebuilds and runs the unchanged Windows solo scenario; inspect its new per-mode readiness notes, actual world/menu/return captures and cleanup receipt before making a new delivery claim.

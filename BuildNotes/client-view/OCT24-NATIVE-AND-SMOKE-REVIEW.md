# October 24 native and smoke evidence review

STATUS: COMPLETE — evidence review; native and official smoke acceptance remain incomplete.

Independent read-only review began at integration HEAD `f865c037ea27a70889097384bfc64e9784164642`. No Unity, player, build, compiler or performance jobs were run. Only this report was changed. Existing review chain `6b4d714e` → `ec989cc3` → `43775d8f` remains separate and intact.

## Native 010750: exact inventory and corrected claim

Raw `TestResults/editmode_20261024T010750Z.xml` contains **26 cases: 24 Passed, 1 Failed, 1 Skipped**. The 24 passes are 22 ordinary authored-content cases, `MeasureObservationLookupIncludingConstruction` (2.190540 s), and the isolated CLOCK floor/occlusion case (0.747198 s). The six-family flow is **Skipped** (0.054452 s), with reason: `Set WORDBOUND_NATIVE_PUZZLES=1 for the native authored six-family interaction proof.` Public TeamSolo failed (43.506172 s).

The original uncommitted JSON verificationScope had swapped the six-family/explicit-measure outcomes. Root corrected it during review. All 26 current JSON case results now match XML, and all six recorded native artifact sizes/hashes match disk. This run does **not** repeat the full six-family proof. Gate exit1/85.7 s and XML case duration48.0179292 s refer to different scopes.

Raw XML SHA256: `64d2d1f2a7d3affe3983dd621dfb3f453045f192fd6fc6b905a46f257680475c`. Raw gate log: `Logs/gate_test_editmode_20261024T010750Z.log`, SHA256 `46614479b46235690750de395380e2519665e41301bdb7b31172846fb63b07ca`. Source in the record: `95219ddde14f1fea969916e2b1d6e30f120f5ebc`.

## Exact public case, not earlier failures

Extracted only `PublicTeamRacePreservesWholeSoloParties` output from XML. Exact public certificate and bootstrap replica-import/ACK observations precede successful scene/start readiness. The first failure is the unchanged client movement deadline after10.079 s, max observed frame gap1.704 s, lifetime not cancelled. There is no recorded session fault at that boundary.

| Boundary fact | Movement begin | Movement end |
| --- | ---: | ---: |
| Host canonical tick | 148 | 555 |
| Host SDK/input tick | 854 | 1372 |
| Client canonical/committed tick | 28 | 28 |
| Client SDK/input tick | 593 | 617 |
| Client latest server tick | 557 | 557 |
| Client input reads / moving reads | 33 / 0 | 57 / 24 |
| Client native position | (-38, 0, -18) | (-38, 0, -18) |

Both runners remained Running with ProvideInput true; the client remained connected. The client boundary CanMove was false while its controller paused flag was false. These observations neither establish host receipt of the24 local moving reads nor identify a collision cause. Unlike001235, the recorded latest-server frontier here also stopped; the earlier run's continuing-server-frontier conclusion must not be copied into this one.

The client completed178 forward/resimulation batches, with no pending/unmatched/mismatched batch. Its last batch was frame31186,28104.260→28109.086 ms,4.826 ms duration. At stop the last batch **start** age was8675.214 ms (completion about8.67 s ago), while the latest Unity Update was49.966 ms old. Host forward batches continued,1372 total ticks, maximum60 ticks and1479.749 ms in one batch. Actual observed UnscaledDeltaTime exceeded1 s on8 host/7 client frames; maximum1728.122 ms, no unavailable/invalid delta samples. These are observer Update/batch facts, not a packet-arrival trace or isolated cause proof.

Ready-window phase evidence is internally consistent:12.718925 s, canonical88→555,467 ObserveHost calls/782.346 ms,467 HostStep/7767.111 ms,481 full validations/2260.283 ms. Nested elapsed scopes are not additive CPU time or a controlled FPS comparison. Service cleanup reports failures0, return0, cancel0, leave0 for two clients/two parties.

## Official 011426 smoke: timeout, incomplete evidence

The actual wrapper stdout/status for `smoke --build 20261024T005254Z --scenario wordbound_workshop_flow --json` reports exit1,122.2 s, unchanged120 s total timeout, process tree killed, and no `checks.json`. The run folder contains precisely three PNGs (`party`, `workshop`, `local_ready`) plus autosave evidence and `.gitattributes`; there are no world/menu/return captures or completed scenario checks. All47 sizes/hashes in `official-visible-smoke-first-20261024.json` match disk, including relevant managed assemblies and log; matching the launcher EXE alone would not establish whole-build identity.

Raw player log `Logs/player/20261024T011426Z-wordbound_workshop_flow.log` confirms Unity6000.6.3f1, readiness at11.050 s/frame14/canonical60, then wait90frames beginning11.661 s. Progress reaches15/90 at50.037 s/frame29/tick960 and30/90 at88.417 s/frame44/tick1860, still ready and unpaused. The log stops there. It does not establish completed90-frame acceptance, a final frame-observer report, or a crash. Shader-stripping warnings are present; they are not proven to cause this timeout.

This reused005254 release-QA player source `c7b0d8a933c2235ba7cca7c195799c7964ae3b33`; the harness source and current native source are different. The peer's saved `BuildNotes/online/UNITY-6000.6.4-INSTALL-VERIFICATION.md` records installerPID51284 starting01:10:13 and still active at01:16:24.852/01:16:43.874, with32,488,332 bytes additional writes. Those timestamped observations corroborate overlap with this smoke (01:14:26→01:16:29); this reviewer did not observe that historical process directly. Overlap prevents a clean timing comparison and does not prove the installer caused the timeout. No later run is included in this verdict.

## Personal visual review: all six 005757 Windows PNGs

Opened every actual PNG in `docs/qa/runs/20261024T005757Z-visible-wordbound_workshop_flow` at1280×720: party, workshop, local_ready, local_world, equipment_menu, returned_workshop.

- Party card is compact with readable guest field and actions, no visible clipping in this viewport.
- Edited and returned workshop views have readable title/toolbar, separated course labels and an opaque readable footer/status line; palette scrolling remains visible. Static images alone do not establish navigation/history correctness.
- Both world images show the original red avatar apparently resting on the wood floor, visible checkpoint/keepsake, HUD and room text. This TimeRun view does not resolve the separately observed Freestyle spawn overlap or prove physical contact from pixels.
- Equipment menu has a single-line Search label, readable disabled recipes and return action; passive HUD panels are hidden while room/checkpoint text remains visible. No obvious text overlap is visible in the inspected default-size menu.

The same run's checks file independently records5/5 steps, no console errors/exceptions/warnings, exact autosave recovery,90 completed observed updates in5.004117 s with89 camera-end frames, return/client/camera cleanup and exact undo/redo history. This supports that bounded scripted windowed route, not sustained production FPS, human input, online acceptance or a stripped final release. Checks SHA256: `7fc06d52bb6db3debd9512ad1f77dfff5f951c9b5b4311d46f3fcd15cf08bd3c`.

Remaining acceptance is explicit: rerun the six-family test with its required opt-in; public TeamSolo movement remains failing; official smoke requires a completed checks file under its unchanged deadline. The passing005757 visible route and three011426 early images cannot override the official timeout.

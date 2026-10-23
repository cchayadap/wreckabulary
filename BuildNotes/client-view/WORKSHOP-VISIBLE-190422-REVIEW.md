# Visible workshop and playtest probe review

Status: SOURCE PASS for the reviewed QA-only probe delta; direct static visual comparison completed. No player, Unity, build or production source changes in this lane. Compilation/execution of the new probes remains pending.

## Actual image comparison

Viewed these actual 1280 × 720 PNGs through the image tool, not generated substitutes:

- Root `docs/qa/runs/20261023T175916Z-visible-wordbound_workshop_flow/wordbound_workshop.png`.
- Root `docs/qa/runs/20261023T190422Z-visible-wordbound_workshop_flow/wordbound_workshop.png`.
- Root `docs/qa/runs/20261023T190422Z-visible-wordbound_workshop_flow/wordbound_returned_workshop.png`.

The newer edited and returned views visibly improve the recorded failure. The active Attic floor now fills a useful part of the map viewport, with the visible map fitting inside its panel. Eight concise room labels are separated and readable; the former stack of overlapping room/prop labels is absent. Mint/cyan outlines stay within the map area in these two static captures. The old capture visibly spilled many mint segments across the title, palette and bottom bar; no comparable spill is visible now. The returned view retains the same framing and labels.

Image identity: the old PNG SHA-256 is `662FCC1E6B9A3AEDCD3E8C648E88B94BE42FDEB04F4C8DB1AED76E2B5F2AF9AB`. Both newer PNGs have identical bytes, SHA-256 `AD72EBA2592686855CB8E79D0156CA3C76E17E10B78BE01D0CAEF85D3D7BE8D0`; they show the same static view. Return/state assertions are separate checks, not inferred from different pixels.

The old bottom status line exposed a long autosave path failure. The new captures show the concise status “Preview ready · exact draft and seed” with no autosave error. This is supported by `190422Z` checks: exact primary autosave recovery, no backup use, unchanged draft and zero temporary files, with 183,314 saved bytes and a 164-character atomic path. The saved evidence is retained in that run's `workshop-autosave.json`.

Limits: static unselected overview views do not verify labels or line clipping during hover, selection, dragging, camera orbit/zoom, near-plane crossings, other floors or other resolutions. They do not demonstrate Inspector property editing or actual mouse/controller input: this QA edits its draft through history commands. Map-preview boxes are not an equipment-art quality review. This is internal visual improvement evidence, not owner approval, broad usability acceptance or production readiness.

## Recorded run scope

`190422Z/checks.json` identifies build `20261023T185900Z`, Unity `6000.6.3f1`, and overall FAIL. `open_wordbound`, `edit_workshop`, `return_to_workshop` and `undo_redo_preserved` pass. The visible run records no console errors/exceptions/warnings and produces a returned-workshop image, so the previous crash is not reproduced in this run. `exact_playtest` fails its outer 90-second deadline with no probes/captures. No world or equipment-menu PNG was written. Return/draft/Undo success cannot establish that playtest reached readiness or explain the timeout.

The current source first waits up to 60 seconds for world/frame/unpaused/guide-marker readiness, then waits 90 frames before capturing. The recorded failure cannot distinguish which substage stalled. Parent-authorized next scope is QA-only substage/frame/elapsed observations and an immediate real ready-screen capture, preserving the 60-second readiness wait, 90-frame observation and 90-second outer deadline, plus explicit failure if Return has no client. Concrete source review follows.

## QA probe source review

Reviewed input lane `Assets/_Project/Scripts/WordboundQa/WordboundWorkshopQa.cs` against its exact-root baseline `084c3016`; reviewed source SHA-256 `513DA584964D73B15D2A5145022D5DE9765485440E513D1E1DD9E390FBC4A16A`. The delta remains under the existing `UNITY_EDITOR || GSK_QA` guard. It records request/readiness/ready-capture/90-frame-wait/world-capture/equipment-capture/completion phases; Unity frame count, elapsed monotonic time, canonical tick, client/controller/world/frame existence, pause/fault flags, marker/visual counts and camera/menu/focus state. Readiness progress and the 90-frame wait log at most once per 15 relevant frames, plus transitions. Dictionary probes retain the last state, while log lines preserve the phase sequence.

The installed kit's `QaContext.WaitFrames(n)` is exactly `n` null yields, so the replacement explicit loop preserves all 90 required yields. Its `QaRunner.Drive` ends a timed-out nested coroutine stack without disposing it; the patch therefore does not depend on an iterator `finally` for final diagnostics. `Return` records the remaining playtest state before shutdown in that step's probes, then explicitly fails if the client is absent. It does not claim a successful return for a missing client.

Immediately after the unchanged readiness predicate succeeds, the scenario captures `wordbound_local_ready.png` through the actual `StoryCamera.CaptureScreen` path. It observes capture errors, records the real path and reuses existing dimension/alpha/nonblack/varied-RGB validation. There is no retry or replacement image. The original world and equipment-menu captures remain after the full 90-frame observation. The additional capture has execution cost within the same outer deadline; no timing budget is extended to hide it.

The 60-second readiness wait, 90-second outer playtest step, 45-second return step, primary autosave/recovery prerequisite, exact manifest/draft/marker checks and return/Undo/Redo assertions are unchanged. Referenced public properties exist in the QA assembly's current direct dependencies. The delta adds no production camera, model, physics, networking, kit or input modifications.

No concrete source blocker found. Frame count measures Unity update frames, not independent displayed-frame delivery. A successful nonblank ready PNG would establish a real retained screen at readiness, not owner visual quality, sustained performance or completion of all 90 subsequent frames. Root must compile/rebuild and run the actual visible scenario to locate the timeout and inspect its new image; this review does not predict a pass.

# Next performance triage — 2026-10-24

STATUS: COMPLETE — read-only triage; one previously deferred, bounded candidate and one small measurement proposed. No runtime, SDK, timing, deadline or capture changes; no jobs launched. Parent owns implementation and execution.

## What the current run establishes

Exact source/build: `9e013dc9e766a616ae1d45ed037cbbd863dbf89e`, QA build `034544`, Unity 6000.6.4f1. Official `035028` workshop passes all five checks. Inspected integration `74f76f3b`; the controller, authority, physics world and workshop QA files inspected here have no difference from that build source. Evidence: `official-short-editor-pass-20261024.json` and `Logs/player/20261024T035028Z-wordbound_workshop_flow.log`. SHA-256 respectively `CEFEDA6F6B22048226D6B4149D1306660190A02AAB107F6B567C7B9F5CD5373F` and `FD0BC29DB256B51BA1903903B75C0B22ED27C66C0F845740754CE1791C22A6D2`.

| Actual interval | Approximate elapsed from rounded log boundaries | Canonical tick change |
| --- | ---: | ---: |
| Ready screenshot and verification, before the update window | 0.217 s | 44→44 |
| First 15 required waits, Unity frames 14→29 | 5.983 s | 44→451: 407 |
| Remaining 75 waits, frames 29→104 | 1.719 s | 451→556: 105 |
| Last 30 waits, frames 74→104 | 0.684 s | 513→556: 43 |

The exact complete window is 7.694559 s/90 observed updates. Its largest early gaps decay from 1,013.667 ms/51 ticks, through 846.955 ms/60 and 819.995 ms/52, to 92.818 ms/8 by frame26. This supports an early catch-up burst that largely settles during this short sample. It is different from the older runs that stayed at 60 canonical ticks per update until timeout. It does not establish the initial backlog's source, sustained presented FPS or a controlled improvement from any individual fix.

The broader ready phase, including captures/menu work, is 8.2921284 s with 528 HostStep attempts/4,933.579 ms (about 9.344 ms/attempt, 59.5% of that elapsed window). FullValidation is 546/1,514.950 ms; Synchronize 528/720.428 ms; ObserveHost 528/538.384 ms; Prepare 528/485.879 ms. Full-validation children include Bodies432.575, Resources310.029 and ProgressRestore264.426 ms. These are inclusive elapsed scopes, with some full validations outside HostStep; subtracting or summing all regions cannot derive exclusive CPU or unexplained render cost.

This is one local player/93 bodies at 1280×720, MSAA4, High shadows, four cascades, HDR/post-processing, vsync0, uncapped target and renderInterval1. It is **not** the eight-player 1080p Low workload. The 89 matched assigned-camera callback pairs total270.671 ms, maximum33.479 ms; callbacks do not measure GPU completion or presentation. Zero unfocused updates does not establish GPU/headroom conclusions.

## Scheduling and capture boundary

`WordboundLocalMatchClient.StartLocal` constructs the authority/view/presentation/HUD, then starts the actual single-player Fusion runner. Controller `PrepareScene` builds/moves the course, `MarkLocalReady` publishes and imports the initial checkpoint, and `BeginHost` synchronously spawns/configures/reconciles bodies before setting hostReady. That is real startup work; the 3.111 s request-return and 4.630 s ready markers do not divide it into exclusive components.

The unchanged tick path is `WordboundGameplayDriver.FixedUpdateNetwork` → `PrepareHostPhysics` → native simulation → `WordboundPhysicsWorld.AfterSimulate` → `CaptureHost` → `AdvanceHost` → authority Advance/Capture/Reconcile. AfterSimulate explicitly requires the resulting canonical revision to be applied before another physics tick. HostStep excludes pre-physics work, native simulation, its CaptureHost argument, rendering and waits. Complete per-tick reconciliation therefore cannot simply be delayed until Render.

The installed SDK analysis already recorded in `PUBLIC-MOVEMENT-234950-REVIEW.md` establishes the default unscaled-delta provider and a maximum60 forward ticks per calculation at60Hz. Local configuration retains those defaults. The current burst fits catch-up scheduling, but this player record has no direct time-provider target/accumulator sample. No accumulator reset, tick cap, delta clamp, checkpoint cadence or deadline change is justified.

Immediately before the 90 waits, QA calls `CaptureVerified`: the kit's existing `StoryCamera.CaptureScreen` waits for end-of-frame, captures a Texture2D, synchronously encodes PNG and writes it; QA also reads/validates the PNG. This adds real elapsed time after readiness. The measured ready→wait boundary is only about217 ms, so attributing the whole six-second burst to that screenshot is unsupported. The first observed gap also includes work after the screenshot and may contain existing startup backlog. Keep the required capture and90 waits unchanged; moving or suppressing them would change acceptance rather than prove a production improvement.

## Already integrated; do not repeat these proposals

Current source contains static course/mode validation indexes, resource/mechanism identity tuples, per-call source/mechanism lookups, BodyBudget's placement-index reuse, capture sort-key reuse, resource retirement buckets, count-only capacity checks, Synchronize team-copy reuse, ObserveHost mechanism lookup, presentation-context reuse, GameplayBody scalar tuple validation caching, validation-only Economy entry, export's returned hash, incremental registration collision pairs and the initial bootstrap ACK barrier. Normal unchanged collision application already avoids a full collision-pair refresh through its dirty flag.

The recorded capture-key allocation reduction (~70.3%/72.5%) and Synchronize Advance reduction (~25.49%/25.58%) belong to their exact calibrated .NET workloads, not this player's FPS. The broader MatchProgress definition descriptor and renderer-hierarchy cache remain unimplemented designs; neither is a new obvious fix. Current ProgressRestore is only264.426 ms including actual dynamic restoration. FrameChanged58.891 ms likewise does not justify coalescing authoritative events. Live renderer traversal still supports later child creation, held views and changed enabled state; a blind cached list or once-per-render suppression would weaken that behavior.

## Smallest bounded source opportunity

Revisit only the **previously deferred immutable supply lookup** in `AuthoritativeMatch.SourceAfterPuzzles` (around921), used by the current Synchronize resource loop. It still scans `course.Supplies.FirstOrDefault(s => s.Id == state.SourceId)` repeatedly. The earlier team-copy change explicitly excluded this lookup; it is not already optimized.

A private, lazily initialized ordinal grouping of the exact authority's `course.Supplies`, queried with `FirstOrDefault`, can retain missing-ID→empty-list and original first-match semantics without an eager duplicate exception. `ResolvedCourse` freezes/sorts the supply array and each `ResolvedCourseSupply` copies its get-only prerequisite list (`CourseManifest.cs:184–196,229`). Do not index mutable document data, cache team progress/demand, or expand this slice into recovery/position lookups. Preserve the original query/short-circuit position, current resource order and live prerequisite membership. Supported MatchResourceState IDs and non-null resource entries must remain required; keep empty-supply and absent-source behavior in tests.

This removes a concrete repeated scan, not a measured dominant cost. All of Synchronize is720.428 ms (8.69% of ready elapsed), and only part is supply lookup; that total is not a predicted saving. It cannot credibly promise to eliminate the startup burst or meet60FPS. This is the narrowest implementation candidate found, subject to parent authorization and review.

Meaningful validation: exact byte/hash and first-error parity for valid→solve→unlock, same-tick teammate unlock, new lap, committed restore/rollback retry and Freestyle station switch; empty/unknown/case-distinct supply IDs and separately resolved course ownership. Existing lifecycle/practice transition cases and complete validation stay intact. Parent can reuse the unchanged `ValidationProbe --advance` before/after with identical starting/ending hashes and calibrated counters, then run the same native and Windows scenarios. No new benchmark framework or changed gameplay limits are needed.

## Smallest discriminating observation before broader changes

If the priority is explaining the burst rather than authorizing that narrow scan removal, add only fixed QA scalar boundary samples at ready, after the verified screenshot, wait15 and wait90. At each boundary retain Stopwatch time, frame/tick and existing collector counts/totals for HostStep/Synchronize/FullValidation. `WordboundPhaseCapture.Count/TotalTicks` already allow owner-thread reads; require no in-flight scopes and keep the capture running. Store fixed values and format after completion, avoiding log/array work at the critical boundary. Do not add a new per-tick profiler, pause, yield, skipped screenshot or timing policy.

That would separate the first15 HostStep cost from the remaining75 and isolate the screenshot's own inclusive boundary. Current whole-ready totals cannot do so. If the later slice still has poor update spacing with small HostStep totals, investigate the excluded native/pre-physics/render work next, rather than assuming GPU or adding unrelated validation caches. A separate sustained eight-client1080p Low workload with actual presentation/GPU-aware evidence remains required regardless of this smoke's success.

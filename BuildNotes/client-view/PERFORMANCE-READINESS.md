# Performance readiness

STATUS: AUDIT AND BOUNDED SOURCE IMPLEMENTATION COMPLETE — native compilation and target performance NOT RUN.

Acceptance: populated large map, eight players and active equipment, 60 fps at 1920×1080 Low on the Core Ultra 7 / Intel Arc reference PC (`docs/wordbound/PLAN.md:316`). This report separates Windows QA flow, synthetic actors and real network peers. Existing `wordbound_workshop_flow` checks local edit/play/return behavior; the source only waits 90 frames in the local world and does not establish that workload.

Inspected integration `44202cc` plus its then-current working tree on 2026-10-23. Source anchors below identify the executable contracts; this audit does not supersede later root run evidence.

## Existing runnable checks

After the assembled native gates and a successful QA build, the smallest existing Windows check is:

```powershell
node Tools/kit/gate/game_gate.mjs build --profile release-qa --json
node Tools/kit/gate/game_gate.mjs smoke --build <successful-build-id> --scenario wordbound_workshop_flow --seconds 300 --json
```

`--seconds 300` is an outer timeout allowance, not a 300-second measurement. `WordboundWorkshopQa.cs` opens the real lobby route, edits a flagship draft at seed `0314159265358979`, launches the exact local playtest, waits 90 world frames, captures party/workshop/world/equipment/returned-workshop views, then checks history and cleanup. It creates one local player. It neither activates equipment nor traverses a fixed workload. Normal QA smoke emits `Docs/qa/runs/<run-id>/checks.json`, screenshots and `Logs/player/<run-id>.log`; QaRunner embeds a whole-run perf summary in checks.json. `cmdSmoke` does not pass `-qa-perf` for this path, so it does not request standalone perf.json. Source: `Assets/_Project/Scripts/WordboundQa/WordboundWorkshopQa.cs`, its `Wreckabulary.Wordbound.Qa.asmdef`, `Tools/kit/gate/lib/player.mjs:217`.

The stripped release path exists separately:

```powershell
node Tools/kit/gate/game_gate.mjs smoke --build <successful-release-build-id> --stripped --seconds 60 --json
```

It starts the ordinary front end with `-qa-perf`, writes `perf.json`, and quits after 60 seconds. It does not autoplay gameplay because the scenario driver is excluded from stripped releases. The generic `smoke_frontend` scenario only boots, idles five seconds and captures the front end. Neither check covers eight actors.

Actual authenticated eight-peer flagship composition already has an exact gated native command:

```powershell
$env:WORDBOUND_EXTERNAL_GAMEPLAY = '1'
node Tools/kit/gate/game_gate.mjs test editmode --filter Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.EightAuthenticatedPeersComposeFlagshipTeamRaceSquad --timeout 900 --json
```

This fixture enters Play Mode, creates eight real authenticated Fusion runners and isolated physics scenes in one Editor process, forms two parties of four, checks canonical bodies and physical callbacks, moves one client and commits a repeated command with exact ACKs. Its production seed is `0123456789abcdef`. It does not sample rendered frame time, drive eight equipped combatants, or establish separate-device connectivity. The existing `AuthenticatedPeersJumpCarryCraftEquipAndStrikeProps` filter exercises active equipment with two actual peers. Source: `Assets/_Project/Tests/OnlineGameplay/WordboundNativeGameplayTests.cs:59`, `WordboundLiveMatch.cs`, `WordboundNativeInteractionTests.cs:16`; setup and timeout rationale in `BuildNotes/online-gameplay-proof/PRODUCTION-CONTENT-PLAN.md`.

## Measurement and settings already available

- `PerfProbe.Begin(name, warmupFrames)` restarts measurement; `MarkFirstVisit` marks hitches; `Finish(true, outDir)` writes the report. It records `Time.unscaledDeltaTime` milliseconds, nearest-rank p50/p95/p99, steady-state worst frame, first-visit hitch, system-memory maximum, machine/GPU key and build kind/ID. Default warmup is 120 frames. It does not provide CPU/GPU timing separation or record screen size, graphics preset, render scale, actor/equipment count or camera path. Release builds do not emit the development-only GC allocation counter. Memory-growth reporting needs at least 600 seconds of post-warmup samples by default. Source: `Packages/com.gamestudiokit.core/Runtime/Qa/{PerfProbe,PerfModel,QaRunner}.cs`.
- The gate currently supplies `-screen-width 1280 -screen-height 720`; its CLI has no per-call player-argument override. A project-owned scenario can set 1920×1080 and assert the resulting dimensions without editing the kit.
- **Low is an existing game preset**, not a Unity quality index. `GraphicsOptions.UsePreset("Low")` applies 0.75 render scale, FXAA, and Low shadows (1024 map, 30 m, one cascade). At a 1920×1080 output this intentionally renders the 3D target at approximately 1440×810. Default is High; saved PlayerPrefs can override it. `UsePreset` and `SetFrameCap` persist preferences, so a QA scenario must preserve and restore the affected keys and V-sync. UI route is lobby Settings → Graphics → Quality preset / Resolution / Frame cap / V-sync. Source: `Assets/_Project/Scripts/Core/GraphicsOptions.cs:123`, `Assets/_Project/Scripts/UI/Lobby/LobbyPages.cs:1326`.
- A QA smoke PASS does **not** automatically enforce the 16.6 ms frame budget: QaRunner automatically evaluates memory growth only, and smoke validates checks/schema/console. An explicit scenario assertion can evaluate p95 after the gameplay window. `perf_compare.mjs <current-perf.json> --baseline <baseline-perf.json> --budget-ms 16.6 --json` requires a real comparable baseline. Never use the candidate itself as its baseline. `probe_report.mjs <run-dir> --json` can summarize emitted files, but there is currently no project `Docs/qa/probes.config.json`.

## Observed evidence and missing workload

The inspected integration checkout has no `Docs/qa/runs` directory and no discovered player `checks.json`/`perf.json`. Its only build manifest is failed QA build `20261020T175830Z` (`ok:false`, 1924.1 seconds); build duration is not a frame-time measurement. The located historical generic eight-peer case in `TestResults/editmode_20261020T154938Z.xml` failed after 187.09 seconds on client authentication and cleanup. That is an older connectivity result, not a result for current source or an FPS number. No measured 1080p Low acceptance or synthetic-eight-actor scenario is claimed.

The smallest missing measurement is one project-owned QA scenario which opens the existing local flagship, explicitly sets/asserts output dimensions and the Low preset, prepares a deterministic visible eight-actor/equipment workload, restarts the existing PerfProbe only after loading, warms up, samples a fixed duration, and records its actual workload/settings with captures. A synthetic eight-actor scene can reveal rendering/animation cost and is useful before the larger live session, but must be labelled **synthetic render workload**, with network peers recorded as one or zero as applicable. It does not replace the existing eight-authenticated-peer fixture or final presented-client measurement while eight real network peers are active.

Recommended next check: finish the current release-QA build and run `wordbound_workshop_flow` once against that exact build ID to establish the Windows route and captures. Add only the bounded workload scenario afterward; reuse PerfProbe and the existing launcher/local-play path. Do not create a second benchmark framework or modify the kit. Final acceptance still requires the populated large map, actual eight-peer gameplay, active equipment and sustained Windows frame-time evidence on the reference PC, with background build/render jobs stopped.

## Authorized bounded implementation — source only

Root subsequently authorized two project-owned files: `Assets/_Project/Scripts/WordboundQa/WordboundPerformanceQa.cs` and `WordboundPerformanceWorkload.cs`. They add `wordbound_synthetic8_low`, reusing the existing QA assembly, launcher/workshop/local-client route, visual resolver, avatar motion and PerfProbe. No kit or production behavior is edited.

- [x] Set and verify 1920×1080 Windowed, the actual Low preset, no frame cap and V-sync off. Snapshot the four string graphics preferences; restore their original existence/bytes immediately after the synchronous preset calls, while retaining the temporary runtime settings. Cleanup reloads the original preferences and restores V-sync, window mode/size and camera/HUD state; it also runs on destroy/quit. No V-sync preference is written. Forced process termination during the synchronous preset writes remains outside graceful cleanup guarantees.
- [x] Require all eight selected items to have the detailed 512×256 normal/mask material set and require created held models to share the selected collection meshes. Use eight animated avatars and eight moving held visuals, without colliders, gameplay attacks or fabricated peers. Record existing local actors/player bodies separately (normally one), total actors (normally nine), zero remote peers, body/renderer/triangle counts, seed/manifest, camera module, output size, preset/scale and hardware.
- [x] Warm up for at least three seconds and 120 frames, restart PerfProbe, then sample 60 seconds. Per-frame health checks do not search the scene or allocate actor arrays. Fail on focus loss, changed output/settings, world/presentation faults, too few frames or p95 over 16.6 ms. Screenshots are outside the measured interval; the 30-second reference pose is explicitly a replay after measurement.
- [x] Independent bounded source review passed through `d6d672c`; no remaining blocking source finding. Native compilation and Windows execution remain NOT RUN.
- [x] Independent review found and repaired two bounded issues: sampling duration now captures its end before JSON/reporting work, with explicit UTC and monotonic bounds retained in checks.json; partial setup does not restore uncaptured camera/view/HUD flags. The scenario also explicitly rejects non-Windows and non-release-QA players.

After integration and a fresh successful release-QA build, the exact new command is:

```powershell
node Tools/kit/gate/game_gate.mjs smoke --build <successful-build-id> --scenario wordbound_synthetic8_low --seconds 300 --json
```

The scenario explicitly writes `perf.json` next to `checks.json`, so no gate argument or kit edit is needed. Its loaded map has real single-player local physics; the synthetic rigs exercise cosmetic locomotion and held-equipment rendering only. This does not establish eight-network-peer, combat/throw physics, full-map traversal, long-soak or human acceptance.

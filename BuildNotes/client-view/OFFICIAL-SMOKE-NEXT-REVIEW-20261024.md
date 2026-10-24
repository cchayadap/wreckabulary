# Official smoke follow-up review — 2026-10-24

STATUS: COMPLETE — read-only evidence/source review; official acceptance remains FAIL

Scope: read-only independent comparison of integration `bace5e5d`, the approved `f865c037` two-file visible-launch correction, and the recorded official/private runs of release-QA build `20261024T005254Z`. Only this report is writable. No Unity, player, compiler, benchmark, process-control or kit work is performed here. Root owns the current Unity 6000.6.4f1 import and subsequent execution.

The same-build evidence already establishes a real acceptance gap: official smoke runs `011426` and `012415` failed the unchanged 120-second gate without `checks.json`; private normal-window run `005757` exited successfully with five passing checks. This does not establish which launch difference caused the slowdown. The earlier build remains a Unity 6000.6.3f1 player despite the newly installed editor.

## Independently checked evidence

| Run of the same `005254` build | Actual result | Bounded timing evidence |
| --- | --- | --- |
| Private `005757-visible-wordbound_workshop_flow` | Exit 0; five PASS checks; six PNGs; console errors/exceptions/warnings all zero | Wrapper interval 25.490 s; scenario completed at 19.254 s; all 90 required waits completed. Ready at action elapsed 3.869 s / frame 14 / tick 44. Complete measured wait: 5.004 s, 90 Unity updates, 349 canonical ticks. |
| Official `011426-wordbound_workshop_flow` | R4 FAIL; 120 s timeout/tree kill; no checks; three PNGs | Ready at 11.050 s / frame 14 / tick 60. Fifteen waits at 50.037 s / tick 960; thirty at 88.417 s / tick 1860. Installer overlap is recorded and remains a confounder. |
| Official `012415-wordbound_workshop_flow` | R4 FAIL; 121.9 s reported gate duration; 120 s timeout/tree kill; no checks; three PNGs | Ready at 9.390 s / frame 14 / tick 60. First fifteen waits take 25.340 s and advance 900 canonical ticks, ending at 35.146 s / frame 29 / tick 960. No completed frame-window summary. |

The action elapsed values start at `RequestPlaytest`, not process launch. The clean official party PNG write at 01:25:05.4118976Z is about 50.6 s after **wrapper** start at 01:24:14.7957757Z. This is an output-latency observation spanning possible pre-spawn, player startup, GUI and capture work; the official record does not preserve the child's actual start time. It does not isolate a Unity initialization stall. It nevertheless prevents attributing the whole launch delay to gameplay HostStep alone.

Both actual ready boundaries report the same graphics/camera settings: PC, vSync 0, targetFPS -1, rendering interval 1, Windowed 1280×720, focused true, background true, timeScale 1, MSAA4, high shadows, URP scale 1; camera active with no render target, same FOV/clipping/HDR/postprocessing. Both logs name Unity 6000.6.3f1, D3D11 Intel Arc `0x7d55`, driver `32.0.101.8860`. This is equality at the sampled boundaries, not proof of identical focus/window/power state throughout each process.

The private status contains **25.40625 aggregate process CPU seconds** (22.078125 user / 3.328125 privileged) over the 25.490 s wrapper interval. The official records have no corresponding process CPU samples. The private value covers every thread and the whole lifetime; it cannot identify per-frame main-thread work, GPU load, waits, or their timing. Existing HostStep measurements are inclusive wall elapsed and overlap child phases. Neither those scopes nor the count of catch-up ticks can substitute for the missing CPU observation.

## Remaining launch and lifecycle distinctions

The five inspected launcher/config files have the same SHA-256 values as the earlier [launch comparison](OFFICIAL-VISIBLE-LAUNCH-COMPARISON.md). `git diff f865c037 -- Tools/kit/lib/proc.mjs Tools/kit/gate/lib/player.mjs` is empty at integration `bace5e5d`. The approved `windowsHide:false` correction is already in the failing official route; another claim that the unpatched hidden launch alone explains these runs would be incorrect.

1. **Invocation and process ancestry differ.** The gate invokes the `.exe` directly through Node `spawn` without a command shell. The private script uses `Start-Process -WindowStyle Normal`. The earlier installed-runtime source audit records CreateProcess/ShowDefault versus ShellExecute/Normal and different child-job lifecycle handling. Those remain candidate distinctions, not evidence of an actual hidden/minimized window, throttling, or a CPU limit. The local launchers do not explicitly change priority or affinity.
2. **Standard handles differ.** Official stdin is ignored and stdout/stderr are piped, with listeners installed immediately; private has no redirect options. The gate retains bounded output in memory but `cmdSmoke` does not persist `r.stdout`/`r.stderr` for an ordinarily started child. Therefore an empty *wrapper* stderr file and small `-logFile` output do not measure player pipe traffic or rule out stream-related work. There is also no evidence here proving backpressure. The two-second drain grace occurs after exit/termination and cannot explain the earlier 25.340-second interval.
3. **Environment inheritance is not fully recorded.** The official job explicitly sets `WORDBOUND_PROFILE_FRAMES=1` and `WORDBOUND_PROFILE_PHASES=1`; the private script inherits its caller's environment. Both logs demonstrate those profiling modes were active. Full environment equality, process priority, affinity, power policy and time-varying foreground state were not captured. Do not dump the full environment to fill this gap.
4. **Timeout and cleanup differ.** The official 90-second configuration plus 30-second grace starts its child timer after spawning and kills the recorded child tree at 120 seconds. It then checks the final artifacts and correctly fails absent `checks.json`. The private workshop watchdog is 360 seconds and leaves an over-limit process recorded rather than automatically killing it. The private success completed at 25.49 seconds, so the longer allowance did not enable this pass; its different failure lifecycle still prevents treating it as R4 replacement. A hard kill also prevents orderly observer/QA finalization: missing final counters or the next fifteen-frame line do not prove that progress stopped exactly at the last log line.

Arguments, executable/build directory, scenario, seed and screen dimensions agree; only fresh evidence paths differ. The private status has quoted argument strings because PowerShell joins them for invocation; the logs/checks demonstrate accepted output paths. No extra `-batchmode`, quality override, alternate mode or movement behavior was found in either route.

## Smallest useful next observation, with the gate unchanged

After root's active import/build ends, run the **ordinary official command and its existing 120-second bound**, retaining the two-file patch, scenario, checks, frame requirement, settings and failure verdict. Add no kit or player changes for this first discriminator. A separate bounded, read-only sampler of just the known wrapper → Node → exact player lineage should record at roughly two-second intervals:

- UTC/monotonic observation time, PID plus actual process creation time and parent, cumulative user/kernel CPU and working set; priority/affinity once and on change. Include Node as well as player so pipe/parent work is not automatically charged to Unity.
- The player's owned window handle and actual visible/minimized/foreground state; no focus activation or window changes. Record unavailable reads rather than inventing a state.
- Existing log length and new `WORDBOUND_QA_PLAYTEST` phase lines plus PNG creation/write times. This gives wrapper→spawn, spawn→first output, ready→wait and pre-kill boundaries even when final callbacks never execute. Record only the allowlisted profiling flags, not credentials or unrelated process arguments.

Use the exact executable path and creation time to reject PID reuse. Record the sampling interval/cost and preserve raw samples. No debugger attachment, new physics/timing instrumentation, profiler injection, priority tuning, stream replacement, cache clearing or deadline extension is needed for this observation. The sampler does not control player lifetime; the unchanged gate remains its owner.

Interpretation is deliberately limited: high process CPU accumulation during a long interval supports substantial CPU work somewhere in the process; low accumulation shows elapsed time not spent executing that process but does **not** distinguish waiting, blocking, scheduler deprivation or GPU synchronization. Window evidence can test a hidden/minimized hypothesis without treating a single `focused=True` as a trajectory. Neither outcome alone explains the source of the work or establishes FPS.

If the official/private difference persists, perform serial, order-reversed comparisons of **one identical build** under recorded idle conditions, with the same external 120-second comparison horizon and fresh folders. The private run remains supplemental. Current new-editor results must be labelled separately: a Unity 6000.6.4f1 rebuild also changes the player, so an old-private/new-official pair cannot isolate launch behavior. Do not wait for an elaborate comparison before preserving the next ordinary official result.

## Evidence binding and closure

Raw reads in integration:

- `BuildNotes/client-view/official-visible-smoke-first-20261024.json` and `official-visible-smoke-clean-20261024.json`; `.harness/delivery/official-smoke-clean-0126-job.json`, `official-smoke-clean-0126-status.json` and `official-smoke-clean-0126-stdout.log`.
- `Logs/player/20261024T011426Z-wordbound_workshop_flow.log`, `20261024T012415Z-wordbound_workshop_flow.log`, and `20261024T005757Z-visible-wordbound_workshop_flow.log`.
- `.harness/delivery/20261024T005757Z-visible-wordbound_workshop_flow-status.json` and the matching `docs/qa/runs/.../checks.json`.
- `Tools/kit/lib/proc.mjs`, `Tools/kit/gate/lib/player.mjs`, `Tools/kit/gate/gate.config.json`, `.harness/delivery/run-gate.ps1`, `.harness/delivery/run-visible-player.ps1`.

Recomputed SHA-256: private log `31637518279223aa09fad5405a0a8e6db342048dc9f572bb7fb09c1e39e41a98`; official clean log `3c10981ee4ae9d666ebbb6067b68682ca90a4e07bb64be8b8d71b7380336379b`; private checks `7fc06d52bb6db3debd9512ad1f77dfff5f951c9b5b4311d46f3fcd15cf08bd3c`. The recorded build tree is `fd77e607d796c8e8b5bf23e9fbffdd241cc5e82b2f642bca7d8fc8e65295f724`; EXE hash alone is not complete payload identity.

Conclusion: the existing report's unresolved launch association is supported; no additional source-proven cause or safe launcher fix was found. The actionable gap is exact child-start/CPU/window history for the official route, especially its pre-game interval. Official FAIL and private supplemental PASS remain separate. No execution or visual acceptance is claimed by this review; no integration files or running processes were changed.

## Prepared private observer (not executed)

Root subsequently authorized preparation only. Script: `wordbound-puzzle-race/.harness/delivery/observe-official-player.ps1`; this ignored file stays private and is not a kit or game change. The existing report is the only tracked deliverable. Root may review and copy the exact bytes before running it later.

Use PowerShell 7 on Windows. Required inputs are the **actual wrapper process creation time** and PID, exact game EXE, fresh QA run directory and player log path. `WrapperCreatedUtc` must come from that process's `StartTime.ToUniversalTime()` with round-trip precision; the wrapper status file's `startedUtc` is a later record timestamp and is not a valid substitute. Absolute target paths are required. `OutputPath`, if supplied, must be a fresh file directly beside the private observer, outside the observed run directory and distinct from the player log. Default output is a fresh timestamped JSONL file there; existing files are never overwritten. Read the JSONL end reason and unavailable facts; observer process exit alone is not proof that discovery succeeded or that the game passed.

Invocation shape for root's later use, **not run here**:

```powershell
pwsh -NoProfile -File <private-script-path> `
  -WrapperPid <recorded-wrapper-pid> `
  -WrapperCreatedUtc '<actual-process-creation-time-in-UTC>' `
  -ExpectedGameExe '<absolute-build-directory>/Wreckabulary.exe' `
  -RunDirectory '<absolute-fresh-QA-run-directory>' `
  -PlayerLogPath '<absolute-fresh-player-log>' `
  -MaximumSeconds 180
```

Start early enough to observe the parent chain. Only direct `wrapper → node.exe → exact expected EXE` children are discovered. Discovery revalidates the parent and matches CIM/process creation times (within 1 ms for provider precision); the pinned identity thereafter uses the exact process `StartTime`. Multiple eligible children, an inaccessible executable path, or a parent already gone are unavailable evidence, never permission to scan all players or guess a PID. Once discovered, each identity is read directly without repeating child discovery. No process command lines or environment blocks are requested.

Output contains process-wide user/kernel CPU, working set, priority and affinity; main-window handle/visibility/minimized/foreground facts when safely readable; and fixed expected capture/check/autosave/log file metadata. Zero main-window handle is labelled unavailable, not hidden. A strict full-line expression admits only the existing workshop `WORDBOUND_QA_PLAYTEST` enum/numeric/boolean fields; other log text, arbitrary messages and malformed phase lines are never emitted. Reads are limited to 64 KiB of incremental log bytes per sample; partial lines are bounded and backlog/reset/rejection counters are recorded. Observation UTC is separate from the action's own elapsed time and is not the log emission time.

The schedule targets two seconds, at most 90 samples, and accepts a maximum from 1 to 180 seconds. It stops scheduling new iterations at the bound or when all known identities have ended. An in-progress synchronous OS/file read can overrun that bound; the final record explicitly reports overrun, setup elapsed, total/max sampling elapsed and the reason. It neither kills nor otherwise controls a process. Native window calls are read-only; there is no window title/text collection, focus change, debugger attachment or game DLL loading. Failed window API setup leaves process/file observation available with type-only failure facts.

Frozen script SHA-256: `c36a214cfdf4a7e4b7b95d59da251d45d50dcde5aa8d5eaa38191d3172b60022`. Source path is `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-puzzle-race/.harness/delivery/observe-official-player.ps1`. It remains ignored/private; copy and verify this exact hash if root elects to run it.

Preparation validation: PowerShell AST parsing only, zero syntax errors. Online independently reviewed those exact bytes and returned bounded SOURCE PASS for lineage/identity, pipeline-return behavior, read-only window APIs, bounded scalar/file output and deterministic process/file/writer disposal. **The observer body, embedded window API compilation, process discovery, sampling and output behavior have NOT RUN** by either reviewer. Root retains execution ownership and the official smoke verdict remains unchanged.

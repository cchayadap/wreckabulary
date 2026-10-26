# Unity ILPP connection stall diagnosis

STATUS: COMPLETE — 2026-10-26 UTC. Bounded source/log diagnosis only; root cause remains unproven and no fix or native retry was performed. Only this report was written.

Scope: compare the owned Unity gate logs from `20261026T022152Z` (successful), `20261026T031649Z` and `20261026T034659Z` (timed out), the installed ILPP runtime configuration, `Library/ilpp.pid` and supported gate startup settings. No Unity launch, process command-line survey, cache deletion, installation or configuration change.

Parent evidence: at 03:51:44 UTC global CPU was 100% and available memory 4,337 MiB. Last owned Unity PID 31340, ILPP PID 43860 / trigger PID 9976 and dotnet PID 50004 were confirmed gone after timeout. These facts are supplied context, not independently measured here.

## Conclusion

The latest attempt stalled in compiler infrastructure before the requested portrait diagnostic callback. Its specific terminal symptom is failure to connect to the IL postprocessor runner, accompanied by a delayed Kestrel heartbeat. Earlier in the same startup, licensing and Burst service startup also failed or took unusually long. This is stronger evidence of delayed service startup/execution than a portrait-rendering defect. It does not identify why the services failed to become ready.

An identical third native retry is not justified by these logs. A bounded retry becomes informative only after a recorded change in conditions, with the actual owned startup priorities and host contention recorded. Increasing the deadline alone is not an established fix. There is no evidence here supporting a new .NET installation, runtimeconfig modification, cache deletion, or deletion of `Library/ilpp.pid`.

Confidence: high in the failure phase and the observed differences; moderate that broad scheduling/resource pressure contributed; low in any single causal explanation. Neither probe produced images, so this investigation establishes nothing about the suspected portrait normals, animation sampling or material path.

## Comparison of actual logs

The successful filename is `Logs/gate_exec_Configure_20261026T022152Z.log`, not `gate_exec_Run_20261026T022152Z.log`. The differing basename reflects the requested method; all three launches enter Unity through `GameStudioKit.Editor.KitCli.RunFromEnv`.

| Observation | Successful 02:21:52 gate | Failed 03:16:49 gate | Failed 03:46:59 gate |
|---|---|---|---|
| Unity | 6000.6.4f1, revision 12bfff696524 | Same | Same |
| Editor location | Installed short path under `wb64/6000.6.4f1/Editor` | Same | Same |
| Compiler prewarm | 113.06 ms (line 3) | 387.36 ms (line 3) | 1,490.58 ms (line 3) |
| Licensing | Connected in 2.03 s (line 94) | Connected in 23.46 s (line 94) | Initialization failed after 75.88 s (line 85), then reconnected successfully (line 99) |
| First domain reset | 3.153 s (line 181) | 8.971 s (line 181) | 26.599 s (line 205) |
| Compilation progress | Bee/Tundra success, 1.78 s; 1 item updated, 1,612 evaluated (line 287) | Changed input signature triggers DAG rebuild; build program exits 0; second Bee pass starts and the log ends (lines 297–305) | Script changes request compilation (line 301), then repeated ILPP connection retries and Kestrel heartbeat warning (lines 313–314) |
| Requested callback | Story setup reports `ok` (line 505), then clean batch quit (line 517) | No callback completion or clean-quit marker | No callback completion or clean-quit marker |

Both failed attempts explicitly observed script-compilation-related changes. The successful setup therefore is not proof that the subsequent changed-source compilation path completed. The first failure's exit code 4 is attached to an explicit request to rebuild the DAG; the following build-program process exits 0. It is not by itself a C# error or proof of an ILPP connection failure. Only the later log explicitly records repeated ILPP connectivity failures.

The duplicate assembly hint-path warnings for `System.Runtime.InteropServices.WindowsRuntime` and `System.ComponentModel.Composition` occur in both the successful run and the first failed run. They do not discriminate success from failure.

## Sanitized terminal evidence

`Logs/gate_exec_Run_20261026T031649Z.log:298–303`:

```text
Rebuilding DAG because FileSignature timestamp changed: Library/Bee/1900b0aE-inputdata.json
*** Tundra requires additional run (6.21 seconds), 1 items updated, 1198 evaluated
ExitCode: 0 Duration: 11s
```

The next log entry starts the second Bee backend, without a subsequent completion entry. The currently present `Library/Bee/backend2.traceevents` predates this attempt, so it cannot stand in for a completed trace of that failed pass.

`Logs/gate_exec_Run_20261026T034659Z.log:160` (also repeated at lines 172, 184 and 195; transient named-pipe identifier omitted):

```text
Deferred Burst compiler server command failed: System.InvalidOperationException: Can't find file <BURST_NAMED_PIPE>. Process output:
```

`Logs/gate_exec_Run_20261026T034659Z.log:313–314`:

```text
Connectivity with IL Post Processor runner cannot be established yet. Retrying.
warn: Microsoft.AspNetCore.Server.Kestrel[22]
As of "10/26/2026 03:56:34 +00:00", the heartbeat has been running for "00:00:01.4085768" which is longer than "00:00:01". This could be caused by thread pool starvation.
```

The connectivity sentence occurs seven times on line 313. The warning names a possible cause, not a measured diagnosis of OS scheduler starvation. Licensing eventually reconnects; it is an additional startup-delay observation, not the terminal blocker.

## Installed runtime and PID evidence

The installed `Editor/Data/Tools/BuildPipeline/Compilation/Unity.ILPP.Runner/Unity.ILPP.Runner.runtimeconfig.json` requests `net8.0` and includes both `Microsoft.NETCore.App` and `Microsoft.AspNetCore.App` version `8.0.29`. Its SHA-256 is `BD0511D67289B1AE924C0F0183CCA1F1787F9EB0BFF953686F96BB3DC89B7D71`; modification time is 2026-09-23. The runner directory contains `coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll`, `System.Private.CoreLib.dll`, and Kestrel named-pipe/socket assemblies. Their inspected versions agree with the bundled .NET 8.0.29 runtime. This provides no missing-runtime explanation for the latest stall.

The same runtimeconfig explicitly sets `System.GC.Server` to `false`. A claim that this runner uses server GC with one heap per processor would contradict the inspected configuration. The gate's `core.tfm: net9.0` belongs to core test compilation and does not change the ILPP runner's runtime. The separately bundled Unity build-program SDK/runtime versions are likewise not proof of a missing ILPP framework.

Current shell plus persistent User/Machine values were absent for the bounded override whitelist: `DOTNET_PROCESSOR_COUNT`, `DOTNET_GCServer`, `COMPlus_gcServer`, `DOTNET_GCHeapCount`, `COMPlus_GCHeapCount`, `DOTNET_ThreadPool_MinThreads`, `DOTNET_ThreadPool_UseWindowsThreadPool`, `DOTNET_GCHeapHardLimit`, and `COMPlus_GCHeapHardLimit`. This does not reconstruct the historical environment of the terminated processes; the gate inherits its launching process's environment.

`Library/ilpp.pid` contains `43860`, with modification time 03:55:04 UTC. Parent confirmed that PID is now gone. A residual PID file after timeout is not evidence that stale PID ownership caused the earlier connection stall. No PID file or cache was changed.

## Supported gate startup and receipt caveat

`Tools/kit/gate/lib/cold.mjs:29–34` constructs the same batch/project/log/crash-directory arguments and merges job environment over the parent environment. `lib/jobs.mjs:612–618` uses the same `RunFromEnv` entry point, supplies the requested method through `GSK_EXEC_METHOD`, and reports timeout separately. It reserves the output file before launch; an existing empty `Logs/exec/20261026T031649Z.json` therefore does not establish callback execution.

`Tools/kit/gate/gate.config.json` provides `GSK_UNITY` selection, a 1,800,000 ms default job timeout, 180,000 ms readiness timeout and 900 s recompile timeout. The inspected public configuration has no dedicated ILPP runtime, connection, thread-pool or process-priority setting. `lib/config.mjs:54` accepts a path or JSON argv array for editor selection; this is an override seam, not evidence for any particular corrective editor flag.

`Logs/gate_editor.json` still names 6000.6.3f1, but `lib/editor.mjs:75` only accepts that cache when its version matches the project. All three actual logs show 6000.6.4f1. The stale cache does not explain a wrong-editor launch here.

The initial `BuildNotes/experience-refresh/native-experience-blocker-20261026.json` correctly recorded the two failed R3 attempts and no probe images, but both R3 entries pointed their `log.path` and empty-file hash at an R0 log. The integrator has corrected the receipt to the actual `Logs/gate_exec_Run_20261026T031649Z.log` and `Logs/gate_exec_Run_20261026T034659Z.log`, with SHA-256 values `30a7838e33e5cd582f332d2d72c6fc1cd196deabfab85d793998428c5280eec6` and `d2f2f152dd9619b2a5f21e09302459b49c4a6a6af8c0df3e50ed55c492821f24` respectively. The receipt retains a correction note; original logs were not changed.

## Retry decision and priority hypothesis

Parent additionally supplied about 82 Unity CPU-seconds over roughly 600 wall-seconds, and 2.7 ILPP CPU-seconds over roughly 180 wall-seconds, with host CPU at 100%. Together with the explicit heartbeat warning and progressively slower independent startup stages, this is compatible with scheduling starvation. It is also compatible with waiting on unavailable IPC/services; aggregate CPU time does not distinguish runnable-but-unscheduled threads from blocked threads. No historical process priority was captured in these logs. Thread-pool starvation and OS scheduling contention must not be treated as the same proven mechanism.

A read-only check of the next gate-owned Unity process's actual priority and the directly verified ILPP child's priority would add useful evidence. If an owned process is unexpectedly BelowNormal, restoring that process to Normal is a concrete changed-condition experiment. If both are already Normal, an AboveNormal trial remains an unproven scheduling experiment, not a diagnosis or fix. This report neither performs nor requests such a change. Parent's proposed limit excludes High/Realtime and unrelated processes.

Before another native attempt, retain these logs, keep the source snapshot stable, record host conditions and owned-process priorities, and identify the exact intended changed condition. Owned-process cleanup has already been confirmed. A single bounded retry after a documented reduction in contention, or a separately approved measured priority correction, could test the scheduling hypothesis. Without that change, stop repeating the same native launch. If connection readiness still stalls, preserve fresh compiler-service/IPC diagnostics for deeper Unity infrastructure investigation rather than extending timeouts or reinstalling components speculatively.

Verification of this report: direct comparison of the three owned logs and local gate/runtime configuration; no Unity, build, test, render, installation, cache mutation, or unrelated process inspection. No claim of a native pass or fixed ILPP connectivity.

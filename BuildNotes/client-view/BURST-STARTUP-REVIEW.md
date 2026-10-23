# Burst startup review

STATUS: COMPLETE — source/log inspection only; startup cause undetermined. No verified workaround established.

Scope: Burst 2.0 compiler-server startup failure in integration `Logs/gate_exec_PrepareFromEnvironment_20261023T120315Z.log`. This is separate from the earlier native interruption, whose cause remains undetermined.

## Confirmed observations

- Integration uses Unity **6000.6.3f1**. Its `Packages/packages-lock.json` resolves `com.unity.burst` **2.0.0**, `source: builtin`. `Library/PackageCache/com.unity.burst@626e66ab421b/package.json` identifies a `shim`; its runtime file forwards Burst types to the editor. The implementation is bundled under `C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/Tools/Burst/Client`, including `Burst.EditorIntegration.Client.dll` and `bcl/bcl.dll`. Cache deletion or an independent Burst package downgrade is not supported by this evidence.
- The current log, lines 281–341, reports `WaitForFileExists` / `StartServerProcess` / `WaitForStartupAsync` failing to find the **named IPC pipe** `unity-burst-d1456c25-02bc-4d8a-b512-c0b886f60e37`. Captured process output is empty. Multiple deferred commands report the same endpoint failure. This is not a missing gameplay file, a demonstrated C# source error, or evidence that the server exited.
- Script compilation is requested at line 342 after those errors. The running gate's eventual verdict is outside this review; later progress does not erase the Burst error.
- Coordinator observation at **2026-10-23T12:09:53Z**: exact Burst child PID 5412 of Unity 8580 remained live at Normal priority, with matching `bcl.dll +persistentserver` and pipe argument, and only 2.30 CPU seconds. Total CPU was 90%; free physical memory was 4,741,716 KiB. This supports limited observed progress, **not a diagnosis of contention, memory exhaustion, deadlock or crash**. These process measurements were supplied by the coordinator; this reviewer did not change or resample processes.
- Bundled `bcl.runtimeconfig.json` declares .NET 8.0 and ASP.NET Core 8.0 frameworks. This identifies expected runtime families, not a missing-runtime failure. No missing-framework diagnostic was present in the inspected error.

## Supported controls and their limits

The installed `Documentation/en/Manual/burst/getting-started.html` and current [Unity 6.6 Burst guide](https://docs.unity.com/en-us/engine/6000.6/manual/programming-environment/script-compilation/burst/getting-started) document only `--burst-disable-compilation` and `--burst-force-sync-compilation` in their Burst command-line section. Neither is documented there as a compiler-server startup fix; neither was used.

Unity's [Burst menu reference](https://docs.unity.com/en-us/engine/6000.6/manual/programming-environment/script-compilation/burst/editor-reference-overview/editor-burst-menu) says **Show Timings** records job JIT compilation phases. Synchronous compilation and Native Debug Mode change compilation behavior. These are not documented pipe-readiness diagnostics; they do not establish a startup-timeout remedy. Editor menu controls also do not substitute for Player AOT settings or Windows acceptance.

Read-only string inspection of the installed client assembly finds startup/shutdown timeout identifiers, `DebugLoggingEnabled`, `DebugLogWriter` and `burst.log`. It does **not** establish a supported external timeout/logging switch or its value. The editor assembly also contains `UNITY_BURST_DOTNET_PATH`; this internal string alone is not justification to override the bundled runtime. No environment changes were made.

No existing `*burst*.log` was found in direct files of the project root, `Logs`, `Library`, `Temp`, `%LOCALAPPDATA%/Unity`, or its `Editor` directory. `Library/BurstCache` contained `JIT` and an AOT settings hash. This was a bounded check, not an exhaustive assertion that no Burst log exists anywhere.

## Smallest next check

Let the coordinator's current gate reach its own verdict. Preserve its exact editor log and child ownership. If startup fails again, the useful discriminator is **whether the matching Burst child exits before the pipe appears or stays alive without publishing it**. Capture timestamped child start/exit and CPU deltas, the matching pipe's existence, and any actual child stderr/exit code during the next coordinator-owned, otherwise unchanged gate. Do not launch a second editor/compiler to probe it.

A retry after competing work has naturally finished is a controlled comparison, not a promised fix. If a live child repeatedly never publishes its pipe, deeper process startup diagnostics or a Unity issue report is the next evidence step. Current observations do not justify disabling Burst, deleting caches, changing the SDK/editor/packages, extending an undocumented timeout, or changing firewall/antivirus policy.

The earlier gate ending with `Shut down.` remains separately **undetermined**; no causal link from this startup error to that termination was demonstrated. No packages, SDK, Tools, settings, environment variables, locks or Unity/compiler processes were changed by this review. No native job was launched.

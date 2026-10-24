# Official observer results — 2026-10-24

STATUS: READ-ONLY EVIDENCE REVIEW COMPLETE. Official smoke PASS is supported by the gate and actual checks. No live process queries, source changes, Unity/player/IO jobs or kit edits were performed. The separate 035159 equipment scenario is not a launch-speed control.

## Bound artifacts and outcome

Root record `BuildNotes/client-view/official-short-editor-pass-20261024.json` binds source `9e013dc9`, release-QA build `20261024T034544Z`, Unity `6000.6.4f1`, and the official `smoke` invocation. Its gate reports PASS/exit 0, 60.7 seconds, no timeout; actual `docs/qa/runs/20261024T035028Z-wordbound_workshop_flow/checks.json` reports all five steps PASS and zero console errors, exceptions or warnings. The root record's embedded checks exactly match the parsed checks file. The build manifest says 236.3 seconds; root's reported 237.3-second build-gate duration is a separate timing boundary.

The record lists 51 artifact hashes. This review verified the checks receipt hash, not every large build artifact. Observer JSONL and launch JSON were not in that list when inspected; their exact hashes are independently pinned here:

| Saved artifact | SHA256 |
| --- | --- |
| `.harness/delivery/observe-official-player.ps1` | `C36A214CFDF4A7E4B7B95D59DA251D45D50DCDE5AA8D5EAA38191D3172B60022` |
| `.harness/delivery/20261024T035028Z-wordbound_workshop_flow-observation.jsonl` | `876026581AED9C7E11DD578EF318FB687FBFC9454D86340E5D2B6BF4DE31A782` |
| `.harness/delivery/official-short-editor-observer-launch.json` | `AE9A05866D2902B74CD52E90FFFFB4C6A51C8AF2CAEE0E0DC9F96E668C7BBAF6` |
| `docs/qa/runs/20261024T035028Z-wordbound_workshop_flow/checks.json` | `FA1643411186E27A969508AD4553EEFD760DCA7C0E6EC11A0F12615D776A7808` |

## Identity, windows and observed timing

The launch record supplies wrapper PID **40988**, creation **03:50:27.5329048Z**. This matches the observer's initial identity. Its direct child Node is PID **13400**, created **03:50:28.2444231Z**; the exact expected `Builds/qa-20261024T034544Z/Wreckabulary.exe` child is PID **41936**, created **03:50:28.5525342Z**. Parent PID plus creation identity and the exact executable-path selection agree with the previously source-reviewed script. The wrapper status's later `startedUtc` is not substituted for process creation.

All three remained identity-matched through sample 29 and were not running at sample 30. The final record is `reason=known-lineage-ended`, 30 samples in **61.580191 s**, no maximum-duration overrun, `noControlActions=true`. No discovery, window-API or observer-error records occur. The script hash matches the reviewed read-only implementation: it queries identity/counters/window state and writes its own output; it has no focus, window-state, process-control or child-launch action. The terminal flag alone would not prove that property without the matching source.

The first **15** player samples returned `no-main-window-handle`: this is explicitly unavailable, not evidence of a hidden/minimized player or an exact window-creation time. The first usable main-window sample is index 16, **03:51:02.7287464Z**, approximately **34.176 s after actual child creation**. Its window read occurs within that sample's 55.123-ms read interval. All **14** usable samples observe the same main window, `visible=true`, `minimized=false`, `foreground=false`. They do not establish window state between samples or which other window was foreground. Unity separately reports focused=true during the QA/frame window; that different observation/semantics must not be turned into a throttling cause.

| Event | Saved file last-write offset from actual child creation | First observer sample offset |
| --- | ---: | ---: |
| Party PNG | 40.573 s | 42.204 s |
| Workshop PNG | 46.046 s | 46.215 s |
| Local-ready PNG | 50.872 s | 52.244 s |
| Local-world PNG | 58.752 s | 60.287 s |
| Checks receipt | 59.323 s | 60.287 s |

File metadata times are not screen-presentation timestamps; polling adds observation delay. Sample timestamps are taken before their reads, not at each individual field read. Similarly, the 14 exported fixed QA phase events have observation time, not original log-emission time. The autosave evidence is a copied file whose last-write can precede its creation at the destination; it is not used to infer startup ordering.

## CPU, memory and sampler cost

At the last matched player sample (+60.287 s), cumulative user CPU is **21.843750 s**, kernel CPU **5.187500 s**, total **27.031250 s** across all player threads. This is not a final lifetime reading, main-thread cost, GPU time or frame-time attribution. Sampled maximum working set is **883834880 bytes / 842.891 MiB**; last matched value is **600387584 bytes / 572.574 MiB**. These are sampled resident values, not private bytes or an OS lifetime peak. Every matched player sample reports Normal priority and affinity mask `0x3FFFFF`; process fields have no recorded unavailable errors. Final exited samples provide no final counters.

Observer setup consumes **3326.1193 ms** wall time. Total sample/read/output cost is **2637.4748 ms**, maximum **1184.0701 ms**, including discovery. Recorded read-only portions sum to **2591.7745 ms**, median **47.5616 ms**; after the first discovery sample, maximum read cost is **117.6721 ms**. Setup plus sampling is about **5.964 s** of measured observer wall time. This is neither observer CPU consumption nor a measured player slowdown; OS waits and concurrent execution are included. It is nonzero measurement overhead, so there is no observer-free timing control here. No iteration exceeded the configured 180-second budget; synchronous reads still lack a separate hard watchdog by design.

The bounded log reader consumed **15220 bytes**, maximum **7198 bytes** in a sample, with zero backlog, resets or rejected phase lines. All phase-log samples report readable. No raw arbitrary log lines, credentials or other process state are exported by this observer.

## Actual playtest performance boundary

The unchanged 90-wait requirement completed in **7.694559 s**, frames 14→104, canonical ticks 44→556 (**512** ticks). That is about **11.697 observed Unity updates/s over this window**, not 60 FPS or presented-frame acceptance. Maximum update gap is **1013.667 ms**, maximum canonical ticks in one update **60**, with seven zero-tick updates. No missing/unpaired snapshots, epoch changes, regressions or unfocused updates were recorded.

The assigned camera reports **89 paired begin/end callbacks**, zero unmatched/in-flight/cross-frame pairs, 270.671-ms summed callback wall time and 33.479-ms maximum. Those callbacks do not measure GPU execution or display presentation. The profiler's broader ready window (8.2921284 s through later captures, 528 host steps) is different from the 90-update window; its nested phase totals cannot be added to derive total CPU or GPU cost.

Conclusion: this particular new-build official smoke completed correctly and its main window was observed visible, with a substantial startup interval before the first usable window/capture observation. Earlier official failures remain historical failures. The new pass changes the build, Unity patch, source and run conditions; neither this observer nor the separate 035159 equipment scenario isolates a causal launcher/Unity fix. No speed comparison between those different scenarios, final stripped-release claim, human interaction acceptance or sustained-FPS claim is made.

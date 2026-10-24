# Official and diagnostic Windows launch comparison

STATUS: COMPLETE — source/evidence review only; launch cause remains unproven. No player, Unity, compiler or installer run, source edit, kit edit, or timeout change.

## Evidence and scope

- Same release-QA build `20261024T005254Z`, player source `c7b0d8a933c2235ba7cca7c195799c7964ae3b33`, executable SHA-256 `96b492cb271111251fe42b8646e65370a1b7b566773a1e35b34c3f2d1ae70873`.
- Official run `20261024T012415Z-wordbound_workshop_flow`: R4 FAIL after the 120-second process limit; three PNGs, no `checks.json`. Root record: `BuildNotes/client-view/official-visible-smoke-clean-20261024.json`.
- Diagnostic run `20261024T005757Z-visible-wordbound_workshop_flow`: five checks PASS, exit 0; recorded process lifetime 25.49 seconds. This remains supplemental evidence, not an official gate replacement.
- Read scope: both player logs/checks/status records; `Tools/kit/lib/proc.mjs`, `Tools/kit/gate/lib/player.mjs`, gate configuration, `.harness/delivery/run-gate.ps1` and `run-visible-player.ps1`. Kit/runtime/source files remain unchanged.

## Result

The official failure is real slow execution before completion, not a successful scenario misclassified while reading its checks. The private run does not establish that the gate launcher causes it. The two runs are separated in time, and neither records comparable machine-load, cold/warm asset/shader/file-cache state, process-priority, affinity, power-throttling or full inherited-environment evidence. Do not attribute this pair to the installer, window state, redirected handles or GPU without a controlled observation.

The approved `windowsHide:false` smoke call is present. It removes the explicit hide request but does not make Node's Windows launch identical to `Start-Process -WindowStyle Normal`.

## Exact launch comparison

| Contract | Official gate | Successful diagnostic |
| --- | --- | --- |
| Executable / working directory | `Builds/qa-20261024T005254Z/Wreckabulary.exe`; its build directory | Same recorded executable SHA and directory |
| Player arguments | `-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -qa-scenario wordbound_workshop_flow -qa-seed 1 -qa-out <fresh folder> -logFile <fresh file>` | Same values; different fresh evidence paths. Quotes in the PowerShell argument array preserve path strings when joined; they are not extra gameplay options. |
| Invocation | `run-gate.ps1` runs Node `game_gate.mjs`, which directly spawns the EXE (no cmd shim) | `run-visible-player.ps1` uses `Start-Process ... -WindowStyle Normal -PassThru` |
| Standard handles | stdin ignored; stdout/stderr piped and immediately asynchronously drained, retaining at most 8 MiB each | No `RedirectStandard*` options; no equivalent kit pipes |
| Environment | `run-gate.ps1` sets the job's allowlisted `WORDBOUND_*` values, then Node inherits its environment | Script inherits its caller environment; it does not set these values itself |
| Profiling actually observed | Both phase and frame logging enabled | Both enabled; this proves these opt-ins, not equality of all environment variables |
| Process lifetime | 90-second configured smoke + 30-second grace; tree kill at 120 seconds; missing checks remains FAIL | Workshop diagnostic waits at most 360 seconds, without automatic kill; this run ended after 25.49 seconds, so the larger bound did not make it pass |
| Priority / affinity | No explicit tuning in inspected launcher | No explicit tuning in inspected launcher |
| Completion | R4 FAIL, exit 1, no checks, three PNGs | Exit 0; five PASS steps, QA ended at 19.254 seconds from scenario start; recorded console 0 errors / 0 exceptions / 0 warnings |

Installed runtime versions read without executing a new application: Node 24.15.0, PowerShell 7.6.5. Node's matching libuv source uses `CreateProcessW`, explicit standard handles, and `SW_SHOWDEFAULT` when hide flags are absent. Its non-detached child job is configured for lifecycle control; that source does not impose a CPU-rate or priority limit. This does not exclude a parent or OS policy. [Node/libuv 24.15.0 Windows process source](https://raw.githubusercontent.com/nodejs/node/v24.15.0/deps/uv/src/win/process.c).

PowerShell 7.6.5 selects ShellExecute on Windows Desktop for this option set: `WorkingDirectory`, `ArgumentList`, `PassThru` and `WindowStyle` do not request its redirected/CreateProcess path. It passes the requested Normal style. This is a source-derived invocation distinction, not a sampled window-state result. [PowerShell 7.6.5 Start-Process source](https://raw.githubusercontent.com/PowerShell/PowerShell/v7.6.5/src/Microsoft.PowerShell.Commands.Management/commands/management/Process.cs).

Windows defines Normal as showing/activating a window and ShowDefault as using startup show information. Therefore `windowsHide:false` is not an explicit `SW_SHOWNORMAL` guarantee. Neither the API distinction nor a hidden wrapper proves that this actual child remained hidden: the official run rendered three images and reported focus true at the gameplay measurement boundary. [Microsoft ShowWindow documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow).

The gate's stdout/stderr listeners are live throughout the run. The recorded player log is only 7,300 bytes and the wrapper stderr is empty, but these are not measurements of child pipe volume, so pipe backpressure is unproven. Its two-second stream drain runs after exit/termination; it cannot explain the earlier gameplay slowdown. PID tracking is retained, and checks are parsed only after the awaited child returns.

## Actual timing and render settings

The following elapsed values are relative to the `exact_playtest` action, not process start:

| Observed point | Private 005757 | Official 012415 |
| --- | ---: | ---: |
| `RequestPlaytest` returned, Unity frame 9 | 2.374 s | 5.604 s |
| Ready, Unity frame 14 | 3.869 s, tick 44 | 9.390 s, tick 60 |
| Begin required 90-update wait | 4.081 s | 9.806 s |
| 15 waits, Unity frame 29 | 7.597 s, tick 301 | 35.146 s, tick 960 |
| 90 waits, Unity frame 104 | 9.088 s, tick 393 | Not observed |
| Playtest returned | 9.424 s, tick 407 | Not observed |

The official first 15 updates took **25.340 seconds and 900 canonical ticks**, averaging 60 ticks per update. The private run's first 15 took 3.516 seconds / 257 ticks, then recovered: its complete frame-observer window reports **5.004 seconds, 90 updates, 349 canonical ticks**, maximum 49 ticks/update. Its bounded tail shows catch-up decreasing from 49 to 5 ticks/update before an additional smaller spike. These are Unity-update/canonical observations, not presented FPS or evidence of human input.

The official startup phase reports 60 HostStep calls / 1,300.442 ms, averaging 21.674 ms. Private startup reports 44 / 449.523 ms (10.216 ms); private completed ready window reports 363 / 3,118.919 ms (8.592 ms). These scopes are **inclusive wall elapsed**, include scheduling interruption and overlap other phase markers; they must not be added together or labelled CPU time. Their difference and the 60-tick/update observation support catch-up pressure as an explanation for few Unity updates. They do not identify why the official run is slower, or establish its unrecorded steady-state HostStep cost.

There is also substantial delay before gameplay: the official party PNG was last written at 01:25:05.4118976Z, about 50.6 seconds after wrapper start; workshop at 01:25:17.4260624Z and readiness at 01:25:27.2143342Z. These filesystem times establish output timing, not which initialization, GUI, capture or scheduling operation consumed that interval. HostStep cannot alone explain a pre-game delay.

Both actual begin boundaries match: PC quality, vSync 0, targetFPS -1, render interval 1, 120 Hz display, 1280×720 Windowed, focus true, run-in-background true, timeScale 1, MSAA4, high shadows, URP scale 1, shadow distance 50 / four cascades / map 2048. Camera is active, display 0, no target texture, FOV 55, near .1 / far 1800, HDR/MSAA/occlusion/postprocessing enabled. Both logs identify D3D11, Intel Arc ID `0x7d55`, driver `32.0.101.8860`, and the same PhysX version. The same stripped DOF/Panini messages occur in both, so their presence does not distinguish this failure.

Only the private run has a completed camera window: 89 matched begin/end pairs, no unmatched or cross-frame pairs, 213.634 ms total callback elapsed / 7.957 ms maximum. This is callback wall time, not GPU execution or presentation. The official hard kill prevented final phase/frame summaries. Its absent next 15-frame log is not proof of zero subsequent progress or a particular blocked operation.

## Smallest useful follow-up (proposed, NOT RUN)

1. Keep this build, same scenario/seed/settings and the existing **120-second limit**. Under one explicitly recorded quiescent machine state, compare the official launcher and Normal diagnostic route in alternating order. Run serially; no concurrent Unity import/build/installer. Record first/warmed-run ordering rather than clearing caches. Preserve each fresh output folder and failure; a diagnostic pass never replaces R4.
2. Before changing either launcher, observe the specific wrapper/Node/player PIDs only: process start/parent, priority class, affinity, cumulative CPU and working-set samples, and actual player visibility/minimized/foreground state at bounded intervals. Record only allowlisted profiling variables and nonsecret launch facts, not the full environment. A bounded system CPU/memory sample can expose load confounding without scanning unrelated process command lines. Keep Unity/camera/tick telemetry already present.
3. If slowdown follows the launcher under those controls, isolate one difference at a time in a disposable diagnostic outside the locked kit (standard-handle routing versus explicit Normal show behavior). Any additional kit change needs its own bounded approval and verification; the existing two-file visibility approval is not permission to replace launch semantics, checks or timeouts.

Do not extend the deadline, tune quality/physics, force priority/focus, discard pipe capture, or assert an installer/GPU/ShowDefault cause from this pair. The evidence currently justifies measuring the launch/load distinction, while retaining the official FAIL and supplemental private PASS.

## Inspected source identity and next recoverable action

Root recorded harness revision: `a57d5a53a57cfdb9331ff2c65384c6b33f746aeb`; approved visible-spawn correction: `f865c037`. SHA-256 of the exact local files read:

| Path | SHA-256 |
| --- | --- |
| `Tools/kit/lib/proc.mjs` | `46e44c043746c9bf3228bb7f851b07a1b191a9daae284ead51b5e886b7c0a5f4` |
| `Tools/kit/gate/lib/player.mjs` | `1217051de489b679ea618163104941c06450eb5cef7d425264fabbccba4b89f0` |
| `Tools/kit/gate/gate.config.json` | `c431de1b3c449986a4fb6d9ec39c652e5a20dc25d90e9750a85f7b8ccf10074d` |
| `.harness/delivery/run-gate.ps1` | `1155997ab7f723629bf3a019735015cf7ee2019af343cb4400c51aa4d302518a` |
| `.harness/delivery/run-visible-player.ps1` | `2de22b964cc4327b372648226bbbb4284c0f06f9393fec48ead25da480d52ef9` |

Raw source of timing assertions: the two named `Logs/player/*.log` files and private `checks.json`/status record. Official record hashes its log, launch job/status, build payload and all three PNGs. No new visual acceptance is made by this launch review. Next action belongs to root: select a controlled observation after its active native work; no follow-up was launched here.

Independent attribution sanity check: Input agreed that timing/order, cache/load, inherited environment and foreground state across the interval remain confounders, and that the launch/pipe/job differences are candidates only. This was a bounded reasoning check, not an independent replay or complete source audit.

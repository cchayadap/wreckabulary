# Unity 6000.6.4f1 installation verification

STATUS: READ-ONLY OBSERVATION COMPLETE — installation was actively extracting,
not ready for use, at 2026-10-24 01:16:44 UTC. No install, launch, kill or configuration
change was performed by this lane.

The intended location is
`wordbound-integration/.harness/toolchain/6000.6.4f1/Editor/Unity.exe`.
The directory appeared at 01:13:00–08 UTC. Executable metadata reads
`6000.6.4f1_12bfff696524` (file version `6000.6.4.1228799`); its Authenticode signature
is Valid, signed by Unity Technologies SF. This small executable is present; that
does not establish a complete Editor installation.

At 01:16:24 UTC, Managed/UnityEditor.dll, UnityEngine.CoreModule.dll and Mono were
present, but `Data/Tools`, `Data/PlaybackEngines` and the PackageManager executable
were absent. New Data/NetCoreRuntime, NetStandard and PluginAPI directories appeared
at 01:15:39–42 UTC. Packaged file dates are preserved by extraction, so their old
September timestamps are not completion evidence.

The elevated `UnitySetup64-6000.6.4f1.exe` process, PID 51284, started at 01:10:13 UTC
and remained active. Two read-only process snapshots prove ongoing work:

| Observation UTC | Read transfer bytes | Write transfer bytes | Kernel/user time (100 ns units) |
| --- | ---: | ---: | --- |
| 01:16:24.852 | 6,805,980,189 | 11,354,063,667 | 1,399,843,750 / 9,707,343,750 |
| 01:16:43.874 | 6,838,492,033 | 11,386,551,999 | 1,484,843,750 / 9,716,250,000 |

Writes increased by 32,488,332 bytes in about 19 seconds. Old Unity CLI PID 39804
also remained present. The installer overlapped root's official smoke launched
01:14:26 UTC; that run cannot isolate the kit patch's timing effect from installation
load. This observation does not establish the cause of any specific stall.

`unity editors --installed --long --format json` returned success but listed only
the registered ProgramFiles 6000.6.3f1 and 6000.3.25f1 editors. The isolated install
was not listed. The bounded CLI log inspection showed recent very short exit-zero
help/inventory invocations, not a completed installer receipt. No relevant installer
failure was established from that log; credentials, arguments and licensing files
were not printed/read.

Cached installer exists at
`C:/Users/mrkl6/AppData/Roaming/UnityHub/downloads/portable-31bf89a41bf21e5e/UnitySetup64-6000.6.4f1.exe`:
3,851,126,776 bytes, version resource `6000.6.4.0`. Its multi-gigabyte signature/hash
was deliberately not rescanned during the measured player run. Cache presence is
confirmed; fresh cache-integrity verification is not claimed.

Next action: allow the existing installer to finish; do not start a second installer
or download. Root can then verify its exit/result and required Editor files, bind
the already approved isolated path, and perform the official gate/build. If the
current installer actually fails, first inspect that failure and validate the existing
cached installer while the measurement slot is idle; use the same approved target
with the valid cache rather than initiating another download. This lane did not
launch an Editor or claim a usable toolchain/build result.

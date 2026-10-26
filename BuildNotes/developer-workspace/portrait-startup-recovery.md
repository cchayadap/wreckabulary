# Portrait startup recovery investigation

STATUS: IN PROGRESS — read-only investigation of root-owned gate26108 / `Logs/gate_exec_Run_20261026T092340Z.log`. Parent reports a Unity.DataStore shared-data/semaphore initialization failure before the portrait diagnostic or AssetDatabase initialization. Preserve other running jobs; no process termination, native job, cache/package/settings change or service operation is authorized here.

Scope: correlate bounded current log evidence with official Unity documentation/issues and installed source where useful, then identify the safest non-destructive recovery and its prerequisites. A process appearing beneath an owned editor is not proof that a shared DataStore process belongs exclusively to that editor.

## Confirmed observations

- Failed092340 log: 9,492 bytes, last write09:23:54Z at initial inspection. The first relevant error is command-buffer shared-memory/pool mismatch, followed by `connect_shared_system_data` → `uds_internal_system_init_as_client` → `UDSInterface` → `InitializeAssetDatabaseV2`. Lines142–160 then show the rebuild attempt, an already-existing semaphore for ID0 and lock initialization failure. There is no diagnostic completion marker or capture02 directory; this is an Editor startup failure, not evidence about portrait lighting or candidate code execution.
- The lock-message suffix is mixed encoding: decoding that line's original bytes as CP950 produces `操作順利完成。` (operation completed successfully). It does not establish access denial, ACL trouble or an actionable Win32 error. Do not change privileges based on the garbled UTF-8 display.
- Independent local review of the preceding Play091621 log found one import worker exceeded its shutdown timeout and was force-killed, followed by a lingering-session warning, then successful UDS shutdown messages. Parent reports82 tests passed and Editor exit0. That sequence is a possible lead, not proof that the worker caused the next shared-memory collision.
- Parent subsequently verified the exact gate26108/Unity46252 identities and found no DataStore process at that inspection. Parent stopped only the failed owned Unity subtree after413s and retained the gateFAIL evidence, without a cache/shared-process change. One same-source retry is root-owned gate22832, `Logs/gate_exec_Run_20261026T093055Z.log`. At read-only observation09:31:05Z, that log reaches `Launching Unity Data Store` at line134, a stage the failed attempt never reached. This is progress only; no diagnostic completion is claimed.
- `Logs/Editor-UDS.log` and `Logs/Editor-UDS-prev.log` rotate across launches. During the retry, the previous file held the failed attempt while the current file reported UDS client initialization. Current-file contents must not be attributed retrospectively to the failed attempt.

## Official evidence and limits

Unity issue [UUM-149968](https://issuetracker.unity.com/issues/24091/editor-hangs-and-reports-assets-as-corrupted-when-the-unity-datastore-service-process-terminates) describes Windows Editor artifact reads hanging after DataStore terminates, including repeated60-second waits and misleading corruption reports. Its reproduction deliberately kills the service; the report also notes a spontaneous build case. Unity marks the6000.6 branch fixed in6000.6.7f1, confirmed by the [6000.6.7f1 release notes](https://unity.com/releases/editor/whats-new/6000.6.7f1). This supports avoiding shared-service termination. It does **not** identify our earlier-initialization semaphore/pool error as the same defect or prove an upgrade would fix it. No editor/package upgrade is proposed within this task.

Microsoft documents that named semaphores may be held by several processes and survive until the final handle closes. Creating an existing semaphore can return its existing handle; names can also conflict with other synchronization objects. These are OS object lifetimes, not ordinary project lock files. [CreateSemaphoreW](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createsemaphorew). This makes surviving handles or stale/incompatible shared-memory state plausible hypotheses, but the log does not identify a handle owner or prove a cross-job collision.

Installed-binary string inspection by the independent reviewer finds shared-client-count diagnostics and internal shutdown/cleanup names. No documented supported command or public installed API for selectively repairing this shared state was found. Internal symbols or `--shutdown-grace` text alone are not a safe recovery interface. The gate's project-editor lock establishes project job ownership, not exclusive ownership of DataStore clients or named OS objects.

## Safest bounded recovery

1. Preserve the failed gate receipt, `Logs/gate_exec_Run_20261026T092340Z.log`, its `Logs/sessions/20261026T092340Z/` snapshots and available rotated UDS evidence. Keep the preceding successful `Logs/gate_test_playmode_20261026T091621Z.log` with its shutdown warnings. A later passing run must not erase this startup failure.
2. Let root's already-started, unchanged093055 attempt settle under its existing job boundary; do not launch another Editor concurrently or alter timeouts/settings. The failed-editor-only stop and single retry already performed are sufficient intervention for this diagnostic stage. If it completes, require the capture's normal source/global/owned-object checks and retain the earlier failure as intermittent infrastructure evidence; a pass will not identify its cause.
3. If the retry fails, hold further portrait launches. Keep other jobs running as requested. No confirmed safe shared-service reset exists under that constraint: do not kill DataStore, force-close another process's handles, delete `uds.lock`/`uds.db-lock`, clear Library/Temp, elevate privileges, alter packages or use undocumented internal cleanup flags.
4. A later maintenance opportunity can use normal graceful closure of the relevant Editors after their owners' jobs finish and save, followed by one pinned-version launch. This is conditional coordination, not permission to interrupt current work. If the shared state cannot be safely isolated or the error persists after a clean lifecycle, retain the logs/version/stack for a Unity bug/support report before considering any wider restart or upgrade. No such action or upload occurred here.

At final read-only check, retry log size remained7,749 bytes with last write09:31:05.2176165Z and DataStore launch present; capture02 was still absent. Final retry outcome belongs to root. Installed6000.6.4f1 remains the baseline; the documented6000.6.7f1 fix is context for later evaluation, not an instruction to switch toolchains now.

STATUS: COMPLETE — investigation/report only. Confirmed startup layer and recovery constraints; underlying cause remains unresolved. No native/GPU job, process inspection/action, cache/package/settings mutation or service operation was performed by this lane.

## Final retry failure and preserved sanitized receipt

The retry is conclusively **FAIL**, not still running. Direct parsing of `.harness/delivery/workspace-portraitcontrols-2026-10-26T09-30-55-262Z-stdout.txt` gives `ok=false`, `verdict=FAIL`, `exit=1`, `secs=10`, receipt UTC09:30:55Z. Its file was last written09:31:06.1794244Z; that is receipt filesystem time, not a separately observed process-exit timestamp. Log093055 line136 records `Unity Data Store initialise timeout, continuing as client processing.`, followed by the build-target/subtarget values. No portrait completion or capture02 exists.

For comparison, the first attempt's exact stdout receipt `.harness/delivery/workspace-portraitcontrols-2026-10-26T09-23-39-353Z-stdout.txt` records `ok=false`, `verdict=FAIL`, `exit=1`, `secs=413.2`; its last write is09:30:33.4032428Z. Both failures remain retained. No further native launches are scheduled by this lane.

The sanitized machine receipt is `BuildNotes/developer-workspace/portrait-startup-evidence.json` (SHA256 `e30ff6dbf5d9ed2f9a71857a2b71bbe11232ffbfb1c6e29e73a422ced870723b`). It contains only allowlisted gate outcome fields, selected startup errors, log byte counts/timestamps/hashes and explicitly attributed parent observations. Full startup logs were **not copied** into BuildNotes; the originals remain unchanged in ignored `Logs/`, preserving their private licensing/session/device metadata outside the staged report.

| Original diagnostic log | Bytes | SHA256 |
| --- | ---: | --- |
| `Logs/gate_exec_Run_20261026T092340Z.log` | 9492 | `5ae043995069f059ded0fac54df9bdfbae7ef612ce62be88c00b3884c9a7b2b3` |
| `Logs/gate_exec_Run_20261026T093055Z.log` | 7749 | `e206c90cc7a90503c09847f086034b5673e335d59d95b8cb36481e346e305724` |
| `Logs/Editor-UDS.log` (retry) | 274 | `46735d4c592bc4da516209d49b047ec5a8f91e36d57b1af4873e0f27fe1912c6` |
| `Logs/Editor-UDS-prev.log` (first failure) | 2536 | `1976787310cda9c79e68dda01c082ab65474eed3d1315f4808c27c9d2e3a80e8` |

Parent reports explicit09:33Z checks found zero Unity.DataStore.exe and zero Unity.exe processes, with approximately4.4GB free physical memory and13GB free virtual memory. These are parent observations, not new process queries by this lane. They do not identify a surviving handle owner, prove out-of-memory, or establish that another user's job caused either failure. All native work remains deferred; the safe next step is retaining this evidence and coordinating a later supported recovery opportunity without interrupting other jobs.

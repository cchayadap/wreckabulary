# Short-path Unity candidate installation verification

STATUS: IN PROGRESS — read-only bounded monitoring. No installer, editor, UI/UAC interaction, process termination, or package mutation by this lane.

Root-owned wrapper PID: `34936`; script: `.harness/delivery/install-unity-short.ps1`; status: `.harness/delivery/unity-short-install-status.json`. Intended fresh destination: `C:/Users/mrkl6/wb64/6000.6.4f1`.

Purpose: observe the actual download/install result and then check the named Sprite metadata/source and ObjectDispatcher assembly-definition files omitted at long paths in the first installation. Existing diagnosis remains `UNITY-PATCH-PACKAGE-DIAGNOSIS.md`; file presence alone will not establish compilation or gameplay success.

## Observations

| UTC | Bounded observation |
| --- | --- |
| 2026-10-24 01:45:34 | Wrapper status `running`, started 01:41:34.6272243Z, exit/finish absent; PID 34936 alive. Wrapper script requests exact `6000.6.4f1`, changeset `12bfff696524`, `--download-directory wb64 --no-elevate --format json` from `C:/Users/mrkl6`. Stdout/stderr are empty; target directory has not yet appeared. No basis to label this UAC waiting or install completion. |
| 2026-10-24 01:47:55 | Still `running`; exact installer cache file is 1,239,195,719 bytes and was being written at this observation. Target/editor absent; stdout/stderr empty. This is observed download progress, not verified download completion or a UAC-state observation. |
| 2026-10-24 01:50:44 | Download 1,745,598,923 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 01:53:11 | Download 2,227,804,295 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 01:55:29 | Download 2,600,231,079 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 01:57:45 | Download 2,948,313,522 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 02:00:13 | Download 3,458,117,318 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 02:02:46 | Download 3,779,101,684 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |

Known download file, supplied by root and read directly: `C:/Users/mrkl6/AppData/Roaming/UnityHub/downloads/portable-624547c3fbda70f2/UnitySetup64-6000.6.4f1.exe`. No hashing while it is being written. The installer may automatically remove this temporary cache after success; preserve the wrapper's final record before drawing conclusions from cache absence.

# Visible Windows crash review

STATUS: CONFIRMED NATIVE CRASH; managed cause not yet established. Read-only review of the existing `release-qa` build `20261023T174755Z` and its visible workshop-flow run `175916Z`. No relaunch, debugger, package install, native DLL load, production edit or heavy job by this reviewer. The earlier official hidden-run FAIL remains unchanged.

## Observed run and failure

- `.harness/delivery/visible-player-status.json` records a diagnostic-only normal-window run at 1280×720, from 17:59:16.3226454Z to process completion 18:00:00.6286047Z. Exit `-1073741819` corresponds to `0xC0000005`. This run is not an official gate replacement.
- The executable SHA-256 is `96B492CB271111251FE42B8646E65370A1B7B566773A1E35B34C3F2D1AE70873`. Build metadata records non-development Mono, Unity 6000.6.3f1, source `a4f9c1fc195c3ec23536053cd9cc9c71ac5d0c10`. Preserve the whole build; the launcher hash alone does not identify all its data.
- The output directory has `wordbound_party.png` (17:59:29Z) and `wordbound_workshop.png` (17:59:37Z), but no final `checks.json`, local-world/menu/return captures, or finished scenario acceptance. I inspected the workshop PNG: it contains the real workshop UI/geometry, dense overlapping labels and a clipped autosave path error. Root inspected both UI captures. Rendering these earlier views does not establish playable-world acceptance.
- The actual player log uses Direct3D 11 on Intel Arc, driver 32.0.101.8860. Its last ordinary line is Fusion adding the local player, followed immediately by the crash report. There is no preceding managed exception stack. Earlier optional URP post-processing shader warnings remain evidence, but are not demonstrated crash causes.

## Minidump facts

Read only the standard minidump directory, exception and module metadata in `C:/Users/mrkl6/AppData/Local/Temp/Wreckabulary Team/Wreckabulary/Crashes/Crash_2026-10-23_175947680/crash.dmp` (1,316,406 bytes). No general memory/string dump was printed. The exception stream identifies the faulting thread and exception record; for access violations, the first parameter distinguishes read/write and the second is the inaccessible address. See [Microsoft's exception stream](https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ns-minidumpapiset-minidump_exception_stream) and [exception parameter definition](https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ns-minidumpapiset-minidump_exception).

| Field | Observed value |
| --- | --- |
| Dump timestamp | 2026-10-23 17:59:48Z |
| Exception thread | 8408 |
| Exception code / flags | `0xC0000005` / 0 |
| Access | Read (`ExceptionInformation[0] = 0`) from address `0x0` |
| Fault instruction | `0x7FF9B5BE059F` inside `UnityPlayer.dll` |
| Module base / fault RVA | `0x7FF9B4B40000` / `0x10A059F` |
| Module image size | 39,845,888 bytes |

The crash directory/log timestamps locate the fault around 17:59:47–48Z; 18:00:00Z is the launcher-observed process completion. The unresolved stack does not establish a graphics-driver defect, an autosave/native causal link, or a specific managed callback. Being in UnityPlayer is a location, not a complete diagnosis.

## Exact symbol capability

The matching symbols are already installed under:

`C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/win64_player_nondevelopment_mono/`

The build and installed `UnityPlayer.dll` both hash to `7C8127007131559A3DAA04AA3A3E8D1477975FE449F59917C5C75D86FA1F904F`. The installed `UnityPlayer_Win64_player_mono_x64.pdb` is 27,463,680 bytes. A bounded read of its MSF info stream gives GUID `742897d4-4d9e-4949-9886-86d4725f961e`, age 1, exactly matching the dump module's RSDS record. No debugger executable was found on PATH; that does not mean symbolization is unavailable. Root owns the proposed offline Windows DbgHelp step with this matching PDB. Unity documents [forensic crash debugging](https://docs.unity3d.com/6000.0/Documentation/Manual/WindowsDebugging-forensic.html).

Logged UnityPlayer RVAs in stack order:

`0x10A059F`, `0x146E38E`, `0x146E67E`, `0x14E76B5`, `0x14FF045`, `0x14FF3C7`, `0x12C780B`, `0x12CA59D`, `0x12CE4FB`, `0x12CE67B`.

The log's generic exported `UnityMain` labels are insufficient to name the internal functions. Resolve the exact fault and callers first; there is no need to modify SDK/packages or relaunch merely to obtain those symbols.

## Managed source boundary and separate usability evidence

The inspected QA, launcher, local-client, controller and workshop sources have no diff from the build source for this path. After the workshop screenshot, `WordboundWorkshopQa.Playtest` calls the real `RequestPlaytest`; the launcher creates the exact local content/client, hides the workshop and awaits `StartLocal`. That client initializes player camera, visuals, guide and HUD before starting the local Fusion session. Scene preparation, course-world construction and host body reconciliation occur during startup. The last player-added line places the failure after runner startup began; without symbols or completed phase observations, it cannot select among those engine-facing operations or prove which completed.

The workshop autosave error is caught and displayed by `WordboundWorkshop.Update`; `RequestPlaytest` validates the in-memory draft and does not require a successful autosave. This explains how the visible error and later player startup can coexist. It does not show that the storage error caused a native null read. The isolated draft directories exist but contain no saved files in this run; investigate that path separately after preserving the error. The crowded labels and clipped error also remain visible usability issues, independent of the crash.

Next acceptance step: use the existing matching PDB to resolve the fault and immediate callers, then select a bounded source/phase check based on those functions. Retain the full failed visible run and official hidden failure. A tested Windows delivery still requires a complete successful workshop→local playtest→return→Undo/Redo run and its captures, followed by the other stated multiplayer/performance gates.

Evidence SHA-256: crash dump `C51E32E6DFBA3EDC5DD2B08C5D8DE40D3BE687D14D5EC8349AA27EF09BFA5409`; player log `384F99851A40850521B200F877E59D12CBD7B55170F9E94B64B8EA5AB90D745A`.
# Windows player launch comparison

STATUS: read-only comparison complete; visible-launch diagnostic NOT-RUN by this lane. Official smoke `20261023T175202Z-wordbound_workshop_flow` remains FAIL. No player launch, UI action, server write or kit edit was performed here. Root owns the subsequent diagnostic.

## Verified build and failure

All paths below are relative to `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration` unless specified otherwise.

- `Builds/qa-20261023T174755Z/build.json`: release-qa, Unity 6000.6.3f1, `ok:true`, source `a4f9c1fc195c3ec23536053cd9cc9c71ac5d0c10`, profile `Assets/Settings/Build Profiles/Win-QA.asset`. Metadata build duration is 230.8 seconds; root reports 232.5 seconds for the enclosing gate.
- `Builds/qa-20261023T174755Z/Wreckabulary.exe` SHA256: `96b492cb271111251fe42b8646e65370a1b7b566773a1e35b34c3f2d1ae70873`. This identifies the launcher; preserve the entire existing build directory, including data/managed assemblies, for comparison.
- `Docs/qa/runs/20261023T175202Z-wordbound_workshop_flow/checks.json`: FAIL, seed 1, player, non-development. `open_wordbound` failed at 48.15398 seconds after the unchanged 45-second render-readiness deadline. Splash finished, application focused, active/visible menu, active assigned camera, display 0 and no target texture were true; successful menu repaints and camera callbacks were both zero. Frame and rendered-frame counters both read 152131, which does not establish displayed rendering.
- The same checks show edit, return and Undo/Redo assertions PASS, but `exact_playtest` failed its 90-second deadline. Both recorded workshop PNGs were opened and are visibly black. No party PNG was recorded because its readiness gate failed before capture. These passing state assertions are not visual acceptance.
- `Logs/player/20261023T175202Z-wordbound_workshop_flow.log`: D3D11 initialized on Intel Arc; the checks recorded zero errors/exceptions. A post-QA autosave warning reports the temporary draft directory missing. That later cleanup warning does not explain the earlier zero repaint/camera observations and should remain visible in the failure record.
- Root observed PID 10976 responding with MainWindowHandle zero twice during execution. A read-only process query after completion found that PID absent. This lane did not observe its desktop window. Root reports the enclosing smoke ended FAIL in 178.8 seconds.

## Exact launch difference

`Tools/kit/gate/lib/player.mjs` constructs the executable from validated build metadata, uses its containing directory as cwd, then calls shared `run(exeAbs,args,...)`. `Tools/kit/lib/proc.mjs:141` ultimately uses Node `spawn` with `windowsHide:true`, ignored stdin and piped stdout/stderr. The Windows executable is not launched through a shell wrapper by this path.

`Tools/kit/gate/gate.config.json` supplies only `-screen-fullscreen 0 -screen-width 1280 -screen-height 720`. The QA path appends `-qa-scenario wordbound_workshop_flow -qa-seed 1 -qa-out <absolute directory> -logFile <absolute file>`. It adds no `-batchmode`, `-nographics` or `-qa-timeout`. The 240-second smoke allowance plus configured 30-second grace is an external 270-second watchdog; it does not replace the scenario's 45/60/90-second limits.

`windowsHide:true` is a concrete difference worth isolating. Node documents it as hiding the subprocess **console** window; that documentation alone does not prove it hid Unity's graphical window or caused these failures. A normal visible launch is a differential experiment, not an established fix. [Node child-process documentation](https://nodejs.org/api/child_process.html)

## Local rule and authorization boundary

`Docs/kit/HARNESS_RULES.md` section 3 says: "Unity work is verified with ... game_gate.mjs ... never by starting Unity.exe yourself." Sections 5 and 8 reserve Unity execution for the integrator and prohibit kit edits mid-run. `CLAUDE.md` places direct owner instructions above kit templates.

This comparison launches an already built `Wreckabulary.exe`, not the Unity editor, and does not record a verification rung or close a milestone. No inspected local rule expressly prohibits this supplemental diagnostic or requires another user approval. Root can perform it under the existing instruction to prepare and test a usable Windows player. All official verification remains through the gate; no gate ledger, kit source or failed evidence should be edited. This conclusion does not authorize shell-based UI automation or unrelated process control.

## Proposed root-only comparison (not executed here)

Wait for the current smoke and its exact player process to exit. Keep the binary, data files, working directory, user session/environment and QA/game arguments unchanged. Use a fresh supplemental output location to preserve the failed gate evidence. `Start-Process -WindowStyle Normal` requests a normal window; actual visibility must still be observed. Microsoft recommends one quoted argument string when arguments contain spaces. [Start-Process documentation](https://learn.microsoft.com/powershell/module/Microsoft.PowerShell.Management/Start-Process)

```powershell
$projectDir = 'C:\Users\mrkl6\Downloads\Projects\Wreckabulary\wordbound-integration'
$buildDir = Join-Path $projectDir 'Builds\qa-20261023T174755Z'
$exePath = Join-Path $buildDir 'Wreckabulary.exe'
$diagnosticId = 'visible-launch-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N')
$diagnosticDir = Join-Path $projectDir ('BuildNotes\client-view\' + $diagnosticId)
$qaOut = Join-Path $diagnosticDir 'qa'
$playerLog = Join-Path $diagnosticDir 'Player.log'
New-Item -ItemType Directory -Path $qaOut | Out-Null
$playerArguments = '-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -qa-scenario wordbound_workshop_flow -qa-seed 1 -qa-out "{0}" -logFile "{1}"' -f $qaOut, $playerLog
$player = Start-Process -FilePath $exePath -ArgumentList $playerArguments -WorkingDirectory $buildDir -WindowStyle Normal -PassThru
```

Root should retain the returned process identity/start time and enforce the same 270-second outer bound without delaying commentary for the entire run. Poll only that owned process; if cleanup is needed, act on its verified PID, never a process name. This snippet deliberately performs no UI automation, changes no rendering flags, adds no retry and does not elevate or switch accounts.

Record the requested launch mode, exact argument string, build/hash, process/session ID, nonzero main-window handle if present, real window observation if available, and final exit/timeout. Inspect fresh checks/log/PNGs for two successful party repaints and assigned-camera callbacks, visible nonblank party/workshop images, exact playtest completion and retained draft/Undo. A normal-window request, `Responding:true`, `Application.isFocused:true`, initialized D3D device or high frame counter alone is not proof of a usable window. Native Computer Use is unavailable in this session; do not substitute unapproved PowerShell UI automation.

If rendering and scenario behavior differ, the launcher/window state becomes a supported cause candidate; retain both runs and investigate the official gate launch path through its permitted workflow. If behavior is identical, keep rendering/camera/player initialization open. A supplemental pass would not repair the official FAIL, prove a stripped release, establish public multiplayer acceptance or establish player performance. R4 and final Windows release acceptance still require their proper exact-build gate runs.

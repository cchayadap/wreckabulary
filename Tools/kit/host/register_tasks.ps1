<#
.SYNOPSIS
  Registers the workspace's watchdog (every minute) and supervisor (every 5 minutes) with Task Scheduler.
.DESCRIPTION
  Human step H14, run through `kit.mjs host-tasks install` after the owner says yes. Tasks run for the current user
  without elevation, through Register-ScheduledTask -Force (never schtasks /Create: it cannot change the battery
  defaults, caps /TR at 261 characters and mangles embedded quotes under 5.1), with the CONTRACTS section 4.3 pinned action,
  trigger and settings: start and keep running on battery, start when available, ignore overlapping runs, stop a
  run after 4 minutes, and launch through `conhost --headless` so no window flashes up.
  The tasks never run code inside W. This script copies the host scripts it ships with (this folder) to
  %LOCALAPPDATA%\GameStudioKit\host\<slug>-<W hash>\, points the task actions there with -Workspace W, and records each
  copy's SHA-256 in the task description (kit doctor compares them).
  With -WhatIf it copies and registers nothing and prints the tasks it would register, built from the same objects.
  It refuses to replace a same-named task that runs for another workspace unless -Force is given, and refuses to touch
  Task Scheduler while test-seam variables (GSK_HOST_FAKE, GSK_NOW, GSK_HOME, GSK_HOST_NO_TOAST) are set.
.PARAMETER Workspace
  The workspace root W (it must hold Tools\kit\kit.json). Default: three folders above this script.
.PARAMETER WhatIf
  Print the task definitions as one JSON line; copy and register nothing.
.PARAMETER TestSeams
  Tests only: honour the GSK_* test-seam variables.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools\kit\host\register_tasks.ps1 -Workspace . -WhatIf
#>
[CmdletBinding()]
param(
  [string]$Workspace,
  [switch]$WhatIf,
  [switch]$Force,
  [switch]$TestSeams
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. ([IO.Path]::Combine($PSScriptRoot, 'lib', 'host.ps1'))
. ([IO.Path]::Combine($PSScriptRoot, 'lib', 'tasks.ps1'))
if ($TestSeams) { Enable-GskTestSeams }

try {
  $W = Resolve-GskWorkspace -Workspace $Workspace -ScriptDir $PSScriptRoot
  $userId = [Security.Principal.WindowsIdentity]::GetCurrent().Name
  $hostDir = Get-GskTaskHostDir $W
  $rows = New-Object 'System.Collections.Generic.List[object]'
  $defs = New-Object 'System.Collections.Generic.List[object]'
  $errors = New-Object 'System.Collections.Generic.List[string]'
  if (-not (Test-GskKitInstalled $W)) { $errors.Add("$W has no Tools\kit\kit.json: install the kit into this workspace first") }
  $leaks = Get-GskSeamLeaks
  if ($leaks.Count -gt 0) { $errors.Add("test-seam variables are set ($($leaks -join ', ')): remove them from the environment before registering tasks") }
  $srcHashes = Get-GskHostFileHashes -Dir $PSScriptRoot
  foreach ($h in $srcHashes) { if ($null -eq $h.sha256) { $errors.Add("missing host file $($h.file) next to register_tasks.ps1") } }
  $wsPrefix = $W.TrimEnd('\') + '\'
  if ($hostDir.StartsWith($wsPrefix, [StringComparison]::OrdinalIgnoreCase)) { $errors.Add("the task copy folder $hostDir is inside the workspace") }
  foreach ($s in (Get-GskTaskSpecs -Workspace $W -HostDir $hostDir)) {
    $action = New-ScheduledTaskAction -Execute 'conhost.exe' -Argument (Get-GskTaskArgument -Script $s.script -Workspace $W) -WorkingDirectory $W
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes $s.interval_min)
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 4)
    $principal = New-ScheduledTaskPrincipal -UserId $userId -LogonType Interactive -RunLevel Limited
    $existing = Get-GskExistingTask -Name $s.name
    $conflict = ($null -ne $existing -and -not (Test-GskTaskForWorkspace -Existing $existing -Workspace $W))
    if ($conflict -and -not $Force) { $errors.Add("task $($s.name) already runs for another workspace ($($existing.argument)); -Force replaces it") }
    $description = Get-GskTaskDescription -What $s.what -Workspace $W -HostDir $hostDir -Hashes $srcHashes
    $rows.Add([ordered]@{
        name = $s.name; execute = [string]$action.Execute; argument = [string]$action.Arguments; working_directory = [string]$action.WorkingDirectory
        interval_min = $s.interval_min; script = $s.script; description = $description
        trigger = [ordered]@{ start = [string]$trigger.StartBoundary; interval = [string]$trigger.Repetition.Interval; duration = [string]$trigger.Repetition.Duration }
        settings = [ordered]@{
          DisallowStartIfOnBatteries = [bool]$settings.DisallowStartIfOnBatteries; StopIfGoingOnBatteries = [bool]$settings.StopIfGoingOnBatteries
          StartWhenAvailable = [bool]$settings.StartWhenAvailable; MultipleInstances = [string]$settings.MultipleInstances
          ExecutionTimeLimit = [string]$settings.ExecutionTimeLimit
        }
        principal = [ordered]@{ user_id = [string]$principal.UserId; logon_type = [string]$principal.LogonType; run_level = [string]$principal.RunLevel }
        exists = ($null -ne $existing); conflict = $conflict; registered = $false
      })
    $defs.Add([pscustomobject]@{ name = $s.name; what = $s.what; action = $action; trigger = $trigger; settings = $settings; principal = $principal })
  }
  $result = [ordered]@{
    ok = $true; exit = 0; cmd = 'register_tasks'; what_if = [bool]$WhatIf; workspace = $W; host_dir = $hostDir
    source_dir = $PSScriptRoot; files = $srcHashes.ToArray(); copied = $false; tasks = $rows.ToArray()
  }
  if ($errors.Count -gt 0) {
    $result.ok = $false
    $result.exit = 1
    $result['errors'] = $errors.ToArray()
    Write-GskResult $result
    exit 1
  }
  if (-not $WhatIf) {
    if (Test-GskFake) { throw 'GSK_HOST_FAKE is set: refusing to touch Task Scheduler (tests use -WhatIf)' }
    $copied = Copy-GskHostFiles -SourceDir $PSScriptRoot -DestDir $hostDir
    $result.files = $copied.ToArray()
    $result.copied = $true
    for ($i = 0; $i -lt $defs.Count; $i++) {
      $d = $defs[$i]
      $desc = Get-GskTaskDescription -What $d.what -Workspace $W -HostDir $hostDir -Hashes $copied
      [void](Register-ScheduledTask -TaskName $d.name -Action $d.action -Trigger $d.trigger -Settings $d.settings -Principal $d.principal -Description $desc -Force)
      $rows[$i].registered = $true
      $rows[$i].description = $desc
    }
    $result.tasks = $rows.ToArray()
  }
  Write-GskResult $result
  exit 0
} catch {
  Write-GskResult ([ordered]@{ ok = $false; exit = 1; cmd = 'register_tasks'; error = $_.Exception.Message })
  exit 1
}

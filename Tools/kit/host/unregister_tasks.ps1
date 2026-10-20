<#
.SYNOPSIS
  Removes the workspace's gsk-watchdog-<slug> and gsk-supervisor-<slug> tasks from Task Scheduler.
.DESCRIPTION
  Run through `kit.mjs host-tasks remove` (and by `kit uninstall`) after the owner says yes. It unregisters exactly
  those two names with Unregister-ScheduledTask -Confirm:$false, and only when they run for this workspace (-Force
  removes a same-named task of another workspace). The per-user copy of the host scripts (host_dir) is left in place:
  without the tasks nothing runs it, and a later registration overwrites it.
  With -WhatIf it removes nothing and prints what it would remove.
.PARAMETER Workspace
  The workspace root W. Default: three folders above this script when W/Tools/kit/kit.json exists there.
.PARAMETER WhatIf
  Print one JSON line with each task and whether it would be removed; remove nothing.
.PARAMETER TestSeams
  Tests only: honour the GSK_* test-seam variables.
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
  $hostDir = Get-GskTaskHostDir $W
  $rows = New-Object 'System.Collections.Generic.List[object]'
  $errors = New-Object 'System.Collections.Generic.List[string]'
  $leaks = Get-GskSeamLeaks
  if ($leaks.Count -gt 0) { $errors.Add("test-seam variables are set ($($leaks -join ', ')): remove them from the environment before changing tasks") }
  foreach ($s in (Get-GskTaskSpecs -Workspace $W -HostDir $hostDir)) {
    $existing = Get-GskExistingTask -Name $s.name
    $conflict = ($null -ne $existing -and -not (Test-GskTaskForWorkspace -Existing $existing -Workspace $W))
    if ($conflict -and -not $Force) { $errors.Add("task $($s.name) runs for another workspace ($($existing.argument)); -Force removes it anyway") }
    $rows.Add([ordered]@{ name = $s.name; exists = ($null -ne $existing); conflict = $conflict; would_remove = ($null -ne $existing -and (-not $conflict -or [bool]$Force)); removed = $false })
  }
  $result = [ordered]@{ ok = $true; exit = 0; cmd = 'unregister_tasks'; what_if = [bool]$WhatIf; workspace = $W; host_dir = $hostDir; tasks = $rows.ToArray() }
  if ($errors.Count -gt 0) {
    $result.ok = $false
    $result.exit = 1
    $result['errors'] = $errors.ToArray()
    Write-GskResult $result
    exit 1
  }
  if (-not $WhatIf) {
    if (Test-GskFake) { throw 'GSK_HOST_FAKE is set: refusing to touch Task Scheduler (tests use -WhatIf)' }
    foreach ($r in $rows) {
      if (-not $r.would_remove) { continue }
      Unregister-ScheduledTask -TaskName $r.name -Confirm:$false
      $r.removed = $true
    }
    $result.tasks = $rows.ToArray()
  }
  Write-GskResult $result
  exit 0
} catch {
  Write-GskResult ([ordered]@{ ok = $false; exit = 1; cmd = 'unregister_tasks'; error = $_.Exception.Message })
  exit 1
}

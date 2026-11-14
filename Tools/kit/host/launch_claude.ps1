<#
.SYNOPSIS
  Starts Claude Code in this console from a launch plan written by run.ps1 or supervisor.ps1.
.DESCRIPTION
  The Windows Terminal tab (or console) that run.ps1 or supervisor.ps1 opens runs this script. The plan lives in the
  agent-writable .harness/launch/ folder, so this script trusts it only after these checks:
    - its SHA-256 equals -PlanSha256, which the opener put on this tab's command line (nobody else can set it);
    - it is <W>\.harness\launch\<name>.json, W is the plan's workspace and this console's folder, and W holds
      Tools\kit\kit.json;
    - it is at most 10 minutes old and has not been used (a used plan is renamed to <name>.json.used before claude
      starts, so a restarted or duplicated tab cannot run it again);
    - its args have exactly the shape run.ps1 and supervisor.ps1 produce ([-n <name>] [--continue | --resume=<uuid>]
      --permission-mode auto <prompt>), its env holds only the allow-listed variables, and no line starts with OWNER:;
    - no Claude Code session of W is running (unless the owner passed run.ps1 -Force);
    - the command line fits the route's limit (32000 characters; 7800 through an npm .cmd shim).
  It then removes the GSK_* test seams from its environment, sets the allow-listed variables (the Stop-hook block cap
  and the workflow concurrency; a new Windows Terminal tab does not inherit the caller's environment), starts claude
  (an npm install runs as node + the package's entry script, so no cmd.exe parses the prompt), records
  .harness/claude.pid (pid, process start time and the image Windows reports), waits, and exits with Claude Code's code.
.PARAMETER Plan
  Path of the launch plan, absolute or relative to the current folder (W).
.PARAMETER PlanSha256
  SHA-256 (hex) of the plan file, from the command line the opener built.
.PARAMETER DryRun
  Validate the plan and print the command; start and rename nothing.
.PARAMETER TestSeams
  Tests only: honour the GSK_* test-seam variables.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$Plan,
  [Parameter(Mandatory = $true)][string]$PlanSha256,
  [switch]$DryRun,
  [switch]$TestSeams
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. ([IO.Path]::Combine($PSScriptRoot, 'lib', 'host.ps1'))
if ($TestSeams) { Enable-GskTestSeams }

$script:MaxPlanAgeMin = 10

try {
  $now = Get-GskNow
  if ($PlanSha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw '-PlanSha256 must be 64 hex characters' }
  $planPath = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Plan))
  if (-not [IO.File]::Exists($planPath)) {
    if ([IO.File]::Exists("$planPath.used")) { throw "launch plan already used: $planPath (each plan starts Claude Code once)" }
    throw "launch plan missing: $planPath"
  }
  $bytes = [IO.File]::ReadAllBytes($planPath)
  if (-not [string]::Equals((Get-GskSha256Hex -Bytes $bytes), $PlanSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'launch plan SHA-256 does not match -PlanSha256: the plan changed after it was written'
  }
  $doc = ConvertFrom-GskJsonBytes $bytes
  if ($null -eq $doc) { throw "launch plan is not JSON: $planPath" }
  if ([int](Get-GskField $doc 'schema' 0) -ne 1) { throw 'launch plan schema must be 1' }

  $ws = [string](Get-GskField $doc 'workspace' '')
  if (-not $ws -or -not [IO.Path]::IsPathRooted($ws) -or -not [IO.Directory]::Exists($ws)) { throw "launch plan workspace not found: $ws" }
  $ws = [IO.Path]::GetFullPath($ws).TrimEnd('\')
  if (-not (Test-GskSameDir ([IO.Path]::GetDirectoryName($planPath)) ([IO.Path]::Combine($ws, '.harness', 'launch'))) -or $planPath -notmatch '\.json$') {
    throw 'launch plan must be <workspace>\.harness\launch\<name>.json of the workspace it names'
  }
  $cwd = (Get-Location).ProviderPath
  if (-not (Test-GskSameDir $cwd $ws)) { throw "this console's folder ($cwd) is not the plan's workspace" }
  if (-not (Test-GskKitInstalled $ws)) { throw "the plan's workspace has no Tools\kit\kit.json" }
  $utc = ConvertFrom-GskUtc ([string](Get-GskField $doc 'utc' ''))
  if ($null -eq $utc) { throw 'launch plan has no utc' }
  $ageMin = ($now - $utc).TotalMinutes
  if ($ageMin -gt $script:MaxPlanAgeMin -or $ageMin -lt -5) { throw "launch plan is $([Math]::Round($ageMin, 1)) min old (limit $($script:MaxPlanAgeMin)): start a new launch" }

  $mode = [string](Get-GskField $doc 'mode' '')
  if (@('new', 'continue', 'resume') -notcontains $mode) { throw "launch plan mode must be new, continue or resume: $mode" }
  $argv = New-Object 'System.Collections.Generic.List[string]'
  foreach ($a in @(Get-GskField $doc 'args' @())) {
    if ($a -isnot [string]) { throw 'launch plan args must be strings' }
    $argv.Add($a)
  }
  if ($argv.Count -eq 0) { throw 'launch plan has no args' }
  foreach ($a in $argv) {
    foreach ($line in ($a -split "`n")) {
      if ($line.Trim() -match '^OWNER(-OVERRIDE)?:') { throw 'launch plan text contains an OWNER: command line; owner commands are typed by the owner' }
    }
  }
  $shapeError = Test-GskLaunchArgs -Argv $argv.ToArray() -Mode $mode
  if ($shapeError) { throw $shapeError }
  $envSet = [ordered]@{}
  $envDoc = Get-GskField $doc 'env' $null
  if ($null -ne $envDoc) {
    foreach ($p in $envDoc.PSObject.Properties) {
      if ($script:GskLaunchEnvAllow -cnotcontains $p.Name) { throw "launch plan env key not allowed: $($p.Name)" }
      $v = [string]$p.Value
      if ($v -notmatch '^\d{1,4}$') { throw "launch plan env value must be a small integer: $($p.Name)" }
      $envSet[$p.Name] = $v
    }
  }
  if ($envSet.Contains('CLAUDE_CODE_STOP_HOOK_BLOCK_CAP') -and $envSet['CLAUDE_CODE_STOP_HOOK_BLOCK_CAP'] -eq '0') { throw 'CLAUDE_CODE_STOP_HOOK_BLOCK_CAP=0 disables the cap; the kit never uses 0' }
  $parallel = Get-GskField $doc 'allow_parallel' $false
  if ($parallel -isnot [bool]) { throw 'launch plan allow_parallel must be true or false' }

  $sessionRef = $null
  $name = $null
  for ($i = 0; $i -lt $argv.Count - 1; $i++) {
    if ($argv[$i].StartsWith('--resume=')) { $sessionRef = $argv[$i].Substring(9) }
    if ($argv[$i] -ceq '-n') { $name = $argv[$i + 1] }
  }
  if (-not $parallel) {
    $sessions = Get-GskWorkspaceSessions -Workspace $ws -Names @($name) -SessionId ([string]$sessionRef)
    $running = (Get-GskPidList $sessions.ours).ToArray()
    if ($running.Length -gt 0) { throw "a Claude Code session of this workspace is already running (PID $($running -join ',')); not starting another" }
  }

  $command = Resolve-GskClaudeCommand
  if ($null -eq $command) { throw 'claude was not found on PATH' }
  $launch = Get-GskClaudeCommandLine -Command $command -Argv $argv.ToArray()
  if ($launch.chars -gt $launch.max) {
    throw "the claude command line would be $($launch.chars) characters (limit $($launch.max) on the $($launch.route) route): put the kickoff text in a project file and point the prompt at it"
  }
  $result = [ordered]@{
    ok = $true; exit = 0; cmd = 'launch_claude'; dry_run = [bool]$DryRun; workspace = $ws; mode = $mode
    exe = $launch.file; route = $launch.route; arguments = $launch.arguments; chars = $launch.chars; env = $envSet; pid = $null
  }
  if ($null -ne $command.shim) { $result['shim'] = $command.shim }
  if ($DryRun) { Write-GskResult $result; exit 0 }
  if (Test-GskInsideClaude) { throw 'refusing to start Claude Code from inside a Claude Code session (CLAUDECODE is set)' }

  # Single use: the rename fails when another tab already took this plan.
  $usedPath = "$planPath.used"
  try { [IO.File]::Move($planPath, $usedPath) } catch { throw "launch plan already used or locked: $planPath" }
  $fakeDir = $null
  if (Test-GskFake) { $fakeDir = Get-GskFakeDir }
  $leaks = Get-GskSeamLeaks
  if ($leaks.Count -gt 0) { Write-GskNote "launch_claude: removing test-seam variables from Claude Code's environment ($($leaks -join ', '))" }
  Clear-GskSeamEnv
  foreach ($k in $envSet.Keys) { [Environment]::SetEnvironmentVariable($k, $envSet[$k], 'Process') }

  $planRel = $usedPath
  $wsPrefix = $ws + '\'
  if ($usedPath.StartsWith($wsPrefix, [StringComparison]::OrdinalIgnoreCase)) { $planRel = $usedPath.Substring($wsPrefix.Length) }
  if ($null -ne $fakeDir) {
    Add-GskJsonl -Path ([IO.Path]::Combine($fakeDir, 'claude_starts.jsonl')) -InputObject ([ordered]@{
        utc = (Format-GskUtc $now); exe = $launch.file; route = $launch.route; arguments = $launch.arguments; workspace = $ws; env = $envSet
        plan = ($planRel -replace '\\', '/'); seams_left = @($script:GskSeamVars | Where-Object { [Environment]::GetEnvironmentVariable($_, 'Process') })
      })
    $result['faked'] = $true
    Write-GskResult $result
    exit 0
  }
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $launch.file
  $psi.Arguments = $launch.arguments
  $psi.WorkingDirectory = $ws
  $psi.UseShellExecute = $false
  $proc = [System.Diagnostics.Process]::Start($psi)
  $start = $now
  try { $start = $proc.StartTime.ToUniversalTime() } catch { }
  Write-GskJsonAtomic -Path (Get-GskHarnessPath $ws @('claude.pid')) -InputObject ([ordered]@{
      pid = $proc.Id; process_start_utc = (Format-GskUtc $start); image = (Get-GskProcessImage -Id $proc.Id -Fallback $launch.file); workspace = $ws
      launched_utc = (Format-GskUtc $now); mode = $mode; route = $launch.route; session_ref = $sessionRef; name = $name
      plan = ($planRel -replace '\\', '/')
    })
  $proc.WaitForExit()
  exit $proc.ExitCode
} catch {
  Write-GskResult ([ordered]@{ ok = $false; exit = 1; cmd = 'launch_claude'; error = $_.Exception.Message })
  exit 1
}

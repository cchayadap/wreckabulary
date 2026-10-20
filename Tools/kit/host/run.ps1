<#
.SYNOPSIS
  Game Studio Kit launcher: opens the build's interactive Claude Code session in a Windows Terminal tab.
.DESCRIPTION
  Run by the owner (never by the agent). Sets CLAUDE_CODE_STOP_HOOK_BLOCK_CAP=200 and
  CLAUDE_CODE_WORKFLOW_MAX_CONCURRENT_AGENTS=6 (the block cap lives in the launch environment, never in a settings
  file), then opens `wt -w gsk nt -d <W>` running `claude -n <name> --permission-mode auto "<kickoff>"`.
    new (default)  claude -n <name> --permission-mode auto "<kickoff>"
    -Resume        claude -n <name> --continue --permission-mode auto "<resume prompt>"
    -SessionId id  claude -n <name> --resume=<id> --permission-mode auto "<resume prompt>"
  The tab runs launch_claude.ps1 on a plan in .harness/launch/ (bound to the tab by its SHA-256), which sets the
  environment there too (a new Windows Terminal tab does not inherit this process's environment) and records
  .harness/claude.pid for the supervisor.
  It writes no settings file. It refuses when a Claude Code session of W is already running or a launch started less
  than 2 minutes ago (-Force overrides both), when started from inside a Claude Code session, when the kickoff text
  holds OWNER: command lines, and when the command line would be too long for the claude route. It warns (read-only)
  when the run is not armed, when the last session ran outside the CLI (desktop app, SDK), and when the PC sleeps or
  hibernates on AC power (human step H3).
.PARAMETER Workspace
  The workspace root W. Default: three folders above this script when W/Tools/kit/kit.json exists there.
.PARAMETER Name
  Session name (claude -n). Default: run.json name.
.PARAMETER KickoffFile
  UTF-8 file with the first prompt. Default: a short prompt that starts the game-studio-run skill.
.PARAMETER DryRun
  Print the plan and the command; write and launch nothing.
.PARAMETER TestSeams
  Tests only: honour the GSK_* test-seam variables.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools\kit\host\run.ps1 -Workspace . -Name m1-overnight
#>
[CmdletBinding()]
param(
  [string]$Workspace,
  [string]$Name,
  [string]$KickoffFile,
  [switch]$Resume,
  [string]$SessionId,
  [ValidateRange(1, 256)][int]$MaxAgents = 6,
  [switch]$Force,
  [switch]$DryRun,
  [switch]$TestSeams
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. ([IO.Path]::Combine($PSScriptRoot, 'lib', 'host.ps1'))
if ($TestSeams) { Enable-GskTestSeams }

$script:RelaunchGuardMin = 2

function Stop-Run {
  param([string]$Message, [AllowNull()]$Extra)
  $r = [ordered]@{ ok = $false; exit = 1; cmd = 'run'; error = $Message }
  if ($null -ne $Extra) { foreach ($k in $Extra.Keys) { $r[$k] = $Extra[$k] } }
  Write-GskResult $r
  exit 1
}

$lock = $null
try {
  $W = Resolve-GskWorkspace -Workspace $Workspace -ScriptDir $PSScriptRoot
  $now = Get-GskNow
  $run = Read-GskJson (Get-GskHarnessPath $W @('run.json'))
  if (-not $Name) { $Name = [string](Get-GskField $run 'name' '') }
  if (-not $Name) { Stop-Run 'missing -Name (and run.json has no name)' $null }
  if (-not (Test-GskRunName $Name)) { Stop-Run "invalid -Name '$Name': use letters, digits, dot, dash or underscore (max 64)" $null }
  if ($SessionId -and -not (Test-GskSessionId $SessionId)) { Stop-Run "invalid -SessionId '$SessionId': a Claude Code session id is a UUID" $null }

  $mode = 'new'
  if ($SessionId) { $mode = 'resume' } elseif ($Resume) { $mode = 'continue' }
  if ($KickoffFile) {
    $kp = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($KickoffFile)
    $prompt = Read-GskText $kp
    if ($null -eq $prompt) { Stop-Run "kickoff file not found: $kp" $null }
    $prompt = ($prompt -replace "`r`n", "`n").Trim()
    if ($prompt.Length -eq 0) { Stop-Run "kickoff file is empty: $kp" $null }
    foreach ($line in ($prompt -split "`n")) {
      if ($line.Trim() -match '^OWNER(-OVERRIDE)?:') { Stop-Run 'kickoff text holds an OWNER: command line; type owner commands into the session yourself' $null }
    }
    # claude would read a leading dash (a Markdown list) as an option.
    if ($prompt.StartsWith('-')) { $prompt = "Kickoff:`n$prompt" }
  } elseif ($mode -eq 'new') {
    $prompt = "Invoke the game-studio-run skill and start the run '$Name': read .harness/brief.md (or run node Tools/kit/harness/next_action.mjs), follow Docs/kit/HARNESS_RULES.md, and keep working until the run window ends or the owner stops you."
  } else {
    $prompt = "Invoke the game-studio-run skill and resume the run '$Name': read .harness/brief.md and .harness/alerts/, append a DEVLOG line about the restart, then continue from node Tools/kit/harness/next_action.mjs."
  }

  $claudeArgs = @('-n', $Name)
  if ($mode -eq 'continue') { $claudeArgs += '--continue' }
  if ($mode -eq 'resume') { $claudeArgs += "--resume=$SessionId" }
  $claudeArgs += @('--permission-mode', 'auto', $prompt)
  $shapeError = Test-GskLaunchArgs -Argv $claudeArgs -Mode $mode
  if ($shapeError) { Stop-Run $shapeError $null }
  $launchEnv = Get-GskLaunchEnv -MaxAgents $MaxAgents

  $warnings = New-Object 'System.Collections.Generic.List[string]'
  foreach ($n in (Get-GskSeamLeaks)) { $warnings.Add("test-seam variable $n is set and ignored; remove it from the user environment") }
  # The new tab resolves claude again; the length check uses the route this shell sees.
  $command = Resolve-GskClaudeCommand
  $route = $null
  if ($null -eq $command) {
    $warnings.Add('claude was not found on PATH in this shell; the new tab looks for it again')
    if ($prompt.Length -gt $script:GskMaxCmdChars['exe'] - 500) { Stop-Run 'kickoff text is too long for a command line; point the prompt at a file in the project instead' $null }
  } else {
    $cl = Get-GskClaudeCommandLine -Command $command -Argv $claudeArgs
    $route = $cl.route
    if ($cl.chars -gt $cl.max) {
      Stop-Run "the claude command line would be $($cl.chars) characters (limit $($cl.max) on the $($cl.route) route): point the prompt at a file in the project instead" $null
    }
  }
  $armed = ($null -ne $run -and [bool](Get-GskField $run 'active' $false))
  if (-not $armed) { $warnings.Add('run.json is not active: keep_working and the supervisor stay idle (kit run start --hours N arms them)') }
  # Human step H3: auto-continue waits for Enter after more than 30 min of sleep, so an overnight run needs a PC that
  # never sleeps on AC power. Read-only check; the owner changes the power plan.
  $power = Get-GskPowerTimeouts
  if ($null -ne $power) {
    if ($power.standby_ac_min -gt 0) { $warnings.Add("the PC sleeps after $($power.standby_ac_min) min on AC power (H3): powercfg /change standby-timeout-ac 0") }
    if ($power.hibernate_ac_min -gt 0) { $warnings.Add("the PC hibernates after $($power.hibernate_ac_min) min on AC power (H3): powercfg /change hibernate-timeout-ac 0") }
  }
  $session = Read-GskJson (Get-GskHarnessPath $W @('session.json'))
  $entry = [string](Get-GskField $session 'entrypoint' '')
  if ($entry -and $entry -ne 'cli') {
    $shown = $entry -creplace '[^A-Za-z0-9._-]', ''
    $warnings.Add("the last session of this workspace ran in $shown, not the CLI: close it there first, or two agents will work in this folder")
  }

  if ((Test-GskInsideClaude) -and -not $DryRun) {
    Stop-Run 'run.ps1 is for the owner: refusing to start Claude Code from inside a Claude Code session (CLAUDECODE is set)' $null
  }
  if (-not $DryRun) {
    $lock = Enter-GskLaunchLock -Workspace $W
    if ($null -eq $lock) { Stop-Run 'another launch of this workspace is in progress (supervisor or run.ps1); try again in a minute' $null }
  }
  $sessions = Get-GskWorkspaceSessions -Workspace $W -Names @($Name)
  $ours = (Get-GskPidList $sessions.ours).ToArray()
  $unknown = (Get-GskPidList $sessions.unknown).ToArray()
  if ($unknown.Length -gt 0) { $warnings.Add("Claude Code process(es) $($unknown -join ',') could not be tied to a project; make sure none of them works in this folder") }
  if ($ours.Length -gt 0 -and -not $Force) {
    Stop-Run "a Claude Code session of this workspace is already running (PID $($ours -join ',')); use it, or pass -Force" ([ordered]@{ running = $ours })
  }
  $pending = Test-GskLaunchPending -Workspace $W -Now $now -Minutes $script:RelaunchGuardMin
  if ($null -ne $pending -and -not $Force) {
    Stop-Run "a launch of this workspace started at $(Format-GskUtc $pending.utc) and has not started Claude Code yet; wait for its tab, or pass -Force" ([ordered]@{ pending_plan = [IO.Path]::GetFileName($pending.path) })
  }
  foreach ($msg in $warnings) { Write-GskNote "run.ps1: $msg" }

  if (-not $DryRun) {
    foreach ($k in $launchEnv.Keys) { [Environment]::SetEnvironmentVariable($k, $launchEnv[$k], 'Process') }
  }
  $launch = Start-GskClaudeTab -Workspace $W -Mode $mode -ClaudeArgs $claudeArgs -Title (Get-GskTabTitle $Name) -LaunchEnv $launchEnv -Now $now -AllowParallel:$Force -DryRun:$DryRun
  Exit-GskLaunchLock $lock
  $lock = $null
  Write-GskResult ([ordered]@{
      ok = $true; exit = 0; cmd = 'run'; dry_run = [bool]$DryRun; workspace = $W; name = $Name; mode = $mode
      env = $launchEnv; args = $claudeArgs; prompt_chars = $prompt.Length; claude_route = $route; armed = $armed; power = $power
      warnings = $warnings.ToArray(); launch = $launch
    })
  exit 0
} catch {
  Exit-GskLaunchLock $lock
  Write-GskResult ([ordered]@{ ok = $false; exit = 1; cmd = 'run'; error = $_.Exception.Message })
  exit 1
}

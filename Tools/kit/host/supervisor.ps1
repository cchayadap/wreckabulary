<#
.SYNOPSIS
  Game Studio Kit supervisor: one pass per call (Task Scheduler runs it every 5 minutes).
.DESCRIPTION
  Acts only while .harness/run.json is active and inside its window. Each pass decides one action:
    skip     - kit uninstalled, owner stop, pause, STALLED, wrap-up done, an unidentified Claude Code process, a session
               hosted outside the CLI (desktop app, SDK), an invalid session id or run name, a launch still starting,
               a cap or cooldown;
    sleep    - a usage limit resets later (WAIT_UNTIL, stopfailure.jsonl resets_utc, quota.json): wait until reset + 2 min;
    alert    - the session's process is alive but its heartbeat, transcript and subagent journals are older than
               -StaleMinutes (and it is not waiting on background work), or a permission prompt (quota.jsonl) is not
               older than the last tool call: raises .harness/alerts/waiting_for_owner and a toast (auto mode falls back
               to prompting after 3 consecutive or 20 total classifier blocks);
    relaunch - no Claude Code process belongs to W and all progress signals are stale: opens
               `claude --resume=<session_id> --permission-mode auto "GSK-SUPERVISOR: ..."` in a Windows Terminal tab
               (or `claude -n <name> --continue ...` when the session's transcript is gone or there is no session.json);
    noop     - the session is alive and making progress, or waiting on background work.
  It appends supervisor.jsonl lines (#/$defs/supervisorLine) when the decision changes, and at most hourly otherwise.
  It never types into a live session, never relaunches while a process of W is alive, never kills anything, and
  never acts after the window ends (ARCHITECTURE section 4.5, Critique C-12). Check-and-launch runs under a per-workspace
  mutex shared with run.ps1.
.PARAMETER Workspace
  The workspace root W. Default: three folders above this script when W/Tools/kit/kit.json exists there.
.PARAMETER DryRun
  Decide and print the result (with the command a relaunch would run); write, toast and launch nothing.
.PARAMETER StaleMinutes
  Progress age after which a live session counts as waiting (default 15).
.PARAMETER TestSeams
  Tests only: honour the GSK_* test-seam variables.
#>
[CmdletBinding()]
param(
  [string]$Workspace,
  [switch]$DryRun,
  [ValidateRange(1, 1440)][int]$StaleMinutes = 15,
  [switch]$NoToast,
  [switch]$TestSeams
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. ([IO.Path]::Combine($PSScriptRoot, 'lib', 'host.ps1'))
if ($TestSeams) { Enable-GskTestSeams }

$script:OwnedAlerts = @('waiting_for_owner')
$script:CooldownMin = 15
$script:MaxRelaunches = 3
$script:RepeatLogMin = 60
$script:WaitGraceMin = 2
$script:BgWaitMaxMin = 120
$script:OwnerErrors = @('authentication_failed', 'billing_error', 'account_on_hold', 'oauth_org_not_allowed', 'cloud_credential_error')
$script:ToastCodes = @('stale_alive', 'permission_prompt', 'unidentified', 'needs_owner', 'disk_emergency', 'relaunch_cap', 'stalled', 'relaunch', 'foreign_host', 'bad_session_ref')
$script:SilentCodes = @('run_inactive', 'kit_missing')

function New-Decision {
  param([string]$Action, [string]$Code, [string]$Reason)
  return [ordered]@{ action = $Action; code = $Code; reason = $Reason }
}

function New-Context {
  $facts = [ordered]@{
    heartbeat_age_min = $null; transcript_age_min = $null; subagent_age_min = $null; bg_wait_age_min = $null
    ours = @(); other = @(); unknown = @(); wait_until = $null; permission_prompt_pending = $false
    relaunches_since_progress = 0; entrypoint = $null; transcript_found = $false; pending_plan = $null
  }
  return [ordered]@{ decision = $null; facts = $facts; session_id = $null; name = $null; pid = $null; resume = $false }
}

function Test-HarnessFile {
  param([string]$W, [string]$Name)
  return [IO.File]::Exists((Get-GskHarnessPath $W @($Name)))
}

function Read-UtcLine {
  param([string]$Path)
  $t = Read-GskText $Path
  if ($null -eq $t) { return $null }
  foreach ($line in ($t -split "`n")) { $d = ConvertFrom-GskUtc $line.Trim(); if ($null -ne $d) { return $d } }
  return $null
}

function Test-WrapupClosed {
  param([string]$W)
  $g = Read-GskJson ([IO.Path]::Combine($W, 'Docs', 'qa', 'gates.json'))
  foreach ($m in @(Get-GskField $g 'milestones' @())) {
    if ($null -ne $m -and [string](Get-GskField $m 'id' '') -eq 'wrapup' -and [string](Get-GskField $m 'status' '') -eq 'closed') { return $true }
  }
  return $false
}

function ConvertFrom-ResetValue {
  param([AllowNull()]$Value)
  if ($null -eq $Value) { return $null }
  if ($Value -is [string]) { return (ConvertFrom-GskUtc $Value) }
  try { return [DateTime]::SpecifyKind((New-Object DateTime(1970, 1, 1, 0, 0, 0)), [DateTimeKind]::Utc).AddSeconds([double]$Value) } catch { return $null }
}

function Get-AgeMinutes {
  param([AllowNull()]$Time, [DateTime]$Now)
  if ($null -eq $Time) { return $null }
  $age = [Math]::Round(($Now - [DateTime]$Time).TotalMinutes, 1)
  if ($age -lt -5) { return $null }
  return $age
}

function Get-SafeText {
  param([AllowNull()][AllowEmptyString()][string]$Text)
  $t = ([string]$Text) -creplace '[^A-Za-z0-9._-]', ''
  if ($t.Length -gt 40) { $t = $t.Substring(0, 40) }
  return $t
}

# The latest usage-limit reset whose grace (reset + 2 min) is still ahead: WAIT_UNTIL, the last stopfailure line's
# resets_utc, or a quota.json window at 100%.
function Get-WaitUntil {
  param([string]$W, [DateTime]$Now)
  $best = $null
  $cands = New-Object 'System.Collections.Generic.List[object]'
  $wu = Read-UtcLine (Get-GskHarnessPath $W @('WAIT_UNTIL'))
  if ($null -ne $wu) { $cands.Add($wu) }
  $sf = Read-GskJsonlTail -Path (Get-GskHarnessPath $W @('stopfailure.jsonl')) -Max 1
  if ($sf.Count -gt 0) {
    $r = ConvertFrom-GskUtc ([string](Get-GskField $sf[$sf.Count - 1] 'resets_utc' ''))
    if ($null -ne $r) { $cands.Add($r) }
  }
  $q = Read-GskJson (Get-GskHarnessPath $W @('quota.json'))
  foreach ($k in @('five_hour', 'seven_day')) {
    $win = Get-GskField $q $k $null
    if ($null -eq $win) { continue }
    $used = Get-GskField $win 'used_percentage' $null
    if ($null -ne $used -and [double]$used -ge 100) {
      $r = ConvertFrom-ResetValue (Get-GskField $win 'resets_at' $null)
      if ($null -ne $r) { $cands.Add($r) }
    }
  }
  foreach ($c in $cands) { if ($c.AddMinutes($script:WaitGraceMin) -gt $Now -and ($null -eq $best -or $c -gt $best)) { $best = $c } }
  return $best
}

function Get-Decision {
  param([string]$W, [DateTime]$Now, [int]$Stale)
  $ctx = New-Context
  $facts = $ctx.facts
  if (-not (Test-GskKitInstalled $W)) {
    $ctx.decision = New-Decision 'skip' 'kit_missing' "$W has no Tools\kit\kit.json (the kit was uninstalled): nothing to supervise"
    return $ctx
  }
  $run = Read-GskJson (Get-GskHarnessPath $W @('run.json'))
  if ($null -eq $run -or -not [bool](Get-GskField $run 'active' $false)) {
    $ctx.decision = New-Decision 'skip' 'run_inactive' 'run.json is not active (kit run start arms the supervisor)'
    return $ctx
  }
  $end = ConvertFrom-GskUtc ([string](Get-GskField $run 'window_end_utc' ''))
  if ($null -eq $end) { $ctx.decision = New-Decision 'skip' 'no_window' 'run.json has no window_end_utc'; return $ctx }
  if ($Now -ge $end) { $ctx.decision = New-Decision 'skip' 'window_ended' "the run window ended at $(Format-GskUtc $end)"; return $ctx }
  if (Test-HarnessFile $W 'ALLOW_STOP_OWNER') { $ctx.decision = New-Decision 'skip' 'owner_stop' 'the owner stopped the run (ALLOW_STOP_OWNER)'; return $ctx }
  if (Test-HarnessFile $W 'PAUSE') { $ctx.decision = New-Decision 'skip' 'paused' 'the owner paused the run (PAUSE)'; return $ctx }
  if (Test-HarnessFile $W 'STALLED') { $ctx.decision = New-Decision 'skip' 'stalled' 'keep_working saw no progress (STALLED): owner attention needed'; return $ctx }
  if ((Test-HarnessFile $W 'ALLOW_STOP') -and (Test-WrapupClosed $W)) { $ctx.decision = New-Decision 'skip' 'wrapup_done' 'the wrap-up gate is closed and the agent asked to stop'; return $ctx }

  $wait = Get-WaitUntil -W $W -Now $Now
  if ($null -ne $wait) {
    $facts.wait_until = Format-GskUtc $wait
    $ctx.decision = New-Decision 'sleep' 'usage_wait' "usage limit resets at $(Format-GskUtc $wait); acting again after $(Format-GskUtc $wait.AddMinutes($script:WaitGraceMin))"
    return $ctx
  }

  # session.json and run.json are agent-writable: only a UUID session id and run-name-shaped names are used.
  $session = Read-GskJson (Get-GskHarnessPath $W @('session.json'))
  $rawSid = [string](Get-GskField $session 'session_id' '')
  $sidOk = (-not $rawSid) -or (Test-GskSessionId $rawSid)
  $sid = $(if ($sidOk) { $rawSid } else { '' })
  $names = @()
  $badNames = 0
  foreach ($n in @([string](Get-GskField $run 'name' ''), [string](Get-GskField $session 'name' ''))) {
    if (-not $n) { continue }
    if (Test-GskRunName $n) { $names += $n } else { $badNames++ }
  }
  $entry = [string](Get-GskField $session 'entrypoint' '')
  $facts.entrypoint = $(if ($entry) { Get-SafeText $entry } else { $null })
  $tpath = ([string](Get-GskField $session 'transcript_path' '')).Replace('/', '\')
  if ((-not $tpath -or -not [IO.File]::Exists($tpath)) -and $sid) {
    $found = Find-GskTranscript -Id $sid -HomeDir (Get-GskHome)
    if ($found) { $tpath = $found }
  }
  $haveTranscript = ([bool]$tpath -and [IO.File]::Exists($tpath))
  $facts.transcript_found = $haveTranscript
  $ctx.session_id = $(if ($sid) { $sid } else { $null })
  $ctx.name = $(if ($names.Length -gt 0) { $names[0] } else { $null })

  $sessions = Get-GskClaudeSessions -Workspace $W -SessionId $sid -Names $names -TranscriptPath $tpath -ClaudePid (Read-GskJson (Get-GskHarnessPath $W @('claude.pid')))
  $facts.ours = (Get-GskPidList $sessions.ours).ToArray()
  $facts.other = (Get-GskPidList $sessions.other).ToArray()
  $facts.unknown = (Get-GskPidList $sessions.unknown).ToArray()
  if ($facts.ours.Length -gt 0) { $ctx.pid = $facts.ours[0] }

  # Progress signals: the guard's heartbeat (PreToolUse), the main transcript, and the journals of Workflow agents
  # (<transcript folder>/<session id>/subagents/**), which move while the main session idles during a wave.
  $hbPath = Get-GskHarnessPath $W @('heartbeat')
  $hbAge = Get-GskFileAgeMinutes -Path $hbPath -Now $Now
  $trAge = $null
  if ($haveTranscript) { $trAge = Get-GskFileAgeMinutes -Path $tpath -Now $Now }
  $subAge = $null
  if ($haveTranscript -and $sid) {
    $subDir = [IO.Path]::Combine([IO.Path]::GetDirectoryName($tpath), $sid, 'subagents')
    $subAge = Get-AgeMinutes -Time (Get-GskNewestWrite -Dir $subDir) -Now $Now
  }
  $facts.heartbeat_age_min = $hbAge
  $facts.transcript_age_min = $trAge
  $facts.subagent_age_min = $subAge
  $fresh = $false
  foreach ($a in @($hbAge, $trAge, $subAge)) { if ($null -ne $a -and $a -lt $Stale) { $fresh = $true } }
  # Second precision: quota.jsonl and bg_wait.json stamps are whole seconds, the heartbeat mtime is not.
  $hbSec = $null
  if ($null -ne $hbAge) {
    $hbTime = [IO.File]::GetLastWriteTimeUtc($hbPath)
    $hbSec = $hbTime.AddTicks(-($hbTime.Ticks % [TimeSpan]::TicksPerSecond))
  }
  $runStart = ConvertFrom-GskUtc ([string](Get-GskField $run 'started_utc' ''))

  # A permission prompt logged in this run no earlier than the last tool call means the session waits for the owner.
  foreach ($q in (Read-GskJsonlTail -Path (Get-GskHarnessPath $W @('quota.jsonl')) -Max 200)) {
    if ([string](Get-GskField $q 'notification_type' '') -ne 'permission_prompt') { continue }
    $qt = ConvertFrom-GskUtc ([string](Get-GskField $q 'utc' ''))
    if ($null -eq $qt -or ($null -ne $runStart -and $qt -lt $runStart)) { continue }
    if ($null -eq $hbSec -or $qt -ge $hbSec) { $facts.permission_prompt_pending = $true }
  }

  # keep_working lets the session stop while background tasks or Workflow agents run (.harness/bg_wait.json): the
  # session is waiting on that work, not on the owner, for up to BgWaitMaxMin.
  $bgWaiting = $false
  $bg = Read-GskJson (Get-GskHarnessPath $W @('bg_wait.json'))
  $bu = ConvertFrom-GskUtc ([string](Get-GskField $bg 'utc' ''))
  if ($null -ne $bu) {
    $facts.bg_wait_age_min = Get-AgeMinutes -Time $bu -Now $Now
    $bgSid = [string](Get-GskField $bg 'session_id' '')
    $sameSession = (-not $bgSid -or -not $sid -or [string]::Equals($bgSid, $sid, [StringComparison]::OrdinalIgnoreCase))
    $bgWaiting = ($null -ne $facts.bg_wait_age_min -and $facts.bg_wait_age_min -lt $script:BgWaitMaxMin -and $sameSession -and ($null -eq $hbSec -or $bu -ge $hbSec))
  }
  $ageText = "heartbeat $(if ($null -ne $hbAge) { "$hbAge min" } else { 'missing' }), transcript $(if ($null -ne $trAge) { "$trAge min" } else { 'missing' })"
  if ($null -ne $subAge) { $ageText += ", subagents $subAge min" }

  if ($facts.ours.Length -gt 0 -or $facts.unknown.Length -gt 0) {
    $who = "Claude Code PID $((@($facts.ours) + @($facts.unknown)) -join ',')"
    if ($facts.permission_prompt_pending) {
      $ctx.decision = New-Decision 'alert' 'permission_prompt' "$who is waiting on a permission prompt no older than the last tool call ($ageText)"
      return $ctx
    }
    if ($facts.ours.Length -gt 0) {
      if ($fresh) {
        $ctx.decision = New-Decision 'noop' 'alive' "$who is alive ($ageText)"
      } elseif ($bgWaiting) {
        $ctx.decision = New-Decision 'noop' 'background_wait' "$who is waiting on background work (bg_wait.json $($facts.bg_wait_age_min) min, cap $($script:BgWaitMaxMin) min; $ageText)"
      } else {
        $ctx.decision = New-Decision 'alert' 'stale_alive' "$who is alive but idle for more than $Stale min ($ageText): it may be waiting for the owner"
      }
      return $ctx
    }
    if ($fresh -or $bgWaiting) { $ctx.decision = New-Decision 'noop' 'fresh_unidentified' "a session is active in W ($ageText); $who not tied to the run"; return $ctx }
    $ctx.decision = New-Decision 'skip' 'unidentified' "$who cannot be tied to W or to another project, and the session is idle ($ageText): relaunch refused because W's session may still be open"
    return $ctx
  }

  if ($fresh) { $ctx.decision = New-Decision 'noop' 'recent_progress' "no Claude Code process for W, but $ageText is under $Stale min; relaunch waits until it is older"; return $ctx }
  if ($entry -and $entry -ne 'cli') {
    $shown = Get-SafeText $entry
    $ctx.decision = New-Decision 'skip' 'foreign_host' "the run's session is hosted by $shown (session.json entrypoint), not the CLI: reopen it there; a CLI relaunch would open a second agent on the same session"
    return $ctx
  }
  if (-not $sidOk -or $badNames -gt 0) {
    $ctx.decision = New-Decision 'skip' 'bad_session_ref' 'session.json session_id is not a session UUID or a run name has characters outside [A-Za-z0-9._-]: relaunch refused; the owner restarts with run.ps1'
    return $ctx
  }
  $sf = Read-GskJsonlTail -Path (Get-GskHarnessPath $W @('stopfailure.jsonl')) -Max 1
  if ($sf.Count -gt 0) {
    $last = $sf[$sf.Count - 1]
    $err = [string](Get-GskField $last 'error' '')
    $lt = ConvertFrom-GskUtc ([string](Get-GskField $last 'utc' ''))
    $inRun = ($null -ne $lt -and ($null -eq $runStart -or $lt -ge $runStart))
    if ($script:OwnerErrors -contains $err -and $inRun -and ($null -eq $hbSec -or $lt -ge $hbSec)) {
      $ctx.decision = New-Decision 'skip' 'needs_owner' "the last turn failed with $err; a relaunch would fail the same way until the owner fixes it"
      return $ctx
    }
  }
  if ([IO.File]::Exists((Get-GskHarnessPath $W @('alerts', 'disk_emergency')))) {
    $ctx.decision = New-Decision 'skip' 'disk_emergency' 'disk_emergency is raised: free disk space before the session is relaunched'
    return $ctx
  }
  $since = $runStart
  if ($null -ne $hbSec -and ($null -eq $since -or $hbSec -gt $since)) { $since = $hbSec }
  $lastRelaunch = $null
  $count = 0
  foreach ($l in (Read-GskJsonlTail -Path (Get-GskHarnessPath $W @('supervisor.jsonl')) -Max 500)) {
    if ([string](Get-GskField $l 'action' '') -ne 'relaunch') { continue }
    $lt = ConvertFrom-GskUtc ([string](Get-GskField $l 'utc' ''))
    if ($null -eq $lt) { continue }
    if ($null -eq $lastRelaunch -or $lt -gt $lastRelaunch) { $lastRelaunch = $lt }
    if ($null -eq $since -or $lt -gt $since) { $count++ }
  }
  $facts.relaunches_since_progress = $count
  if ($null -ne $lastRelaunch -and ($Now - $lastRelaunch).TotalMinutes -lt $script:CooldownMin) {
    $ctx.decision = New-Decision 'skip' 'relaunch_cooldown' "relaunched at $(Format-GskUtc $lastRelaunch); waiting $($script:CooldownMin) min for its heartbeat"
    return $ctx
  }
  # A launch plan (run.ps1 or a supervisor pass) younger than the cooldown with no claude.pid after it: its tab may
  # still be starting.
  $pending = Test-GskLaunchPending -Workspace $W -Now $Now -Minutes $script:CooldownMin
  if ($null -ne $pending) {
    $facts.pending_plan = [IO.Path]::GetFileName($pending.path)
    $ctx.decision = New-Decision 'skip' 'launch_pending' "launch plan $($facts.pending_plan) from $(Format-GskUtc $pending.utc) has not started Claude Code yet; waiting $($script:CooldownMin) min"
    return $ctx
  }
  if ($count -ge $script:MaxRelaunches) {
    $ctx.decision = New-Decision 'skip' 'relaunch_cap' "$count relaunches without progress since $(if ($null -ne $since) { Format-GskUtc $since } else { 'the run start' }): owner attention needed"
    return $ctx
  }
  # --resume needs the session's transcript; without it Claude Code exits with "No conversation found".
  $ctx.resume = ([bool]$sid -and $haveTranscript)
  if ($ctx.resume) { $target = "resuming session $sid" }
  elseif ($sid) { $target = "session $sid has no transcript, so continuing the latest session in W" }
  else { $target = 'continuing the latest session in W (no session.json)' }
  $ctx.decision = New-Decision 'relaunch' 'relaunch' "no Claude Code process for W and $ageText is over $Stale min; $target"
  return $ctx
}

function Get-RelaunchArgs {
  param([System.Collections.Specialized.OrderedDictionary]$Ctx, [DateTime]$Now, [string]$W)
  $prompt = "GSK-SUPERVISOR: the host supervisor restarted this session at $(Format-GskUtc $Now) ($($Ctx.decision.reason)). " +
    'Invoke the game-studio-run skill, read .harness/brief.md and .harness/alerts/, check the Unity lock and kill orphans by PID only, ' +
    'append a DEVLOG line about the interruption, then continue from node Tools/kit/harness/next_action.mjs.'
  if ($Ctx.resume) {
    $ra = [ordered]@{ mode = 'resume'; args = @("--resume=$($Ctx.session_id)", '--permission-mode', 'auto', $prompt) }
  } else {
    $name = $Ctx.name
    if (-not $name) { $name = 'gsk-' + (ConvertTo-GskTaskSlug ([IO.Path]::GetFileName($W.TrimEnd('\')))) }
    $ra = [ordered]@{ mode = 'continue'; args = @('-n', $name, '--continue', '--permission-mode', 'auto', $prompt) }
  }
  $bad = Test-GskLaunchArgs -Argv $ra.args -Mode $ra.mode
  if ($bad) { throw "relaunch refused: $bad" }
  return $ra
}

try {
  $W = Resolve-GskWorkspace -Workspace $Workspace -ScriptDir $PSScriptRoot
  $now = Get-GskNow
  $dry = [bool]$DryRun
  $lock = $null
  $result = $null
  $ctx = $null
  if (-not $dry) { $lock = Enter-GskLaunchLock -Workspace $W }
  try {
    if (-not $dry -and $null -eq $lock) {
      $ctx = New-Context
      $ctx.decision = New-Decision 'skip' 'launch_pending' 'another launch of this workspace holds the launch lock'
    } else {
      $ctx = Get-Decision -W $W -Now $now -Stale $StaleMinutes
    }
    $d = $ctx.decision
    if ($d.action -eq 'relaunch' -and (Test-GskInsideClaude) -and -not $dry) {
      $d = New-Decision 'skip' 'inside_claude_code' 'refusing to start Claude Code from inside a Claude Code session (CLAUDECODE is set); Task Scheduler runs the supervisor'
      $ctx.decision = $d
    }
    $result = [ordered]@{
      ok = $true; exit = 0; cmd = 'supervisor'; dry_run = $dry; workspace = $W; utc = (Format-GskUtc $now)
      action = $d.action; code = $d.code; reason = $d.reason; pid = $ctx.pid; session_id = $ctx.session_id
      facts = $ctx.facts; alert = $null; launch = $null; logged = $false; toasted = $false
    }
    $leaks = Get-GskSeamLeaks
    if ($leaks.Count -gt 0) {
      $result['seam_env'] = $leaks.ToArray()
      Write-GskNote "supervisor: test-seam variables are set and ignored ($($leaks -join ', ')); remove them from the user environment"
    }
    if ($d.action -eq 'relaunch') {
      $ra = Get-RelaunchArgs -Ctx $ctx -Now $now -W $W
      $title = Get-GskTabTitle $(if ($ctx.name) { $ctx.name } else { 'resume' })
      $result.launch = Start-GskClaudeTab -Workspace $W -Mode $ra.mode -ClaudeArgs $ra.args -Title $title -LaunchEnv (Get-GskLaunchEnv) -Now $now -DryRun:$dry
    }
  } finally {
    Exit-GskLaunchLock $lock
  }
  $harnessExists = [IO.Directory]::Exists((Get-GskHarnessPath $W @()))
  $newAlert = $false
  if (-not $dry -and $harnessExists -and $script:SilentCodes -notcontains $d.code) {
    if ($d.action -eq 'alert') {
      $r = Set-GskAlert -Workspace $W -Name 'waiting_for_owner' -Level 'warn' -Message $d.reason -Value $ctx.facts.heartbeat_age_min -Now $now -By 'supervisor'
      $result.alert = 'waiting_for_owner'
      $newAlert = [bool]$r.created
    } elseif ($d.action -eq 'noop' -or $d.action -eq 'relaunch') {
      if ([IO.File]::Exists((Get-GskHarnessPath $W @('alerts', 'waiting_for_owner')))) {
        [void](Remove-GskOwnAlert -Workspace $W -Name 'waiting_for_owner' -Owned $script:OwnedAlerts)
      }
    }
    # One line per change of decision, at most hourly while it repeats.
    $logPath = Get-GskHarnessPath $W @('supervisor.jsonl')
    $tail = Read-GskJsonlTail -Path $logPath -Max 1
    $append = $true
    if ($tail.Count -gt 0 -and $d.action -ne 'relaunch') {
      $last = $tail[$tail.Count - 1]
      $lt = ConvertFrom-GskUtc ([string](Get-GskField $last 'utc' ''))
      if ([string](Get-GskField $last 'code' '') -eq $d.code -and $null -ne $lt -and ($now - $lt).TotalMinutes -lt $script:RepeatLogMin) { $append = $false }
    }
    if ($append) {
      $line = [ordered]@{ utc = (Format-GskUtc $now); action = $d.action; reason = $d.reason; pid = $ctx.pid; session_id = $ctx.session_id; code = $d.code }
      if ($null -ne $result.launch) { $line['route'] = $result.launch.route; $line['plan'] = $result.launch.plan }
      Add-GskJsonl -Path $logPath -InputObject $line
      $result.logged = $true
      if (-not $NoToast -and ($newAlert -or ($script:ToastCodes -contains $d.code))) {
        $result.toasted = [bool](Send-GskToast -Title 'Game Studio Kit supervisor' -Text "$($d.code): $($d.reason)" -Now $now)
      }
    }
  }
  Write-GskResult $result
  exit 0
} catch {
  Write-GskResult ([ordered]@{ ok = $false; exit = 1; cmd = 'supervisor'; error = $_.Exception.Message })
  exit 1
}

<#
.SYNOPSIS
  Game Studio Kit hygiene watchdog: one measurement pass per call (Task Scheduler runs it every minute).
.DESCRIPTION
  Measures free disk on the workspace volume, commit charge, available RAM, the resident Unity editor's private bytes,
  working set and uptime, and the pagefile size. Writes .harness/health.json, appends .harness/metrics.jsonl, raises
  or clears its own alert flags in .harness/alerts/ and toasts new or escalated alerts.
  It measures and alerts only: it never deletes project files, never kills a process and never starts one
  (ARCHITECTURE section 4.5, principle P13). The only files it removes are its own alert flags once a condition has cleared.
  Unity is read from .harness/unity.pid only when that PID's start time (+-2 s) and image still match the file;
  otherwise Unity counts as not running (CONTRACTS section 1.6, section 4.3).
  It does nothing once W\Tools\kit\kit.json is gone (the kit was uninstalled), and raises test_seam_in_env when a
  GSK_* test-seam variable reached it (the seams themselves are ignored without -TestSeams).
.PARAMETER Workspace
  The workspace root W. Default: three folders above this script when W/Tools/kit/kit.json exists there.
.PARAMETER Once
  One pass (the default; kept for the documented command line).
.PARAMETER Loop
  Keep running, one pass every -IntervalSec seconds (an alternative to the scheduled task).
.PARAMETER DryRun
  Measure and print the result; write nothing and toast nothing.
.PARAMETER TestSeams
  Tests only: honour the GSK_* test-seam variables (fake machine facts, pinned clock, fake home, no toasts).
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools\kit\host\hygiene_watchdog.ps1 -Workspace . -DryRun
#>
[CmdletBinding()]
param(
  [string]$Workspace,
  [switch]$Once,
  [switch]$Loop,
  [ValidateRange(10, 3600)][int]$IntervalSec = 60,
  [switch]$DryRun,
  [switch]$NoToast,
  [switch]$TestSeams
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. ([IO.Path]::Combine($PSScriptRoot, 'lib', 'host.ps1'))
if ($TestSeams) { Enable-GskTestSeams }

# ARCHITECTURE section 4.5 thresholds ([P] numbers); the margins keep an alert from flapping at its threshold.
$script:T = @{
  disk_warn_gb = 50.0; disk_stop_gb = 25.0; disk_emergency_gb = 10.0; disk_margin_gb = 1.0
  commit_warn_pct = 80.0; commit_stop_pct = 90.0; commit_margin_pct = 2.0
  ram_warn_mb = 4096.0; ram_stop_mb = 2048.0; ram_warn_samples = 2; ram_margin_mb = 256.0
  unity_warn_mb = 10240.0; unity_stop_mb = 13312.0; unity_max_uptime_h = 18.0; unity_margin_mb = 512.0
  pagefile_growth_mb = 8192.0; pagefile_margin_mb = 512.0
}
$script:MetricAlerts = @('disk_warn', 'disk_stop', 'disk_emergency', 'commit_high', 'ram_low', 'unity_restart_due', 'pagefile_growth')
# test_seam_in_env: a GSK_* test-seam variable reached this production process (for example through setx). The
# seams are ignored without -TestSeams; the flag tells the owner to remove them. It is not a health.alerts name.
$script:OwnedAlerts = $script:MetricAlerts + @('test_seam_in_env')
$script:AlertNames = $script:MetricAlerts + @('secret_in_prompt', 'waiting_for_owner')

function Test-Held {
  param([string]$W, [string]$Name)
  return [IO.File]::Exists((Get-GskHarnessPath $W @('alerts', $Name)))
}

function Get-UnityFacts {
  param([string]$W, [DateTime]$Now)
  $u = [ordered]@{ pid = $null; private_mb = $null; uptime_h = $null; working_set_mb = $null; status = 'not_running' }
  $file = Get-GskHarnessPath $W @('unity.pid')
  if (-not [IO.File]::Exists($file)) { return $u }
  $doc = Read-GskJson $file
  $id = [int](Get-GskField $doc 'pid' 0)
  $start = ConvertFrom-GskUtc ([string](Get-GskField $doc 'process_start_utc' ''))
  $image = [string](Get-GskField $doc 'image' '')
  if ($id -le 0 -or $null -eq $start -or -not $image) { $u.status = 'unverifiable_pid_file'; return $u }
  $m = Test-GskPidMatch -Id $id -StartUtc $start -Image $image
  if ($m.status -ne 'match') { $u.status = "pid_$($m.status)"; return $u }
  $p = $m.process
  $u.pid = $id
  $u.status = 'running'
  if ($null -ne $p.private_bytes) { $u.private_mb = [Math]::Round($p.private_bytes / 1MB) }
  if ($null -ne $p.working_set) { $u.working_set_mb = [Math]::Round($p.working_set / 1MB) }
  $u.uptime_h = [Math]::Round(($Now - $p.start_utc).TotalHours, 2)
  return $u
}

function Invoke-WatchdogPass {
  param([string]$W, [bool]$Dry, [bool]$Quiet)
  $now = Get-GskNow
  $utc = Format-GskUtc $now
  $healthPath = Get-GskHarnessPath $W @('health.json')
  $prev = Read-GskJson $healthPath
  $run = Read-GskJson (Get-GskHarnessPath $W @('run.json'))

  $disk = Get-GskDiskInfo -Workspace $W
  $mem = Get-GskMemoryInfo
  $pf = Get-GskPagefileMb
  $unity = Get-UnityFacts -W $W -Now $now

  # RAM "< 4 GB twice": the streak of low samples carries over in health.json.
  $streak = 0
  if ($null -ne $mem -and $mem.available_mb -lt $script:T.ram_warn_mb) {
    $prevRam = Get-GskField $prev 'ram' $null
    $streak = [int](Get-GskField $prevRam 'low_streak' 0) + 1
  }

  # Pagefile growth is measured against a baseline taken at the first pass, reset when a new run starts.
  $pagefile = $null
  if ($null -ne $pf) {
    $prevPf = Get-GskField $prev 'pagefile' $null
    $baseMb = Get-GskField $prevPf 'baseline_mb' $null
    $baseUtc = [string](Get-GskField $prevPf 'baseline_utc' '')
    $runStart = ConvertFrom-GskUtc ([string](Get-GskField $run 'started_utc' ''))
    $baseDate = ConvertFrom-GskUtc $baseUtc
    if ($null -eq $baseMb -or $null -eq $baseDate -or ($null -ne $runStart -and $runStart -gt $baseDate)) { $baseMb = $pf; $baseUtc = $utc }
    $grew = [Math]::Max(0.0, $pf - [double]$baseMb)
    $pagefile = [ordered]@{ mb = $pf; grew_mb = $grew; baseline_mb = [double]$baseMb; baseline_utc = $baseUtc }
  }

  # Desired alerts: name -> level, message, value. $null metrics leave the existing flags untouched.
  $want = [ordered]@{}
  $measured = @{}
  if ($null -ne $disk) {
    $measured['disk'] = $true
    $f = $disk.free_gb
    $where = "free disk $f GB on $($disk.drive)"
    if ($f -lt $script:T.disk_warn_gb -or ((Test-Held $W 'disk_warn') -and $f -lt $script:T.disk_warn_gb + $script:T.disk_margin_gb)) {
      $want['disk_warn'] = @('warn', "$where < $($script:T.disk_warn_gb) GB: capture as JPEG/WebP, run kit prune --dry-run", $f)
    }
    if ($f -lt $script:T.disk_stop_gb -or ((Test-Held $W 'disk_stop') -and $f -lt $script:T.disk_stop_gb + $script:T.disk_margin_gb)) {
      $want['disk_stop'] = @('stop', "$where < $($script:T.disk_stop_gb) GB: stop recordings and heavy lanes, review the prune list", $f)
    }
    if ($f -lt $script:T.disk_emergency_gb -or ((Test-Held $W 'disk_emergency') -and $f -lt $script:T.disk_emergency_gb + $script:T.disk_margin_gb)) {
      $want['disk_emergency'] = @('emergency', "$where < $($script:T.disk_emergency_gb) GB: save the editor and stop heavy work now (pagefile growth can fill the rest)", $f)
    }
  }
  if ($null -ne $mem) {
    $measured['memory'] = $true
    $c = $mem.commit_used_pct
    if ($c -gt $script:T.commit_stop_pct) {
      $want['commit_high'] = @('stop', "commit charge $c% > $($script:T.commit_stop_pct)%: pause new lanes and Blender batch jobs", $c)
    } elseif ($c -gt $script:T.commit_warn_pct -or ((Test-Held $W 'commit_high') -and $c -gt $script:T.commit_warn_pct - $script:T.commit_margin_pct)) {
      $want['commit_high'] = @('warn', "commit charge $c% > $($script:T.commit_warn_pct)%", $c)
    }
    $a = $mem.available_mb
    if ($a -lt $script:T.ram_stop_mb) {
      $want['ram_low'] = @('stop', "available RAM $a MB < $($script:T.ram_stop_mb) MB: stop background Blender", $a)
    } elseif ($streak -ge $script:T.ram_warn_samples -or ((Test-Held $W 'ram_low') -and $a -lt $script:T.ram_warn_mb + $script:T.ram_margin_mb)) {
      $want['ram_low'] = @('warn', "available RAM $a MB < $($script:T.ram_warn_mb) MB ($streak samples)", $a)
    }
  }
  $measured['unity'] = $true
  if ($unity.status -eq 'running') {
    $pm = $unity.private_mb
    $up = $unity.uptime_h
    $big = ($null -ne $pm -and $pm -gt $script:T.unity_stop_mb)
    $old = ($null -ne $up -and $up -ge $script:T.unity_max_uptime_h)
    if ($big -or $old) {
      $want['unity_restart_due'] = @('stop', "Unity private $pm MB, uptime $up h: restart the editor at the next green gate (limits $($script:T.unity_stop_mb) MB or $($script:T.unity_max_uptime_h) h)", $pm)
    } elseif ($null -ne $pm -and ($pm -gt $script:T.unity_warn_mb -or ((Test-Held $W 'unity_restart_due') -and $pm -gt $script:T.unity_warn_mb - $script:T.unity_margin_mb))) {
      $want['unity_restart_due'] = @('warn', "Unity private $pm MB > $($script:T.unity_warn_mb) MB: plan an editor restart", $pm)
    }
  }
  if ($null -ne $pagefile) {
    $measured['pagefile'] = $true
    $g = $pagefile.grew_mb
    if ($g -gt $script:T.pagefile_growth_mb -or ((Test-Held $W 'pagefile_growth') -and $g -gt $script:T.pagefile_growth_mb - $script:T.pagefile_margin_mb)) {
      $want['pagefile_growth'] = @('warn', "pagefile grew $g MB since $($pagefile.baseline_utc): commit pressure; run fewer processes", $g)
    }
  }
  $leaks = Get-GskSeamLeaks
  $measured['env'] = $true
  if ($leaks.Count -gt 0) {
    $want['test_seam_in_env'] = @('warn', "test-seam variables are set in this scheduled process ($($leaks -join ', ')); the host tools ignore them. Remove them from the user environment (HKCU\Environment) and restart Windows Terminal.", ($leaks -join ','))
  }
  $source = @{ disk_warn = 'disk'; disk_stop = 'disk'; disk_emergency = 'disk'; commit_high = 'memory'; ram_low = 'memory'; unity_restart_due = 'unity'; pagefile_growth = 'pagefile'; test_seam_in_env = 'env' }

  $raised = New-Object 'System.Collections.Generic.List[string]'
  $cleared = New-Object 'System.Collections.Generic.List[string]'
  $news = New-Object 'System.Collections.Generic.List[string]'
  if (-not $Dry) {
    foreach ($name in $script:OwnedAlerts) {
      if ($want.Contains($name)) {
        $spec = $want[$name]
        $r = Set-GskAlert -Workspace $W -Name $name -Level $spec[0] -Message $spec[1] -Value $spec[2] -Now $now -By 'hygiene_watchdog'
        $raised.Add($name)
        if ($r.created -or $r.escalated) { $news.Add("${name}: $($spec[1])") }
      } elseif ($measured.ContainsKey($source[$name]) -and (Test-Held $W $name)) {
        if (Remove-GskOwnAlert -Workspace $W -Name $name -Owned $script:OwnedAlerts) { $cleared.Add($name) }
      }
    }
  } else {
    foreach ($name in $want.Keys) { $raised.Add($name) }
  }

  $active = New-Object 'System.Collections.Generic.List[string]'
  foreach ($name in $script:AlertNames) {
    if ($Dry) { if ($want.Contains($name) -or (Test-Held $W $name)) { $active.Add($name) } }
    elseif (Test-Held $W $name) { $active.Add($name) }
  }

  $diskDoc = [ordered]@{ drive = [IO.Path]::GetPathRoot($W).TrimEnd('\'); free_gb = -1.0; total_gb = -1.0; measured = $false }
  if ($null -ne $disk) { $diskDoc = [ordered]@{ drive = $disk.drive; free_gb = $disk.free_gb; total_gb = $disk.total_gb } }
  $commitDoc = [ordered]@{ used_pct = -1.0; measured = $false }
  $ramDoc = [ordered]@{ available_mb = -1.0; low_streak = 0; measured = $false }
  if ($null -ne $mem) {
    $commitDoc = [ordered]@{ used_pct = $mem.commit_used_pct; limit_mb = $mem.commit_limit_mb }
    $ramDoc = [ordered]@{ available_mb = $mem.available_mb; low_streak = $streak }
  }
  $health = [ordered]@{
    schema = 1; utc = $utc; disk = $diskDoc; commit = $commitDoc; ram = $ramDoc; unity = $unity
  }
  if ($null -ne $pagefile) { $health['pagefile'] = $pagefile }
  $health['alerts'] = $active.ToArray()
  if ($leaks.Count -gt 0) { $health['seam_env'] = $leaks.ToArray() }
  $health['by'] = 'hygiene_watchdog'

  $metrics = [ordered]@{ utc = $utc }
  if ($null -ne $disk) { $metrics['disk_free_gb'] = $disk.free_gb }
  if ($null -ne $mem) { $metrics['commit_pct'] = $mem.commit_used_pct; $metrics['ram_available_mb'] = $mem.available_mb }
  $metrics['unity_private_mb'] = $unity.private_mb
  $metrics['unity_working_set_mb'] = $unity.working_set_mb
  $metrics['unity_uptime_h'] = $unity.uptime_h
  $metrics['pagefile_mb'] = $pf

  $toasted = $false
  if (-not $Dry) {
    Write-GskJsonAtomic -Path $healthPath -InputObject $health
    Add-GskJsonl -Path (Get-GskHarnessPath $W @('metrics.jsonl')) -InputObject $metrics
    if ($news.Count -gt 0 -and -not $Quiet) {
      $toasted = Send-GskToast -Title 'Game Studio Kit watchdog' -Text (($news.ToArray()) -join "`n") -Now $now
    }
  }
  return [ordered]@{
    ok = $true; exit = 0; cmd = 'hygiene_watchdog'; dry_run = $Dry; workspace = $W
    raised = $raised.ToArray(); cleared = $cleared.ToArray(); toasted = [bool]$toasted; health = $health
  }
}

# The kit was uninstalled from W: do nothing (a leftover scheduled task must not keep writing into W).
function Get-KitMissingResult {
  param([string]$W)
  return [ordered]@{ ok = $true; exit = 0; cmd = 'hygiene_watchdog'; dry_run = [bool]$DryRun; workspace = $W; skipped = 'kit_missing'; reason = "$W has no Tools\kit\kit.json; nothing measured or written" }
}

try {
  $W = Resolve-GskWorkspace -Workspace $Workspace -ScriptDir $PSScriptRoot
  $quiet = [bool]$NoToast
  if ($Loop -and -not $Once -and -not $DryRun) {
    while ($true) {
      if (-not (Test-GskKitInstalled $W)) { Write-GskResult (Get-KitMissingResult $W); exit 0 }
      try { Write-GskResult (Invoke-WatchdogPass -W $W -Dry $false -Quiet $quiet) }
      catch { Write-GskResult ([ordered]@{ ok = $false; exit = 1; cmd = 'hygiene_watchdog'; error = $_.Exception.Message }) }
      Start-Sleep -Seconds $IntervalSec
    }
  }
  if (-not (Test-GskKitInstalled $W)) { Write-GskResult (Get-KitMissingResult $W); exit 0 }
  Write-GskResult (Invoke-WatchdogPass -W $W -Dry ([bool]$DryRun) -Quiet $quiet)
  exit 0
} catch {
  Write-GskResult ([ordered]@{ ok = $false; exit = 1; cmd = 'hygiene_watchdog'; error = $_.Exception.Message })
  exit 1
}
